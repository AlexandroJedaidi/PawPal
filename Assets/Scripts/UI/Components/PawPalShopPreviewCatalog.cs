using System;
using System.Collections.Generic;

public static class PawPalShopPreviewCatalog
{
    private sealed class PreviewDescriptor
    {
        public PawPalShopPreviewMode Mode;
        public string ResourcePath;

        public PreviewDescriptor(PawPalShopPreviewMode mode, string resourcePath)
        {
            Mode = mode;
            ResourcePath = resourcePath ?? string.Empty;
        }
    }

    private static readonly Dictionary<string, PreviewDescriptor> Descriptors = new Dictionary<string, PreviewDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        { "toy_bone_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, "PawPal/RoomPrefabs/Bone_1") },
        { "toy_bone_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_ball_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_ball_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_ball_3", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_big_ball_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, "PawPal/RoomPrefabs/Big_ball_1") },
        { "toy_big_ball_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_big_ball_3", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "toy_big_ball_4", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_3", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_4", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_5", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_6", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cat_ball_7", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cattoy", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "mouse_c1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "mouse_c2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "collar_simple_c1", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, "PawPal/Accessories/CollarSimple_C1") },
        { "collar_simple_c2", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, "PawPal/Accessories/CollarSimple_C2") },
        { "collar_simple_c3", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, "PawPal/Accessories/CollarSimple_C3") },
        { "collar_standard", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, "PawPal/Accessories/CollarSimple_C1") },
        { "collar_c1", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "collar_c2", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "collar_c3", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "collarbow_c1", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "collarbow_c2", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "collarbow_c3", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "ears_1", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "ears_2", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "ears_3", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "ears_4", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "straycollar_1", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "straycollar_2", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "straycollar_3", new PreviewDescriptor(PawPalShopPreviewMode.WearableOnDog, string.Empty) },
        { "cardboardbox", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cattray", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "carrier_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "carrier_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cathouse_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "cathouse_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "scratchingpost_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "scratchingpost_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "catsofa", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "doblebowl", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bowl_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bowl_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bowlauto", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bed_1_color_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bed_1_color_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bed_2_color_1", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) },
        { "bed_2_color_2", new PreviewDescriptor(PawPalShopPreviewMode.StandaloneModel, string.Empty) }
    };

    public static void ApplyPreviewDefaults(PawPalCatalogItemDefinition item)
    {
        PreviewDescriptor descriptor;
        if (item == null || string.IsNullOrEmpty(item.Id) || !Descriptors.TryGetValue(item.Id, out descriptor) || descriptor == null)
        {
            return;
        }

        item.PreviewMode = descriptor.Mode;
        if (string.IsNullOrEmpty(item.PreviewPrefabResourcePath) && !string.IsNullOrEmpty(descriptor.ResourcePath))
        {
            item.PreviewPrefabResourcePath = descriptor.ResourcePath;
        }

        if (item.PreviewMode == PawPalShopPreviewMode.StandaloneModel && string.IsNullOrEmpty(item.RoomPrefabResourcePath))
        {
            item.RoomPrefabResourcePath = descriptor.ResourcePath;
        }

        if (item.PreviewMode == PawPalShopPreviewMode.WearableOnDog && string.IsNullOrEmpty(item.CollarPrefabResourcePath))
        {
            item.CollarPrefabResourcePath = descriptor.ResourcePath;
        }
    }
}
