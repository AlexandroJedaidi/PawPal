using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PawPalWalkTimeOfDayController : MonoBehaviour
{
    private const string ControllerObjectName = "PawPalWalkTimeOfDayController";
    private const string DayVolumeResourcePath = "PawPal/WalkDayVolume";
    private const string NightVolumeResourcePath = "PawPal/WalkNightVolume";
    private const string RuntimeLampLightName = "PawPalWalkLampLightRuntime";

    [Header("Time")]
    [SerializeField] private bool useDeviceLocalTime = true;
    [SerializeField] private bool usePreviewHour;
    [SerializeField, Range(0f, 24f)] private float previewHour = 12f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 7f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 20f;
    [SerializeField, Range(0.1f, 6f)] private float transitionHours = 1f;

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Vector3 dayLightEulerAngles = new Vector3(53f, -30f, 0f);
    [SerializeField] private Vector3 nightLightEulerAngles = new Vector3(15f, -22f, 0f);
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.86f, 1f);
    [SerializeField] private Color nightLightColor = new Color(0.62f, 0.70f, 0.92f, 1f);
    [SerializeField] private float dayLightIntensity = 1.35f;
    [SerializeField] private float nightLightIntensity = 0.82f;
    [SerializeField, Range(0f, 1f)] private float dayShadowStrength = 0.88f;
    [SerializeField, Range(0f, 1f)] private float nightShadowStrength = 0.72f;

    [Header("Ambient")]
    [SerializeField] private Color dayAmbientSky = new Color32(186, 208, 235, 255);
    [SerializeField] private Color nightAmbientSky = new Color32(86, 104, 140, 255);
    [SerializeField] private Color dayAmbientEquator = new Color32(148, 156, 156, 255);
    [SerializeField] private Color nightAmbientEquator = new Color32(58, 66, 86, 255);
    [SerializeField] private Color dayAmbientGround = new Color32(82, 83, 85, 255);
    [SerializeField] private Color nightAmbientGround = new Color32(30, 34, 44, 255);
    [SerializeField, Range(0f, 2f)] private float dayAmbientIntensity = 1.1f;
    [SerializeField, Range(0f, 2f)] private float nightAmbientIntensity = 0.72f;
    [SerializeField, Range(0f, 2f)] private float dayReflectionIntensity = 1f;
    [SerializeField, Range(0f, 2f)] private float nightReflectionIntensity = 0.48f;

    [Header("Volume")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private string dayVolumeResourcePath = DayVolumeResourcePath;
    [SerializeField] private string nightVolumeResourcePath = NightVolumeResourcePath;
    [SerializeField, Range(0f, 1f)] private float dayVolumeThreshold = 0.5f;

    [Header("Lamp A Practical Lights")]
    [SerializeField] private string lampRootNamePrefix = "Lamp_A";
    [SerializeField, Range(0f, 1f)] private float lampActivationDaylightThreshold = 0.35f;
    [SerializeField] private Color lampLightColor = new Color(1f, 0.91f, 0.72f, 1f);
    [SerializeField, Min(0f)] private float lampNightIntensity = 30f;
    [SerializeField, Min(0.1f)] private float lampRange = 9f;
    [SerializeField, Range(1f, 179f)] private float lampSpotAngle = 68f;
    [SerializeField] private Vector3 lampLocalOffset = new Vector3(0f, 5.804f, -1.8186f);
    [SerializeField] private Vector3 lampLocalEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField] private LightShadows lampShadowMode = LightShadows.None;

    private int lastAppliedMinuteStamp = int.MinValue;
    private VolumeProfile dayProfile;
    private VolumeProfile nightProfile;
    private readonly List<ManagedLampLight> managedLampLights = new List<ManagedLampLight>();

    public static bool IsSupportedScene(Scene scene)
    {
        return scene.IsValid()
            && string.Equals(scene.name, PawPalWalkSceneFlow.WalkSceneName, StringComparison.Ordinal);
    }

    public void ApplyVisualsNow()
    {
        ApplyTimeOfDay(true);
    }

    private void OnEnable()
    {
        ApplyTimeOfDay(true);
    }

    private void Update()
    {
        ApplyTimeOfDay(false);
    }

    private void OnDisable()
    {
        ClearManagedLampLights();
    }

    private void ApplyTimeOfDay(bool force)
    {
        if (!IsSupportedScene(gameObject.scene))
        {
            return;
        }

        PawPalTimeOfDayState state = PawPalTimeOfDayEvaluator.Evaluate(
            useDeviceLocalTime,
            usePreviewHour,
            previewHour,
            sunriseHour,
            sunsetHour,
            transitionHours);
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        if (!force && minuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        float daylight01 = Mathf.Clamp01(state.Daylight01);

        ApplyRenderSettings(daylight01);
        ApplyDirectionalLight(daylight01);
        ApplyVolumeProfile(daylight01);
        ApplyLampLights(daylight01);
    }

    private void ApplyRenderSettings(float daylight01)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight01);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, daylight01);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, daylight01);
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, daylight01);
        RenderSettings.reflectionIntensity = Mathf.Lerp(nightReflectionIntensity, dayReflectionIntensity, daylight01);
    }

    private void ApplyDirectionalLight(float daylight01)
    {
        Light resolvedLight = ResolveDirectionalLight();
        if (resolvedLight == null)
        {
            return;
        }

        directionalLight = resolvedLight;
        RenderSettings.sun = resolvedLight;
        resolvedLight.type = LightType.Directional;
        resolvedLight.transform.rotation = Quaternion.Slerp(
            Quaternion.Euler(nightLightEulerAngles),
            Quaternion.Euler(dayLightEulerAngles),
            daylight01);
        resolvedLight.color = Color.Lerp(nightLightColor, dayLightColor, daylight01);
        resolvedLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, daylight01);
        resolvedLight.shadows = LightShadows.Soft;
        resolvedLight.shadowStrength = Mathf.Lerp(nightShadowStrength, dayShadowStrength, daylight01);
    }

    private void ApplyVolumeProfile(float daylight01)
    {
        Volume resolvedVolume = ResolveGlobalVolume();
        if (resolvedVolume == null)
        {
            return;
        }

        LoadProfilesIfNeeded();
        VolumeProfile targetProfile = daylight01 >= dayVolumeThreshold ? dayProfile : nightProfile;
        if (targetProfile == null)
        {
            return;
        }

        resolvedVolume.isGlobal = true;
        resolvedVolume.weight = 1f;
        if (resolvedVolume.sharedProfile != targetProfile)
        {
            resolvedVolume.sharedProfile = targetProfile;
        }
    }

    private void LoadProfilesIfNeeded()
    {
        if (dayProfile == null && !string.IsNullOrWhiteSpace(dayVolumeResourcePath))
        {
            dayProfile = Resources.Load<VolumeProfile>(dayVolumeResourcePath);
        }

        if (nightProfile == null && !string.IsNullOrWhiteSpace(nightVolumeResourcePath))
        {
            nightProfile = Resources.Load<VolumeProfile>(nightVolumeResourcePath);
        }
    }

    private void ApplyLampLights(float daylight01)
    {
        EnsureLampLights();
        bool shouldEnableLampLights = daylight01 <= lampActivationDaylightThreshold;
        for (int i = managedLampLights.Count - 1; i >= 0; i--)
        {
            ManagedLampLight entry = managedLampLights[i];
            if (entry.Root == null || entry.Light == null)
            {
                managedLampLights.RemoveAt(i);
                continue;
            }

            if (!entry.Root.gameObject.activeInHierarchy)
            {
                entry.Light.enabled = false;
                continue;
            }

            entry.Light.enabled = shouldEnableLampLights;
            entry.Light.color = lampLightColor;
            entry.Light.intensity = shouldEnableLampLights ? lampNightIntensity : 0f;
            entry.Light.range = lampRange;
            entry.Light.spotAngle = lampSpotAngle;
            entry.Light.shadows = lampShadowMode;
            entry.Light.transform.localPosition = lampLocalOffset;
            entry.Light.transform.localRotation = Quaternion.Euler(lampLocalEulerAngles);
        }
    }

    private void EnsureLampLights()
    {
        if (managedLampLights.Count > 0)
        {
            bool hasMissingReference = false;
            for (int i = 0; i < managedLampLights.Count; i++)
            {
                if (managedLampLights[i].Root == null || managedLampLights[i].Light == null)
                {
                    hasMissingReference = true;
                    break;
                }
            }

            if (!hasMissingReference)
            {
                return;
            }
        }

        managedLampLights.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (!IsMatchingLampRoot(current))
            {
                continue;
            }

            Light runtimeLight = ResolveOrCreateLampLight(current);
            if (runtimeLight == null)
            {
                continue;
            }

            managedLampLights.Add(new ManagedLampLight(current, runtimeLight));
        }
    }

    private Light ResolveOrCreateLampLight(Transform lampRoot)
    {
        if (lampRoot == null)
        {
            return null;
        }

        Transform existingChild = lampRoot.Find(RuntimeLampLightName);
        GameObject lightObject = existingChild != null ? existingChild.gameObject : new GameObject(RuntimeLampLightName);
        if (existingChild == null)
        {
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(lampRoot, false);
        }

        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }

        light.type = LightType.Spot;
        light.renderMode = LightRenderMode.ForcePixel;
        light.range = lampRange;
        light.spotAngle = lampSpotAngle;
        light.color = lampLightColor;
        light.intensity = lampNightIntensity;
        light.shadows = lampShadowMode;
        light.transform.localPosition = lampLocalOffset;
        light.transform.localRotation = Quaternion.Euler(lampLocalEulerAngles);
        return light;
    }

    private bool IsMatchingLampRoot(Transform candidate)
    {
        return candidate != null
            && candidate.gameObject.scene == gameObject.scene
            && !string.IsNullOrEmpty(lampRootNamePrefix)
            && candidate.name.StartsWith(lampRootNamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private void ClearManagedLampLights()
    {
        for (int i = 0; i < managedLampLights.Count; i++)
        {
            Light light = managedLampLights[i].Light;
            if (light == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(light.gameObject);
            }
            else
            {
                DestroyImmediate(light.gameObject);
            }
        }

        managedLampLights.Clear();
    }

    private Light ResolveDirectionalLight()
    {
        if (IsSceneObject(directionalLight) && directionalLight.type == LightType.Directional)
        {
            return directionalLight;
        }

        if (IsSceneObject(RenderSettings.sun) && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (IsSceneObject(candidate) && candidate.type == LightType.Directional)
            {
                return candidate;
            }
        }

        return null;
    }

    private Volume ResolveGlobalVolume()
    {
        if (IsSceneObject(globalVolume))
        {
            return globalVolume;
        }

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume candidate = volumes[i];
            if (candidate == null || candidate.gameObject.scene != gameObject.scene || !candidate.isGlobal)
            {
                continue;
            }

            globalVolume = candidate;
            return candidate;
        }

        return null;
    }

    private bool IsSceneObject(Component component)
    {
        if (component == null)
        {
            return false;
        }

        Scene componentScene = component.gameObject.scene;
        return componentScene.IsValid() && componentScene == gameObject.scene;
    }

    private readonly struct ManagedLampLight
    {
        public ManagedLampLight(Transform root, Light light)
        {
            Root = root;
            Light = light;
        }

        public Transform Root { get; }
        public Light Light { get; }
    }
}

