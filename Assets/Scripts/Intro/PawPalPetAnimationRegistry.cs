using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum PawPalPetLifeStage
{
    Adult,
    Puppy,
    Kitten
}

public enum PawPalPetVocalClass
{
    BarkLight,
    BarkDark,
    Cat
}

public sealed class PawPalPetAnimationEntry
{
    public PawPalPetAnimationEntry(
        string canonicalKey,
        IntroPetSpecies species,
        PawPalPetLifeStage lifeStage,
        PawPalPetSizeClass sizeClass,
        PawPalPetVocalClass vocalClass,
        string importedAnimationAssetPath,
        string importedClipPrefix,
        string runtimeControllerAssetPath,
        string pettingAnimationAssetPath,
        params string[] aliases)
    {
        CanonicalKey = canonicalKey ?? string.Empty;
        Species = species;
        LifeStage = lifeStage;
        SizeClass = sizeClass;
        VocalClass = vocalClass;
        ImportedAnimationAssetPath = importedAnimationAssetPath ?? string.Empty;
        ImportedClipPrefix = importedClipPrefix ?? string.Empty;
        RuntimeControllerAssetPath = runtimeControllerAssetPath ?? string.Empty;
        PettingAnimationAssetPath = pettingAnimationAssetPath ?? string.Empty;
        Aliases = aliases ?? Array.Empty<string>();
    }

    public string CanonicalKey { get; }
    public IntroPetSpecies Species { get; }
    public PawPalPetLifeStage LifeStage { get; }
    public PawPalPetSizeClass SizeClass { get; }
    public PawPalPetVocalClass VocalClass { get; }
    public string ImportedAnimationAssetPath { get; }
    public string ImportedClipPrefix { get; }
    public string RuntimeControllerAssetPath { get; }
    public string PettingAnimationAssetPath { get; }
    public string[] Aliases { get; }
}

public static class PawPalPetAnimationRegistry
{
    public const string DogBaseControllerAssetPath = "Assets/Animations/Dog_BaseController.controller";
    public const string ImportedLabradorPuppyPettingAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Anim/Puppy_Labrador_petting.fbx";

