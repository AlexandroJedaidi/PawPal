using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum IntroPetSpawnStatus
{
    Spawned,
    SpawnedWithBasePrefabFallback,
    Failed
}

public sealed class IntroPetSpawnOutcome
{
    public IntroPetSpawnOutcome(IntroPetRuntimeSelection selection, IntroPetAgent agent, IntroPetSpawnStatus status)
    {
        Selection = selection;
        Agent = agent;
        Status = status;
    }

    public IntroPetRuntimeSelection Selection { get; }
    public IntroPetAgent Agent { get; }
    public IntroPetSpawnStatus Status { get; }
    public bool IsUsable => Agent != null && Selection != null;
}

public sealed class IntroPetSpawner : MonoBehaviour
{
    private readonly List<IntroPetAgent> agents = new List<IntroPetAgent>();
    private IntroPetSelectionController controller;
    private Bounds fieldBounds;

    public IReadOnlyList<IntroPetAgent> Agents
    {
        get { return agents; }
    }

    public DogRoomAgent[] GetOrderedDogAgents()
    {
        List<DogRoomAgent> orderedDogs = new List<DogRoomAgent>(agents.Count);
        for (int i = 0; i < agents.Count; i++)
        {
            IntroPetAgent agent = agents[i];
            if (agent == null || agent.RuntimeDogAgent == null)
            {
                continue;
            }

            orderedDogs.Add(agent.RuntimeDogAgent);
        }

        return orderedDogs.ToArray();
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

    public List<IntroPetSpawnOutcome> Spawn(IList<IntroPetRuntimeSelection> selections)
    {
        Clear();
        List<IntroPetSpawnOutcome> outcomes = new List<IntroPetSpawnOutcome>();

        if (selections == null)
        {
            return outcomes;
        }

        Vector3[] spawnPositions = BuildSpawnPositions();

        for (int i = 0; i < selections.Count; i++)
        {
            IntroPetRuntimeSelection selection = selections[i];
            Vector3 position = ResolveSpawnPosition(selection, i, spawnPositions);
            Quaternion rotation = Quaternion.Euler(0f, 180f + UnityEngine.Random.Range(-24f, 24f), 0f);
            IntroPetSpawnStatus status;
            IntroPetAgent agent = CreateAgent(selection, agents.Count, position, rotation, out status);
            if (agent != null)
            {
                agents.Add(agent);
                RefreshAgentIndexes();
            }

            outcomes.Add(new IntroPetSpawnOutcome(selection, agent, status));
        }

        return outcomes;
    }

    public IntroPetAgent ReplaceAgentVariant(IntroPetAgent oldAgent, IntroPetRuntimeSelection selection)
    {
        if (oldAgent == null || selection == null || selection.Definition == null)
        {
            return oldAgent;
        }

        if (CanApplyVariantInPlace(oldAgent, selection.FurVariant))
        {
            oldAgent.ApplyFurVariant(selection.FurVariant);
            return oldAgent;
        }

        if (selection.FurVariant != null && selection.FurVariant.ReplacementMaterial == null)
        {
            Debug.LogWarning(
                "IntroPetSelection fur variant '" + selection.FurVariant.SafeDisplayName
                + "' for " + selection.Definition.DisplayName
                + " has no ReplacementMaterial. Falling back to respawned preview swap.");
        }

        int index = oldAgent.SelectionIndex;
        Vector3 position = oldAgent.transform.position;
        Quaternion rotation = oldAgent.transform.rotation;

        IntroPetSpawnStatus status;
        IntroPetAgent replacement = CreateAgent(selection, index, position, rotation, out status);
        if (replacement == null)
        {
            oldAgent.ApplyFurVariant(selection.FurVariant);
            return oldAgent;
        }

        agents.Remove(oldAgent);
        RedirectEditorSelectionBeforeDestroy(oldAgent.gameObject, replacement.gameObject);
        oldAgent.gameObject.SetActive(false);
        Destroy(oldAgent.gameObject);
        agents.Insert(Mathf.Clamp(index, 0, agents.Count), replacement);
        RefreshAgentIndexes();
        return replacement;
    }

    private static bool CanApplyVariantInPlace(IntroPetAgent agent, FurVariantDefinition variant)
    {
        if (agent == null)
        {
            return false;
        }

        if (variant == null)
        {
            return true;
        }

        return variant.ReplacementMaterial != null;
    }

    private IntroPetAgent CreateAgent(IntroPetRuntimeSelection selection, int index, Vector3 position, Quaternion rotation, out IntroPetSpawnStatus status)
    {
        status = IntroPetSpawnStatus.Failed;
        IntroPetDefinition definition = selection != null ? selection.Definition : null;
        FurVariantDefinition variant = selection != null ? selection.FurVariant : null;
        if (definition == null)
        {
            Debug.LogWarning("IntroPetSelection could not create an intro pet because the roster definition was null.");
            return null;
        }

        bool usedBasePrefabFallback;
        GameObject usedPrefab;
        GameObject instance = InstantiatePrefabObject(definition, variant, position, rotation, out usedBasePrefabFallback, out usedPrefab);
        if (instance == null)
        {
            return null;
        }

        Vector3 navMeshPosition;
        if (TryResolveSpawnPoint(position, out navMeshPosition))
        {
            instance.transform.position = navMeshPosition;
        }
        else
        {
            navMeshPosition = instance.transform.position;
            Debug.LogWarning("IntroPetSelection could not find an in-field NavMesh point for " + GetPetLabel(definition) + ". Spawning at fallback position " + navMeshPosition + ".");
        }

        instance.name = "IntroPet_" + (definition != null ? definition.DisplayName : "Pet");
        SelectedPetSessionData session = selection != null ? selection.ToSessionData() : SelectionToSession(definition, variant, index);
        SelectedPetSessionData runtimeSession = CreateRuntimeSession(session, usedPrefab, variant, usedBasePrefabFallback);
        PawPalRoomPetHandle roomPet = BuildRuntimeRoomPet(instance, definition, runtimeSession);
        SnapRuntimePetToNavMesh(instance, navMeshPosition);
        IntroPetAgent agent = instance.GetComponent<IntroPetAgent>();
        if (agent == null)
        {
            agent = instance.AddComponent<IntroPetAgent>();
        }

        agent.Initialize(controller, definition, variant, usedPrefab, index, roomPet);
        if (usedBasePrefabFallback)
        {
            agent.ApplyFurVariant(variant);
            status = IntroPetSpawnStatus.SpawnedWithBasePrefabFallback;
            Debug.LogWarning("IntroPetSelection spawned " + GetPetLabel(definition) + " from the base prefab because variant '" + GetVariantLabel(variant) + "' could not be instantiated directly.");
        }
        else
        {
            status = IntroPetSpawnStatus.Spawned;
        }

        return agent;
    }

    private static SelectedPetSessionData CreateRuntimeSession(
        SelectedPetSessionData source,
        GameObject usedPrefab,
        FurVariantDefinition variant,
        bool usedBasePrefabFallback)
    {
        if (source == null)
        {
            return null;
        }

        if (usedBasePrefabFallback || PetVariantApplier.ShouldApplyMaterialOverride(usedPrefab, variant))
        {
            return source;
        }

        return new SelectedPetSessionData
        {
            Definition = source.Definition,
            FurVariant = null,
            FurIndex = source.FurIndex,
            Gender = source.Gender,
            Personality = source.Personality,
            PetName = source.PetName,
            RuntimePetId = source.RuntimePetId
        };
    }

    private SelectedPetSessionData SelectionToSession(IntroPetDefinition definition, FurVariantDefinition variant, int index)
    {
        IntroPetRuntimeSelection selection = IntroPetRuntimeSelection.Create(definition, index);
        selection.SetFurIndex(0);
        selection.FurVariant = variant != null ? variant : selection.FurVariant;
        return selection.ToSessionData();
    }

    private PawPalRoomPetHandle BuildRuntimeRoomPet(GameObject instance, IntroPetDefinition definition, SelectedPetSessionData session)
    {
        if (instance == null)
        {
            return null;
        }

        UnityEngine.AI.NavMeshAgent navMeshAgent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = instance.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        if (definition != null && definition.Species == IntroPetSpecies.Cat)
        {
            ConfigureRuntimeLodGroups(instance);

            DogRoomAgent legacyDogAgent = instance.GetComponent<DogRoomAgent>();
            if (legacyDogAgent != null)
            {
                Destroy(legacyDogAgent);
            }

            PawPalCatRoomAgent catAgent = instance.GetComponent<PawPalCatRoomAgent>();
            if (catAgent == null)
            {
                catAgent = instance.GetComponentInChildren<PawPalCatRoomAgent>(true);
            }

            if (catAgent == null)
            {
                catAgent = instance.AddComponent<PawPalCatRoomAgent>();
            }

            catAgent.ConfigureRoomBounds(fieldBounds);
            catAgent.Initialize(session);
            return new PawPalRoomPetHandle(catAgent);
        }

        DogRoomAgent dogAgent = instance.GetComponent<DogRoomAgent>();
        if (dogAgent == null)
        {
            dogAgent = instance.AddComponent<DogRoomAgent>();
        }

        PawPalCatRoomAgent legacyCatAgent = instance.GetComponent<PawPalCatRoomAgent>();
        if (legacyCatAgent != null)
        {
            Destroy(legacyCatAgent);
        }

        dogAgent.SetRuntimeDogId(session != null ? session.RuntimePetId : string.Empty);
        dogAgent.ConfigureRoomBounds(fieldBounds);
        dogAgent.ApplySelectedPetPresentation(session);
        dogAgent.ConfigureSelectedPetRuntime(session);
        return new PawPalRoomPetHandle(dogAgent);
    }

    private static void ConfigureRuntimeLodGroups(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        LODGroup[] lodGroups = instance.GetComponentsInChildren<LODGroup>(true);
        for (int i = 0; i < lodGroups.Length; i++)
        {
            if (lodGroups[i] != null)
            {
                lodGroups[i].enabled = true;
                lodGroups[i].ForceLOD(0);
            }
        }
    }

    private void SnapRuntimePetToNavMesh(GameObject instance, Vector3 candidate)
    {
        if (instance == null)
        {
            return;
        }

        NavMeshAgent navMeshAgent = instance.GetComponent<NavMeshAgent>();
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return;
        }

        Vector3 resolved;
        if (TryResolveSpawnPoint(candidate, out resolved))
        {
            if (!navMeshAgent.Warp(resolved))
            {
                Debug.LogWarning("IntroPetSelection failed to warp " + instance.name + " onto the intro NavMesh at " + resolved + ".");
            }
        }
    }

