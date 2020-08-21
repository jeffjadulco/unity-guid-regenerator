/*
 
GitHub: https://github.com/jeffjads/unity-guid-regenerator
Related Docs: https://docs.unity3d.com/ScriptReference/AssetDatabase.html
              https://docs.unity3d.com/ScriptReference/AssetDatabase.FindAssets.html
              
=== DISCLAIMER ===

Only use this if really needed. Intentionally modifying asset GUID is not recommended unless certain issues are encountered.

=== DISCLAIMER ===

=== LICENSE ===

MIT License

Copyright (c) 2020 Jefferson Jadulco

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

=== LICENSE ===
*/

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Jads.Tools
{
    public class AssetGUIDRegeneratorMenu
    {
        public static readonly string Version = "1.0.1";
        [MenuItem("Assets/Regenerate GUID", true)]
        public static bool RegenerateGUID_Validation()
        {
            var bAreSelectedAssetsValid = true;

            foreach (var guid in Selection.assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                bAreSelectedAssetsValid = !string.IsNullOrEmpty(guid) && guid != "0";
            }
            
            return bAreSelectedAssetsValid;
        }

        [MenuItem("Assets/Regenerate GUID")]
        public static void RegenerateGUID_Implementation()
        {
            var assetGUIDS = AssetGUIDRegenerator.ExtractGUIDs(Selection.assetGUIDs);

            var option = EditorUtility.DisplayDialogComplex($"Regenerate GUID for {assetGUIDS.Length} asset/s",
                "DISCLAIMER: Intentionally modifying asset GUID is not recommended unless certain issues are encountered. " +
                "\n\nMake sure you have a backup or is using a version control system. \n\nThis operation can take a long time on larger projects. Do you want to proceed?",
                "Yes, please", "Nope", "I need more info");

            if (option == 0)
            {
                AssetDatabase.StartAssetEditing();
                AssetGUIDRegenerator.RegenerateGUIDs(assetGUIDS);
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else if (option == 2)
            {
                Application.OpenURL("https://github.com/jeffjads/unity-guid-regenerator/blob/master/README.md");
            }
        }
    }

    internal class AssetGUIDRegenerator
    {
        // Basically, we want to limit the types here (e.g. "t:GameObject t:Scene t:Material").
        // But to support ScriptableObjects dynamically, we just include the base of all assets which is "t:Object"
        private const string SearchFilter = "t:Object";

        // Set to "Assets/" folder only. We don't want to include other directories of the root folder
        private static readonly string[] SearchDirectories = { "Assets" };

        public static void RegenerateGUIDs(string[] selectedGUIDs)
        {
            var assetGUIDs = AssetDatabase.FindAssets(SearchFilter, SearchDirectories);

            var updatedAssets = new Dictionary<string, int>();
            var skippedAssets = new List<string>();

            foreach (var selectedGUID in selectedGUIDs)
            {
                var newGUID = GUID.Generate().ToString();
                
                try
                {
                    /*
                     * PART 1 - Replace the GUID of the selected asset itself. If the .meta file does not exists or does not match the guid (which shouldn't happen), do not proceed to part 2
                     */
                    var assetPath = AssetDatabase.GUIDToAssetPath(selectedGUID);
                    var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(assetPath);

                    if (!File.Exists(metaPath))
                    {
                        skippedAssets.Add(assetPath);
                        throw new FileNotFoundException($"The meta file of selected asset cannot be found. Asset: {assetPath}");
                    }

                    var metaContents = File.ReadAllText(metaPath);
                    
                    // Check if guid in .meta file matches the guid of selected asset
                    if (!metaContents.Contains(selectedGUID))
                    {
                        skippedAssets.Add(assetPath);
                        throw new ArgumentException($"The GUID of [{assetPath}] does not match the GUID in its meta file.");
                    }

                    // Skip folders
                    if (IsDirectory(assetPath)) continue;

                    // Skip scene files
                    if (assetPath.EndsWith(".unity"))
                    {
                        skippedAssets.Add(assetPath);
                        continue;
                    }

                    var metaAttributes = File.GetAttributes(metaPath);
                    var bIsInitiallyHidden = false;
                    
                    // If the .meta file is hidden, unhide it temporarily
                    if (metaAttributes.HasFlag(FileAttributes.Hidden))
                    {
                        bIsInitiallyHidden = true;
                        HideFile(metaPath, metaAttributes);
                    }

                    metaContents = metaContents.Replace(selectedGUID, newGUID);
                    File.WriteAllText(metaPath, metaContents);
                    
                    if (bIsInitiallyHidden) UnhideFile(metaPath, metaAttributes);

                    /*
                     * PART 2 - Update the GUID for all assets that references the selected GUID
                     */
                    var countProgress = 0;
                    var countReplaced = 0;
                    foreach (var guid in assetGUIDs)
                    {
                        countProgress++;
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        
                        EditorUtility.DisplayProgressBar($"Regenerating GUID: {assetPath}", path, (float) countProgress / assetGUIDs.Length);
                        
                        if (IsDirectory(path)) continue;

                        var contents = File.ReadAllText(path);
                        
                        if (!contents.Contains(selectedGUID)) continue;
                        
                        contents = contents.Replace(selectedGUID, newGUID);
                        File.WriteAllText(path, contents);
                        
                        countReplaced++;
                    }
                    
                    updatedAssets.Add(AssetDatabase.GUIDToAssetPath(selectedGUID), countReplaced);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            
            if (EditorUtility.DisplayDialog("Regenerate GUID",
                $"Regenerated GUID for {updatedAssets.Count} assets. \nSee console logs for detailed report.", "Done"))
            {
                var message = $"<b>GUID Regenerator {AssetGUIDRegeneratorMenu.Version}</b>\n";
                
                if (updatedAssets.Count > 0) message += $"<b><color=green>{updatedAssets.Count} Updated Asset/s</color></b>\tSelect this log for more info\n";
                message = updatedAssets.Aggregate(message, (current, kvp) => current + $"{kvp.Value} references\t{kvp.Key}\n");
                
                if (skippedAssets.Count > 0) message += $"\n<b><color=red>{skippedAssets.Count} Skipped Asset/s</color></b>\n";
                message = skippedAssets.Aggregate(message, (current, skipped) => current + $"{skipped}\n");

                Debug.Log($"{message}");
            }
        }

        // Searches for Directories and extracts all asset guids inside it using AssetDatabase.FindAssets
        public static string[] ExtractGUIDs(string[] selectedGUIDs)
        {
            var finalGuids = new List<string>();
            foreach (var guid in selectedGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (IsDirectory(assetPath))
                {
                    string[] searchDirectory = {assetPath};
                    finalGuids.AddRange(AssetDatabase.FindAssets(SearchFilter, searchDirectory));
                }
                else
                {
                    finalGuids.Add(guid);
                }
            }
            
            return finalGuids.ToArray();
        }

        private static void HideFile(string path, FileAttributes attributes)
        {
            attributes &= ~FileAttributes.Hidden;
            File.SetAttributes(path, attributes);
        }

        private static void UnhideFile(string path, FileAttributes attributes)
        {
            attributes |= FileAttributes.Hidden;
            File.SetAttributes(path, attributes);
        }

        private static bool IsDirectory(string path) => File.GetAttributes(path).HasFlag(FileAttributes.Directory);
    }
}

#endif
