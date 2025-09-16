using UnityEngine;
using UnityEngine.UI;

/// ChatGPT-like left-scrolling mic strip driven by MicrophoneCaptureController.CurrentLevel.
/// - No spectrum needed.
/// - Scrolls left 1px per frame and draws a symmetric bar on the right.
/// - heightScale enlarges the *drawing* only (keeps idle line thin).
[RequireComponent(typeof(RawImage))]
public class AudioVisualizer : MonoBehaviour
{
    [Header("Data Source")]
    public MicrophoneCaptureController mic;   // assign in Inspector

    [Header("Texture / Layout")]
    public int width = 900;
    public int height = 60;
    [Tooltip("Top & bottom padding in pixels inside the pill.")]
    public int verticalPadding = 6;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f); // dark
    public Color barColor = new Color(0.92f, 0.92f, 0.92f, 1f);        // light

    [Header("Dynamics")]
    [Tooltip("How fast the bar grows (per second).")]
    public float attack = 16f;
    [Tooltip("How fast the bar shrinks (per second).")]
    public float decay = 6f;
    [Tooltip("Bends perceived loudness; <1 boosts quiet parts, >1 compresses.")]
    public float responseGamma = 0.6f;

    [Header("Display (visual-only scale)")]
    [Tooltip("Scales wave height after normalization, without fattening the idle line.")]
    public float heightScale = 1.5f;

    [Header("Rate")]
    [Range(10,120)] public int fps = 30;

    // internals
    private RawImage _img;
    private Texture2D _tex;
    private Color32[] _buffer;
    private Color32 _bg32, _fg32;
    private float _interval, _nextTick;
    private float _smoothed; // 0..1 (normalized level after gamma)

    // ... inside AudioVisualizer

    [Header("Lifecycle")]
    public bool hideWhenIdle = false;   // optional: auto-hide when not visualizing

    private bool _active;               // are we drawing right now?


    public void SetUp()
    {
        _img = GetComponent<RawImage>();
        _img.color = Color.white; // ensure RawImage tint doesn't hide texture
        _interval = 1f / Mathf.Max(1, fps);
 
        
        if (mic != null)
        {
            mic.OnRecordStarted += () => StartVisualizing(true);
            mic.OnRecordStopped += () => StopVisualizing(true);
        }
        
        _bg32 = (Color32)backgroundColor;
        _fg32 = (Color32)barColor;

        _tex = new Texture2D(Mathf.Max(8, width), Mathf.Max(16, height), TextureFormat.RGBA32, false);
        _tex.wrapMode = TextureWrapMode.Clamp;
        _img.texture = _tex;

        _buffer = new Color32[_tex.width * _tex.height];
        for (int i = 0; i < _buffer.Length; i++) _buffer[i] = _bg32;
        _tex.SetPixels32(_buffer);
        _tex.Apply(false, false);

        _nextTick = Time.unscaledTime;
        StopVisualizing(true);
    }

    void OnDestroy()
    {
        if (_tex) Destroy(_tex);
    }
    
    
    

    void Update()
    {
        if (!_active) return;                     // <- only draw when started
        if (mic == null || !mic.IsRecording())    // safety: if mic isn’t recording, don’t update bars
            return;
        // hot-reload if any size/fps/color changed externally
        if (_tex == null || _tex.width != width || _tex.height != height)
        {
            ApplySettings(); // will resize, update interval, and recolor as needed
        }
        
        // Hot-apply fps changes (optional)
        float newInterval = 1f / Mathf.Max(1, fps);
        if (!Mathf.Approximately(newInterval, _interval)) _interval = newInterval;

        if (Time.unscaledTime < _nextTick) return;
        _nextTick += _interval;

        float level = 0f;
        // Use mic level only when recording; otherwise decay to zero gracefully
        if (mic != null && mic.IsRecording())
            level = Mathf.Clamp01(mic.CurrentLevel * 5f); // small boost so whispers show a bit

        // perceptual curve (gamma) after normalization (keeps idle line thin)
        float energy = Mathf.Pow(level, Mathf.Max(0.01f, responseGamma));

        // attack/decay smoothing
        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        float up = attack * dt;
        float down = decay * dt;
        _smoothed = (energy > _smoothed)
            ? Mathf.MoveTowards(_smoothed, energy, up)
            : Mathf.MoveTowards(_smoothed, energy, down);

        // convert to pixels (centered, symmetric), then apply visual-only heightScale
        int pad = Mathf.Clamp(verticalPadding, 0, _tex.height / 3);
        int drawable = Mathf.Max(1, _tex.height - 2 * pad);
        int half = drawable / 2;
        int halfPixels = Mathf.Clamp(Mathf.RoundToInt(_smoothed * half * Mathf.Max(0.01f, heightScale)), 0, half);

        // scroll and draw
        ScrollLeftOnePixelInBuffer();
        DrawRightColumnInBuffer(pad, half, halfPixels);

        _tex.SetPixels32(_buffer);
        _tex.Apply(false, false);
    }

    // Call this when mic starts (record button)
    public void StartVisualizing(bool clear = true)
    {
        _active = true;
        if (hideWhenIdle)
        {
            gameObject.SetActive(true);
        }
        if (clear) ClearTexture();
        _smoothed = 0f;
        _nextTick = Time.unscaledTime;
    }

// Call this when mic stops (cancel/confirm)
    public void StopVisualizing(bool clear = true)
    {
        _active = false;
        if (clear) ClearTexture();
        if (hideWhenIdle)
        {
            gameObject.SetActive(false);
        }
    }
    

    private void ClearTexture()
    {
        if (_buffer == null || _tex == null) return;
        for (int i = 0; i < _buffer.Length; i++) _buffer[i] = _bg32;
        _tex.SetPixels32(_buffer);
        _tex.Apply(false, false);
    }

    void ScrollLeftOnePixelInBuffer()
    {
        int w = _tex.width;
        int h = _tex.height;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w - 1; x++)
                _buffer[row + x] = _buffer[row + x + 1];
            _buffer[row + (w - 1)] = _bg32; // clear rightmost col
        }
    }

    void DrawRightColumnInBuffer(int pad, int half, int halfPixels)
    {
        int w = _tex.width;
        int h = _tex.height;
        int x = w - 1;
        int centerY = pad + half;

        int y0 = Mathf.Clamp(centerY - halfPixels, 0, h - 1);
        int y1 = Mathf.Clamp(centerY + halfPixels, 0, h - 1);

        for (int y = y0; y <= y1; y++)
            _buffer[y * w + x] = _fg32;
    }

    // Optional: call from UI if you change colors/size at runtime
    public void ApplySettings(bool clear = true)
    {
        _bg32 = (Color32)backgroundColor;
        _fg32 = (Color32)barColor;
        _interval = 1f / Mathf.Max(1, fps);

        if (_tex == null || _tex.width != width || _tex.height != height)
        {
            if (_tex) Destroy(_tex);
            _tex = new Texture2D(Mathf.Max(8, width), Mathf.Max(16, height), TextureFormat.RGBA32, false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _img.texture = _tex;
            _buffer = new Color32[_tex.width * _tex.height];
            clear = true;
        }

        if (clear)
        {
            for (int i = 0; i < _buffer.Length; i++) _buffer[i] = _bg32;
            _tex.SetPixels32(_buffer);
            _tex.Apply(false, false);
        }
    }
}
