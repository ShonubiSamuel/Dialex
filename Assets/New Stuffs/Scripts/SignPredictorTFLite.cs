using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mediapipe;
using Mediapipe.Unity;
using TensorFlowLite; 
using TMPro;

public class SignPredictorTFLite : MonoBehaviour
{
    [Header("Model & Data")]
    [Tooltip("Assign your DYNAMIC-SHAPE model.tflite file here.")]
    public TextAsset tfliteModelAsset;
    [Tooltip("Your sign_to_prediction_index_map.json file")]
    public TextAsset labelMapJson;

    [Header("Scene References")]
    public HolisticLandmarkListAnnotationController holistic;
    [Tooltip("Optional: Slider UI element to show recording progress.")]
    public Slider recordingProgressSlider;

    public TextMeshProUGUI resultLabel;

    [Header("Assign in Inspector")]
    public Button recordButton;

     [Header("Button State Settings")]
    // The color when in the "Stop" state
    public UnityEngine.Color stopColor = UnityEngine.Color.green;
    
    // The color when in the "Record" state
    public UnityEngine.Color recordColor = UnityEngine.Color.white;
    
    // The text to display for the "Stop" state
    public string stopText = "Stop";
    
    // The text to display for the "Record" state
    public string recordText = "Record";
    private TextMeshProUGUI buttonText;
    private UnityEngine.UI.Image buttonImage;


    [Header("Inference Settings")]
    [Tooltip("Maximum frames to record before auto-stopping. Also used for buffer size.")]
    public int maxSequenceLength = 128;

    [Header("Prediction Filtering")]
    [Range(0.0f, 1.0f)]
    public float confidenceThreshold = 0.50f;

    [Header("TFLite Settings")]
    [Tooltip("Number of threads TFLite will use (-1 uses default).")]
    public int numThreads = -1;

    // --- Constants ---
    private const int TOTAL_LANDMARKS = 543;
    private const int COORDINATES = 3;

    // --- Runtime ---
    private Interpreter m_Interpreter;
    private float[] m_InputBuffer;
    private float[] m_OutputBuffer;
    private Interpreter.TensorInfo m_InputTensorInfo;
    private Interpreter.TensorInfo m_OutputTensorInfo;
    private string[] m_Labels;
    private float[,,] m_FramesBuffer; // Recording buffer
    private int m_CurrentWriteIndex = 0;
    private int m_FramesRecordedThisSession = 0;
    private bool m_IsRunningInference = false;
    private bool m_IsRecording = false;

    async void Start()
    {
        if (numThreads <= 0) {
            numThreads = SystemInfo.processorCount > 0 ? SystemInfo.processorCount : 1;
        }

        if (tfliteModelAsset == null) {
            Debug.LogError("TFLite Model Asset is not assigned!"); this.enabled = false; return;
        }
        byte[] modelData = tfliteModelAsset.bytes;

        InitializeInferenceEngine(modelData);

        if (m_Interpreter == null) { this.enabled = false; return; } // Stop if init failed

        m_FramesBuffer = new float[maxSequenceLength, TOTAL_LANDMARKS, COORDINATES];
        InitializeBufferWithNaN(); // Clear recording buffer

        if (recordingProgressSlider != null) {
            recordingProgressSlider.minValue = 0;
            recordingProgressSlider.maxValue = maxSequenceLength;
            recordingProgressSlider.value = 0;
            recordingProgressSlider.gameObject.SetActive(false);
        }

        if (holistic != null)
        {
            holistic.OnHolisticLandmarks += OnHolisticLandmarks;
        }
        else
        {
            Debug.LogError("Holistic controller is not assigned!");
        }
        
        if (recordButton != null)
        {
            recordButton.onClick.AddListener( () =>
            {
                if (!m_IsRecording)
                {
                    SetRecordingState(true);
                    StartRecording();
                }
                else
                {
                    SetRecordingState(false);
                    StopAndProcessSign();
                }
            } );
            buttonImage = recordButton.GetComponent<UnityEngine.UI.Image>();
        
            buttonText = recordButton.GetComponentInChildren<TextMeshProUGUI>();
           
        }
        else
        {
             Debug.LogError("RecordButtonToggle: The 'Record Button' is not assigned in the Inspector! Please check the script on " + gameObject.name);
        }
       
    }

