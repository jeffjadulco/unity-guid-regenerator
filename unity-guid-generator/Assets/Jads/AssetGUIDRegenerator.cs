/*
 
GitHub: https://github.com/jeffjads/unity-guid-regenerator

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
using System.Runtime.Serialization;
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
            
            AssetDatabase.StartAssetEditing();
            AssetGUIDRegenerator.RegenerateGUIDs(selectedGUIDs);
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    internal class AssetGUIDRegenerator
    {
        // Add documentation
        private const string SearchFilter = "t:GameObject t:Scene t:Material";

        // Add documentation
        private static readonly string[] SearchDirectories = { "Assets" };

        public static void RegenerateGUIDs(string[] selectedGUIDs)
        {
            // Store all asset GUIDs
            var assetGUIDs = AssetDatabase.FindAssets(SearchFilter, SearchDirectories);
            // Debug.Log($"RegenerateGUIDs - Assets found count: {assetGUIDs.Length}");

            var countSuccess = 0; 
            var countReplaced = 0;
            
            foreach (var selectedGUID in selectedGUIDs)
            {
                Debug.Log($"RegenerateGUIDs - GUID: {selectedGUID}");
                var newGUID = GUID.Generate();
                
                try
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(selectedGUID);
                    var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(assetPath);

                    // Check if .meta file exists
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

                    var counter = 0;
                    foreach (var guid in assetGUIDs)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
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
