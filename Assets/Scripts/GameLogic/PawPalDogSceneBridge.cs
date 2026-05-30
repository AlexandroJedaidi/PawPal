using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PawPalDogSceneBridge : MonoBehaviour
{
    private const string RuntimeCollarName = "PawPalRuntimeCollar";
    private const float ReferenceCollarHeadDistance = 0.034422904f;
    private const float ReferenceCollarHeadOffsetFactor = 0.4967607f;
    private const float ReferenceCollarNeckDistance = 0.049051054f;
    private const float FallbackCollarScaleExponent = 0.5f;
    private const float MinimumCollarScaleMultiplier = 0.85f;
    private const float MaximumCollarScaleMultiplier = 1.75f;
    private const int MaxActiveSpawnedToys = 3;
    private static readonly string[] PreferredCollarPlacementReferenceNames =
    {
        "Collar",
        "CollarSimple_C1",
        "CollarSimple_C2",
        "CollarSimple_C3"
    };

    private static readonly string[] PreferredCollarAnchorNames =
    {
        "CollarAnchor",
        "AccessoryAnchor",
        "Accessories",
        "Accessory",
        "NeckSocket",
        "neck",
        "Neck",
        "HeadAimRig",
        "Spine_05",
        "Spine_04",
        "Spine_03"
    };

    private readonly Dictionary<string, GameObject> collarInstancesByDogId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly List<SpawnedToyRecord> activeSpawnedToys = new List<SpawnedToyRecord>();
    private readonly HashSet<string> warningKeys = new HashSet<string>(StringComparer.Ordinal);

    private PawPalGameRuntime runtime;
    private DogRoomAgent currentPresentationAgent;
    private BoxCollider fallbackToyFloor;

    private sealed class SpawnedToyRecord
    {
        public SpawnedToyRecord(GameObject instance, string itemId)
        {
            Instance = instance;
            ItemId = itemId;
        }

        public GameObject Instance { get; }
        public string ItemId { get; }
    }

    private readonly struct CollarPlacement
    {
        public CollarPlacement(Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            Parent = parent;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public Transform Parent { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    public void Initialize(PawPalGameRuntime gameRuntime)
    {
        if (runtime == gameRuntime)
        {
            RefreshSceneBindings();
            EnsurePlayerToyThrowController();
            return;
        }

        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        runtime = gameRuntime;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        RefreshSceneBindings();
        EnsurePlayerToyThrowController();
    }

    private void OnDestroy()
    {
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void RefreshSceneBindings()
    {
        if (runtime == null)
        {
            return;
        }

        EnsurePlayerToyThrowController();

        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (agents == null || agents.Length == 0)
        {
            currentPresentationAgent = null;
            ClearAllRuntimeCollars();
            return;
        }

        currentPresentationAgent = null;

        if (agents.Length == 1)
        {
            RefreshSingleAgentPresentation(agents[0]);
            return;
        }

        Dictionary<string, DogRoomAgent> agentsByDogId = new Dictionary<string, DogRoomAgent>(StringComparer.OrdinalIgnoreCase);
        List<DogRoomAgent> fallbackAgents = new List<DogRoomAgent>();
        HashSet<DogRoomAgent> assignedAgents = new HashSet<DogRoomAgent>();
        string activeDogId = runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;

        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (agent == null)
            {
                continue;
            }

            if (agent.HasExplicitDogId)
            {
                if (agentsByDogId.ContainsKey(agent.DogId))
                {
                    WarnOnce(
                        "duplicate_scene_dog_" + agent.DogId,
                        "Multiple DogRoomAgent objects are bound to dogId '" + agent.DogId + "'. PawPal will use the first one it found.");
                    continue;
                }

                agentsByDogId.Add(agent.DogId, agent);
            }
            else
            {
                fallbackAgents.Add(agent);
            }
        }

        HashSet<string> liveDogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, DogRoomAgent> pair in agentsByDogId)
        {
            if (!RuntimeContainsDogId(pair.Key))
            {
                runtime.TryEnsureDogForSceneBinding(pair.Key);
            }
        }

        int fallbackIndex = 0;
        for (int i = 0; i < runtime.Dogs.Count; i++)
        {
            PawPalDogState dogState = runtime.Dogs[i];
            if (dogState == null)
            {
                continue;
            }

            DogRoomAgent agent = null;
            if (dogState.Id != null && agentsByDogId.TryGetValue(dogState.Id, out agent))
            {
                assignedAgents.Add(agent);
            }
            else
            {
                while (fallbackIndex < fallbackAgents.Count && assignedAgents.Contains(fallbackAgents[fallbackIndex]))
                {
                    fallbackIndex++;
                }

                if (fallbackIndex < fallbackAgents.Count)
                {
                    agent = fallbackAgents[fallbackIndex];
                    assignedAgents.Add(agent);
                    fallbackIndex++;
                    WarnOnce(
                        "fallback_scene_dog_" + dogState.Id,
                        "Dog '" + dogState.Id + "' is using fallback scene binding by object order. Add a matching dogId on the DogRoomAgent to make this stable.");
                }
            }

            if (agent == null)
            {
                WarnOnce(
                    "missing_scene_dog_" + dogState.Id,
                    "No DogRoomAgent was found for runtime dog '" + dogState.Id + "'. Collar visuals for this dog will be skipped.");
                RemoveCollarInstance(dogState.Id);
                continue;
            }

            liveDogIds.Add(dogState.Id);
            SyncCollarForDog(agent, dogState);

            if (string.Equals(dogState.Id, activeDogId, StringComparison.OrdinalIgnoreCase))
            {
                currentPresentationAgent = agent;
            }
        }

        foreach (KeyValuePair<string, DogRoomAgent> pair in agentsByDogId)
        {
            if (!RuntimeContainsDogId(pair.Key))
            {
                WarnOnce(
                    "orphan_scene_dog_" + pair.Key,
                    "Scene DogRoomAgent '" + pair.Value.name + "' is bound to dogId '" + pair.Key + "', but no runtime dog currently uses that id.");
            }
        }

        RemoveOrphanedCollars(liveDogIds);

        if (currentPresentationAgent == null)
        {
            foreach (DogRoomAgent assignedAgent in assignedAgents)
            {
                if (assignedAgent != null)
                {
                    currentPresentationAgent = assignedAgent;
                    break;
                }
            }
        }

        if (currentPresentationAgent == null && agents.Length > 0)
        {
            currentPresentationAgent = agents[0];
        }

        ApplyPresentationState(agents);
    }

    public bool TrySpawnToy(PawPalCatalogItemDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.RoomPrefabResourcePath))
        {
            return false;
        }

        PruneDestroyedToys();
        if (!EnsureSpawnCapacity())
        {
            Debug.Log("PawPalDogSceneBridge could not spawn another toy because all active toys are still in use.");
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>(definition.RoomPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("PawPalDogSceneBridge could not load toy prefab at Resources path '" + definition.RoomPrefabResourcePath + "'.");
            return false;
        }

        GameObject spawnedToy = Instantiate(prefab);
        spawnedToy.name = prefab.name;
        PawPalToyRuntimeMetadata metadata = spawnedToy.GetComponent<PawPalToyRuntimeMetadata>();
        if (metadata == null)
        {
            metadata = spawnedToy.AddComponent<PawPalToyRuntimeMetadata>();
        }

        metadata.Initialize(definition.Id, definition.ToyInteractionMode);
        ApplyInstanceTint(spawnedToy, definition.ToyTint);
        spawnedToy.transform.rotation = Quaternion.identity;
        EnsureDynamicToyCollider(spawnedToy);
        spawnedToy.transform.position = ResolveToySpawnPosition(spawnedToy, GetToySpawnPosition());
        if (definition.ToyInteractionMode == PawPalToyInteractionMode.PawHitRoll)
        {
            ConfigureLargeToyNavigationBlocker(spawnedToy, metadata);
        }

        Rigidbody body = spawnedToy.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = spawnedToy.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = definition.ToyInteractionMode == PawPalToyInteractionMode.PawHitRoll ? 0.9f : 0.35f;
        body.linearDamping = definition.ToyInteractionMode == PawPalToyInteractionMode.PawHitRoll ? 0.14f : 0f;
        body.angularDamping = definition.ToyInteractionMode == PawPalToyInteractionMode.PawHitRoll ? 0.26f : 0.05f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        TrySetToyTag(spawnedToy);
        activeSpawnedToys.Add(new SpawnedToyRecord(spawnedToy, definition.Id));
        return true;
    }

    public bool IsToyActiveInScene(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        PruneDestroyedToys();
        for (int i = 0; i < activeSpawnedToys.Count; i++)
        {
            SpawnedToyRecord record = activeSpawnedToys[i];
            if (record != null
                && record.Instance != null
                && string.Equals(record.ItemId, itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRemoveToyFromScene(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        PruneDestroyedToys();
        bool removed = false;
        for (int i = activeSpawnedToys.Count - 1; i >= 0; i--)
        {
            SpawnedToyRecord record = activeSpawnedToys[i];
            if (record == null || record.Instance == null)
            {
                activeSpawnedToys.RemoveAt(i);
                continue;
            }

            if (!string.Equals(record.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            GameObject toy = record.Instance;
            activeSpawnedToys.RemoveAt(i);
            Destroy(toy);
            removed = true;
        }

        return removed;
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshSceneBindings();
        EnsurePlayerToyThrowController();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        warningKeys.Clear();
        PruneDestroyedToys();
        currentPresentationAgent = null;
        DestroyFallbackToyFloor();
        ClearAllRuntimeCollars();
        RefreshSceneBindings();
        EnsurePlayerToyThrowController();
    }

    private void EnsurePlayerToyThrowController()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        if (mainCamera.GetComponent<PawPalPlayerToyThrowController>() == null)
        {
            mainCamera.gameObject.AddComponent<PawPalPlayerToyThrowController>();
        }

        EnsureDogCycleCamera(mainCamera);
    }

    private static void EnsureDogCycleCamera(Camera mainCamera)
    {
        if (mainCamera == null || mainCamera.GetComponent<DogCycleCamera>() != null)
        {
            return;
        }

        if (mainCamera.GetComponent<CameraFollow>() != null)
        {
            return;
        }

        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (agents == null || agents.Length == 0)
        {
            return;
        }

        mainCamera.gameObject.AddComponent<DogCycleCamera>();
    }

    private void RefreshSingleAgentPresentation(DogRoomAgent agent)
    {
        currentPresentationAgent = agent;
        SetDogPresentation(agent, true);

        PawPalDogState presentedDog = runtime != null ? runtime.ActiveDog : null;
        if (presentedDog == null && runtime != null && runtime.Dogs.Count > 0)
        {
            presentedDog = runtime.Dogs[0];
        }

        HashSet<string> liveDogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (presentedDog != null)
        {
            liveDogIds.Add(presentedDog.Id);
            SyncCollarForDog(agent, presentedDog);
        }

        RemoveOrphanedCollars(liveDogIds);
    }

    private void SyncCollarForDog(DogRoomAgent agent, PawPalDogState dogState)
    {
        HideBuiltInCollarSources(agent);

        string equippedCollarItemId = runtime.GetEquippedCollarItemId(dogState.Id);
        if (string.IsNullOrEmpty(equippedCollarItemId))
        {
            RemoveCollarInstance(dogState.Id);
            return;
        }

        PawPalCatalogItemDefinition item = runtime.GetCatalogItem(equippedCollarItemId);
        if (item == null || string.IsNullOrEmpty(item.CollarPrefabResourcePath))
        {
            RemoveCollarInstance(dogState.Id);
            return;
        }

        CollarPlacement placement = ResolveCollarPlacement(agent, item);
        if (placement.Parent == null)
        {
            RemoveCollarInstance(dogState.Id);
            return;
        }

        GameObject currentInstance;
        collarInstancesByDogId.TryGetValue(dogState.Id, out currentInstance);
        if (currentInstance == null || currentInstance.name != RuntimeCollarName + "_" + item.Id)
        {
            RemoveCollarInstance(dogState.Id);
            currentInstance = CreateCollarInstance(agent, item, placement.Parent);
            if (currentInstance == null)
            {
                return;
            }

            collarInstancesByDogId[dogState.Id] = currentInstance;
        }

        currentInstance.transform.SetParent(placement.Parent, false);
        currentInstance.transform.localPosition = placement.LocalPosition;
        currentInstance.transform.localRotation = placement.LocalRotation;
        currentInstance.transform.localScale = placement.LocalScale;
        currentInstance.SetActive(true);
    }

    private GameObject CreateCollarInstance(DogRoomAgent agent, PawPalCatalogItemDefinition item, Transform collarAnchor)
    {
        GameObject prefab = Resources.Load<GameObject>(item.CollarPrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("PawPalDogSceneBridge could not load collar prefab at Resources path '" + item.CollarPrefabResourcePath + "'.");
            return null;
        }

        GameObject instance = Instantiate(prefab, collarAnchor, false);
        instance.name = RuntimeCollarName + "_" + item.Id;
        SetLayerRecursively(instance.transform, agent.gameObject.layer);
        ApplyInstanceTint(instance, item.CollarTint);
        return instance;
    }

    private static void ApplyInstanceTint(GameObject instance, Color tint)
    {
        if (instance == null)
        {
            return;
        }

        if (ApproximatelyWhite(tint))
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            Material[] tintedMaterials = new Material[sharedMaterials.Length];
            bool anyTinted = false;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = sharedMaterials[materialIndex];
                if (sourceMaterial == null)
                {
                    tintedMaterials[materialIndex] = null;
                    continue;
                }

                Material clonedMaterial = new Material(sourceMaterial);
                if (clonedMaterial.HasProperty("_BaseColor"))
                {
                    clonedMaterial.SetColor("_BaseColor", tint);
                    anyTinted = true;
                }

                if (clonedMaterial.HasProperty("_Color"))
                {
                    clonedMaterial.SetColor("_Color", tint);
                    anyTinted = true;
                }

                tintedMaterials[materialIndex] = clonedMaterial;
            }

            if (anyTinted)
            {
                renderer.materials = tintedMaterials;
            }
        }
    }

    private static bool ApproximatelyWhite(Color tint)
    {
        return Mathf.Abs(tint.r - 1f) < 0.0001f
            && Mathf.Abs(tint.g - 1f) < 0.0001f
            && Mathf.Abs(tint.b - 1f) < 0.0001f
            && Mathf.Abs(tint.a - 1f) < 0.0001f;
    }

    private CollarPlacement ResolveCollarPlacement(DogRoomAgent agent, PawPalCatalogItemDefinition item)
    {
        if (agent == null)
        {
            return new CollarPlacement(transform, item.CollarLocalPosition, item.CollarLocalRotation, item.CollarLocalScale);
        }

        if (agent.CollarAnchor != null)
        {
            return new CollarPlacement(agent.CollarAnchor, item.CollarLocalPosition, item.CollarLocalRotation, item.CollarLocalScale);
        }

        Transform placementReference;
        if (TryFindExistingCollarPlacementReference(agent, out placementReference))
        {
            Transform parent = placementReference.parent != null ? placementReference.parent : agent.transform;
            return new CollarPlacement(
                parent,
                placementReference.localPosition,
                placementReference.localRotation,
                placementReference.localScale);
        }

        Transform neck = FindNamedTransform(agent, "neck", "Neck");
        if (neck != null)
        {
            Transform head = FindHeadTransform(neck, agent);
            float scaleMultiplier = CalculateFallbackCollarScaleMultiplier(neck, head);
            if (head != null)
            {
                Vector3 headLocalPosition = neck.InverseTransformPoint(head.position);
                float headDistance = headLocalPosition.magnitude;
                if (headDistance > 0.0001f)
                {
                    Vector3 localPosition = headLocalPosition * ReferenceCollarHeadOffsetFactor;
                    return new CollarPlacement(
                        neck,
                        localPosition,
                        item.CollarLocalRotation,
                        item.CollarLocalScale * scaleMultiplier);
                }
            }

            return new CollarPlacement(
                neck,
                item.CollarLocalPosition,
                item.CollarLocalRotation,
                item.CollarLocalScale * scaleMultiplier);
        }

        Transform fallbackAnchor = ResolveFallbackCollarAnchor(agent);
        return new CollarPlacement(fallbackAnchor, item.CollarLocalPosition, item.CollarLocalRotation, item.CollarLocalScale);
    }

    private Transform ResolveFallbackCollarAnchor(DogRoomAgent agent)
    {
        Transform[] children = agent.GetComponentsInChildren<Transform>(true);
        Transform containsMatch = null;

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            string candidateName = candidate.name;

            for (int j = 0; j < PreferredCollarAnchorNames.Length; j++)
            {
                string preferredName = PreferredCollarAnchorNames[j];
                if (string.Equals(candidateName, preferredName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (containsMatch == null && candidateName.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatch = candidate;
                }
            }
        }

        if (containsMatch != null)
        {
            return containsMatch;
        }

        WarnOnce(
            "collar_root_anchor_" + agent.GetInstanceID(),
            "DogRoomAgent '" + agent.name + "' has no explicit collar anchor and no neck/accessory fallback was found. Using the root transform.");
        return agent.transform;
    }

    private void HideBuiltInCollarSources(DogRoomAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        Transform[] children = agent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || IsRuntimeCollarTransform(candidate))
            {
                continue;
            }

            if (!IsBuiltInCollarReferenceName(candidate.name))
            {
                continue;
            }

            if (candidate.gameObject.activeSelf)
            {
                candidate.gameObject.SetActive(false);
            }
        }
    }

    private bool TryFindExistingCollarPlacementReference(DogRoomAgent agent, out Transform placementReference)
    {
        placementReference = null;
        if (agent == null)
        {
            return false;
        }

        Transform containsMatch = null;
        Transform[] children = agent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || IsRuntimeCollarTransform(candidate))
            {
                continue;
            }

            string candidateName = candidate.name;
            for (int j = 0; j < PreferredCollarPlacementReferenceNames.Length; j++)
            {
                string preferredName = PreferredCollarPlacementReferenceNames[j];
                if (string.Equals(candidateName, preferredName, StringComparison.OrdinalIgnoreCase))
                {
                    placementReference = candidate;
                    return true;
                }

                if (containsMatch == null && candidateName.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatch = candidate;
                }
            }
        }

        placementReference = containsMatch;
        return placementReference != null;
    }

    private Transform FindHeadTransform(Transform neck, DogRoomAgent agent)
    {
        if (neck == null)
        {
            return null;
        }

        Transform[] descendants = neck.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate == null || candidate == neck)
            {
                continue;
            }

            if (string.Equals(candidate.name, "head", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return FindNamedTransform(agent, "head", "Head");
    }

    private Transform FindNamedTransform(DogRoomAgent agent, params string[] names)
    {
        if (agent == null || names == null || names.Length == 0)
        {
            return null;
        }

        Transform[] children = agent.GetComponentsInChildren<Transform>(true);
        Transform containsMatch = null;

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || IsRuntimeCollarTransform(candidate))
            {
                continue;
            }

            string candidateName = candidate.name;
            for (int j = 0; j < names.Length; j++)
            {
                string expectedName = names[j];
                if (string.Equals(candidateName, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (containsMatch == null && candidateName.IndexOf(expectedName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatch = candidate;
                }
            }
        }

        return containsMatch;
    }

    private float CalculateFallbackCollarScaleMultiplier(Transform neck, Transform head)
    {
        if (neck == null)
        {
            return 1f;
        }

        float sizeProxy = 0f;
        if (neck.parent != null)
        {
            sizeProxy = Vector3.Distance(neck.position, neck.parent.position);
        }

        if (sizeProxy <= 0.0001f && head != null)
        {
            sizeProxy = Vector3.Distance(neck.position, head.position);
        }

        if (sizeProxy <= 0.0001f)
        {
            return 1f;
        }

        float normalized = sizeProxy / ReferenceCollarNeckDistance;
        float scaled = Mathf.Pow(Mathf.Max(normalized, 0.0001f), FallbackCollarScaleExponent);
        return Mathf.Clamp(scaled, MinimumCollarScaleMultiplier, MaximumCollarScaleMultiplier);
    }

    private bool IsBuiltInCollarReferenceName(string candidateName)
    {
        if (string.IsNullOrEmpty(candidateName))
        {
            return false;
        }

        for (int i = 0; i < PreferredCollarPlacementReferenceNames.Length; i++)
        {
            string expectedName = PreferredCollarPlacementReferenceNames[i];
            if (string.Equals(candidateName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyPresentationState(DogRoomAgent[] agents)
    {
        if (agents == null)
        {
            return;
        }

        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (agent == null)
            {
                continue;
            }

            SetDogPresentation(agent, true);
        }
    }

    private void SetDogPresentation(DogRoomAgent agent, bool isVisible)
    {
        if (agent == null)
        {
            return;
        }

        if (agent.enabled != isVisible)
        {
            agent.enabled = isVisible;
        }

        Renderer[] renderers = agent.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled != isVisible)
            {
                renderer.enabled = isVisible;
            }
        }
    }

    private bool IsRuntimeCollarTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform current = candidate;
        while (current != null)
        {
            if (current.name.StartsWith(RuntimeCollarName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void RemoveOrphanedCollars(HashSet<string> liveDogIds)
    {
        List<string> dogIdsToRemove = new List<string>();
        foreach (KeyValuePair<string, GameObject> pair in collarInstancesByDogId)
        {
            if (!liveDogIds.Contains(pair.Key))
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }

                dogIdsToRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < dogIdsToRemove.Count; i++)
        {
            collarInstancesByDogId.Remove(dogIdsToRemove[i]);
        }
    }

    private void RemoveCollarInstance(string dogId)
    {
        GameObject instance;
        if (!collarInstancesByDogId.TryGetValue(dogId, out instance))
        {
            return;
        }

        if (instance != null)
        {
            Destroy(instance);
        }

        collarInstancesByDogId.Remove(dogId);
    }

    private void ClearAllRuntimeCollars()
    {
        foreach (KeyValuePair<string, GameObject> pair in collarInstancesByDogId)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        collarInstancesByDogId.Clear();
    }

    private bool EnsureSpawnCapacity()
    {
        while (activeSpawnedToys.Count >= MaxActiveSpawnedToys)
        {
            int removableIndex = FindOldestRemovableToyIndex();
            if (removableIndex < 0)
            {
                return false;
            }

            SpawnedToyRecord removableRecord = activeSpawnedToys[removableIndex];
            activeSpawnedToys.RemoveAt(removableIndex);
            if (removableRecord != null && removableRecord.Instance != null)
            {
                Destroy(removableRecord.Instance);
            }
        }

        return true;
    }

    private int FindOldestRemovableToyIndex()
    {
        for (int i = 0; i < activeSpawnedToys.Count; i++)
        {
            SpawnedToyRecord record = activeSpawnedToys[i];
            if (record == null || record.Instance == null)
            {
                return i;
            }

            if (record.Instance.GetComponentInParent<DogRoomAgent>() == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void PruneDestroyedToys()
    {
        for (int i = activeSpawnedToys.Count - 1; i >= 0; i--)
        {
            SpawnedToyRecord record = activeSpawnedToys[i];
            if (record == null || record.Instance == null)
            {
                activeSpawnedToys.RemoveAt(i);
            }
        }
    }

    private Vector3 GetToySpawnPosition()
    {
        if (currentPresentationAgent != null)
        {
            return currentPresentationAgent.transform.position + currentPresentationAgent.transform.forward * 0.55f + Vector3.up * 1.05f;
        }

        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (agents != null && agents.Length > 0)
        {
            int activeIndex = Mathf.Clamp(0, 0, agents.Length - 1);
            if (runtime != null && runtime.ActiveDog != null && runtime.Dogs.Count > 1)
            {
                for (int i = 0; i < runtime.Dogs.Count; i++)
                {
                    if (runtime.Dogs[i].Id == runtime.ActiveDog.Id)
                    {
                        activeIndex = Mathf.Clamp(i, 0, agents.Length - 1);
                        break;
                    }
                }
            }

            DogRoomAgent activeAgent = agents[activeIndex];
            if (activeAgent != null)
            {
                return activeAgent.transform.position + activeAgent.transform.forward * 0.55f + Vector3.up * 1.05f;
            }
        }

        return new Vector3(0f, 1f, 0f);
    }

    private void TrySetToyTag(GameObject toy)
    {
        try
        {
            toy.tag = "Toy";
        }
        catch (UnityException)
        {
            // The project may not have the Toy tag in every scene context; keyword pickup still works.
        }
    }

    private Vector3 ResolveToySpawnPosition(GameObject spawnedToy, Vector3 requestedPosition)
    {
        float bottomOffset = GetToyBottomOffset(spawnedToy);
        RaycastHit hit;
        Vector3 rayOrigin = requestedPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * (bottomOffset + 0.04f);
        }

        BoxCollider supportFloor = EnsureFallbackToyFloor(requestedPosition);
        if (supportFloor != null)
        {
            Bounds bounds = supportFloor.bounds;
            return new Vector3(requestedPosition.x, bounds.max.y + bottomOffset + 0.04f, requestedPosition.z);
        }

        return requestedPosition;
    }

    private static float GetToyBottomOffset(GameObject spawnedToy)
    {
        if (spawnedToy == null)
        {
            return 0.05f;
        }

        Collider[] colliders = spawnedToy.GetComponentsInChildren<Collider>(true);
        Bounds combinedBounds = new Bounds(spawnedToy.transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            Renderer[] renderers = spawnedToy.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds ? Mathf.Max(0.02f, combinedBounds.extents.y) : 0.05f;
    }

    private static void EnsureDynamicToyCollider(GameObject spawnedToy)
    {
        if (spawnedToy == null)
        {
            return;
        }

        Collider[] colliders = spawnedToy.GetComponentsInChildren<Collider>(true);
        bool hasSolidCollider = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.enabled = true;
            collider.isTrigger = false;

            MeshCollider meshCollider = collider as MeshCollider;
            if (meshCollider != null)
            {
                meshCollider.convex = true;
            }

            hasSolidCollider = true;
        }

        if (!hasSolidCollider)
        {
            BoxCollider fallbackCollider = spawnedToy.GetComponent<BoxCollider>();
            if (fallbackCollider == null)
            {
                fallbackCollider = spawnedToy.AddComponent<BoxCollider>();
            }

            fallbackCollider.isTrigger = false;
            fallbackCollider.enabled = true;
        }
    }

    private static void ConfigureLargeToyNavigationBlocker(GameObject spawnedToy, PawPalToyRuntimeMetadata metadata)
    {
        if (spawnedToy == null)
        {
            return;
        }

        Bounds bounds;
        float radius = TryGetToyBlockingBounds(spawnedToy, out bounds)
            ? Mathf.Max(0.18f, Mathf.Max(bounds.extents.x, bounds.extents.z))
            : 0.35f;
        float paddedRadius = radius + 0.18f;

        if (metadata != null)
        {
            metadata.ConfigureLargeToyPhysicsAndNavigation();
            metadata.SetBlocksDogNavigation(true, paddedRadius);
        }

        PawPalBallBounceAudio.EnsureOn(spawnedToy);

        NavMeshObstacle obstacle = spawnedToy.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = spawnedToy.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = paddedRadius;
        obstacle.height = TryGetToyBlockingBounds(spawnedToy, out bounds)
            ? Mathf.Max(0.2f, bounds.size.y)
            : Mathf.Max(0.2f, paddedRadius * 2f);
        obstacle.center = Vector3.zero;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingMoveThreshold = 0.08f;
        obstacle.carvingTimeToStationary = 0.15f;
    }

    private static bool TryGetToyBlockingBounds(GameObject spawnedToy, out Bounds bounds)
    {
        bounds = spawnedToy != null ? new Bounds(spawnedToy.transform.position, Vector3.zero) : new Bounds();
        if (spawnedToy == null)
        {
            return false;
        }

        bool hasBounds = false;
        Collider[] colliders = spawnedToy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = spawnedToy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private BoxCollider EnsureFallbackToyFloor(Vector3 nearPosition)
    {
        if (fallbackToyFloor == null)
        {
            GameObject floorObject = new GameObject("PawPalToySupportFloor");
            floorObject.hideFlags = HideFlags.HideAndDontSave;
            fallbackToyFloor = floorObject.AddComponent<BoxCollider>();
        }

        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        Vector3 center = nearPosition;
        Vector3 min = nearPosition;
        Vector3 max = nearPosition;

        if (agents != null && agents.Length > 0)
        {
            center = Vector3.zero;
            for (int i = 0; i < agents.Length; i++)
            {
                DogRoomAgent agent = agents[i];
                if (agent == null)
                {
                    continue;
                }

                Vector3 position = agent.transform.position;
                center += position;
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            center /= Mathf.Max(1, agents.Length);
        }

        float width = Mathf.Max(8f, (max.x - min.x) + 6f);
        float depth = Mathf.Max(8f, (max.z - min.z) + 6f);
        float floorY = Mathf.Min(min.y, nearPosition.y) - 0.08f;

        fallbackToyFloor.transform.position = new Vector3(center.x, floorY, center.z);
        fallbackToyFloor.size = new Vector3(width, 0.2f, depth);
        fallbackToyFloor.center = Vector3.zero;
        fallbackToyFloor.enabled = true;
        return fallbackToyFloor;
    }

    private void DestroyFallbackToyFloor()
    {
        if (fallbackToyFloor == null)
        {
            return;
        }

        Destroy(fallbackToyFloor.gameObject);
        fallbackToyFloor = null;
    }

    private bool RuntimeContainsDogId(string dogId)
    {
        if (runtime == null || string.IsNullOrEmpty(dogId))
        {
            return false;
        }

        for (int i = 0; i < runtime.Dogs.Count; i++)
        {
            PawPalDogState dog = runtime.Dogs[i];
            if (dog != null && string.Equals(dog.Id, dogId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void WarnOnce(string key, string message)
    {
        if (!warningKeys.Add(key))
        {
            return;
        }

        Debug.LogWarning("PawPalDogSceneBridge: " + message);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