    private GameObject InstantiatePrefabObject(
        IntroPetDefinition definition,
        FurVariantDefinition variant,
        Vector3 position,
        Quaternion rotation,
        out bool usedBasePrefabFallback,
        out GameObject usedPrefab)
    {
        usedBasePrefabFallback = false;
        usedPrefab = null;
        GameObject preferredPrefab = variant != null && variant.VariantPrefab != null
            ? variant.VariantPrefab
            : (definition != null ? definition.BasePrefab : null);
        if (preferredPrefab == null)
        {
            Debug.LogWarning("IntroPetSelection is missing a prefab for " + GetPetLabel(definition) + ".");
            return null;
        }

        string preferredError;
        if (PetVariantApplier.TryInstantiatePrefab(preferredPrefab, position, rotation, transform, out GameObject preferredInstance, out preferredError))
        {
            usedPrefab = preferredPrefab;
            return preferredInstance;
        }

        GameObject basePrefab = definition != null ? definition.BasePrefab : null;
        if (basePrefab != null && basePrefab != preferredPrefab)
        {
            string baseError;
            if (PetVariantApplier.TryInstantiatePrefab(basePrefab, position, rotation, transform, out GameObject baseInstance, out baseError))
            {
                usedBasePrefabFallback = true;
                usedPrefab = basePrefab;
                return baseInstance;
            }

            preferredError = string.IsNullOrWhiteSpace(baseError) ? preferredError : preferredError + " " + baseError;
        }

        Debug.LogWarning(
            "IntroPetSelection could not spawn "
            + GetPetLabel(definition)
            + " from variant '"
            + GetVariantLabel(variant)
            + "'. "
            + preferredError);

        return null;
    }

