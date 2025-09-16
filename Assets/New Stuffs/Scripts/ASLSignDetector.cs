// using UnityEngine;
// using System;
// using TensorFlowLite; // from asus4 package
//
//
// public class TFLiteSignDetector : MonoBehaviour
// {
//     [Header("Model + Labels")]
//     public TextAsset tfliteModel;     // drag your .tflite here
//     public TextAsset labelsText;      // 250 labels, one per line (optional)
//
//     [Header("Sequence")]
//     public int T = 64;                // window length you’ll feed
//     const int LM = 543;               // landmarks
//     const int C  = 3;                 // x,y,z
//
//     Ihnterpreter interpreter;
//     InterpreterOptions options;
//     string[] labels;
//     float[] seqBuffer;                // rolling [T*LM*C]
//
//     // cache I/O
//     TensorInfo inputInfo, outputInfo;
//     bool inputIs4D = false;           // [1,T,543,3] vs [T,543,3]
//     bool inputIsFloat = true;         // else (u)int8
//     float inScale = 1f; int inZero = 0;
//
//     void Awake()
//     {
//         options = new InterpreterOptions()
//         {
//             threads = Mathf.Max(1, SystemInfo.processorCount - 1),
//         };
//         // GPU/NNAPI/Metal delegates can be added later, start with CPU for correctness
//         // options.AddGpuDelegate(); // Android GPU delegate (if supported)
//         // options.AddNNAPIDelegate(); // Android NNAPI
//         // options.AddMetalDelegate(); // iOS/macOS Metal
//
//         interpreter = new Interpreter(tfliteModel.bytes, options);
//
//         // Query input/output
//         inputInfo  = interpreter.GetInputTensorInfo(0);
//         outputInfo = interpreter.GetOutputTensorInfo(0);
//         Debug.Log($"[TFLite] Input: {inputInfo.name} {inputInfo.type} [{string.Join(",", inputInfo.shape)}]");
//         Debug.Log($"[TFLite] Output: {outputInfo.name} {outputInfo.type} [{string.Join(",", outputInfo.shape)}]");
//
//         // Determine rank and type. Many TFLite models are NHWC-like with a leading batch dim.
//         inputIs4D   = inputInfo.shape.Length == 4;               // expect [1,T,543,3]
//         inputIsFloat = inputInfo.type == TensorType.Float32;
//
//         // Quantization (for INT8/UINT8 models)
//         if (!inputIsFloat && inputInfo.quantizationParams != null)
//         {
//             inScale = inputInfo.quantizationParams.scale;
//             inZero  = inputInfo.quantizationParams.zeroPoint;
//             Debug.Log($"[TFLite] Quantized input scale={inScale} zeroPoint={inZero}");
//         }
//
//         // Resize input tensor if it’s dynamic or wrong T
//         var desired = inputIs4D ? new int[] { 1, T, LM, C } : new int[] { T, LM, C };
//         bool needsResize = false;
//         if (inputInfo.shape.Length != desired.Length) needsResize = true;
//         else for (int i = 0; i < desired.Length; i++) if (inputInfo.shape[i] != desired[i]) { needsResize = true; break; }
//
//         if (needsResize)
//         {
//             interpreter.ResizeInputTensor(0, desired);
//         }
//         interpreter.AllocateTensors(); // must re-allocate after any resize
//
//         // Prepare buffers
//         seqBuffer = new float[T * LM * C];
//
//         if (labelsText != null)
//             labels = labelsText.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
//     }
//
//     // Call this each frame with a single frame already packed as face->pose->left->right (length = 543*3)
//     public void PushFrame(float[] frame543x3)
//     {
//         if (frame543x3 == null || frame543x3.Length != LM * C) return;
//         int frameSize = LM * C;
//         // slide left and append
//         Buffer.BlockCopy(seqBuffer, frameSize * sizeof(float), seqBuffer, 0, (T - 1) * frameSize * sizeof(float));
//         Buffer.BlockCopy(frame543x3, 0, seqBuffer, (T - 1) * frameSize, frameSize);
//     }
//
//     public (int topIndex, float topProb, float[] probs) Predict()
//     {
//         // Upload input
//         if (inputIsFloat)
//         {
//             if (inputIs4D)
//             {
//                 // input expects [1,T,543,3] but our buffer is [T,543,3] flattened
//                 // SetInputTensorData will accept flat arrays as long as sizes match
//                 interpreter.SetInputTensorData(0, seqBuffer);
//             }
//             else
//             {
//                 interpreter.SetInputTensorData(0, seqBuffer);
//             }
//         }
//         else
//         {
//             // Quantized input: map float [0..1]-like to int8/uint8 using scale/zeroPoint
//             // NOTE: this assumes your floats are already in the trained normalization domain.
//             var qbuf = Quantize(seqBuffer, inScale, inZero, inputInfo.type == TensorType.UInt8);
//             interpreter.SetInputTensorData(0, qbuf);
//         }
//
//         // Inference
//         interpreter.Invoke();
//
//         // Fetch output
//         float[] logitsOrProbs = new float[outputInfo.shape[outputInfo.shape.Length - 1]]; // expect 250
//         interpreter.GetOutputTensorData(0, logitsOrProbs);
//
//         // If the model outputs logits, apply softmax
//         float[] probs = Softmax(logitsOrProbs);
//
//         // Argmax
//         int bestIdx = 0; float bestVal = probs[0];
//         for (int i = 1; i < probs.Length; i++) if (probs[i] > bestVal) { bestVal = probs[i]; bestIdx = i; }
//
//         if (labels != null && bestIdx < labels.Length) Debug.Log($"Pred: {labels[bestIdx]} p={bestVal:0.000}");
//         else Debug.Log($"Pred class #{bestIdx} p={bestVal:0.000}");
//
//         return (bestIdx, bestVal, probs);
//     }
//
//     static float[] Softmax(float[] x)
//     {
//         float max = float.NegativeInfinity;
//         for (int i = 0; i < x.Length; i++) if (x[i] > max) max = x[i];
//         double sum = 0;
//         var y = new float[x.Length];
//         for (int i = 0; i < x.Length; i++) sum += Math.Exp(x[i] - max);
//         for (int i = 0; i < x.Length; i++) y[i] = (float)(Math.Exp(x[i] - max) / sum);
//         return y;
//     }
//
//     static Array Quantize(float[] src, float scale, int zero, bool isUint8)
//     {
//         if (scale <= 0f) scale = 1f;
//         int n = src.Length;
//         if (isUint8)
//         {
//             byte[] dst = new byte[n];
//             for (int i = 0; i < n; i++)
//             {
//                 int q = Mathf.RoundToInt(src[i] / scale) + zero;
//                 dst[i] = (byte)Mathf.Clamp(q, byte.MinValue, byte.MaxValue);
//             }
//             return dst;
//         }
//         else
//         {
//             sbyte[] dst = new sbyte[n];
//             for (int i = 0; i < n; i++)
//             {
//                 int q = Mathf.RoundToInt(src[i] / scale) + zero;
//                 dst[i] = (sbyte)Mathf.Clamp(q, sbyte.MinValue, sbyte.MaxValue);
//             }
//             return dst;
//         }
//     }
//
//     void OnDestroy()
//     {
//         interpreter?.Dispose();
//     }
// }
