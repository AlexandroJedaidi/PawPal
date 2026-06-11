using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PawPalBreedShopPreviewEntry
{
    public string BreedKey;
    public IntroPetDefinition Definition;
    public Vector3 GroupOffset = Vector3.zero;
    public float GroupScale = 1f;
}

public static class PawPalBreedShopPreviewLibrary
{
    private static readonly Dictionary<string, PawPalBreedShopPreviewEntry> EntryCache = new Dictionary<string, PawPalBreedShopPreviewEntry>(StringComparer.OrdinalIgnoreCase);
    private static bool cacheBuilt;

    public static bool TryResolveEntry(PawPalCatalogItemDefinition item, out PawPalBreedShopPreviewEntry entry)
    {
        EnsureCache();
        entry = null;
        if (item == null)
        {
            return false;
        }

        string breedKey = NormalizeBreedKey(item);
        return !string.IsNullOrEmpty(breedKey)
            && EntryCache.TryGetValue(breedKey, out entry)
            && entry != null
            && entry.Definition != null
            && entry.Definition.BasePrefab != null;
    }

    public static GameObject LoadLeftPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return entry != null && entry.Definition != null ? entry.Definition.BasePrefab : null;
    }

    public static GameObject LoadCenterPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return entry != null && entry.Definition != null ? entry.Definition.BasePrefab : null;
    }

    public static GameObject LoadRightPrefab(PawPalBreedShopPreviewEntry entry)
    {
        return entry != null && entry.Definition != null ? entry.Definition.BasePrefab : null;
    }

    private static void EnsureCache()
    {
        if (cacheBuilt)
        {
            return;
        }

        cacheBuilt = true;
        EntryCache.Clear();

        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || definition.Species != IntroPetSpecies.Dog || definition.BasePrefab == null)
            {
                continue;
            }

            PawPalBreedShopPreviewEntry entry = new PawPalBreedShopPreviewEntry
            {
                BreedKey = ResolveDefinitionKey(definition),
                Definition = definition
            };

            RegisterEntry(definition.PetId, entry);
            RegisterEntry(definition.BreedName, entry);
            RegisterEntry(definition.DisplayName, entry);
            RegisterEntry(entry.BreedKey, entry);
        }
    }

    private static void RegisterEntry(string rawKey, PawPalBreedShopPreviewEntry entry)
    {
        string normalizedKey = PawPalPetAnimationRegistry.NormalizeKey(rawKey);
        if (!string.IsNullOrEmpty(normalizedKey) && entry != null)
        {
            EntryCache[normalizedKey] = entry;
        }
    }

    private static string ResolveDefinitionKey(IntroPetDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(definition.PetId))
        {
            return PawPalPetAnimationRegistry.NormalizeKey(definition.PetId);
        }

        if (!string.IsNullOrEmpty(definition.BreedName))
        {
            return PawPalPetAnimationRegistry.NormalizeKey(definition.BreedName);
        }

        return PawPalPetAnimationRegistry.NormalizeKey(definition.DisplayName);
    }

    private static string NormalizeBreedKey(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(item.Id) && item.Id.StartsWith("dog_", StringComparison.OrdinalIgnoreCase))
        {
            return PawPalPetAnimationRegistry.NormalizeKey(item.Id.Substring(4));
        }

        return PawPalPetAnimationRegistry.NormalizeKey(item.DisplayName);
    }
}
