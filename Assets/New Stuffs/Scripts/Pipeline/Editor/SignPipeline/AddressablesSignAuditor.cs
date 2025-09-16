#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

/// <summary>
/// Tools → Sign Pipeline → Addressables Sign Auditor
/// Audits your SignResolver JSON list for:
///  - duplicate keys
///  - missing assets/addresses
///  - non-AnimationClip assets
///  - Resources/Addressables routing mismatches
/// Works even if Addressables package isn't installed (reduced checks).
/// </summary>
public class AddressablesSignAuditor : EditorWindow
{
    [SerializeField] private SignResolver resolver;     // drag from scene/prefab (it holds the JSON list)
    [SerializeField] private bool treatMapPathAsAddressableKey = true;
    [SerializeField] private bool onlyShowProblems = true;

    private struct Row
    {
        public string key;
        public string mapPath;
        public string status;     // OK / Warning / Error
        public string detail;
        public UnityEngine.Object assetObj; // if we can locate it
    }

    private readonly List<Row> _rows = new();
    private Vector2 _scroll;

    [MenuItem("Tools/Sign Pipeline/Addressables Sign Auditor")]
    public static void Open() => GetWindow<AddressablesSignAuditor>("Sign Auditor");

    private void OnGUI()
    {
        EditorGUILayout.Space();
        resolver = (SignResolver)EditorGUILayout.ObjectField("SignResolver (scene/prefab)", resolver, typeof(SignResolver), true);

        treatMapPathAsAddressableKey = EditorGUILayout.ToggleLeft("Treat JSON 'path' as Addressables key", treatMapPathAsAddressableKey);
        onlyShowProblems = EditorGUILayout.ToggleLeft("Only show problems", onlyShowProblems);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(resolver == null))
        {
            if (GUILayout.Button("Run Audit", GUILayout.Height(28))) RunAudit();
        }

        EditorGUILayout.Space();
        if (_rows.Count == 0)
        {
            EditorGUILayout.HelpBox("No results yet. Click 'Run Audit'.", MessageType.Info);
            return;
        }

        DrawSummary();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var r in _rows)
        {
            if (onlyShowProblems && r.status == "OK") continue;

            var color = r.status == "OK" ? Color.green : (r.status == "Warning" ? new Color(1f, .65f, 0f) : Color.red);
            using (new GUILayout.VerticalScope("box"))
            {
                var old = GUI.color; GUI.color = color;
                GUILayout.Label($"{r.status}: {r.key}", EditorStyles.boldLabel);
                GUI.color = old;

                EditorGUILayout.LabelField("Map Path / Address", r.mapPath ?? "—");
                EditorGUILayout.LabelField("Detail", r.detail ?? "—");
                EditorGUILayout.ObjectField("Asset", r.assetObj, typeof(UnityEngine.Object), false);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        int ok = _rows.Count(x => x.status == "OK");
        int warn = _rows.Count(x => x.status == "Warning");
        int err = _rows.Count(x => x.status == "Error");

        EditorGUILayout.LabelField($"Results  •  OK: {ok}   Warnings: {warn}   Errors: {err}", EditorStyles.boldLabel);
    }

    private void RunAudit()
    {
        _rows.Clear();

        if (resolver == null || resolver.signMapListJson == null)
        {
            EditorUtility.DisplayDialog("Sign Auditor", "Assign a SignResolver with a JSON list.", "OK");
            return;
        }

        // Parse the JSON list just like SignResolver.BuildFromJson does
        var list = JsonUtility.FromJson<SignEntryList>(resolver.signMapListJson.text);
        if (list?.entries == null || list.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Sign Auditor", "JSON list is empty or invalid.", "OK");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dupes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in list.entries)
        {
            var key = GlossNormalizer.Normalize(e.key);
            var path = e.path ?? "";

            if (!seen.Add(key)) dupes.Add(key);

            var row = new Row { key = key, mapPath = path, status = "OK", detail = "Looks good." };

            // If using Resources-mode semantics, verify there is a valid Resources path
            if (!treatMapPathAsAddressableKey)
            {
                if (!TryMakeResourcesKey(path, out var resKey))
                {
                    row.status = "Error";
                    row.detail = "Not in a Resources/ folder (cannot derive Resources.Load key).";
                }
                else
                {
                    var clip = Resources.Load<AnimationClip>(resKey);
                    if (clip == null)
                    {
                        row.status = "Error";
                        row.detail = $"Resources.Load failed at '{resKey}'.";
                    }
                    else
                    {
                        row.assetObj = clip;
                    }
                }

                _rows.Add(row);
                continue;
            }

            // Addressables path semantics
#if ADDRESSABLES
            // Try: path is an address string, or a Unity asset path whose entry is addressable
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                row.status = "Warning";
                row.detail = "Addressables not configured (Settings is null).";
            }
            else
            {
                // Try by GUID if 'path' looks like an asset path
                string guid = AssetDatabase.AssetPathToGUID(path);
                AddressableAssetEntry entry = null;

                if (!string.IsNullOrEmpty(guid))
                    entry = settings.FindAssetEntry(guid);

                if (entry == null)
                {
                    // Fall back: search by address string (slow but ok for audit)
                    entry = FindEntryByAddress(settings, path);
                }

                if (entry == null)
                {
                    row.status = "Error";
                    row.detail = "No Addressable entry found for this path/address.";
                }
                else
                {
                    var assetPath = entry.AssetPath;
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    row.assetObj = obj;

                    var clip = obj as AnimationClip;
                    if (clip == null)
                    {
                        row.status = "Warning";
                        row.detail = $"Address points to a non-AnimationClip asset: {AssetDatabase.GetMainAssetTypeAtPath(assetPath).Name}";
                    }
                }
            }
#else
            row.status = "Warning";
            row.detail = "ADDRESSABLES scripting symbol not defined / package not installed. Only superficial checks run.";
            // Try to resolve as an asset path anyway
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null) row.assetObj = obj;
#endif
            _rows.Add(row);
        }

        // Mark duplicates
        if (dupes.Count > 0)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (dupes.Contains(_rows[i].key))
                {
                    var r = _rows[i];
                    r.status = "Error";
                    r.detail = string.IsNullOrEmpty(r.detail) ? "Duplicate key in JSON list." : (r.detail + "  •  Duplicate key.");
                    _rows[i] = r;
                }
            }
        }

        Repaint();
    }

    private static bool TryMakeResourcesKey(string assetPath, out string resKey)
    {
        resKey = null;
        if (string.IsNullOrEmpty(assetPath)) return false;
        int i = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return false;
        var after = assetPath.Substring(i + "/Resources/".Length);
        if (after.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) after = after[..^4];
        if (after.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) after = after[..^5];
        if (string.IsNullOrEmpty(after)) return false;
        resKey = after;
        return true;
    }

#if ADDRESSABLES
    private static AddressableAssetEntry FindEntryByAddress(AddressableAssetSettings settings, string address)
    {
        foreach (var g in settings.groups)
        {
            if (g == null) continue;
            foreach (var e in g.entries)
            {
                if (e == null) continue;
                if (string.Equals(e.address, address, StringComparison.Ordinal)) return e;
            }
        }
        return null;
    }
#endif

    [Serializable] private class SignEntry { public string key; public string path; }
    [Serializable] private class SignEntryList { public List<SignEntry> entries = new(); }
}
#endif
