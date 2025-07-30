using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SignAnimationPlayer : MonoBehaviour
{
    public SignAnimationLibrary animationLibrary;
    public Animator animator;

    private AnimatorOverrideController overrideController;
    private AnimationClipOverrides clipOverrides;

    private const int MaxSlots = 20;

    public void Init()
    {
        animationLibrary.Init();

        // Setup Animator Override Controller
        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;

        print("overrideController.overridesCount  " + overrideController.overridesCount);
        clipOverrides = new AnimationClipOverrides(overrideController.overridesCount);
        overrideController.GetOverrides(clipOverrides);
    }

    public void PlaySequence(List<string> sequence)
    {
        if (sequence.Count > MaxSlots)
        {
            Debug.LogWarning($"Sequence too long. Max allowed is {MaxSlots}. Truncating.");
            sequence = sequence.GetRange(0, MaxSlots);
        }

        // Override animation clips for the defined slots
        for (int i = 0; i < MaxSlots; i++)
        {
            if (i < sequence.Count)
            {
                string cleanName = sequence[i].ToLower();
                AnimationClip clip = animationLibrary.GetClip(cleanName);
                
                // The name of the animation clip in the edior should match the clipOverrides index name
                //"cleanName"
                print(clip.name);
                clipOverrides[$"{i}"] = clip;
            }
        }
        overrideController.ApplyOverrides(clipOverrides);

        
        // Start coroutine to play one by one
        StartCoroutine(PlayRoutine(sequence));

    }
    
    private IEnumerator PlayRoutine(List<string> sequence)
    {
        animator.SetTrigger("start");
        
        yield return StartCoroutine(WaitForAnimationToEnd((sequence.Count - 1).ToString()));
        
        animator.Play("idle");
    }
    
    private IEnumerator WaitForAnimationToEnd(string stateName)
    {
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;
        
        // Then wait until state is done (normalized time >= 1)
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) &&
               animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            yield return null;
        }
    }
}
