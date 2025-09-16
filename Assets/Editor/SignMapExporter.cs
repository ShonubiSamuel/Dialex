// // Assets/Editor/SignMapExporter.cs
// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Text;
// using System.Text.RegularExpressions;
// using UnityEditor;
// using UnityEngine;
//
// public static class SignMapExporter
// {
//     // ==== CONFIG ====
//     // Folder that contains your .anim clips (from your screenshot this is right)
//     private const string RootFolder   = "Assets/Signs";
//
//     // Output JSON files
//     private const string DictJsonPath = "Assets/SignMap.json";
//     private const string ListJsonPath = "Assets/SignMap_List.json";
//
//     // What should we put in the "path" field?
//     //  - "Auto": prefer Addressables address if present; else Resources key if under /Resources/; else Asset path
//     //  - "AddressablesOnly": require an Addressables address; else skip the entry
//     //  - "ResourcesOnly": require a Resources key; else skip the entry
//     //  - "AssetPath": always use the Unity asset path (easiest if you’re not loading by string at runtime)
//     private enum PathMode { Auto, AddressablesOnly, ResourcesOnly, AssetPath }
//     private const PathMode Mode = PathMode.Auto;
//
//     // Optional: skip some known junk names you showed (leave empty if you want everything)
//     private static readonly HashSet<string> ExactSkips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
//     {
//         "anim_layer", "anim_layer001", "animation", "base_layer", "armature", "armaturearmatureaction001"
//     };
//
//     // =================
//
//     [Serializable] public class SignEntry { public string key; public string path; }
//     [Serializable] public class SignEntryList { public List<SignEntry> entries = new List<SignEntry>(); }
//
//     [MenuItem("Tools/Sign Mapping/Export Sign Map JSON (from .anim)")]
//     public static void Export()
//     {
//         if (!AssetDatabase.IsValidFolder(RootFolder))
//         {
//             Debug.LogError($"[SignMapExporter] Folder not found: {RootFolder}");
//             return;
//         }
//
//         // IMPORTANT: find AnimationClips, not Models
//         string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { RootFolder });
//
//         var dict = new Dictionary<string, string>(StringComparer.Ordinal);
//         var list = new SignEntryList();
//         int skippedDuplicates = 0, skippedJunk = 0, skippedNoHandle = 0, total = 0;
//
//         foreach (var guid in guids)
//         {
//             string assetPath = AssetDatabase.GUIDToAssetPath(guid);
//             if (!assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
//
//             var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
//             if (clip == null) continue;
//
//             total++;
//             string fileName = Path.GetFileNameWithoutExtension(assetPath);
//             string key = CreateKeyFromFilename(fileName);
//             if (string.IsNullOrEmpty(key)) { skippedJunk++; continue; }
//             if (ExactSkips.Contains(key)) { skippedJunk++; continue; }
//
//             // Resolve what goes into "path" based on the selected mode
//             if (!TryResolveHandle(assetPath, key, Mode, out var handle))
//             {
//                 skippedNoHandle++;
//                 continue;
//             }
//
//             if (!dict.ContainsKey(key))
//             {
//                 dict[key] = handle;
//                 list.entries.Add(new SignEntry { key = key, path = handle });
//             }
//             else
//             {
//                 skippedDuplicates++;
//                 Debug.LogWarning($"[SignMapExporter] Duplicate key '{key}' from '{assetPath}' (already mapped to '{dict[key]}'). Skipping.");
//             }
//         }
//
//         // Write dictionary JSON (pretty)
//         string dictJson = ToPrettyDictionaryJson(dict);
//         File.WriteAllText(DictJsonPath, dictJson, Encoding.UTF8);
//
//         // Write list JSON (JsonUtility-friendly)
//         string listJson = JsonUtility.ToJson(list, true);
//         File.WriteAllText(ListJsonPath, listJson, Encoding.UTF8);
//
//         AssetDatabase.Refresh();
//         Debug.Log($"[SignMapExporter] Exported {dict.Count} entries (from {total} clips). " +
//                   $"Skipped: duplicates={skippedDuplicates}, junk={skippedJunk}, no-handle={skippedNoHandle}.\n" +
//                   $"- Dict: {DictJsonPath}\n- List: {ListJsonPath}\n- Mode: {Mode}");
//     }
//
//     // --- Handle resolution (Addressables / Resources / AssetPath) ---
//
//     private static bool TryResolveHandle(string assetPath, string key, PathMode mode, out string handle)
//     {
//         handle = null;
//
//         if (mode == PathMode.AddressablesOnly || mode == PathMode.Auto)
//         {
//             if (TryGetAddressablesAddress(assetPath, key, out var addr))
//             {
//                 handle = addr;
//                 if (mode == PathMode.AddressablesOnly) return true;
//                 // In Auto, prefer Addressables if present
//                 return true;
//             }
//             else if (mode == PathMode.AddressablesOnly)
//             {
//                 return false;
//             }
//         }
//
//         if (mode == PathMode.ResourcesOnly || mode == PathMode.Auto)
//         {
//             if (TryGetResourcesKey(assetPath, out var resKey))
//             {
//                 handle = resKey;
//                 if (mode == PathMode.ResourcesOnly) return true;
//                 // In Auto, take Resources if no Addressables
//                 return true;
//             }
//             else if (mode == PathMode.ResourcesOnly)
//             {
//                 return false;
//             }
//         }
//
//         // Fallback to asset path (always available)
//         if (mode == PathMode.AssetPath || mode == PathMode.Auto)
//         {
//             handle = assetPath;
//             return true;
//         }
//
//         return false;
//     }
//
//     private static bool TryGetResourcesKey(string assetPath, out string resourcesKey)
//     {
//         resourcesKey = null;
//         int idx = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
//         if (idx < 0) return false;
//         string after = assetPath.Substring(idx + "/Resources/".Length);
//         if (after.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
//             after = after.Substring(0, after.Length - 5);
//         if (string.IsNullOrWhiteSpace(after)) return false;
//         resourcesKey = after;
//         return true;
//     }
//
//     private static bool TryGetAddressablesAddress(string assetPath, string key, out string address)
//     {
//         address = null;
//         // Only works if Addressables package is installed and define is set
// #if ADDRESSABLES
//         var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
//         if (settings == null) return false;
//         string guid = AssetDatabase.AssetPathToGUID(assetPath);
//         var entry = settings.FindAssetEntry(guid);
//         if (entry == null) return false;
//
//         // If no custom address set, fall back to a sane default signs/<key>
//         address = !string.IsNullOrEmpty(entry.address) ? entry.address : ("signs/" + key);
//         return true;
// #else
//         return false;
// #endif
//     }
//
//     // --- Key normalization (keep simple; your files already look clean) ---
//     private static string CreateKeyFromFilename(string name)
//     {
//         if (string.IsNullOrWhiteSpace(name)) return "";
//
//         // lower + handle decimals "7.2" -> "7_point_2"
//         string s = name.Trim();
//         s = Regex.Replace(s, @"(?<=\d)\.(?=\d)", "_point_");
//         s = s.ToLowerInvariant();
//
//         // remove parentheses content
//         s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");
//
//         // collapse whitespace -> underscore
//         s = Regex.Replace(s, @"\s+", "_");
//
//         // keep only [a-z0-9_]
//         s = Regex.Replace(s, @"[^a-z0-9_]", "");
//
//         // dataset junk endings (if any slipped in)
//         s = Regex.Replace(s, @"_(\d{6,12})(_\d+)?$", "");
//         s = Regex.Replace(s, @"_animation$", "");
//
//         // trim
//         s = s.Trim('_');
//
//         return s;
//     }
//
//     // --- JSON helpers ---
//     private static string ToPrettyDictionaryJson(Dictionary<string, string> map)
//     {
//         var sb = new StringBuilder();
//         sb.AppendLine("{");
//         int i = 0;
//         foreach (var kvp in map)
//         {
//             string keyEsc = EscapeJson(kvp.Key);
//             string valEsc = EscapeJson(kvp.Value);
//             sb.Append("  \"").Append(keyEsc).Append("\": \"").Append(valEsc).Append("\"");
//             i++;
//             if (i < map.Count) sb.Append(",");
//             sb.AppendLine();
//         }
//         sb.AppendLine("}");
//         return sb.ToString();
//     }
//
//     private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
// }
// #endif
