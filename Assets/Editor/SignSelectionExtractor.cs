// #if UNITY_EDITOR
// using System;
// using System.IO;
// using System.Text.RegularExpressions;
// using UnityEditor;
// using UnityEngine;
//
// public static class RenameSelectedToCleanKeys
// {
//     [MenuItem("Assets/Signs/Rename + Addressify Selected (clean keys)", priority = 0)]
//     private static void RenameSelected()
//     {
//         var guids = Selection.assetGUIDs;
//         if (guids == null || guids.Length == 0)
//         {
//             EditorUtility.DisplayDialog("No Selection", "Select one or more .anim clips in Project view.", "OK");
//             return;
//         }
//
//         // Toggle these to taste:
//         bool makeAddressable = true;             // set Addressables if available
//         string addressPrefix = "signs/";         // signs/<key>
//         string addressLabel  = "signs";
//
//         int renamed = 0, skippedSame = 0, skippedNotAnim = 0, skippedCollision = 0;
//
//         AssetDatabase.StartAssetEditing();
//         try
//         {
//             foreach (var guid in guids)
//             {
//                 string path = AssetDatabase.GUIDToAssetPath(guid);
//                 if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
//                 {
//                     skippedNotAnim++;
//                     continue;
//                 }
//
//                 var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
//                 if (clip == null) { skippedNotAnim++; continue; }
//
//                 string currentNameNoExt = Path.GetFileNameWithoutExtension(path);
//                 string key = CleanKeyFromDatasetyName(currentNameNoExt);
//
//                 if (string.IsNullOrEmpty(key))
//                 {
//                     Debug.LogWarning($"[Rename] Empty key for {path} — skipped.");
//                     continue;
//                 }
//
//                 string desiredDir  = Path.GetDirectoryName(path)!.Replace("\\","/");
//                 string desiredName = key + ".anim";
//                 string desiredPath = (desiredDir + "/" + desiredName).Replace("\\","/");
//
//                 if (string.Equals(currentNameNoExt, key, StringComparison.Ordinal))
//                 {
//                     Debug.Log($"[Rename] OK: {currentNameNoExt}.anim");
//                     skippedSame++;
//                 }
//                 else
//                 {
//                     if (AssetDatabase.LoadAssetAtPath<AnimationClip>(desiredPath) != null)
//                     {
//                         Debug.LogWarning($"[Rename] Collision: {desiredName} already exists in {desiredDir}. Keeping original {currentNameNoExt}.anim");
//                         skippedCollision++;
//                     }
//                     else
//                     {
//                         string err = AssetDatabase.RenameAsset(path, key);
//                         if (!string.IsNullOrEmpty(err))
//                             Debug.LogError($"[Rename] Error renaming {path}: {err}");
//                         else
//                         {
//                             Debug.Log($"[Rename] {currentNameNoExt}.anim → {desiredName}");
//                             path = desiredPath; // update path after rename
//                             clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
//                             renamed++;
//                         }
//                     }
//                 }
//
// #if ADDRESSABLES
//                 if (makeAddressable)
//                 {
//                     TryMarkAddressable(path, addressPrefix + key, addressLabel);
//                 }
// #else
//                 if (makeAddressable)
//                 {
//                     Debug.LogWarning("[Rename] Addressables requested, but package/define missing. Install Addressables and add 'ADDRESSABLES' to Scripting Define Symbols.");
//                 }
// #endif
//             }
//         }
//         finally
//         {
//             AssetDatabase.StopAssetEditing();
//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();
//         }
//
//         Debug.Log($"[Rename] Renamed: {renamed}, Name-OK: {skippedSame}, Collisions: {skippedCollision}, Not .anim: {skippedNotAnim}");
//     }
//
//     // --- Normalization tuned for names like: sg_asl_alaska_alt_3_2023515_animation ---
//     private static string CleanKeyFromDatasetyName(string raw)
//     {
//         if (string.IsNullOrWhiteSpace(raw)) return "";
//
//         string s = raw.ToLowerInvariant();
//
//         // Drop common dataset prefixes at start: sg_, asl_, bsl_, etc (one or two tokens like sg_asl_)
//         s = Regex.Replace(s, @"^(sg_)?(asl|bsl|isl|ssl)_", "");
//
//         // Remove trailing timestamps / dataset suffixes: _YYYYMMDD or _YYYYMMDD_n or _animation
//         s = Regex.Replace(s, @"_(\d{6,12})(_\d+)?(_animation)?$", "");
//         s = Regex.Replace(s, @"_animation$", "");
//
//         // Replace underscores with spaces for token ops
//         s = s.Replace('_', ' ');
//
//         // Remove parentheticals if any
//         s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");
//
//         // decimals: 7.2 -> 7 point 2
//         s = Regex.Replace(s, @"(?<=\d)\.(?=\d)", " point ");
//
//         // Collapse whitespace
//         s = Regex.Replace(s, @"\s+", " ").Trim();
//
//         // Remove nuisance tokens that denote alternates/duplicates
//         // e.g., "alaska alt 3" -> "alaska"
//         s = Regex.Replace(s, @"\b(alt|alternate|variant|ver|vers|copy|take|dup|duplicate)\b", " ");
//         s = Regex.Replace(s, @"\s+", " ").Trim();
//
//         // Drop trailing numeric copy index for word phrases (keep single-letter + number like "d 2")
//         var tokens = s.Split(' ');
//         if (tokens.Length >= 2 && Regex.IsMatch(tokens[^1], @"^\d+$"))
//         {
//             bool singleLetterPlusNumber = tokens.Length == 2 && Regex.IsMatch(tokens[0], @"^[a-z]$");
//             bool hasWordLongerThan1 = false;
//             for (int i = 0; i < tokens.Length - 1; i++)
//                 if (Regex.IsMatch(tokens[i], "[a-z]") && tokens[i].Length > 1) { hasWordLongerThan1 = true; break; }
//
//             if (!singleLetterPlusNumber && hasWordLongerThan1)
//                 s = string.Join(" ", tokens, 0, tokens.Length - 1);
//         }
//
//         // Final shape
//         s = s.Replace(' ', '_');
//         s = Regex.Replace(s, @"[^a-z0-9_]", "");
//         s = s.Trim('_');
//
//         return s;
//     }
//
// #if ADDRESSABLES
//     private static void TryMarkAddressable(string assetPath, string address, string label)
//     {
//         var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
//         if (settings == null)
//         {
//             Debug.LogWarning("[Addressables] Settings not found. Open Addressables Groups window to auto-create.");
//             return;
//         }
//
//         // Ensure "Signs" group
//         var group = settings.FindGroup("Signs") ?? settings.CreateGroup(
//             "Signs", false, false, true, null,
//             new[] {
//                 ScriptableObject.CreateInstance<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>(),
//                 ScriptableObject.CreateInstance<UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema>()
//             });
//
//         var guid = AssetDatabase.AssetPathToGUID(assetPath);
//         var entry = settings.CreateOrMoveEntry(guid, group, false, false);
//         entry.address = address;
//
//         if (!string.IsNullOrEmpty(label))
//         {
//             settings.AddLabel(label);
//             entry.SetLabel(label, true, true);
//         }
//
//         EditorUtility.SetDirty(group);
//         EditorUtility.SetDirty(settings);
//     }
// #endif
// }
// #endif
