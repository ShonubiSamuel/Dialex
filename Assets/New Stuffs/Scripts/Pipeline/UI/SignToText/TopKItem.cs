using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TopKItem : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI probText;
    public Image barFill; // Image.type = Filled, Fill Method = Horizontal

    public void SetRow(string label, float prob01)
    {
        if (labelText) labelText.text = label.ToUpperInvariant();
        if (probText)  probText.text  = prob01.ToString("0.00");
        if (barFill)   barFill.fillAmount = Mathf.Clamp01(prob01);
    }
}