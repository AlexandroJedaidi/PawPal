using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum PawPalGraphicsPreset
{
    Battery = 0,
    Balanced = 1,
    Quality = 2,
    Ultra = 3
}

public enum PawPalGraphicsProfileKind
{
    MobileBattery = 0,
    MobileBalanced = 1,
    MobileQuality = 2,
    PCHigh = 3,
    PCUltra = 4
}

public static class PawPalGraphicsSettings
{
    private const string GraphicsPresetKey = "pawpal_graphics_preset_v1";

    private static bool loaded;
    private static PawPalGraphicsPreset selectedPreset = PawPalGraphicsPreset.Balanced;

    public static event Action GraphicsPresetChanged;

    private static bool UsePresetSelectorPlatform
    {
        get { return Application.isMobilePlatform || Application.isEditor || IsStandalonePresetPlatform; }
    }

    private static bool IsStandalonePresetPlatform
    {
        get
        {
            if (Application.isEditor)
            {
                return true;
            }

            RuntimePlatform platform = Application.platform;
            return !Application.isMobilePlatform
                && (platform == RuntimePlatform.WindowsPlayer
                    || platform == RuntimePlatform.OSXPlayer
                    || platform == RuntimePlatform.LinuxPlayer);
        }
    }

    private static PawPalGraphicsPreset DefaultPreset
    {
        get { return IsStandalonePresetPlatform ? PawPalGraphicsPreset.Ultra : PawPalGraphicsPreset.Balanced; }
    }

    public static bool SupportsPresetSelector
    {
        get { return UsePresetSelectorPlatform; }
    }

    public static PawPalGraphicsPreset MaximumSelectablePreset
    {
        get { return IsStandalonePresetPlatform ? PawPalGraphicsPreset.Ultra : PawPalGraphicsPreset.Quality; }
    }

    public static PawPalGraphicsPreset MobilePreset
    {
        get
        {
            EnsureLoaded();
            return selectedPreset;
        }
    }

    public static PawPalGraphicsProfileKind ActiveProfile
    {
        get
        {
            switch (selectedPreset)
            {
                case PawPalGraphicsPreset.Battery:
                    return PawPalGraphicsProfileKind.MobileBattery;
                case PawPalGraphicsPreset.Ultra:
                    return IsStandalonePresetPlatform
                        ? PawPalGraphicsProfileKind.PCUltra
                        : PawPalGraphicsProfileKind.MobileQuality;
                case PawPalGraphicsPreset.Quality:
                    return IsStandalonePresetPlatform
                        ? PawPalGraphicsProfileKind.PCHigh
                        : PawPalGraphicsProfileKind.MobileQuality;
                default:
                    return PawPalGraphicsProfileKind.MobileBalanced;
            }
        }
    }

