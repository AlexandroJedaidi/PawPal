using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class WindowBackdropImagePlane : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedWindowBackdropImage";
    private const string LegacyGeneratedRootName = "GeneratedWindowBackdrop";
    private const string DayCardName = "DayBackdropCard";
    private const string NightCardName = "NightBackdropCard";
    private const string DayBackdropResourcePath = "WindowBackdrops/window_exterior_day";
    private const string NightBackdropResourcePath = "WindowBackdrops/window_exterior_night";

    [Header("Build")]
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool rebuildInEditMode = true;

    [Header("Time")]
    [SerializeField] private bool useDeviceLocalTime = true;
    [SerializeField] private bool usePreviewHour;
    [SerializeField, Range(0f, 24f)] private float previewHour = 12f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 7f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 20f;
    [SerializeField, Range(0.1f, 6f)] private float transitionHours = 1f;

    [Header("Images")]
    [SerializeField] private Texture2D dayBackdropTexture;
    [SerializeField] private Texture2D nightBackdropTexture;

    [Header("Placement")]
    [SerializeField] private Transform anchor;
    [SerializeField] private bool autoResolveFromScene = true;
    [SerializeField] private bool autoSizeFromWindowBounds = true;
    [SerializeField] private Vector2 size = new Vector2(12f, 6.5f);
    [SerializeField] private Vector2 autoSizePadding = new Vector2(1.25f, 0.75f);
    [SerializeField] private float distance = 10f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Color tint = Color.white;
    [SerializeField]
    private string[] windowNameKeywords =
    {
        "Window_Frame_Apt_01",
        "Ext_Apt_01_Wall_Window_01"
    };

    private Material dayMaterial;
    private Material nightMaterial;
    private Renderer dayRenderer;
    private Renderer nightRenderer;
    private int lastAppliedMinuteStamp = int.MinValue;
    private static Mesh quadMesh;
#if UNITY_EDITOR
    private bool rebuildQueued;
#endif

    private void OnEnable()
    {
        if (rebuildOnEnable)
        {
            Rebuild();
        }
        else
        {
            ApplyBackdropState();
        }
    }

    private void Update()
    {
        if (dayMaterial == null && nightMaterial == null)
        {
            return;
        }

        PawPalTimeOfDayState state = EvaluateTimeOfDay();
        int nextMinuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        if (nextMinuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        ApplyBackdropState(state);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);
        autoSizePadding.x = Mathf.Max(0f, autoSizePadding.x);
        autoSizePadding.y = Mathf.Max(0f, autoSizePadding.y);
        distance = Mathf.Max(0.01f, distance);
        transitionHours = Mathf.Clamp(transitionHours, 0.1f, 6f);

        if (!Application.isPlaying && rebuildInEditMode)
        {
            QueueEditorRebuild();
        }
    }
#endif

    private void OnDisable()
    {
#if UNITY_EDITOR
        CancelQueuedEditorRebuild();
#endif
        if (!Application.isPlaying)
        {
            ClearGenerated();
        }

        ReleaseMaterials();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        CancelQueuedEditorRebuild();
#endif
    }

    public void ConfigureTimeSettings(
        bool nextUseDeviceLocalTime,
        bool nextUsePreviewHour,
        float nextPreviewHour,
        float nextSunriseHour,
        float nextSunsetHour,
        float nextTransitionHours)
    {
        useDeviceLocalTime = nextUseDeviceLocalTime;
        usePreviewHour = nextUsePreviewHour;
        previewHour = nextPreviewHour;
        sunriseHour = nextSunriseHour;
        sunsetHour = nextSunsetHour;
        transitionHours = nextTransitionHours;
        ApplyBackdropState();
    }

    [ContextMenu("Rebuild Window Image Plane")]
    public void Rebuild()
    {
        ClearGenerated();
        ReleaseMaterials();
        ClearLegacyGenerated();

        Texture2D resolvedDayTexture = ResolveBackdropTexture(dayBackdropTexture, DayBackdropResourcePath);
        Texture2D resolvedNightTexture = ResolveBackdropTexture(nightBackdropTexture, NightBackdropResourcePath);
        if (resolvedDayTexture == null && resolvedNightTexture == null)
        {
            return;
        }

        Vector3 position;
        Quaternion rotation;
        Vector2 resolvedSize;
        ResolvePlacement(out position, out rotation, out resolvedSize);

        Transform generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.position = position;
        generatedRoot.rotation = rotation;
        generatedRoot.localScale = Vector3.one;

        if (resolvedDayTexture != null)
        {
            dayMaterial = CreateMaterial(resolvedDayTexture);
            dayRenderer = CreateCard(DayCardName, generatedRoot, resolvedSize, 0f, dayMaterial);
        }

        if (resolvedNightTexture != null)
        {
            nightMaterial = CreateMaterial(resolvedNightTexture);
            nightRenderer = CreateCard(NightCardName, generatedRoot, resolvedSize, 0.002f, nightMaterial);
        }

        ApplyBackdropState();
    }

    [ContextMenu("Clear Window Image Plane")]
    public void ClearGenerated()
    {
        DestroyGeneratedChildrenByName(GeneratedRootName);

        dayRenderer = null;
        nightRenderer = null;
    }

    [ContextMenu("Clear Legacy Window Backdrop")]
    public void ClearLegacyGenerated()
    {
        DestroyGeneratedChildrenByName(LegacyGeneratedRootName);
    }

