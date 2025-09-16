using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LanguageSelectorUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Dropdown dropdown;

    [Header("Target")]
    public SignPipelineController pipeline;

    [Header("Languages")]
    // Display -> ISO
    public List<string> displayNames = new() { "English", "Yorùbá", "Hausa", "Igbo" };
    public List<string> isoCodes     = new() { "English", "Yorùbá", "Hausa", "Igbo"   };

    [Header("Defaults")]
    public string defaultIso = "en";

    private void Awake()
    {
        if (!dropdown) dropdown = GetComponentInChildren<TMP_Dropdown>();
        BuildOptions();
        SetByIso(defaultIso);
    }

    private void OnEnable()
    {
        if (dropdown) dropdown.onValueChanged.AddListener(OnChanged);
    }
    private void OnDisable()
    {
        if (dropdown) dropdown.onValueChanged.RemoveListener(OnChanged);
    }

    private void BuildOptions()
    {
        if (!dropdown) return;
        dropdown.options.Clear();
        for (int i = 0; i < Mathf.Min(displayNames.Count, isoCodes.Count); i++)
            dropdown.options.Add(new TMP_Dropdown.OptionData(displayNames[i]));
        dropdown.RefreshShownValue();
    }

    private void OnChanged(int idx)
    {
        if (idx < 0 || idx >= isoCodes.Count) return;
        var iso = isoCodes[idx].ToLowerInvariant();
        if (pipeline) pipeline.SetLanguage(iso);
    }

    public void SetByIso(string iso)
    {
        iso = string.IsNullOrWhiteSpace(iso) ? "en" : iso.ToLowerInvariant();
        int idx = isoCodes.FindIndex(s => s.ToLowerInvariant() == iso);
        if (idx < 0) idx = 0;
        if (dropdown) dropdown.value = idx;
        if (pipeline) pipeline.SetLanguage(isoCodes[idx]);
    }
}