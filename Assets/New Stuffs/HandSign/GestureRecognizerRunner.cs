using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Tasks.Vision.GestureRecognizer;
using Stopwatch = System.Diagnostics.Stopwatch;
using TMPro;

namespace Mediapipe.Unity.Tutorial
{
  public class GestureRecognizerRunner : MonoBehaviour
  {
    [SerializeField] private RawImage screen;
    [SerializeField] private TextAsset modelAsset;
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] TextMeshProUGUI alph;

    private WebCamTexture webCamTexture;

    private IEnumerator Start()
    {
      // 1. Webcam setup
      webCamTexture = new WebCamTexture(WebCamTexture.devices[0].name, width, height, fps);
      webCamTexture.Play();
      yield return new WaitUntil(() => webCamTexture.width > 16);
      screen.texture = webCamTexture;

      // 2. Configure GestureRecognizer Options
      var options = new GestureRecognizerOptions(
        baseOptions: new Tasks.Core.BaseOptions(
          Tasks.Core.BaseOptions.Delegate.CPU,
          modelAssetBuffer: modelAsset.bytes
        ),
        runningMode: Tasks.Vision.Core.RunningMode.VIDEO,
        numHands: 2 // track 2 hands
      );

      using var gestureRecognizer = GestureRecognizer.CreateFromOptions(options);

      var stopwatch = new Stopwatch();
      stopwatch.Start();

      using var textureFrame = new Experimental.TextureFrame(width, height, TextureFormat.RGBA32);
      var waitForEndOfFrame = new WaitForEndOfFrame();

      while (true)
      {
        // 3. Prepare Image
        textureFrame.ReadTextureOnCPU(webCamTexture, flipHorizontally: false, flipVertically: true);
        using var image = textureFrame.BuildCPUImage();

        // 4. Run Gesture Detection
        var result = gestureRecognizer.RecognizeForVideo(image, stopwatch.ElapsedMilliseconds);
        // 5. Debug gestures
        if (result.gestures?.Count > 0)
        {
          foreach (var gesture in result.gestures)
          {
            var topGesture = gesture.categories[0]; // most confident
            alph.text = topGesture.categoryName;
            Debug.Log($"Detected: {topGesture.categoryName}");
          }
        }

        yield return waitForEndOfFrame;
      }
    }

    private void OnDestroy()
    {
      webCamTexture?.Stop();
    }
  }
}
