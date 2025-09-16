using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using Newtonsoft.Json;

public class ISLR_AuditReplay : MonoBehaviour
{
    [Header("Files inside StreamingAssets/subDir")]
    public string subDir  = "islr_test";
    public string onnx    = "islr_unity.onnx";                 // if you load via ModelAsset, you can ignore this
    public string binName = "sample_tensor.bin";
    public string labelMap= "sign_to_prediction_index_map.json";
    public string metadata= "metadata.json";
    public string refTopK = "ref_topk.json";                   // optional: compare with Kaggle

    [Header("Inference")]
    public ModelAsset modelAsset;                              // drag your ONNX asset here
    public BackendType backend = BackendType.CPU;             // CPU for exact reproducibility

    Worker worker;
    Dictionary<int,string> idx2label;

    [Serializable] class Shape { public int B, T, N, C; }
    [Serializable] class DatasetRow { public long participant_id; public long sequence_id; public string sign; public string path; }
    [Serializable] class Metadata { public DatasetRow dataset_row; public Shape shape; }

    //[Serializable] class TopKItem { public int idx; public double prob; public string label; }
    [Serializable] class TopKFile { public List<TopKItem> topk; }  // if you wrote a list root, adapt accordingly

    [Serializable] class TopKItem { public int idx; public double prob; public string label; }
    [Serializable] class TopKWrapper { public List<TopKItem> top5; }


    void Start()
    {
        string basePath = Path.Combine(Application.streamingAssetsPath, subDir);

        // 1) Label map
        idx2label = LoadIdx2Label(basePath, labelMap);

        // 2) Metadata (just to echo)
        var meta = JsonConvert.DeserializeObject<Metadata>(File.ReadAllText(Path.Combine(basePath, metadata)));
        Debug.Log($"Dataset row → participant={meta.dataset_row.participant_id}, sequence={meta.dataset_row.sequence_id}, sign='{meta.dataset_row.sign}', path='{meta.dataset_row.path}'");
        Debug.Log($"Shape (Unity input) → B={meta.shape.B}, T={meta.shape.T}, N={meta.shape.N}, C={meta.shape.C}");

        // 3) Model + worker
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, backend);

        // 4) Load the exact tensor
        var raw = File.ReadAllBytes(Path.Combine(basePath, binName));
        var data = new float[raw.Length / 4];
        Buffer.BlockCopy(raw, 0, data, 0, raw.Length);
        using var input = new Tensor<float>(new TensorShape(meta.shape.B, meta.shape.T, meta.shape.N, meta.shape.C));
        input.Upload(data);

        // 5) Run
        worker.Schedule(input);
        var output = worker.PeekOutput() as Tensor<float>;
        var logits = output.DownloadToArray(); // [250]

        // 6) Softmax + Top5 (Unity)
        var (order, probs) = TopKWithProbs(logits, 5);
        // old (wrong): {probs[i]:0.9}
        string unityTop5 = string.Join(", ", order.Select(i =>
            $"({i}, {probs[i]:0.000}, '{LabelOf(i)}')"));
        Debug.Log("Unity Top5: " + unityTop5);

        Buffer.BlockCopy(raw, 0, data, 0, raw.Length);

// Shape constants
        int T = 384, N = 543, C = 3;

// Print first 2 frames, first 5 landmarks
        for (int t = 0; t < 2; t++)
        {
            Debug.Log($"Frame {t}:");
            for (int n = 0; n < 5; n++)
            {
                int idx = (t * N + n) * C;
                float x = data[idx + 0];
                float y = data[idx + 1];
                float z = data[idx + 2];
                Debug.Log($"  Landmark {n}: x={x:F6}, y={y:F6}, z={z:F6}");
            }
        }
        // 7) (Optional) Compare with Kaggle topk
        string refTopKPath = Path.Combine(basePath, refTopK);
     
        if (File.Exists(refTopKPath))
        {
            var refK = JsonConvert.DeserializeObject<List<TopKItem>>(File.ReadAllText(refTopKPath));
            string KaggleTop5Str = string.Join(", ", refK.Select(it => $"({it.idx}, {it.prob:0.9}, '{it.label}')"));
            Debug.Log("Kaggle Top5: " + KaggleTop5Str);

            // quick match check
            bool sameOrder = order.Zip(refK, (u, r) => u == r.idx).All(b => b);
            Debug.Log($"Top-5 indices match: {sameOrder}");
        }
        else
        {
            Debug.LogWarning("ref_topk.json not found — skipping comparison.");
        }
        

  
    }

    Dictionary<int,string> LoadIdx2Label(string basePath, string file)
    {
        var signToIdx = JsonConvert.DeserializeObject<Dictionary<string,int>>(
            File.ReadAllText(Path.Combine(basePath, file)));
        return signToIdx.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    string LabelOf(int idx) => idx2label != null && idx2label.TryGetValue(idx, out var s) ? s : idx.ToString();

    (int[] order, double[] probs) TopKWithProbs(float[] logits, int k)
    {
        double maxLogit = logits.Max();
        var exps  = logits.Select(v => Math.Exp(v - maxLogit)).ToArray();
        var sum   = exps.Sum();
        var probs = exps.Select(v => v / sum).ToArray();
        var order = Enumerable.Range(0, logits.Length)
                              .OrderByDescending(i => probs[i])
                              .Take(k)
                              .ToArray();
        return (order, probs);
    }

    void OnDestroy() => worker?.Dispose();
}