#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public sealed class PawPalMobileTextureImportPostprocessor : AssetPostprocessor
{
    private const string ImportVersionKey = "PawPal.MobileTextureImport.Version";
    private const int ImportVersion = 1;

    private static readonly string[] TargetFolders =
    {
        "Assets/Resources/UI",
        "Assets/Resources/WindowBackdrops"
    };

    [InitializeOnLoadMethod]
    private static void ReimportTargetedTexturesOnVersionChange()
    {
        EditorApplication.delayCall += ReimportIfNeeded;
    }

    private static void ReimportIfNeeded()
    {
        if (EditorPrefs.GetInt(ImportVersionKey, 0) >= ImportVersion)
        {
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", TargetFolders);
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (ShouldOptimize(assetPath))
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EditorPrefs.SetInt(ImportVersionKey, ImportVersion);
        AssetDatabase.SaveAssets();
    }

    private void OnPreprocessTexture()
    {
        if (!(assetImporter is TextureImporter importer) || !ShouldOptimize(assetPath))
        {
            return;
        }

        bool isWindowBackdrop = assetPath.StartsWith("Assets/Resources/WindowBackdrops/", StringComparison.Ordinal);
        bool isIcon = assetPath.StartsWith("Assets/Resources/UI/Icons/", StringComparison.Ordinal);
        bool isLargeUiBackground = assetPath.StartsWith("Assets/Resources/UI/Figma/Map/", StringComparison.Ordinal)
            || assetPath.StartsWith("Assets/Resources/UI/Generated/ShopOffers/", StringComparison.Ordinal);
        bool isCatalogSprite = assetPath.StartsWith("Assets/Resources/UI/Downloaded/Shop/", StringComparison.Ordinal)
            || assetPath.StartsWith("Assets/Resources/UI/Figma/Shop/", StringComparison.Ordinal)
            || assetPath.StartsWith("Assets/Resources/UI/Generated/Shop/", StringComparison.Ordinal);

        importer.mipmapEnabled = false;
        importer.streamingMipmaps = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = !isWindowBackdrop;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = false;

        if (isWindowBackdrop)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
        else
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
        }

        int defaultMaxSize = 2048;
        int mobileMaxSize = 1024;
        if (isLargeUiBackground || isWindowBackdrop)
        {
            mobileMaxSize = 2048;
        }
        else if (isIcon)
        {
            defaultMaxSize = 512;
            mobileMaxSize = 256;
        }
        else if (isCatalogSprite)
        {
            defaultMaxSize = 1024;
            mobileMaxSize = 1024;
        }

        importer.maxTextureSize = defaultMaxSize;
        ApplyPlatformSettings(importer, "Android", mobileMaxSize);
        ApplyPlatformSettings(importer, "iPhone", mobileMaxSize);
    }

    private static void ApplyPlatformSettings(TextureImporter importer, string platformName, int maxTextureSize)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
        settings.name = platformName;
        settings.overridden = true;
        settings.maxTextureSize = maxTextureSize;
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        settings.textureCompression = TextureImporterCompression.Compressed;
        settings.compressionQuality = 50;
        settings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(settings);
    }

    private static bool ShouldOptimize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        for (int i = 0; i < TargetFolders.Length; i++)
        {
            if (path.StartsWith(TargetFolders[i] + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
