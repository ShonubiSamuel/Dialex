using System;
using System.Linq;
using System.Collections.Generic;
using Mediapipe;
using UnityEngine;
using Unity.InferenceEngine;                 // Unity Inference Engine (ModelAsset, Worker, Tensor)
using Mediapipe.Unity;                      // your HolisticLandmarkListAnnotationController
using Newtonsoft.Json;

public class ISLRWithInferenceEngine : MonoBehaviour
{
    [Header("Wiring")]
    public HolisticLandmarkListAnnotationController holistic;     // assign from your scene
    public ModelAsset modelAsset;                                  // drag your ONNX ModelAsset
    public TextAsset labelMapJson;                                 // (optional) sign_to_prediction_index_map.json as TextAsset (Resources or direct)

    [Header("Model / IO")]
    public bool modelHasPreprocess = true;                         // ONNX already includes Preprocess (expects [1,T,543,3])
    public BackendType backend = BackendType.GPUCompute;           // CPU for determinism, GPU for speed
    [Range(64,1024)] public int T = 384;                           // time steps (must match export)
    public int N = 543;                                            // landmarks
    public int C = 3;                                              // x,y,z
    public int numClasses = 250;

    [Header("Prediction cadence")]
    [Tooltip("Seconds between predictions")] public float predictInterval = 0.15f;
    [Tooltip("Only emit label when stable for this many consecutive frames")]
    public int stableCountNeeded = 3;
    [Range(0,1)] public float minConfidence = 0.55f;

    [Header("Smoothing (EMA on logits)")]
    [Range(0f,1f)] public float emaAlpha = 0.6f;

    [Header("Debug")]
    public bool debugDetections = false;      // print landmark counts + a few samples
    public bool debugTopK = true;             // print Top-5 with probs

    // ring buffer [T, N, C]
    float[,,] frames;
    int writeIdx = 0;
    int framesFilled = 0;

    // inference
    Model runtimeModel;
    Worker worker;
    Tensor<float> inputTensor;                // shape [1,T,543,3] or [1,T,6*P]
    float[] logits;                           // latest logits

    // label map
    Dictionary<int, string> idx2label;

    // timing
    float timer = 0f;

    // smoothing & debouncing
    double[] emaLogits;
    int lastIdx = -1, stableCount = 0;

    // features mode dims
    int P;                // selected landmark count (when modelHasPreprocess==false)
    int CHANNELS;         // 6*P

    // ===== Unity lifecycle =====

    void Awake()
    {
        if (holistic == null)
        {
            Debug.LogError("HolisticLandmarkListAnnotationController is not assigned.");
            enabled = false; return;
        }

        frames = new float[T, N, C];
        logits = new float[numClasses];

        LoadLabelMap();

        // prepare model & worker
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, backend);

        // allocate input tensor according to model type
        if (modelHasPreprocess)
        {
            // ONNX expects raw landmarks [1,T,543,3]
            inputTensor = new Tensor<float>(new TensorShape(1, T, N, C));
        }
        else
        {
            // ONNX expects features [1,T,6*P]
            P = ISLRPreprocess.NumSelectedPoints;
            CHANNELS = 6 * P;
            inputTensor = new Tensor<float>(new TensorShape(1, T, CHANNELS));
        }

