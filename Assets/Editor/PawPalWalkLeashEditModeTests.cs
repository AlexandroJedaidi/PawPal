using NUnit.Framework;
using UnityEngine;

public sealed class PawPalWalkLeashEditModeTests
{
    [Test]
    public void ClassifierReturnsRunForwardForAbruptRouteAlignedDrag()
    {
        PawPalWalkLeashGestureSettings settings = new PawPalWalkLeashGestureSettings();

        PawPalWalkLeashGestureType gesture = PawPalWalkLeashGestureClassifier.Classify(
            new Vector3(0.2f, 0f, 0f),
            new Vector3(3f, 0f, 0f),
            Vector3.right,
            settings);

        Assert.AreEqual(PawPalWalkLeashGestureType.RunForward, gesture);
    }

    [Test]
    public void ClassifierReturnsStopBackwardForAbruptOppositeDrag()
    {
        PawPalWalkLeashGestureSettings settings = new PawPalWalkLeashGestureSettings();

        PawPalWalkLeashGestureType gesture = PawPalWalkLeashGestureClassifier.Classify(
            new Vector3(-0.2f, 0f, 0f),
            new Vector3(-3f, 0f, 0f),
            Vector3.right,
            settings);

        Assert.AreEqual(PawPalWalkLeashGestureType.StopBackward, gesture);
    }

    [Test]
    public void ClassifierReturnsJumpForAbruptUpwardDrag()
    {
        PawPalWalkLeashGestureSettings settings = new PawPalWalkLeashGestureSettings();

        PawPalWalkLeashGestureType gesture = PawPalWalkLeashGestureClassifier.Classify(
            new Vector3(0.02f, 0.2f, 0f),
            new Vector3(0.2f, 3f, 0f),
            Vector3.right,
            settings);

        Assert.AreEqual(PawPalWalkLeashGestureType.JumpUp, gesture);
    }

    [Test]
    public void ClassifierIgnoresSlowDrag()
    {
        PawPalWalkLeashGestureSettings settings = new PawPalWalkLeashGestureSettings();

        PawPalWalkLeashGestureType gesture = PawPalWalkLeashGestureClassifier.Classify(
            new Vector3(0.2f, 0f, 0f),
            new Vector3(0.1f, 0f, 0f),
            Vector3.right,
            settings);

        Assert.AreEqual(PawPalWalkLeashGestureType.None, gesture);
    }

    [Test]
    public void CooldownPreventsRepeatedReaction()
    {
        Assert.IsFalse(PawPalWalkLeashGestureClassifier.IsReactionAllowed(1f, 2f));
        Assert.IsTrue(PawPalWalkLeashGestureClassifier.IsReactionAllowed(2f, 2f));
    }

    [Test]
    public void ActivePetDefinitionResolverHonorsDogAndCatSpecies()
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        IntroPetDefinition dogDefinition = FindDefinition(definitions, IntroPetSpecies.Dog);
        IntroPetDefinition catDefinition = FindDefinition(definitions, IntroPetSpecies.Cat);

        Assert.NotNull(dogDefinition, "Expected at least one dog IntroPetDefinition in Resources.");
        Assert.NotNull(catDefinition, "Expected at least one cat IntroPetDefinition in Resources.");

        PawPalDogState dogState = BuildDogState(dogDefinition);
        PawPalDogState catState = BuildDogState(catDefinition);

        IntroPetDefinition resolvedDog = PawPalWalkSceneController.ResolveIntroPetDefinition(dogState, IntroPetSpecies.Dog);
        IntroPetDefinition resolvedCat = PawPalWalkSceneController.ResolveIntroPetDefinition(catState, IntroPetSpecies.Cat);

        Assert.NotNull(resolvedDog);
        Assert.NotNull(resolvedCat);
        Assert.AreEqual(IntroPetSpecies.Dog, resolvedDog.Species);
        Assert.AreEqual(IntroPetSpecies.Cat, resolvedCat.Species);
    }

    [Test]
    public void CatLocomotionStateNamesUseWalkTrotRunAndNotRunFast()
    {
        string[] walkStates = PawPalWalkPetAnimationPlayer.GetLocomotionStateNames(IntroPetSpecies.Cat, DogMovementPace.Walk);
        string[] trotStates = PawPalWalkPetAnimationPlayer.GetLocomotionStateNames(IntroPetSpecies.Cat, DogMovementPace.Trot);
        string[] runStates = PawPalWalkPetAnimationPlayer.GetLocomotionStateNames(IntroPetSpecies.Cat, DogMovementPace.Run);

        Assert.AreEqual("CatSimple_Walk_F_RM", walkStates[0]);
        Assert.AreEqual("CatSimple_Trot_F_RM", trotStates[0]);
        Assert.AreEqual("CatSimple_Run_F_RM", runStates[0]);

        for (int i = 0; i < runStates.Length; i++)
        {
            StringAssert.DoesNotContain("RunFast", runStates[i]);
        }
    }

    [Test]
    public void CatJumpReactionStateNamesStayOnCatSpecificJumpPlaceClip()
    {
        string[] catJumpStates = PawPalWalkPetAnimationPlayer.GetJumpReactionStateNames(IntroPetSpecies.Cat);

        CollectionAssert.AreEqual(
            new[] { "CatSimple_JumpPlace_RM", "Arm_Cat|JumpPlace_RM" },
            catJumpStates);
    }

    private static IntroPetDefinition FindDefinition(IntroPetDefinition[] definitions, IntroPetSpecies species)
    {
        if (definitions == null)
        {
            return null;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition != null && definition.Species == species)
            {
                return definition;
            }
        }

        return null;
    }

    private static PawPalDogState BuildDogState(IntroPetDefinition definition)
    {
        return new PawPalDogState
        {
            Id = definition != null ? definition.PetId : string.Empty,
            Breed = definition != null ? definition.BreedLabel : string.Empty,
            DisplayName = definition != null ? definition.DisplayName : "Pet"
        };
    }
}
