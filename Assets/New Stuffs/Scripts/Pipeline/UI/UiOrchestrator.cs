using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using YourApp.Signs.Pipeline.InputLayer; // TextInputController
using YourApp.Signs.Pipeline.Input;       // AudioFilePicker (your namespace)

public class UiOrchestrator : MonoBehaviour
{
    public enum UiState { Idle, Recording, TextReady, Busy }

    [Header("Core")]
    public SignPipelineController pipeline;
    public TextInputController textInput;               // adapter (TMP wrapper)
    public MicrophoneCaptureController mic;
    public AudioFilePicker audioFilePicker;             // optional (upload)
    public AudioVisualizer audioVisualizer;             // optional (visuals)
    public DemoPhraseRouter demoRouter;

    [Header("UI")]
    public Button recordButton;
    public Button cancelButton;
    public Button confirmButton;
    public Button textSubmitButton;
    public Button uploadButton;

    [Header("UX")]
    public bool unfocusAfterSend = true;
    public ClearMode clearMode = ClearMode.OnPlaybackStart;

    public enum ClearMode { Immediate, OnPlaybackStart, Never }

    [Header("State (read-only)")]
    [SerializeField] private UiState state = UiState.Idle;
    
    [Header("Transcription UX")]
    [Tooltip("If ON, focus the text box after a transcript arrives so the user can edit.")]
    public bool focusTextAfterTranscribe = true;
    [Tooltip("If ON, immediately submit the transcribed text (UI override even if controller is OFF).")]
    public bool autoSubmitTranscripts = false;   // optional UI-level override


    // ---------------- Unity ----------------

    private void OnEnable()
    {
        // Input adapters
        if (textInput)
        {
            // IMPORTANT: Only one script should handle Enter. Let TextInputController do it.
            textInput.SetListenForEnter(true);
            textInput.OnSubmit.AddListener(HandleTextSubmitted);
            textInput.OnChange.AddListener(HandleTextChanged);
        }

        if (mic)
        {
            mic.OnRecordStarted += OnMicStarted;
            mic.OnRecordStopped += OnMicStopped;
        }

        if (audioFilePicker)
        {
            audioFilePicker.OnAudioLoaded += OnAudioLoaded;
            audioFilePicker.OnError += OnUploadError;
        }

        // Buttons
        if (recordButton)  recordButton.onClick.AddListener(OnRecordClicked);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (textSubmitButton) textSubmitButton.onClick.AddListener(OnTextSubmitClicked);
        if (uploadButton)  uploadButton.onClick.AddListener(OnUploadClicked);

        PipelineEvents.OnTranscribed += OnTranscribed;     // NEW
        PipelineEvents.OnPlaybackStart += OnPlaybackStart; // keep if you use it
        PipelineEvents.OnError += OnPipelineError;

        audioVisualizer. SetUp();
        ApplyState(UiState.Idle, force: true);
    }

    private void OnDisable()
    {
        if (textInput)
        {
            textInput.OnSubmit.RemoveListener(HandleTextSubmitted);
            textInput.OnChange.RemoveListener(HandleTextChanged);
        }

        if (mic)
        {
            mic.OnRecordStarted -= OnMicStarted;
            mic.OnRecordStopped -= OnMicStopped;
        }

        if (audioFilePicker)
        {
            audioFilePicker.OnAudioLoaded -= OnAudioLoaded;
            audioFilePicker.OnError -= OnUploadError;
        }

        if (recordButton)  recordButton.onClick.RemoveListener(OnRecordClicked);
        if (cancelButton)  cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (textSubmitButton) textSubmitButton.onClick.RemoveListener(OnTextSubmitClicked);
        if (uploadButton)  uploadButton.onClick.RemoveListener(OnUploadClicked);

        PipelineEvents.OnTranscribed -= OnTranscribed;     // NEW
        PipelineEvents.OnPlaybackStart -= OnPlaybackStart;
        PipelineEvents.OnError -= OnPipelineError;
    }

    // ---------------- UI State Machine ----------------

    private void ApplyState(UiState next, bool force = false)
    {
        if (!force && state == next) return;
        state = next;

        bool hasText = textInput && !string.IsNullOrWhiteSpace(textInput.GetText());

        SetActive(recordButton, state == UiState.Idle && !hasText);
        SetActive(cancelButton, state == UiState.Recording);
        SetActive(confirmButton, state == UiState.Recording);

        SetActive(textSubmitButton, state == UiState.TextReady || state == UiState.Busy || hasText);
        SetInteractable(textSubmitButton, state == UiState.TextReady);

        SetActive(uploadButton, state == UiState.Idle || state == UiState.TextReady);

        // Visualizer hint
        if (audioVisualizer)
        {
            if (state == UiState.Recording)audioVisualizer.StartVisualizing(true);
            else audioVisualizer.StopVisualizing(true);
        }
    }

    private static void SetActive(Behaviour b, bool active)
    {
        if (!b) return;
        b.gameObject.SetActive(active);
    }

