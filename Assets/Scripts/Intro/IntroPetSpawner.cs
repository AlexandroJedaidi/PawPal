using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class IntroPetSpawner : MonoBehaviour
{
    private readonly List<IntroPetAgent> agents = new List<IntroPetAgent>();
    private IntroPetSelectionController controller;
    private Bounds fieldBounds;

    public IReadOnlyList<IntroPetAgent> Agents
    {
        get { return agents; }
    }

    public void Initialize(IntroPetSelectionController owner, Bounds roamBounds)
    {
        controller = owner;
        fieldBounds = roamBounds;
    }

    public IntroPetDefinition[] LoadDefinitions()
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        Array.Sort(definitions, CompareDefinitions);
        return definitions;
    }

    public void Spawn(IntroPetDefinition[] definitions, IList<IntroPetRuntimeSelection> selections)
    {
        Clear();

        if (definitions == null || selections == null)
        {
            return;
        }

        float spacing = 1.75f;
        float startX = -(definitions.Length - 1) * spacing * 0.5f;
        for (int i = 0; i < definitions.Length; i++)
        {
            Vector3 position = new Vector3(startX + i * spacing, 0f, UnityEngine.Random.Range(-0.6f, 1.2f));
            Quaternion rotation = Quaternion.Euler(0f, 180f + UnityEngine.Random.Range(-24f, 24f), 0f);
            IntroPetRuntimeSelection selection = selections[i];
            SpawnAgent(definitions[i], selection.FurVariant, i, position, rotation);
        }
    }

    public IntroPetAgent ReplaceAgentVariant(IntroPetAgent oldAgent, IntroPetRuntimeSelection selection)
    {
        if (oldAgent == null || selection == null || selection.Definition == null)
        {
            return oldAgent;
        }

        GameObject desiredPrefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        if (desiredPrefab == null || desiredPrefab == oldAgent.SourcePrefab)
        {
            oldAgent.ApplyFurVariant(selection.FurVariant);
            return oldAgent;
        }

        int index = oldAgent.SelectionIndex;
        Vector3 position = oldAgent.transform.position;
        Quaternion rotation = oldAgent.transform.rotation;
        bool wasSelected = controller != null && controller.CurrentAgent == oldAgent;

        agents.Remove(oldAgent);
        Destroy(oldAgent.gameObject);

        IntroPetAgent replacement = SpawnAgent(selection.Definition, selection.FurVariant, index, position, rotation);
        if (replacement != null)
        {
            replacement.SetSelected(wasSelected, Camera.main);
        }

        return replacement;
    }

    private IntroPetAgent SpawnAgent(IntroPetDefinition definition, FurVariantDefinition variant, int index, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = PetVariantApplier.GetPrefabForVariant(definition, variant);
        if (prefab == null)
        {
            Debug.LogWarning("IntroPetSelection is missing a prefab for " + (definition != null ? definition.DisplayName : "an intro pet") + ".");
            return null;
        }

        GameObject instance = InstantiatePrefabObject(prefab, definition, variant, position, rotation);
        if (instance == null)
        {
            return null;
        }

        instance.name = "IntroPet_" + (definition != null ? definition.DisplayName : "Pet");
        IntroPetAgent agent = instance.GetComponent<IntroPetAgent>();
        if (agent == null)
        {
            agent = instance.AddComponent<IntroPetAgent>();
        }

        agent.Initialize(controller, definition, variant, prefab, index, fieldBounds);
        agents.Insert(Mathf.Clamp(index, 0, agents.Count), agent);
        RefreshAgentIndexes();
        return agent;
    }

    private GameObject InstantiatePrefabObject(
        GameObject prefab,
        IntroPetDefinition definition,
        FurVariantDefinition variant,
        Vector3 position,
        Quaternion rotation)
    {
        try
        {
            UnityEngine.Object clone = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, position, rotation, transform);
            if (clone is GameObject gameObject)
            {
                return gameObject;
            }

            if (clone is Component component)
            {
                return component.gameObject;
            }

            Debug.LogWarning(
                "IntroPetSelection could not spawn "
                + GetPetLabel(definition)
                + " because the prefab reference resolved to "
                + (clone != null ? clone.GetType().Name : "null")
                + " instead of a GameObject.");

            if (clone != null)
            {
                Destroy(clone);
            }
        }
        catch (InvalidCastException exception)
        {
            Debug.LogWarning(
                "IntroPetSelection could not spawn "
                + GetPetLabel(definition)
                + " from variant '"
                + GetVariantLabel(variant)
                + "' because its prefab reference is not a valid GameObject. "
                + exception.Message);
        }

        return null;
    }

    private void Clear()
    {
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i] != null)
            {
                Destroy(agents[i].gameObject);
            }
        }

        agents.Clear();
    }

    private void RefreshAgentIndexes()
    {
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null)
            {
                agents[i].SetSelectionIndex(i);
            }
        }
    }

    private static int CompareDefinitions(IntroPetDefinition first, IntroPetDefinition second)
    {
        return GetSortOrder(first).CompareTo(GetSortOrder(second));
    }

    private static string GetPetLabel(IntroPetDefinition definition)
    {
        return definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName
            : "an intro pet";
    }

    private static string GetVariantLabel(FurVariantDefinition variant)
    {
        return variant != null ? variant.SafeDisplayName : "default";
    }

    private static int GetSortOrder(IntroPetDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.PetId))
        {
            return 999;
        }

        switch (definition.PetId)
        {
            case "labrador":
                return 0;
            case "corgi":
                return 1;
            case "husky":
                return 2;
            case "pug":
                return 3;
            case "cat_simple":
                return 4;
            case "cat_chubby":
                return 5;
            default:
                return 100;
        }
    }
}
