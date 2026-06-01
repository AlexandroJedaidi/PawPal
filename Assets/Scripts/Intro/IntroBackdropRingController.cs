using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroBackdropRingController : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedIntroBackdropRing";
    private const string DayBackdropResourcePath = "WindowBackdrops/IntroPet_day";
    private const string NightBackdropResourcePath = "WindowBackdrops/IntroPet_night";
    [Header("Time")]
    [SerializeField] private bool useDeviceLocalTime = true;
    [SerializeField] private bool usePreviewHour;
    [SerializeField, Range(0f, 24f)] private float previewHour = 12f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 7f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 20f;
    [SerializeField, Range(0.1f, 6f)] private float transitionHours = 1f;

    [Header("Assets")]
    [SerializeField] private Texture2D dayBackdropTexture;
    [SerializeField] private Texture2D nightBackdropTexture;

    [Header("Layout Defaults")]
    [SerializeField] private Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(9.9f, 1f, 8.1f));
    [SerializeField] private float perimeterPadding = 5.5f;
    [SerializeField] private float sideWidthPadding = 5f;
    [SerializeField] private float screenHeight = 7.5f;
    [SerializeField] private float screenCenterY = 3.85f;

    private Renderer[] screenRenderers = new Renderer[0];
    private IntroSceneAssetCatalog catalog;
    private Material dayMaterial;
    private Material nightMaterial;
    private int lastAppliedMinuteStamp = int.MinValue;
    private static Mesh quadMesh;
    private bool applyingLayout;

    public bool IsApplyingLayout
    {
        get { return applyingLayout; }
    }

    public void Configure(IntroSceneAssetCatalog nextCatalog, Bounds nextFieldBounds)
    {
        catalog = nextCatalog;
        fieldBounds = nextFieldBounds;
        dayBackdropTexture = ResolveTexture(catalog != null ? catalog.DayBackdropTexture : null, DayBackdropResourcePath);
        nightBackdropTexture = ResolveTexture(catalog != null ? catalog.NightBackdropTexture : null, NightBackdropResourcePath);
        EnsureCatalogLayoutDefaults();
        Rebuild();
    }

    public PawPalTimeOfDayState EvaluateTimeOfDayState()
    {
        return PawPalTimeOfDayEvaluator.Evaluate(
            useDeviceLocalTime,
            usePreviewHour,
            previewHour,
            sunriseHour,
            sunsetHour,
            transitionHours);
    }

    public void SaveScreenLayoutFromTransform(int screenIndex, Vector3 localPosition, Vector3 localEulerAngles, Vector2 size)
    {
        if (applyingLayout)
        {
            return;
        }

        EnsureCatalogLayoutDefaults();
        IntroBackdropScreenLayout[] layouts = GetActiveLayouts();
        if (layouts == null || screenIndex < 0 || screenIndex >= layouts.Length)
        {
            return;
        }

        layouts[screenIndex].LocalPosition = localPosition;
        layouts[screenIndex].LocalEulerAngles = NormalizeEulerAngles(localEulerAngles);
        layouts[screenIndex].Size = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));

#if UNITY_EDITOR
        if (catalog != null)
        {
            EditorUtility.SetDirty(catalog);
        }
#endif
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void Update()
    {
        bool hasAnyRenderer = false;
        for (int i = 0; i < screenRenderers.Length; i++)
        {
            if (screenRenderers[i] != null)
            {
                hasAnyRenderer = true;
                break;
            }
        }

        if (!hasAnyRenderer)
        {
            return;
        }

        ApplyTimeOfDayState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        perimeterPadding = Mathf.Max(0.1f, perimeterPadding);
        sideWidthPadding = Mathf.Max(0f, sideWidthPadding);
        screenHeight = Mathf.Max(0.5f, screenHeight);
        screenCenterY = Mathf.Max(0.25f, screenCenterY);
        transitionHours = Mathf.Clamp(transitionHours, 0.1f, 6f);
        fieldBounds.size = new Vector3(
            Mathf.Max(1f, fieldBounds.size.x),
            Mathf.Max(0.1f, fieldBounds.size.y),
            Mathf.Max(1f, fieldBounds.size.z));

        EnsureCatalogLayoutDefaults();
        if (isActiveAndEnabled)
        {
            Rebuild();
        }
    }