    private static void SetInteractable(Selectable s, bool interactable)
    {
        if (!s) return;
        s.interactable = interactable;
    }

    // ---------------- Input handlers ----------------

    private void HandleTextChanged(string txt)
    {
        if (state == UiState.Recording) return; // ignore typing while recording UI
        ApplyState(string.IsNullOrWhiteSpace(txt) ? UiState.Idle : UiState.TextReady);
    }

    private void HandleTextSubmitted(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt) || pipeline == null) return;
        SubmitTextInternal(txt);
    }

    // ---------------- Button callbacks ----------------

    private void OnRecordClicked()
    {
        if (!mic) { Debug.LogWarning("Mic not assigned."); return; }
        mic.StartCapture();
        ApplyState(UiState.Recording);
    }

    private void OnCancelClicked()
    {
        if (mic && mic.IsRecording()) mic.StopCapture();
        ApplyState(UiState.Idle);
    }

    private void OnConfirmClicked()
    {
        if (!mic) { Debug.LogWarning("Mic not assigned."); return; }
        var clip = mic.StopAndGetFinalClip();
        if (!clip) { ApplyState(UiState.Idle); return; }

        if (pipeline == null) { Debug.LogError("Pipeline not assigned."); ApplyState(UiState.Idle); return; }

        pipeline.SubmitAudio(clip);
        ApplyState(UiState.Busy);
    }

    private void OnTextSubmitClicked()
    {
        if (!textInput || pipeline == null) return;

        var txt = textInput.GetText();
        if (string.IsNullOrWhiteSpace(txt)) { ApplyState(UiState.Idle); return; }

        SubmitTextInternal(txt);
    }

    private void OnUploadClicked()
    {
#if UNITY_EDITOR
        audioFilePicker?.PickInEditor();
#else
        Debug.Log("Hook your platform picker or call audioFilePicker.LoadFromUrl(path)");
#endif
        if (uploadButton) uploadButton.interactable = false;
    }

    // ---------------- Adapters’ events ----------------

    private void OnMicStarted() => ApplyState(UiState.Recording);

    private void OnMicStopped()
    {
        // Do not force Idle here; stop may come from confirm path
        if (state == UiState.Recording) ApplyState(UiState.Idle);
    }

    private void OnAudioLoaded(AudioClip clip)
    {
        if (uploadButton) uploadButton.interactable = true;
        if (!clip || pipeline == null) { ApplyState(UiState.Idle); return; }
        pipeline.SubmitAudio(clip);
        ApplyState(UiState.Busy);
    }

    private void OnUploadError(string err)
    {
        if (uploadButton) uploadButton.interactable = true;
        Debug.LogError($"Upload error: {err}");
        ApplyState(UiState.Idle);
    }

    // ---------------- PipelineEvents ----------------

    private void OnPlaybackStart(PipelineEvents.PlaybackArgs args)
    {
        if (clearMode == ClearMode.OnPlaybackStart && textInput) textInput.Clear();
        if (unfocusAfterSend) EventSystem.current?.SetSelectedGameObject(null);
        ApplyState(UiState.Idle);
    }

    private void OnPipelineError(PipelineEvents.ErrorArgs err)
    {
        Debug.LogError($"Pipeline error at {err.Stage}: {err.Message}");
        ApplyState(UiState.Idle);
    }

    // ---------------- Helpers ----------------

    private void SubmitTextInternal(string txt)
    {
        if (demoRouter && demoRouter.TryHandle(txt))
        {
            // consumed by demo: set state and exit
            if (unfocusAfterSend) EventSystem.current?.SetSelectedGameObject(null);
            switch (clearMode)
            {
                case ClearMode.Immediate: textInput?.Clear(); ApplyState(UiState.Idle); break;
                case ClearMode.OnPlaybackStart: ApplyState(UiState.Busy); break; // demo may enqueue → PlaybackStart
                case ClearMode.Never: ApplyState(UiState.TextReady); break;
            }
            return;
        }
        pipeline.SubmitText(txt);

        if (unfocusAfterSend) EventSystem.current?.SetSelectedGameObject(null);

        switch (clearMode)
        {
            case ClearMode.Immediate:
                textInput?.Clear();
                ApplyState(UiState.Idle);
                break;
            case ClearMode.OnPlaybackStart:
                ApplyState(UiState.Busy);
                break;
            case ClearMode.Never:
                ApplyState(UiState.TextReady);
                break;
        }
    }
    
    private void OnTranscribed(PipelineEvents.TranscribedArgs args)
    {
        var t = args.Transcript ?? "";
        if (textInput) textInput.SetText(t);

        if (autoSubmitTranscripts)
        {
            // optional UI override: send immediately
            SubmitTextInternal(t);
            return;
        }

        // Otherwise just show it and wait for user
        if (focusTextAfterTranscribe)
            EventSystem.current?.SetSelectedGameObject(textInput?.gameObject);

        ApplyState(string.IsNullOrWhiteSpace(t) ? UiState.Idle : UiState.TextReady);
    }
}
