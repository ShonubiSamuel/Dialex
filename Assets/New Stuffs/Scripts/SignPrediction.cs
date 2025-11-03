using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using Mediapipe;
using Mediapipe.Unity;
using UnityEngine.UI; // Needed for potential timer UI

public class SignPredictor : MonoBehaviour
{
    [Header("Model & Data")]
    public ModelAsset modelAsset;
    public TextAsset labelMapJson;

    [Header("Scene References")]
    public HolisticLandmarkListAnnotationController holistic;
    [Tooltip("Optional: Slider UI element to show recording progress.")]
    public Slider recordingProgressSlider; // <-- NEW: For visual feedback

    [Header("Inference Settings")]
    public int sequenceLength = 128;

    [Header("Prediction Filtering")]
    [Range(0.0f, 1.0f)]
    public float confidenceThreshold = 0.50f;
    // Stability threshold is less useful now, but kept for consistency
    public int stabilityThreshold = 1; // Lowered for single prediction

    // --- Constants ---
    private const int TOTAL_LANDMARKS = 543;

    // --- Runtime ---
    private Model m_RuntimeModel;
    private Worker m_Worker;
    private string[] m_Labels;
    private float[,,] m_FramesBuffer;
    private int m_CurrentWriteIndex = 0; // Where the *next* frame will be written
    private int m_FramesRecordedThisSession = 0; // How many frames recorded since 'Start'
    private string m_LastPrediction = "";
    private int m_StabilityCounter = 0;
    private bool m_IsRunningInference = false;
    private bool m_IsRecording = false;

    void Start()
    {
        InitializeInferenceEngine();
        m_FramesBuffer = new float[sequenceLength, TOTAL_LANDMARKS, 3];
        InitializeBufferWithNaN(); // Start with a clean buffer

        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.minValue = 0;
            recordingProgressSlider.maxValue = sequenceLength;
            recordingProgressSlider.value = 0;
            recordingProgressSlider.gameObject.SetActive(false); // Hide initially
        }

