using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PawPalBreedShopPreviewEntry
{
    public string BreedKey;
    public string LeftPrefabAssetPath;
    public string CenterPrefabAssetPath;
    public string RightPrefabAssetPath;
    public Vector3 GroupOffset = Vector3.zero;
    public float GroupScale = 1f;
}

public static class PawPalBreedShopPreviewLibrary
{
    private static readonly Dictionary<string, PawPalBreedShopPreviewEntry> EntryCache = new Dictionary<string, PawPalBreedShopPreviewEntry>(StringComparer.OrdinalIgnoreCase);
    private static bool cacheBuilt;

    public static bool TryResolveEntry(PawPalCatalogItemDefinition item, out PawPalBreedShopPreviewEntry entry)
    {
        EnsureCache();
        entry = null;
        if (item == null)
        {
            return false;
        }

        string breedKey = NormalizeBreedKey(item);
        return !string.IsNullOrEmpty(breedKey)
            && EntryCache.TryGetValue(breedKey, out entry)
            && entry != null;
    }

#if UNITY_EDITOR
    public static GameObject LoadLeftPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return LoadPrefab(entry != null ? entry.LeftPrefabAssetPath : null);
    }

    public static GameObject LoadCenterPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return LoadPrefab(entry != null ? entry.CenterPrefabAssetPath : null);
    }

    public static GameObject LoadRightPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return LoadPrefab(entry != null ? entry.RightPrefabAssetPath : null);
    }
#endif

    private static void EnsureCache()
    {
        if (cacheBuilt)
        {
            return;
        }

        cacheBuilt = true;
        EntryCache.Clear();

#if UNITY_EDITOR
        foreach (PawPalPetAnimationEntry animationEntry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            if (animationEntry == null || animationEntry.Species != IntroPetSpecies.Dog || animationEntry.LifeStage != PawPalPetLifeStage.Adult)
            {
                continue;
            }

            PawPalBreedShopPreviewEntry previewEntry;
            if (!TryBuildEditorEntry(animationEntry, out previewEntry) || previewEntry == null)
            {
                continue;
            }

            EntryCache[animationEntry.CanonicalKey] = previewEntry;
            string normalizedKey = PawPalPetAnimationRegistry.NormalizeKey(animationEntry.CanonicalKey);
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                EntryCache[normalizedKey] = previewEntry;
            }
        }
#endif
    }

    private static string NormalizeBreedKey(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(item.Id) && item.Id.StartsWith("dog_", StringComparison.OrdinalIgnoreCase))
        {
            return PawPalPetAnimationRegistry.NormalizeKey(item.Id.Substring(4));
        }

        return PawPalPetAnimationRegistry.NormalizeKey(item.DisplayName);
    }

#if UNITY_EDITOR
    private static bool TryBuildEditorEntry(PawPalPetAnimationEntry animationEntry, out PawPalBreedShopPreviewEntry entry)
    {
        entry = null;
        if (animationEntry == null || string.IsNullOrWhiteSpace(animationEntry.ImportedAnimationAssetPath))
        {
            return false;
        }

        string importedAnimationAssetPath = animationEntry.ImportedAnimationAssetPath.Replace('\\', '/');
        int animFolderMarker = importedAnimationAssetPath.LastIndexOf("/FBX/Anim/", StringComparison.OrdinalIgnoreCase);
        if (animFolderMarker <= 0)
        {
            return false;
        }

        string breedFolder = importedAnimationAssetPath.Substring(0, animFolderMarker);
        string prefabFolder = breedFolder + "/Prefabs";
        if (string.IsNullOrWhiteSpace(prefabFolder) || !AssetDatabase.IsValidFolder(prefabFolder))
        {
            return false;
        }

        string leftPrefabAssetPath;
        string centerPrefabAssetPath;
        string rightPrefabAssetPath;
        if (!TryFindBreedVariantPrefab(prefabFolder, "c1", out centerPrefabAssetPath)
            || !TryFindBreedVariantPrefab(prefabFolder, "c2", out leftPrefabAssetPath)
            || !TryFindBreedVariantPrefab(prefabFolder, "c3", out rightPrefabAssetPath))
        {
            return false;
        }

        entry = new PawPalBreedShopPreviewEntry
        {
            BreedKey = animationEntry.CanonicalKey,
            LeftPrefabAssetPath = leftPrefabAssetPath,
            CenterPrefabAssetPath = centerPrefabAssetPath,
            RightPrefabAssetPath = rightPrefabAssetPath
        };
        return true;
    }

    private static bool TryFindBreedVariantPrefab(string prefabFolder, string variantSuffix, out string assetPath)
    {
        assetPath = string.Empty;
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { prefabFolder });
        if (guids == null || guids.Length == 0)
        {
            return false;
        }

        string requiredSuffix = "_" + variantSuffix + ".prefab";
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            string fileName = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            if (!fileName.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string normalized = fileName.ToLowerInvariant();
            if (normalized.Contains("_lod_")
                || normalized.Contains("_lowpoly_")
                || normalized.Contains("_noalpha_")
                || normalized.Contains("_anim_"))
            {
                continue;
            }

            assetPath = path;
            return true;
        }

        return false;
    }

    private static GameObject LoadPrefab(string assetPath)
    {
        return string.IsNullOrWhiteSpace(assetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }
#endif
}
