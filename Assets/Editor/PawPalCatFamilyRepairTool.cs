using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class PawPalCatFamilyRepairTool
{
    private const string CatFamilyRoot = "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily";
    private const string SimplePrefabDirectory = CatFamilyRoot + "/Cats/Cat_Simple/Cat/Prefabs";
    private const string SimpleMaterialsDirectory = CatFamilyRoot + "/Cats/Cat_Simple/Cat/Materials";
    private const string SimpleAccessoryMaterialsDirectory = CatFamilyRoot + "/Cats/Cat_Simple/Accessories/Materials";
    private const string KittenPrefabDirectory = CatFamilyRoot + "/Kittens/KittenSimple/Kitten_Simple/Prefabs";
    private const string KittenMaterialsDirectory = CatFamilyRoot + "/Kittens/KittenSimple/Kitten_Simple/Materials";
    private const string KittenAccessoryMaterialsDirectory = CatFamilyRoot + "/Kittens/KittenSimple/KittenAccessories/Materials";
    private const string StrayPrefabDirectory = CatFamilyRoot + "/Cats/Cat_Stray/CatStray/Prefabs";
    private const string StrayMaterialsDirectory = CatFamilyRoot + "/Cats/Cat_Stray/CatStray/Materials";
    private const string FatPrefabDirectory = CatFamilyRoot + "/Cats/Cat_Fat/CatFat/Prefabs";
    private const string FatMaterialsDirectory = CatFamilyRoot + "/Cats/Cat_Fat/CatFat/Materials";

    private const string SimpleSourceAssetPath = CatFamilyRoot + "/Cats/Cat_Simple/Cat/FBX/Anim/Cat_Simple_anim_IP.fbx";
    private const string KittenSourceAssetPath = CatFamilyRoot + "/Kittens/KittenSimple/Kitten_Simple/FBX/Anim/KittenSimple_anim_IP.fbx";
    private const string KittenRuntimeAnimationAssetPath = CatFamilyRoot + "/Kittens/KittenSimple/Kitten_Simple/FBX/Anim/KittenSimple_anim_RM.fbx";
    private const string StraySourceAssetPath = CatFamilyRoot + "/Cats/Cat_Stray/CatStray/FBX/Anim/CatStray_anim_IP.fbx";
    private const string StrayRuntimeAnimationAssetPath = CatFamilyRoot + "/Cats/Cat_Stray/CatStray/FBX/Anim/CatStray_anim_RM.fbx";
    private const string SkinnySourceAssetPath = CatFamilyRoot + "/Cats/Cat_Stray/CatStray/FBX/CatSkinny.fbx";
    private const string FatSourceAssetPath = CatFamilyRoot + "/Cats/Cat_Fat/CatFat/FBX/Anim/CatFat_anim_IP.fbx";

    private static readonly string[] TargetDirectories =
    {
        SimplePrefabDirectory,
        KittenPrefabDirectory,
        StrayPrefabDirectory,
        FatPrefabDirectory
    };

    [MenuItem("PawPal/Rebuild Or Repair Cat Prefabs")]
    private static void RepairCatFamily()
    {
        int importerChanges = RepairModelImporters();
        PawPalPetAnimationRegistry.ClearEditorCaches();

        int repairedPrefabs = 0;
        int skippedPrefabs = 0;
        int failedPrefabs = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", TargetDirectories);
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]).Replace('\\', '/');
            if (!ShouldRepairPrefab(prefabPath))
            {
                continue;
            }

            try
            {
                if (RepairPrefab(prefabPath))
                {
                    repairedPrefabs++;
                }
                else
                {
                    skippedPrefabs++;
                }
            }
            catch (Exception ex)
            {
                failedPrefabs++;
                Debug.LogWarning("PawPal cat prefab repair failed for '" + prefabPath + "': " + ex.Message);
            }
        }

        PawPalPetAnimationRegistry.ClearEditorCaches();
        PawPalCatBaseControllerBuilder.RebuildNow(true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "PawPal cat prefab repair completed. Importers updated=" + importerChanges
            + ", prefabs repaired=" + repairedPrefabs
            + ", prefabs skipped=" + skippedPrefabs
            + ", prefabs failed=" + failedPrefabs + ".");
    }

    private static int RepairModelImporters()
    {
        int changes = 0;

        changes += ConfigureSourceImporter(SimpleSourceAssetPath);
        changes += ConfigureSourceImporter(KittenSourceAssetPath);
        changes += ConfigureSourceImporter(StraySourceAssetPath);
        changes += ConfigureSourceImporter(SkinnySourceAssetPath);
        changes += ConfigureSourceImporter(FatSourceAssetPath);

        changes += ConfigureRuntimeAnimationImporter(KittenRuntimeAnimationAssetPath, KittenSourceAssetPath);
        changes += ConfigureRuntimeAnimationImporter(StrayRuntimeAnimationAssetPath, StraySourceAssetPath);

        return changes;
    }

    private static int ConfigureSourceImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("PawPal cat prefab repair could not find source importer at '" + assetPath + "'.");
            return 0;
        }

        bool changed = false;
        if (importer.importAnimation != true)
        {
            importer.importAnimation = true;
            changed = true;
        }

        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            return 1;
        }

        return 0;
    }

    private static int ConfigureRuntimeAnimationImporter(string assetPath, string avatarSourceAssetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("PawPal cat prefab repair could not find runtime animation importer at '" + assetPath + "'.");
            return 0;
        }

        Avatar sourceAvatar = LoadAvatar(avatarSourceAssetPath);
        if (sourceAvatar == null)
        {
            Debug.LogWarning("PawPal cat prefab repair could not resolve source avatar '" + avatarSourceAssetPath + "' for '" + assetPath + "'.");
            return 0;
        }

        bool changed = false;
        if (importer.importAnimation != true)
        {
            importer.importAnimation = true;
            changed = true;
        }

        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            changed = true;
        }

        if (importer.sourceAvatar != sourceAvatar)
        {
            importer.sourceAvatar = sourceAvatar;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            return 1;
        }

        return 0;
    }

    private static bool RepairPrefab(string prefabPath)
    {
        string sourceAssetPath = ResolveGeometrySourceAssetPath(prefabPath);
        if (string.IsNullOrEmpty(sourceAssetPath))
        {
            return false;
        }

        string avatarSourceAssetPath = ResolveAvatarSourceAssetPath(prefabPath);
        if (string.IsNullOrEmpty(avatarSourceAssetPath))
        {
            avatarSourceAssetPath = sourceAssetPath;
        }

        string materialDonorPath = ResolveMaterialDonorAssetPath(prefabPath);

        GameObject targetRoot = null;
        GameObject sourceRoot = null;
        GameObject donorRoot = null;
        bool sourceLoadedFromPrefabContents = false;
        bool donorLoadedFromPrefabContents = false;
        bool changed = false;

        try
        {
            targetRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            sourceRoot = LoadAssetRoot(sourceAssetPath, out sourceLoadedFromPrefabContents);
            donorRoot = string.IsNullOrEmpty(materialDonorPath)
                ? null
                : LoadAssetRoot(materialDonorPath, out donorLoadedFromPrefabContents);

            if (targetRoot == null || sourceRoot == null)
            {
                return false;
            }

            Dictionary<string, SkinnedMeshRenderer> sourceSkinned = BuildSkinnedMeshMap(sourceRoot.transform);
            Dictionary<string, MeshRenderer> sourceMeshRenderers = BuildMeshRendererMap(sourceRoot.transform);
            Dictionary<string, MeshFilter> sourceMeshFilters = BuildMeshFilterMap(sourceRoot.transform);

            Dictionary<string, SkinnedMeshRenderer> donorSkinned = donorRoot != null
                ? BuildSkinnedMeshMap(donorRoot.transform)
                : sourceSkinned;
            Dictionary<string, MeshRenderer> donorMeshRenderers = donorRoot != null
                ? BuildMeshRendererMap(donorRoot.transform)
                : sourceMeshRenderers;
            Dictionary<string, MeshFilter> donorMeshFilters = donorRoot != null
                ? BuildMeshFilterMap(donorRoot.transform)
                : sourceMeshFilters;

            SkinnedMeshRenderer[] targetSkinned = targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < targetSkinned.Length; i++)
            {
                changed |= RepairSkinnedMeshRenderer(
                    targetRoot.transform,
                    targetSkinned[i],
                    sourceSkinned,
                    donorSkinned);
            }

            MeshRenderer[] targetMeshRenderers = targetRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < targetMeshRenderers.Length; i++)
            {
                changed |= RepairMeshRenderer(
                    targetRoot.transform,
                    targetMeshRenderers[i],
                    sourceMeshRenderers,
                    sourceMeshFilters,
                    donorMeshRenderers,
                    donorMeshFilters);
            }

            Avatar avatar = LoadAvatar(avatarSourceAssetPath);
            if (avatar != null)
            {
                Animator[] animators = targetRoot.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator animator = animators[i];
                    if (animator != null && animator.avatar != avatar)
                    {
                        animator.avatar = avatar;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(targetRoot, prefabPath);
            }

            return changed;
        }
        finally
        {
            if (targetRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }

            UnloadAssetRoot(sourceRoot, sourceLoadedFromPrefabContents);
            UnloadAssetRoot(donorRoot, donorLoadedFromPrefabContents);
        }
    }

    private static bool RepairSkinnedMeshRenderer(
        Transform targetRoot,
        SkinnedMeshRenderer targetRenderer,
        Dictionary<string, SkinnedMeshRenderer> sourceSkinned,
        Dictionary<string, SkinnedMeshRenderer> donorSkinned)
    {
        if (targetRoot == null || targetRenderer == null)
        {
            return false;
        }

        string path = GetRelativePath(targetRoot, targetRenderer.transform);
        SkinnedMeshRenderer sourceRenderer = FindBestSkinnedMeshRenderer(path, targetRenderer.name, sourceSkinned);
        if (sourceRenderer == null)
        {
            return false;
        }

        bool changed = false;
        bool isAccessoryRenderer = IsAccessoryRenderer(targetRenderer.name);

        if (targetRenderer.sharedMesh != sourceRenderer.sharedMesh)
        {
            targetRenderer.sharedMesh = sourceRenderer.sharedMesh;
            changed = true;
        }

        Transform resolvedRootBone = ResolveTargetTransform(targetRoot, sourceRenderer.rootBone);
        if (resolvedRootBone != null && targetRenderer.rootBone != resolvedRootBone)
        {
            targetRenderer.rootBone = resolvedRootBone;
            changed = true;
        }

        if (sourceRenderer.bones != null && sourceRenderer.bones.Length > 0)
        {
            Transform[] repairedBones = new Transform[sourceRenderer.bones.Length];
            bool allBonesResolved = true;
            for (int i = 0; i < sourceRenderer.bones.Length; i++)
            {
                repairedBones[i] = ResolveTargetTransform(targetRoot, sourceRenderer.bones[i]);
                allBonesResolved &= repairedBones[i] != null;
            }

            if (allBonesResolved && !BonesEqual(targetRenderer.bones, repairedBones))
            {
                targetRenderer.bones = repairedBones;
                changed = true;
            }
        }

        SkinnedMeshRenderer donorRenderer = FindBestSkinnedMeshRenderer(path, targetRenderer.name, donorSkinned) ?? sourceRenderer;
        Material[] preferredMaterials = isAccessoryRenderer
            ? (donorRenderer != null ? donorRenderer.sharedMaterials : targetRenderer.sharedMaterials)
            : sourceRenderer.sharedMaterials;

        if (HasBadMaterialReferences(preferredMaterials))
        {
            preferredMaterials = donorRenderer != null ? donorRenderer.sharedMaterials : preferredMaterials;
        }

        preferredMaterials = NormalizeMaterials(preferredMaterials, targetRenderer.name, targetRoot.gameObject.name, isAccessoryRenderer);

        if (preferredMaterials != null && !MaterialsEqual(targetRenderer.sharedMaterials, preferredMaterials))
        {
            targetRenderer.sharedMaterials = preferredMaterials;
            changed = true;
        }

        return changed;
    }

    private static bool RepairMeshRenderer(
        Transform targetRoot,
        MeshRenderer targetRenderer,
        Dictionary<string, MeshRenderer> sourceMeshRenderers,
        Dictionary<string, MeshFilter> sourceMeshFilters,
        Dictionary<string, MeshRenderer> donorMeshRenderers,
        Dictionary<string, MeshFilter> donorMeshFilters)
    {
        if (targetRoot == null || targetRenderer == null)
        {
            return false;
        }

        string path = GetRelativePath(targetRoot, targetRenderer.transform);
        MeshRenderer sourceRenderer = FindBestMeshRenderer(path, targetRenderer.name, sourceMeshRenderers);
        if (sourceRenderer == null)
        {
            return false;
        }

        bool changed = false;
        bool isAccessoryRenderer = IsAccessoryRenderer(targetRenderer.name);

        MeshFilter targetFilter = targetRenderer.GetComponent<MeshFilter>();
        MeshFilter sourceFilter = FindBestMeshFilter(path, targetRenderer.name, sourceMeshFilters);
        if (targetFilter != null && sourceFilter != null && targetFilter.sharedMesh != sourceFilter.sharedMesh)
        {
            targetFilter.sharedMesh = sourceFilter.sharedMesh;
            changed = true;
        }

        MeshRenderer donorRenderer = FindBestMeshRenderer(path, targetRenderer.name, donorMeshRenderers) ?? sourceRenderer;
        Material[] preferredMaterials = isAccessoryRenderer
            ? (donorRenderer != null ? donorRenderer.sharedMaterials : targetRenderer.sharedMaterials)
            : sourceRenderer.sharedMaterials;

        if (HasBadMaterialReferences(preferredMaterials))
        {
            preferredMaterials = donorRenderer != null ? donorRenderer.sharedMaterials : preferredMaterials;
        }

        preferredMaterials = NormalizeMaterials(preferredMaterials, targetRenderer.name, targetRoot.gameObject.name, isAccessoryRenderer);

        if (preferredMaterials != null && !MaterialsEqual(targetRenderer.sharedMaterials, preferredMaterials))
        {
            targetRenderer.sharedMaterials = preferredMaterials;
            changed = true;
        }

        return changed;
    }

    private static GameObject LoadAssetRoot(string assetPath, out bool loadedFromPrefabContents)
    {
        loadedFromPrefabContents = false;
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        GameObject loadedRoot = null;
        try
        {
            loadedRoot = PrefabUtility.LoadPrefabContents(assetPath);
            if (loadedRoot != null)
            {
                loadedFromPrefabContents = true;
                return loadedRoot;
            }
        }
        catch
        {
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            return null;
        }

        return UnityEngine.Object.Instantiate(asset);
    }

    private static void UnloadAssetRoot(GameObject root, bool loadedFromPrefabContents)
    {
        if (root == null)
        {
            return;
        }

        if (loadedFromPrefabContents)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        UnityEngine.Object.DestroyImmediate(root);
    }

    private static Dictionary<string, SkinnedMeshRenderer> BuildSkinnedMeshMap(Transform root)
    {
        Dictionary<string, SkinnedMeshRenderer> map = new Dictionary<string, SkinnedMeshRenderer>(StringComparer.Ordinal);
        if (root == null)
        {
            return map;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string path = GetRelativePath(root, renderers[i].transform);
            if (!map.ContainsKey(path))
            {
                map[path] = renderers[i];
            }
        }

        return map;
    }

    private static Dictionary<string, MeshRenderer> BuildMeshRendererMap(Transform root)
    {
        Dictionary<string, MeshRenderer> map = new Dictionary<string, MeshRenderer>(StringComparer.Ordinal);
        if (root == null)
        {
            return map;
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string path = GetRelativePath(root, renderers[i].transform);
            if (!map.ContainsKey(path))
            {
                map[path] = renderers[i];
            }
        }

        return map;
    }

    private static Dictionary<string, MeshFilter> BuildMeshFilterMap(Transform root)
    {
        Dictionary<string, MeshFilter> map = new Dictionary<string, MeshFilter>(StringComparer.Ordinal);
        if (root == null)
        {
            return map;
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            string path = GetRelativePath(root, filters[i].transform);
            if (!map.ContainsKey(path))
            {
                map[path] = filters[i];
            }
        }

        return map;
    }

    private static SkinnedMeshRenderer FindBestSkinnedMeshRenderer(
        string path,
        string rendererName,
        Dictionary<string, SkinnedMeshRenderer> renderers)
    {
        if (renderers == null || renderers.Count == 0)
        {
            return null;
        }

        SkinnedMeshRenderer renderer;
        if (!string.IsNullOrEmpty(path) && renderers.TryGetValue(path, out renderer))
        {
            return renderer;
        }

        foreach (KeyValuePair<string, SkinnedMeshRenderer> pair in renderers)
        {
            if (pair.Value != null && string.Equals(pair.Value.name, rendererName, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        foreach (KeyValuePair<string, SkinnedMeshRenderer> pair in renderers)
        {
            return pair.Value;
        }

        return null;
    }

    private static MeshRenderer FindBestMeshRenderer(
        string path,
        string rendererName,
        Dictionary<string, MeshRenderer> renderers)
    {
        if (renderers == null || renderers.Count == 0)
        {
            return null;
        }

        MeshRenderer renderer;
        if (!string.IsNullOrEmpty(path) && renderers.TryGetValue(path, out renderer))
        {
            return renderer;
        }

        foreach (KeyValuePair<string, MeshRenderer> pair in renderers)
        {
            if (pair.Value != null && string.Equals(pair.Value.name, rendererName, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        foreach (KeyValuePair<string, MeshRenderer> pair in renderers)
        {
            return pair.Value;
        }

        return null;
    }

    private static MeshFilter FindBestMeshFilter(
        string path,
        string rendererName,
        Dictionary<string, MeshFilter> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return null;
        }

        MeshFilter filter;
        if (!string.IsNullOrEmpty(path) && filters.TryGetValue(path, out filter))
        {
            return filter;
        }

        foreach (KeyValuePair<string, MeshFilter> pair in filters)
        {
            if (pair.Value != null && string.Equals(pair.Value.name, rendererName, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        foreach (KeyValuePair<string, MeshFilter> pair in filters)
        {
            return pair.Value;
        }

        return null;
    }

    private static Avatar LoadAvatar(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null)
            {
                return avatar;
            }
        }

        return null;
    }

    private static Transform ResolveTargetTransform(Transform targetRoot, Transform sourceTransform)
    {
        if (targetRoot == null || sourceTransform == null)
        {
            return null;
        }

        string path = GetRelativePath(sourceTransform.root, sourceTransform);
        return string.IsNullOrEmpty(path) ? targetRoot : targetRoot.Find(path);
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null)
        {
            return string.Empty;
        }

        if (root == target)
        {
            return string.Empty;
        }

        List<string> segments = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return string.Empty;
        }

        segments.Reverse();
        return string.Join("/", segments.ToArray());
    }

    private static bool MaterialsEqual(Material[] left, Material[] right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool BonesEqual(Transform[] left, Transform[] right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBadMaterialReferences(UnityEngine.Object[] objects)
    {
        if (objects == null || objects.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            Material material = objects[i] as Material;
            if (material == null || IsEmbeddedModelMaterial(material))
            {
                return true;
            }
        }

        return false;
    }

    private static Material[] NormalizeMaterials(Material[] materials, string rendererName, string prefabObjectName, bool isAccessoryRenderer)
    {
        if (materials == null || materials.Length == 0)
        {
            return materials;
        }

        Material[] normalized = new Material[materials.Length];
        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            Material resolved = material;
            if (material == null || IsEmbeddedModelMaterial(material))
            {
                resolved = ResolveStandaloneMaterial(prefabObjectName, rendererName, material != null ? material.name : null, isAccessoryRenderer) ?? material;
            }

            normalized[i] = resolved;
            changed |= normalized[i] != materials[i];
        }

        return changed ? normalized : materials;
    }

    private static Material ResolveStandaloneMaterial(string prefabObjectName, string rendererName, string sourceMaterialName, bool isAccessoryRenderer)
    {
        List<string> candidateNames = BuildMaterialCandidateNames(prefabObjectName, rendererName, sourceMaterialName, isAccessoryRenderer);
        string[] directories = GetMaterialSearchDirectories(prefabObjectName);

        for (int i = 0; i < candidateNames.Count; i++)
        {
            string candidate = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            for (int directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++)
            {
                string materialPath = directories[directoryIndex] + "/" + candidate + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material != null)
                {
                    return material;
                }
            }
        }

        return null;
    }

    private static List<string> BuildMaterialCandidateNames(string prefabObjectName, string rendererName, string sourceMaterialName, bool isAccessoryRenderer)
    {
        List<string> names = new List<string>();
        AddCandidateName(names, sourceMaterialName);

        string normalizedPrefabName = prefabObjectName ?? string.Empty;
        string normalizedRendererName = rendererName ?? string.Empty;
        string variant = NormalizeVariantKeySafe(ExtractVariantKey(normalizedPrefabName));
        string numericVariant = ExtractNumericVariant(variant);

        if (normalizedPrefabName.IndexOf("Cat_Simple", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("Cat_Collar", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("Cat_NoAlpha", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (isAccessoryRenderer && !string.IsNullOrEmpty(numericVariant))
            {
                AddCandidateName(names, "CatCollar_color_" + numericVariant);
            }

            AddCandidateName(names, "CatSim_color_" + numericVariant);
            AddCandidateName(names, "CatSim_color_M" + numericVariant);
        }
        else if (normalizedPrefabName.IndexOf("KittenSimple", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (isAccessoryRenderer && !string.IsNullOrEmpty(numericVariant))
            {
                AddCandidateName(names, "CatCollar_color_" + numericVariant);
                AddCandidateName(names, "Collar_color_" + numericVariant);
            }

            AddCandidateName(names, "KittenSim_color_" + numericVariant);
            if (isAccessoryRenderer)
            {
                AddCandidateName(names, "KittenSim_mobile_" + numericVariant);
            }
        }
        else if (normalizedPrefabName.IndexOf("CatStray", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("CatSkinny", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            bool isDirtyVariant = variant.IndexOf("D", StringComparison.OrdinalIgnoreCase) >= 0;
            if (normalizedPrefabName.IndexOf("CatSkinny", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddCandidateName(names, "SkinnyCat_color_" + numericVariant);
                AddCandidateName(names, "SkinnyCat_Mobile_" + numericVariant);
            }
            else
            {
                AddCandidateName(names, "StrayCat_color_" + numericVariant);
                AddCandidateName(names, "StrayCat_Mobile_" + numericVariant);
                if (isDirtyVariant)
                {
                    AddCandidateName(names, "StrayCat_color_" + numericVariant + "Dirt");
                    AddCandidateName(names, "StrayCat_color_" + numericVariant + "Damage");
                    AddCandidateName(names, "StrayCat_Mobile_" + numericVariant + "D");
                }
            }
        }
        else if (normalizedPrefabName.IndexOf("CatFat", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (isAccessoryRenderer && !string.IsNullOrEmpty(numericVariant))
            {
                AddCandidateName(names, "CatCollar_color_" + numericVariant);
            }

            AddCandidateName(names, "CatFat_color_" + numericVariant);
            AddCandidateName(names, "CatFat_mobile_" + numericVariant);
        }

        return names;
    }

    private static string[] GetMaterialSearchDirectories(string prefabObjectName)
    {
        string normalizedPrefabName = prefabObjectName ?? string.Empty;
        if (normalizedPrefabName.IndexOf("Cat_Simple", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("Cat_Collar", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("Cat_NoAlpha", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new[] { SimpleMaterialsDirectory, SimpleAccessoryMaterialsDirectory };
        }

        if (normalizedPrefabName.IndexOf("KittenSimple", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new[] { KittenMaterialsDirectory, KittenAccessoryMaterialsDirectory };
        }

        if (normalizedPrefabName.IndexOf("CatStray", StringComparison.OrdinalIgnoreCase) >= 0
            || normalizedPrefabName.IndexOf("CatSkinny", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new[] { StrayMaterialsDirectory };
        }

        if (normalizedPrefabName.IndexOf("CatFat", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new[] { FatMaterialsDirectory, SimpleAccessoryMaterialsDirectory };
        }

        return Array.Empty<string>();
    }

    private static bool IsAccessoryRenderer(string rendererName)
    {
        if (string.IsNullOrWhiteSpace(rendererName))
        {
            return false;
        }

        return rendererName.IndexOf("Collar", StringComparison.OrdinalIgnoreCase) >= 0
            || rendererName.IndexOf("Ears", StringComparison.OrdinalIgnoreCase) >= 0
            || rendererName.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsEmbeddedModelMaterial(Material material)
    {
        if (material == null)
        {
            return true;
        }

        string assetPath = AssetDatabase.GetAssetPath(material);
        return string.IsNullOrWhiteSpace(assetPath) || assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCandidateName(List<string> names, string candidate)
    {
        if (names == null || string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (!names.Contains(candidate))
        {
            names.Add(candidate);
        }
    }

    private static string ExtractNumericVariant(string variant)
    {
        if (string.IsNullOrWhiteSpace(variant))
        {
            return string.Empty;
        }

        for (int i = 0; i < variant.Length; i++)
        {
            if (char.IsDigit(variant[i]))
            {
                return variant[i].ToString();
            }
        }

        return string.Empty;
    }

    private static string ResolveGeometrySourceAssetPath(string prefabPath)
    {
        string normalizedPath = prefabPath.Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);

        if (normalizedPath.StartsWith(SimplePrefabDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return SimpleSourceAssetPath;
        }

        if (normalizedPath.StartsWith(KittenPrefabDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return KittenSourceAssetPath;
        }

        if (normalizedPath.StartsWith(FatPrefabDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return FatSourceAssetPath;
        }

        if (normalizedPath.StartsWith(StrayPrefabDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fileName.StartsWith("CatSkinny", StringComparison.OrdinalIgnoreCase)
                ? SkinnySourceAssetPath
                : StraySourceAssetPath;
        }

        return string.Empty;
    }

    private static string ResolveAvatarSourceAssetPath(string prefabPath)
    {
        return ResolveGeometrySourceAssetPath(prefabPath);
    }

    private static string ResolveMaterialDonorAssetPath(string prefabPath)
    {
        string geometrySource = ResolveGeometrySourceAssetPath(prefabPath);
        string fileName = Path.GetFileNameWithoutExtension(prefabPath);
        string directory = Path.GetDirectoryName(prefabPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return geometrySource;
        }

        directory = directory.Replace('\\', '/');
        string[] candidateNames =
        {
            fileName.Replace("_anim_RM", "_anim_IP"),
            ReplaceStyleToken(fileName, "_LOD_"),
            ReplaceStyleToken(fileName, "_LowPoly_"),
            ReplaceStyleToken(fileName, "_NoAlpha_"),
            ReplaceStyleToken(fileName, "_LOD"),
            ReplaceStyleToken(fileName, "_LowPoly"),
            ReplaceStyleToken(fileName, "_NoAlpha")
        };

        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidateName) || string.Equals(candidateName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string candidatePath = directory + "/" + candidateName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath) != null)
            {
                return candidatePath;
            }
        }

        string[] siblingGuids = AssetDatabase.FindAssets("t:Prefab", new[] { directory });
        string targetVariantKey = ExtractVariantKey(fileName);
        string normalizedVariantKey = NormalizeVariantKeySafe(targetVariantKey);

        for (int i = 0; i < siblingGuids.Length; i++)
        {
            string siblingPath = AssetDatabase.GUIDToAssetPath(siblingGuids[i]).Replace('\\', '/');
            if (string.Equals(siblingPath, prefabPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string siblingName = Path.GetFileNameWithoutExtension(siblingPath);
            if (string.IsNullOrWhiteSpace(siblingName))
            {
                continue;
            }

            bool donorStyle = siblingName.IndexOf("LOD", StringComparison.OrdinalIgnoreCase) >= 0
                || siblingName.IndexOf("LowPoly", StringComparison.OrdinalIgnoreCase) >= 0
                || siblingName.IndexOf("NoAlpha", StringComparison.OrdinalIgnoreCase) >= 0
                || siblingName.IndexOf("anim_IP", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!donorStyle)
            {
                continue;
            }

            if (!string.Equals(normalizedVariantKey, NormalizeVariantKeySafe(ExtractVariantKey(siblingName)), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return siblingPath;
        }

        return geometrySource;
    }

    private static string ReplaceStyleToken(string fileName, string replacementToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        int underscoreIndex = fileName.LastIndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex >= fileName.Length - 1)
        {
            return string.Empty;
        }

        return fileName.Substring(0, underscoreIndex) + replacementToken + fileName.Substring(underscoreIndex + 1);
    }

    private static string ExtractVariantKey(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        int separatorIndex = fileName.LastIndexOf('_');
        if (separatorIndex >= 0 && separatorIndex < fileName.Length - 1)
        {
            return fileName.Substring(separatorIndex + 1);
        }

        int spaceIndex = fileName.LastIndexOf(' ');
        if (spaceIndex >= 0 && spaceIndex < fileName.Length - 1)
        {
            return fileName.Substring(spaceIndex + 1);
        }

        return fileName;
    }

    private static string NormalizeVariantKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('С', 'C')
            .Replace('с', 'c')
            .Trim();
    }

    private static string NormalizeVariantKeySafe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('\u0421', 'C')
            .Replace('\u0441', 'c')
            .Trim();
    }

    private static bool ShouldRepairPrefab(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(prefabPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.IndexOf("LowPoly", StringComparison.OrdinalIgnoreCase) >= 0
            || fileName.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }
}
