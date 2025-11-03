using System.IO;
using System.Text;
using UnityEngine;

public static class DataWriter
{
    /// <summary>
    /// Writes the collected landmark data for a single video to a specified CSV file path.
    /// This updated method now takes the full file path as a parameter.
    /// </summary>
    /// <param name="videoData">The landmark data for the video.</param>
    /// <param name="filePath">The full path where the CSV file will be saved.</param>
    public static void WriteToCSV(VideoLandmarkData videoData, string filePath)
    {
        var csvBuilder = new StringBuilder();
        // Add the header row. This detailed format is better for potential debugging.
        csvBuilder.AppendLine("frame,type,landmark_index,x,y,z");

        int frameIndex = 0;
        foreach (var frame in videoData.Frames)
        {
            // The order here MUST match the order they are added in DataManager:
            // POSE (33), FACE (468), LEFT_HAND (21), RIGHT_HAND (21)
            int landmarkIndex = 0;
            
            // Pose
            for (int i = 0; i < 33 && landmarkIndex < frame.Landmarks.Count; i++, landmarkIndex++)
            {
                var lm = frame.Landmarks[landmarkIndex];
                csvBuilder.AppendLine($"{frameIndex},pose,{i},{lm.x},{lm.y},{lm.z}");
            }
            // Face
            for (int i = 0; i < 468 && landmarkIndex < frame.Landmarks.Count; i++, landmarkIndex++)
            {
                var lm = frame.Landmarks[landmarkIndex];
                csvBuilder.AppendLine($"{frameIndex},face,{i},{lm.x},{lm.y},{lm.z}");
            }
            // Left Hand
            for (int i = 0; i < 21 && landmarkIndex < frame.Landmarks.Count; i++, landmarkIndex++)
            {
                var lm = frame.Landmarks[landmarkIndex];
                csvBuilder.AppendLine($"{frameIndex},left_hand,{i},{lm.x},{lm.y},{lm.z}");
            }
            // Right Hand
            for (int i = 0; i < 21 && landmarkIndex < frame.Landmarks.Count; i++, landmarkIndex++)
            {
                var lm = frame.Landmarks[landmarkIndex];
                csvBuilder.AppendLine($"{frameIndex},right_hand,{i},{lm.x},{lm.y},{lm.z}");
            }
            
            frameIndex++;
        }

        File.WriteAllText(filePath, csvBuilder.ToString());
        Debug.Log($"<color=green>Successfully saved data to: {filePath}</color>");
    }
}

