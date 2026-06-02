using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class PetAnimationControllerResolver
{
    private const string DogAnimationSetResourcePath = "PawPal/IntroPets/Animations/DogIntroAnimationSet";

#if UNITY_EDITOR
    private struct BreedAnimationSpec
    {
        public BreedAnimationSpec(string key, string animationAssetPath)
        {
            Key = key;
            AnimationAssetPath = animationAssetPath;
        }

        public string Key { get; }
        public string AnimationAssetPath { get; }
    }

    private static readonly BreedAnimationSpec[] BreedAnimationSpecs =
    {
        new BreedAnimationSpec("corgi", "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Corgi/Dog/FBX/Anim/Corgi_anim_IP.fbx"),
        new BreedAnimationSpec("husky", "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Husky/Dog/FBX/Anim/Husky_anim_IP.fbx"),
        new BreedAnimationSpec("labrador", "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Labrador/Dog/FBX/Anim/Labrador_anim_IP.fbx"),
        new BreedAnimationSpec("pug", "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Pug/Dog/FBX/Anim/Pug_anim_IP.fbx"),
        new BreedAnimationSpec("cat_simple", "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Simple/Cat/FBX/Anim/Cat_Simple_anim_RM.fbx"),
        new BreedAnimationSpec("cat_chubby", "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Fat/CatFat/FBX/Anim/CatFat_anim_RM.fbx")
    };

    private static readonly Dictionary<string, RuntimeAnimatorController> OverrideCache = new Dictionary<string, RuntimeAnimatorController>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, AnimationClip>> ClipCache = new Dictionary<string, Dictionary<string, AnimationClip>>(StringComparer.OrdinalIgnoreCase);
#endif

    public static RuntimeAnimatorController Resolve(
        RuntimeAnimatorController configuredController,
        Animator animator,
        IntroPetDefinition definition)
    {
        RuntimeAnimatorController baseController = configuredController;
        if (baseController == null && definition != null && definition.Species == IntroPetSpecies.Cat)
        {
            PetAnimationSet dogAnimationSet = Resources.Load<PetAnimationSet>(DogAnimationSetResourcePath);
            if (dogAnimationSet != null)
            {
                baseController = dogAnimationSet.RuntimeController;
            }
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

#if UNITY_EDITOR
    private static RuntimeAnimatorController ResolveEditorOverride(
        RuntimeAnimatorController baseController,
        Animator animator,
        IntroPetDefinition definition)
    {
        if (baseController == null || animator == null)
        {
            return null;
        }

        BreedAnimationSpec spec;
        if (!TryResolveSpec(animator, definition, out spec))
        {
            return null;
        }

        string cacheKey = baseController.GetInstanceID() + "|" + spec.Key;
        RuntimeAnimatorController cachedController;
        if (OverrideCache.TryGetValue(cacheKey, out cachedController) && cachedController != null)
        {
            return cachedController;
        }

        Dictionary<string, AnimationClip> replacementClips = GetReplacementClips(spec);
        if (replacementClips == null || replacementClips.Count == 0)
        {
            return null;
        }

        AnimatorOverrideController overrideController = new AnimatorOverrideController(baseController);
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        bool changed = false;
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            AnimationClip replacement;
            if (original == null || !TryFindReplacementClip(original.name, replacementClips, out replacement) || replacement == null)
            {
                continue;
            }

            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            changed = true;
        }

        if (!changed)
        {
            return null;
        }

        overrideController.ApplyOverrides(overrides);
        overrideController.name = baseController.name + "_" + spec.Key + "_RuntimeOverride";
        OverrideCache[cacheKey] = overrideController;
        return overrideController;
    }

    private static Dictionary<string, AnimationClip> GetReplacementClips(BreedAnimationSpec spec)
    {
        Dictionary<string, AnimationClip> cachedClips;
        if (ClipCache.TryGetValue(spec.Key, out cachedClips))
        {
            return cachedClips;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(spec.AnimationAssetPath);
        if (assets == null || assets.Length == 0)
        {
            return null;
        }

        Dictionary<string, AnimationClip> clipsByKey = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || string.IsNullOrWhiteSpace(clip.name) || clip.name.StartsWith("__preview", StringComparison.Ordinal))
            {
                continue;
            }

            AddClipKey(clipsByKey, clip.name, clip);
            AddClipKey(clipsByKey, GetCanonicalClipKey(clip.name), clip);
        }

        ClipCache[spec.Key] = clipsByKey;
        return clipsByKey;
    }

    private static void AddClipKey(Dictionary<string, AnimationClip> clipsByKey, string key, AnimationClip clip)
    {
        if (clipsByKey == null || clip == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!clipsByKey.ContainsKey(key))
        {
            clipsByKey[key] = clip;
        }
    }

    private static bool TryFindReplacementClip(string originalClipName, Dictionary<string, AnimationClip> replacementClips, out AnimationClip replacement)
    {
        replacement = null;
        if (replacementClips == null || string.IsNullOrWhiteSpace(originalClipName))
        {
            return false;
        }

        if (replacementClips.TryGetValue(originalClipName, out replacement))
        {
            return replacement != null;
        }

        string canonicalKey = GetCanonicalClipKey(originalClipName);
        if (!string.IsNullOrEmpty(canonicalKey) && replacementClips.TryGetValue(canonicalKey, out replacement))
        {
            return replacement != null;
        }

        return false;
    }

    private static bool TryResolveSpec(Animator animator, IntroPetDefinition definition, out BreedAnimationSpec spec)
    {
        string identityKey = ResolveIdentityKey(animator, definition);
        for (int i = 0; i < BreedAnimationSpecs.Length; i++)
        {
            if (string.Equals(BreedAnimationSpecs[i].Key, identityKey, StringComparison.OrdinalIgnoreCase))
            {
                spec = BreedAnimationSpecs[i];
                return true;
            }
        }

        spec = new BreedAnimationSpec();
        return false;
    }

    private static string ResolveIdentityKey(Animator animator, IntroPetDefinition definition)
    {
        if (definition != null)
        {
            string petId = definition.PetId != null ? definition.PetId.Trim().ToLowerInvariant() : string.Empty;
            switch (petId)
            {
                case "corgi":
                case "husky":
                case "labrador":
                case "pug":
                case "cat_simple":
                case "cat_chubby":
                    return petId;
            }

            string breedLabel = definition.BreedLabel != null ? definition.BreedLabel.Trim().ToLowerInvariant() : string.Empty;
            if (breedLabel.Contains("simple"))
            {
                return "cat_simple";
            }

            if (breedLabel.Contains("chubby") || breedLabel.Contains("fat"))
            {
                return "cat_chubby";
            }
        }

        string identity = BuildIdentitySource(animator);
        if (identity.Contains("corgi"))
        {
            return "corgi";
        }

        if (identity.Contains("husky"))
        {
            return "husky";
        }

        if (identity.Contains("labrador"))
        {
            return "labrador";
        }

        if (identity.Contains("pug"))
        {
            return "pug";
        }

        if (identity.Contains("catsimple") || identity.Contains("cat_simple"))
        {
            return "cat_simple";
        }

        if (identity.Contains("catfat") || identity.Contains("chubbycat") || identity.Contains("cat_chubby"))
        {
            return "cat_chubby";
        }

        return string.Empty;
    }

    private static string BuildIdentitySource(Animator animator)
    {
        StringBuilder builder = new StringBuilder(256);
        if (animator != null)
        {
            AppendIdentityToken(builder, animator.name);

            if (animator.avatar != null)
            {
                AppendIdentityToken(builder, animator.avatar.name);
            }

            Transform root = animator.transform.root;
            if (root != null)
            {
                AppendIdentityToken(builder, root.name);
                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child == null)
                    {
                        continue;
                    }

                    AppendIdentityToken(builder, child.name);

                    SkinnedMeshRenderer skinnedMesh = child.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMesh != null)
                    {
                        if (skinnedMesh.sharedMesh != null)
                        {
                            AppendIdentityToken(builder, skinnedMesh.sharedMesh.name);
                        }

                        if (skinnedMesh.sharedMaterial != null)
                        {
                            AppendIdentityToken(builder, skinnedMesh.sharedMaterial.name);
                        }
                    }
                }
            }
        }

        return builder.ToString().ToLowerInvariant();
    }

    private static void AppendIdentityToken(StringBuilder builder, string token)
    {
        if (builder == null || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(token);
    }

    private static string GetCanonicalClipKey(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return string.Empty;
        }

        string value = clipName.Trim();
        int pipeIndex = value.LastIndexOf('|');
        if (pipeIndex >= 0 && pipeIndex < value.Length - 1)
        {
            value = value.Substring(pipeIndex + 1);
        }

        string[] markers =
        {
            "RunFast",
            "Walk",
            "Trot",
            "Idle",
            "Lie",
            "EatDrink",
            "Eating",
            "Drinking",
            "Run",
            "Jump",
            "Trans",
            "Sit",
            "Attack",
            "Bark",
            "TailWag",
            "Scratching",
            "Pickup",
            "PutDown",
            "Petting",
            "Crouch"
        };

        for (int i = 0; i < markers.Length; i++)
        {
            int markerIndex = value.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0)
            {
                value = value.Substring(markerIndex);
                break;
            }
        }

        value = value.Replace(' ', '_');
        if (value.EndsWith("_RM", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("_IP", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3);
        }

        return value.ToLowerInvariant();
    }
#endif
}
