using UnityEngine;

internal static class PetAnimationControllerResolver
{
    private const string CatAnimationSetResourcePath = "PawPal/IntroPets/Animations/CatIntroAnimationSet";
    private const string DogAnimationSetResourcePath = "PawPal/IntroPets/Animations/DogIntroAnimationSet";

    public static RuntimeAnimatorController Resolve(
        RuntimeAnimatorController configuredController,
        Animator animator,
        IntroPetDefinition definition)
    {
        RuntimeAnimatorController baseController = configuredController;
        if (baseController == null)
        {
            baseController = ResolveSpeciesFallbackController(definition);
        }

#if UNITY_EDITOR
        RuntimeAnimatorController overrideController = ResolveEditorOverride(baseController, animator, definition);
        if (overrideController != null)
        {
            return overrideController;
        }
#endif

        return baseController;
    }

    private static RuntimeAnimatorController ResolveSpeciesFallbackController(IntroPetDefinition definition)
    {
        string resourcePath = definition != null && definition.Species == IntroPetSpecies.Cat
            ? CatAnimationSetResourcePath
            : DogAnimationSetResourcePath;
        PetAnimationSet animationSet = Resources.Load<PetAnimationSet>(resourcePath);
        return animationSet != null ? animationSet.RuntimeController : null;
    }

#if UNITY_EDITOR
    private static RuntimeAnimatorController ResolveEditorOverride(
        RuntimeAnimatorController baseController,
        Animator animator,
        IntroPetDefinition definition)
    {
        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntry(animator, definition, null, animator != null ? animator.name : null, out entry))
        {
            return null;
        }

        RuntimeAnimatorController resolvedBaseController = baseController;
        if (resolvedBaseController == null)
        {
            resolvedBaseController = PawPalPetAnimationRegistry.ResolveEditorBaseController(entry);
        }

        return PawPalPetAnimationRegistry.ResolveEditorOverrideController(resolvedBaseController, entry);
    }
#endif
}
