using UnityEngine;
using TMPro;   // for TextMeshPro
using UnityEngine.UI; // for Button

public class ThankYouDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI messageText; // assign in Inspector
    public Button myButton;             // assign in Inspector

    void Start()
    {
        if (myButton != null)
        {
            // Attach the listener
            myButton.onClick.AddListener(DisplayThankYou);
        }
    }

    void DisplayThankYou()
    {
        if (messageText != null)
        {
            messageText.text = "Thank you";
        }
    }
}