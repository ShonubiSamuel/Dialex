// using System;
// using System.Linq;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
//
// public class PredictionUIController : MonoBehaviour
// {
//     [Header("Wire these")]
//     public ISLRWithInferenceEngine recognizer; // your script
//     public TextMeshProUGUI statusText;
//     public TextMeshProUGUI finalLabelText;
//     public TextMeshProUGUI finalConfText;
//     public Transform topKContainer;          // parent with VerticalLayout
//     public TopKItem[] topKItems;             // size 5, or we can Instantiate prefabs
//
//     [Header("Controls")]
//     public Slider smoothingSlider;
//     public Slider thresholdSlider;
//     public TextMeshProUGUI smoothingValue;
//     public TextMeshProUGUI thresholdValue;
//
//     [Header("Runtime")]
//     public string backendDisplay = "GPUCompute";
//
//     void Awake()
//     {
//         if (recognizer != null)
//         {
//             recognizer.OnSignPredicted += OnPredicted;
//         }
//         if (smoothingSlider != null)
//         {
//             smoothingSlider.onValueChanged.AddListener(v => {
//                 if (recognizer != null) recognizer.emaAlpha = v;
//                 if (smoothingValue) smoothingValue.text = v.ToString("0.00");
//             });
//             smoothingSlider.value = recognizer != null ? recognizer.emaAlpha : 0.6f;
//         }
//         if (thresholdSlider != null)
//         {
//             thresholdSlider.onValueChanged.AddListener(v => {
//                 if (recognizer != null) recognizer.minConfidence = v;
//                 if (thresholdValue) thresholdValue.text = v.ToString("0.00");
//             });
//             thresholdSlider.value = recognizer != null ? recognizer.minConfidence : 0.55f;
//         }
//     }
//
//     void OnDestroy()
//     {
//         if (recognizer != null) recognizer.OnSignPredicted -= OnPredicted;
//     }
//
//     void Update()
//     {
//         // lightweight status (FPS + backend)
//         int fps = (int)(1f / Mathf.Max(Time.unscaledDeltaTime, 1e-3f));
//         if (statusText != null)
//             statusText.text = $"{backendDisplay} | {fps} FPS";
//         
//         // even when not announcing, show live top-k for feedback
//         if (recognizer != null && recognizer.LogitsAvailable(out var logits))
//             UpdateTopK(logits);
//     }
//
//     private void OnPredicted(int idx, string label, double prob)
//     {
//         if (finalLabelText) finalLabelText.text = label?.ToUpperInvariant() ?? "—";
//         if (finalConfText)  finalConfText.text  = prob.ToString("0.00");
//         // Optionally append to history UI here…
//     }
//
//     private void UpdateTopK(float[] logits)
//     {
//         if (topKItems == null || topKItems.Length == 0) return;
//
//         // compute softmax
//         float max = logits.Max();
//         double sum = 0;
//         var probs = new double[logits.Length];
//         for (int i = 0; i < logits.Length; i++) { var v = Math.Exp(logits[i] - max); probs[i] = v; sum += v; }
//         for (int i = 0; i < probs.Length; i++) probs[i] /= sum;
//
//         var idxs = Enumerable.Range(0, probs.Length)
//                              .OrderByDescending(i => probs[i])
//                              .Take(topKItems.Length)
//                              .ToArray();
//
//         for (int r = 0; r < topKItems.Length; r++)
//         {
//             int i = idxs[r];
//             string name = recognizer.TryLabel(i, out var lbl) ? lbl : i.ToString();
//             topKItems[r].SetRow(name, (float)probs[i]);
//         }
//     }
// }