#endif

    private void OnDisable()
    {
        ClearGenerated();
        ReleaseMaterials();
    }

    [ContextMenu("Rebuild Intro Backdrop Ring")]
    private void Rebuild()
    {
        EnsureCatalogLayoutDefaults();
        ClearGenerated();
        ReleaseMaterials();

        if (dayBackdropTexture == null && nightBackdropTexture == null)
        {
            return;
        }

        IntroBackdropScreenLayout[] layouts = GetActiveLayouts();
        if (layouts == null || layouts.Length == 0)
        {
            return;
        }

        applyingLayout = true;
        Transform generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);

        dayMaterial = CreateMaterial(dayBackdropTexture, "IntroBackdropRingDay");
        nightMaterial = CreateMaterial(nightBackdropTexture, "IntroBackdropRingNight");
        screenRenderers = new Renderer[layouts.Length];

        for (int i = 0; i < layouts.Length; i++)
        {
            IntroBackdropScreenLayout layout = layouts[i];
            screenRenderers[i] = CreateScreenRenderer(
                generatedRoot,
                "BackdropScreen_" + i,
                layout.LocalPosition,
                Quaternion.Euler(layout.LocalEulerAngles),
                layout.Size);

            IntroBackdropScreenHandle handle = screenRenderers[i].GetComponent<IntroBackdropScreenHandle>();
            if (handle == null)
            {
                handle = screenRenderers[i].gameObject.AddComponent<IntroBackdropScreenHandle>();
            }

            handle.Initialize(this, i);
        }

        ApplyTimeOfDayState(true);
        applyingLayout = false;
    }

    private void ApplyTimeOfDayState()
    {
        ApplyTimeOfDayState(false);
    }

    private void ApplyTimeOfDayState(bool force)
    {
        PawPalTimeOfDayState state = EvaluateTimeOfDayState();
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        bool useNightBackdrop = state.Daylight01 <= 0.001f;
        if (!force && minuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        Material activeMaterial = useNightBackdrop ? nightMaterial : dayMaterial;
        if (activeMaterial == null)
        {
            activeMaterial = useNightBackdrop ? dayMaterial : nightMaterial;
        }

        for (int i = 0; i < screenRenderers.Length; i++)
        {
            Renderer renderer = screenRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = activeMaterial != null;
            if (activeMaterial != null)
            {
                renderer.sharedMaterial = activeMaterial;
            }
        }
    }

    private void EnsureCatalogLayoutDefaults()
    {
        if (catalog != null && catalog.BackdropScreens != null && catalog.BackdropScreens.Length > 0)
        {
            return;
        }

        IntroBackdropScreenLayout[] defaults = BuildDefaultLayouts();
        if (catalog != null)
        {
            catalog.BackdropScreens = defaults;
#if UNITY_EDITOR
            EditorUtility.SetDirty(catalog);
#endif
            return;
        }
    }

    private IntroBackdropScreenLayout[] GetActiveLayouts()
    {
        if (catalog != null && catalog.BackdropScreens != null && catalog.BackdropScreens.Length > 0)
        {
            return catalog.BackdropScreens;
        }

        return BuildDefaultLayouts();
    }

    private IntroBackdropScreenLayout[] BuildDefaultLayouts()
    {
        Vector3 center = fieldBounds.center;
        Vector3 extents = fieldBounds.extents;

        return new[]
        {
            new IntroBackdropScreenLayout
            {
                LocalPosition = center + (Vector3.forward * (extents.z + perimeterPadding)) + (Vector3.up * screenCenterY),
                LocalEulerAngles = new Vector3(0f, 180f, 0f),
                Size = new Vector2(fieldBounds.size.x + sideWidthPadding, screenHeight)
            },
            new IntroBackdropScreenLayout
            {
                LocalPosition = center + (Vector3.left * (extents.x + perimeterPadding)) + (Vector3.up * screenCenterY),
                LocalEulerAngles = new Vector3(0f, 90f, 0f),
                Size = new Vector2(fieldBounds.size.z + sideWidthPadding, screenHeight)
            },
            new IntroBackdropScreenLayout
            {
                LocalPosition = center + (Vector3.right * (extents.x + perimeterPadding)) + (Vector3.up * screenCenterY),
                LocalEulerAngles = new Vector3(0f, -90f, 0f),
                Size = new Vector2(fieldBounds.size.z + sideWidthPadding, screenHeight)
            }
        };
    }

    private void ClearGenerated()
    {
        screenRenderers = new Renderer[0];

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != GeneratedRootName)
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

    private static Renderer CreateScreenRenderer(Transform parent, string screenName, Vector3 localPosition, Quaternion localRotation, Vector2 size)
    {
        GameObject screen = new GameObject(screenName, typeof(MeshFilter), typeof(MeshRenderer));
        screen.transform.SetParent(parent, false);
        screen.transform.localPosition = localPosition;
        screen.transform.localRotation = localRotation;
        screen.transform.localScale = new Vector3(size.x, size.y, 1f);

        MeshFilter filter = screen.GetComponent<MeshFilter>();
        filter.sharedMesh = GetQuadMesh();

        MeshRenderer renderer = screen.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return renderer;
    }

    private static Material CreateMaterial(Texture2D texture, string materialName)
    {
        if (texture == null)
        {
            return null;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.DontSave;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        return material;
    }

    private void ReleaseMaterials()
    {
        ReleaseMaterial(ref dayMaterial);
        ReleaseMaterial(ref nightMaterial);
    }

    private static void ReleaseMaterial(ref Material material)
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

    private static Texture2D ResolveTexture(Texture2D assignedTexture, string resourcePath)
    {
        return assignedTexture != null ? assignedTexture : Resources.Load<Texture2D>(resourcePath);
    }

    private static Vector3 NormalizeEulerAngles(Vector3 eulerAngles)
    {
        return new Vector3(
            NormalizeAngle(eulerAngles.x),
            NormalizeAngle(eulerAngles.y),
            NormalizeAngle(eulerAngles.z));
    }

    private static float NormalizeAngle(float angle)
    {
        float normalized = angle % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null)
        {
            return quadMesh;
        }

        quadMesh = new Mesh();
        quadMesh.name = "IntroBackdropRingQuad";
        quadMesh.hideFlags = HideFlags.DontSave;
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
