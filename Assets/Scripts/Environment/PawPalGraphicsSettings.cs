using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum PawPalGraphicsPreset
{
    Battery = 0,
    Balanced = 1,
    Quality = 2
}

public enum PawPalGraphicsProfileKind
{
    MobileBattery = 0,
    MobileBalanced = 1,
    MobileQuality = 2,
    PCHigh = 3
}

public static class PawPalGraphicsSettings
{
    private const string GraphicsPresetKey = "pawpal_graphics_preset_v1";
    private const PawPalGraphicsPreset DefaultMobilePreset = PawPalGraphicsPreset.Balanced;

    private static bool loaded;
    private static PawPalGraphicsPreset mobilePreset = DefaultMobilePreset;

    public static event Action GraphicsPresetChanged;

    private static bool UsePresetSelectorPlatform
    {
        get { return Application.isMobilePlatform || Application.isEditor; }
    }

    public static bool SupportsPresetSelector
    {
        get { return UsePresetSelectorPlatform; }
    }

    public static PawPalGraphicsPreset MobilePreset
    {
        get
        {
            EnsureLoaded();
            return mobilePreset;
        }
    }

    public static PawPalGraphicsProfileKind ActiveProfile
    {
        get
        {
            if (!UsePresetSelectorPlatform)
            {
                return PawPalGraphicsProfileKind.PCHigh;
            }

            switch (MobilePreset)
            {
                case PawPalGraphicsPreset.Battery:
                    return PawPalGraphicsProfileKind.MobileBattery;
                case PawPalGraphicsPreset.Quality:
                    return PawPalGraphicsProfileKind.MobileQuality;
                default:
                    return PawPalGraphicsProfileKind.MobileBalanced;
            }
        }
    }

    public static bool EnableHdr
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static bool EnableCameraMsaa
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool EnablePostProcessing
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool EnableBloom
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool EnableDithering
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality || ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static bool RequireDepthTexture
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static bool RequireOpaqueTexture
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static bool UseReflectionProbeBoxProjection
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality || ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static int ReflectionProbeResolution
    {
        get
        {
            switch (ActiveProfile)
            {
                case PawPalGraphicsProfileKind.MobileBattery:
                case PawPalGraphicsProfileKind.MobileBalanced:
                    return 64;
                default:
                    return 128;
            }
        }
    }

    public static float GetBloomIntensity(float baseIntensity)
    {
        switch (ActiveProfile)
        {
            case PawPalGraphicsProfileKind.MobileBattery:
                return 0f;
            case PawPalGraphicsProfileKind.MobileBalanced:
                return baseIntensity * 0.45f;
            default:
                return baseIntensity;
        }
    }

    public static int GetBloomMaxIterations()
    {
        switch (ActiveProfile)
        {
            case PawPalGraphicsProfileKind.MobileBattery:
                return 2;
            case PawPalGraphicsProfileKind.MobileBalanced:
                return 4;
            default:
                return 6;
        }
    }

    public static bool UseHighQualityBloomFiltering
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality || ActiveProfile == PawPalGraphicsProfileKind.PCHigh; }
    }

    public static float GetVignetteIntensity(float baseIntensity)
    {
        return EnablePostProcessing ? baseIntensity : 0f;
    }

    public static AntialiasingMode GetCameraAntialiasingMode()
    {
        return EnablePostProcessing && EnableCameraMsaa
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : AntialiasingMode.None;
    }

    public static AntialiasingQuality GetCameraAntialiasingQuality()
    {
        switch (ActiveProfile)
        {
            case PawPalGraphicsProfileKind.MobileBalanced:
                return AntialiasingQuality.Low;
            case PawPalGraphicsProfileKind.MobileQuality:
                return AntialiasingQuality.Medium;
            case PawPalGraphicsProfileKind.PCHigh:
                return AntialiasingQuality.High;
            default:
                return AntialiasingQuality.Low;
        }
    }

    public static SoftShadowQuality GetSoftShadowQuality()
    {
        switch (ActiveProfile)
        {
            case PawPalGraphicsProfileKind.MobileBattery:
                return SoftShadowQuality.Low;
            case PawPalGraphicsProfileKind.MobileBalanced:
                return SoftShadowQuality.Medium;
            default:
                return SoftShadowQuality.High;
        }
    }

    public static string GetPresetLabel(PawPalGraphicsPreset preset)
    {
        switch (preset)
        {
            case PawPalGraphicsPreset.Battery:
                return "Battery";
            case PawPalGraphicsPreset.Quality:
                return "Quality";
            default:
                return "Balanced";
        }
    }

    public static void AdjustPreset(int delta)
    {
        if (!UsePresetSelectorPlatform || delta == 0)
        {
            return;
        }

        int next = Mathf.Clamp((int)MobilePreset + delta, (int)PawPalGraphicsPreset.Battery, (int)PawPalGraphicsPreset.Quality);
        SetPreset((PawPalGraphicsPreset)next);
    }

    public static void SetPreset(PawPalGraphicsPreset preset)
    {
        if (!UsePresetSelectorPlatform)
        {
            return;
        }

        EnsureLoaded();
        if (mobilePreset == preset)
        {
            ApplyRuntimeProfile();
            return;
        }

        mobilePreset = preset;
        PlayerPrefs.SetInt(GraphicsPresetKey, (int)mobilePreset);
        PlayerPrefs.Save();
        ApplyRuntimeProfile();
        GraphicsPresetChanged?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        ApplyRuntimeProfile();
    }

    public static void ApplyRuntimeProfile()
    {
        EnsureLoaded();

        int qualityLevel = FindQualityLevel(GetQualityLevelName(ActiveProfile));
        if (qualityLevel >= 0 && QualitySettings.GetQualityLevel() != qualityLevel)
        {
            QualitySettings.SetQualityLevel(qualityLevel, true);
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = GetTargetFrameRate(ActiveProfile);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        if (UsePresetSelectorPlatform)
        {
            int storedValue = PlayerPrefs.GetInt(GraphicsPresetKey, (int)DefaultMobilePreset);
            storedValue = Mathf.Clamp(storedValue, (int)PawPalGraphicsPreset.Battery, (int)PawPalGraphicsPreset.Quality);
            mobilePreset = (PawPalGraphicsPreset)storedValue;
        }
        else
        {
            mobilePreset = DefaultMobilePreset;
        }

        loaded = true;
    }

    private static int FindQualityLevel(string qualityName)
    {
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (string.Equals(qualityNames[i], qualityName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetQualityLevelName(PawPalGraphicsProfileKind profile)
    {
        switch (profile)
        {
            case PawPalGraphicsProfileKind.MobileBattery:
                return "MobileBattery";
            case PawPalGraphicsProfileKind.MobileBalanced:
                return "MobileBalanced";
            case PawPalGraphicsProfileKind.MobileQuality:
                return "MobileQuality";
            default:
                return "PCHigh";
        }
    }

    private static int GetTargetFrameRate(PawPalGraphicsProfileKind profile)
    {
        switch (profile)
        {
            case PawPalGraphicsProfileKind.MobileBattery:
                return 30;
            case PawPalGraphicsProfileKind.MobileBalanced:
            case PawPalGraphicsProfileKind.MobileQuality:
                return 60;
            default:
                return -1;
        }
    }
}
