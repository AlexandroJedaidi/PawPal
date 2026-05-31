using System;
using System.Collections.Generic;
using UnityEngine;

public interface ITrickRequirementProvider
{
    bool MeetsRequirements(PawPalDogState dog, out string failureReason);
}

[Serializable]
public sealed class CompetitionTrickLoadout
{
    public string CompetitionId;
    public List<PawPalTrickId> RequiredTricks = new List<PawPalTrickId>();
    public List<PawPalTrickId> OptionalTricks = new List<PawPalTrickId>();
}

public static class TrickScoreCalculator
{
    public static int CalculateScore(PawPalDogState dog, IEnumerable<PawPalTrickId> tricks)
    {
        if (dog == null || tricks == null)
        {
            return 0;
        }

        PawPalTrickCatalog.EnsureDogTrickData(dog);
        int score = 0;
        foreach (PawPalTrickId trickId in tricks)
        {
            PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
            PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(dog, trickId);
            if (definition == null || progress == null || !progress.IsLearned)
            {
                continue;
            }

            int baseValue = Mathf.Max(1, definition.CompetitionScoreValue);
            float masteryMultiplier = progress.MasteryLevel >= 2 ? 1.35f : 1f;
            score += Mathf.RoundToInt(baseValue * masteryMultiplier);
        }

        score += Mathf.RoundToInt(PawPalGameRuntime.GetBondProgress01(dog) * 8f);
        score += Mathf.RoundToInt(Mathf.Clamp01(dog.Mood01) * 5f);
        score += Mathf.RoundToInt(PawPalGameRuntime.GetDogStamina01(dog) * 5f);
        return Mathf.Max(0, score);
    }
}

public sealed class BeginnerObedienceTrickRequirementProvider : ITrickRequirementProvider
{
    public bool MeetsRequirements(PawPalDogState dog, out string failureReason)
    {
        failureReason = string.Empty;
        if (dog == null)
        {
            failureReason = "Choose a dog first.";
            return false;
        }

        PawPalTrickCatalog.EnsureDogTrickData(dog);
        if (!IsLearned(dog, PawPalTrickId.Sit))
        {
            failureReason = "Teach Sit first.";
            return false;
        }

        if (!IsLearned(dog, PawPalTrickId.Shake))
        {
            failureReason = "Teach Shake first.";
            return false;
        }

        if (!IsLearned(dog, PawPalTrickId.Jump))
        {
            failureReason = "Teach Jump first.";
            return false;
        }

        return true;
    }

    private static bool IsLearned(PawPalDogState dog, PawPalTrickId trickId)
    {
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(dog, trickId);
        return progress != null && progress.IsLearned;
    }
}
