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
    [SerializeField, Range(0f, 1f)] private float nightActivationDaylightThreshold = 0.35f;

    private readonly List<ManagedLampLight> managedLights = new List<ManagedLampLight>();
    private int lastAppliedMinuteStamp = int.MinValue;
    private bool lastAppliedNightState;

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
        bool useNightLights = state.Daylight01 <= nightActivationDaylightThreshold;
        if (!force && minuteStamp == lastAppliedMinuteStamp && useNightLights == lastAppliedNightState)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        lastAppliedNightState = useNightLights;

        for (int i = managedLights.Count - 1; i >= 0; i--)
        {
            ManagedLampLight entry = managedLights[i];
            if (entry.Light == null)
            {
                managedLights.RemoveAt(i);
                continue;
            }

            entry.Light.enabled = useNightLights && entry.WasInitiallyEnabled;
        }
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

                managedLights.Add(new ManagedLampLight(light, light.enabled));
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
        managedLights.Clear();
        lastAppliedMinuteStamp = int.MinValue;
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
        public ManagedLampLight(Light light, bool wasInitiallyEnabled)
        {
            Light = light;
            WasInitiallyEnabled = wasInitiallyEnabled;
        }

        public Light Light { get; }
        public bool WasInitiallyEnabled { get; }
    }
}
