using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TimeDilationController : MonoBehaviour
{
    [Header("UI (optional)")]
    public TMP_Text timeScaleLabel;      // shows "Time x0.10", etc.
    public Slider timeScaleSlider;       // 0..1 mapped to discrete steps below

    [Header("Settings")]
    [Range(0.0f, 1.0f)] public float defaultSlowMo = 0.1f;
    public float rampSeconds = 0.15f;    // smooth ramp to avoid jarring
    public KeyCode togglePauseKey = KeyCode.P;
    public KeyCode stepKey = KeyCode.O;
    public KeyCode slowMoKey = KeyCode.L;

    float _origFixedDelta;
    Coroutine _rampCo;
    bool _isPaused;

    // Common steps you can expose on buttons
    readonly float[] _steps = new float[] { 1f, 0.5f, 0.25f, 0.1f, 0.05f, 0.02f };

    void Awake()
    {
        _origFixedDelta = Time.fixedDeltaTime;
        UpdateLabel();
        if (timeScaleSlider)
        {
            timeScaleSlider.minValue = 0;
            timeScaleSlider.maxValue = _steps.Length - 1;
            timeScaleSlider.wholeNumbers = true;
            timeScaleSlider.value = 0; // index 0 => 1x
            timeScaleSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(togglePauseKey)) TogglePause();
        if (Input.GetKeyDown(stepKey)) StepOneFrame();
        if (Input.GetKeyDown(slowMoKey)) SetScale(defaultSlowMo);
    }

    // --- Public button hooks ---
    public void Pause()                   => SetPaused(true);
    public void Resume()                  => SetPaused(false);
    public void TogglePause()
    {
        SetPaused(!_isPaused);
    }

    public void SlowMoDefault()           => SetScale(defaultSlowMo);
    public void OneX()                    => SetScale(1f);
    public void HalfX()                   => SetScale(0.5f);
    public void QuarterX()                => SetScale(0.25f);
    public void TenthX()                  => SetScale(0.1f);
    public void TinyX()                   => SetScale(0.02f);

    public void StepOneFrame()
    {
        // Works in builds: release exactly one rendered frame while paused
        if (!_isPaused) SetPaused(true);
        StartCoroutine(StepFrameCo());
    }

    public void SetScale(float target)
    {
        _isPaused = target <= 0f;
        StartRamp(Mathf.Max(0f, target));
    }

    // --- Internals ---
    IEnumerator StepFrameCo()
    {
        // Temporarily unpause for one frame at very small timescale
        const float stepScale = 0.02f;
        float prevScale = Time.timeScale;

        Time.timeScale = stepScale;
        Time.fixedDeltaTime = _origFixedDelta * Time.timeScale;
        UpdateLabel();

        // Let a single frame render
        yield return new WaitForEndOfFrame();

        // Return to paused
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f; // no physics while paused
        UpdateLabel();
    }

    void SetPaused(bool paused)
    {
        _isPaused = paused;
        if (paused)
        {
            if (_rampCo != null) StopCoroutine(_rampCo);
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f; // stop physics
        }
        else
        {
            StartRamp(Mathf.Max(0.0001f, Time.timeScale == 0f ? defaultSlowMo : Time.timeScale));
        }
        UpdateLabel();
    }

    void StartRamp(float targetScale)
    {
        if (_rampCo != null) StopCoroutine(_rampCo);
        _rampCo = StartCoroutine(RampTo(targetScale));
    }

    IEnumerator RampTo(float target)
    {
        float start = Time.timeScale;
        float t = 0f;
        float dur = Mathf.Max(0.01f, rampSeconds);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;                 // ramp independent of timeScale
            Time.timeScale = Mathf.Lerp(start, target, t);
            Time.fixedDeltaTime = _origFixedDelta * Time.timeScale;
            UpdateLabel();
            yield return null;
        }
        Time.timeScale = target;
        Time.fixedDeltaTime = _origFixedDelta * Time.timeScale;
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (timeScaleLabel)
            timeScaleLabel.text = $"Time x{Time.timeScale:0.00}" + (_isPaused ? " (Paused)" : "");
    }

    void OnSliderChanged(float idx)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(idx), 0, _steps.Length - 1);
        SetScale(_steps[i]);
    }

    // Safety: always restore on quit/disable
    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _origFixedDelta;
    }
}
