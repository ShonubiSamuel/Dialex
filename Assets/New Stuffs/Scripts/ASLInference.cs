// using UnityEngine;
// using Unity.InferenceEngine;
// using System.Linq;
// using System.Collections.Generic;
// using Mediapipe;
// using Mediapipe.Unity;
//
// public class SignPredictor : MonoBehaviour
// {
//     [Header("Model & Data")]
//     public ModelAsset modelAsset;
//     public TextAsset labelMapJson;
//
//     [Header("Scene References")]
//     public HolisticLandmarkListAnnotationController holistic;
//
//     [Header("Inference Settings")]
//     public int sequenceLength = 30;
//
//     [Header("Prediction Filtering")]
//     [Range(0.0f, 1.0f)]
//     public float confidenceThreshold = 0.50f;
//     public int stabilityThreshold = 5;
//
//     // --- Model & Data Constants ---
//     private const int TOTAL_LANDMARKS = 543;
//
//     // --- Runtime Variables ---
//     private Model runtimeModel;
//     private Worker worker;
//     private string[] labels;
//     private float[,,] framesBuffer;
//     private int currentFrameIndex = 0;
//     private int framesFilled = 0;
//     private string lastPrediction = "";
//     private int stabilityCounter = 0;
//
//     void Start()
//     {
//         InitializeInferenceEngine();
//         framesBuffer = new float[sequenceLength, TOTAL_LANDMARKS, 3];
//
//         if (holistic != null)
//         {
//             holistic.OnHolisticLandmarks += OnHolisticLandmarks;
//         }
//         else
//         {
//             Debug.LogError("Holistic controller is not assigned!");
//         }
//     }
//
//     void OnDisable()
//     {
//         if (holistic != null)
//         {
//             holistic.OnHolisticLandmarks -= OnHolisticLandmarks;
//         }
//         worker?.Dispose();
//     }
//
//     void OnHolisticLandmarks(
//       IReadOnlyList<NormalizedLandmark> face, IReadOnlyList<NormalizedLandmark> pose,
//       IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand)
//     {
//         WriteFrame(pose, face, leftHand, rightHand, currentFrameIndex);
//         currentFrameIndex = (currentFrameIndex + 1) % sequenceLength;
//         if (framesFilled < sequenceLength) framesFilled++;
//
//         if (framesFilled >= sequenceLength)
//         {
//             RunPrediction();
//         }
//     }
//
//     private void RunPrediction()
//     {
//         // 1. Get raw frames in chronological order. NO MORE PREPROCESSING!
//         float[,,] orderedFrames = GetOrderedFrames();
//
//         // 2. Create the tensor with the RAW data shape the model expects.
//         // Shape is [batch_size, sequence_length, landmarks, coordinates]
//         var shape = new TensorShape(1, orderedFrames.GetLength(0), orderedFrames.GetLength(1), orderedFrames.GetLength(2));
//         
//         // Flatten the array for the tensor constructor
//         float[] flatInput = Flatten3DArray(orderedFrames);
//
//         using (var inputTensor = new Tensor<float>(shape, flatInput))
//         {
//             worker.Schedule(inputTensor);
//             Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
//             float[] results = outputTensor.DownloadToArray();
//
//             // 3. Process and filter the results
//             ProcessResults(results);
//         }
//     }
//     
//     #region Helper and Initialization Methods
//
//     void ProcessResults(float[] results)
//     {
//         float maxProbability = results.Max();
//         // if (maxProbability < confidenceThreshold)
//         // {
//         //     stabilityCounter = 0;
//         //     lastPrediction = "";
//         //     return;
//         // }
//
//         int predictedIndex = System.Array.IndexOf(results, maxProbability);
//         string currentPrediction = labels[predictedIndex];
//
//         if (currentPrediction == lastPrediction)
//         {
//             stabilityCounter++;
//         }
//         else
//         {
//             stabilityCounter = 1;
//             lastPrediction = currentPrediction;
//         }
//
//         if (stabilityCounter >= stabilityThreshold)
//         {
//             Debug.Log($"<color=green><b>FINAL PREDICTION: {currentPrediction} ({maxProbability:P1})</b></color>");
//         }
//     }
//
//     private void InitializeInferenceEngine()
//     {
//         runtimeModel = ModelLoader.Load(modelAsset);
//         ParseLabelMap();
//         worker = new Worker(runtimeModel, BackendType.GPUCompute);
//         Debug.Log("Inference Engine Initialized.");
//     }
//     
//     private void ParseLabelMap()
//     {
//         var tempLabels = new Dictionary<int, string>();
//         string jsonText = labelMapJson.text.Trim('{', '}');
//         string[] entries = jsonText.Split(',');
//         foreach (var entry in entries)
//         {
//             string[] pair = entry.Split(':');
//             string sign = pair[0].Trim().Trim('"');
//             int index = int.Parse(pair[1].Trim());
//             tempLabels[index] = sign;
//         }
//         labels = new string[tempLabels.Count];
//         foreach (var pair in tempLabels) { labels[pair.Key] = pair.Value; }
//     }
//     
//     void WriteFrame(
//         IReadOnlyList<NormalizedLandmark> pose, IReadOnlyList<NormalizedLandmark> face,
//         IReadOnlyList<NormalizedLandmark> leftHand, IReadOnlyList<NormalizedLandmark> rightHand,
//         int frameIdx)
//     {
//         CopyLandmarks(pose, 0, 33, frameIdx);
//         CopyLandmarks(face, 33, 468, frameIdx);
//         CopyLandmarks(leftHand, 501, 21, frameIdx);
//         CopyLandmarks(rightHand, 522, 21, frameIdx);
//     }
//
//     void CopyLandmarks(IReadOnlyList<NormalizedLandmark> source, int startIndex, int count, int frameIdx)
//     {
//         bool sourceIsValid = source != null && source.Count > 0;
//         for (int i = 0; i < count; i++)
//         {
//             int bufferIndex = startIndex + i;
//             if (bufferIndex >= TOTAL_LANDMARKS) continue;
//
//             if (sourceIsValid && i < source.Count)
//             {
//                 var lm = source[i];
//                 framesBuffer[frameIdx, bufferIndex, 0] = lm.X;
//                 framesBuffer[frameIdx, bufferIndex, 1] = lm.Y;
//                 framesBuffer[frameIdx, bufferIndex, 2] = lm.Z;
//             }
//             else
//             {
//                 framesBuffer[frameIdx, bufferIndex, 0] = 0;
//                 framesBuffer[frameIdx, bufferIndex, 1] = 0;
//                 framesBuffer[frameIdx, bufferIndex, 2] = 0;
//             }
//         }
//     }
//     
//     private float[,,] GetOrderedFrames()
//     {
//         var ordered = new float[sequenceLength, TOTAL_LANDMARKS, 3];
//         for (int i = 0; i < sequenceLength; i++)
//         {
//             int sourceFrameIndex = (currentFrameIndex + i) % sequenceLength;
//             System.Buffer.BlockCopy(
//                 framesBuffer, (sourceFrameIndex * TOTAL_LANDMARKS * 3) * sizeof(float),
//                 ordered, (i * TOTAL_LANDMARKS * 3) * sizeof(float),
//                 (TOTAL_LANDMARKS * 3) * sizeof(float)
//             );
//         }
//         return ordered;
//     }
//     
//     private float[] Flatten3DArray(float[,,] input)
//     {
//         int dim1 = input.GetLength(0);
//         int dim2 = input.GetLength(1);
//         int dim3 = input.GetLength(2);
//         float[] flat = new float[dim1 * dim2 * dim3];
//         System.Buffer.BlockCopy(input, 0, flat, 0, flat.Length * sizeof(float));
//         return flat;
//     }
//     
//     #endregion
// }