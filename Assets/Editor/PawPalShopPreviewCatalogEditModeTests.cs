using NUnit.Framework;
using UnityEngine;

public sealed class PawPalShopPreviewCatalogEditModeTests
{
    [Test]
    public void ApplyPreviewDefaultsMarksFurnitureAsStandaloneModel()
    {
        PawPalCatalogItemDefinition item = new PawPalCatalogItemDefinition
        {
            Id = "bed_1_color_1",
            PreviewMode = PawPalShopPreviewMode.SpriteOnly
        };

        PawPalShopPreviewCatalog.ApplyPreviewDefaults(item);

        Assert.AreEqual(PawPalShopPreviewMode.StandaloneModel, item.PreviewMode);
    }

    [Test]
    public void ApplyPreviewDefaultsMarksAccessoryAsWearable()
    {
        PawPalCatalogItemDefinition item = new PawPalCatalogItemDefinition
        {
            Id = "collarbow_c2",
            PreviewMode = PawPalShopPreviewMode.SpriteOnly
        };

        PawPalShopPreviewCatalog.ApplyPreviewDefaults(item);

        Assert.AreEqual(PawPalShopPreviewMode.WearableOnDog, item.PreviewMode);
    }

    [Test]
    public void ApplyPreviewDefaultsAssignsKnownResourcePrefabWhenAvailable()
    {
        PawPalCatalogItemDefinition item = new PawPalCatalogItemDefinition
        {
            Id = "toy_bone_1",
            PreviewMode = PawPalShopPreviewMode.SpriteOnly
        };

        PawPalShopPreviewCatalog.ApplyPreviewDefaults(item);

        Assert.AreEqual("PawPal/RoomPrefabs/Bone_1", item.PreviewPrefabResourcePath);
        Assert.AreEqual("PawPal/RoomPrefabs/Bone_1", item.RoomPrefabResourcePath);
    }

    [Test]
    public void BreedPreviewLibraryResolvesRuntimeDefinition()
    {
        PawPalCatalogItemDefinition item = new PawPalCatalogItemDefinition
        {
            Id = "dog_husky",
            DisplayName = "Husky"
        };

        PawPalBreedShopPreviewEntry entry;
        bool resolved = PawPalBreedShopPreviewLibrary.TryResolveEntry(item, out entry);

        Assert.IsTrue(resolved);
        Assert.NotNull(entry);
        Assert.NotNull(entry.Definition);
        Assert.NotNull(entry.Definition.BasePrefab);
    }
}