        if (holistic != null)
        {
            holistic.OnHolisticLandmarks += OnHolisticLandmarks;
        }
        else
        {
            Debug.LogError("Holistic controller is not assigned!");
        }
    }

    void InitializeBufferWithNaN()
    {
        for (int frame = 0; frame < sequenceLength; frame++)
        {
            for (int landmark = 0; landmark < TOTAL_LANDMARKS; landmark++)
            {
                m_FramesBuffer[frame, landmark, 0] = float.NaN;
                m_FramesBuffer[frame, landmark, 1] = float.NaN;
                m_FramesBuffer[frame, landmark, 2] = float.NaN;
            }
        }
        // Reset counters related to buffer content
        m_CurrentWriteIndex = 0;
        m_FramesRecordedThisSession = 0;
    }


    void OnDisable()
    {
        if (holistic != null)
        {
            holistic.OnHolisticLandmarks -= OnHolisticLandmarks;
        }
        m_Worker?.Dispose();
    }

    /// <summary>
    /// Called by MediaPipe. Records frames if active, stops automatically when full.
    /// </summary>
    void OnHolisticLandmarks(
      IReadOnlyList<NormalizedLandmark> face, IReadOnlyList<NormalizedLandmark> pose,
      IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand)
    {
        if (!m_IsRecording || m_IsRunningInference) return; // Stop if not recording or busy

        // --- Recording Logic ---
        // Write frame at the current index
        WriteFrame(pose, face, leftHand, rightHand, m_CurrentWriteIndex);

        m_FramesRecordedThisSession++; // Increment count for this recording session
        m_CurrentWriteIndex = (m_CurrentWriteIndex + 1); // Move to next slot

        // Update visualizer (if assigned)
        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.value = m_FramesRecordedThisSession;
        }
        
        print(m_FramesRecordedThisSession);

        // --- NEW: Automatic Stop & Process ---
        if (m_FramesRecordedThisSession >= sequenceLength)
        {
            Debug.Log($"Buffer full ({sequenceLength} frames). Automatically stopping and processing.");
            StopAndProcessSign(); // Trigger the processing
        }
    }

    //-----------------------------------------------------
    // Public functions to control recording/processing
    //-----------------------------------------------------

    public void StartRecording()
    {
        if (m_IsRunningInference)
        {
            Debug.LogWarning("Cannot start recording while inference is running.");
            return;
        }
        if (m_IsRecording)
        {
             Debug.LogWarning("Already recording.");
             return;
        }

        // Reset buffer and counters for a new recording
        InitializeBufferWithNaN();
        m_LastPrediction = ""; // Reset prediction state
        m_StabilityCounter = 0;

        m_IsRecording = true;
        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.value = 0;
            recordingProgressSlider.gameObject.SetActive(true); // Show slider
        }
        Debug.Log("Started Recording...");
    }

    public void StopAndProcessSign()
    {
        // Check if we *can* stop (must be recording, not already processing)
        if (!m_IsRecording)
        {
            // Allow processing even if not recording IF frames exist from a previous partial record?
            // Let's prevent it for clarity unless specifically needed.
             if (m_FramesRecordedThisSession > 0 && !m_IsRunningInference) {
                 Debug.Log("Processing previously recorded frames...");
                 // Fall through to processing logic
             } else {
                Debug.LogWarning("Not currently recording or no frames recorded.");
                return;
             }
        }
         if (m_IsRunningInference)
        {
            Debug.LogWarning("Cannot process while previous inference is running.");
            return;
        }


        // Stop recording immediately
        m_IsRecording = false;
        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.gameObject.SetActive(false); // Hide slider
        }

        Debug.Log($"Stopped Recording. Processing {m_FramesRecordedThisSession} captured frames...");

        if (m_FramesRecordedThisSession == 0)
        {
            Debug.LogWarning("No frames were recorded.");
            // Reset state in case something weird happened
             InitializeBufferWithNaN();
            return;
        }

        m_IsRunningInference = true;
        RunPredictionWithPadding(); // Run inference
    }

    /// <summary>
    /// Gathers RECORDED data, PADS it if needed, runs the model, and processes the output.
    /// </summary>
    private async void RunPredictionWithPadding()
    {
        // 1. Get the frames recorded *in this session* and pad the rest
        float[,,] inputFrames = GetPaddedRecordedFrames();

        // 2. Create the tensor shape (always the full sequenceLength)
        var shape = new TensorShape(sequenceLength, TOTAL_LANDMARKS, 3);

        // 3. Flatten the potentially padded array
        float[] flatInput = Flatten3DArray(inputFrames);

        // 4. Create input tensor and execute
        using (var inputTensor = new Tensor<float>(shape, flatInput))
        {
            m_Worker.Schedule(inputTensor);
            Tensor<float> outputTensor = m_Worker.PeekOutput() as Tensor<float>;

            try
            {
                var cpuCopyTensor = await outputTensor.ReadbackAndCloneAsync();
                float[] results = cpuCopyTensor.DownloadToArray();
                ProcessResults(results); // Process the single result
                cpuCopyTensor.Dispose();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error during inference or readback: {ex.Message}\n{ex.StackTrace}");
            }
            finally // Ensures this always runs, even if there's an error
            {
                 // 5. Allow processing again
                 m_IsRunningInference = false;
                 // Optionally clear buffer *after* processing is fully done (success or fail)
                 // InitializeBufferWithNaN();
                 // Decide: Do you want the buffer cleared automatically or wait for StartRecording?
                 // Let's clear it to prevent accidentally reprocessing old data.
                  InitializeBufferWithNaN();
                   if (recordingProgressSlider != null) recordingProgressSlider.value = 0; // Reset slider visually
            }
        }
    }


    /// <summary>
    /// Processes the raw float array from the model output.
    /// </summary>
    void ProcessResults(float[] results)
    {
        float maxProbability = -Mathf.Infinity;
        int predictedIndex = -1;

        for (int i = 0; i < results.Length; i++)
        {
            if (!float.IsNaN(results[i]) && results[i] > maxProbability)
            {
                maxProbability = results[i];
                predictedIndex = i;
            }
        }

        if (predictedIndex < 0 || predictedIndex >= m_Labels.Length)
        {
            Debug.LogWarning($"Prediction failed. Index: {predictedIndex}. Max prob: {maxProbability}. Results length: {results.Length}");
            return;
        }

        string currentPrediction = m_Labels[predictedIndex];

        // --- Simplified Output for Manual Mode ---
        if (maxProbability >= confidenceThreshold)
        {
             Debug.Log($"<color=cyan><b>PROCESSED SIGN: {currentPrediction} ({maxProbability:P1})</b></color>");
        }
        else
        {
             Debug.Log($"<color=orange>PROCESSED SIGN (Low Confidence): {currentPrediction} ({maxProbability:P1})</color>");
        }

        // Reset stability stuff - less relevant for single-shot prediction
        m_LastPrediction = currentPrediction; // Store for potential future use?
        m_StabilityCounter = 0;
    }

    #region Helper and Initialization Methods

    // --- (InitializeInferenceEngine, ParseLabelMap, WriteFrame, CopyLandmarks, Flatten3DArray remain mostly the same) ---
     private void InitializeInferenceEngine()
    {
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        ParseLabelMap();
        // Use CPU for stability, switch back to GPUCompute if desired and stable
        m_Worker = new Worker(m_RuntimeModel, BackendType.CPU);
        Debug.Log("Inference Engine Initialized.");
    }

    private void ParseLabelMap()
    {
        var tempLabels = new Dictionary<int, string>();
        if (labelMapJson == null) {
            Debug.LogError("Label Map JSON is not assigned in the Inspector!");
            m_Labels = new string[0];
            return;
        }
        string jsonText = labelMapJson.text.Trim('{', '}');
        string[] entries = jsonText.Split(',');

        foreach (var entry in entries)
        {
            if(string.IsNullOrWhiteSpace(entry)) continue;
            string[] pair = entry.Split(':');
            if(pair.Length != 2) {
                 Debug.LogWarning($"Skipping invalid label map entry: {entry}");
                 continue;
            }

            string sign = pair[0].Trim().Trim('"');
            string indexStr = pair[1].Trim();

             if (int.TryParse(indexStr, out int index))
             {
                 if (string.IsNullOrWhiteSpace(sign)) continue; // Skip empty sign names
                 tempLabels[index] = sign;
             }
             else
             {
                 Debug.LogWarning($"Skipping invalid index in label map entry: {entry}");
             }
        }

        if(tempLabels.Count == 0) {
            Debug.LogError("Label map parsing failed or JSON is empty/invalid!");
            m_Labels = new string[0];
            return;
        }

        int maxIndex = -1;
        foreach(var key in tempLabels.Keys) {
            if (key > maxIndex) maxIndex = key;
        }
        int arraySize = maxIndex + 1;


        m_Labels = new string[arraySize];
        for(int i=0; i< arraySize; ++i) m_Labels[i] = $"UNKNOWN_{i}"; // Default


        foreach (var pair in tempLabels)
        {
            if(pair.Key >= 0 && pair.Key < arraySize)
            {
                m_Labels[pair.Key] = pair.Value;
            } else {
                 Debug.LogWarning($"Label index {pair.Key} is out of bounds for array size {arraySize}. Label '{pair.Value}' skipped.");
            }
        }
         Debug.Log($"Label map parsed. Labels: {tempLabels.Count}. Array size: {arraySize}.");
    }

     void WriteFrame(
        IReadOnlyList<NormalizedLandmark> pose, IReadOnlyList<NormalizedLandmark> face,
        IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand,
        int frameIdx)
    {
         // Use the MODULO operator (%) to wrap the index correctly for the buffer size
         int bufferWriteIndex = frameIdx % sequenceLength;
         if (bufferWriteIndex < 0) bufferWriteIndex += sequenceLength; // Ensure positive index


        // Check if index is valid *after* modulo operation
         if (bufferWriteIndex < 0 || bufferWriteIndex >= sequenceLength)
         {
             Debug.LogError($"Invalid buffer write index {bufferWriteIndex} (original index: {frameIdx}). Buffer size: {sequenceLength}");
             return;
         }

        CopyLandmarks(face, 0, 468, bufferWriteIndex);
        CopyLandmarks(leftHand, 468, 21, bufferWriteIndex);
        CopyLandmarks(pose, 489, 33, bufferWriteIndex);
        CopyLandmarks(rightHand, 522, 21, bufferWriteIndex);
    }

    void CopyLandmarks(IReadOnlyList<NormalizedLandmark> source, int startIndex, int count, int bufferFrameIdx)
    {
         if (bufferFrameIdx < 0 || bufferFrameIdx >= sequenceLength) return; // Already logged in WriteFrame

        bool sourceIsValid = source != null && source.Count > 0;
        for (int i = 0; i < count; i++)
        {
            int bufferIndex = startIndex + i;
            if (bufferIndex >= TOTAL_LANDMARKS) continue;

            if (sourceIsValid && i < source.Count)
            {
                var lm = source[i];
                if (float.IsNaN(lm.X) || float.IsInfinity(lm.X) ||
                    float.IsNaN(lm.Y) || float.IsInfinity(lm.Y) ||
                    float.IsNaN(lm.Z) || float.IsInfinity(lm.Z))
                {
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = float.NaN;
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = float.NaN;
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = float.NaN;
                }
                else
                {
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = lm.X;
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = lm.Y;
                    m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = lm.Z;
                }
            }
            else
            {
                m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = float.NaN;
                m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = float.NaN;
                m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = float.NaN;
            }
        }
    }

    private float[] Flatten3DArray(float[,,] input)
    {
        int dim1 = input.GetLength(0);
        int dim2 = input.GetLength(1);
        int dim3 = input.GetLength(2);
        if (dim1 == 0 || dim2 == 0 || dim3 == 0) return new float[0];
        float[] flat = new float[dim1 * dim2 * dim3];
        System.Buffer.BlockCopy(input, 0, flat, 0, flat.Length * sizeof(float));
        return flat;
    }


    /// <summary>
    /// Gets the frames recorded *in this session* and pads the rest with NaN.
    /// Ensures chronological order based on how they were written.
    /// </summary>
    private float[,,] GetPaddedRecordedFrames()
    {
        var paddedOrdered = new float[sequenceLength, TOTAL_LANDMARKS, 3];
        int framesToCopy = Mathf.Min(m_FramesRecordedThisSession, sequenceLength); // Should always be <= sequenceLength now

        // Determine the starting index in the circular buffer
        // This is the index *after* the last written frame if buffer wrapped, or 0 if not.
        int startReadIndex = (m_CurrentWriteIndex - framesToCopy + sequenceLength) % sequenceLength;


        // Copy the recorded frames chronologically into the start of the new array
        for (int i = 0; i < framesToCopy; i++)
        {
            int sourceBufferIndex = (startReadIndex + i) % sequenceLength;

            // Copy data frame by frame
            for (int landmark = 0; landmark < TOTAL_LANDMARKS; landmark++)
            {
                 // Add safety check for source index if needed, though modulo should handle it
                 // if (sourceBufferIndex < 0 || sourceBufferIndex >= sequenceLength) continue;

                paddedOrdered[i, landmark, 0] = m_FramesBuffer[sourceBufferIndex, landmark, 0];
                paddedOrdered[i, landmark, 1] = m_FramesBuffer[sourceBufferIndex, landmark, 1];
                paddedOrdered[i, landmark, 2] = m_FramesBuffer[sourceBufferIndex, landmark, 2];
            }
        }

        // Fill the remaining frames (if any) with NaN
        for (int i = framesToCopy; i < sequenceLength; i++)
        {
            for (int landmark = 0; landmark < TOTAL_LANDMARKS; landmark++)
            {
                paddedOrdered[i, landmark, 0] = float.NaN;
                paddedOrdered[i, landmark, 1] = float.NaN;
                paddedOrdered[i, landmark, 2] = float.NaN;
            }
        }

        return paddedOrdered;
    }


    #endregion
}