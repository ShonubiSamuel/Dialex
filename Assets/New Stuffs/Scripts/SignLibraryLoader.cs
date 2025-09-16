using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

/// <summary>
/// Loads AnimationClips by normalized key using one of two modes:
///   - Resources: uses Resources.LoadAsync with a key derived by SignResolver
///   - Addressables: uses Addressables.LoadAssetAsync with the key stored in the map
/// Also supports direct manual clip overrides from SignResolver.
/// Includes an in-memory cache.
/// </summary>
public class SignLibraryLoader : MonoBehaviour
{
    public enum LoadMode { Resources, Addressables }

    [Header("Dependencies")]
    public SignResolver resolver;

    [Header("Settings")]
    public LoadMode mode = LoadMode.Resources;
    [Tooltip("If true, caches loaded clips by key for reuse.")]
    public bool enableCache = true;

    private readonly Dictionary<string, AnimationClip> _cache = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);

#if ADDRESSABLES
    // Track handles to release later if you want to free memory (optional).
    private readonly Dictionary<string, AsyncOperationHandle<AnimationClip>> _addrHandles =
        new Dictionary<string, AsyncOperationHandle<AnimationClip>>(StringComparer.Ordinal);
#endif

    public void ClearCache(bool releaseAddressables = false)
    {
        _cache.Clear();

#if ADDRESSABLES
        if (releaseAddressables)
        {
            foreach (var kv in _addrHandles)
                if (kv.Value.IsValid()) Addressables.Release(kv.Value);
            _addrHandles.Clear();
        }
#endif
    }

    /// <summary>
    /// Coroutine loader that invokes a callback when done.
    /// </summary>
    public Coroutine LoadClip(string normalizedKey, Action<AnimationClip> onLoaded)
    {
        return StartCoroutine(LoadClipRoutine(normalizedKey, onLoaded));
    }
    

    private IEnumerator LoadClipRoutine(string normalizedKey, Action<AnimationClip> onLoaded)
    {
        if (string.IsNullOrEmpty(normalizedKey))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        // Cache first
        if (enableCache && _cache.TryGetValue(normalizedKey, out var cached))
        {
            onLoaded?.Invoke(cached);
            yield break;
        }

        // Direct manual override clip?
        if (resolver != null && resolver.TryGetDirectClip(normalizedKey, out var direct))
        {
            if (enableCache) _cache[normalizedKey] = direct;
            onLoaded?.Invoke(direct);
            yield break;
        }

        // Load by selected mode
        switch (mode)
        {
            case LoadMode.Resources:
            {
                if (resolver != null && resolver.TryGetResourcesKey(normalizedKey, out var resKey))
                {
                    var req = Resources.LoadAsync<AnimationClip>(resKey);
                    yield return req;
                    var clip = req.asset as AnimationClip;
                    if (clip != null && enableCache) _cache[normalizedKey] = clip;
                    onLoaded?.Invoke(clip);
                    yield break;
                }

#if ADDRESSABLES
                // Fallback: if the map path is actually an Addressables key and ADDRESSABLES is defined, try that.
                if (resolver != null && resolver.TryGetAddressableKey(normalizedKey, out var addrKeyResFallback))
                {
                    var handle = Addressables.LoadAssetAsync<AnimationClip>(addrKeyResFallback);
                    yield return handle;
                    var clip = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
                    if (clip != null && enableCache) _cache[normalizedKey] = clip;
                    onLoaded?.Invoke(clip);
                    // Track handle so we can release later if needed
                    if (!_addrHandles.ContainsKey(normalizedKey)) _addrHandles.Add(normalizedKey, handle);
                    yield break;
                }
#endif

                Debug.LogWarning($"[SignLibraryLoader] Resources mode could not resolve key '{normalizedKey}'. " +
                                 $"Make sure the asset is under a Resources folder or provide a manual override.");
                onLoaded?.Invoke(null);
                yield break;
            }

            case LoadMode.Addressables:
            {
#if ADDRESSABLES
                if (resolver != null && resolver.TryGetAddressableKey(normalizedKey, out var addrKey))
                {
                    var handle = Addressables.LoadAssetAsync<AnimationClip>(addrKey);
                    yield return handle;
                    var clip = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
                    if (clip != null && enableCache) _cache[normalizedKey] = clip;
                    onLoaded?.Invoke(clip);
                    if (!_addrHandles.ContainsKey(normalizedKey)) _addrHandles.Add(normalizedKey, handle);
                    yield break;
                }
                // Fallback to Resources
                if (resolver != null && resolver.TryGetResourcesKey(normalizedKey, out var resKey2))
                {
                    var req = Resources.LoadAsync<AnimationClip>(resKey2);
                    yield return req;
                    var clip = req.asset as AnimationClip;
                    if (clip != null && enableCache) _cache[normalizedKey] = clip;
                    onLoaded?.Invoke(clip);
                    yield break;
                }
                Debug.LogWarning($"[SignLibraryLoader] Addressables mode could not resolve key '{normalizedKey}'.");
                onLoaded?.Invoke(null);
                yield break;
#else
                Debug.LogError("[SignLibraryLoader] Addressables mode selected, but ADDRESSABLES symbol is not defined and package not referenced.");
                onLoaded?.Invoke(null);
                yield break;
#endif
            }
        }
    }
}
