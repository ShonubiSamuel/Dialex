using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using WebSocketSharp;

public class InputModeController : MonoBehaviour
{
    public Button actionButton;
    public Image buttonIcon;
    public Sprite micIcon;
    public Sprite sendIcon;
    public TMP_InputField inputField;
    
    private Action micAction;
    private Action textAction;
    private bool isTextMode = false;

    

    public void SetButtonCallbacks(Action micCallback, Action textCallback)
    {
        micAction = micCallback;
        textAction = () =>
        {
            textCallback?.Invoke();
            // After sending, reset to mic mode
            SwitchToMicMode();
        };
    }

    void Start()
    {
        actionButton.onClick.AddListener(OnActionButtonClicked);
        inputField.onSelect.AddListener(_ => SwitchToTextMode());
        inputField.onDeselect.AddListener(_ => SwitchToMicMode());
        SwitchToMicMode(); // Default state
    }

    void OnActionButtonClicked()
    {
        if (isTextMode && textAction != null)
        {
            textAction.Invoke();
            inputField.text = "";
        }
        else if (!isTextMode && micAction != null)
            micAction.Invoke();
    }

    void SwitchToTextMode()
    {
        isTextMode = true;
        buttonIcon.sprite = sendIcon;
    }
    
    public void SwitchToMicMode() // Expose in case others want to call it
    {
        if (inputField.text.IsNullOrEmpty())
        {
            isTextMode = false;
            buttonIcon.sprite = micIcon;
        }
        
    }
}