using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Reflection;

public sealed class PawPalIntroPetDefinitionsEditModeTests
{
    [TestCase("kitten_simple")]
    [TestCase("cat_stray")]
    public void RequiredCatSelectorDefinitionsExist(string petId)
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        IntroPetDefinition definition = FindDefinition(definitions, petId);

        Assert.NotNull(definition, "Missing intro definition for pet id '" + petId + "'.");
        Assert.AreEqual(IntroPetSpecies.Cat, definition.Species);
        Assert.NotNull(definition.BasePrefab, "Expected a prefab or model root for '" + petId + "'.");
        Assert.NotNull(definition.AnimationSet, "Expected a cat animation set for '" + petId + "'.");
        string prefabPath = AssetDatabase.GetAssetPath(definition.BasePrefab);
        StringAssert.DoesNotContain("LowPoly", prefabPath);
        StringAssert.DoesNotContain("_LOD", prefabPath);
    }

    [Test]
    public void StrayCatDefinitionUsesStandardFullDetailPrefab()
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        IntroPetDefinition stray = FindDefinition(definitions, "cat_stray");

        Assert.NotNull(stray);
        Assert.NotNull(stray.BasePrefab);

        string prefabPath = AssetDatabase.GetAssetPath(stray.BasePrefab);
        StringAssert.EndsWith("CatStray_C4.prefab", prefabPath);
        StringAssert.DoesNotContain("NoAlpha", prefabPath);
    }

    [Test]
    public void CatAnimationSetUsesCatBaseController()
    {
        PetAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<PetAnimationSet>(
            "Assets/Resources/PawPal/IntroPets/Animations/CatIntroAnimationSet.asset");

        Assert.NotNull(animationSet);
        Assert.NotNull(animationSet.RuntimeController);
        StringAssert.EndsWith(
            "Assets/Animations/Cat_BaseController.controller",
            AssetDatabase.GetAssetPath(animationSet.RuntimeController));
    }

    [Test]
    public void CatBaseControllerContainsRequiredCatStates()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/Cat_BaseController.controller");

        Assert.NotNull(controller);
        Assert.AreEqual("Cat_BaseController", controller.name);
        Assert.NotNull(controller.layers);
        Assert.IsNotEmpty(controller.layers);
        Assert.NotNull(controller.layers[0].stateMachine);

        string[] requiredStateNames =
        {
            "Idle1",
            "Idle2",
            "CatSimple_Walk_F_RM",
            "CatSimple_Trot_F_RM",
            "CatSimple_Run_F_RM",
            "CatSimple_JumpPlace_RM",
            "CatSimple_Sit_start",
            "CatSimple_Sit_loop_1",
            "CatSimple_Sit_end",
            "CatSimple_Lie_belly_loop_1",
            "CatSimple_Scratching"
        };

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int i = 0; i < requiredStateNames.Length; i++)
        {
            Assert.IsTrue(ContainsState(states, requiredStateNames[i]), "Missing cat state '" + requiredStateNames[i] + "'.");
        }

        Assert.IsFalse(ContainsState(states, "Locomotion"), "Cat controller should not keep the generic Locomotion blend tree state.");
    }

    [TestCase(DogMovementPace.Walk, "CatSimple_Walk_F_RM")]
    [TestCase(DogMovementPace.Trot, "CatSimple_Trot_F_RM")]
    [TestCase(DogMovementPace.Run, "CatSimple_Run_F_RM")]
    public void CatWalkHelperPrefersExplicitCatLocomotionStates(DogMovementPace pace, string expectedPrimaryState)
    {
        string[] stateNames = PawPalWalkPetAnimationPlayer.GetLocomotionStateNames(IntroPetSpecies.Cat, pace);

        Assert.NotNull(stateNames);
        Assert.IsNotEmpty(stateNames);
        Assert.AreEqual(expectedPrimaryState, stateNames[0]);
        CollectionAssert.DoesNotContain(stateNames, "Locomotion");
    }

    [TestCase("kitten_simple", "KittenSimple_anim_IP.fbx")]
    [TestCase("cat_simple", "Cat_Simple_anim_IP.fbx")]
    [TestCase("cat_stray", "CatStray_anim_IP.fbx")]
    [TestCase("cat_chubby", "CatFat_anim_IP.fbx")]
    public void CatRegistryUsesFullDetailModelSources(string petId, string expectedModelAssetName)
    {
        PawPalPetAnimationEntry entry = FindEntry(petId);

        Assert.NotNull(entry);
        StringAssert.EndsWith(expectedModelAssetName, entry.ModelAssetPath);
        StringAssert.DoesNotContain("LowPoly", entry.ModelAssetPath);
        StringAssert.DoesNotContain("_LOD", entry.ModelAssetPath);
    }

    [Test]
    public void IntroSelectorRosterBuildsRealCatDefinitionsWithCTypeVariants()
    {
        IntroPetDefinition[] sourceDefinitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        MethodInfo buildRosterMethod = typeof(IntroPetSelectionController).GetMethod(
            "BuildSpawnRoster",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(buildRosterMethod);

        IntroPetDefinition[] roster = buildRosterMethod.Invoke(null, new object[] { sourceDefinitions }) as IntroPetDefinition[];
        Assert.NotNull(roster);

        string[] expectedPetIds =
        {
            "kitten_simple",
            "cat_simple",
            "cat_chubby",
            "cat_stray"
        };

        int[] expectedVariantCounts =
        {
            5,
            6,
            5,
            5
        };

        for (int i = 0; i < expectedPetIds.Length; i++)
        {
            IntroPetDefinition definition = FindDefinition(roster, expectedPetIds[i]);
            Assert.NotNull(definition);
            Assert.AreEqual(IntroPetSpecies.Cat, definition.Species);
            Assert.NotNull(definition.BasePrefab);
            Assert.NotNull(definition.AnimationSet);
            Assert.NotNull(definition.FurVariants);
            Assert.AreEqual(expectedVariantCounts[i], definition.FurVariants.Length);
            Assert.NotNull(definition.FurVariants[0]);
            Assert.AreEqual("1", definition.FurVariants[0].SafeDisplayName);
            Assert.NotNull(definition.FurVariants[0].VariantPrefab);
        }
    }

    private static IntroPetDefinition FindDefinition(IntroPetDefinition[] definitions, string petId)
    {
        if (definitions == null)
        {
            return null;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition != null && definition.PetId == petId)
            {
                return definition;
            }
        }

        return null;
    }

    private static bool ContainsState(ChildAnimatorState[] states, string stateName)
    {
        if (states == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName)
            {
                return true;
            }
        }

        return false;
    }

    private static PawPalPetAnimationEntry FindEntry(string petId)
    {
        foreach (PawPalPetAnimationEntry entry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            if (entry != null && entry.CanonicalKey == petId)
            {
                return entry;
            }
        }

        return null;
    }
}