        // subscribe to mediapipe stream
        holistic.OnHolisticLandmarks += OnHolistic;
    }

    void OnDestroy()
    {
        if (holistic != null) holistic.OnHolisticLandmarks -= OnHolistic;
        inputTensor?.Dispose();
        worker?.Dispose();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= predictInterval)
        {
            timer = 0f;
            var (idx, label) = PredictOnce();
            if (idx >= 0)
                Debug.Log($"ASL: {label} (#{idx})");
        }
    }

    // ===== Stream ingest =====

    void OnHolistic(
      IReadOnlyList<NormalizedLandmark> face,      // 468
      IReadOnlyList<NormalizedLandmark> pose,      // 33
      IReadOnlyList<NormalizedLandmark> leftHand,  // 21
      IReadOnlyList<NormalizedLandmark> rightHand  // 21
    )
    {
        // write current frame into ring buffer
        WriteFrame(face, pose, leftHand, rightHand, writeIdx);
        writeIdx = (writeIdx + 1) % T;
        framesFilled = Mathf.Min(framesFilled + 1, T);

        if (debugDetections)
        {
            int f = face?.Count ?? 0, l = leftHand?.Count ?? 0, p = pose?.Count ?? 0, r = rightHand?.Count ?? 0;
            var (fx, fy) = SampleXY(face, 0);
            var (lx, ly) = SampleXY(leftHand, 0);
            var (rx, ry) = SampleXY(rightHand, 0);
            Debug.Log($"Detections: face={f}/468, L={l}/21, pose={p}/33, R={r}/21 | face[0]=({fx:0.3},{fy:0.3}) L0=({lx:0.3},{ly:0.3}) R0=({rx:0.3},{ry:0.3})");
        }
    }

    void WriteFrame(
        IReadOnlyList<NormalizedLandmark> face,
        IReadOnlyList<NormalizedLandmark> pose,
        IReadOnlyList<NormalizedLandmark> leftHand,
        IReadOnlyList<NormalizedLandmark> rightHand,
        int tIndex)
    {
        // zero-fill whole frame first
        for (int n = 0; n < N; n++) { frames[tIndex, n, 0] = 0f; frames[tIndex, n, 1] = 0f; frames[tIndex, n, 2] = 0f; }

        CopyBlock(face,      0,   468, tIndex);
        CopyBlock(leftHand,  468, 21,  tIndex);
        CopyBlock(pose,      489, 33,  tIndex);
        CopyBlock(rightHand, 522, 21,  tIndex, true);
    }

    // Call for RH with show=true: CopyBlock(rightHand, 522, 21, tIndex, show:true);

    static readonly int[] RH_DEBUG = { 0, 4, 8, 12, 16, 20 }; // wrist + tips (thumb, index, middle, ring, pinky)

    void CopyBlock(IReadOnlyList<NormalizedLandmark> src, int startIndex, int expectedCount, int tIndex, bool show = false)
    {
        if (src == null) return;
        int count = Math.Min(src.Count, expectedCount);

        if (show) Debug.Log($"RH frame={tIndex} (start={startIndex})");

        for (int i = 0; i < count; i++)
        {
            var lm = src[i];
            int n = startIndex + i;
            float x = San(lm.X), y = San(lm.Y), z = San(lm.Z);
            frames[tIndex, n, 0] = x;
            frames[tIndex, n, 1] = y;
            frames[tIndex, n, 2] = z;

            if (show && RH_DEBUG.Contains(i))
                Debug.Log($"  n={n} (i={i})  x={x:0.000}  y={y:0.000}  z={z:0.000}");
        }

        if (show)
        {
            // per-frame quick stats over the whole right hand block
            int off = startIndex;
            double sx = 0, sy = 0; int k = 0;
            for (int i = 0; i < count; i++) { sx += frames[tIndex, off+i, 0]; sy += frames[tIndex, off+i, 1]; k++; }
            Debug.Log($"  RH mean x={sx/k:0.000}, y={sy/k:0.000}");
        }
    }

    static float San(float v) => (float.IsNaN(v) || float.IsInfinity(v)) ? 0f : v;
    static (float, float) SampleXY(IReadOnlyList<NormalizedLandmark> list, int idx)
    {
        if (list == null || list.Count == 0 || idx >= list.Count) return (0, 0);
        return (list[idx].X, list[idx].Y);
    }

    // ===== Predict once =====

    private (int idx, string label) PredictOnce()
    {
        if (framesFilled == 0) return (-1, null);

        // unroll ring buffer chronologically into a contiguous [T, N, C] window
        var window = new float[T, N, C];
        for (int t = 0; t < T; t++)
        {
            int srcT = (framesFilled < T) ? t : (writeIdx + t) % T;
            for (int n = 0; n < N; n++)
            {
                window[t, n, 0] = frames[srcT, n, 0];
                window[t, n, 1] = frames[srcT, n, 1];
                window[t, n, 2] = frames[srcT, n, 2];
            }
        }

        // upload depending on model type
        if (modelHasPreprocess)
        {
            // raw landmarks path [1,T,543,3]
            // flatten time-major into tensor’s internal layout via Upload(float[])
            // but Tensor<float>(1,T,N,C).Upload expects contiguous array; we’ll stream via a temp buffer:
            var buf = new float[T * N * C];
            int d = 0;
            for (int t = 0; t < T; t++)
                for (int n = 0; n < N; n++)
                {
                    buf[d++] = window[t, n, 0];
                    buf[d++] = window[t, n, 1];
                    buf[d++] = window[t, n, 2];
                }
            inputTensor.Upload(buf);
        }
        else
        {
            // features path [1,T,6*P]
            float[] feat = ISLRPreprocess.BuildFeatures(window, T);
            inputTensor.Upload(feat);
        }

        // run inference
        worker.Schedule(inputTensor);
        var o = worker.PeekOutput() as Tensor<float>;
        var arr = o.DownloadToArray(); // logits [numClasses]

        // EMA smoothing
        UpdateEma(arr);
        var smoothed = emaLogits.Select(v => (float)v).ToArray();

        // Top-K
        var (bestIdx, bestLabel, bestProb, top) = TopK(smoothed, 5);
        if (debugTopK)
        {
            string topStr = string.Join(", ", top.Select(t => $"{t.label}({t.prob:0.00})"));
            Debug.Log($"Top1: {bestLabel} #{bestIdx} p={bestProb:0.00} | Top5: {topStr}");
        }

        // debounced announce
        if (ShouldAnnounce(bestIdx, bestProb))
            return (bestIdx, bestLabel);

        return (-1, null);
    }

    // ===== Smoothing & TopK =====

    void UpdateEma(float[] newLogits)
    {
        if (emaLogits == null || emaLogits.Length != newLogits.Length)
            emaLogits = new double[newLogits.Length];
        for (int i = 0; i < newLogits.Length; i++)
            emaLogits[i] = emaAlpha * newLogits[i] + (1 - emaAlpha) * emaLogits[i];
    }

    bool ShouldAnnounce(int idx, double prob)
    {
        if (prob < minConfidence) { stableCount = 0; lastIdx = -1; return false; }
        if (idx == lastIdx) stableCount++;
        else { lastIdx = idx; stableCount = 1; }
        return stableCount >= stableCountNeeded;
    }

    (int idx, string label, double prob, (int idx, string label, double prob)[] topk)
    TopK(float[] logitsIn, int k = 5)
    {
        double maxLogit = logitsIn.Max();
        var exps = logitsIn.Select(v => Math.Exp(v - maxLogit)).ToArray();
        double sum = exps.Sum();
        var probs = exps.Select(v => v / sum).ToArray();

        var order = Enumerable.Range(0, probs.Length).OrderByDescending(i => probs[i]).Take(k).ToArray();
        var top = order.Select(i => (i, LabelOf(i), probs[i])).ToArray();
        return (top[0].Item1, top[0].Item2, top[0].Item3, top);
    }

    string LabelOf(int idx) => (idx2label != null && idx2label.TryGetValue(idx, out var s)) ? s : idx.ToString();

    void LoadLabelMap()
    {
        // 1) prefer assigned TextAsset (dragged in Inspector)
        if (labelMapJson != null)
        {
            var signToIdx = JsonConvert.DeserializeObject<Dictionary<string, int>>(labelMapJson.text);
            idx2label = signToIdx.ToDictionary(kv => kv.Value, kv => kv.Key);
            return;
        }

        // 2) fallback to Resources/sign_to_prediction_index_map.json
        var res = Resources.Load<TextAsset>("sign_to_prediction_index_map");
        if (res != null)
        {
            var signToIdx = JsonConvert.DeserializeObject<Dictionary<string, int>>(res.text);
            idx2label = signToIdx.ToDictionary(kv => kv.Value, kv => kv.Key);
            return;
        }

        // 3) fallback to index as string
        Debug.LogWarning("Label map not found; will display class indices.");
        idx2label = new Dictionary<int, string>();
    }

    // ======== C# mirror of your Python `Preprocess` (used only when modelHasPreprocess==false) ========


}