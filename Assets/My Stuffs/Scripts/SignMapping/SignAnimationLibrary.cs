using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SignAnimationLibrary : MonoBehaviour
{
    public List<NamedClip> clips;

    private Dictionary<string, AnimationClip> clipMap;

    public void Init()
    {
        clipMap = new Dictionary<string, AnimationClip>();
        foreach (var item in clips)
        {
            if (string.IsNullOrEmpty(item.name) || item.clip == null)
            {
                Debug.LogWarning($"Missing name or clip: name = '{item.name}', clip = '{item.clip}'");
                continue;
            }

            string key = item.name.ToLower();
            Debug.Log($"Adding clip: {key} -> {item.clip.name}");
            clipMap[key] = item.clip;
        }
    }


    public AnimationClip GetClip(string name)
    {
        return clipMap.TryGetValue(name.ToLower(), out var clip) ? clip : null;
    }

    [System.Serializable]
    public struct NamedClip
    {
        public string name;
        public AnimationClip clip;
    }
}