// using UnityEngine;
// using System;
// using System.Collections.Generic;
// using Mediapipe;
// using Mediapipe.Unity;
//
// public class HolisticToInferenceBridge : MonoBehaviour
// {
//     [SerializeField] HolisticLandmarkListAnnotationController source;   // your controller
//     [SerializeField] ASLSignDetector detector;                          // the Inference Engine script from before
//
//     const int FACE = 468, POSE = 33, HAND = 21, C = 3;
//
//     void OnEnable()
//     {
//         if (source != null) source.OnHolisticLandmarks += Handle;
//     }
//
//     void OnDisable()
//     {
//         if (source != null) source.OnHolisticLandmarks -= Handle;
//     }
//
//     void Handle(IReadOnlyList<NormalizedLandmark> face,
//         IReadOnlyList<NormalizedLandmark> pose,
//         IReadOnlyList<NormalizedLandmark> left,
//         IReadOnlyList<NormalizedLandmark> right)
//     {
//         // Flatten to [543*3] in order: face -> pose -> left -> right
//         var frame = new float[(FACE + POSE + HAND + HAND) * C];
//         int o = 0;
//
//         WriteBlock(face, FACE, frame, ref o);
//         WriteBlock(pose, POSE, frame, ref o);
//         WriteBlock(left, HAND, frame, ref o);
//         WriteBlock(right, HAND, frame, ref o);
//
//         detector?.PushFrame(frame);
//         // (Optionally call detector.Predict() here at a chosen cadence, e.g., every N frames)
//     }
//
//     static void WriteBlock(IReadOnlyList<NormalizedLandmark> list, int expectedCount, float[] dst, ref int o)
//     {
//         if (list == null || list.Count != expectedCount)
//         {
//             // zero-fill missing block to keep shape consistent
//             for (int i = 0; i < expectedCount * C; i++) dst[o++] = 0f;
//             return;
//         }
//
//         // NormalizedLandmark has x,y in [0,1]; z is relative depth. Use as-is to match training.
//         for (int i = 0; i < expectedCount; i++)
//         {
//             var lm = list[i];
//             dst[o++] = lm.X;
//             dst[o++] = lm.Y;
//             dst[o++] = lm.Z;
//         }
//     }
// }