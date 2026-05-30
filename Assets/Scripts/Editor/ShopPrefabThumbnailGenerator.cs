using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class ShopPrefabThumbnailGenerator
{
    private const string OutputFolder = "Assets/Resources/UI/Generated/Shop";
    private const string PreviewDogResourcePath = "PawPal/Preview/DogPreviewMannequin";
    private const int PreviewLayer = 31;
    private const int ThumbnailSize = 512;
    private const float StageOffset = 20000f;
    private const float StandaloneFramePadding = 1.35f;
    private const float WearableFocusPadding = 2.8f;
    private const float WearableDogHeightPadding = 0.18f;

    private static readonly string[] PreferredCollarPlacementReferenceNames =
    {
        "Collar",
        "CollarSimple_C1",
        "CollarSimple_C2",
        "CollarSimple_C3"
    };

    private static readonly ThumbnailJob[] Jobs =
    {
        new ThumbnailJob("toy_bubble_bone", "PawPal/RoomPrefabs/Bone_1", PawPalShopPreviewMode.StandaloneModel, new Color(0.88f, 0.34f, 0.58f, 1f)),
        new ThumbnailJob("toy_bouncy_ball", "PawPal/RoomPrefabs/Big_ball_1", PawPalShopPreviewMode.StandaloneModel, new Color(0.82f, 0.84f, 0.24f, 1f)),
        new ThumbnailJob("toy_turbo_roll", "PawPal/RoomPrefabs/Wheel", PawPalShopPreviewMode.StandaloneModel, new Color(0.40f, 0.60f, 0.86f, 1f)),
        new ThumbnailJob("toy_star_ball", "PawPal/RoomPrefabs/Big_ball_1", PawPalShopPreviewMode.StandaloneModel, new Color(0.93f, 0.83f, 0.36f, 1f)),
        new ThumbnailJob("collar_ocean_band", "PawPal/Accessories/CollarSimple_C2", PawPalShopPreviewMode.WearableOnDog, Color.white),
        new ThumbnailJob("collar_maple_loop", "PawPal/Accessories/CollarSimple_C1", PawPalShopPreviewMode.WearableOnDog, new Color(0.78f, 0.64f, 0.50f, 1f)),
        new ThumbnailJob("collar_cherry_charm", "PawPal/Accessories/CollarSimple_C1", PawPalShopPreviewMode.WearableOnDog, Color.white)
    };

    [MenuItem("PawFriends/Shop/Generate Prefab Thumbnails")]
    public static void GeneratePrefabThumbnails()
    {
        Directory.CreateDirectory(OutputFolder);

        try
        {
            for (int i = 0; i < Jobs.Length; i++)
            {
                ThumbnailJob job = Jobs[i];
                EditorUtility.DisplayProgressBar("Shop thumbnails", "Rendering " + job.Id, (float)i / Jobs.Length);
                RenderJob(job);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
    }

    private static void RenderJob(ThumbnailJob job)
    {
        GameObject prefab = Resources.Load<GameObject>(job.PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("ShopPrefabThumbnailGenerator could not load " + job.PrefabResourcePath + " for " + job.Id + ".");
            return;
        }

        RenderTexture renderTexture = null;
        GameObject stageRoot = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            renderTexture = new RenderTexture(ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32);
            renderTexture.antiAliasing = 4;
            renderTexture.Create();

            stageRoot = new GameObject("ShopThumbnailStage_" + job.Id);
            stageRoot.hideFlags = HideFlags.HideAndDontSave;
            stageRoot.transform.position = new Vector3(StageOffset, -StageOffset, StageOffset);

            Transform modelRoot = new GameObject("ModelRoot").transform;
            modelRoot.SetParent(stageRoot.transform, false);

            Transform focusRoot;
            if (job.PreviewMode == PawPalShopPreviewMode.WearableOnDog)
            {
                focusRoot = BuildWearablePreview(modelRoot, prefab, job.Tint);
            }
            else
            {
                GameObject model = Object.Instantiate(prefab, modelRoot, false);
                model.name = prefab.name;
                focusRoot = model.transform;
                StripPreviewOnlyComponents(model);
                ApplyTint(model, job.Tint);
            }

            SetLayerRecursively(stageRoot.transform, PreviewLayer);

            Camera camera = CreateCamera(stageRoot.transform, renderTexture);
            CreateLight(stageRoot.transform, "KeyLight", new Vector3(-1.8f, 2.2f, -2.2f), 2.4f, 8f);
            CreateLight(stageRoot.transform, "FillLight", new Vector3(2.2f, 1.4f, -1.2f), 0.9f, 7f);
            CreateLight(stageRoot.transform, "RimLight", new Vector3(0f, 1.8f, 2.4f), 0.75f, 7f);

            FrameModel(camera, modelRoot, focusRoot, job.PreviewMode);
            camera.Render();

            texture = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.ARGB32, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, ThumbnailSize, ThumbnailSize), 0, 0);
            texture.Apply();

            string outputPath = OutputFolder + "/" + job.Id + ".png";
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            ConfigureImportedSprite(outputPath);
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            if (stageRoot != null)
            {
                Object.DestroyImmediate(stageRoot);
            }
        }
    }

    private static Camera CreateCamera(Transform stageRoot, RenderTexture renderTexture)
    {
        GameObject cameraObject = new GameObject("ThumbnailCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.layer = PreviewLayer;
        cameraObject.transform.SetParent(stageRoot, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.99f, 0.97f, 0.91f, 0f);
        camera.cullingMask = 1 << PreviewLayer;
        camera.orthographic = true;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 60f;
        camera.targetTexture = renderTexture;
        camera.enabled = false;
        return camera;
    }

    private static void CreateLight(Transform stageRoot, string name, Vector3 localPosition, float intensity, float range)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.layer = PreviewLayer;
        lightObject.transform.SetParent(stageRoot, false);
        lightObject.transform.localPosition = localPosition;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
        light.cullingMask = 1 << PreviewLayer;
    }

    private static Transform BuildWearablePreview(Transform modelRoot, GameObject wearablePrefab, Color tint)
    {
        GameObject dogPrefab = Resources.Load<GameObject>(PreviewDogResourcePath);
        if (dogPrefab == null)
        {
            GameObject standalone = Object.Instantiate(wearablePrefab, modelRoot, false);
            StripPreviewOnlyComponents(standalone);
            ApplyTint(standalone, tint);
            return standalone.transform;
        }

        GameObject dog = Object.Instantiate(dogPrefab, modelRoot, false);
        dog.name = dogPrefab.name;
        StripPreviewOnlyComponents(dog);

        Transform placementReference;
        Transform parent = TryFindExistingCollarPlacementReference(dog.transform, out placementReference) && placementReference.parent != null
            ? placementReference.parent
            : dog.transform;
        Vector3 localPosition = placementReference != null ? placementReference.localPosition : Vector3.zero;
        Quaternion localRotation = placementReference != null ? placementReference.localRotation : Quaternion.identity;
        Vector3 localScale = placementReference != null ? placementReference.localScale : Vector3.one;
        HideBuiltInCollarSources(dog.transform);

        GameObject wearable = Object.Instantiate(wearablePrefab, parent, false);
        wearable.name = wearablePrefab.name;
        wearable.transform.localPosition = localPosition;
        wearable.transform.localRotation = localRotation;
        wearable.transform.localScale = localScale;
        StripPreviewOnlyComponents(wearable);
        ApplyTint(wearable, tint);
        return wearable.transform;
    }

    private static void FrameModel(Camera camera, Transform modelRoot, Transform focusRoot, PawPalShopPreviewMode mode)
    {
        Bounds fullBounds;
        if (!TryGetRendererBounds(modelRoot, out fullBounds))
        {
            return;
        }

        Vector3 pivot = modelRoot.position;
        modelRoot.position += pivot - fullBounds.center;

        if (!TryGetRendererBounds(modelRoot, out fullBounds))
        {
            return;
        }

        Bounds focusBounds;
        if (focusRoot == null || !TryGetRendererBounds(focusRoot, out focusBounds))
        {
            focusBounds = fullBounds;
        }

        bool wearableFocus = mode == PawPalShopPreviewMode.WearableOnDog && focusRoot != null;
        Vector3 center = wearableFocus ? focusBounds.center : fullBounds.center;
        float largestExtent = wearableFocus
            ? Mathf.Max(focusBounds.extents.x, focusBounds.extents.y, focusBounds.extents.z)
            : Mathf.Max(fullBounds.extents.x, fullBounds.extents.y, fullBounds.extents.z);
        float size = wearableFocus
            ? Mathf.Max(largestExtent * WearableFocusPadding, fullBounds.size.y * WearableDogHeightPadding)
            : largestExtent * StandaloneFramePadding;
        float orthographicSize = Mathf.Max(0.05f, size);
        float cameraDistance = Mathf.Clamp(orthographicSize * 9f, 2.5f, 18f);

        modelRoot.localRotation = Quaternion.Euler(0f, wearableFocus ? 18f : 24f, 0f);
        camera.orthographicSize = orthographicSize;
        camera.transform.position = center + new Vector3(0f, orthographicSize * 0.08f, -cameraDistance);
        camera.transform.LookAt(center + Vector3.up * orthographicSize * 0.02f);
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void ConfigureImportedSprite(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = ThumbnailSize;
        importer.SaveAndReimport();
    }

    private static void StripPreviewOnlyComponents(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }

        NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null)
            {
                agents[i].enabled = false;
            }
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = false;
            }
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].enabled = false;
            }
        }
    }

    private static void ApplyTint(GameObject instance, Color tint)
    {
        if (instance == null || ApproximatelyWhite(tint))
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            bool rendererChanged = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", tint);
                    rendererChanged = true;
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", tint);
                    rendererChanged = true;
                }
            }

            if (rendererChanged)
            {
                renderer.materials = materials;
            }
        }
    }

    private static bool ApproximatelyWhite(Color tint)
    {
        return Mathf.Abs(tint.r - 1f) < 0.0001f
            && Mathf.Abs(tint.g - 1f) < 0.0001f
            && Mathf.Abs(tint.b - 1f) < 0.0001f
            && Mathf.Abs(tint.a - 1f) < 0.0001f;
    }

    private static bool TryFindExistingCollarPlacementReference(Transform root, out Transform placementReference)
    {
        placementReference = null;
        if (root == null)
        {
            return false;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < PreferredCollarPlacementReferenceNames.Length; j++)
            {
                if (string.Equals(candidate.name, PreferredCollarPlacementReferenceNames[j], System.StringComparison.OrdinalIgnoreCase))
                {
                    placementReference = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static void HideBuiltInCollarSources(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < PreferredCollarPlacementReferenceNames.Length; j++)
            {
                if (string.Equals(candidate.name, PreferredCollarPlacementReferenceNames[j], System.StringComparison.OrdinalIgnoreCase))
                {
                    candidate.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private struct ThumbnailJob
    {
        public readonly string Id;
        public readonly string PrefabResourcePath;
        public readonly PawPalShopPreviewMode PreviewMode;
        public readonly Color Tint;

        public ThumbnailJob(string id, string prefabResourcePath, PawPalShopPreviewMode previewMode, Color tint)
        {
            Id = id;
            PrefabResourcePath = prefabResourcePath;
            PreviewMode = previewMode;
            Tint = tint;
        }
    }
}
