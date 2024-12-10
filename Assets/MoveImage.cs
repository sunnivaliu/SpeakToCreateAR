using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class MoveImage : MonoBehaviour
{
    public GameObject contentParent;
    public RectTransform parentDisplay;
    public GameObject rightHand;
    public GameObject imageCanvasTemplate;
    public GameObject imageHandleTemplate;

    private int previousChildCount = 0;
    private List<string> imageChildren = new List<string>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }


    // Update is called once per frame
    void Update()
    {

        if (contentParent != null)
        {
            if (contentParent.transform.childCount > previousChildCount)
            {
                CheckAllChildNames(contentParent.transform);
                previousChildCount = contentParent.transform.childCount;
            }
        }

    }



    private void CheckAllChildNames(Transform OriginalParent)
    {
        foreach (Transform child in OriginalParent)
        {
            if (child.name.Contains("Image") && !imageChildren.Contains(child.name))
            {
                Debug.Log("Creating Child Name: " + child.name);

                //// Duplicate image
                //RawImage childRawImage = child.GetComponent<RawImage>();
                //GameObject duplicateCanva = Instantiate(imageCanvasTemplate);
                //Transform duplicateChild = duplicateCanva.transform.Find("ImageAff"); // Replace with the actual child name
                //RawImage duplicateRawImage = duplicateChild.GetComponentInChildren<RawImage>();
                //duplicateRawImage.texture = childRawImage.texture;
                //duplicateCanva.transform.position = rightHand.transform.position;
                //imageChildren.Add(child.name);

                //// Duplicate handle
                //GameObject duplicateHandle = Instantiate(imageHandleTemplate);
                //Transform duplicateHandleChild = duplicateHandle.transform.Find("TableOffset"); // Replace with the actual child name
                //TransformSync transformSyncComponent = duplicateHandleChild.GetComponent<TransformSync>();
                //Vector3 duplicateHandlePos = new Vector3(duplicateCanva.transform.position.x, duplicateCanva.transform.position.y - 0.2f, duplicateCanva.transform.position.z);
                //duplicateHandle.transform.position = duplicateHandlePos;
                //Debug.Log("duplicateHandle.transform.position: " + duplicateHandle.transform.position);

                //2D Flat UI Object
                GameObject imageObject = new GameObject($"{parentDisplay.childCount + 1}_Image");
                RawImage childRawImage = child.GetComponent<RawImage>();
                var rawImage = imageObject.AddComponent<RawImage>();
                rawImage.texture = childRawImage.texture;
                imageObject.transform.SetParent(parentDisplay, false);
                imageChildren.Add(child.name);

                // Add the XRPokeFollowAffordance component
                imageObject.AddComponent<XRPokeFollowAffordance>();
                // Add the Button Component
                imageObject.AddComponent<Button>();
                Button imageObjectButton = imageObject.GetComponent<Button>();
                imageObjectButton.onClick.AddListener(() => DuplicatingBehavior(imageObject));
                ColorBlock colors_assigned = imageObjectButton.colors;
                colors_assigned.normalColor = Color.white; // Default color when not interacted with
                colors_assigned.highlightedColor = Color.grey; // Color when hovered over
                colors_assigned.selectedColor = Color.cyan; // Color when hovered over
                imageObjectButton.colors = colors_assigned;

                var layoutElement = imageObject.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = rawImage.texture.height / 4f;
                layoutElement.preferredWidth = rawImage.texture.width / 4f;
                var aspectRatioFitter = imageObject.AddComponent<AspectRatioFitter>();
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                aspectRatioFitter.aspectRatio = rawImage.texture.width / (float)rawImage.texture.height;


            }
        }

    }

    private void DuplicatingBehavior(GameObject Object)
    {
        Debug.Log("duplicateImage Pressed!");
        GameObject imageDragParent = new GameObject($"{Object.name}_DraggedimageDragParent");
        GameObject imageDrag = new GameObject($"{Object.name}_Dragged");
        imageDrag.transform.position = Vector3.zero;
        string imageDragBaseName = imageDrag.name;
        imageDrag.name = GetUniqueName(imageDragBaseName, imageDragParent.transform);

        RawImage childRawImage = Object.GetComponent<RawImage>();
        var rawImage = imageDrag.AddComponent<RawImage>();
        rawImage.texture = childRawImage.texture;
        imageDragParent.transform.SetParent(imageCanvasTemplate.transform, false);
        imageDrag.transform.SetParent(imageDragParent.transform, false);

        // Add the XRPokeFollowAffordance component
        imageDrag.AddComponent<XRPokeFollowAffordance>();
        // Add button
        imageDrag.AddComponent<Button>();
        Button imageDragButton = imageDrag.GetComponent<Button>();
        ColorBlock colors_assigned = imageDragButton.colors;
        colors_assigned.normalColor = Color.white; // Default color when not interacted with
        colors_assigned.highlightedColor = Color.grey; // Color when hovered over
        colors_assigned.selectedColor = Color.cyan; // Color when hovered over
        imageDragButton.colors = colors_assigned;
        imageDragButton.onClick.AddListener(() => RecordAgain(imageDrag));


        GameObject duplicateHandle = Instantiate(imageHandleTemplate);
        string baseHandleName = imageDrag.name + "_Handle";
        duplicateHandle.name = GetUniqueName(baseHandleName, imageDragParent.transform);
        // Ensure the handle is set to the same coordinate space as imageDrag
        duplicateHandle.transform.SetParent(imageDragParent.transform, false);
        Vector3 duplicateHandlePos = new Vector3(imageDrag.transform.position.x, imageDrag.transform.position.y - 0.15f, imageDrag.transform.position.z); // duplicateHandle.transform.position = imageDrag.transform.position; // Align positions in world space
        duplicateHandle.transform.position = duplicateHandlePos;
        Transform duplicateHandleChild = duplicateHandle.transform.Find("TableOffset");
        TransformSync transformSyncComponent = duplicateHandleChild.GetComponent<TransformSync>();
        transformSyncComponent.m_TargetTransform = imageDrag.transform;

        imageDragParent.transform.position = rightHand.transform.position;
        Debug.Log(imageDragParent.transform.position + imageDrag.transform.position + duplicateHandle.transform.position);

    }

    private void RecordAgain(GameObject Object)
    {
        Debug.Log("Click RecordAgain" + Object.name);
    }

    private string GetUniqueName(string baseName, Transform parent)
    {
        int counter = 1;
        string uniqueName = baseName;

        // Check all children of the parent to avoid name conflicts
        while (parent.Find(uniqueName) != null)
        {
            uniqueName = $"{baseName}_{counter}";
            counter++;
        }

        return uniqueName;
    }



}