    void InitializeBufferWithNaN()
    {
        // Clear the *recording* buffer, not the TFLite input/output buffers
        for (int frame = 0; frame < maxSequenceLength; frame++) {
            for (int landmark = 0; landmark < TOTAL_LANDMARKS; landmark++) {
                m_FramesBuffer[frame, landmark, 0] = float.NaN;
                m_FramesBuffer[frame, landmark, 1] = float.NaN;
                m_FramesBuffer[frame, landmark, 2] = float.NaN;
            }
        }
        m_CurrentWriteIndex = 0;
        m_FramesRecordedThisSession = 0;
    }

    void OnDisable()
    {
        if (holistic != null) {
            holistic.OnHolisticLandmarks -= OnHolisticLandmarks;
        }
        m_Interpreter?.Dispose();
    }

    void OnHolisticLandmarks(
      IReadOnlyList<NormalizedLandmark> face, IReadOnlyList<NormalizedLandmark> pose,
      IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand)
    {
        if (!m_IsRecording || m_IsRunningInference) return;

        WriteFrame(pose, face, leftHand, rightHand, m_CurrentWriteIndex);
        m_FramesRecordedThisSession++;
        m_CurrentWriteIndex++; // Simple increment for recording position

        if (recordingProgressSlider != null) {
            recordingProgressSlider.value = m_FramesRecordedThisSession;
        }
        
        if (m_FramesRecordedThisSession >= maxSequenceLength) {
            Debug.Log($"Max sequence length ({maxSequenceLength}) reached. Auto-stopping.");
            StopAndProcessSign();
        }
    }

    //-----------------------------------------------------
    // Recording Control
    //-----------------------------------------------------

    public void StartRecording()
    {
        if (m_IsRunningInference) { Debug.LogWarning("Busy"); return; }
        if (m_IsRecording) { Debug.LogWarning("Already recording."); return; }
        InitializeBufferWithNaN(); // Clear recording buffer
        m_IsRecording = true;
        if (recordingProgressSlider != null) {
            recordingProgressSlider.value = 0;
            recordingProgressSlider.gameObject.SetActive(true);
        }
        Debug.Log("Started Recording...");
    }

    public void StopAndProcessSign()
    {
        if (!m_IsRecording && m_FramesRecordedThisSession == 0) { Debug.LogWarning("Nothing to process."); return; }
        if (m_IsRunningInference) { Debug.LogWarning("Busy"); return; }
        m_IsRecording = false;
        if (recordingProgressSlider != null)
        {
            recordingProgressSlider.gameObject.SetActive(false);
        }
        if (m_FramesRecordedThisSession == 0)
        {
            Debug.LogWarning("No frames recorded."); InitializeBufferWithNaN(); return;
        }
        Debug.Log($"Stopped Recording. Processing {m_FramesRecordedThisSession} frames...");
        m_IsRunningInference = true;
        RunPredictionAsync();
    }
    
    private void SetRecordingState(bool m_IsRecording)
    {
        if (buttonText != null && m_IsRecording)
        {
            buttonText.text = stopText;
            buttonImage.color = stopColor;
        }
        
        if (buttonImage != null&& !m_IsRecording)
        {
            buttonText.text = recordText;
            buttonImage.color = recordColor;
        }
    }

    //-----------------------------------------------------
    // Inference Logic
    //-----------------------------------------------------

    private async void RunPredictionAsync()
    {
        int framesToProcess = m_FramesRecordedThisSession;
        try {
            float[] results = await Task.Run(() => RunTFLiteInference(framesToProcess));
            if (results != null) {
                ProcessResults(results);
            } else { Debug.LogError("TFLite inference returned null results."); }
        } catch (System.Exception ex) {
            Debug.LogError($"Error during async TFLite inference: {ex.Message}\n{ex.StackTrace}");
        } finally {
            m_IsRunningInference = false;
            InitializeBufferWithNaN(); // Clear recording buffer after attempt
            if (recordingProgressSlider != null) recordingProgressSlider.value = 0;
        }
    }

