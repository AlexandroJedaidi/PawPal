using System;
using System.Globalization;
using UnityEngine;

[Serializable]
public sealed class IntroPetRuntimeSelection
{
    public const string DefaultName = "Buddy";
    public const int MaxNameVisibleCharacters = 12;

    public IntroPetDefinition Definition;
    public FurVariantDefinition FurVariant;
    public int FurIndex;
    public PawPalDogGender Gender;
    public PawPalDogPersonality Personality;
    public string PetName;
    public string RuntimePetId;

    public static IntroPetRuntimeSelection Create(IntroPetDefinition definition, int index)
    {
        IntroPetRuntimeSelection selection = new IntroPetRuntimeSelection();
        selection.Definition = definition;
        selection.FurIndex = 0;
        selection.FurVariant = definition != null ? definition.GetDefaultFurVariant() : null;
        selection.Gender = index % 2 == 0 ? PawPalDogGender.Male : PawPalDogGender.Female;
        selection.Personality = PickPersonality(definition != null ? definition.PetId : "pet", index);
        selection.PetName = DefaultName;
        selection.RuntimePetId = "intro_" + (definition != null && !string.IsNullOrWhiteSpace(definition.PetId) ? definition.PetId : "pet") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        return selection;
    }

    public string SafePetName
    {
        get { return SanitizeName(PetName); }
    }

    public void SetName(string value)
    {
        PetName = SanitizeName(value);
    }

    public void SetGender(PawPalDogGender gender)
    {
        Gender = gender;
    }

    public void SetFurIndex(int index)
    {
        if (Definition == null || Definition.FurVariants == null || Definition.FurVariants.Length == 0)
        {
            FurIndex = 0;
            FurVariant = null;
            return;
        }

        int count = Definition.FurVariants.Length;
        FurIndex = ((index % count) + count) % count;
        FurVariant = Definition.GetFurVariant(FurIndex);
    }

    public SelectedPetSessionData ToSessionData()
    {
        return new SelectedPetSessionData
        {
            Definition = Definition,
            FurVariant = FurVariant,
            FurIndex = FurIndex,
            Gender = Gender,
            Personality = Personality,
            PetName = SafePetName,
            RuntimePetId = RuntimePetId
        };
    }

    public static string SanitizeName(string value)
    {
        string trimmed = string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();
        trimmed = trimmed.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", " ");
        while (trimmed.Contains("  "))
        {
            trimmed = trimmed.Replace("  ", " ");
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            trimmed = DefaultName;
        }

        return LimitVisibleCharacters(trimmed, MaxNameVisibleCharacters);
    }

    private static string LimitVisibleCharacters(string value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || maxCharacters <= 0)
        {
            return string.Empty;
        }

        int[] textElements = StringInfo.ParseCombiningCharacters(value);
        if (textElements == null || textElements.Length <= maxCharacters)
        {
            return value;
        }

        return value.Substring(0, textElements[maxCharacters]);
    }

    private static PawPalDogPersonality PickPersonality(string key, int salt)
    {
        Array personalities = Enum.GetValues(typeof(PawPalDogPersonality));
        int hash = Mathf.Abs((key ?? string.Empty).GetHashCode() + salt * 397);
        return (PawPalDogPersonality)personalities.GetValue(hash % personalities.Length);
    }
}

public static class IntroPetFormatting
{
    public const int RuntimeProfileVersion = 1;

    public static string FormatGender(PawPalDogGender gender)
    {
        return gender == PawPalDogGender.Female ? "Female" : "Male";
    }

    public static string FormatPersonality(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Gentle:
                return "Gentle";
            case PawPalDogPersonality.Energetic:
                return "Energetic";
            case PawPalDogPersonality.Loyal:
                return "Loyal";
            case PawPalDogPersonality.Curious:
                return "Curious";
            case PawPalDogPersonality.Clever:
                return "Clever";
            case PawPalDogPersonality.Social:
                return "Social";
            case PawPalDogPersonality.Playful:
                return "Playful";
            case PawPalDogPersonality.Mischievous:
                return "Mischievous";
            default:
                return "Relaxed";
        }
    }
}
