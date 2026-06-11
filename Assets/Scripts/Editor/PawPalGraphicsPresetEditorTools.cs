using UnityEditor;
using UnityEngine;

public static class PawPalGraphicsPresetEditorTools
{
    private const string MenuRoot = "PawFriends/Graphics/Preset/";
    private const string BatteryMenuPath = MenuRoot + "Battery";
    private const string BalancedMenuPath = MenuRoot + "Balanced";
    private const string QualityMenuPath = MenuRoot + "Quality";
    private const string UltraMenuPath = MenuRoot + "Ultra";

    [MenuItem(BatteryMenuPath)]
    public static void SetBattery()
    {
        ApplyPreset(PawPalGraphicsPreset.Battery);
    }

    [MenuItem(BalancedMenuPath)]
    public static void SetBalanced()
    {
        ApplyPreset(PawPalGraphicsPreset.Balanced);
    }

    [MenuItem(QualityMenuPath)]
    public static void SetQuality()
    {
        ApplyPreset(PawPalGraphicsPreset.Quality);
    }

    [MenuItem(UltraMenuPath)]
    public static void SetUltra()
    {
        ApplyPreset(PawPalGraphicsPreset.Ultra);
    }

    [MenuItem(BatteryMenuPath, true)]
    public static bool ValidateBattery()
    {
        return ValidatePresetMenu(PawPalGraphicsPreset.Battery);
    }

    [MenuItem(BalancedMenuPath, true)]
    public static bool ValidateBalanced()
    {
        return ValidatePresetMenu(PawPalGraphicsPreset.Balanced);
    }

    [MenuItem(QualityMenuPath, true)]
    public static bool ValidateQuality()
    {
        return ValidatePresetMenu(PawPalGraphicsPreset.Quality);
    }

    [MenuItem(UltraMenuPath, true)]
    public static bool ValidateUltra()
    {
        return ValidatePresetMenu(PawPalGraphicsPreset.Ultra);
    }

    private static bool ValidatePresetMenu(PawPalGraphicsPreset preset)
    {
        bool isAllowed = preset <= PawPalGraphicsSettings.MaximumSelectablePreset;
        Menu.SetChecked(GetMenuPath(preset), isAllowed && PawPalGraphicsSettings.MobilePreset == preset);
        return isAllowed;
    }

    private static void ApplyPreset(PawPalGraphicsPreset preset)
    {
        if (preset > PawPalGraphicsSettings.MaximumSelectablePreset)
        {
            Debug.LogWarning("That graphics preset is not supported on this platform.");
            return;
        }

        PawPalGraphicsSettings.SetPreset(preset);
        ReapplyOpenGraphicsEnhancers();
        Debug.Log("PawFriends graphics preset set to " + PawPalGraphicsSettings.GetPresetLabel(preset) + ".");
    }

    private static void ReapplyOpenGraphicsEnhancers()
    {
#if UNITY_2022_2_OR_NEWER
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsByType<LivingRoomGraphicsEnhancer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsOfType<LivingRoomGraphicsEnhancer>(true);
#endif
        for (int i = 0; i < enhancers.Length; i++)
        {
            LivingRoomGraphicsEnhancer enhancer = enhancers[i];
            if (enhancer == null || !enhancer.gameObject.scene.IsValid() || !enhancer.gameObject.scene.isLoaded)
            {
                continue;
            }

            enhancer.ApplyGraphics();
            EditorUtility.SetDirty(enhancer);
        }

        SceneView.RepaintAll();
    }

    private static string GetMenuPath(PawPalGraphicsPreset preset)
    {
        switch (preset)
        {
            case PawPalGraphicsPreset.Battery:
                return BatteryMenuPath;
            case PawPalGraphicsPreset.Quality:
                return QualityMenuPath;
            case PawPalGraphicsPreset.Ultra:
                return UltraMenuPath;
            default:
                return BalancedMenuPath;
        }
    }
}
