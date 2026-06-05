using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public sealed class PawPalPetMovementProfilesEditModeTests
{
    [TestCase("puppy_labrador", PawPalPetSizeClass.Small)]
    [TestCase("kitten_simple", PawPalPetSizeClass.Small)]
    [TestCase("chihuahua", PawPalPetSizeClass.Small)]
    [TestCase("jackrussellterrier", PawPalPetSizeClass.Small)]
    [TestCase("toyterrier", PawPalPetSizeClass.Small)]
    [TestCase("cat_simple", PawPalPetSizeClass.Medium)]
    [TestCase("cat_chubby", PawPalPetSizeClass.Medium)]
    [TestCase("cat_stray", PawPalPetSizeClass.Medium)]
    [TestCase("pug", PawPalPetSizeClass.Medium)]
    [TestCase("corgi", PawPalPetSizeClass.Medium)]
    [TestCase("beagle", PawPalPetSizeClass.Medium)]
    [TestCase("bullterrier", PawPalPetSizeClass.Medium)]
    [TestCase("frenchbulldog", PawPalPetSizeClass.Medium)]
    [TestCase("shibainu", PawPalPetSizeClass.Medium)]
    [TestCase("spitz", PawPalPetSizeClass.Medium)]
    [TestCase("cur", PawPalPetSizeClass.Medium)]
    [TestCase("border_collie", PawPalPetSizeClass.Large)]
    [TestCase("boxer", PawPalPetSizeClass.Large)]
    [TestCase("dalmatian", PawPalPetSizeClass.Large)]
    [TestCase("doberman", PawPalPetSizeClass.Large)]
    [TestCase("goldenretriever", PawPalPetSizeClass.Large)]
    [TestCase("husky", PawPalPetSizeClass.Large)]
    [TestCase("labrador", PawPalPetSizeClass.Large)]
    [TestCase("pitbull", PawPalPetSizeClass.Large)]
    [TestCase("rottweiler", PawPalPetSizeClass.Large)]
    [TestCase("shepherd", PawPalPetSizeClass.Large)]
    [TestCase("germanshepherd", PawPalPetSizeClass.Large)]
    public void ExplicitBreedKeysResolveToExpectedSizeClass(string breedKey, PawPalPetSizeClass expectedSizeClass)
    {
        PawPalPetMovementProfile profile = PawPalPetMovementProfiles.Resolve(null, breedKey, null, null);

        Assert.AreEqual(expectedSizeClass, profile.SizeClass);
    }

    [Test]
    public void AliasNormalizationAndKeywordFallbacksResolveAsExpected()
    {
        Assert.AreEqual(PawPalPetSizeClass.Small, PawPalPetMovementProfiles.Resolve(null, "Kitten Simple", null, null).SizeClass);
        Assert.AreEqual(PawPalPetSizeClass.Small, PawPalPetMovementProfiles.Resolve(null, null, null, "Puppy-Labrador").SizeClass);
        Assert.AreEqual(PawPalPetSizeClass.Large, PawPalPetMovementProfiles.Resolve(null, "German Shepherd", null, null).SizeClass);
        Assert.AreEqual(PawPalPetSizeClass.Medium, PawPalPetMovementProfiles.Resolve(null, "Unknown Breed", null, null).SizeClass);
    }

    [Test]
    public void SizeProfilesExposeExpectedSpeedValues()
    {
        PawPalPetMovementProfile small = PawPalPetMovementProfiles.Resolve("puppy_labrador", null, null, null);
        PawPalPetMovementProfile medium = PawPalPetMovementProfiles.Resolve("cat_simple", null, null, null);
        PawPalPetMovementProfile large = PawPalPetMovementProfiles.Resolve("husky", null, null, null);

        Assert.AreEqual(0.24f, small.WalkSpeed, 0.0001f);
        Assert.AreEqual(0.78f, small.TrotSpeed, 0.0001f);
        Assert.AreEqual(2.34f, small.RunSpeed, 0.0001f);

        Assert.AreEqual(0.52f, medium.WalkSpeed, 0.0001f);
        Assert.AreEqual(0.98f, medium.TrotSpeed, 0.0001f);
        Assert.AreEqual(2.94f, medium.RunSpeed, 0.0001f);

        Assert.AreEqual(0.66f, large.WalkSpeed, 0.0001f);
        Assert.AreEqual(1.18f, large.TrotSpeed, 0.0001f);
        Assert.AreEqual(3.54f, large.RunSpeed, 0.0001f);
    }

    [Test]
    public void RunSpeedIsTripleTrotSpeedForEverySizeClass()
    {
        PawPalPetMovementProfile small = PawPalPetMovementProfiles.Resolve("puppy_labrador", null, null, null);
        PawPalPetMovementProfile medium = PawPalPetMovementProfiles.Resolve("cat_simple", null, null, null);
        PawPalPetMovementProfile large = PawPalPetMovementProfiles.Resolve("husky", null, null, null);

        Assert.AreEqual(small.TrotSpeed * 3f, small.RunSpeed, 0.0001f);
        Assert.AreEqual(medium.TrotSpeed * 3f, medium.RunSpeed, 0.0001f);
        Assert.AreEqual(large.TrotSpeed * 3f, large.RunSpeed, 0.0001f);
    }

    [Test]
    public void CatProfileExposesExpectedAnimatorSpeedValues()
    {
        PawPalPetMovementProfile medium = PawPalPetMovementProfiles.Resolve("cat_simple", null, null, null);

        Assert.AreEqual(PawPalPetSizeClass.Medium, medium.SizeClass);
        Assert.AreEqual(0.5f, medium.WalkAnimatorSpeed, 0.0001f);
        Assert.AreEqual(0.78f, medium.TrotAnimatorSpeed, 0.0001f);
        Assert.AreEqual(1f, medium.RunAnimatorSpeed, 0.0001f);
    }

    [Test]
    public void AnimatorSpeedProfilesStayFixedAcrossSizeClasses()
    {
        PawPalPetMovementProfile small = PawPalPetMovementProfiles.Resolve("puppy_labrador", null, null, null);
        PawPalPetMovementProfile medium = PawPalPetMovementProfiles.Resolve("cat_simple", null, null, null);
        PawPalPetMovementProfile large = PawPalPetMovementProfiles.Resolve("husky", null, null, null);

        Assert.AreEqual(0.5f, small.WalkAnimatorSpeed, 0.0001f);
        Assert.AreEqual(0.78f, small.TrotAnimatorSpeed, 0.0001f);
        Assert.AreEqual(1f, small.RunAnimatorSpeed, 0.0001f);

        Assert.AreEqual(0.5f, medium.WalkAnimatorSpeed, 0.0001f);
        Assert.AreEqual(0.78f, medium.TrotAnimatorSpeed, 0.0001f);
        Assert.AreEqual(1f, medium.RunAnimatorSpeed, 0.0001f);

        Assert.AreEqual(0.5f, large.WalkAnimatorSpeed, 0.0001f);
        Assert.AreEqual(0.78f, large.TrotAnimatorSpeed, 0.0001f);
        Assert.AreEqual(1f, large.RunAnimatorSpeed, 0.0001f);
    }

    [Test]
    public void NearbyHeadAttentionEligibilityRejectsSuppressedPetStates()
    {
        Assert.IsTrue(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, false, false, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(false, true, false, false, false, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, false, false, false, false, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, true, false, false, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, true, false, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, false, true, false, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, false, false, true, false, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, false, false, false, true, false));
        Assert.IsFalse(PawPalRoomPetRuntime.IsNearbyHeadAttentionStateEligible(true, true, false, false, false, false, false, true));
    }

    [Test]
    public void NearbyHeadTiltAngleSelectionStaysWithinSignedRange()
    {
        Assert.AreEqual(-20f, PawPalRoomPetRuntime.ResolveSignedHeadTiltAngle(20f, 30f, 0f, false), 0.0001f);
        Assert.AreEqual(30f, PawPalRoomPetRuntime.ResolveSignedHeadTiltAngle(20f, 30f, 1f, true), 0.0001f);
        Assert.AreEqual(25f, PawPalRoomPetRuntime.ResolveSignedHeadTiltAngle(30f, 20f, 0.5f, true), 0.0001f);
        Assert.AreEqual(-30f, PawPalRoomPetRuntime.ResolveSignedHeadTiltAngle(20f, 30f, 2f, false), 0.0001f);
    }

    [Test]
    public void NearbyHeadTiltCooldownRequiresAllowedTime()
    {
        Assert.IsFalse(PawPalRoomPetRuntime.IsHeadTiltCooldownReady(4.99f, 5f));
        Assert.IsTrue(PawPalRoomPetRuntime.IsHeadTiltCooldownReady(5f, 5f));
        Assert.IsTrue(PawPalRoomPetRuntime.IsHeadTiltCooldownReady(6f, 5f));
    }

    [Test]
    public void CatRoomAgentUsesResolvedMovementProfileForPaceAndKeepsAuthoredPlaybackSpeed()
    {
        GameObject root = new GameObject("CatAgentTest");
        NavMeshAgent navMeshAgent = root.AddComponent<NavMeshAgent>();
        Animator animator = root.AddComponent<Animator>();
        PawPalCatRoomAgent agent = root.AddComponent<PawPalCatRoomAgent>();
        PetAnimationSet animationSet = ScriptableObject.CreateInstance<PetAnimationSet>();

        PawPalPetMovementProfile catProfile = PawPalPetMovementProfiles.Resolve("cat_simple", null, null, null);
        SetPrivateField(agent, "movementProfile", catProfile);
        SetPrivateField(agent, "hasMovementProfile", true);
        SetPrivateField(agent, "agent", navMeshAgent);
        SetPrivateField(agent, "animator", animator);
        SetPrivateField(agent, "animationSet", animationSet);

        InvokePrivate(agent, "SetMovePace", DogMovementPace.Run);
        InvokePrivate(agent, "SetMoving", true);

        float currentMoveSpeed = (float)GetPrivateField(agent, "currentMoveSpeed");
        Assert.AreEqual(catProfile.RunSpeed, currentMoveSpeed, 0.0001f);
        Assert.AreEqual(catProfile.RunSpeed, navMeshAgent.speed, 0.0001f);
        Assert.AreEqual(1f, animator.speed, 0.0001f);

        Object.DestroyImmediate(animationSet);
        Object.DestroyImmediate(root);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(target, arguments);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }
}
