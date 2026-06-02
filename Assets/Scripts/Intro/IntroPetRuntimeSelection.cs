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

    public static IntroPetRuntimeSelection Create(IntroPetDefinition definition, int index, string petName = null)
    {
        IntroPetRuntimeSelection selection = new IntroPetRuntimeSelection();
        selection.Definition = definition;
        selection.FurIndex = 0;
        selection.FurVariant = definition != null ? definition.GetDefaultFurVariant() : null;
        selection.Gender = index % 2 == 0 ? PawPalDogGender.Male : PawPalDogGender.Female;
        selection.Personality = PickPersonality(definition != null ? definition.PetId : "pet", index);
        selection.PetName = SanitizeName(petName);
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

    public static string FormatIntroLifeStage(IntroPetDefinition definition)
    {
        string normalizedIdentity = NormalizeIntroIdentity(definition);
        if (normalizedIdentity.Contains("puppy"))
        {
            return "Puppy";
        }

        if (normalizedIdentity.Contains("kitten"))
        {
            return "Kitten";
        }

        return "Adult";
    }

    private static string NormalizeIntroIdentity(IntroPetDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return NormalizeIntroIdentityPart(definition.PetId)
            + NormalizeIntroIdentityPart(definition.BreedName)
            + NormalizeIntroIdentityPart(definition.DisplayName);
    }

    private static string NormalizeIntroIdentityPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                buffer[count++] = char.ToLowerInvariant(character);
            }
        }

        return count > 0 ? new string(buffer, 0, count) : string.Empty;
    }
}

internal static class IntroPetRandomNameGenerator
{
    private static readonly string[] DogNames =
    {
        "Archie",
        "Bailey",
        "Biscuit",
        "Clover",
        "Copper",
        "Daisy",
        "Finn",
        "Maple",
        "Milo",
        "Ollie",
        "Pepper",
        "Poppy",
        "Scout",
        "Sunny",
        "Teddy",
        "Waffles"
    };

    private static readonly string[] CatNames =
    {
        "Bean",
        "Cleo",
        "Fig",
        "Iris",
        "Junie",
        "Miso",
        "Mochi",
        "Nori",
        "Olive",
        "Pippa",
        "Suki",
        "Taro",
        "Willow",
        "Yuki",
        "Ziggy",
        "Zuzu"
    };

    public static string GenerateName(IntroPetDefinition definition, System.Collections.Generic.ISet<string> usedNames)
    {
        IntroPetSpecies species = definition != null ? definition.Species : IntroPetSpecies.Dog;
        string[] pool = species == IntroPetSpecies.Cat ? CatNames : DogNames;
        if (pool == null || pool.Length == 0)
        {
            return IntroPetRuntimeSelection.DefaultName;
        }

        int startIndex = UnityEngine.Random.Range(0, pool.Length);
        if (usedNames != null && usedNames.Count < pool.Length)
        {
            for (int offset = 0; offset < pool.Length; offset++)
            {
                string candidate = pool[(startIndex + offset) % pool.Length];
                if (usedNames.Contains(candidate))
                {
                    continue;
                }

                usedNames.Add(candidate);
                return candidate;
            }
        }

        string fallback = pool[startIndex];
        if (usedNames != null)
        {
            usedNames.Add(fallback);
        }

        return fallback;
    }
}
