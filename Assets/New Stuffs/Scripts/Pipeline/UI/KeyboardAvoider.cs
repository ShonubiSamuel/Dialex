using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// Pushes a bottom-anchored panel up by the mobile keyboard height (Screen Space - Overlay).
[RequireComponent(typeof(RectTransform))]
public class KeyboardAvoider : MonoBehaviour
{
    public float extraPadding = 8f;   // px extra above keyboard
    public float lerpSpeed = 15f;     // smoothness

    public RectTransform rt;
    public Canvas rootCanvas;

    private float initialButton;
    private void Start()
    {
        initialButton = rt.offsetMin.y;
    }

    void Update()
    {
        if (!rootCanvas || rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return; // keep it simple; for Camera/World modes you'd convert via canvas scale/projection

        float targetBottom = initialButton;

#if UNITY_IOS || UNITY_ANDROID
        float kbUI = TouchScreenKeyboard.area.height / Mathf.Max(0.0001f, rootCanvas.scaleFactor);
        bool kbOpen = (kbUI > 1f) && AnyInputFocused();
        if (kbOpen)
        {
            // Keyboard area is in screen pixels; convert for Canvas scaleFactor
            float kb = TouchScreenKeyboard.area.height / Mathf.Max(0.0001f, rootCanvas.scaleFactor);
            targetBottom = kb + extraPadding;
            var off = rt.offsetMin;
            off.y = Mathf.Lerp(off.y, targetBottom, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));
            rt.offsetMin = off;
        }
        else
        {
             var off = rt.offsetMin;
             off.y = Mathf.Lerp(off.y, targetBottom, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));
           
            rt.offsetMin = off;
        }
#endif

    }
    
    bool AnyInputFocused()
    {
        if (!EventSystem.current) return false;
        var go = EventSystem.current.currentSelectedGameObject;
        if (!go) return false;
        return go.GetComponent<TMP_InputField>();
    }

}