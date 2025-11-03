using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.Holistic; 

[RequireComponent(typeof(HolisticTrackingSolution))]
public class DataManager : MonoBehaviour
{
    public enum ProcessingMode
    {
        SkipExisting,
        OverwriteExisting
    }

    [Header("Configuration")]
    [Tooltip("Controls the playback speed of the videos during extraction. 1 is normal speed, 0.5 is half speed.")]
    [Range(0.1f, 2f)]
    public float playbackSpeed = 0.75f;
    [Tooltip("Skip: Don't re-process videos that already have a CSV file. Overwrite: Re-process all videos.")]
    public ProcessingMode mode = ProcessingMode.SkipExisting;
    

    [Header("References")]
    [Tooltip("Drag the GameObject with the HolisticLandmarkListAnnotationController script here.")]
    public HolisticLandmarkListAnnotationController landmarkController;

    private HolisticTrackingSolution solution;
    private VideoSource videoSource;
    private bool isRecording = false;
    private VideoLandmarkData currentVideoData;

    // --- NEW: A list to safely store the entries for train.csv ---
    private List<string> processedVideoEntries = new List<string>();

    IEnumerator Start()
    {
        solution = GetComponent<HolisticTrackingSolution>();
        while (ImageSourceProvider.ImageSource == null || !ImageSourceProvider.ImageSource.isPrepared)
        {
            Debug.Log("DataManager is waiting for ImageSource to be prepared...");
            yield return new WaitForSeconds(0.5f); 
        }

        Debug.Log("<color=cyan>ImageSource is ready. Initializing DataManager.</color>");
        videoSource = (VideoSource)ImageSourceProvider.ImageSource;

        if (landmarkController == null)
        {
            Debug.LogError("DataManager is not configured correctly. Please assign the Landmark Controller.");
            this.enabled = false;
            yield break;
        }

        landmarkController.OnHolisticLandmarks += OnNewHolisticData;
        StartCoroutine(ProcessAllVideos());
    }

    /// <summary>
    /// This is automatically called by Unity when you stop playing in the editor or close the application.
    /// This is our new, safe way to save the manifest file.
    /// </summary>
    void OnApplicationQuit()
    {
        if (processedVideoEntries.Count > 0)
        {
            Debug.Log($"Application quitting. Saving {processedVideoEntries.Count} processed entries to train.csv...");
            RebuildTrainManifest();
        }
    }

    void OnDestroy()
    {
        if (landmarkController != null)
        {
            landmarkController.OnHolisticLandmarks -= OnNewHolisticData;
        }
    }
    
    private IEnumerator ProcessAllVideos()
    {
        var sourceCandidates = videoSource.sourceCandidateNames.ToList();
        string datasetPath = Path.Combine(Application.persistentDataPath, "ASL_Dataset");
        Directory.CreateDirectory(datasetPath);
        var existingFiles = new HashSet<string>(Directory.GetFiles(datasetPath, "*.csv").Select(Path.GetFileNameWithoutExtension));

        for (int i = 0; i < sourceCandidates.Count; i++)
        {
            videoSource.SelectSource(i);
            string currentSignLabel = videoSource.GetSignLabel();
            string videoFileName = videoSource.sourceName;
            string uniqueFileName = $"{currentSignLabel}_{videoFileName}";
            
            if (mode == ProcessingMode.SkipExisting && existingFiles.Contains(uniqueFileName))
            {
                Debug.Log($"<color=yellow>Skipping existing file: {uniqueFileName}.csv</color>");
                // --- MODIFIED: Still add skipped files to our list to ensure they are in the final train.csv ---
                processedVideoEntries.Add($"{uniqueFileName}.csv,{currentSignLabel}");
                continue;
            }

            solution.Stop();
            yield return new WaitForEndOfFrame();
            
            videoSource.SelectSource(i);
            Debug.Log($"Processing video {i + 1}/{sourceCandidates.Count}: '{uniqueFileName}'");
            currentVideoData = new VideoLandmarkData(currentSignLabel, uniqueFileName);
            
            solution.Play();
            yield return new WaitUntil(() => ImageSourceProvider.ImageSource.isPrepared);
            
            var videoPlayer = FindObjectOfType<UnityEngine.Video.VideoPlayer>();
            if (videoPlayer != null) {
                videoPlayer.playbackSpeed = playbackSpeed;
                videoPlayer.isLooping = false;
                videoPlayer.loopPointReached += OnVideoFinished;
            } else {
                 Debug.LogError("Could not find the VideoPlayer component after starting solution.");
                 yield break;
            }
            
            isRecording = true;
            while(isRecording)
            {
                yield return null;
            }
        }
        
        Debug.Log("<color=green>--- All videos in queue have been processed! ---</color>");
        // --- MODIFIED: The final save will now happen automatically OnApplicationQuit. ---
    }
    
    private void OnVideoFinished(UnityEngine.Video.VideoPlayer source)
    {
        if (!isRecording) return;
        source.loopPointReached -= OnVideoFinished; 

        string datasetPath = Path.Combine(Application.persistentDataPath, "ASL_Dataset");
        string outputPath = Path.Combine(datasetPath, $"{currentVideoData.UniqueFileName}.csv");
        DataWriter.WriteToCSV(currentVideoData, outputPath);
        
        // --- MODIFIED: Add the successfully processed file to our safe list. ---
        processedVideoEntries.Add($"{currentVideoData.UniqueFileName}.csv,{currentVideoData.SignName}");
        
        isRecording = false; 
    }
    
    /// <summary>
    /// This method is now called safely on quit. It uses the in-memory list.
    /// </summary>
    private void RebuildTrainManifest()
    {
        string datasetPath = Path.Combine(Application.persistentDataPath, "ASL_Dataset");
        string manifestPath = Path.Combine(datasetPath, "train.csv");
        
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("path,sign"); // CSV Header

        // Use the list of entries we collected, ensuring no duplicates.
        foreach (var entry in processedVideoEntries.Distinct())
        {
            stringBuilder.AppendLine(entry);
        }

        File.WriteAllText(manifestPath, stringBuilder.ToString());
        Debug.Log($"<color=cyan>Successfully saved 'train.csv' with {processedVideoEntries.Distinct().Count()} entries.</color>");
    }

    private void OnNewHolisticData(IReadOnlyList<NormalizedLandmark> face, IReadOnlyList<NormalizedLandmark> pose, IReadOnlyList<NormalizedLandmark> left, IReadOnlyList<NormalizedLandmark> right)
    {
        if (!isRecording) return;
        var frame = new FrameLandmarkData();
        if (pose != null) { foreach (var lm in pose) { frame.Landmarks.Add(new Landmark(lm)); } }
        if (face != null) { foreach (var lm in face) { frame.Landmarks.Add(new Landmark(lm)); } }
        if (left != null) { foreach (var lm in left) { frame.Landmarks.Add(new Landmark(lm)); } } else { for(int i=0; i<21; i++) frame.Landmarks.Add(Landmark.Empty); }
        if (right != null) { foreach (var lm in right) { frame.Landmarks.Add(new Landmark(lm)); } } else { for(int i=0; i<21; i++) frame.Landmarks.Add(Landmark.Empty); }
        currentVideoData.Frames.Add(frame);
    }
}