    private Vector3[] BuildSpawnPositions()
    {
        Vector3 center = fieldBounds.center;
        Vector3 extents = fieldBounds.extents;
        Vector3[] candidates =
        {
            new Vector3(center.x - extents.x * 0.42f, fieldBounds.min.y, center.z + extents.z * 0.18f),
            new Vector3(center.x - extents.x * 0.27f, fieldBounds.min.y, center.z + extents.z * 0.34f),
            new Vector3(center.x - extents.x * 0.12f, fieldBounds.min.y, center.z + extents.z * 0.18f),
            new Vector3(center.x + extents.x * 0.04f, fieldBounds.min.y, center.z + extents.z * 0.34f),
            new Vector3(center.x + extents.x * 0.19f, fieldBounds.min.y, center.z + extents.z * 0.18f),
            new Vector3(center.x + extents.x * 0.34f, fieldBounds.min.y, center.z + extents.z * 0.32f),
            new Vector3(center.x + extents.x * 0.46f, fieldBounds.min.y, center.z + extents.z * 0.12f)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            candidates[i] = SampleSpawnPosition(candidates[i]);
        }

        return candidates;
    }

    private Vector3 ResolveSpawnPosition(IntroPetRuntimeSelection selection, int index, Vector3[] spawnPositions)
    {
        Vector3 defaultPosition = index < spawnPositions.Length
            ? spawnPositions[index]
            : SampleSpawnPosition(fieldBounds.center);
        IntroPetDefinition definition = selection != null ? selection.Definition : null;
        string petId = definition != null ? definition.PetId : string.Empty;
        if (string.IsNullOrWhiteSpace(petId))
        {
            return defaultPosition;
        }

        Vector3 center = fieldBounds.center;
        Vector3 extents = fieldBounds.extents;

        if (string.Equals(petId, "husky", StringComparison.OrdinalIgnoreCase))
        {
            return SampleSpawnPosition(new Vector3(
                center.x,
                fieldBounds.min.y,
                center.z + extents.z * 0.04f));
        }

        if (string.Equals(petId, "corgi", StringComparison.OrdinalIgnoreCase))
        {
            return SampleSpawnPosition(new Vector3(
                center.x + extents.x * 0.22f,
                fieldBounds.min.y,
                center.z + extents.z * 0.22f));
        }

        return defaultPosition;
    }