    private static readonly PawPalPetAnimationEntry[] Entries =
    {
        new PawPalPetAnimationEntry(
            "puppy_labrador",
            IntroPetSpecies.Dog,
            PawPalPetLifeStage.Puppy,
            PawPalPetSizeClass.Small,
            PawPalPetVocalClass.BarkLight,
            "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Anim/Puppy_Labrador_anim_RM.fbx",
            "Arm_Labrador",
            DogBaseControllerAssetPath,
            ImportedLabradorPuppyPettingAssetPath,
            "puppylabrador",
            "puppy_labrador",
            "labradorpuppy"),
        new PawPalPetAnimationEntry(
            "kitten_simple",
            IntroPetSpecies.Cat,
            PawPalPetLifeStage.Kitten,
            PawPalPetSizeClass.Small,
            PawPalPetVocalClass.Cat,
            "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Simple/Cat/FBX/Anim/Cat_Simple_anim_RM.fbx",
            "Arm_Cat",
            DogBaseControllerAssetPath,
            string.Empty,
            "kittensimple",
            "kitten_simple"),
        new PawPalPetAnimationEntry(
            "cat_simple",
            IntroPetSpecies.Cat,
            PawPalPetLifeStage.Adult,
            PawPalPetSizeClass.Medium,
            PawPalPetVocalClass.Cat,
            "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Simple/Cat/FBX/Anim/Cat_Simple_anim_RM.fbx",
            "Arm_Cat",
            DogBaseControllerAssetPath,
            string.Empty,
            "catsimple",
            "cat_simple",
            "catstray",
            "cat_stray",
            "straycat"),
        new PawPalPetAnimationEntry(
            "cat_chubby",
            IntroPetSpecies.Cat,
            PawPalPetLifeStage.Adult,
            PawPalPetSizeClass.Medium,
            PawPalPetVocalClass.Cat,
            "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Fat/CatFat/FBX/Anim/CatFat_anim_RM.fbx",
            "Arm_Cat",
            DogBaseControllerAssetPath,
            string.Empty,
            "catchubby",
            "cat_chubby",
            "catfat",
            "chubbycat"),
        new PawPalPetAnimationEntry("beagle", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Beagle/Dog/FBX/Anim/Beagle_anim_IP.fbx", "Arm_Beagle", DogBaseControllerAssetPath, string.Empty, "beagle"),
        new PawPalPetAnimationEntry("border_collie", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Border_Collie/Dog/FBX/Anim/BorderCollie_anim_IP.fbx", "Arm_Collie", DogBaseControllerAssetPath, string.Empty, "bordercollie", "border_collie", "collie"),
        new PawPalPetAnimationEntry("boxer", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Boxer/Dog/FBX/Anim/Dog_Boxer_anim_IP.fbx", "Arm_Boxer", DogBaseControllerAssetPath, string.Empty, "boxer"),
        new PawPalPetAnimationEntry("bullterrier", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/BullTerrier/Dog/FBX/Anim/BullTerrier_anim_IP.fbx", "Arm_BullTerrier", DogBaseControllerAssetPath, string.Empty, "bullterrier", "bull_terrier"),
        new PawPalPetAnimationEntry("corgi", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Corgi/Dog/FBX/Anim/Corgi_anim_IP.fbx", "Arm_Corgi", DogBaseControllerAssetPath, string.Empty, "corgi"),
        new PawPalPetAnimationEntry("dalmatian", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dalmatian/Dog/FBX/Anim/Dalmatian_anim_IP.fbx", "Arm_Dalmatian", DogBaseControllerAssetPath, string.Empty, "dalmatian"),
        new PawPalPetAnimationEntry("doberman", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Doberman/Dog/FBX/Anim/Doberman_anim_IP.fbx", "Arm_Doberman", DogBaseControllerAssetPath, string.Empty, "doberman"),
        new PawPalPetAnimationEntry("frenchbulldog", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/FrenchBulldog/Dog/FBX/Anim/FrenchBulldog_anim_IP.fbx", "Arm_FrBulldog", DogBaseControllerAssetPath, string.Empty, "frenchbulldog", "french_bulldog"),
        new PawPalPetAnimationEntry("goldenretriever", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/GoldenRetriever/Dog/FBX/Anim/Retriever_anim_IP.fbx", "Arm_Retriever", DogBaseControllerAssetPath, string.Empty, "goldenretriever", "golden_retriever", "retriever", "golden"),
        new PawPalPetAnimationEntry("husky", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Husky/Dog/FBX/Anim/Husky_anim_IP.fbx", "Arm_Husky", DogBaseControllerAssetPath, string.Empty, "husky"),
        new PawPalPetAnimationEntry("jackrussellterrier", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Small, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/JackRussellTerrier/Dog/FBX/Anim/JRTerrier_anim_IP.fbx", "Arm_JRTerrier", DogBaseControllerAssetPath, string.Empty, "jackrussellterrier", "jack_russell_terrier", "jrterrier"),
        new PawPalPetAnimationEntry("labrador", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Labrador/Dog/FBX/Anim/Labrador_anim_IP.fbx", "Arm_Labrador", DogBaseControllerAssetPath, string.Empty, "labrador"),
        new PawPalPetAnimationEntry("pitbull", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Pitbull/Dog/FBX/Anim/Pitbull_anim_IP.fbx", "Arm_Pitbull", DogBaseControllerAssetPath, string.Empty, "pitbull", "pit_bull"),
        new PawPalPetAnimationEntry("pug", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Pug/Dog/FBX/Anim/Pug_anim_IP.fbx", "Arm_Pug", DogBaseControllerAssetPath, string.Empty, "pug"),
        new PawPalPetAnimationEntry("rottweiler", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Rottweiler/Dog/FBX/Anim/Rottweiler_anim_IP.fbx", "Arm_Rottweiler", DogBaseControllerAssetPath, string.Empty, "rottweiler"),
        new PawPalPetAnimationEntry("shepherd", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Large, PawPalPetVocalClass.BarkDark, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Shepherd/Dog/FBX/Anim/Shepherd_anim_IP.fbx", "Arm_Shepherd", DogBaseControllerAssetPath, string.Empty, "shepherd", "germanshepherd", "german_shepherd"),
        new PawPalPetAnimationEntry("shibainu", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/ShibaInu/Dog/FBX/Anim/ShibaInu_anim_IP.fbx", "Arm_Shiba", DogBaseControllerAssetPath, string.Empty, "shibainu", "shiba_inu", "shiba"),
        new PawPalPetAnimationEntry("spitz", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Medium, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Spitz/Dog/FBX/Anim/Spitz_anim_IP.fbx", "Arm_Spitz", DogBaseControllerAssetPath, string.Empty, "spitz"),
        new PawPalPetAnimationEntry("toyterrier", IntroPetSpecies.Dog, PawPalPetLifeStage.Adult, PawPalPetSizeClass.Small, PawPalPetVocalClass.BarkLight, "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/ToyTerrier/Dog/FBX/Anim/ToyTerrier_anim_IP.fbx", "Arm_ToyTerrier", DogBaseControllerAssetPath, string.Empty, "toyterrier", "toy_terrier")
    };

#if UNITY_EDITOR
    private static readonly Dictionary<string, Dictionary<string, AnimationClip>> ClipCache = new Dictionary<string, Dictionary<string, AnimationClip>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RuntimeAnimatorController> OverrideControllerCache = new Dictionary<string, RuntimeAnimatorController>(StringComparer.OrdinalIgnoreCase);
#endif

    public static IEnumerable<PawPalPetAnimationEntry> GetAllEntries()
    {
        return Entries;
    }

    public static bool IsKnownPetIdentity(params string[] values)
    {
        PawPalPetAnimationEntry unused;
        return TryResolveEntry(values, out unused);
    }

    public static PawPalPetSizeClass ResolveSizeClass(string petId, string breedName, string runtimeBreed, string objectName)
    {
        PawPalPetAnimationEntry entry;
        if (TryResolveEntry(petId, breedName, runtimeBreed, objectName, out entry))
        {
            return entry.SizeClass;
        }

        return PawPalPetSizeClass.Medium;
    }

    public static bool TryResolveEntry(IntroPetDefinition definition, string runtimeBreed, string objectName, out PawPalPetAnimationEntry entry)
    {
        return TryResolveEntry(
            definition != null ? definition.PetId : null,
            definition != null ? definition.BreedLabel : null,
            runtimeBreed,
            objectName,
            out entry);
    }

    public static bool TryResolveEntry(
        string petId,
        string breedName,
        string runtimeBreed,
        string objectName,
        out PawPalPetAnimationEntry entry)
    {
        return TryResolveEntry(new[]
        {
            petId,
            breedName,
            runtimeBreed,
            objectName
        }, out entry);
    }

    public static bool TryResolveEntry(
        string petId,
        string breedName,
        string runtimeBreed,
        string objectName,
        string controllerName,
        string avatarName,
        out PawPalPetAnimationEntry entry)
    {
        return TryResolveEntry(new[]
        {
            petId,
            breedName,
            runtimeBreed,
            objectName,
            controllerName,
            avatarName
        }, out entry);
    }

    public static bool TryResolveEntry(Animator animator, IntroPetDefinition definition, string runtimeBreed, string objectName, out PawPalPetAnimationEntry entry)
    {
        string controllerName = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : null;
        string avatarName = animator != null && animator.avatar != null
            ? animator.avatar.name
            : null;
        string animatorName = animator != null ? animator.name : null;
        string rootName = animator != null && animator.transform != null && animator.transform.root != null
            ? animator.transform.root.name
            : null;

        return TryResolveEntry(new[]
        {
            definition != null ? definition.PetId : null,
            definition != null ? definition.BreedLabel : null,
            runtimeBreed,
            objectName,
            animatorName,
            rootName,
            controllerName,
            avatarName
        }, out entry);
    }

    public static bool TryResolveEntryFromRawValues(out PawPalPetAnimationEntry entry, params string[] values)
    {
        return TryResolveEntry(values, out entry);
    }

    private static bool TryResolveEntry(IEnumerable<string> values, out PawPalPetAnimationEntry entry)
    {
        List<string> normalizedValues = new List<string>();
        if (values != null)
        {
            foreach (string value in values)
            {
                string normalized = NormalizeKey(value);
                if (!string.IsNullOrEmpty(normalized))
                {
                    normalizedValues.Add(normalized);
                }
            }
        }

        for (int entryIndex = 0; entryIndex < Entries.Length; entryIndex++)
        {
            PawPalPetAnimationEntry candidateEntry = Entries[entryIndex];
            for (int aliasIndex = 0; aliasIndex < candidateEntry.Aliases.Length; aliasIndex++)
            {
                string normalizedAlias = NormalizeKey(candidateEntry.Aliases[aliasIndex]);
                if (string.IsNullOrEmpty(normalizedAlias))
                {
                    continue;
                }

                for (int valueIndex = 0; valueIndex < normalizedValues.Count; valueIndex++)
                {
                    if (normalizedValues[valueIndex].Contains(normalizedAlias))
                    {
                        entry = candidateEntry;
                        return true;
                    }
                }
            }
        }

        entry = null;
        return false;
    }

    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

#if UNITY_EDITOR
    public static RuntimeAnimatorController ResolveEditorOverrideController(RuntimeAnimatorController baseController, PawPalPetAnimationEntry entry)
    {
        if (baseController == null || entry == null || string.IsNullOrWhiteSpace(entry.ImportedAnimationAssetPath))
        {
            return null;
        }

        string cacheKey = baseController.GetInstanceID() + "|" + entry.CanonicalKey;
        RuntimeAnimatorController cachedController;
        if (OverrideControllerCache.TryGetValue(cacheKey, out cachedController) && cachedController != null)
        {
            return cachedController;
        }

        Dictionary<string, AnimationClip> replacementClips = GetEditorImportedClips(entry);
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
        overrideController.name = baseController.name + "_" + entry.CanonicalKey + "_RuntimeOverride";
        OverrideControllerCache[cacheKey] = overrideController;
        return overrideController;
    }

    public static RuntimeAnimatorController ResolveEditorBaseController(PawPalPetAnimationEntry entry)
    {
        string path = entry != null && !string.IsNullOrWhiteSpace(entry.RuntimeControllerAssetPath)
            ? entry.RuntimeControllerAssetPath
            : DogBaseControllerAssetPath;
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }

    public static Dictionary<string, AnimationClip> GetEditorImportedClips(PawPalPetAnimationEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.ImportedAnimationAssetPath))
        {
            return null;
        }

        Dictionary<string, AnimationClip> cachedClips;
        if (ClipCache.TryGetValue(entry.CanonicalKey, out cachedClips))
        {
            return cachedClips;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(entry.ImportedAnimationAssetPath);
        Dictionary<string, AnimationClip> clipsByKey = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        if (assets != null)
        {
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || string.IsNullOrWhiteSpace(clip.name) || clip.name.StartsWith("__preview", StringComparison.Ordinal))
                {
                    continue;
                }

                AddClipKey(clipsByKey, clip.name, clip);
                string canonicalKey = GetCanonicalClipKey(clip.name);
                AddClipKey(clipsByKey, canonicalKey, clip);
                AddEquivalentClipKeys(clipsByKey, canonicalKey, clip);
                AddEntrySpecificClipAliases(clipsByKey, entry, clip);
            }
        }

        ClipCache[entry.CanonicalKey] = clipsByKey;
        return clipsByKey;
    }

    public static bool HasEditorAnimationSource(PawPalPetAnimationEntry entry)
    {
        return entry != null
            && !string.IsNullOrWhiteSpace(entry.ImportedAnimationAssetPath)
            && AssetDatabase.LoadMainAssetAtPath(entry.ImportedAnimationAssetPath) != null;
    }

    public static bool HasEditorControllerSource(PawPalPetAnimationEntry entry)
    {
        string path = entry != null && !string.IsNullOrWhiteSpace(entry.RuntimeControllerAssetPath)
            ? entry.RuntimeControllerAssetPath
            : DogBaseControllerAssetPath;
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path) != null;
    }

    public static bool HasRequiredGameplayClips(PawPalPetAnimationEntry entry, out string missingClipSuffix)
    {
        missingClipSuffix = string.Empty;
        Dictionary<string, AnimationClip> clips = GetEditorImportedClips(entry);
        if (clips == null || clips.Count == 0)
        {
            missingClipSuffix = "<no_clips>";
            return false;
        }

        string[] requiredSuffixes =
        {
            "Idle_1",
            "Idle_2",
            "Idle_3",
            "Idle_4",
            "Idle_7",
            "Bark",
            "Lie_belly_start",
            "Lie_belly_loop_1",
            "Lie_belly_sleep_start",
            "Lie_belly_sleep",
            "Lie_belly_sleep_end",
            "Lie_belly_end",
            "EatDrink_start",
            "Eat_loop",
            "Drink_loop",
            "EatDrink_end"
        };

        for (int i = 0; i < requiredSuffixes.Length; i++)
        {
            if (!TryFindClipBySuffix(clips, requiredSuffixes[i]))
            {
                missingClipSuffix = requiredSuffixes[i];
                return false;
            }
        }

        bool hasRunForward = TryFindClipBySuffix(clips, "Run_F_IP")
            || TryFindClipBySuffix(clips, "Run_F_RM")
            || TryFindClipBySuffix(clips, "Walk_F_RM");
        if (!hasRunForward)
        {
            missingClipSuffix = "Run_F_*";
            return false;
        }

        return true;
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

    private static string GetCanonicalClipKey(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return string.Empty;
        }

        int separatorIndex = clipName.LastIndexOf('|');
        string suffix = separatorIndex >= 0 && separatorIndex < clipName.Length - 1
            ? clipName.Substring(separatorIndex + 1)
            : clipName;
        return suffix.Trim().ToLowerInvariant();
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

    private static void AddEquivalentClipKeys(Dictionary<string, AnimationClip> clipsByKey, string canonicalKey, AnimationClip clip)
    {
        if (clipsByKey == null || clip == null || string.IsNullOrWhiteSpace(canonicalKey))
        {
            return;
        }

        if (canonicalKey.EndsWith("_rm", StringComparison.OrdinalIgnoreCase))
        {
            AddClipKey(clipsByKey, canonicalKey.Substring(0, canonicalKey.Length - 3) + "_ip", clip);
        }
        else if (canonicalKey.EndsWith("_ip", StringComparison.OrdinalIgnoreCase))
        {
            AddClipKey(clipsByKey, canonicalKey.Substring(0, canonicalKey.Length - 3) + "_rm", clip);
        }
    }

    private static void AddEntrySpecificClipAliases(Dictionary<string, AnimationClip> clipsByKey, PawPalPetAnimationEntry entry, AnimationClip clip)
    {
        if (clipsByKey == null || entry == null || clip == null || string.IsNullOrWhiteSpace(clip.name))
        {
            return;
        }

        string simplifiedKey = string.Empty;
        if ((entry.CanonicalKey == "cat_simple" || entry.CanonicalKey == "kitten_simple")
            && clip.name.StartsWith("CatSimple_", StringComparison.OrdinalIgnoreCase))
        {
            simplifiedKey = clip.name.Substring("CatSimple_".Length);
        }
        else if (entry.CanonicalKey == "cat_chubby"
            && clip.name.StartsWith("CatFat_", StringComparison.OrdinalIgnoreCase))
        {
            simplifiedKey = clip.name.Substring("CatFat_".Length);
        }

        if (string.IsNullOrWhiteSpace(simplifiedKey))
        {
            return;
        }

        string canonicalKey = simplifiedKey.Trim().ToLowerInvariant();
        AddClipKey(clipsByKey, canonicalKey, clip);
        AddEquivalentClipKeys(clipsByKey, canonicalKey, clip);
    }

    private static bool TryFindClipBySuffix(Dictionary<string, AnimationClip> clips, string suffix)
    {
        if (clips == null || string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        string canonicalSuffix = suffix.Trim().ToLowerInvariant();
        if (clips.ContainsKey(canonicalSuffix))
        {
            return true;
        }

        foreach (KeyValuePair<string, AnimationClip> clip in clips)
        {
            if (clip.Key.EndsWith("|" + suffix, StringComparison.OrdinalIgnoreCase)
                || clip.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
#endif
}
