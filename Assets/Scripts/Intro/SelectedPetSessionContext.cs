using System;
using System.Text;
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
            Energy01 = 1f,
            TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion,
            Bond01 = 0f,
            BondXp = 0,
            BondLevel = 1,
            Mood01 = PawPalTrickSaveDefaults.DefaultMood01,
            TrainingFatigue01 = 0f,
            LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks,
            Endurance = 1,
            Mobility = 1,
            Speed = 1,
            Focus = 1
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

public enum PawPalPetSizeClass
{
    Small,
    Medium,
    Large
}

internal enum PawPalAutoTravelContext
{
    AmbientRoam,
    SelfDirectedPlay,
    ForcedChase
}

public struct PawPalPetMovementProfile
{
    public PawPalPetSizeClass SizeClass;
    public float WalkSpeed;
    public float TrotSpeed;
    public float RunSpeed;
    public float WalkAnimatorSpeed;
    public float TrotAnimatorSpeed;
    public float RunAnimatorSpeed;

    public float GetSpeed(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return RunSpeed;
            case DogMovementPace.Trot:
                return TrotSpeed;
            default:
                return WalkSpeed;
        }
    }

    public float GetAnimatorSpeed(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return RunAnimatorSpeed;
            case DogMovementPace.Trot:
                return TrotAnimatorSpeed;
            default:
                return WalkAnimatorSpeed;
        }
    }
}

internal struct PawPalPetAutoTravelThresholdProfile
{
    public PawPalPetSizeClass SizeClass;
    public float WalkDistanceMax;
    public float TrotDistanceMax;

    public DogMovementPace ResolveBasePace(float travelDistance, PawPalAutoTravelContext context)
    {
        if (context == PawPalAutoTravelContext.ForcedChase)
        {
            return DogMovementPace.Run;
        }

        DogMovementPace resolvedPace = travelDistance <= WalkDistanceMax
            ? DogMovementPace.Walk
            : (travelDistance <= TrotDistanceMax ? DogMovementPace.Trot : DogMovementPace.Run);

        if (context == PawPalAutoTravelContext.SelfDirectedPlay && resolvedPace == DogMovementPace.Walk)
        {
            return DogMovementPace.Trot;
        }

        return resolvedPace;
    }
}

public static class PawPalPetMovementProfiles
{
    private const float MediumWalkSpeed = 0.52f;
    private const float MediumTrotSpeed = 0.98f;
    private const float MediumRunSpeed = 1.30f;
    private const float LargeWalkSpeed = 0.66f;
    private const float LargeTrotSpeed = 1.18f;
    private const float LargeRunSpeed = 3.35f;
    private const float LegacyMediumWalkSpeedForAnimator = 0.65f;
    private const float LegacyLargeWalkSpeedForAnimator = 0.80f;
    private const float LegacyLargeRunSpeedForAnimator = 1.55f;
    private const float MediumWalkAnimatorBaseline = 0.5f;
    private const float MediumTrotAnimatorBaseline = 0.78f;
    private const float MediumRunAnimatorBaseline = 1f;

    private static readonly PawPalPetMovementProfile SmallProfile = BuildProfile(
        PawPalPetSizeClass.Small,
        0.24f,
        0.78f,
        1.05f,
        0.42f,
        ScaleAnimatorBaseline(MediumTrotAnimatorBaseline, 0.78f, MediumTrotSpeed),
        ScaleAnimatorBaseline(MediumRunAnimatorBaseline, 1.05f, MediumRunSpeed));
    private static readonly PawPalPetMovementProfile MediumProfile = BuildProfile(
        PawPalPetSizeClass.Medium,
        MediumWalkSpeed,
        MediumTrotSpeed,
        MediumRunSpeed,
        MediumWalkAnimatorBaseline,
        MediumTrotAnimatorBaseline,
        MediumRunAnimatorBaseline);
    private static readonly PawPalPetMovementProfile LargeProfile = BuildProfile(
        PawPalPetSizeClass.Large,
        LargeWalkSpeed,
        LargeTrotSpeed,
        LargeRunSpeed,
        ScaleAnimatorBaseline(MediumWalkAnimatorBaseline, LegacyLargeWalkSpeedForAnimator, LegacyMediumWalkSpeedForAnimator),
        ScaleAnimatorBaseline(MediumTrotAnimatorBaseline, LargeTrotSpeed, MediumTrotSpeed),
        ScaleAnimatorBaseline(MediumRunAnimatorBaseline, LegacyLargeRunSpeedForAnimator, MediumRunSpeed));