#if UNITY_EDITOR
    private void QueueEditorRebuild()
    {
        if (rebuildQueued)
        {
            return;
        }

        rebuildQueued = true;
        EditorApplication.delayCall += ExecuteQueuedEditorRebuild;
    }

    private void CancelQueuedEditorRebuild()
    {
        if (!rebuildQueued)
        {
            return;
        }

        rebuildQueued = false;
        EditorApplication.delayCall -= ExecuteQueuedEditorRebuild;
    }

    private void ExecuteQueuedEditorRebuild()
    {
        EditorApplication.delayCall -= ExecuteQueuedEditorRebuild;
        rebuildQueued = false;

        if (this == null ||
            !isActiveAndEnabled ||
            Application.isPlaying ||
            !rebuildInEditMode)
        {
            return;
        }

        Rebuild();
    }
#endif

    private void ApplyBackdropState()
    {
        ApplyBackdropState(EvaluateTimeOfDay());
    }

    private void DestroyGeneratedChildrenByName(string childName)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != childName)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ApplyBackdropState(PawPalTimeOfDayState state)
    {
        lastAppliedMinuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        float dayAlpha = dayMaterial != null ? state.Daylight01 : 0f;
        float nightAlpha = nightMaterial != null ? 1f - state.Daylight01 : 0f;

        if (dayMaterial != null)
        {
            ApplyTint(dayMaterial, tint, dayAlpha);
        }

        if (nightMaterial != null)
        {
            ApplyTint(nightMaterial, tint, nightAlpha);
        }

        if (dayRenderer != null)
        {
            dayRenderer.enabled = dayAlpha > 0.001f;
        }

        if (nightRenderer != null)
        {
            nightRenderer.enabled = nightAlpha > 0.001f;
        }
    }

    private PawPalTimeOfDayState EvaluateTimeOfDay()
    {
        return PawPalTimeOfDayEvaluator.Evaluate(
            useDeviceLocalTime,
            usePreviewHour,
            previewHour,
            sunriseHour,
            sunsetHour,
            transitionHours);
    }

    private void ResolvePlacement(out Vector3 position, out Quaternion rotation, out Vector2 resolvedSize)
    {
        if (anchor != null)
        {
            position = anchor.TransformPoint(localOffset + (Vector3.forward * distance));
            rotation = anchor.rotation;
            resolvedSize = size;
            return;
        }

        if (autoResolveFromScene && TryResolveWindowWallPose(out position, out rotation, out resolvedSize))
        {
            return;
        }

        position = transform.TransformPoint(localOffset + (Vector3.forward * distance));
        rotation = transform.rotation;
        resolvedSize = size;
    }

    private bool TryResolveWindowWallPose(out Vector3 position, out Quaternion rotation, out Vector2 resolvedSize)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        resolvedSize = size;

        Bounds windowBounds;
        if (!TryResolveWindowBounds(out windowBounds))
        {
            return false;
        }

        Bounds roomBounds = ResolveSceneBounds();
        Vector3 outwardNormal = ResolveNearestRoomFaceNormal(roomBounds, windowBounds.center);
        rotation = Quaternion.LookRotation(outwardNormal, Vector3.up);

        Vector2 autoSize = size;
        if (Mathf.Abs(outwardNormal.x) > Mathf.Abs(outwardNormal.z))
        {
            autoSize = new Vector2(windowBounds.size.z, windowBounds.size.y);
        }
        else
        {
            autoSize = new Vector2(windowBounds.size.x, windowBounds.size.y);
        }

        if (autoSizeFromWindowBounds)
        {
            autoSize += autoSizePadding;
            autoSize.x = Mathf.Max(autoSize.x, size.x);
            autoSize.y = Mathf.Max(autoSize.y, size.y);
        }
        else
        {
            autoSize = size;
        }

        position = windowBounds.center + outwardNormal * distance + (rotation * localOffset);
        resolvedSize = autoSize;
        return true;
    }

    private bool TryResolveWindowBounds(out Bounds bounds)
    {
        Renderer[] renderers = FindSceneObjects<Renderer>();
        bool foundAny = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneRendererCandidate(renderer) || !MatchesWindowKeyword(renderer.name))
            {
                continue;
            }

            if (!foundAny)
            {
                bounds = renderer.bounds;
                foundAny = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return foundAny;
    }

    private Bounds ResolveSceneBounds()
    {
        Renderer[] renderers = FindSceneObjects<Renderer>();
        bool foundAny = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneRendererCandidate(renderer) || IsEffectRenderer(renderer))
            {
                continue;
            }

            if (!foundAny)
            {
                bounds = renderer.bounds;
                foundAny = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private Vector3 ResolveNearestRoomFaceNormal(Bounds roomBounds, Vector3 windowCenter)
    {
        float distanceToMinX = Mathf.Abs(windowCenter.x - roomBounds.min.x);
        float distanceToMaxX = Mathf.Abs(windowCenter.x - roomBounds.max.x);
        float distanceToMinZ = Mathf.Abs(windowCenter.z - roomBounds.min.z);
        float distanceToMaxZ = Mathf.Abs(windowCenter.z - roomBounds.max.z);

        float nearestDistance = distanceToMaxX;
        Vector3 normal = Vector3.right;

        if (distanceToMinX < nearestDistance)
        {
            nearestDistance = distanceToMinX;
            normal = Vector3.left;
        }

        if (distanceToMaxZ < nearestDistance)
        {
            nearestDistance = distanceToMaxZ;
            normal = Vector3.forward;
        }

        if (distanceToMinZ < nearestDistance)
        {
            normal = Vector3.back;
        }

        return normal;
    }

    private Renderer CreateCard(string name, Transform parent, Vector2 cardSize, float localDepthOffset, Material material)
    {
        GameObject card = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        card.transform.SetParent(parent, false);
        card.transform.localPosition = new Vector3(0f, 0f, localDepthOffset);
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = new Vector3(cardSize.x, cardSize.y, 1f);

        MeshFilter meshFilter = card.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetQuadMesh();

        MeshRenderer meshRenderer = card.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return meshRenderer;
    }

    private Texture2D ResolveBackdropTexture(Texture2D assignedTexture, string resourcePath)
    {
        if (assignedTexture != null)
        {
            return assignedTexture;
        }

        return Resources.Load<Texture2D>(resourcePath);
    }

    private void ReleaseMaterials()
    {
        ReleaseMaterial(ref dayMaterial);
        ReleaseMaterial(ref nightMaterial);
    }

    private void ReleaseMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }

        material = null;
    }

    private static Material CreateMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader);
        material.name = "WindowBackdropImagePlaneMaterial";
        material.hideFlags = HideFlags.DontSave;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static void ApplyTint(Material material, Color baseTint, float alpha)
    {
        Color color = new Color(baseTint.r, baseTint.g, baseTint.b, Mathf.Clamp01(alpha) * baseTint.a);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private bool MatchesWindowKeyword(string rendererName)
    {
        if (string.IsNullOrWhiteSpace(rendererName) || windowNameKeywords == null)
        {
            return false;
        }

        for (int i = 0; i < windowNameKeywords.Length; i++)
        {
            string keyword = windowNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) &&
                rendererName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSceneRendererCandidate(Renderer renderer)
    {
        return renderer != null &&
               renderer.gameObject.scene == gameObject.scene &&
               renderer.enabled &&
               !renderer.transform.IsChildOf(transform);
    }

    private bool IsEffectRenderer(Renderer renderer)
    {
        return renderer is ParticleSystemRenderer ||
               renderer is LineRenderer ||
               renderer is TrailRenderer;
    }

    private T[] FindSceneObjects<T>() where T : Component
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null)
        {
            return quadMesh;
        }

        quadMesh = new Mesh();
        quadMesh.name = "WindowBackdropImagePlaneQuad";
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        quadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
        return quadMesh;
    }
}
