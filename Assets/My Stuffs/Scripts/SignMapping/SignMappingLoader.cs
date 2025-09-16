using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SignMappingLoader 
{
    public static Dictionary<string, string> SignMap { get; private set; }

    [ContextMenu("Load Mapping")]
    public static void Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "sign_mappings.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SignMap = JsonUtilityWrapper.FromJson<Dictionary<string, string>>(json);
//            Debug.Log($"Loaded {SignMap.Count} sign mappings.");
        }
        else
        {
            Debug.LogError("sign_mappings.json not found.");
        }
    }
}

// Utility to parse Dictionary from JSON
public static class JsonUtilityWrapper
{
    public static T FromJson<T>(string json)
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
    }
}