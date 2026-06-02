using NUnit.Framework;

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
        PawPalPetMovementProfile medium = PawPalPetMovementProfiles.Resolve("corgi", null, null, null);
        PawPalPetMovementProfile large = PawPalPetMovementProfiles.Resolve("husky", null, null, null);

        Assert.AreEqual(0.50f, small.WalkSpeed, 0.0001f);
        Assert.AreEqual(0.78f, small.TrotSpeed, 0.0001f);
        Assert.AreEqual(1.05f, small.RunSpeed, 0.0001f);

        Assert.AreEqual(0.65f, medium.WalkSpeed, 0.0001f);
        Assert.AreEqual(0.98f, medium.TrotSpeed, 0.0001f);
        Assert.AreEqual(1.30f, medium.RunSpeed, 0.0001f);

        Assert.AreEqual(0.80f, large.WalkSpeed, 0.0001f);
        Assert.AreEqual(1.18f, large.TrotSpeed, 0.0001f);
        Assert.AreEqual(1.55f, large.RunSpeed, 0.0001f);
    }
}
