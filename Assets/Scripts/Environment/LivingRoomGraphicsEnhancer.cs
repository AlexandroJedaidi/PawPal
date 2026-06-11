using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class LivingRoomGraphicsEnhancer : MonoBehaviour
{
    private const string AutoBootstrapSceneName = "David_Test";
    private const string RuntimeEnhancerObjectName = "Runtime Living Room Graphics Enhancer";
    private const string WindowFillLightName = "Window Soft Fill";
    private const string SofaFillLightName = "Sofa Warm Fill";
    private const string ReflectionProbeName = "Living Room Reflection Probe";
    private const string WindowBackdropObjectName = "Window Backdrop";
    private const float RoomFootprintPadding = 0.85f;
    private const float RoomRendererMaxFootprintMultiplier = 1.8f;
    private const float MinimumRoomBoundsHeight = 3.25f;
    private const float MinimumRoomBoundsCenterY = 1.4f;

    [Header("Apply")]
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool configureMainCamera = true;
    [SerializeField] private bool configureRenderSettings = true;
    [SerializeField] private bool configureSkybox;
    [SerializeField] private bool configurePostProcessing = true;
    [SerializeField] private bool configureReflectionProbe = true;
    [SerializeField] private bool configureWindowBackdrop = true;
    [SerializeField] private bool configureQualityInPlayMode = true;

    [Header("Time Of Day")]
    [SerializeField] private bool useDeviceLocalTime = true;
    [SerializeField] private bool usePreviewHour;
    [SerializeField, Range(0f, 24f)] private float previewHour = 12f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 7f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 20f;
    [SerializeField, Range(0.1f, 6f)] private float transitionHours = 1f;

    [Header("Pet Circadian")]
    [SerializeField, Range(0f, 24f)] private float petMorningStartHour = 7f;
    [SerializeField, Range(0f, 24f)] private float petDaySteadyStartHour = 10f;
    [SerializeField, Range(0f, 24f)] private float petEveningStartHour = 18.5f;
    [SerializeField, Range(0f, 24f)] private float petNightStartHour = 22f;
    [SerializeField, Range(0.1f, 6f)] private float petWakeTransitionHours = 0.75f;
    [SerializeField, Range(0.1f, 6f)] private float petEveningTransitionHours = 1.25f;
    [SerializeField] private float petInteractionWakeMinutes = 8f;

    [Header("Sun")]
    [SerializeField] private Light sun;
    [FormerlySerializedAs("sunEulerAngles")]
    [SerializeField] private Vector3 daySunEulerAngles = new Vector3(45f, -32f, 0f);
    [SerializeField] private Vector3 nightSunEulerAngles = new Vector3(-18f, -32f, 0f);
    [FormerlySerializedAs("sunIntensity")]
    [SerializeField] private float daySunIntensity = 1.25f;
    [SerializeField] private float nightSunIntensity = 0.08f;
    [FormerlySerializedAs("sunColor")]
    [SerializeField] private Color daySunColor = new Color(1f, 0.94f, 0.84f);
    [SerializeField] private Color nightSunColor = new Color(0.53f, 0.63f, 0.90f);
    [FormerlySerializedAs("sunColorTemperature")]
    [SerializeField] private float daySunColorTemperature = 6100f;
    [SerializeField] private float nightSunColorTemperature = 9000f;
    [FormerlySerializedAs("sunShadowStrength")]
    [SerializeField, Range(0f, 1f)] private float daySunShadowStrength = 0.78f;
    [SerializeField, Range(0f, 1f)] private float nightSunShadowStrength = 0.18f;
    [SerializeField] private float sunShadowBias = 0.025f;
    [SerializeField] private float sunShadowNormalBias = 0.18f;
    [SerializeField] private bool allowManualIndoorSunPlacement = true;

    [Header("Room Fill")]
    [SerializeField] private bool createFillLights = true;
    [SerializeField] private Light windowFillLight;
    [SerializeField] private Light sofaFillLight;
    [FormerlySerializedAs("windowFillIntensity")]
    [SerializeField] private float dayWindowFillIntensity = 0.18f;
    [SerializeField] private float nightWindowFillIntensity = 0.04f;
    [SerializeField] private Color dayWindowFillColor = new Color(1f, 0.80f, 0.55f);
    [SerializeField] private Color nightWindowFillColor = new Color(0.45f, 0.56f, 0.92f);
    [FormerlySerializedAs("sofaFillIntensity")]
    [SerializeField] private float daySofaFillIntensity = 0.12f;
    [SerializeField] private float nightSofaFillIntensity = 0.05f;
    [SerializeField] private Color daySofaFillColor = new Color(1f, 0.74f, 0.50f);
    [SerializeField] private Color nightSofaFillColor = new Color(0.67f, 0.65f, 0.80f);

    [Header("Practical Lights")]
    [SerializeField] private bool configurePracticalLights = true;
    [SerializeField] private float dayPracticalIntensityMultiplier = 1f;
    [SerializeField] private float nightPracticalIntensityMultiplier = 1.35f;
    [SerializeField] private Color dayPracticalColor = Color.white;
    [SerializeField] private Color nightPracticalColor = new Color(1f, 0.82f, 0.62f);
    [SerializeField]
    private string[] practicalLightNameKeywords =
    {
        "lamp",
        "area light",
        "ceiling"
    };

    [Header("Ambient")]
    [FormerlySerializedAs("ambientIntensity")]
    [SerializeField, Range(0f, 2f)] private float dayAmbientIntensity = 0.55f;
    [SerializeField, Range(0f, 2f)] private float nightAmbientIntensity = 0.28f;
    [FormerlySerializedAs("reflectionIntensity")]
    [SerializeField, Range(0f, 2f)] private float dayReflectionIntensity = 0.35f;
    [SerializeField, Range(0f, 2f)] private float nightReflectionIntensity = 0.18f;
    [FormerlySerializedAs("ambientSky")]
    [SerializeField] private Color dayAmbientSky = new Color(0.70f, 0.72f, 0.72f);
    [SerializeField] private Color nightAmbientSky = new Color(0.16f, 0.20f, 0.31f);
    [FormerlySerializedAs("ambientEquator")]
    [SerializeField] private Color dayAmbientEquator = new Color(0.45f, 0.43f, 0.39f);
    [SerializeField] private Color nightAmbientEquator = new Color(0.12f, 0.13f, 0.18f);
    [FormerlySerializedAs("ambientGround")]
    [SerializeField] private Color dayAmbientGround = new Color(0.14f, 0.13f, 0.12f);
    [SerializeField] private Color nightAmbientGround = new Color(0.05f, 0.05f, 0.07f);

    [Header("Skybox")]
    [FormerlySerializedAs("skyboxTint")]
    [SerializeField] private Color daySkyboxTint = new Color(0.42f, 0.70f, 1f);
    [SerializeField] private Color nightSkyboxTint = new Color(0.05f, 0.08f, 0.16f);
    [FormerlySerializedAs("skyboxGroundColor")]
    [SerializeField] private Color daySkyboxGroundColor = new Color(0.32f, 0.47f, 0.60f);
    [SerializeField] private Color nightSkyboxGroundColor = new Color(0.02f, 0.03f, 0.07f);
    [FormerlySerializedAs("skyboxExposure")]
    [SerializeField, Range(0f, 8f)] private float daySkyboxExposure = 1.08f;
    [SerializeField, Range(0f, 8f)] private float nightSkyboxExposure = 0.35f;
    [FormerlySerializedAs("skyboxAtmosphereThickness")]
    [SerializeField, Range(0f, 5f)] private float daySkyboxAtmosphereThickness = 0.85f;
    [SerializeField, Range(0f, 5f)] private float nightSkyboxAtmosphereThickness = 0.45f;

    [Header("Look")]
    [SerializeField] private float exposure = -0.08f;
    [SerializeField, Range(-100f, 100f)] private float contrast = 2f;
    [SerializeField, Range(-100f, 100f)] private float saturation = -10f;
    [SerializeField, Range(-100f, 100f)] private float whiteBalanceTemperature = 0f;
    [SerializeField, Range(0f, 1f)] private float bloomIntensity = 0.025f;
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.025f;

    [Header("Shadows")]
    [SerializeField] private bool enablePetShadows = true;
    [SerializeField] private bool enableRoomReceiveShadows = true;
    [SerializeField] private bool makeOpaqueRoomObjectsCastShadows = true;
    [SerializeField] private bool addPetContactShadowFallback = true;
    [SerializeField, Range(0f, 1f)] private float contactShadowOpacity = 0.42f;
    [SerializeField] private Vector2 contactShadowScale = new Vector2(1.35f, 0.82f);
    [SerializeField] private float contactShadowYOffset = 0.035f;
    [SerializeField] private float playModeShadowDistance = 45f;
    [SerializeField]
    private string[] petNameKeywords =
    {
        "dog",
        "cat",
        "puppy",
        "labrador",
        "retriever",
        "shiba",
        "corgi",
        "husky",
        "beagle",
        "boxer",
        "bulldog",
        "doberman",
        "dalmatian",
        "shepherd",
        "terrier",
        "rottweiler",
        "pitbull",
        "pug",
        "spitz",
        "collie"
    };

    private Volume lookVolume;
    private Material runtimeSkyboxMaterial;
    private WindowBackdropImagePlane windowBackdropImagePlane;
    private PawPalTimeOfDayState currentTimeOfDayState;
    private int lastAppliedMinuteStamp = int.MinValue;
    private static bool sceneBootstrapInstalled;
    private readonly Dictionary<int, PracticalLightState> practicalLightStates = new Dictionary<int, PracticalLightState>();

    private struct PracticalLightState
    {
        public Light Light;
        public float BaseIntensity;
        public Color BaseColor;
        public bool HasColorTemperature;
        public float BaseColorTemperature;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneBootstrap()
    {
        if (sceneBootstrapInstalled)
        {
            return;
        }

        sceneBootstrapInstalled = true;
        SceneManager.sceneLoaded += HandleSceneLoadedForBootstrap;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapInPlayMode()
    {
        EnsureEnhancerForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoadedForBootstrap(Scene scene, LoadSceneMode mode)
    {
        EnsureEnhancerForScene(scene);
    }

    private static void EnsureEnhancerForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != AutoBootstrapSceneName)
        {
            return;
        }

#if UNITY_2022_2_OR_NEWER
        LivingRoomGraphicsEnhancer[] enhancers = UnityEngine.Object.FindObjectsByType<LivingRoomGraphicsEnhancer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LivingRoomGraphicsEnhancer[] enhancers = UnityEngine.Object.FindObjectsOfType<LivingRoomGraphicsEnhancer>(true);
#endif
        for (int i = 0; i < enhancers.Length; i++)
        {
            if (enhancers[i] != null && enhancers[i].gameObject.scene == scene)
            {
                enhancers[i].ApplyGraphics();
                return;
            }
        }

        GameObject enhancerObject = new GameObject(RuntimeEnhancerObjectName);
        LivingRoomGraphicsEnhancer enhancer = enhancerObject.AddComponent<LivingRoomGraphicsEnhancer>();
        enhancer.ApplyGraphics();
    }

    private void OnEnable()
    {
        if (!applyOnEnable)
        {
            return;
        }

        ApplyGraphics();
    }

    private void Start()
    {
        if (applyOnEnable)
        {
            ApplyGraphics();
        }
    }

    private void Update()
    {
        if (!applyOnEnable ||
            !gameObject.scene.IsValid() ||
            !gameObject.scene.isLoaded)
        {
            return;
        }

        PawPalTimeOfDayState nextState = EvaluateTimeOfDayState();
        int nextMinuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(nextState);
        if (nextMinuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        ApplyGraphics(nextState);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        daySunIntensity = Mathf.Max(0f, daySunIntensity);
        nightSunIntensity = Mathf.Max(0f, nightSunIntensity);
        sunShadowBias = Mathf.Max(0f, sunShadowBias);
        sunShadowNormalBias = Mathf.Max(0f, sunShadowNormalBias);
        dayWindowFillIntensity = Mathf.Max(0f, dayWindowFillIntensity);
        nightWindowFillIntensity = Mathf.Max(0f, nightWindowFillIntensity);
        daySofaFillIntensity = Mathf.Max(0f, daySofaFillIntensity);
        nightSofaFillIntensity = Mathf.Max(0f, nightSofaFillIntensity);
        daySkyboxExposure = Mathf.Max(0f, daySkyboxExposure);
        nightSkyboxExposure = Mathf.Max(0f, nightSkyboxExposure);
        daySkyboxAtmosphereThickness = Mathf.Max(0f, daySkyboxAtmosphereThickness);
        nightSkyboxAtmosphereThickness = Mathf.Max(0f, nightSkyboxAtmosphereThickness);
        transitionHours = Mathf.Clamp(transitionHours, 0.1f, 6f);
        petDaySteadyStartHour = Mathf.Clamp(petDaySteadyStartHour, petMorningStartHour, 24f);
        petEveningStartHour = Mathf.Clamp(petEveningStartHour, petDaySteadyStartHour, 24f);
        petNightStartHour = Mathf.Clamp(petNightStartHour, petEveningStartHour, 24f);
        petWakeTransitionHours = Mathf.Clamp(petWakeTransitionHours, 0.1f, 6f);
        petEveningTransitionHours = Mathf.Clamp(petEveningTransitionHours, 0.1f, 6f);
        petInteractionWakeMinutes = Mathf.Max(1f, petInteractionWakeMinutes);
        playModeShadowDistance = Mathf.Max(1f, playModeShadowDistance);
        dayPracticalIntensityMultiplier = Mathf.Max(0f, dayPracticalIntensityMultiplier);
        nightPracticalIntensityMultiplier = Mathf.Max(0f, nightPracticalIntensityMultiplier);

        if (!Application.isPlaying && isActiveAndEnabled && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            EditorApplication.delayCall += ApplyGraphicsInEditor;
        }
    }

    private void ApplyGraphicsInEditor()
    {
        if (this == null || Application.isPlaying || !isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return;
        }

        ApplyGraphics();
        SceneView.RepaintAll();
    }
#endif

    [ContextMenu("Apply Graphics Now")]
    public void ApplyGraphics()
    {
        ApplyGraphics(EvaluateTimeOfDayState());
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

    private void ApplyGraphics(PawPalTimeOfDayState timeOfDayState)
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return;
        }

        currentTimeOfDayState = timeOfDayState;
        lastAppliedMinuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(timeOfDayState);

        if (Application.isPlaying && configureQualityInPlayMode)
        {
            PawPalGraphicsSettings.ApplyRuntimeProfile();
        }

        Bounds roomBounds = CalculateSceneBounds();

        if (configureRenderSettings)
        {
            ConfigureRenderSettings();
        }

        ConfigureSun();

        if (createFillLights)
        {
            ConfigureFillLights(roomBounds);
        }

        if (configurePracticalLights)
        {
            ConfigurePracticalLights();
        }

        if (configureMainCamera)
        {
            ConfigureCameras();
        }

        if (configurePostProcessing)
        {
            ConfigureLookVolume();
        }

        if (configureReflectionProbe)
        {
            ConfigureRoomReflectionProbe(roomBounds);
        }

        if (configureWindowBackdrop)
        {
            EnsureWindowBackdrop();
        }

        ConfigurePetCircadianBehavior();

        if (enablePetShadows || enableRoomReceiveShadows)
        {
            ConfigureRendererShadows();
        }

    }

    private void ConfigureRenderSettings()
    {
        float daylight01 = currentTimeOfDayState.Daylight01;
        bool isIndoorCaptureScene = IsIndoorCaptureScene();
        float ambientBoost = PawPalGraphicsSettings.IsUltraQuality && isIndoorCaptureScene ? 1.35f : 1f;
        float reflectionBoost = PawPalGraphicsSettings.IsUltraQuality && isIndoorCaptureScene ? 1.15f : 1f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight01);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, daylight01);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, daylight01);
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity * ambientBoost, daylight01);
        RenderSettings.reflectionIntensity = Mathf.Lerp(nightReflectionIntensity, dayReflectionIntensity * reflectionBoost, daylight01);
        RenderSettings.reflectionBounces = 1;

        if (configureSkybox)
        {
            ConfigureSkybox();
        }
    }

    private void ConfigureSkybox()
    {
        Material skybox = ResolveRuntimeSkyboxMaterial();
        if (skybox == null)
        {
            return;
        }

        float daylight01 = currentTimeOfDayState.Daylight01;
        Color skyTint = Color.Lerp(nightSkyboxTint, daySkyboxTint, daylight01);
        Color skyGroundColor = Color.Lerp(nightSkyboxGroundColor, daySkyboxGroundColor, daylight01);
        float skyExposure = Mathf.Lerp(nightSkyboxExposure, daySkyboxExposure, daylight01);
        float skyAtmosphereThickness = Mathf.Lerp(nightSkyboxAtmosphereThickness, daySkyboxAtmosphereThickness, daylight01);

        SetMaterialColor(skybox, "_SkyTint", skyTint);
        SetMaterialColor(skybox, "_Tint", skyTint);
        SetMaterialColor(skybox, "_Color", skyTint);
        SetMaterialColor(skybox, "_GroundColor", skyGroundColor);
        SetMaterialFloat(skybox, "_Exposure", skyExposure);
        SetMaterialFloat(skybox, "_AtmosphereThickness", skyAtmosphereThickness);

        RenderSettings.skybox = skybox;
        DynamicGI.UpdateEnvironment();
    }

    private Material ResolveRuntimeSkyboxMaterial()
    {
        if (runtimeSkyboxMaterial != null && runtimeSkyboxMaterial.shader != null)
        {
            return runtimeSkyboxMaterial;
        }

        Material sourceSkybox = RenderSettings.skybox;
        if (sourceSkybox != null && sourceSkybox.shader != null)
        {
            runtimeSkyboxMaterial = new Material(sourceSkybox);
        }
        else
        {
            Shader proceduralSkybox = Shader.Find("Skybox/Procedural");
            if (proceduralSkybox == null)
            {
                return null;
            }

            runtimeSkyboxMaterial = new Material(proceduralSkybox);
        }

        runtimeSkyboxMaterial.name = "PawFriends Blue Skybox";
        runtimeSkyboxMaterial.hideFlags = HideFlags.DontSave;
        return runtimeSkyboxMaterial;
    }

    private void SetMaterialColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void ConfigureSun()
    {
        Light resolvedSun = ResolveSun();
        if (resolvedSun == null)
        {
            return;
        }

        float daylight01 = currentTimeOfDayState.Daylight01;
        bool isIndoorCaptureScene = IsIndoorCaptureScene();
        float sunBoost = PawPalGraphicsSettings.IsUltraQuality && isIndoorCaptureScene ? 1.15f : 1f;
        sun = resolvedSun;
        RenderSettings.sun = resolvedSun;

        resolvedSun.type = LightType.Directional;
        if (!(isIndoorCaptureScene && allowManualIndoorSunPlacement))
        {
            resolvedSun.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(nightSunEulerAngles),
                Quaternion.Euler(daySunEulerAngles),
                daylight01);
        }
        resolvedSun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity * sunBoost, daylight01);
        resolvedSun.color = Color.Lerp(nightSunColor, daySunColor, daylight01);
        resolvedSun.useColorTemperature = true;
        resolvedSun.colorTemperature = Mathf.Lerp(nightSunColorTemperature, daySunColorTemperature, daylight01);
        resolvedSun.shadows = LightShadows.Soft;
        resolvedSun.shadowStrength = Mathf.Lerp(nightSunShadowStrength, daySunShadowStrength, daylight01);
        resolvedSun.shadowBias = sunShadowBias;
        resolvedSun.shadowNormalBias = sunShadowNormalBias;
        resolvedSun.shadowNearPlane = 0.1f;
        resolvedSun.renderMode = LightRenderMode.ForcePixel;

        UniversalAdditionalLightData lightData = resolvedSun.GetUniversalAdditionalLightData();
        lightData.usePipelineSettings = false;
        lightData.softShadowQuality = PawPalGraphicsSettings.GetSoftShadowQuality();
    }

    private Light ResolveSun()
    {
        if (IsSceneObject(sun) && sun.type == LightType.Directional)
        {
            return sun;
        }

        if (IsSceneObject(RenderSettings.sun) && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light fallback = null;
        Light[] lights = FindSceneObjects<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (!IsSceneObject(candidate) || candidate.type != LightType.Directional)
            {
                continue;
            }

            if (fallback == null || candidate.name.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fallback = candidate;
            }
        }

        if (fallback != null)
        {
            return fallback;
        }

        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(transform, false);
        return sunObject.AddComponent<Light>();
    }

    private void ConfigureFillLights(Bounds roomBounds)
    {
        float daylight01 = currentTimeOfDayState.Daylight01;
        bool isIndoorCaptureScene = IsIndoorCaptureScene();
        float windowBoost = PawPalGraphicsSettings.IsUltraQuality && isIndoorCaptureScene ? 2.1f : 1f;
        float sofaBoost = PawPalGraphicsSettings.IsUltraQuality && isIndoorCaptureScene ? 1.15f : 1f;
        Light windowLight = ResolveOrCreateLight(ref windowFillLight, WindowFillLightName, LightType.Spot);
        Vector3 windowPosition = new Vector3(
            roomBounds.max.x - Mathf.Max(0.35f, roomBounds.size.x * 0.08f),
            Mathf.Max(roomBounds.center.y + 1.7f, roomBounds.max.y - 0.35f),
            Mathf.Lerp(roomBounds.min.z, roomBounds.max.z, 0.55f));

        windowLight.transform.position = windowPosition;
        AimAt(windowLight.transform, roomBounds.center + new Vector3(-roomBounds.extents.x * 0.25f, 0.25f, -roomBounds.extents.z * 0.15f));
        windowLight.type = LightType.Spot;
        windowLight.intensity = Mathf.Lerp(nightWindowFillIntensity, dayWindowFillIntensity * windowBoost, daylight01);
        windowLight.range = Mathf.Max(4f, roomBounds.extents.magnitude * 0.7f);
        windowLight.spotAngle = 82f;
        windowLight.innerSpotAngle = 48f;
        windowLight.color = Color.Lerp(nightWindowFillColor, dayWindowFillColor, daylight01);
        windowLight.shadows = LightShadows.None;
        windowLight.renderMode = LightRenderMode.Auto;

        Light sofaLight = ResolveOrCreateLight(ref sofaFillLight, SofaFillLightName, LightType.Point);
        sofaLight.transform.position = roomBounds.center + new Vector3(roomBounds.extents.x * 0.22f, Mathf.Max(1.25f, roomBounds.extents.y * 0.55f), roomBounds.extents.z * 0.2f);
        sofaLight.type = LightType.Point;
        sofaLight.intensity = Mathf.Lerp(nightSofaFillIntensity, daySofaFillIntensity * sofaBoost, daylight01);
        sofaLight.range = Mathf.Max(2.5f, roomBounds.size.x * 0.35f);
        sofaLight.color = Color.Lerp(nightSofaFillColor, daySofaFillColor, daylight01);
        sofaLight.shadows = LightShadows.None;
        sofaLight.renderMode = LightRenderMode.Auto;
    }

    private void ConfigurePracticalLights()
    {
        RefreshPracticalLightStates();

        float daylight01 = currentTimeOfDayState.Daylight01;
        float intensityMultiplier = Mathf.Lerp(nightPracticalIntensityMultiplier, dayPracticalIntensityMultiplier, daylight01);
        Color colorTint = Color.Lerp(nightPracticalColor, dayPracticalColor, daylight01);

        foreach (PracticalLightState state in practicalLightStates.Values)
        {
            if (!IsSceneObject(state.Light))
            {
                continue;
            }

            state.Light.intensity = state.BaseIntensity * intensityMultiplier;
            state.Light.color = state.BaseColor * colorTint;
            if (string.Equals(state.Light.name, "Ceiling Lamp", StringComparison.OrdinalIgnoreCase))
            {
                state.Light.shadows = LightShadows.None;
            }

            if (state.HasColorTemperature)
            {
                state.Light.useColorTemperature = true;
                state.Light.colorTemperature = Mathf.Lerp(2600f, state.BaseColorTemperature, daylight01);
            }
        }
    }

    private bool IsIndoorCaptureScene()
    {
        return gameObject.scene.IsValid() && string.Equals(gameObject.scene.name, AutoBootstrapSceneName, StringComparison.Ordinal);
    }

    private void ConfigureCameras()
    {
        float daylight01 = currentTimeOfDayState.Daylight01;
        Color backgroundColor = Color.Lerp(nightSkyboxTint, daySkyboxTint, daylight01);
        Camera[] cameras = FindSceneObjects<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (!IsSceneObject(camera) || !camera.enabled)
            {
                continue;
            }

            if (configureSkybox)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = backgroundColor;
            }

            camera.allowHDR = PawPalGraphicsSettings.EnableHdr;
            camera.allowMSAA = PawPalGraphicsSettings.EnableCameraMsaa;
            camera.useOcclusionCulling = true;

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = PawPalGraphicsSettings.EnablePostProcessing;
            cameraData.requiresDepthOption = PawPalGraphicsSettings.RequireDepthTexture ? CameraOverrideOption.On : CameraOverrideOption.Off;
            cameraData.requiresColorOption = PawPalGraphicsSettings.RequireOpaqueTexture ? CameraOverrideOption.On : CameraOverrideOption.Off;
            cameraData.antialiasing = PawPalGraphicsSettings.GetCameraAntialiasingMode();
            cameraData.antialiasingQuality = PawPalGraphicsSettings.GetCameraAntialiasingQuality();
            cameraData.stopNaN = true;
            cameraData.dithering = PawPalGraphicsSettings.EnableDithering;
        }
    }

    private void ConfigureLookVolume()
    {
        if (lookVolume == null)
        {
            lookVolume = GetComponent<Volume>();
            if (lookVolume == null)
            {
                lookVolume = gameObject.AddComponent<Volume>();
            }
        }

        lookVolume.isGlobal = true;
        lookVolume.priority = 60f;
        lookVolume.weight = PawPalGraphicsSettings.EnablePostProcessing ? 1f : 0f;

        VolumeProfile profile = lookVolume.profile;
        profile.name = "PawFriends Living Room Look";
        profile.hideFlags = HideFlags.DontSave;

        Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(PawPalGraphicsSettings.GetTonemappingMode());

        ColorAdjustments colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(profile);
        colorAdjustments.postExposure.Override(IsIndoorCaptureScene()
            ? PawPalGraphicsSettings.GetPostExposure(exposure)
            : PawPalGraphicsSettings.GetPostExposure(exposure));
        colorAdjustments.contrast.Override(IsIndoorCaptureScene()
            ? PawPalGraphicsSettings.GetIndoorContrast(contrast)
            : PawPalGraphicsSettings.GetContrast(contrast));
        colorAdjustments.saturation.Override(IsIndoorCaptureScene()
            ? PawPalGraphicsSettings.GetIndoorSaturation(saturation)
            : PawPalGraphicsSettings.GetSaturation(saturation));
        colorAdjustments.colorFilter.Override(Color.white);

        WhiteBalance whiteBalance = GetOrAddVolumeComponent<WhiteBalance>(profile);
        whiteBalance.temperature.Override(whiteBalanceTemperature);
        whiteBalance.tint.Override(0f);

        LiftGammaGain liftGammaGain = GetOrAddVolumeComponent<LiftGammaGain>(profile);
        liftGammaGain.lift.Override(new Vector4(1f, 1f, 1f, 0f));
        liftGammaGain.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
        liftGammaGain.gain.Override(new Vector4(1f, 1f, 1f, 0f));

        Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
        bloom.threshold.Override(1.4f);
        bloom.intensity.Override(PawPalGraphicsSettings.GetBloomIntensity(bloomIntensity));
        bloom.scatter.Override(0.35f);
        bloom.tint.Override(Color.white);
        bloom.highQualityFiltering.Override(PawPalGraphicsSettings.UseHighQualityBloomFiltering);
        bloom.maxIterations.Override(PawPalGraphicsSettings.GetBloomMaxIterations());
        bloom.active = PawPalGraphicsSettings.EnableBloom;

        Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(PawPalGraphicsSettings.GetVignetteIntensity(vignetteIntensity));
        vignette.smoothness.Override(0.42f);
        vignette.rounded.Override(false);

        DepthOfField depthOfField = GetOrAddVolumeComponent<DepthOfField>(profile);
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        bool isIndoorCaptureScene = IsIndoorCaptureScene();
        if (isIndoorCaptureScene)
        {
            depthOfField.gaussianStart.Override(4f);
            depthOfField.gaussianEnd.Override(8f);
            depthOfField.gaussianMaxRadius.Override(0.12f);
            depthOfField.highQualitySampling.Override(false);
            depthOfField.focalLength.Override(70f);
            depthOfField.aperture.Override(12f);
            depthOfField.focusDistance.Override(6f);
            depthOfField.active = PawPalGraphicsSettings.IsUltraQuality;
        }
        else
        {
            depthOfField.gaussianStart.Override(PawPalGraphicsSettings.GetDepthOfFieldFocusDistance());
            depthOfField.gaussianEnd.Override(PawPalGraphicsSettings.GetDepthOfFieldFocusDistance() + 3.5f);
            depthOfField.gaussianMaxRadius.Override(PawPalGraphicsSettings.IsUltraQuality ? 0.6f : 0.3f);
            depthOfField.highQualitySampling.Override(PawPalGraphicsSettings.IsUltraQuality);
            depthOfField.focalLength.Override(PawPalGraphicsSettings.GetDepthOfFieldFocalLength());
            depthOfField.aperture.Override(PawPalGraphicsSettings.GetDepthOfFieldAperture());
            depthOfField.focusDistance.Override(PawPalGraphicsSettings.GetDepthOfFieldFocusDistance());
            depthOfField.active = PawPalGraphicsSettings.EnableDepthOfField;
        }

        ChromaticAberration chromaticAberration = GetOrAddVolumeComponent<ChromaticAberration>(profile);
        chromaticAberration.intensity.Override(PawPalGraphicsSettings.GetChromaticAberrationIntensity());
        chromaticAberration.active = PawPalGraphicsSettings.EnablePostProcessing;

        FilmGrain filmGrain = GetOrAddVolumeComponent<FilmGrain>(profile);
        filmGrain.type.Override(FilmGrainLookup.Thin1);
        filmGrain.intensity.Override(PawPalGraphicsSettings.GetFilmGrainIntensity());
        filmGrain.response.Override(0.8f);
        filmGrain.active = PawPalGraphicsSettings.EnablePostProcessing;

        ScreenSpaceLensFlare screenSpaceLensFlare = GetOrAddVolumeComponent<ScreenSpaceLensFlare>(profile);
        screenSpaceLensFlare.intensity.Override(PawPalGraphicsSettings.IsUltraQuality ? 0.55f : 0f);
        screenSpaceLensFlare.tintColor.Override(new Color(1f, 0.96f, 0.82f, 1f));
        screenSpaceLensFlare.bloomMip.Override(1);
        screenSpaceLensFlare.firstFlareIntensity.Override(1f);
        screenSpaceLensFlare.secondaryFlareIntensity.Override(0.85f);
        screenSpaceLensFlare.warpedFlareIntensity.Override(0.6f);
        screenSpaceLensFlare.warpedFlareScale.Override(new Vector2(1f, 1f));
        screenSpaceLensFlare.samples.Override(2);
        screenSpaceLensFlare.sampleDimmer.Override(0.6f);
        screenSpaceLensFlare.vignetteEffect.Override(0.85f);
        screenSpaceLensFlare.startingPosition.Override(1.2f);
        screenSpaceLensFlare.scale.Override(1.35f);
        screenSpaceLensFlare.streaksIntensity.Override(0.45f);
        screenSpaceLensFlare.streaksLength.Override(0.85f);
        screenSpaceLensFlare.active = PawPalGraphicsSettings.IsUltraQuality;
    }

    private void ConfigureRoomReflectionProbe(Bounds roomBounds)
    {
        ReflectionProbe probe = ResolveOrCreateChildComponent<ReflectionProbe>(ReflectionProbeName);
        probe.transform.position = roomBounds.center + Vector3.up * 0.15f;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.resolution = PawPalGraphicsSettings.ReflectionProbeResolution;
        probe.intensity = Mathf.Lerp(nightReflectionIntensity, dayReflectionIntensity, currentTimeOfDayState.Daylight01);
        probe.boxProjection = PawPalGraphicsSettings.UseReflectionProbeBoxProjection;
        probe.center = Vector3.zero;
        probe.size = new Vector3(
            Mathf.Max(3f, roomBounds.size.x + 0.6f),
            Mathf.Max(2.5f, roomBounds.size.y + 1.0f),
            Mathf.Max(3f, roomBounds.size.z + 0.6f));
        probe.cullingMask = -1;

        if (Application.isPlaying)
        {
            probe.RenderProbe();
        }
    }

    private void EnsureWindowBackdrop()
    {
        if (windowBackdropImagePlane == null)
        {
            Transform child = transform.Find(WindowBackdropObjectName);
            if (child != null)
            {
                windowBackdropImagePlane = child.GetComponent<WindowBackdropImagePlane>();
            }

            if (windowBackdropImagePlane == null)
            {
                windowBackdropImagePlane = FindExistingWindowBackdropImagePlane();
            }

            if (windowBackdropImagePlane == null)
            {
                GameObject childObject = new GameObject(WindowBackdropObjectName);
                childObject.transform.SetParent(transform, false);
                windowBackdropImagePlane = childObject.AddComponent<WindowBackdropImagePlane>();
            }
        }

        windowBackdropImagePlane.ConfigureTimeSettings(
            useDeviceLocalTime,
            usePreviewHour,
            previewHour,
            sunriseHour,
            sunsetHour,
            transitionHours);

        if (ChildNeedsBackdropRebuild(windowBackdropImagePlane))
        {
            windowBackdropImagePlane.Rebuild();
        }
    }

    private bool ChildNeedsBackdropRebuild(WindowBackdropImagePlane backdrop)
    {
        if (backdrop == null)
        {
            return false;
        }

        Transform backdropTransform = backdrop.transform;
        return backdropTransform.Find("GeneratedWindowBackdropImage") == null;
    }

    private WindowBackdropImagePlane FindExistingWindowBackdropImagePlane()
    {
        WindowBackdropImagePlane[] backdrops = FindSceneObjects<WindowBackdropImagePlane>();
        for (int i = 0; i < backdrops.Length; i++)
        {
            WindowBackdropImagePlane candidate = backdrops[i];
            if (!IsSceneObject(candidate) || candidate == windowBackdropImagePlane)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private void ConfigurePetCircadianBehavior()
    {
        DogRoomAgent[] agents = FindSceneObjects<DogRoomAgent>();
        if (agents == null || agents.Length == 0)
        {
            return;
        }

        PawPalPetCircadianState petState = PawPalTimeOfDayEvaluator.EvaluatePetCircadian(
            currentTimeOfDayState.LocalHour,
            petMorningStartHour,
            petDaySteadyStartHour,
            petEveningStartHour,
            petNightStartHour,
            petWakeTransitionHours,
            petEveningTransitionHours);
        DogCircadianProfile profile = BuildPetCircadianProfile(petState);

        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (!IsSceneObject(agent))
            {
                continue;
            }

            agent.ApplyCircadianProfile(profile);
        }
    }

    private DogCircadianProfile BuildPetCircadianProfile(PawPalPetCircadianState petState)
    {
        float activity01 = Mathf.Clamp01(petState.Activity01);
        float sleepiness01 = Mathf.Clamp01(petState.Sleepiness01);
        DogCircadianProfile profile = DogCircadianProfile.Default;
        profile.ForceSleep = petState.ForceSleep;
        profile.RoamWaitMultiplier = Mathf.Lerp(1.7f, 0.72f, activity01);
        profile.AmbientIdleChanceMultiplier = Mathf.Lerp(1.08f, 0.88f, activity01);
        profile.BarkChanceMultiplier = petState.ForceSleep ? 0f : Mathf.Lerp(0.18f, 1.1f, activity01);
        profile.ChillChanceMultiplier = Mathf.Lerp(0.65f, 1.95f, sleepiness01);
        profile.SleepChanceMultiplier = petState.ForceSleep ? 0.25f : Mathf.Lerp(0.25f, 2.8f, sleepiness01);
        profile.ToyInterestMultiplier = petState.ForceSleep ? 0f : Mathf.Lerp(0.12f, 1.35f, activity01);
        profile.TrotChanceMultiplier = petState.ForceSleep ? 0f : Mathf.Lerp(0.45f, 1.45f, activity01);
        profile.MinimumForcedSleepDuration = 45f;
        profile.MaximumForcedSleepDuration = 90f;
        profile.InteractionWakeOverrideSeconds = Mathf.Max(60f, petInteractionWakeMinutes * 60f);
        return profile;
    }

    private void ConfigureRendererShadows()
    {
        Renderer[] renderers = FindSceneObjects<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneObject(renderer) || !renderer.enabled || IsEffectRenderer(renderer))
            {
                continue;
            }

            bool isPet = IsPetRenderer(renderer);
            if (isPet && enablePetShadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                EnsurePetContactShadow(renderer);
                continue;
            }

            if (!enableRoomReceiveShadows)
            {
                continue;
            }

            renderer.receiveShadows = true;
            if (makeOpaqueRoomObjectsCastShadows && !UsesTransparentMaterial(renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        if (enableRoomReceiveShadows)
        {
            ConfigureTerrainShadows();
        }
    }

    private void ConfigureTerrainShadows()
    {
        Terrain[] terrains = FindSceneObjects<Terrain>();
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (!IsSceneObject(terrain) || !terrain.enabled)
            {
                continue;
            }

            terrain.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private Bounds CalculateSceneBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, new Vector3(6f, 3f, 6f));
        bool hasRoomFootprint = TryResolveRoomFootprint(out Bounds roomFootprint);
        Renderer[] renderers = FindSceneObjects<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneObject(renderer) || !renderer.enabled || IsEffectRenderer(renderer))
            {
                continue;
            }

            if (hasRoomFootprint && !IsInsideRoomFootprint(renderer.bounds, roomFootprint))
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

        if (!hasBounds)
        {
            if (hasRoomFootprint)
            {
                return CreateFallbackRoomBounds(roomFootprint);
            }

            return bounds;
        }

        bounds.Expand(new Vector3(0.5f, 0.25f, 0.5f));
        bounds = EnsureMinimumRoomHeight(bounds);
        return bounds;
    }

    private bool TryResolveRoomFootprint(out Bounds footprint)
    {
        DogRoomAgent[] agents = FindSceneObjects<DogRoomAgent>();
        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (!IsSceneObject(agent) || !agent.TryGetRoomBounds(out footprint))
            {
                continue;
            }

            footprint.Expand(new Vector3(RoomFootprintPadding * 2f, 0f, RoomFootprintPadding * 2f));
            return true;
        }

        footprint = new Bounds();
        return false;
    }

    private static bool IsInsideRoomFootprint(Bounds bounds, Bounds footprint)
    {
        Vector3 center = bounds.center;
        if (center.x < footprint.min.x || center.x > footprint.max.x ||
            center.z < footprint.min.z || center.z > footprint.max.z)
        {
            return false;
        }

        return bounds.size.x <= footprint.size.x * RoomRendererMaxFootprintMultiplier &&
               bounds.size.z <= footprint.size.z * RoomRendererMaxFootprintMultiplier;
    }

    private static Bounds CreateFallbackRoomBounds(Bounds footprint)
    {
        Vector3 center = new Vector3(footprint.center.x, MinimumRoomBoundsCenterY, footprint.center.z);
        Vector3 size = new Vector3(
            Mathf.Max(6f, footprint.size.x),
            MinimumRoomBoundsHeight,
            Mathf.Max(6f, footprint.size.z));
        return new Bounds(center, size);
    }

    private static Bounds EnsureMinimumRoomHeight(Bounds bounds)
    {
        if (bounds.size.y >= MinimumRoomBoundsHeight)
        {
            return bounds;
        }

        Vector3 center = bounds.center;
        center.y = Mathf.Max(center.y, MinimumRoomBoundsCenterY);
        bounds.center = center;

        Vector3 size = bounds.size;
        size.y = MinimumRoomBoundsHeight;
        bounds.size = size;
        return bounds;
    }

    private Light ResolveOrCreateLight(ref Light cachedLight, string lightName, LightType lightType)
    {
        if (IsSceneObject(cachedLight))
        {
            return cachedLight;
        }

        Light foundLight = ResolveChildComponent<Light>(lightName);
        if (foundLight == null)
        {
            Light[] lights = FindSceneObjects<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsSceneObject(lights[i]) && string.Equals(lights[i].name, lightName, StringComparison.OrdinalIgnoreCase))
                {
                    foundLight = lights[i];
                    break;
                }
            }
        }

        if (foundLight == null)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            foundLight = lightObject.AddComponent<Light>();
        }

        foundLight.type = lightType;
        cachedLight = foundLight;
        return foundLight;
    }

    private void RefreshPracticalLightStates()
    {
        List<int> staleLightIds = null;
        foreach (KeyValuePair<int, PracticalLightState> pair in practicalLightStates)
        {
            if (IsSceneObject(pair.Value.Light))
            {
                continue;
            }

            if (staleLightIds == null)
            {
                staleLightIds = new List<int>();
            }

            staleLightIds.Add(pair.Key);
        }

        if (staleLightIds != null)
        {
            for (int i = 0; i < staleLightIds.Count; i++)
            {
                practicalLightStates.Remove(staleLightIds[i]);
            }
        }

        Light[] lights = FindSceneObjects<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (!IsPracticalLight(candidate))
            {
                continue;
            }

            int lightId = candidate.GetInstanceID();
            if (practicalLightStates.ContainsKey(lightId))
            {
                continue;
            }

            practicalLightStates.Add(lightId, new PracticalLightState
            {
                Light = candidate,
                BaseIntensity = candidate.intensity,
                BaseColor = candidate.color,
                HasColorTemperature = candidate.useColorTemperature,
                BaseColorTemperature = candidate.colorTemperature
            });
        }
    }

    private T ResolveOrCreateChildComponent<T>(string childName) where T : Component
    {
        T component = ResolveChildComponent<T>(childName);
        if (component != null)
        {
            return component;
        }

        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        return child.AddComponent<T>();
    }

    private T ResolveChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }

    private T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
        }

        component.active = true;
        return component;
    }

    private bool IsPetRenderer(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            if (NameMatchesAnyPetKeyword(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        Animator animator = renderer.GetComponentInParent<Animator>();
        return animator != null && NameMatchesAnyPetKeyword(animator.gameObject.name);
    }

    private void EnsurePetContactShadow(Renderer renderer)
    {
        if (!addPetContactShadowFallback)
        {
            return;
        }

        Transform petRoot = ResolvePetRoot(renderer);
        if (petRoot == null)
        {
            return;
        }

        DogContactShadow contactShadow = petRoot.GetComponent<DogContactShadow>();
        if (contactShadow == null)
        {
            contactShadow = petRoot.gameObject.AddComponent<DogContactShadow>();
        }

        contactShadow.Configure(contactShadowOpacity, contactShadowScale, contactShadowYOffset);
    }

    private Transform ResolvePetRoot(Renderer renderer)
    {
        Animator animator = renderer.GetComponentInParent<Animator>();
        if (animator != null && NameMatchesAnyPetKeyword(animator.gameObject.name))
        {
            return animator.transform;
        }

        Transform current = renderer.transform;
        Transform bestMatch = null;
        while (current != null)
        {
            if (NameMatchesAnyPetKeyword(current.name))
            {
                bestMatch = current;
            }

            current = current.parent;
        }

        if (bestMatch != null)
        {
            return bestMatch;
        }

        return animator != null ? animator.transform : renderer.transform.root;
    }

    private bool NameMatchesAnyPetKeyword(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || petNameKeywords == null)
        {
            return false;
        }

        for (int i = 0; i < petNameKeywords.Length; i++)
        {
            string keyword = petNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool UsesTransparentMaterial(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            if (material.renderQueue >= 3000)
            {
                return true;
            }

            string materialName = material.name;
            if (materialName.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("alpha", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Shader shader = material.shader;
            if (shader != null && shader.name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEffectRenderer(Renderer renderer)
    {
        return renderer is ParticleSystemRenderer ||
               renderer is LineRenderer ||
               renderer is TrailRenderer;
    }

    private bool IsSceneObject(Component component)
    {
        return component != null && component.gameObject.scene == gameObject.scene;
    }

    private bool IsPracticalLight(Light light)
    {
        if (!IsSceneObject(light) ||
            light == sun ||
            light == windowFillLight ||
            light == sofaFillLight ||
            light.type == LightType.Directional)
        {
            return false;
        }

        if (string.Equals(light.name, "Ceiling Lamp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (practicalLightNameKeywords == null || practicalLightNameKeywords.Length == 0)
        {
            return false;
        }

        string lightName = light.name;
        for (int i = 0; i < practicalLightNameKeywords.Length; i++)
        {
            string keyword = practicalLightNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) &&
                lightName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private T[] FindSceneObjects<T>() where T : Component
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    private void AimAt(Transform source, Vector3 target)
    {
        Vector3 direction = target - source.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        source.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
