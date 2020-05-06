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
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jads.Tools
{
    public class AssetGUIDRegeneratorMenu
    {
        [MenuItem("Assets/Regenerate GUID", true)]
        public static bool RegenerateGUID_Validation()
        {
            var bAreSelectedAssetsValid = true;

            foreach (var guid in Selection.assetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                bAreSelectedAssetsValid = !string.IsNullOrEmpty(guid) && guid != "0" &&
                                          !File.GetAttributes(assetPath).HasFlag(FileAttributes.Directory);
            }
            
            return bAreSelectedAssetsValid;
        }

        [MenuItem("Assets/Regenerate GUID")]
        public static void RegenerateGUID_Implementation()
        {
            var selectedGUIDs = Selection.assetGUIDs;

            var option = EditorUtility.DisplayDialogComplex($"Regenerate GUID for {selectedGUIDs.Length} asset/s",
                "Intentionally modifying asset GUID is not recommended unless certain issues are encountered. \nDo you want to proceed?",
                "Yes, please", "Nope", "I need more info");

            if (option == 0)
            {
                AssetDatabase.StartAssetEditing();
                AssetGUIDRegenerator.RegenerateGUIDs(selectedGUIDs);
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

            var countSuccess = 0;
            var countReplaced = 0;
            
            
            foreach (var selectedGUID in selectedGUIDs)
            {
                var newGUID = GUID.Generate();
                
                try
                {
                    /*
                     * PART 1 - Replace the GUID of the selected asset itself. If the .meta file does not exists or does not match the guid (which shouldn't happen), do not proceed to part 2
                     */
                    var assetPath = AssetDatabase.GUIDToAssetPath(selectedGUID);
                    var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(assetPath);

                    if (!File.Exists(metaPath))
                    {
                        throw new FileNotFoundException($"The meta file of selected asset cannot be found. Asset: {assetPath}");
                    }

                    var metaContents = File.ReadAllText(metaPath);
                    
                    // Check if guid in .meta file matches the guid of selected asset
                    if (!metaContents.Contains(selectedGUID))
                    {
                        throw new ArgumentException($"The GUID of selected asset does not match the GUID in its meta file.");
                    }

                    if (assetPath.EndsWith(".unity"))
                    {
                        Debug.Log($"RegenerateGUIDs - Skipping scene: {assetPath}");
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

                    metaContents = metaContents.Replace(selectedGUID, newGUID.ToString());
                    File.WriteAllText(metaPath, metaContents);
                    
                    if (bIsInitiallyHidden) UnhideFile(metaPath, metaAttributes);

                    /*
                     * PART 2 - Update the GUID for all assets that references the selected GUID
                     */
                    var counter = 0;
                    foreach (var guid in assetGUIDs)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        
                        if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) continue;

                        var contents = File.ReadAllText(path);
                        
                        if (!contents.Contains(selectedGUID)) continue;
                        
                        EditorUtility.DisplayProgressBar("Regenerating GUIDs", path, (float) counter / assetGUIDs.Length);
                        contents = contents.Replace(selectedGUID, newGUID.ToString());
                        File.WriteAllText(path, contents);
                        
                        counter++;
                        countReplaced++;
                    }

                    countSuccess++;
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
                $"Regenerated GUID for {countSuccess} files and {countReplaced} references.", "Done"))
            {
                // Display report to Console 
            }
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
    }
}

#endif
