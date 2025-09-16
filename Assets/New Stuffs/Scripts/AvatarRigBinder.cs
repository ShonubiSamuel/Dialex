using UnityEngine;

/// <summary>
/// Ensures the avatar/animator binding is valid for playing sign clips.
/// - Verifies an Animator exists and is bound to a valid Avatar
/// - Optionally enforces Humanoid
/// - Provides helpers to check clip compatibility
/// - (Optional) applies an AvatarMask to a specific Animator layer
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class AvatarRigBinder : MonoBehaviour
{
    [Header("Rig Requirements")]
    [Tooltip("If true, enforce that the Animator has a Humanoid Avatar.")]
    public bool requireHumanoid = true;

    [Tooltip("Optional mask you might apply to a specific Animator layer (e.g., upper-body-only signs).")]
    public AvatarMask optionalLayerMask;

    [Tooltip("Animator layer index where the optional mask should apply (if you are using Animator layers).")]
    public int maskedLayerIndex = 1;

    private Animator _animator;

    public Animator Animator => _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        ValidateAnimator(_animator, requireHumanoid);
    }

    /// <summary>Assigns a new Avatar at runtime. Returns false if incompatible.</summary>
    public bool BindAvatar(Avatar avatar)
    {
        if (avatar == null)
        {
            Debug.LogError("[AvatarRigBinder] Cannot bind null Avatar.");
            return false;
        }

        if (requireHumanoid && !avatar.isHuman)
        {
            Debug.LogError("[AvatarRigBinder] Avatar is not Humanoid but Humanoid is required.");
            return false;
        }

        _animator.avatar = avatar;
        return true;
    }

    /// <summary>Quick validation on Animator + Avatar.</summary>
    public static bool ValidateAnimator(Animator animator, bool requireHumanoid)
    {
        if (animator == null)
        {
            Debug.LogError("[AvatarRigBinder] Missing Animator.");
            return false;
        }

        if (animator.avatar == null)
        {
            Debug.LogWarning("[AvatarRigBinder] Animator has no Avatar assigned.");
            return !requireHumanoid; // allow if not strictly required
        }

        if (requireHumanoid && !animator.avatar.isHuman)
        {
            Debug.LogError("[AvatarRigBinder] Animator Avatar is not Humanoid.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Basic compatibility check. For Humanoid avatars, Unity retargets most clips automatically.
    /// For Generic clips, you usually need matching hierarchy.
    /// </summary>
    public bool IsClipLikelyCompatible(AnimationClip clip)
    {
        if (clip == null) return false;
        if (_animator == null) return false;
        if (requireHumanoid) return _animator.avatar != null && _animator.avatar.isHuman;
        // For Generic rigs, we can't know for sure without deep checks; assume true.
        return true;
    }

    /// <summary>
    /// Optionally apply an AvatarMask to an Animator layer (useful if you use a controller with multiple layers).
    /// Note: This requires that you configured layers in the Animator Controller.
    /// </summary>
    public void ApplyMaskToAnimatorLayer()
    {
        if (optionalLayerMask == null)
        {
            Debug.Log("[AvatarRigBinder] No AvatarMask assigned; skipping layer mask application.");
            return;
        }

        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("[AvatarRigBinder] Animator or Controller missing; cannot apply mask.");
            return;
        }

        if (maskedLayerIndex <= 0)
        {
            Debug.LogWarning("[AvatarRigBinder] Masked layer index should be > 0 (base layer is 0 and cannot be masked).");
            return;
        }

        // AnimatorController runtime masking is configured in the controller asset.
        // At runtime we can only toggle layer weight; the mask is read from the controller.
        // This call is here to make intent explicit.
        _animator.SetLayerWeight(maskedLayerIndex, 1f);
        Debug.Log($"[AvatarRigBinder] Enabled masked layer {maskedLayerIndex}. Ensure the controller layer has the correct AvatarMask assigned in the asset.");
    }
}
