// using UnityEngine;
// using System;
// using System.Collections;
// using System.IO;
// using UnityEngine.UI;
// #if UNITY_WEBGL && !UNITY_EDITOR
// using uMicrophoneWebGL;
// #endif
//
// public class MicRecorder : MonoBehaviour
// {
//     public int timeToRecord = 20;
//     public int sampleRate = 16000;
//     private AudioClip recording;
//     private string microphoneName;
//     private string filePath;
//     private bool isRecording = false;
//
//     public Action<string> OnRecordingFinished;
//     public GlossPipelineManager glossManager;
//     public Image recordingImage;
//
// #if UNITY_WEBGL && !UNITY_EDITOR
//     private MicrophoneWebGL micWebGL;
//     private float[] webGLBuffer;
// #endif
//
//     void Awake()
//     {
//         filePath = Path.Combine(Application.persistentDataPath, "recorded.wav");
//
// #if UNITY_WEBGL && !UNITY_EDITOR
//         micWebGL = gameObject.AddComponent<MicrophoneWebGL>();
//         micWebGL.isAutoStart = false;
//         micWebGL.readyEvent.AddListener(() => Debug.Log("WebGL Microphone ready."));
//         micWebGL.dataEvent.AddListener(OnWebGLMicData);
// #else
//         microphoneName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
//         if (string.IsNullOrEmpty(microphoneName))
//         {
//             Debug.LogError("No microphone detected.");
//         }
// #endif
//     }
//
//     public void ToggleRecording()
//     {
//         if (isRecording)
//         {
//             recordingImage.color = Color.white;
//             StopRecording();
//         }
//         else
//         {
//             recordingImage.color = Color.green;
//             StartRecording();
//         }
//
//         isRecording = !isRecording;
//     }
//
//     public void StartRecording()
//     {
// #if UNITY_WEBGL && !UNITY_EDITOR
//         Debug.Log("Starting WebGL Microphone...");
//         micWebGL.Begin();
//         StartCoroutine(AutoStopRecordingAfterDuration(timeToRecord));
// #else
//         if (Microphone.IsRecording(microphoneName)) return;
//
//         Debug.Log("Recording started...");
//         recording = Microphone.Start(microphoneName, false, timeToRecord, sampleRate);
//         StartCoroutine(AutoStopRecordingAfterDuration(timeToRecord));
// #endif
//     }
//
//     public void StopRecording()
//     {
// #if UNITY_WEBGL && !UNITY_EDITOR
//         Debug.Log("Stopping WebGL Microphone...");
//         micWebGL.End();
//         SaveWebGLBufferToWav();
// #else
//         if (!Microphone.IsRecording(microphoneName)) return;
//
//         Debug.Log("Recording stopped.");
//         Microphone.End(microphoneName);
//         SaveWavFile();
// #endif
//     }
//
//     private IEnumerator AutoStopRecordingAfterDuration(int seconds)
//     {
//         yield return new WaitForSeconds(seconds);
//         if (isRecording)
//         {
//             StopRecording();
//             isRecording = false;
//         }
//     }
//
//     private void SaveWavFile()
//     {
//         Debug.Log("Saving WAV file...");
//         byte[] wavData = WavUtility.FromAudioClip(recording);
//         File.WriteAllBytes(filePath, wavData);
//         OnRecordingFinished?.Invoke(filePath);
//         glossManager.Audio(filePath);
//     }
//
// #if UNITY_WEBGL && !UNITY_EDITOR
//     private void OnWebGLMicData(float[] buffer)
//     {
//         // Cache the last buffer received
//         webGLBuffer = buffer;
//     }
//
//     private void SaveWebGLBufferToWav()
//     {
//         if (webGLBuffer == null || webGLBuffer.Length == 0)
//         {
//             Debug.LogWarning("No audio data captured in WebGL.");
//             return;
//         }
//
//         Debug.Log("Saving WebGL WAV file...");
//         var clip = AudioClip.Create("WebGLRecording", webGLBuffer.Length, 1, sampleRate, false);
//         clip.SetData(webGLBuffer, 0);
//         byte[] wavData = WavUtility.FromAudioClip(clip);
//         File.WriteAllBytes(filePath, wavData);
//         OnRecordingFinished?.Invoke(filePath);
//         glossManager.Audio(filePath);
//     }
// #endif
// }