    private Vector3 SampleSpawnPosition(Vector3 candidate)
    {
        Vector3 resolved;
        if (TryResolveSpawnPoint(candidate, out resolved))
        {
            return resolved;
        }

        return ClampToField(candidate);
    }

    private bool TryResolveSpawnPoint(Vector3 candidate, out Vector3 position)
    {
        Vector3 clampedCandidate = ClampToField(candidate);
        if (TrySampleNavMesh(clampedCandidate, 2.5f, out position))
        {
            return true;
        }

        Vector3 centerCandidate = ClampToField(new Vector3(fieldBounds.center.x, clampedCandidate.y, fieldBounds.center.z));
        if (TrySampleNavMesh(centerCandidate, 4f, out position))
        {
            return true;
        }

        Vector3[] ringCandidates =
        {
            centerCandidate + new Vector3(-fieldBounds.extents.x * 0.18f, 0f, fieldBounds.extents.z * 0.16f),
            centerCandidate + new Vector3(fieldBounds.extents.x * 0.18f, 0f, fieldBounds.extents.z * 0.16f),
            centerCandidate + new Vector3(-fieldBounds.extents.x * 0.22f, 0f, -fieldBounds.extents.z * 0.08f),
            centerCandidate + new Vector3(fieldBounds.extents.x * 0.22f, 0f, -fieldBounds.extents.z * 0.08f),
            centerCandidate + new Vector3(0f, 0f, fieldBounds.extents.z * 0.24f)
        };

        for (int i = 0; i < ringCandidates.Length; i++)
        {
            if (TrySampleNavMesh(ClampToField(ringCandidates[i]), 4f, out position))
            {
                return true;
            }
        }

        if (TryFindGridSample(clampedCandidate, out position))
        {
            return true;
        }

        position = ClampToField(centerCandidate);
        if (Terrain.activeTerrain != null)
        {
            position.y = Terrain.activeTerrain.SampleHeight(position) + Terrain.activeTerrain.transform.position.y;
        }

        return false;
    }