    public static bool EnableHdr
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh || ActiveProfile == PawPalGraphicsProfileKind.PCUltra; }
    }

    public static bool EnableCameraMsaa
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool EnablePostProcessing
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool IsUltraQuality
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCUltra; }
    }

    public static bool EnableBloom
    {
        get { return ActiveProfile != PawPalGraphicsProfileKind.MobileBattery; }
    }

    public static bool EnableDithering
    {
        get
        {
            return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality
                || ActiveProfile == PawPalGraphicsProfileKind.PCHigh
                || ActiveProfile == PawPalGraphicsProfileKind.PCUltra;
        }
    }

    public static bool RequireDepthTexture
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh || ActiveProfile == PawPalGraphicsProfileKind.PCUltra; }
    }

    public static bool RequireOpaqueTexture
    {
        get { return ActiveProfile == PawPalGraphicsProfileKind.PCHigh || ActiveProfile == PawPalGraphicsProfileKind.PCUltra; }
    }

    public static bool UseReflectionProbeBoxProjection
    {
        get
        {
            return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality
                || ActiveProfile == PawPalGraphicsProfileKind.PCHigh
                || ActiveProfile == PawPalGraphicsProfileKind.PCUltra;
        }
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
                case PawPalGraphicsProfileKind.PCUltra:
                    return 512;
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
            case PawPalGraphicsProfileKind.PCUltra:
                return baseIntensity * 1.2f;
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
            case PawPalGraphicsProfileKind.PCUltra:
                return 8;
            default:
                return 6;
        }
    }

    public static bool UseHighQualityBloomFiltering
    {
        get
        {
            return ActiveProfile == PawPalGraphicsProfileKind.MobileQuality
                || ActiveProfile == PawPalGraphicsProfileKind.PCHigh
                || ActiveProfile == PawPalGraphicsProfileKind.PCUltra;
        }
    }

    public static float GetVignetteIntensity(float baseIntensity)
    {
        if (!EnablePostProcessing)
        {
            return 0f;
        }

        return IsUltraQuality ? baseIntensity * 1.15f : baseIntensity;
    }

    public static TonemappingMode GetTonemappingMode()
    {
        return IsUltraQuality ? TonemappingMode.ACES : TonemappingMode.Neutral;
    }

    public static float GetPostExposure(float baseExposure)
    {
        return IsUltraQuality ? baseExposure + 0.02f : baseExposure;
    }

    public static float GetContrast(float baseContrast)
    {
        return IsUltraQuality ? baseContrast + 1.5f : baseContrast;
    }

    public static float GetIndoorContrast(float baseContrast)
    {
        return IsUltraQuality ? baseContrast - 2f : baseContrast;
    }

    public static float GetIndoorSaturation(float baseSaturation)
    {
        return IsUltraQuality ? baseSaturation - 6f : baseSaturation;
    }

    public static float GetSaturation(float baseSaturation)
    {
        return IsUltraQuality ? baseSaturation + 1.5f : baseSaturation;
    }

    public static bool EnableDepthOfField
    {
        get { return IsUltraQuality; }
    }

    public static float GetDepthOfFieldFocalLength()
    {
        return IsUltraQuality ? 55f : 45f;
    }

    public static float GetDepthOfFieldAperture()
    {
        return IsUltraQuality ? 7.2f : 8.5f;
    }

    public static float GetDepthOfFieldFocusDistance()
    {
        return IsUltraQuality ? 2.4f : 2.8f;
    }

    public static float GetChromaticAberrationIntensity()
    {
        return IsUltraQuality ? 0.02f : 0.008f;
    }

    public static float GetFilmGrainIntensity()
    {
        return IsUltraQuality ? 0.08f : 0.03f;
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
            case PawPalGraphicsProfileKind.PCUltra:
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
            case PawPalGraphicsProfileKind.PCUltra:
                return SoftShadowQuality.High;
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
            case PawPalGraphicsPreset.Ultra:
                return "Ultra";
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

        int next = Mathf.Clamp((int)MobilePreset + delta, (int)PawPalGraphicsPreset.Battery, (int)MaximumSelectablePreset);
        SetPreset((PawPalGraphicsPreset)next);
    }

    public static void SetPreset(PawPalGraphicsPreset preset)
    {
        if (!UsePresetSelectorPlatform)
        {
            return;
        }

        EnsureLoaded();
        if (selectedPreset == preset)
        {
            ApplyRuntimeProfile();
            return;
        }

        selectedPreset = preset;
        PlayerPrefs.SetInt(GraphicsPresetKey, (int)selectedPreset);
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
            int storedValue = PlayerPrefs.GetInt(GraphicsPresetKey, (int)DefaultPreset);
            storedValue = Mathf.Clamp(storedValue, (int)PawPalGraphicsPreset.Battery, (int)MaximumSelectablePreset);
            selectedPreset = (PawPalGraphicsPreset)storedValue;
        }
        else
        {
            selectedPreset = DefaultPreset;
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
            case PawPalGraphicsProfileKind.PCUltra:
                return "PCUltra";
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
