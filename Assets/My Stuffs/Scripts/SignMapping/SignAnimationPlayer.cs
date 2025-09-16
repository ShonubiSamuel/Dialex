using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SignAnimationPlayer : MonoBehaviour
{
    public Animator animator;

    private AnimatorOverrideController overrideController;
    private ClipsOverrides clipsOverrides;

    private const int MaxSlots = 20;
    
    List<AnimationClip> animationClips;

    public AnimationClip idle;

    public void Init()
    {
        LoadAnimations();
    }

    void LoadAnimations() {
        animationClips = Resources.LoadAll<AnimationClip>("Animations").ToList();
    }
    
    
    public void PlaySequence(List<string> sequence)
    {
        if (sequence.Count > MaxSlots)
        {
            Debug.LogWarning($"Sequence too long. Max allowed is {MaxSlots}. Truncating.");
            sequence = sequence.GetRange(0, MaxSlots);
        }

        SetUpOverrideController(sequence.Count + 1); //one for the idle For now 
        
        // Override animation clips for the defined slots
        for (int i = 0; i < MaxSlots; i++)
        {
            if (i < sequence.Count)
            {
                string cleanName = sequence[i].ToLower();
                AnimationClip animationClip = animationClips.Find(clip => clip.name == cleanName);
                
                // The name of the animation clip in the edior should match the clipOverrides index name
                //"cleanName"
                print(animationClip.name);
                clipsOverrides[$"{i}"] = animationClip;
            }
        }
        clipsOverrides[$"{sequence.Count}"] = idle;
        overrideController.ApplyOverrides(clipsOverrides);
        
    }
    
    private void SetUpOverrideController(int totalSlots)
    {
        
        // Setup Animator Override Controller
        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;
        
        clipsOverrides = new ClipsOverrides(totalSlots);
        overrideController.GetOverrides(clipsOverrides);
    }
    
}
