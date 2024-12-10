// Licensed under the MIT License. See LICENSE in the project root for license information.

using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utilities.Async;
using Utilities.Audio;
using Utilities.Encoding.Wav;
using Utilities.Extensions;
using Utilities.WebRequestRest;

namespace OpenAI.Samples.Chat
{
    public class ChatBehaviour : MonoBehaviour
    {
        [SerializeField]
        private OpenAIConfiguration configuration;

        [SerializeField]
        private bool enableDebug;

        [SerializeField]
        private Button submitButton;

        [SerializeField]
        private Button recordButton;

        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private GameObject submitButtonObject;

        [SerializeField]
        private GameObject inputFieldObject;

        [SerializeField]
        private GameObject AwaitVisuals;

        [SerializeField]
        private RectTransform contentArea;

        [SerializeField]
        private ScrollRect scrollView;

        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private SpeechVoice voice;

        [SerializeField]
        [TextArea(3, 10)]
        //private string systemPrompt = "You are a helpful assistant.\n- If an image is requested then use \"![Image](output.jpg)\" to display it.\n- When performing function calls, use the defaults unless explicitly told to use a specific value.\n- Images should always be generated in base64.";
        //private string systemPrompt = "You are a helpful assistant that is having a talkative chat with me.\r\n- If an image is requested, use \"![Image](output.jpg)\" to reference it.\r\n- Always generate images in base64 format and return the string.\r\n- Ensure the response includes a \"base64_image\" field if generating an image.\r\n- If unable to generate an image, explain why.\r\n";
        private string systemPrompt = "You are a helpful assistant.\n\n- If an image is requested, generate it in PNG format. \n- When performing function calls, use the defaults unless explicitly told to use a specific value. \n- Return the image as a base64-encoded string with the PNG format. Do not add extra text.\n- Ensure the response contains only the base64-encoded image data so that it can be directly decoded and used by the application.";

        private OpenAIClient openAI;

        private readonly Conversation conversation = new();

        private readonly List<Tool> assistantTools = new();

#if !UNITY_2022_3_OR_NEWER
        private readonly CancellationTokenSource lifetimeCts = new();
        private CancellationToken destroyCancellationToken => lifetimeCts.Token;
#endif

        private void OnValidate()
        {
            inputField.Validate();
            contentArea.Validate();
            submitButton.Validate();
            recordButton.Validate();
            audioSource.Validate();
        }

        private void Awake()
        {
            OnValidate();
            openAI = new OpenAIClient(configuration)
            {
                EnableDebug = enableDebug
            };
            assistantTools.Add(Tool.GetOrCreateTool(openAI.ImagesEndPoint, nameof(ImagesEndpoint.GenerateImageAsync)));
            conversation.AppendMessage(new Message(Role.System, systemPrompt));
            inputField.onSubmit.AddListener(SubmitChat);
            submitButton.onClick.AddListener(SubmitChat);
            recordButton.onClick.AddListener(ToggleRecording);
        }


#if !UNITY_2022_3_OR_NEWER
        private void OnDestroy()
        {
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }
#endif

        private void SubmitChat(string _) => SubmitChat();

        private static bool isChatPending;

