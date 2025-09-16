using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_InputField))]
public class AutoGrowInputHeightV2 : MonoBehaviour
{
    [Header("Size")]
    public float minHeight = 56f;
    public float maxHeight = 220f;
    [Tooltip("Extra pixels added above+below the text.")]
    public float verticalPadding = 16f;

    [Header("Apply Mode")]
    [Tooltip("If true and a LayoutElement is present, set preferredHeight. Otherwise set RectTransform height directly.")]
    public bool preferLayoutElement = true;

    private TMP_InputField _input;
    private TMP_Text _text;
    private TMP_Text _placeholder;
    private LayoutElement _layout;       // optional
    private RectTransform _rt;           // this input's rect
    private RectTransform _viewport;     // TMP's text viewport (Text Area)

    void Awake()
    {
        _input = GetComponent<TMP_InputField>();
        _text = _input.textComponent;
        _placeholder = _input.placeholder as TMP_Text;
        _layout = GetComponent<LayoutElement>();
        _rt = (RectTransform)transform;

        // TMP exposes the viewport; if null fallback to parent of text
        _viewport = _input.textViewport != null
            ? _input.textViewport
            : (_text != null ? _text.rectTransform.parent as RectTransform : null);

        // ensure multi-line typing
        _input.lineType = TMP_InputField.LineType.MultiLineNewline;

        // Make sure word wrap is on so preferredHeight reflects line breaks
        if (_text) _text.enableWordWrapping = true;

        _input.onValueChanged.AddListener(_ => Refresh());
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnRectTransformDimensionsChange()
    {
        // If width changes (e.g., device rotation), recompute height
        Refresh();
    }

    /// <summary>Call after you programmatically set inputField.text.</summary>
    public void Refresh()
    {
        if (_text == null) return;

        float availableWidth = _viewport != null
            ? _viewport.rect.width
            : _rt.rect.width;

        // Important: use GetPreferredValues with a known width so TMP wraps correctly.
        Vector2 pref = _text.GetPreferredValues(_input.text, availableWidth, Mathf.Infinity);
        float contentH = pref.y;

        // Consider placeholder when empty
        if (string.IsNullOrEmpty(_input.text) && _placeholder != null)
        {
            Vector2 prefPH = _placeholder.GetPreferredValues(_placeholder.text, availableWidth, Mathf.Infinity);
            contentH = Mathf.Max(contentH, prefPH.y);
        }

        float target = Mathf.Clamp(contentH + verticalPadding, minHeight, maxHeight);

        if (preferLayoutElement && _layout != null)
        {
            _layout.preferredHeight = target;
            // Nudge layout to rebuild up the chain
            var parent = _rt.parent as RectTransform;
            if (parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
        else
        {
            // Directly set height when no Layout Group controls this rect
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, target);
        }
    }
}
