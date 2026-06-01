using UnityEngine;

[CreateAssetMenu(menuName = "PawFriends/Intro/Fur Variant", fileName = "FurVariant")]
public sealed class FurVariantDefinition : ScriptableObject
{
    public string VariantId = "default";
    public string DisplayName = "Default";
    public Color SwatchColor = Color.white;
    public GameObject VariantPrefab;
    public Material ReplacementMaterial;
    public string MaterialNameContains = string.Empty;

    public string SafeDisplayName
    {
        get { return string.IsNullOrWhiteSpace(DisplayName) ? "Default" : DisplayName; }
    }

    public bool HasPrefab
    {
        get { return VariantPrefab != null; }
    }

    public bool HasMaterial
    {
        get { return ReplacementMaterial != null; }
    }
}
