using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroTimeOfDayPracticalLightsController : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private IntroBackdropRingController sharedTimeOfDaySource;

    [Header("Lamp Matching")]
    [SerializeField] private string lampRootNamePrefix = "lamp_pole_small";
    [Tooltip("Lamp lights reach their original scene intensity once daylight falls to this value.")]
    [SerializeField, Range(0f, 1f)] private float nightActivationDaylightThreshold = 0.35f;
    [SerializeField, Range(0f, 2f)] private float dayIntensityMultiplier;
    [SerializeField, Range(0f, 2f)] private float nightIntensityMultiplier = 1f;
    [SerializeField] private bool usePointLightsForLampPosts = true;
    [SerializeField, Min(0f)] private float minimumNightIntensity = 4f;
    [SerializeField, Min(0f)] private float minimumNightRange = 6f;

    private readonly List<ManagedLampLight> managedLights = new List<ManagedLampLight>();
    private int lastAppliedMinuteStamp = int.MinValue;
    private float lastAppliedNightBlend = -1f;

    public void Configure(IntroBackdropRingController backdropRingController)
    {
        sharedTimeOfDaySource = backdropRingController;
        InvalidateCache();
        ApplyLightingState(true);
    }

    private void OnEnable()
    {
        ApplyLightingState(true);
    }

    private void Update()
    {
        ApplyLightingState(false);
    }

    private void OnDisable()
    {
        RestoreOriginalState();
    }

    private void OnTransformChildrenChanged()
    {
        InvalidateCache();
    }

    private void ApplyLightingState(bool force)
    {
        EnsureManagedLights();

        PawPalTimeOfDayState state = ResolveTimeOfDayState();
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        float daylight01 = Mathf.Clamp01(state.Daylight01);
        float nightBlend = CalculateNightBlend(daylight01);
        if (!force && minuteStamp == lastAppliedMinuteStamp && Mathf.Approximately(nightBlend, lastAppliedNightBlend))
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        lastAppliedNightBlend = nightBlend;

        for (int i = managedLights.Count - 1; i >= 0; i--)
        {
            ManagedLampLight entry = managedLights[i];
            if (entry.Light == null)
            {
                managedLights.RemoveAt(i);
                continue;
            }

            float dayIntensity = entry.BaseIntensity * dayIntensityMultiplier;
            float nightIntensity = Mathf.Max(entry.BaseIntensity * nightIntensityMultiplier, minimumNightIntensity);
            float intensity = Mathf.Lerp(dayIntensity, nightIntensity, nightBlend);

            if (usePointLightsForLampPosts)
            {
                entry.Light.type = LightType.Point;
            }

            entry.Light.enabled = intensity > 0.001f;
            entry.Light.intensity = intensity;
            entry.Light.range = Mathf.Lerp(entry.BaseRange, Mathf.Max(entry.BaseRange, minimumNightRange), nightBlend);
            entry.Light.shadows = LightShadows.None;
            entry.Light.renderMode = LightRenderMode.ForcePixel;
        }
    }

    private float CalculateNightBlend(float daylight01)
    {
        float fullNightDaylight = Mathf.Clamp01(nightActivationDaylightThreshold);
        if (Mathf.Approximately(fullNightDaylight, 1f))
        {
            return daylight01 < 1f ? 1f : 0f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(1f, fullNightDaylight, daylight01));
    }

    private void EnsureManagedLights()
    {
        bool needsRefresh = managedLights.Count == 0;
        for (int i = 0; !needsRefresh && i < managedLights.Count; i++)
        {
            if (managedLights[i].Light == null)
            {
                needsRefresh = true;
            }
        }

        if (!needsRefresh)
        {
            return;
        }

        managedLights.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null || !IsMatchingLampRoot(current))
            {
                continue;
            }

            Light[] childLights = current.GetComponentsInChildren<Light>(true);
            for (int lightIndex = 0; lightIndex < childLights.Length; lightIndex++)
            {
                Light light = childLights[lightIndex];
                if (light == null || light.type == LightType.Directional)
                {
                    continue;
                }

                if (ContainsLight(light))
                {
                    continue;
                }

                managedLights.Add(new ManagedLampLight(light));
            }
        }
    }

    private bool IsMatchingLampRoot(Transform candidate)
    {
        return candidate != null
            && candidate.gameObject.scene == gameObject.scene
            && !string.IsNullOrEmpty(lampRootNamePrefix)
            && candidate.name.StartsWith(lampRootNamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsLight(Light light)
    {
        for (int i = 0; i < managedLights.Count; i++)
        {
            if (managedLights[i].Light == light)
            {
                return true;
            }
        }

        return false;
    }

    private void InvalidateCache()
    {
        RestoreOriginalState();
        managedLights.Clear();
        lastAppliedMinuteStamp = int.MinValue;
        lastAppliedNightBlend = -1f;
    }

    private void RestoreOriginalState()
    {
        for (int i = 0; i < managedLights.Count; i++)
        {
            ManagedLampLight entry = managedLights[i];
            if (entry.Light == null)
            {
                continue;
            }

            entry.Light.enabled = entry.WasInitiallyEnabled;
            entry.Light.intensity = entry.BaseIntensity;
            entry.Light.range = entry.BaseRange;
            entry.Light.type = entry.BaseType;
            entry.Light.shadows = entry.BaseShadows;
            entry.Light.renderMode = entry.BaseRenderMode;
        }
    }

    private PawPalTimeOfDayState ResolveTimeOfDayState()
    {
        if (sharedTimeOfDaySource != null)
        {
            return sharedTimeOfDaySource.EvaluateTimeOfDayState();
        }

        return PawPalTimeOfDayEvaluator.Evaluate(
            true,
            false,
            12f,
            7f,
            20f,
            1f);
    }

    private readonly struct ManagedLampLight
    {
        public ManagedLampLight(Light light)
        {
            Light = light;
            WasInitiallyEnabled = light != null && light.enabled;
            BaseIntensity = light != null ? light.intensity : 0f;
            BaseRange = light != null ? light.range : 0f;
            BaseType = light != null ? light.type : LightType.Point;
            BaseShadows = light != null ? light.shadows : LightShadows.None;
            BaseRenderMode = light != null ? light.renderMode : LightRenderMode.Auto;
        }

        public Light Light { get; }
        public bool WasInitiallyEnabled { get; }
        public float BaseIntensity { get; }
        public float BaseRange { get; }
        public LightType BaseType { get; }
        public LightShadows BaseShadows { get; }
        public LightRenderMode BaseRenderMode { get; }
    }
}