        private async void SubmitChat()
        {
            if (isChatPending || string.IsNullOrWhiteSpace(inputField.text)) { return; }
            isChatPending = true;

            inputField.ReleaseSelection();
            inputField.interactable = false;
            submitButton.interactable = false;
            conversation.AppendMessage(new Message(Role.User, inputField.text));
            var userMessageContent = AddNewTextMessageContent(Role.User);
            userMessageContent.text = $"User: {inputField.text}";
            inputField.text = string.Empty;
            var assistantMessageContent = AddNewTextMessageContent(Role.Assistant);
            assistantMessageContent.text = "Assistant: ";
            //Audio chat
            //audioInputField.text = string.Empty;

            try
            {
                var request = new ChatRequest(conversation.Messages, tools: assistantTools);
                var response = await openAI.ChatEndpoint.StreamCompletionAsync(request, resultHandler: deltaResponse =>
                {
                    if (deltaResponse?.FirstChoice?.Delta == null) { return; }
                    assistantMessageContent.text += deltaResponse.FirstChoice.Delta.ToString();
                    scrollView.verticalNormalizedPosition = 0f;
                }, cancellationToken: destroyCancellationToken);

                conversation.AppendMessage(response.FirstChoice.Message);

                if (response.FirstChoice.FinishReason == "tool_calls")
                {
                    response = await ProcessToolCallsAsync(response);
                    assistantMessageContent.text += response.ToString().Replace("![Image](output.jpg)", string.Empty);
                }

                await GenerateSpeechAsync(response, destroyCancellationToken);
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case TaskCanceledException:
                    case OperationCanceledException:
                        break;
                    default:
                        Debug.LogError(e);
                        break;
                }
            }
            finally
            {
                if (destroyCancellationToken is { IsCancellationRequested: false })
                {
                    inputField.interactable = true;
                    EventSystem.current.SetSelectedGameObject(inputField.gameObject);
                    submitButton.interactable = true;
                }

                isChatPending = false;
            }

            async Task<ChatResponse> ProcessToolCallsAsync(ChatResponse response)
            {
                var toolCalls = new List<Task>();

                foreach (var toolCall in response.FirstChoice.Message.ToolCalls)
                {
                    if (enableDebug)
                    {
                        Debug.Log($"{response.FirstChoice.Message.Role}: {toolCall.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
                        Debug.Log($"{toolCall.Function.Arguments}");
                    }

                    toolCalls.Add(ProcessToolCall());

                    async Task ProcessToolCall()
                    {
                        await Awaiters.UnityMainThread;
                        AwaitVisuals.SetActive(true);
                        try
                        {
                            var imageResults = await toolCall.InvokeFunctionAsync<IReadOnlyList<ImageResult>>().ConfigureAwait(true);
                            Debug.Log("Getting Image" + imageResults);
                            foreach (var imageResult in imageResults)
                            {
                                AddNewImageContent(imageResult);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e);
                            conversation.AppendMessage(new(toolCall, $"{{\"result\":\"{e.Message}\"}}"));
                            return;
                        }

                        conversation.AppendMessage(new(toolCall, "{\"result\":\"completed\"}"));
                        AwaitVisuals.SetActive(false);

                    }
                }


                await Task.WhenAll(toolCalls).ConfigureAwait(true);
                ChatResponse toolCallResponse;

                try
                {
                    var toolCallRequest = new ChatRequest(conversation.Messages, tools: assistantTools);
                    toolCallResponse = await openAI.ChatEndpoint.GetCompletionAsync(toolCallRequest);
                    conversation.AppendMessage(toolCallResponse.FirstChoice.Message);
                }
                catch (RestException restEx)
                {
                    Debug.LogError(restEx);

                    foreach (var toolCall in response.FirstChoice.Message.ToolCalls)
                    {
                        conversation.AppendMessage(new Message(toolCall, restEx.Response.Body));
                    }

                    var toolCallRequest = new ChatRequest(conversation.Messages, tools: assistantTools);
                    toolCallResponse = await openAI.ChatEndpoint.GetCompletionAsync(toolCallRequest);
                    conversation.AppendMessage(toolCallResponse.FirstChoice.Message);
                }

                if (toolCallResponse.FirstChoice.FinishReason == "tool_calls")
                {
                    return await ProcessToolCallsAsync(toolCallResponse);
                }

                return toolCallResponse;
            }
        }

