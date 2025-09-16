using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Simple mic recorder that produces an AudioClip and sends it to SignPipelineController.
/// - Device selection (optional)
/// - Push-to-talk or button control (manual stop only)
/// - Trims clip on stop based on current mic position
/// - Optional resample to 16 kHz mono
/// - Level meter (read-only) for UI feedback
/// </summary>
public class MicrophoneCaptureController : MonoBehaviour
{
    [Header("Pipeline Target")]
    public SignPipelineController pipeline;
    

    [Header("Device")]
    public string deviceName = "";               // empty = default device
    public bool autoSelectDefault = true;
    public TMP_Dropdown deviceDropdown;

    [Header("Capture Settings")]
    [Tooltip("Ring buffer length in seconds (must be > 0). We trim to actual length on stop.")]
    [Range(1, 300)] public int maxLengthSec = 60;
    [Tooltip("Requested sample rate. Many STT work best at 16k or 16–24k.")]
    public int sampleRate = 16000;
    [Tooltip("Force final clip to 16k mono (good for STT). If off, we pass the raw mic rate/channels.")]
    public bool resampleTo16kMono = true;

    [Header("Control")]
    public bool pushToTalk = false;
    public KeyCode pttKey = KeyCode.Space;

    [Header("Validation")]
    [Tooltip("Don’t accept clips shorter than this.")]
    [Range(0.1f, 3f)] public float minRecordDuration = 0.4f;

    [Header("Level Meter (read-only)")]
    [SerializeField, Range(0f,1f)] private float currentLevel;
    public float CurrentLevel => currentLevel;

    // Runtime
    private AudioClip _recording;
    private int _micChannels = 1;
    private bool _isRecording;

    // Buffer for level reading
    private float[] _levelBuf = new float[1024];
    
    public System.Action OnRecordStarted;
    public System.Action OnRecordStopped;

    // -------------------- Unity --------------------

    private void Start()
    {
        if (autoSelectDefault && string.IsNullOrEmpty(deviceName))
        {
            var devs = Microphone.devices;
            if (devs != null && devs.Length > 0) deviceName = devs[0];
        }

        BuildDeviceDropdown();
    }

    private void Update()
    {
        // if (pushToTalk)
        // {
        //     if (Input.GetKeyDown(pttKey) && !_isRecording) StartCapture();
        //     if (Input.GetKeyUp(pttKey) && _isRecording) StopAndSendToPipeline();
        // }

        if (_isRecording && _recording != null)
        {
            // Level only (no auto-stop)
            currentLevel = ComputeRms(_recording, _levelBuf, deviceName);
        }
        else
        {
            currentLevel = 0f;
        }
    }

    // -------------------- Public API --------------------
    
    public void StartCapture()
    {
        if (_isRecording) return;

        // Looping ring buffer prevents auto-stop.
        _recording = Microphone.Start(deviceName, /*loop*/ true, maxLengthSec, sampleRate);
        if (_recording == null)
        {
            Debug.LogError("[Mic] Failed to start microphone. Check permissions and device selection.");
            return;
        }

        _micChannels = 1; // assume 1; confirm on trim
        _isRecording = true;
        OnRecordStarted?.Invoke();

        Debug.Log($"[Mic] Recording... device='{deviceName}' rate={sampleRate}Hz loop=true len={maxLengthSec}s");
    }

    public void StopCapture()
    {
        if (!_isRecording) return;
        try { Microphone.End(deviceName); } catch {}
        _isRecording = false;
        print("StopCapture");
        OnRecordStopped?.Invoke();     
    }

    /// <summary>
    /// Manual finalize: trims to current mic position, optional resample to 16k mono, sends to pipeline.
    /// </summary>
    public void StopAndSendToPipeline()
    {
        if (!_isRecording) return;

        int pos = SafeGetMicPosition(deviceName);
        StopCapture();

        if (_recording == null || pos <= 0)
        {
            Debug.LogWarning("[Mic] No samples captured.");
            return;
        }

        // Trim to actual length
        var trimmed = TrimClip(_recording, pos, out int channels, out int hz);
        _micChannels = channels;

        if (trimmed.length < minRecordDuration)
        {
            Debug.LogWarning($"[Mic] Too short ({trimmed.length:0.00}s). Discarded.");
            Destroy(trimmed);
            return;
        }

        AudioClip finalClip = trimmed;

        // Optional: downmix to mono + resample to 16k
        if (resampleTo16kMono)
        {
            finalClip = DownmixAndResample(trimmed, 16000);
            Destroy(trimmed); // free the intermediate
        }

        Debug.Log($"[Mic] Final clip: {finalClip.frequency} Hz, {finalClip.channels} ch, {finalClip.length:0.00}s");

        if (pipeline != null)
        {
            pipeline.SubmitAudio(finalClip);
        }
        else
        {
            Debug.LogWarning("[Mic] No SignPipelineController assigned. Clip created but not sent.");
        }
    }
    
