using System.Collections.Generic;
using UnityEngine;

public class SignTestRunner : MonoBehaviour
{
    public SignAnimationPlayer player;

    void Start()
    {
        SignMappingLoader loader = FindFirstObjectByType<SignMappingLoader>();
        loader.Load();

        List<string> gloss = new List<string> { "thank you", "my", "name", "samuel" };

        SignMatcher matcher = new SignMatcher(SignMappingLoader.SignMap);
        List<string> animationSequence = matcher.GetAnimationSequence(gloss);

        Debug.Log("Animation Sequence:");
        foreach (string anim in animationSequence)
        {
            Debug.Log(anim);
        }
        
        player.Init();
        player.PlaySequence(animationSequence);
    }
}