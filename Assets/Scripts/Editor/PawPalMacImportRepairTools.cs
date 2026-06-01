using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PawPalMacImportRepairTools
{
    private const string UiResourcesRoot = "Assets/Resources/UI";

    [MenuItem("PawFriends/Repair/Fix UI Resource Import Settings")]
    public static void FixUiResourceImportSettings()
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiResourcesRoot });
        List<string> updatedAssets = new List<string>();

        for (int i = 0; i < assetGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                updatedAssets.Add(assetPath);
            }
        }

        Debug.Log("PawFriends UI import repair finished. Updated " + updatedAssets.Count + " texture imports under " + UiResourcesRoot + ".");
    }
}