    /// <summary>
    /// Executes TFLite inference. Designed for background thread.
    /// </summary>
    private float[] RunTFLiteInference(int frameCount)
    {
        if (m_Interpreter == null) return null;

        // 1. Get Chronological Frames
        float[,,] recordedFrames = GetRecordedFramesChronological(frameCount);
        if (recordedFrames.GetLength(0) == 0) return null;

        // 2. Prepare Input Buffer
        int requiredInputSize = frameCount * TOTAL_LANDMARKS * COORDINATES;
        if (m_InputBuffer == null || m_InputBuffer.Length < requiredInputSize) {
            // Allocate if first time or if somehow frameCount exceeds maxSequenceLength buffer
            m_InputBuffer = new float[requiredInputSize];
        }
        Flatten3DArrayInto(recordedFrames, m_InputBuffer, requiredInputSize);

        // 3. Define Input Shape
        int[] inputShape = { frameCount, TOTAL_LANDMARKS, COORDINATES };

        // 4. Resize & Allocate TFLite Tensors
        m_Interpreter.ResizeInputTensor(0, inputShape); // Using index 0 directly
        m_Interpreter.AllocateTensors();

        // 5. Set Input Data
        // Create a temporary array matching the exact size needed by TFLite
        // This avoids potential issues if m_InputBuffer is larger
        float[] exactInputData = new float[requiredInputSize];
        System.Array.Copy(m_InputBuffer, exactInputData, requiredInputSize);
        m_Interpreter.SetInputTensorData(0, exactInputData); // Using index 0

        // 6. Invoke
        m_Interpreter.Invoke();

        // 7. Get Output Data
        // Use the pre-allocated m_OutputBuffer
        m_Interpreter.GetOutputTensorData(0, m_OutputBuffer); // Using index 0

        // Return the buffer (results valid until next Invoke overwrites it)
        return m_OutputBuffer;
    }

    /// <summary>
    /// Processes the model output.
    /// </summary>
    void ProcessResults(float[] results)
    {
        if (results == null || results.Length == 0) { Debug.LogWarning("Empty results."); return; }
        if (m_Labels == null || m_Labels.Length == 0) { Debug.LogError("Labels not loaded."); return; }

        float maxProbability = -Mathf.Infinity; int predictedIndex = -1;
        int loopLength = Mathf.Min(results.Length, m_Labels.Length);

        for (int i = 0; i < loopLength; i++) {
            if (!float.IsNaN(results[i]) && results[i] > maxProbability) {
                maxProbability = results[i]; predictedIndex = i;
            }
        }

        if (predictedIndex < 0) {
            Debug.LogWarning($"Prediction failed. No valid probabilities. Max prob: {maxProbability}. Output size: {results.Length}"); return;
        }
        if (predictedIndex >= m_Labels.Length) { // Should be caught by loopLength, but extra safe
            Debug.LogWarning($"Prediction index {predictedIndex} >= label array size {m_Labels.Length}."); return;
        }

        string currentPrediction = m_Labels[predictedIndex];
        if (maxProbability >= confidenceThreshold) {
            Debug.Log($"<color=teal><b>TFLITE PREDICTION: {currentPrediction} ({maxProbability:P1})</b></color>");
            resultLabel.text = currentPrediction;
        } else {
            Debug.Log($"<color=gray>TFLITE PREDICTION (Low Confidence): {currentPrediction} ({maxProbability:P1})</color>");
        }
    }

    #region Helper and Initialization Methods