public static class PawPalWalkTimeOfDayBootstrap
{
    private const string ControllerObjectName = "PawPalWalkTimeOfDayController";
    private static bool runtimeInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallRuntime()
    {
        if (!runtimeInstalled)
        {
            runtimeInstalled = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        EnsureController(SceneManager.GetActiveScene(), false);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureController(scene, false);
    }

    public static PawPalWalkTimeOfDayController EnsureController(Scene scene, bool editorPreview)
    {
        if (!PawPalWalkTimeOfDayController.IsSupportedScene(scene))
        {
            return null;
        }

        PawPalWalkTimeOfDayController[] existing = UnityEngine.Object.FindObjectsByType<PawPalWalkTimeOfDayController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            PawPalWalkTimeOfDayController controller = existing[i];
            if (controller != null && controller.gameObject.scene == scene)
            {
                controller.ApplyVisualsNow();
                return controller;
            }
        }

        GameObject root = new GameObject(ControllerObjectName);
#if UNITY_EDITOR
        if (editorPreview)
        {
            root.hideFlags = HideFlags.DontSaveInEditor;
        }
#endif
        SceneManager.MoveGameObjectToScene(root, scene);
        PawPalWalkTimeOfDayController created = root.AddComponent<PawPalWalkTimeOfDayController>();
        created.ApplyVisualsNow();
        return created;
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class PawPalWalkTimeOfDayEditorBootstrap
{
    static PawPalWalkTimeOfDayEditorBootstrap()
    {
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorApplication.delayCall += EnsureActiveScenePreview;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        PawPalWalkTimeOfDayBootstrap.EnsureController(scene, true);
    }

    private static void EnsureActiveScenePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        PawPalWalkTimeOfDayBootstrap.EnsureController(SceneManager.GetActiveScene(), true);
    }
}
#endif
