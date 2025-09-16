using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YourApp.Signs.Pipeline.Deterministic;

public class DeterministicGlossExtractor : MonoBehaviour, SignPipelineController.IGlossExtractor
{
    [Header("Config")]
    public GlossAliases aliases;

    [Header("Validation (optional)")]
    public SignResolver resolver;
    public bool validateKeysWithResolver = true;

    [Header("Options")]
    public bool dedupeConsecutive = true;
    public bool logCoverage = true;

    private DeterministicGlossMapper _mapper;

    private void Awake()
    {
        Func<string, bool> contains = (resolver != null)
            ? new Func<string, bool>(resolver.Contains)
            : new Func<string, bool>(_ => true);

        _mapper = new DeterministicGlossMapper(
            aliases,
            keyExists: contains,
            validateKeysWithResolver: validateKeysWithResolver);
    }

    /// <summary>
    /// Expects ENGLISH text (your pipeline translates earlier if needed).
    /// Returns a list of KEYS (already normalized & validated).
    /// </summary>
    public IEnumerator ExtractAsync(string englishText,
        Action<List<string>> onDone, Action<Exception> onError = null)
    {
        try
        {
            var keys = _mapper.Map(englishText, out var cov, "en", dedupeConsecutive);
            if (logCoverage)
                Debug.Log($"[DeterministicGlossExtractor] tokens={cov.totalTokens} phrases={cov.phraseHits} " +
                          $"numbers={cov.numberHits} singles={cov.tokenHits} stop={cov.stopwordSkipped} " +
                          $"oov={cov.oovTokens} dropped={cov.dropped}");

            onDone?.Invoke(keys);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }
        yield break;
    }
}