    /// <summary>
    /// Initializes TFLite Interpreter.
    /// </summary>
    private void InitializeInferenceEngine(byte[] modelData)
    {
        ParseLabelMap();

        var options = new InterpreterOptions() {
            threads = this.numThreads
        };

        try {
            m_Interpreter = new Interpreter(modelData, options);

            // Initial Resize & Allocate using max sequence length
            int[] initialInputShape = { maxSequenceLength, TOTAL_LANDMARKS, COORDINATES };
            m_Interpreter.ResizeInputTensor(0, initialInputShape); // Use index 0
            m_Interpreter.AllocateTensors();

            // Get Tensor Info (using index 0 for input/output)
            m_InputTensorInfo = m_Interpreter.GetInputTensorInfo(0);
            m_OutputTensorInfo = m_Interpreter.GetOutputTensorInfo(0);

            // *** CORRECTED: Calculate size from shape ***
            long inputElementCount = 1;
            if (m_InputTensorInfo.shape != null) {
                foreach (int dim in m_InputTensorInfo.shape) { inputElementCount *= dim; }
            } else {
                 Debug.LogError("Could not get input tensor shape during initialization!");
                 inputElementCount = maxSequenceLength * TOTAL_LANDMARKS * COORDINATES; // Fallback estimate
            }

            long outputElementCount = 1;
             if (m_OutputTensorInfo.shape != null && m_OutputTensorInfo.shape.Length > 0) {
                 // Check dims > 0 before multiplying
                 bool validShape = true;
                 foreach(int dim in m_OutputTensorInfo.shape) {
                     if (dim <= 0) validShape = false; // TFLite might use -1 for unknown dims initially
                     else outputElementCount *= dim;
                 }
                 if (!validShape) {
                     Debug.LogWarning("Output tensor shape contains non-positive dimensions. Cannot calculate exact size.");
                     outputElementCount = -1; // Indicate unknown size
                 }

             } else {
                 Debug.LogError("Could not get output tensor shape or shape is empty!");
                 outputElementCount = -1; // Indicate unknown size
             }

             // Allocate C# Buffers
             m_InputBuffer = new float[inputElementCount]; // Based on initial allocation

             // Determine output buffer size carefully
             int labelCount = m_Labels?.Length ?? 0;
             int outputBufferSize = 0;

             if (outputElementCount > 0) {
                 outputBufferSize = (int)outputElementCount;
                 if (labelCount > 0 && outputBufferSize != labelCount) {
                     Debug.LogWarning($"Model output element count ({outputBufferSize}) differs from label count ({labelCount}). Using model output count for buffer.");
                 }
             } else if (labelCount > 0) {
                 Debug.LogWarning($"Could not determine output size from model shape. Using label count ({labelCount}) for buffer.");
                 outputBufferSize = labelCount;
             } else {
                 Debug.LogError("Cannot determine output buffer size from model or labels! Using fallback size 250.");
                 outputBufferSize = 250; // Last resort
             }
             m_OutputBuffer = new float[outputBufferSize];


            // *** CORRECTED: Logging (removed .index) ***
            Debug.Log("TensorFlow Lite Interpreter Initialized.");
            Debug.Log($"Threads Used: {options.threads}");
            string inputShapeStr = (m_InputTensorInfo.shape != null) ? string.Join(",", m_InputTensorInfo.shape) : "N/A";
            string outputShapeStr = (m_OutputTensorInfo.shape != null) ? string.Join(",", m_OutputTensorInfo.shape) : "N/A";
            Debug.Log($"Input Tensor [Index 0]: Name: {m_InputTensorInfo.name}, Type: {m_InputTensorInfo.type}, Shape: [{inputShapeStr}]");
            Debug.Log($"Output Tensor [Index 0]: Name: {m_OutputTensorInfo.name}, Type: {m_OutputTensorInfo.type}, Shape: [{outputShapeStr}]");

        } catch (System.Exception e) {
            Debug.LogError($"Failed to initialize TFLite Interpreter: {e.Message}\n{e.StackTrace}");
            m_Interpreter = null;
        }
    }

    // ParseLabelMap (Robust version)
     private void ParseLabelMap()
    {
        var tempLabels = new Dictionary<int, string>();
        if (labelMapJson == null) {
            Debug.LogError("Label Map JSON not assigned!"); m_Labels = new string[0]; return;
        }
        try {
            string jsonText = labelMapJson.text.Trim('{', '}'); string[] entries = jsonText.Split(',');
            foreach (var entry in entries) {
                if(string.IsNullOrWhiteSpace(entry)) continue; string[] pair = entry.Split(':');
                 if(pair.Length != 2) { Debug.LogWarning($"Skipping invalid label map entry: {entry}"); continue; }
                string sign = pair[0].Trim().Trim('"'); string indexStr = pair[1].Trim();
                 if (int.TryParse(indexStr, out int index)) { if (!string.IsNullOrWhiteSpace(sign)) tempLabels[index] = sign; }
                 else { Debug.LogWarning($"Skipping invalid index: {entry}"); }
            }
             if(tempLabels.Count == 0) throw new System.Exception("No valid labels parsed.");
            int maxIndex = tempLabels.Keys.Max(); int arraySize = maxIndex + 1;
            m_Labels = new string[arraySize]; for(int i=0; i< arraySize; ++i) m_Labels[i] = $"UNUSED_{i}";
            foreach (var pair in tempLabels) { if(pair.Key >= 0 && pair.Key < arraySize) m_Labels[pair.Key] = pair.Value; else Debug.LogWarning($"Label index {pair.Key} out of bounds ({arraySize})."); }
             Debug.Log($"Label map parsed. Labels: {tempLabels.Count}. Array size: {arraySize}.");
        } catch (System.Exception ex) { Debug.LogError($"Error parsing Label Map JSON: {ex.Message}"); m_Labels = new string[0]; }
    }