    // Add this to MicrophoneCaptureController (near StopAndSendToPipeline)
    public AudioClip StopAndGetFinalClip()
    {
        if (!_isRecording) return null;

        int pos = SafeGetMicPosition(deviceName);
        StopCapture();

        if (_recording == null || pos <= 0)
        {
            Debug.LogWarning("[Mic] No samples captured.");
            return null;
        }

        // Trim to actual length
        var trimmed = TrimClip(_recording, pos, out int channels, out int hz);
        _micChannels = channels;

        if (trimmed.length < minRecordDuration)
        {
            Debug.LogWarning($"[Mic] Too short ({trimmed.length:0.00}s). Discarded.");
            Destroy(trimmed);
            return null;
        }

        AudioClip finalClip = trimmed;

        // Optional: downmix to mono + resample to 16k
        if (resampleTo16kMono)
        {
            finalClip = DownmixAndResample(trimmed, 16000);
            Destroy(trimmed); // free the intermediate
        }

        Debug.Log($"[Mic] Final clip (return): {finalClip.frequency} Hz, {finalClip.channels} ch, {finalClip.length:0.00}s");
        return finalClip;
    }


    public bool IsRecording() => _isRecording;

    public void SelectDeviceByIndex(int index)
    {
        var devs = Microphone.devices;
        if (devs == null || devs.Length == 0) return;
        deviceName = Mathf.Clamp(index, 0, devs.Length - 1) >= 0 ? devs[index] : "";
        Debug.Log($"[Mic] Selected device: {deviceName}");
    }

    // -------------------- UI helpers --------------------

    private void BuildDeviceDropdown()
    {
        if (deviceDropdown == null) return;
        var devs = Microphone.devices ?? Array.Empty<string>();

        deviceDropdown.ClearOptions();
        deviceDropdown.AddOptions(new List<string>(devs));
        deviceDropdown.onValueChanged.RemoveAllListeners();
        deviceDropdown.onValueChanged.AddListener(SelectDeviceByIndex);

        int idx = Array.IndexOf(devs, deviceName);
        if (idx < 0) idx = 0;
        if (devs.Length > 0) deviceDropdown.value = idx;
    }

    // -------------------- Audio utils --------------------

    private static int SafeGetMicPosition(string deviceName)
    {
        int pos = 0;
        try { pos = Microphone.GetPosition(deviceName); } catch { /* ignore */ }
        return pos;
    }

    private static float ComputeRms(AudioClip clip, float[] buf, string deviceName)
    {
        if (clip == null) return 0f;

        int count = Mathf.Min(buf.Length, clip.samples);
        int pos = SafeGetMicPosition(deviceName);
        if (pos <= 0) return 0f;

        int start = pos - count;
        if (start < 0) start = 0;

        clip.GetData(buf, start);

        double sum = 0;
        for (int i = 0; i < count; i++) sum += buf[i] * buf[i];
        return count > 0 ? Mathf.Sqrt((float)(sum / count)) : 0f;
    }

    private static AudioClip TrimClip(AudioClip src, int endPositionSamples, out int channels, out int hz)
    {
        channels = src.channels;
        hz = src.frequency;
        endPositionSamples = Mathf.Clamp(endPositionSamples, 0, src.samples);

        var data = new float[endPositionSamples * channels];
        src.GetData(data, 0);

        var dst = AudioClip.Create("Mic_Trimmed", endPositionSamples, channels, hz, false);
        dst.SetData(data, 0);
        return dst;
    }

    /// <summary>
    /// Downmix to mono and resample by linear interpolation to targetHz.
    /// </summary>
    private static AudioClip DownmixAndResample(AudioClip src, int targetHz)
    {
        int srcHz = src.frequency;
        int srcCh = src.channels;
        int srcSamples = src.samples;

        // Pull all samples
        var srcData = new float[srcSamples * srcCh];
        src.GetData(srcData, 0);

        // Downmix to mono
        var mono = new float[srcSamples];
        if (srcCh == 1)
        {
            Array.Copy(srcData, mono, mono.Length);
        }
        else
        {
            for (int i = 0; i < srcSamples; i++)
            {
                double sum = 0;
                for (int c = 0; c < srcCh; c++) sum += srcData[i * srcCh + c];
                mono[i] = (float)(sum / srcCh);
            }
        }

        if (srcHz == targetHz)
        {
            var same = AudioClip.Create("Mic_Mono", mono.Length, 1, targetHz, false);
            same.SetData(mono, 0);
            return same;
        }

        // Resample (linear)
        double ratio = (double)targetHz / srcHz;
        int dstSamples = Math.Max(1, (int)Math.Round(mono.Length * ratio));
        var dst = new float[dstSamples];

        for (int i = 0; i < dstSamples; i++)
        {
            double srcPos = i / ratio;
            int i0 = (int)Math.Floor(srcPos);
            int i1 = Mathf.Min(i0 + 1, mono.Length - 1);
            float t = (float)(srcPos - i0);
            dst[i] = Mathf.Lerp(mono[i0], mono[i1], t);
        }

        var clip = AudioClip.Create("Mic_16kMono", dstSamples, 1, targetHz, false);
        clip.SetData(dst, 0);
        return clip;
    }
}