    public static PawPalPetMovementProfile DefaultProfile
    {
        get { return MediumProfile; }
    }

    public static PawPalPetMovementProfile Resolve(SelectedPetSessionData selection, string objectName)
    {
        if (selection == null)
        {
            return Resolve(null, null, null, objectName);
        }

        return Resolve(
            selection.Definition != null ? selection.Definition.PetId : null,
            selection.BreedName,
            null,
            objectName);
    }

    public static PawPalPetMovementProfile Resolve(IntroPetDefinition definition, string runtimeBreed, string objectName)
    {
        return Resolve(
            definition != null ? definition.PetId : null,
            definition != null ? definition.BreedLabel : null,
            runtimeBreed,
            objectName);
    }

    public static PawPalPetMovementProfile Resolve(string petId, string breedName, string runtimeBreed, string objectName)
    {
        return GetProfile(ResolveSizeClass(petId, breedName, runtimeBreed, objectName));
    }

    public static PawPalPetSizeClass ResolveSizeClass(string petId, string breedName, string runtimeBreed, string objectName)
    {
        return PawPalPetAnimationRegistry.ResolveSizeClass(petId, breedName, runtimeBreed, objectName);
    }

    private static PawPalPetMovementProfile GetProfile(PawPalPetSizeClass sizeClass)
    {
        switch (sizeClass)
        {
            case PawPalPetSizeClass.Small:
                return SmallProfile;
            case PawPalPetSizeClass.Large:
                return LargeProfile;
            default:
                return MediumProfile;
        }
    }

    private static PawPalPetMovementProfile BuildProfile(PawPalPetSizeClass sizeClass, float walkSpeed, float trotSpeed, float runSpeed)
    {
        return BuildProfile(
            sizeClass,
            walkSpeed,
            trotSpeed,
            runSpeed,
            ScaleAnimatorBaseline(MediumWalkAnimatorBaseline, walkSpeed, MediumWalkSpeed),
            ScaleAnimatorBaseline(MediumTrotAnimatorBaseline, trotSpeed, MediumTrotSpeed),
            ScaleAnimatorBaseline(MediumRunAnimatorBaseline, runSpeed, MediumRunSpeed));
    }

    private static PawPalPetMovementProfile BuildProfile(
        PawPalPetSizeClass sizeClass,
        float walkSpeed,
        float trotSpeed,
        float runSpeed,
        float walkAnimatorSpeed,
        float trotAnimatorSpeed,
        float runAnimatorSpeed)
    {
        return new PawPalPetMovementProfile
        {
            SizeClass = sizeClass,
            WalkSpeed = walkSpeed,
            TrotSpeed = trotSpeed,
            RunSpeed = runSpeed,
            WalkAnimatorSpeed = walkAnimatorSpeed,
            TrotAnimatorSpeed = trotAnimatorSpeed,
            RunAnimatorSpeed = runAnimatorSpeed
        };
    }

    private static float ScaleAnimatorBaseline(float baseline, float targetSpeed, float mediumSpeed)
    {
        if (mediumSpeed <= 0.0001f)
        {
            return baseline;
        }

        return baseline * (targetSpeed / mediumSpeed);
    }

}

internal static class PawPalPetAutoTravelProfiles
{
    private static readonly PawPalPetAutoTravelThresholdProfile SmallProfile = new PawPalPetAutoTravelThresholdProfile
    {
        SizeClass = PawPalPetSizeClass.Small,
        WalkDistanceMax = 1.4f,
        TrotDistanceMax = 3.0f
    };

    private static readonly PawPalPetAutoTravelThresholdProfile MediumProfile = new PawPalPetAutoTravelThresholdProfile
    {
        SizeClass = PawPalPetSizeClass.Medium,
        WalkDistanceMax = 1.8f,
        TrotDistanceMax = 4.2f
    };

    private static readonly PawPalPetAutoTravelThresholdProfile LargeProfile = new PawPalPetAutoTravelThresholdProfile
    {
        SizeClass = PawPalPetSizeClass.Large,
        WalkDistanceMax = 2.4f,
        TrotDistanceMax = 5.8f
    };

    public static PawPalPetAutoTravelThresholdProfile Resolve(PawPalPetSizeClass sizeClass)
    {
        switch (sizeClass)
        {
            case PawPalPetSizeClass.Small:
                return SmallProfile;
            case PawPalPetSizeClass.Large:
                return LargeProfile;
            default:
                return MediumProfile;
        }
    }
}
