using TMPro;
using UnityEngine;

public class TextField : MonoBehaviour
{
    public TMP_InputField inputField;
    public GlossPipelineManager glossManager;

    public void OnConvertClicked()
    {
        string inputSentence = inputField.text;
        if (!string.IsNullOrEmpty(inputSentence))
        {
            glossManager.Text(inputSentence);
            inputField.text = ""; // Optional clear
        }
    }

}