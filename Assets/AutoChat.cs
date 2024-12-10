using OpenAI.Samples.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoChat : MonoBehaviour
{
    //string autoMessage = "Hello! This is my vlog shooting. You are my assistant";
    string autoMessage = "Hello! Can you generate a cat image for me?";

    public TMP_InputField ChatField;

    void Start()
    {
        ChatField.text = autoMessage;
    }

    // Update is called once per frame
    void Update()
    {
        //ChatBehaviour.SubmitChat();
        ChatField.text = autoMessage;
    }
}
