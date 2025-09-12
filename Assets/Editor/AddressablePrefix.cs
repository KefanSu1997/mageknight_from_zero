using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class PrefixCardAddresses {
    [MenuItem("Tools/Addressables/Add cards/ Prefix")]
    static void AddPrefix() {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found. Please configure addressables first.");
            return;
        }

        var group = settings.FindGroup("CardSprites");
        if (group == null)
        {
            Debug.LogWarning("CardSprites group not found. Checking Default Local Group...");
            group = settings.FindGroup("Default Local Group");
        }

        if (group == null)
        {
            Debug.LogError("No suitable addressable group found.");
            return;
        }

        int changedCount = 0;

        if (group.entries == null || group.entries.Count == 0)
        {
            Debug.LogWarning($"Group '{group.Name}' has no entries or is null. Checking card sprites folder...");
            
            // Try to find card sprite assets in cards folder
            var cardPath = "Assets/GameData/cards";
            if (!System.IO.Directory.Exists(cardPath))
            {
                Debug.LogWarning("Cards folder not found, skipping asset processing");
                return;
            }
            
            // Get all PNG files in the cards folder
            var cardFiles = System.IO.Directory.GetFiles(cardPath, "*.png", System.IO.SearchOption.AllDirectories);
            if (cardFiles.Length == 0)
            {
                Debug.LogWarning("No PNG files found in cards folder");
                return;
            }

            // Add sprites to addressable group
            foreach (var file in cardFiles)
            {
                var relativePath = file.Replace("\\", "/");
                var assetsIndex = relativePath.IndexOf("Assets/");
                var assetPath = assetsIndex >= 0 ? relativePath.Substring(assetsIndex) : relativePath;
                var address = "cards/" + System.IO.Path.GetFileNameWithoutExtension(assetPath);
                
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    settings.CreateOrMoveEntry(guid, group);
                    var entry = group.GetAssetEntry(guid);
                    if (entry != null)
                    {
                        entry.SetAddress(address);
                        changedCount++;
                    }
                }
            }
        }
        else
        {
            // Update existing entries
            foreach (var e in group.entries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.address))
                {
                    Debug.LogWarning("Found entry with null address, skipping...");
                    continue;
                }

                if (!e.address.StartsWith("cards/"))
                {
                    e.SetAddress("cards/" + e.address);
                    changedCount++;
                }
            }
        }

        if (changedCount > 0)
        {
            Debug.Log($"Updated {changedCount} addressable entries with 'cards/' prefix");
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(); // 重建 catalog
        }
        else
        {
            Debug.Log("No addressable entries needed updating.");
        }
    }
}
