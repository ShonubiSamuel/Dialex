using Mediapipe;
using System.Collections.Generic;

// Represents a single landmark point (x, y, z)
[System.Serializable]
public struct Landmark
{
    public float x;
    public float y;
    public float z;

    public Landmark(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    // Constructor to convert from MediaPipe's NormalizedLandmark
    public Landmark(NormalizedLandmark lm)
    {
        this.x = lm.X;
        this.y = lm.Y;
        this.z = lm.Z;
    }

    // A static property for an empty landmark (used when hands aren't detected)
    public static Landmark Empty => new Landmark(0, 0, 0);
}

// Represents all landmarks collected in a single frame of a video
[System.Serializable]
public class FrameLandmarkData
{
    public List<Landmark> Landmarks { get; private set; }

    public FrameLandmarkData()
    {
        // 543 = 33 pose + 468 face + 21 left hand + 21 right hand
        Landmarks = new List<Landmark>(543);
    }
}

// Represents all data for a single video, including all its frames
[System.Serializable]
public class VideoLandmarkData
{
    public string SignName { get; private set; }
    // NEW: Stores the unique filename for the CSV (e.g., "dog_video1.csv")
    public string UniqueFileName { get; private set; }
    public List<FrameLandmarkData> Frames { get; private set; }

    // This is the updated constructor that matches the call in DataManager.cs
    public VideoLandmarkData(string signName, string uniqueFileName)
    {
        SignName = signName;
        UniqueFileName = uniqueFileName;
        Frames = new List<FrameLandmarkData>();
    }
}