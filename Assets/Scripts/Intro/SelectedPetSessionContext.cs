using System;
using UnityEngine;

[Serializable]
public sealed class SelectedPetSessionData
{
    public IntroPetDefinition Definition;
    public FurVariantDefinition FurVariant;
    public int FurIndex;
    public PawPalDogGender Gender;
    public PawPalDogPersonality Personality;
    public string PetName;
    public string RuntimePetId;

    public IntroPetSpecies Species
    {
        get { return Definition != null ? Definition.Species : IntroPetSpecies.Dog; }
    }

    public string BreedName
    {
        get { return Definition != null ? Definition.BreedLabel : "Mixed breed"; }
    }

    public string SafeName
    {
        get { return IntroPetRuntimeSelection.SanitizeName(PetName); }
    }

    public PawPalDogState BuildTemporaryDogState()
    {
        PawPalDogState state = new PawPalDogState
        {
            Id = string.IsNullOrWhiteSpace(RuntimePetId) ? "intro_pet_" + Guid.NewGuid().ToString("N").Substring(0, 8) : RuntimePetId,
            DisplayName = SafeName,
            Gender = Gender,
            Personality = Personality,
            FurColor = FurVariant != null ? FurVariant.SafeDisplayName : "Default",
            Breed = BreedName,
            ProfileVersion = IntroPetFormatting.RuntimeProfileVersion,
            Food01 = 0.86f,
            Water01 = 0.86f,
            Hygiene01 = 0.84f,
            Activity01 = 0.9f,
            Energy01 = 0.92f,
            TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion,
            Bond01 = PawPalTrickSaveDefaults.DefaultBond01,
            BondXp = Mathf.RoundToInt(PawPalTrickSaveDefaults.DefaultBond01 * PawPalGameRuntime.MaxBondXp),
            BondLevel = PawPalGameRuntime.GetBondLevelForXp(Mathf.RoundToInt(PawPalTrickSaveDefaults.DefaultBond01 * PawPalGameRuntime.MaxBondXp)),
            Mood01 = PawPalTrickSaveDefaults.DefaultMood01,
            TrainingFatigue01 = 0f,
            LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks,
            Endurance = 3,
            Mobility = 3,
            Speed = 3,
            Focus = 3
        };

        PawPalGameRuntime.EnsureDogBondProgression(state);
        return state;
    }
}

public static class SelectedPetSessionContext
{
    private static SelectedPetSessionData pendingSelection;

    public static bool HasPendingSelection
    {
        get { return pendingSelection != null; }
    }

    public static void SetPendingSelection(IntroPetRuntimeSelection selection)
    {
        pendingSelection = selection != null ? selection.ToSessionData() : null;
    }

    public static bool TryPeekPendingSelection(out SelectedPetSessionData selection)
    {
        selection = pendingSelection;
        return selection != null;
    }

    public static bool TryConsumePendingSelection(out SelectedPetSessionData selection)
    {
        selection = pendingSelection;
        pendingSelection = null;
        return selection != null;
    }

    public static void Clear()
    {
        pendingSelection = null;
    }
}