    private bool TrySampleNavMesh(Vector3 candidate, float radius, out Vector3 position)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(candidate, out navHit, radius, NavMesh.AllAreas) && IsInsideField(navHit.position))
        {
            position = navHit.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private bool TryFindGridSample(Vector3 preferred, out Vector3 position)
    {
        Vector3 bestPosition = Vector3.zero;
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        float minX = fieldBounds.min.x + 0.7f;
        float maxX = fieldBounds.max.x - 0.7f;
        float minZ = fieldBounds.min.z + 0.7f;
        float maxZ = fieldBounds.max.z - 0.7f;
        const int gridSteps = 6;

        for (int x = 0; x <= gridSteps; x++)
        {
            float xLerp = gridSteps == 0 ? 0f : (float)x / gridSteps;
            float sampleX = Mathf.Lerp(minX, maxX, xLerp);
            for (int z = 0; z <= gridSteps; z++)
            {
                float zLerp = gridSteps == 0 ? 0f : (float)z / gridSteps;
                Vector3 candidate = new Vector3(sampleX, preferred.y, Mathf.Lerp(minZ, maxZ, zLerp));
                Vector3 sampled;
                if (!TrySampleNavMesh(candidate, 1.75f, out sampled))
                {
                    continue;
                }

                float distance = (sampled - preferred).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = sampled;
                    found = true;
                }
            }
        }

        position = found ? bestPosition : Vector3.zero;
        return found;
    }

    private Vector3 ClampToField(Vector3 candidate)
    {
        return new Vector3(
            Mathf.Clamp(candidate.x, fieldBounds.min.x + 0.7f, fieldBounds.max.x - 0.7f),
            candidate.y,
            Mathf.Clamp(candidate.z, fieldBounds.min.z + 0.7f, fieldBounds.max.z - 0.7f));
    }

    private bool IsInsideField(Vector3 position)
    {
        return position.x >= fieldBounds.min.x - 0.05f
            && position.x <= fieldBounds.max.x + 0.05f
            && position.z >= fieldBounds.min.z - 0.05f
            && position.z <= fieldBounds.max.z + 0.05f;
    }

    private void Clear()
    {
        RedirectEditorSelectionBeforeBulkDestroy();

        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i] != null)
            {
                agents[i].gameObject.SetActive(false);
                Destroy(agents[i].gameObject);
            }
        }

        agents.Clear();
    }

#if UNITY_EDITOR
    private void RedirectEditorSelectionBeforeBulkDestroy()
    {
        Transform activeTransform = Selection.activeTransform;
        if (activeTransform == null)
        {
            return;
        }

        for (int i = 0; i < agents.Count; i++)
        {
            IntroPetAgent agent = agents[i];
            if (agent == null || !activeTransform.IsChildOf(agent.transform))
            {
                continue;
            }

            Selection.activeGameObject = controller != null ? controller.gameObject : gameObject;
            return;
        }
    }

    private void RedirectEditorSelectionBeforeDestroy(GameObject destroyedObject, GameObject replacementObject)
    {
        if (destroyedObject == null)
        {
            return;
        }

        Transform activeTransform = Selection.activeTransform;
        if (activeTransform == null || !activeTransform.IsChildOf(destroyedObject.transform))
        {
            return;
        }

        Selection.activeGameObject = replacementObject != null
            ? replacementObject
            : controller != null ? controller.gameObject : gameObject;
    }
#else
    private void RedirectEditorSelectionBeforeBulkDestroy()
    {
    }

    private void RedirectEditorSelectionBeforeDestroy(GameObject destroyedObject, GameObject replacementObject)
    {
    }
#endif

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
