using UnityEngine;

public class SwapCard : MonoBehaviour
{
    private int stepNum = 1; // Current step (1 to 4)
    public GameObject backButton; // Back button object
    public GameObject card1;
    public GameObject card2;
    public GameObject card3;
    public GameObject card4;

    void Start()
    {
        // Initialize cards and backButton
        UpdateUI();
    }

    void Update()
    {
        //// Ensure UI state is correct
        //UpdateUI();
    }

    // Move to the next step
    public void NextStep()
    {
        if (stepNum < 4)
        {
            stepNum++;
            UpdateUI();
        }
    }

    // Move to the previous step
    public void PreviousStep()
    {
        if (stepNum > 1)
        {
            stepNum--;
            UpdateUI();
        }
    }

    // Update the UI based on the current step
    private void UpdateUI()
    {
        // Activate/deactivate cards based on the current step
        card1.SetActive(stepNum == 1);
        card2.SetActive(stepNum == 2);
        card3.SetActive(stepNum == 3);
        card4.SetActive(stepNum == 4);

        // Activate backButton only for steps 2, 3, and 4
        backButton.SetActive(stepNum >= 2 && stepNum <= 4);
    }
}
