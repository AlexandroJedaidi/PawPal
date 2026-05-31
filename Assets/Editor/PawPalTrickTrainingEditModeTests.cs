using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PawPalTrickTrainingEditModeTests
{
    [Test]
    public void OldDogStateMigrationInitializesCoreTricksWithoutDuplicates()
    {
        PawPalDogState dog = BuildDog();
        dog.TrickProfileVersion = 0;
        dog.Bond01 = 0f;
        dog.Mood01 = 0f;
        dog.Tricks = new List<PawPalDogTrickProgress>
        {
            new PawPalDogTrickProgress { TrickId = "sit", IsDiscovered = true, MasteryXp = 8f },
            new PawPalDogTrickProgress { TrickId = "Sit", IsLearned = true, MasteryXp = 50f, MasteryLevel = 1 }
        };

        bool changed = PawPalTrickCatalog.EnsureDogTrickData(dog);

        Assert.IsTrue(changed);
        Assert.AreEqual(PawPalTrickSaveDefaults.CurrentTrickProfileVersion, dog.TrickProfileVersion);
        Assert.Greater(dog.Bond01, 0f);
        Assert.Greater(dog.BondXp, 0);
        Assert.GreaterOrEqual(dog.BondLevel, 1);
        Assert.Greater(dog.Mood01, 0f);
        Assert.AreEqual(1, CountProgress(dog, PawPalTrickId.Sit));
        Assert.AreEqual(1, CountProgress(dog, PawPalTrickId.Lie));
        Assert.AreEqual(1, CountProgress(dog, PawPalTrickId.Shake));
        Assert.AreEqual(1, CountProgress(dog, PawPalTrickId.Jump));
        Assert.AreEqual(1, CountProgress(dog, PawPalTrickId.Spin));
        Assert.IsTrue(PawPalTrickCatalog.GetProgress(dog, PawPalTrickId.Sit).IsLearned);
    }

    [Test]
    public void SitRequiresAtLeastTenSuccessfulRepsToLearn()
    {
        PawPalDogState dog = BuildDog();
        dog.Personality = PawPalDogPersonality.Loyal;
        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalTrainingConfig config = ScriptableObject.CreateInstance<PawPalTrainingConfig>();
        SetPrivateField(config, "baseSuccessChance", 1f);
        SetPrivateField(config, "baseXpPerSuccess", 10f);
        SetPrivateField(config, "praiseXpBonus", 0f);

        try
        {
            for (int i = 0; i < 9; i++)
            {
                PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dog, sit, config, false, false, true);
                Assert.IsTrue(result.Success);
            }

            PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(dog, PawPalTrickId.Sit);
            Assert.NotNull(progress);
            Assert.Greater(progress.MasteryXp, 0f);
            Assert.IsFalse(progress.IsLearned);

            for (int i = 0; i < 3; i++)
            {
                PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dog, sit, config, false, false, true);
                Assert.IsTrue(result.Success);
            }

            Assert.IsTrue(progress.IsLearned);
            Assert.GreaterOrEqual(progress.MasteryXp, sit.LearnedRequiredXp);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void SuccessfulTrainingConsumesCanonicalStaminaAndReportsProgress()
    {
        UnityEngine.Random.InitState(72);
        PawPalDogState dog = BuildDog();
        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalTrainingConfig config = PawPalTrainingConfig.CreateRuntimeDefault();
        PawPalGameRuntime.EnsureCanonicalStaminaData(dog);
        PawPalWalkStaminaSnapshot before = PawPalGameRuntime.GetCanonicalStaminaSnapshot(dog);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dog, sit, config, true, false, true);

        Assert.Less(result.CurrentStamina, before.Current);
        Assert.Less(result.StaminaChange, 0f);
        Assert.GreaterOrEqual(result.CurrentProgress01, result.PreviousProgress01);
        Assert.AreEqual(result.CurrentProgress01 > result.PreviousProgress01 + 0.0001f, result.MadeProgress);
    }

    [Test]
    public void BlockedTrainingDoesNotConsumeCanonicalStamina()
    {
        PawPalDogState dog = BuildDog();
        dog.Focus = 0;
        PawPalTrickDefinition shake = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake);
        PawPalGameRuntime.EnsureCanonicalStaminaData(dog);
        PawPalWalkStaminaSnapshot before = PawPalGameRuntime.GetCanonicalStaminaSnapshot(dog);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            dog,
            shake,
            PawPalTrainingConfig.CreateRuntimeDefault(),
            false,
            false,
            false);

        PawPalWalkStaminaSnapshot after = PawPalGameRuntime.GetCanonicalStaminaSnapshot(dog);
        Assert.AreEqual(PawPalTrickFailureReason.LowFocus, result.Reason);
        Assert.AreEqual(before.Current, after.Current, 0.0001f);
        Assert.AreEqual(0f, result.StaminaChange, 0.0001f);
        Assert.AreEqual(result.PreviousProgress01, result.CurrentProgress01, 0.0001f);
        Assert.IsFalse(result.MadeProgress);
    }

    [Test]
    public void TrickAvailabilityUsesCanonicalStaminaInsteadOfLegacyEnergy()
    {
        PawPalDogState dog = BuildDog();
        dog.Energy01 = 1f;
        dog.WalkData = new PawPalDogWalkData
        {
            CurrentWalkStamina = 5f,
            MaxWalkStamina = 100f,
            LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks
        };
        MarkLearned(dog, PawPalTrickId.Sit);
        MarkLearned(dog, PawPalTrickId.Lie);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            dog,
            PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake),
            PawPalTrainingConfig.CreateRuntimeDefault(),
            true,
            false,
            true);

        Assert.AreEqual(PawPalTrickFailureReason.LowEnergy, result.Reason);
    }

    [Test]
    public void CanonicalStaminaMigrationUsesLegacyEnergyOnlyWhenWalkDataIsMissing()
    {
        PawPalDogState migratedDog = BuildDog();
        migratedDog.Energy01 = 0.4f;
        migratedDog.WalkData = null;

        PawPalGameRuntime.EnsureCanonicalStaminaData(migratedDog);
        PawPalWalkStaminaSnapshot migrated = PawPalGameRuntime.GetCanonicalStaminaSnapshot(migratedDog);
        Assert.AreEqual(0.4f, migrated.Fill01, 0.0001f);

        PawPalDogState existingDog = BuildDog();
        existingDog.Energy01 = 0.1f;
        existingDog.WalkData = new PawPalDogWalkData
        {
            CurrentWalkStamina = 75f,
            MaxWalkStamina = 100f,
            LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks
        };

        PawPalGameRuntime.EnsureCanonicalStaminaData(existingDog);
        PawPalWalkStaminaSnapshot existing = PawPalGameRuntime.GetCanonicalStaminaSnapshot(existingDog);
        Assert.AreEqual(0.75f, existing.Fill01, 0.0001f);
    }

    [Test]
    public void PersonalityModifiersChangeXpAndSuccessWithoutBlocking()
    {
        PawPalTrickDefinition jump = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Jump);

        Assert.Greater(jump.GetXpMultiplier(PawPalDogPersonality.Playful), jump.GetXpMultiplier(PawPalDogPersonality.Relaxed));
        Assert.Greater(jump.GetSuccessChanceOffset(PawPalDogPersonality.Energetic), jump.GetSuccessChanceOffset(PawPalDogPersonality.Mischievous));

        PawPalDogState mischievousDog = BuildDog();
        mischievousDog.Personality = PawPalDogPersonality.Mischievous;
        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            mischievousDog,
            jump,
            PawPalTrainingConfig.CreateRuntimeDefault(),
            true,
            false,
            true);

        Assert.AreNotEqual(PawPalTrickFailureReason.LowBond, result.Reason);
        Assert.AreNotEqual(PawPalTrickFailureReason.LowMood, result.Reason);
        Assert.AreNotEqual(PawPalTrickFailureReason.LowEnergy, result.Reason);
    }

    [Test]
    public void BondProgressionMigratesDeterministicallyFromLegacyBond()
    {
        PawPalDogState dog = BuildDog();
        dog.Bond01 = 0.42f;
        dog.BondXp = 0;
        dog.BondLevel = 0;

        PawPalTrickCatalog.EnsureDogTrickData(dog);

        Assert.AreEqual(Mathf.RoundToInt(0.42f * PawPalGameRuntime.MaxBondXp), dog.BondXp);
        Assert.AreEqual(PawPalGameRuntime.GetBondLevelForXp(dog.BondXp), dog.BondLevel);
        Assert.AreEqual(dog.BondXp / (float)PawPalGameRuntime.MaxBondXp, dog.Bond01, 0.0001f);
    }

    [Test]
    public void TrainingFatigueReducesAndRecoversOverTime()
    {
        PawPalDogState dog = BuildDog();
        dog.TrainingFatigue01 = 0.8f;
        dog.LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.AddHours(-2d).Ticks;

        PawPalTrickProgressionService.RecoverFatigue(dog, PawPalTrainingConfig.CreateRuntimeDefault(), DateTime.UtcNow);

        Assert.Less(dog.TrainingFatigue01, 0.8f);
        Assert.GreaterOrEqual(dog.TrainingFatigue01, 0f);
    }

    [Test]
    public void VoiceUnavailableStillAllowsButtonFallbackService()
    {
        string unavailableMessage = null;
        bool permissionGranted = true;
        IPawPalVoiceCommandService noMic = new PawPalUnavailableVoiceCommandService(PawPalVoiceCommandFailureReason.NoMicrophone, "No mic.");
        Run(noMic.RequestPermission(delegate(bool granted, string message)
        {
            permissionGranted = granted;
            unavailableMessage = message;
        }));

        Assert.IsFalse(permissionGranted);
        Assert.AreEqual("No mic.", unavailableMessage);

        PawPalVoiceCommandFailureReason failureReason = PawPalVoiceCommandFailureReason.None;
        bool started = noMic.StartListening(null, delegate(PawPalVoiceCommandFailureReason reason, string message)
        {
            failureReason = reason;
            unavailableMessage = message;
        });

        Assert.IsFalse(started);
        Assert.AreEqual(PawPalVoiceCommandFailureReason.NoMicrophone, failureReason);
        Assert.AreEqual("No mic.", unavailableMessage);

        PawPalVoiceRecognizedPhrase recognized = null;
        IPawPalVoiceCommandService button = new PawPalButtonFallbackVoiceCommandService();
        Run(button.RequestPermission(delegate(bool granted, string message)
        {
            permissionGranted = granted;
        }));
        button.ConfigurePhrases(new[] { "pepper sit" });
        Assert.IsTrue(button.StartListening(delegate(PawPalVoiceRecognizedPhrase phrase)
        {
            recognized = phrase;
        }, null));
        button.Tick();

        Assert.IsTrue(permissionGranted);
        Assert.NotNull(recognized);
        Assert.AreEqual("pepper sit", recognized.Transcript);
    }

    [Test]
    public void VoiceCommandCatalogResolvesDogTrickThenDogThenActiveDogTrick()
    {
        PawPalDogState activeDog = BuildDog();
        activeDog.Id = "pepper";
        activeDog.DisplayName = "Pepper";
        PawPalDogState namedDog = BuildDog();
        namedDog.Id = "sit_dog";
        namedDog.DisplayName = "Sit";

        PawPalTrickCatalog.EnsureDogTrickData(activeDog);
        PawPalTrickCatalog.EnsureDogTrickData(namedDog);

        PawPalDogTrickProgress activeSit = PawPalTrickCatalog.GetOrCreateProgress(activeDog, PawPalTrickId.Sit);
        activeSit.IsLearned = true;
        activeSit.CustomVoiceCommand = "sit";

        PawPalVoiceCommandCatalog catalog = PawPalVoiceCommandCatalogBuilder.Build(
            new[] { activeDog, namedDog },
            activeDog.Id);

        PawPalResolvedVoiceCommand dogAndTrick = catalog.Resolve("Pepper sit");
        Assert.AreEqual(PawPalResolvedVoiceCommandType.PerformTrick, dogAndTrick.Type);
        Assert.AreEqual(activeDog.Id, dogAndTrick.DogId);
        Assert.AreEqual(PawPalTrickId.Sit, dogAndTrick.TrickId);

        PawPalResolvedVoiceCommand dogOnly = catalog.Resolve("Pepper");
        Assert.AreEqual(PawPalResolvedVoiceCommandType.CallDog, dogOnly.Type);
        Assert.AreEqual(activeDog.Id, dogOnly.DogId);

        PawPalResolvedVoiceCommand namePrecedence = catalog.Resolve("sit");
        Assert.AreEqual(PawPalResolvedVoiceCommandType.CallDog, namePrecedence.Type);
        Assert.AreEqual(namedDog.Id, namePrecedence.DogId);
    }

    [Test]
    public void VoiceCommandCatalogBuildsLearnedPhrasesAndFeedbackForUnlearnedTricks()
    {
        PawPalDogState activeDog = BuildDog();
        activeDog.Id = "pepper";
        activeDog.DisplayName = "Pepper";

        PawPalTrickCatalog.EnsureDogTrickData(activeDog);
        PawPalDogTrickProgress sit = PawPalTrickCatalog.GetOrCreateProgress(activeDog, PawPalTrickId.Sit);
        sit.IsLearned = false;
        sit.CustomVoiceCommand = "sit";

        PawPalVoiceCommandCatalog unlearnedCatalog = PawPalVoiceCommandCatalogBuilder.Build(new[] { activeDog }, activeDog.Id);
        Assert.IsFalse(unlearnedCatalog.Phrases.Contains("sit"));
        Assert.AreEqual("Pepper does not know sit yet.", unlearnedCatalog.GetFeedbackForUnresolvedPhrase("sit"));

        sit.IsLearned = true;
        PawPalVoiceCommandCatalog learnedCatalog = PawPalVoiceCommandCatalogBuilder.Build(new[] { activeDog }, activeDog.Id);
        Assert.IsTrue(learnedCatalog.Phrases.Contains("pepper"));
        Assert.IsTrue(learnedCatalog.Phrases.Contains("pepper sit"));
        Assert.IsTrue(learnedCatalog.Phrases.Contains("sit"));
    }

    [Test]
    public void LearningATrickAssignsDefaultVoiceCommandButPreservesCustomValue()
    {
        UnityEngine.Random.InitState(72);
        PawPalDogState defaultDog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(defaultDog);
        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalTrainingConfig config = PawPalTrainingConfig.CreateRuntimeDefault();
        PawPalDogTrickProgress defaultProgress = PawPalTrickCatalog.GetOrCreateProgress(defaultDog, PawPalTrickId.Sit);

        for (int i = 0; i < 12 && !defaultProgress.IsLearned; i++)
        {
            PawPalTrickProgressionService.AttemptTrick(defaultDog, sit, config, true, false, true);
        }

        Assert.IsTrue(defaultProgress.IsLearned);
        Assert.AreEqual(PawPalTrickCatalog.GetCommandLabel(PawPalTrickId.Sit), defaultProgress.CustomVoiceCommand);

        UnityEngine.Random.InitState(72);
        PawPalDogState customDog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(customDog);
        PawPalDogTrickProgress customProgress = PawPalTrickCatalog.GetOrCreateProgress(customDog, PawPalTrickId.Sit);
        customProgress.CustomVoiceCommand = "down";

        for (int i = 0; i < 12 && !customProgress.IsLearned; i++)
        {
            PawPalTrickProgressionService.AttemptTrick(customDog, sit, config, true, false, true);
        }

        Assert.IsTrue(customProgress.IsLearned);
        Assert.AreEqual("down", customProgress.CustomVoiceCommand);
    }

    [Test]
    public void TrainingPresentationUsesOrderedLocksForFreshDog()
    {
        PawPalDogState dog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(dog);

        PawPalTrainingTrickPresentation sit = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Sit);
        PawPalTrainingTrickPresentation lie = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Lie);
        PawPalTrainingTrickPresentation shake = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Shake);

        Assert.AreEqual(PawPalTrainingTrickVisualState.Available, sit.VisualState);
        Assert.IsEmpty(sit.ActionText);
        Assert.IsFalse(sit.CanAct);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, lie.VisualState);
        Assert.AreEqual("Learn Sit first", lie.StateText);
        Assert.IsEmpty(lie.ActionText);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, shake.VisualState);
        Assert.AreEqual("Learn Lie first", shake.StateText);
    }

    [Test]
    public void TrainingPresentationShowsLearnedCommandDogAndUnlockedLie()
    {
        PawPalDogState dog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(dog);

        PawPalDogTrickProgress sitProgress = PawPalTrickCatalog.GetOrCreateProgress(dog, PawPalTrickId.Sit);
        PawPalTrickDefinition sitDefinition = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        sitProgress.IsDiscovered = true;
        sitProgress.IsLearned = true;
        sitProgress.MasteryLevel = 1;
        sitProgress.MasteryXp = sitDefinition.LearnedRequiredXp;

        PawPalTrainingTrickPresentation sit = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Sit);
        PawPalTrainingTrickPresentation lie = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Lie);
        PawPalTrainingTrickPresentation shake = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Shake);

        Assert.AreEqual(PawPalTrainingTrickVisualState.Learned, sit.VisualState);
        Assert.IsEmpty(sit.ActionText);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Available, lie.VisualState);
        Assert.IsEmpty(lie.ActionText);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, shake.VisualState);
    }

    [Test]
    public void TrainingPresentationUnlocksLaterTricksOnlyAfterThePreviousOneIsLearned()
    {
        PawPalDogState dog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(dog);

        PawPalTrickDefinition sitDefinition = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalDogTrickProgress sitProgress = PawPalTrickCatalog.GetOrCreateProgress(dog, PawPalTrickId.Sit);
        sitProgress.IsDiscovered = true;
        sitProgress.IsLearned = true;
        sitProgress.MasteryLevel = 1;
        sitProgress.MasteryXp = sitDefinition.LearnedRequiredXp;

        PawPalTrickDefinition lieDefinition = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie);
        PawPalDogTrickProgress lieProgress = PawPalTrickCatalog.GetOrCreateProgress(dog, PawPalTrickId.Lie);
        lieProgress.IsDiscovered = true;
        lieProgress.IsLearned = true;
        lieProgress.MasteryLevel = 1;
        lieProgress.MasteryXp = lieDefinition.LearnedRequiredXp;

        PawPalTrainingTrickPresentation shake = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Shake);
        PawPalTrainingTrickPresentation jump = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Jump);
        PawPalTrainingTrickPresentation spin = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Spin);

        Assert.AreEqual(PawPalTrainingTrickVisualState.Available, shake.VisualState);
        Assert.IsEmpty(shake.ActionText);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, jump.VisualState);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, spin.VisualState);
    }

    [Test]
    public void FocusRequirementLocksTrainingPresentationAndProgression()
    {
        PawPalDogState dog = BuildDog();
        dog.Focus = 3;
        PawPalTrickCatalog.EnsureDogTrickData(dog);
        MarkLearned(dog, PawPalTrickId.Sit);
        MarkLearned(dog, PawPalTrickId.Lie);

        PawPalTrainingTrickPresentation shake = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Shake);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, shake.VisualState);
        Assert.AreEqual("Focus 4 required", shake.StateText);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            dog,
            PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake),
            PawPalTrainingConfig.CreateRuntimeDefault(),
            true,
            false,
            true);

        Assert.AreEqual(PawPalTrickFailureReason.LowFocus, result.Reason);
    }

    [Test]
    public void BondLevelRequirementLocksTrainingPresentationAndProgression()
    {
        PawPalDogState dog = BuildDog();
        dog.BondXp = 0;
        dog.BondLevel = 1;
        dog.Bond01 = 0f;
        PawPalTrickCatalog.EnsureDogTrickData(dog);
        MarkLearned(dog, PawPalTrickId.Sit);

        PawPalTrainingTrickPresentation lie = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Lie);
        Assert.AreEqual(PawPalTrainingTrickVisualState.Locked, lie.VisualState);
        Assert.AreEqual("Bond L2", lie.StateText);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            dog,
            PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie),
            PawPalTrainingConfig.CreateRuntimeDefault(),
            false,
            false,
            true);

        Assert.AreEqual(PawPalTrickFailureReason.LowBond, result.Reason);
    }

    [Test]
    public void CoreTricksUseExplicitFocusThresholds()
    {
        Assert.AreEqual(0, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit)));
        Assert.AreEqual(2, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie)));
        Assert.AreEqual(4, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake)));
        Assert.AreEqual(6, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Jump)));
        Assert.AreEqual(8, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Spin)));
        Assert.AreEqual(6, PawPalTrickFocusRequirement.GetRequiredFocus(PawPalTrickCatalog.GetDefinition(PawPalTrickId.RollOver)));
    }

    [Test]
    public void CoreTricksUseExplicitBondThresholds()
    {
        Assert.AreEqual(1, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit).GetResolvedRequiredBondLevel());
        Assert.AreEqual(2, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie).GetResolvedRequiredBondLevel());
        Assert.AreEqual(3, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake).GetResolvedRequiredBondLevel());
        Assert.AreEqual(5, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Jump).GetResolvedRequiredBondLevel());
        Assert.AreEqual(6, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Spin).GetResolvedRequiredBondLevel());
    }

    [Test]
    public void LowMoodNoLongerBlocksAvailabilityButStillReducesSuccessChance()
    {
        PawPalDogState lowMoodDog = BuildDog();
        lowMoodDog.Mood01 = 0.01f;
        PawPalDogState highMoodDog = BuildDog();
        highMoodDog.Mood01 = 1f;
        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalTrainingConfig config = PawPalTrainingConfig.CreateRuntimeDefault();

        Assert.AreEqual(PawPalTrickFailureReason.None, PawPalTrickProgressionService.GetAvailabilityFailure(lowMoodDog, sit, true));

        PawPalTrickCatalog.EnsureDogTrickData(lowMoodDog);
        PawPalTrickCatalog.EnsureDogTrickData(highMoodDog);
        PawPalDogTrickProgress lowMoodProgress = PawPalTrickCatalog.GetOrCreateProgress(lowMoodDog, PawPalTrickId.Sit);
        PawPalDogTrickProgress highMoodProgress = PawPalTrickCatalog.GetOrCreateProgress(highMoodDog, PawPalTrickId.Sit);

        float lowMoodChance = InvokePrivateStatic<float>(
            typeof(PawPalTrickProgressionService),
            "CalculateSuccessChance",
            lowMoodDog,
            sit,
            lowMoodProgress,
            config,
            false);
        float highMoodChance = InvokePrivateStatic<float>(
            typeof(PawPalTrickProgressionService),
            "CalculateSuccessChance",
            highMoodDog,
            sit,
            highMoodProgress,
            config,
            false);

        Assert.Less(lowMoodChance, highMoodChance);
    }

    [Test]
    public void LaterCoreTricksRequireStrictlyMoreLearnedXpThanEarlierOnes()
    {
        Assert.Less(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit).LearnedRequiredXp, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie).LearnedRequiredXp);
        Assert.Less(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie).LearnedRequiredXp, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake).LearnedRequiredXp);
        Assert.Less(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake).LearnedRequiredXp, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Jump).LearnedRequiredXp);
        Assert.Less(PawPalTrickCatalog.GetDefinition(PawPalTrickId.Jump).LearnedRequiredXp, PawPalTrickCatalog.GetDefinition(PawPalTrickId.Spin).LearnedRequiredXp);
    }

    [Test]
    public void MissingPrerequisiteFailureNamesSpecificRequiredTrick()
    {
        PawPalDogState dog = BuildDog();
        PawPalTrickCatalog.EnsureDogTrickData(dog);

        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(
            dog,
            PawPalTrickCatalog.GetDefinition(PawPalTrickId.Shake),
            PawPalTrainingConfig.CreateRuntimeDefault(),
            false,
            false,
            true);

        Assert.AreEqual(PawPalTrickFailureReason.MissingPrerequisite, result.Reason);
        Assert.AreEqual("Learn Lie first.", result.FeedbackText);
    }

    [Test]
    public void TrainingInstructionsShowFullRequirementsForSelectedLockedTrick()
    {
        PawPalDogState dog = BuildDog();
        dog.Focus = 0;
        dog.BondXp = 0;
        dog.BondLevel = 1;
        dog.Bond01 = 0f;
        PawPalTrickCatalog.EnsureDogTrickData(dog);
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Lie);
        PawPalTrainingTrickPresentation presentation = PawPalTrainingModeView.DescribeTrick(dog, PawPalTrickId.Lie);

        string text = InvokePrivateStatic<string>(
            typeof(PawPalTrainingModeView),
            "BuildTrainingInstructionText",
            dog,
            definition,
            presentation);

        StringAssert.Contains("Requirements", text);
        StringAssert.Contains("Prerequisite: Sit", text);
        StringAssert.Contains("Focus: 2 required", text);
        StringAssert.Contains("Bond: Level 2", text);
        StringAssert.DoesNotContain("Learn Sit first.", text);
        StringAssert.DoesNotContain("Mood", text);
        StringAssert.DoesNotContain("Stamina", text);
        StringAssert.Contains("Instructions", text);
        StringAssert.Contains("Voice:", text);
        StringAssert.Contains("Touch:", text);
    }

    [Test]
    public void VoiceCatalogOnlyIncludesUnlearnedActiveDogTricksForInteractionMode()
    {
        PawPalDogState activeDog = BuildDog();
        activeDog.Id = "pepper";
        activeDog.DisplayName = "Pepper";
        PawPalTrickCatalog.EnsureDogTrickData(activeDog);

        PawPalDogTrickProgress sit = PawPalTrickCatalog.GetOrCreateProgress(activeDog, PawPalTrickId.Sit);
        sit.IsLearned = false;
        sit.CustomVoiceCommand = "sit";

        PawPalVoiceCommandCatalog normalCatalog = PawPalVoiceCommandCatalogBuilder.Build(new[] { activeDog }, activeDog.Id);
        Assert.AreEqual(PawPalResolvedVoiceCommandType.None, normalCatalog.Resolve("sit").Type);

        PawPalVoiceCommandCatalog interactionCatalog = PawPalVoiceCommandCatalogBuilder.Build(new[] { activeDog }, activeDog.Id, true);
        PawPalResolvedVoiceCommand resolved = interactionCatalog.Resolve("sit");
        Assert.AreEqual(PawPalResolvedVoiceCommandType.PerformTrick, resolved.Type);
        Assert.AreEqual(PawPalTrickId.Sit, resolved.TrickId);
    }

    [Test]
    public void DogInteractionBondChangesClampNeedsMoodAndBond()
    {
        PawPalDogState dog = BuildDog();
        dog.Bond01 = 0.995f;
        dog.BondXp = Mathf.RoundToInt(0.995f * PawPalGameRuntime.MaxBondXp);
        dog.BondLevel = PawPalGameRuntime.GetBondLevelForXp(dog.BondXp);
        dog.Mood01 = 0.997f;
        dog.Activity01 = 0.998f;

        PawPalGameRuntime.ApplyDogInteractionBond(dog, 0.25f, 0.25f, 0.25f);

        Assert.AreEqual(1f, dog.Bond01);
        Assert.AreEqual(PawPalGameRuntime.MaxBondXp, dog.BondXp);
        Assert.AreEqual(PawPalGameRuntime.MaxBondLevel, dog.BondLevel);
        Assert.AreEqual(1f, dog.Mood01);
        Assert.AreEqual(1f, dog.Activity01);
    }

    [Test]
    public void BondRewardsIncreaseXpAndCanLevelUp()
    {
        PawPalDogState dog = BuildDog();
        dog.BondXp = 195;
        dog.BondLevel = PawPalGameRuntime.GetBondLevelForXp(dog.BondXp);
        dog.Bond01 = dog.BondXp / (float)PawPalGameRuntime.MaxBondXp;

        PawPalGameRuntime.ApplyDogInteractionBond(dog, 0.012f, 0f, 0f);

        Assert.Greater(dog.BondXp, 195);
        Assert.AreEqual(3, dog.BondLevel);
    }

    [Test]
    public void DogInteractionTimeoutsUsePersonalityPresetMap()
    {
        Assert.AreEqual(24f, PawPalDogInteractionTuning.GetInactivityTimeoutSeconds(PawPalDogPersonality.Loyal));
        Assert.AreEqual(11f, PawPalDogInteractionTuning.GetInactivityTimeoutSeconds(PawPalDogPersonality.Energetic));
        Assert.AreEqual(9f, PawPalDogInteractionTuning.GetInactivityTimeoutSeconds(PawPalDogPersonality.Mischievous));
    }

    [Test]
    public void InteractionAnnoyedPersonalityMultipliersFavorImpatientDogs()
    {
        float impatientChance = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedChanceMultiplier",
            PawPalDogPersonality.Mischievous);
        float mediumChance = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedChanceMultiplier",
            PawPalDogPersonality.Loyal);
        float calmChance = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedChanceMultiplier",
            PawPalDogPersonality.Gentle);
        float impatientCooldown = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedCooldownMultiplier",
            PawPalDogPersonality.Mischievous);
        float mediumCooldown = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedCooldownMultiplier",
            PawPalDogPersonality.Loyal);
        float calmCooldown = InvokeStaticOnRuntimeType<float>(
            "PawPalDogPersonalityProfiles",
            "GetInteractionAnnoyedCooldownMultiplier",
            PawPalDogPersonality.Gentle);

        Assert.Greater(
            impatientChance,
            mediumChance);
        Assert.Greater(
            mediumChance,
            calmChance);

        Assert.Less(
            impatientCooldown,
            mediumCooldown);
        Assert.Less(
            mediumCooldown,
            calmCooldown);
    }

    [Test]
    public void NewDogWhinyHoursInitializeOnlyForGentleDogs()
    {
        PawPalDogState gentleDog = BuildDog();
        gentleDog.Personality = PawPalDogPersonality.Gentle;
        InvokePrivateStatic(typeof(PawPalGameRuntime), "InitializeNewDogWhinyState", gentleDog);
        Assert.AreEqual(3f, gentleDog.NewDogWhinyHoursRemaining, 0.0001f);

        PawPalDogState playfulDog = BuildDog();
        playfulDog.Personality = PawPalDogPersonality.Playful;
        InvokePrivateStatic(typeof(PawPalGameRuntime), "InitializeNewDogWhinyState", playfulDog);
        Assert.AreEqual(0f, playfulDog.NewDogWhinyHoursRemaining, 0.0001f);
    }

    [Test]
    public void NewDogWhinyHoursTickDownUsingGameHourScale()
    {
        GameObject gameObject = new GameObject("RuntimeWhinyTickTest");
        try
        {
            PawPalGameRuntime runtime = gameObject.AddComponent<PawPalGameRuntime>();
            PawPalDogState dog = BuildDog();
            dog.Personality = PawPalDogPersonality.Gentle;
            dog.NewDogWhinyHoursRemaining = 3f;

            SetPrivateField(runtime, "secondsPerGameHour", 180f);
            SetPrivateField(runtime, "dogs", new List<PawPalDogState> { dog });
            InvokePrivate(runtime, "TickDogNeeds", 90f);

            Assert.AreEqual(2.5f, dog.NewDogWhinyHoursRemaining, 0.0001f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void CloneDogStatePreservesNewDogWhinyHours()
    {
        PawPalDogState source = BuildDog();
        source.NewDogWhinyHoursRemaining = 1.75f;

        PawPalDogState clone = InvokePrivateStatic<PawPalDogState>(typeof(PawPalGameRuntime), "CloneDogState", source);

        Assert.NotNull(clone);
        Assert.AreEqual(1.75f, clone.NewDogWhinyHoursRemaining, 0.0001f);
    }

    [Test]
    public void InteractionFrustrationEligibilityOnlyCoversNoProgressReasons()
    {
        Assert.IsTrue(InvokePrivateStatic<bool>(
            typeof(PawPalDogInteractionModeController),
            "IsFrustrationEligibleFailureReason",
            PawPalTrickFailureReason.RandomMiss));
        Assert.IsTrue(InvokePrivateStatic<bool>(
            typeof(PawPalDogInteractionModeController),
            "IsFrustrationEligibleFailureReason",
            PawPalTrickFailureReason.LowFocus));
        Assert.IsTrue(InvokePrivateStatic<bool>(
            typeof(PawPalDogInteractionModeController),
            "IsFrustrationEligibleFailureReason",
            PawPalTrickFailureReason.MissingPrerequisite));
        Assert.IsFalse(InvokePrivateStatic<bool>(
            typeof(PawPalDogInteractionModeController),
            "IsFrustrationEligibleFailureReason",
            PawPalTrickFailureReason.Hungry));
        Assert.IsFalse(InvokePrivateStatic<bool>(
            typeof(PawPalDogInteractionModeController),
            "IsFrustrationEligibleFailureReason",
            PawPalTrickFailureReason.LowMood));
    }

    [Test]
    public void PraiseStateEnablesAfterSuccessAndResetsAfterPraiseOrTrickChange()
    {
        GameObject gameObject = new GameObject("TrainingControllerTest");
        try
        {
            PawPalTrainingController controller = gameObject.AddComponent<PawPalTrainingController>();
            controller.Open(PawPalTrickId.Sit);

            Assert.IsFalse(controller.CanPraiseSelectedTrick);

            InvokePrivate(controller, "ApplyAttemptResult", new PawPalTrickAttemptResult
            {
                TrickId = PawPalTrickId.Sit,
                Success = true,
                FeedbackText = "Pepper nailed it."
            });
            Assert.IsTrue(controller.CanPraiseSelectedTrick);

            InvokePrivate(controller, "HandleTrickSelected", PawPalTrickId.Lie);
            Assert.IsFalse(controller.CanPraiseSelectedTrick);

            InvokePrivate(controller, "ApplyAttemptResult", new PawPalTrickAttemptResult
            {
                TrickId = PawPalTrickId.Lie,
                Success = true,
                FeedbackText = "Pepper nailed it again."
            });
            Assert.IsTrue(controller.CanPraiseSelectedTrick);

            InvokePrivate(controller, "HandlePraiseRequested");
            Assert.IsFalse(controller.CanPraiseSelectedTrick);

            InvokePrivate(controller, "ApplyAttemptResult", new PawPalTrickAttemptResult
            {
                TrickId = PawPalTrickId.Lie,
                Success = false,
                FeedbackText = "Not this time."
            });
            Assert.IsFalse(controller.CanPraiseSelectedTrick);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static PawPalDogState BuildDog()
    {
        return new PawPalDogState
        {
            Id = "test_dog",
            DisplayName = "Test",
            Gender = PawPalDogGender.Female,
            Personality = PawPalDogPersonality.Clever,
            FurColor = "Black",
            Breed = "Test breed",
            // Keep the fixture out of profile migration without depending on internal runtime helpers.
            ProfileVersion = int.MaxValue,
            Food01 = 1f,
            Water01 = 1f,
            Hygiene01 = 1f,
            Activity01 = 1f,
            Energy01 = 1f,
            Bond01 = 1f,
            BondXp = PawPalGameRuntime.MaxBondXp,
            BondLevel = PawPalGameRuntime.MaxBondLevel,
            Mood01 = 1f,
            Focus = 10,
            Tricks = new List<PawPalDogTrickProgress>()
        };
    }

    private static int CountProgress(PawPalDogState dog, PawPalTrickId trickId)
    {
        int count = 0;
        for (int i = 0; i < dog.Tricks.Count; i++)
        {
            if (dog.Tricks[i] != null && dog.Tricks[i].ParsedTrickId == trickId)
            {
                count++;
            }
        }

        return count;
    }

    private static void MarkLearned(PawPalDogState dog, PawPalTrickId trickId)
    {
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(dog, trickId);
        progress.IsDiscovered = true;
        progress.IsLearned = true;
        progress.MasteryLevel = 1;
        progress.MasteryXp = definition != null ? definition.LearnedRequiredXp : 100f;
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, "Missing method " + methodName);
        method.Invoke(target, arguments);
    }

    private static TResult InvokePrivateStatic<TResult>(Type targetType, string methodName, params object[] arguments)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method, "Missing static method " + methodName);
        object result = method.Invoke(null, arguments);
        return result is TResult typedResult ? typedResult : default(TResult);
    }

    private static void InvokePrivateStatic(Type targetType, string methodName, params object[] arguments)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method, "Missing static method " + methodName);
        method.Invoke(null, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, "Missing field " + fieldName);
        field.SetValue(target, value);
    }

    private static TResult InvokeStaticOnRuntimeType<TResult>(string typeName, string methodName, params object[] arguments)
    {
        Type targetType = typeof(PawPalGameRuntime).Assembly.GetType(typeName);
        Assert.NotNull(targetType, "Missing type " + typeName);
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method, "Missing static method " + methodName);
        object result = method.Invoke(null, arguments);
        return result is TResult typedResult ? typedResult : default(TResult);
    }

    private static void Run(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
        }
    }
}