    // WriteFrame (Uses modulo)
    void WriteFrame( IReadOnlyList<NormalizedLandmark> pose, IReadOnlyList<NormalizedLandmark> face,
        IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand, int frameNumber)
    {
         int bufferWriteIndex = (frameNumber > 0 ? frameNumber - 1 : 0) % maxSequenceLength; // frameNumber starts at 1, buffer index starts at 0
         if (bufferWriteIndex < 0 || bufferWriteIndex >= maxSequenceLength) { Debug.LogError($"Invalid buffer write index {bufferWriteIndex}"); return; }
        CopyLandmarks(face, 0, 468, bufferWriteIndex); CopyLandmarks(leftHand, 468, 21, bufferWriteIndex);
        CopyLandmarks(pose, 489, 33, bufferWriteIndex); CopyLandmarks(rightHand, 522, 21, bufferWriteIndex);
    }

    // CopyLandmarks (Includes safety)
    void CopyLandmarks(IReadOnlyList<NormalizedLandmark> source, int startIndex, int count, int bufferFrameIdx)
    {
        if (bufferFrameIdx < 0 || bufferFrameIdx >= maxSequenceLength) return;
        bool sourceIsValid = source != null && source.Count > 0;
        for (int i = 0; i < count; i++) {
            int bufferIndex = startIndex + i; if (bufferIndex >= TOTAL_LANDMARKS) continue;
            if (sourceIsValid && i < source.Count) {
                var lm = source[i];
                if (IsInvalidFloat(lm.X) || IsInvalidFloat(lm.Y) || IsInvalidFloat(lm.Z)) { m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = float.NaN; m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = float.NaN; m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = float.NaN; }
                else { m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = lm.X; m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = lm.Y; m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = lm.Z; }
            } else { m_FramesBuffer[bufferFrameIdx, bufferIndex, 0] = float.NaN; m_FramesBuffer[bufferFrameIdx, bufferIndex, 1] = float.NaN; m_FramesBuffer[bufferFrameIdx, bufferIndex, 2] = float.NaN; }
        }
    }
    bool IsInvalidFloat(float f) => float.IsNaN(f) || float.IsInfinity(f);

    /// <summary> Flattens 3D array into the start of a pre-allocated 1D buffer. </summary>
    private void Flatten3DArrayInto(float[,,] input3D, float[] outputFlat, int elementCountToCopy)
    {
        int requiredSize = input3D.GetLength(0) * input3D.GetLength(1) * input3D.GetLength(2);
        if (elementCountToCopy != requiredSize) {
             Debug.LogError($"FlattenInto: elementCountToCopy ({elementCountToCopy}) != requiredSize ({requiredSize})");
             // Handle error or adjust count? For now, proceed with elementCountToCopy
             elementCountToCopy = Mathf.Min(elementCountToCopy, requiredSize); // Prevent overflow
        }
        if (outputFlat == null || outputFlat.Length < elementCountToCopy) {
             Debug.LogError($"FlattenInto: Output buffer too small or null. Size: {outputFlat?.Length ?? -1}, Need: {elementCountToCopy}");
             return; // Cannot copy
        }
        // Copy only the required number of elements
        System.Buffer.BlockCopy(input3D, 0, outputFlat, 0, elementCountToCopy * sizeof(float));
    }

    /// <summary> Gets recorded frames chronologically from the circular buffer. </summary>
    private float[,,] GetRecordedFramesChronological(int frameCount)
    {
         frameCount = Mathf.Min(frameCount, m_FramesRecordedThisSession, maxSequenceLength);
         if (frameCount <= 0) return new float[0, TOTAL_LANDMARKS, COORDINATES];
        var orderedFrames = new float[frameCount, TOTAL_LANDMARKS, COORDINATES];
        // m_CurrentWriteIndex is ONE PAST the last written frame's index in the buffer
        int startReadIndex = (m_CurrentWriteIndex - frameCount + maxSequenceLength) % maxSequenceLength;
        for (int i = 0; i < frameCount; i++) {
            int sourceBufferIndex = (startReadIndex + i) % maxSequenceLength;
            for (int landmark = 0; landmark < TOTAL_LANDMARKS; landmark++) {
                orderedFrames[i, landmark, 0] = m_FramesBuffer[sourceBufferIndex, landmark, 0];
                orderedFrames[i, landmark, 1] = m_FramesBuffer[sourceBufferIndex, landmark, 1];
                orderedFrames[i, landmark, 2] = m_FramesBuffer[sourceBufferIndex, landmark, 2];
            }
        }
        return orderedFrames;
    }

    #endregion
}