        private async Task GenerateSpeechAsync(string text, CancellationToken cancellationToken)
        {
            text = text.Replace("![Image](output.jpg)", string.Empty);
            if (string.IsNullOrWhiteSpace(text)) { return; }
            var request = new SpeechRequest(text, Model.TTS_1, voice, SpeechResponseFormat.PCM);
            var streamClipQueue = new Queue<AudioClip>();
            var streamTcs = new TaskCompletionSource<bool>();
            var audioPlaybackTask = PlayStreamQueueAsync(streamTcs.Task);
            var (clipPath, fullClip) = await openAI.AudioEndpoint.CreateSpeechStreamAsync(request, clip => streamClipQueue.Enqueue(clip), destroyCancellationToken);
            streamTcs.SetResult(true);

            if (enableDebug)
            {
                Debug.Log(clipPath);
            }

            await audioPlaybackTask;
            audioSource.clip = fullClip;

            async Task PlayStreamQueueAsync(Task streamTask)
            {
                try
                {
                    await new WaitUntil(() => streamClipQueue.Count > 0);
                    var endOfFrame = new WaitForEndOfFrame();

                    do
                    {
                        if (!audioSource.isPlaying &&
                            streamClipQueue.TryDequeue(out var clip))
                        {
                            if (enableDebug)
                            {
                                Debug.Log($"playing partial clip: {clip.name} | ({streamClipQueue.Count} remaining)");
                            }

                            audioSource.PlayOneShot(clip);
                            // ReSharper disable once MethodSupportsCancellation
                            await Task.Delay(TimeSpan.FromSeconds(clip.length)).ConfigureAwait(true);
                        }
                        else
                        {
                            await endOfFrame;
                        }

                        if (streamTask.IsCompleted && !audioSource.isPlaying && streamClipQueue.Count == 0)
                        {
                            return;
                        }
                    } while (!cancellationToken.IsCancellationRequested);
                }
                catch (Exception e)
                {
                    switch (e)
                    {
                        case TaskCanceledException:
                        case OperationCanceledException:
                            break;
                        default:
                            Debug.LogError(e);
                            break;
                    }
                }
            }
        }

        private TextMeshProUGUI AddNewTextMessageContent(Role role)
        {
            var textObject = new GameObject($"{contentArea.childCount + 1}_{role}");
            textObject.transform.SetParent(contentArea, false);
            var textMesh = textObject.AddComponent<TextMeshProUGUI>();
            textMesh.fontSize = 24;
#if UNITY_2023_1_OR_NEWER
            textMesh.textWrappingMode = TextWrappingModes.Normal;
#else
            textMesh.enableWordWrapping = true;
#endif
            return textMesh;
        }

        private void AddNewImageContent(Texture2D texture)
        {
            var imageObject = new GameObject($"{contentArea.childCount + 1}_Image");
            imageObject.transform.SetParent(contentArea, false);
            var rawImage = imageObject.AddComponent<RawImage>();
            rawImage.texture = texture;
            var layoutElement = imageObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = texture.height / 4f;
            layoutElement.preferredWidth = texture.width / 4f;
            var aspectRatioFitter = imageObject.AddComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            aspectRatioFitter.aspectRatio = texture.width / (float)texture.height;
        }

        private void ToggleRecording()
        {
            RecordingManager.EnableDebug = enableDebug;

            if (RecordingManager.IsRecording)
            {
                submitButtonObject.SetActive(true); // ADDED
                inputFieldObject.SetActive(true); // ADDED
                RecordingManager.EndRecording();
                Debug.Log("Enable buttons record");
            }
            else
            {
                inputField.interactable = false;
                RecordingManager.StartRecording<WavEncoder>(callback: ProcessRecording);
                submitButtonObject.SetActive(false); // ADDED
                inputFieldObject.SetActive(false); // ADDED
                Debug.Log("Disable buttons record");
            }
        }

        private async void ProcessRecording(Tuple<string, AudioClip> recording)
        {
            var (path, clip) = recording;

            if (enableDebug)
            {
                Debug.Log(path);
            }

            try
            {
                recordButton.interactable = false;
                var request = new AudioTranscriptionRequest(clip, temperature: 0.1f, language: "en");
                var userInput = await openAI.AudioEndpoint.CreateTranscriptionTextAsync(request, destroyCancellationToken);

                if (enableDebug)
                {
                    Debug.Log(userInput);
                }

                inputField.text = userInput;
                //audioInputField.text = userInput;
                SubmitChat();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                inputField.interactable = true;
            }
            finally
            {
                recordButton.interactable = true;
            }
        }
    }
}
