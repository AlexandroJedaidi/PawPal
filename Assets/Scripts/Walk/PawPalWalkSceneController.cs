using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PawPalWalkSceneController : MonoBehaviour
{
    private enum WalkState
    {
        Running,
        PausedForStop,
        Completing
    }

    private sealed class EncounterVisitorPet
    {
        public GameObject GameObject;
        public Transform Root;
        public Animator Animator;
        public DogRoomAgent DogAgent;
        public PawPalCatRoomAgent CatAgent;
        public IntroPetDefinition Definition;
        public PawPalPetMovementProfile MovementProfile;
        public IntroPetSpecies Species;
        public string RuntimeBreed;
    }

    private const float GroundProbeHeight = 8f;
    private const float GroundProbeDistance = 24f;
    private const float WalkMusicVolume = 0.1875f;
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");

    private readonly List<Vector3> routePoints = new List<Vector3>();
    private readonly List<Collider> allowedRoads = new List<Collider>();

    private PawPalWalkSessionSaveData session;
    private PawPalGameRuntime runtime;
    private PawPalWalkingSceneBindings sceneBindings;
    private PawPalDogState runtimeDog;
    private SelectedPetSessionData runtimeDogSelection;
    private Transform walker;
    private DogRoomAgent walkerRoomAgent;
    private PawPalCatRoomAgent walkerCatAgent;
    private Animator walkerAnimator;
    private Camera sceneCamera;
    private PawPalWalkLeashDirector leashDirector;
    private PawPalWalkPetGraphicsEnhancer petGraphicsEnhancer;
    private AudioSource walkMusicSource;
    private AudioClip walkMusicClip;
    private float[] cumulativeRouteDistances = new float[0];
    private PawPalPetMovementProfile walkMovementProfile = PawPalPetMovementProfiles.DefaultProfile;
    private IntroPetSpecies activePetSpecies = IntroPetSpecies.Dog;
    private float totalRouteDistance = 1f;
    private float currentDistance;
    private float progress;
    private float routeSpeedScale = 1f;
    private string returnSceneName = PawPalWalkSceneFlow.HomeSceneName;
    private WalkState state;
    private Coroutine stateRoutine;
    private string startupFailureMessage = string.Empty;
    private bool startupFailed;
    private bool cameraLockedForEncounter;
    private Vector3 lockedCameraPosition;
    private Quaternion lockedCameraRotation;

    public Vector3 CurrentRouteDirection => GetRouteDirectionAtDistance(currentDistance);
    public Camera CurrentWalkCamera => sceneCamera;

    public void Initialize(PawPalWalkSessionSaveData walkSession)
    {
        session = walkSession;
    }

    private void OnEnable()
    {
        PawPalAudioSettings.MusicVolumeChanged += HandleMusicVolumeChanged;
    }

    private void OnDisable()
    {
        PawPalAudioSettings.MusicVolumeChanged -= HandleMusicVolumeChanged;
        StopWalkMusic();
    }

    private void Start()
    {
        runtime = PawPalGameRuntime.Instance;
        if (session == null && runtime != null)
        {
            session = runtime.ActiveWalkSession;
        }

        if (session == null)
        {
            FailStartup("No active walk session was found.");
            return;
        }

        returnSceneName = string.IsNullOrEmpty(session.ReturnSceneName)
            ? PawPalWalkSceneFlow.HomeSceneName
            : session.ReturnSceneName;

        sceneBindings = PawPalWalkingSceneBindings.FindInSceneOrCreateRuntimeFallback();
        sceneBindings.AutoAssignAllowedRoadsFromScene();
        sceneBindings.GetResolvedAllowedRoads(allowedRoads);

        if (!ResolveRoutePlan())
        {
            FailStartup(string.IsNullOrEmpty(startupFailureMessage)
                ? "The walking scene could not resolve a route from the authored graph."
                : startupFailureMessage);
            return;
        }

        if (!BuildRouteDefinition())
        {
            FailStartup("The walking scene route path is empty or invalid.");
            return;
        }

        if (!ResolveOrSpawnWalker())
        {
            FailStartup(string.IsNullOrEmpty(startupFailureMessage)
                ? "The selected dog could not be resolved or spawned."
                : startupFailureMessage);
            return;
        }

        PawPalWalkSceneFlow.SetAppShellVisible(false);
        ConfigureWalkerForWalk();
        EnsureWalkMusic();
        progress = Mathf.Clamp01(session.LastProgress);
        currentDistance = Mathf.Clamp01(progress) * totalRouteDistance;
        MoveWalkerToDistance(currentDistance, true);
        ConfigureSceneCamera();
        ConfigureWalkPetGraphics();
        PawPalLeashRig.TryInstallSceneRig();
        ConfigureWalkLeashDirector();
        state = WalkState.Running;
        PushProgressToRuntime();
    }

    private void EnsureWalkMusic()
    {
        if (walkMusicSource == null)
        {
            walkMusicSource = GetComponent<AudioSource>();
            if (walkMusicSource == null)
            {
                walkMusicSource = gameObject.AddComponent<AudioSource>();
            }

            walkMusicSource.playOnAwake = false;
            walkMusicSource.loop = true;
            walkMusicSource.spatialBlend = 0f;
            walkMusicSource.ignoreListenerPause = true;
        }

        if (walkMusicClip == null)
        {
            walkMusicClip = PawPalAudioResources.LoadClip(PawPalAudioResources.WalkingTheme);
            if (walkMusicClip == null)
            {
                Debug.LogWarning("PawPalWalkSceneController could not load walk music clip '" + PawPalAudioResources.WalkingTheme + "'.");
                return;
            }
        }

        walkMusicSource.clip = walkMusicClip;
        ApplyWalkMusicVolume();
        if (!walkMusicSource.isPlaying)
        {
            walkMusicSource.Play();
        }
    }

    private void StopWalkMusic()
    {
        if (walkMusicSource != null && walkMusicSource.isPlaying)
        {
            walkMusicSource.Stop();
        }
    }

    private void HandleMusicVolumeChanged()
    {
        ApplyWalkMusicVolume();
    }

    private void ApplyWalkMusicVolume()
    {
        if (walkMusicSource != null)
        {
            walkMusicSource.volume = PawPalAudioSettings.ApplyMusicVolume(WalkMusicVolume);
        }
    }

    private void Update()
    {
        if (startupFailed || session == null || state != WalkState.Running)
        {
            return;
        }

        if (leashDirector != null && leashDirector.BlocksRouteProgress)
        {
            MoveWalkerToDistance(currentDistance, false);
            return;
        }

        currentDistance = Mathf.Min(totalRouteDistance, currentDistance + ResolveCurrentRouteSpeed() * Time.deltaTime);
        MoveWalkerToDistance(currentDistance, false);
        progress = totalRouteDistance <= 0.001f ? 1f : Mathf.Clamp01(currentDistance / totalRouteDistance);
        PushProgressToRuntime();

        PawPalWalkGeneratedEventState nextEvent = GetNextPendingEvent();
        if (nextEvent != null && progress >= nextEvent.Progress && stateRoutine == null)
        {
            stateRoutine = StartCoroutine(PlayEncounterRoutine(nextEvent));
            return;
        }

        if (currentDistance >= totalRouteDistance - 0.02f)
        {
            BeginCompleteRoutine();
        }
    }

    private void LateUpdate()
    {
        if (startupFailed)
        {
            return;
        }

        if (cameraLockedForEncounter)
        {
            ApplyLockedEncounterCamera();
            return;
        }

        PositionCamera(false);
    }

    private void OnApplicationPause(bool pausedStatus)
    {
        if (!pausedStatus || runtime == null)
        {
            return;
        }

        PushProgressToRuntime();
        runtime.SaveProfile();
    }

    private bool ResolveRoutePlan()
    {
        if (runtime == null || session == null)
        {
            return false;
        }

        if (!session.DeferredGraphResolution && session.WorldPath != null && session.WorldPath.Count >= 2)
        {
            return true;
        }

        PawPalWalkRoutePlan routePlan;
        string failureMessage;
        if (HasRequestedVisitRoute(session))
        {
            if (!PawPalWalkGraphService.TryGetSceneGraphSnapshot(sceneBindings, out PawPalWalkGraphSnapshot snapshot, out failureMessage))
            {
                startupFailureMessage = failureMessage;
                Debug.LogWarning("PawPalWalkSceneController could not resolve the authored scene walk graph. " + failureMessage);
                return false;
            }

            string startNodeId = string.IsNullOrEmpty(session.StartNodeId) ? snapshot.DefaultStartNodeId : session.StartNodeId;
            string endNodeId = string.IsNullOrEmpty(session.EndNodeId) ? snapshot.DefaultEndNodeId : session.EndNodeId;
            if (!PawPalWalkGraphService.TryBuildPlanThroughVisitNodeIds(
                    snapshot,
                    startNodeId,
                    endNodeId,
                    session.RequestedVisitNodeIds,
                    out routePlan,
                    out failureMessage))
            {
                Debug.LogWarning("PawPalWalkSceneController could not build the selected map route in the walking scene, so it will use the default walking route. " + failureMessage);
                if (!PawPalWalkGraphService.TryBuildDefaultRoutePlan(sceneBindings, out routePlan, out failureMessage))
                {
                    startupFailureMessage = failureMessage;
                    Debug.LogWarning("PawPalWalkSceneController could not resolve the default scene walk route. " + failureMessage);
                    return false;
                }
            }
        }
        else if (!PawPalWalkGraphService.TryBuildDefaultRoutePlan(sceneBindings, out routePlan, out failureMessage))
        {
            startupFailureMessage = failureMessage;
            Debug.LogWarning("PawPalWalkSceneController could not resolve the default scene walk route. " + failureMessage);
            return false;
        }

        if (HasRequestedVisitRoute(session))
        {
            PawPalWalkGraphService.TryAddSceneEncounterPointsToPlan(sceneBindings, routePlan);
        }

        routePlan.ReturnSceneName = returnSceneName;
        if (!runtime.TryResolveActiveWalkSessionRoute(routePlan, out failureMessage))
        {
            startupFailureMessage = failureMessage;
            Debug.LogWarning("PawPalWalkSceneController could not activate the resolved walk route. " + failureMessage);
            return false;
        }

        session = runtime.ActiveWalkSession;
        return session != null;
    }

    private bool BuildRouteDefinition()
    {
        routePoints.Clear();
        if (session == null || session.WorldPath == null)
        {
            return false;
        }

        for (int i = 0; i < session.WorldPath.Count; i++)
        {
            PawPalWalkWorldPointData point = session.WorldPath[i];
            if (point != null)
            {
                routePoints.Add(HasGraphBackedRoute(session)
                    ? point.ToVector3()
                    : ProjectToAllowedRoad(point.ToVector3(), point.Y, true));
            }
        }

        RemoveDuplicateRoutePoints();
        if (routePoints.Count < 2)
        {
            return false;
        }

        cumulativeRouteDistances = new float[routePoints.Count];
        totalRouteDistance = 0f;
        for (int i = 1; i < routePoints.Count; i++)
        {
            totalRouteDistance += Vector3.Distance(routePoints[i - 1], routePoints[i]);
            cumulativeRouteDistances[i] = totalRouteDistance;
        }

        totalRouteDistance = Mathf.Max(0.001f, totalRouteDistance);
        return true;
    }

    private bool ResolveOrSpawnWalker()
    {
        runtimeDog = ResolveRuntimeDogState();
        EnsureWalkerHasCollarEquipped();
        runtimeDogSelection = BuildSelectionForDog(runtimeDog);
        if (runtimeDog == null)
        {
            startupFailureMessage = "No active dog was found in the runtime session.";
            return false;
        }

        DeactivateForeignScenePets();
        if (TryUsePreparedWalkDog())
        {
            return true;
        }

        if (runtimeDogSelection == null || runtimeDogSelection.Definition == null)
        {
            return false;
        }

        return TrySpawnRuntimeWalker();
    }

    private void EnsureWalkerHasCollarEquipped()
    {
        if (runtime == null || runtimeDog == null)
        {
            return;
        }

        runtime.TryAutoEquipOwnedCollar(runtimeDog.Id);
    }

    private bool TryUsePreparedWalkDog()
    {
        if (!PawPalWalkSceneFlow.TryConsumePreparedWalkDog(out GameObject preparedDog) || preparedDog == null)
        {
            return false;
        }

        walker = preparedDog.transform;
        walkerRoomAgent = activePetSpecies == IntroPetSpecies.Cat ? null : preparedDog.GetComponent<DogRoomAgent>();
        walkerCatAgent = preparedDog.GetComponent<PawPalCatRoomAgent>();
        if (walkerCatAgent == null)
        {
            walkerCatAgent = preparedDog.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        walkerAnimator = preparedDog.GetComponent<Animator>();
        if (walkerAnimator == null)
        {
            walkerAnimator = preparedDog.GetComponentInChildren<Animator>(true);
        }

        if (walkerRoomAgent != null && runtimeDog != null)
        {
            walkerRoomAgent.SetRuntimeDogId(runtimeDog.Id);
        }

        if (walkerCatAgent != null)
        {
            walkerCatAgent.SetExternalWalkControl(true);
        }

        return true;
    }

    private PawPalDogState ResolveRuntimeDogState()
    {
        if (runtime == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(session.SelectedDogId))
        {
            IReadOnlyList<PawPalDogState> dogs = runtime.Dogs;
            for (int i = 0; i < dogs.Count; i++)
            {
                PawPalDogState candidate = dogs[i];
                if (candidate != null
                    && string.Equals(candidate.Id, session.SelectedDogId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return runtime.ActiveDog;
    }

    private bool TrySpawnRuntimeWalker()
    {
        Vector3 spawnPosition = GetSpawnPosition();
        Vector3 initialDirection = GetRouteDirectionAtDistance(0f);
        if (initialDirection.sqrMagnitude <= 0.001f)
        {
            initialDirection = Vector3.right;
        }

        if (!TryInstantiateRuntimeDog(runtimeDogSelection, spawnPosition, Quaternion.LookRotation(initialDirection, Vector3.up), out GameObject dogObject))
        {
            return false;
        }

        walker = dogObject.transform;
        walkerRoomAgent = activePetSpecies == IntroPetSpecies.Cat ? null : dogObject.GetComponent<DogRoomAgent>();
        walkerCatAgent = dogObject.GetComponent<PawPalCatRoomAgent>();
        if (walkerCatAgent == null)
        {
            walkerCatAgent = dogObject.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        walkerAnimator = dogObject.GetComponent<Animator>();
        if (walkerAnimator == null)
        {
            walkerAnimator = dogObject.GetComponentInChildren<Animator>();
        }

        return true;
    }

    private bool TryInstantiateRuntimeDog(SelectedPetSessionData selection, Vector3 position, Quaternion rotation, out GameObject dogObject)
    {
        dogObject = null;
        if (selection == null || selection.Definition == null)
        {
            return false;
        }

        GameObject preferredPrefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        GameObject basePrefab = selection.Definition.BasePrefab;
        string preferredError;
        if (!PetVariantApplier.TryInstantiatePrefab(preferredPrefab != null ? preferredPrefab : basePrefab, position, rotation, null, out dogObject, out preferredError))
        {
            if (basePrefab == null || basePrefab == preferredPrefab)
            {
                startupFailureMessage = "The selected dog prefab could not be instantiated. " + preferredError;
                Debug.LogWarning("PawPalWalkSceneController could not spawn the selected dog '" + selection.SafeName + "'. " + preferredError);
                return false;
            }

            string baseError;
            if (!PetVariantApplier.TryInstantiatePrefab(basePrefab, position, rotation, null, out dogObject, out baseError))
            {
                startupFailureMessage = "The selected dog prefab and fallback prefab both failed to instantiate.";
                Debug.LogWarning("PawPalWalkSceneController could not spawn the selected dog '" + selection.SafeName + "'. " + preferredError + " " + baseError);
                return false;
            }

            if (selection.FurVariant != null)
            {
                PetVariantApplier.ApplyMaterial(dogObject, selection.FurVariant);
            }
        }

        dogObject.name = (selection.Species == IntroPetSpecies.Cat ? "WalkCat_" : "WalkDog_") + selection.SafeName;
        DogRoomAgent roomAgent = dogObject.GetComponent<DogRoomAgent>();
        if (selection.Species == IntroPetSpecies.Dog)
        {
            PawPalCatRoomAgent[] catAgents = dogObject.GetComponentsInChildren<PawPalCatRoomAgent>(true);
            for (int i = 0; i < catAgents.Length; i++)
            {
                if (catAgents[i] != null)
                {
                    Destroy(catAgents[i]);
                }
            }
        }
        else if (roomAgent != null)
        {
            Destroy(roomAgent);
            roomAgent = null;
        }

        ApplySpawnedDogPresentation(selection, dogObject);

        if (roomAgent != null)
        {
            roomAgent.SetRuntimeDogId(selection.RuntimePetId);
            roomAgent.ApplySelectedPetPresentation(selection);
            roomAgent.ConfigureSelectedPetRuntime(selection);
        }
        else if (selection.Species == IntroPetSpecies.Cat)
        {
            PawPalCatRoomAgent catAgent = dogObject.GetComponent<PawPalCatRoomAgent>();
            if (catAgent == null)
            {
                catAgent = dogObject.GetComponentInChildren<PawPalCatRoomAgent>(true);
            }

            if (catAgent == null)
            {
                catAgent = dogObject.AddComponent<PawPalCatRoomAgent>();
            }

            catAgent.Initialize(selection);
            catAgent.SetExternalWalkControl(true);
        }

        return true;
    }

    private static void ApplySpawnedDogPresentation(SelectedPetSessionData selection, GameObject dogObject)
    {
        if (selection == null || selection.Definition == null || dogObject == null)
        {
            return;
        }

        if (selection.Definition.HomeScale != Vector3.zero)
        {
            dogObject.transform.localScale = selection.Definition.HomeScale;
        }

        Animator targetAnimator = dogObject.GetComponentInChildren<Animator>(true);
        if (selection.Definition.AnimationSet != null)
        {
            selection.Definition.AnimationSet.ApplyTo(targetAnimator, selection.Definition);
        }

        if (selection.FurVariant != null && selection.FurVariant.ReplacementMaterial != null)
        {
            PetVariantApplier.ApplyMaterial(dogObject, selection.FurVariant);
        }

        PetVariantApplier.EnsureTapCollider(dogObject);
    }

    private SelectedPetSessionData BuildSelectionForDog(PawPalDogState dog)
    {
        if (dog == null)
        {
            return null;
        }

        activePetSpecies = runtime != null ? runtime.GetPetSpecies(dog.Id) : IntroPetSpecies.Dog;
        IntroPetDefinition definition = ResolveIntroPetDefinition(dog, activePetSpecies);
        if (definition == null)
        {
            startupFailureMessage = "Could not match runtime pet '" + dog.DisplayName + "' / breed '" + dog.Breed + "' to an IntroPetDefinition.";
            Debug.LogWarning("PawPalWalkSceneController could not resolve an IntroPetDefinition for runtime pet '" + dog.Id + "' / breed '" + dog.Breed + "'.");
            return null;
        }

        FurVariantDefinition furVariant = ResolveFurVariant(definition, dog.FurColor);
        return new SelectedPetSessionData
        {
            Definition = definition,
            FurVariant = furVariant,
            FurIndex = definition.GetFurVariantIndex(furVariant),
            Gender = dog.Gender,
            Personality = dog.Personality,
            PetName = string.IsNullOrWhiteSpace(dog.DisplayName) ? definition.SpeciesLabel : dog.DisplayName,
            RuntimePetId = dog.Id
        };
    }

    private static IntroPetDefinition ResolveIntroPetDefinition(PawPalDogState dog)
    {
        return ResolveIntroPetDefinition(dog, IntroPetSpecies.Dog);
    }

    public static IntroPetDefinition ResolveIntroPetDefinition(PawPalDogState dog, IntroPetSpecies species)
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        if (definitions == null || definitions.Length == 0 || dog == null)
        {
            return null;
        }

        string breedKey = NormalizeKey(dog.Breed);
        string idKey = NormalizeKey(dog.Id);
        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || definition.Species != species)
            {
                continue;
            }

            string petIdKey = NormalizeKey(definition.PetId);
            string breedLabelKey = NormalizeKey(definition.BreedLabel);
            string displayNameKey = NormalizeKey(definition.DisplayName);
            if (breedKey == breedLabelKey || breedKey == petIdKey || idKey == petIdKey || idKey == breedLabelKey || breedKey == displayNameKey)
            {
                return definition;
            }
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || definition.Species != species)
            {
                continue;
            }

            string candidate = NormalizeKey(definition.BreedLabel);
            if (!string.IsNullOrEmpty(candidate) && !string.IsNullOrEmpty(breedKey) && breedKey.Contains(candidate))
            {
                return definition;
            }
        }

        return null;
    }

    private static FurVariantDefinition ResolveFurVariant(IntroPetDefinition definition, string furColor)
    {
        if (definition == null || definition.FurVariants == null || definition.FurVariants.Length == 0)
        {
            return null;
        }

        string furKey = NormalizeKey(furColor);
        for (int i = 0; i < definition.FurVariants.Length; i++)
        {
            FurVariantDefinition variant = definition.FurVariants[i];
            if (variant == null)
            {
                continue;
            }

            string variantKey = NormalizeKey(variant.SafeDisplayName);
            if (variantKey == furKey || (!string.IsNullOrEmpty(furKey) && variantKey.Contains(furKey)))
            {
                return variant;
            }
        }

        return definition.GetDefaultFurVariant();
    }

    private void ConfigureWalkerForWalk()
    {
        if (walker == null)
        {
            return;
        }

        if (walkerAnimator == null)
        {
            walkerAnimator = walker.GetComponent<Animator>();
            if (walkerAnimator == null)
            {
                walkerAnimator = walker.GetComponentInChildren<Animator>();
            }
        }

        if (walkerRoomAgent == null && activePetSpecies != IntroPetSpecies.Cat)
        {
            walkerRoomAgent = walker.GetComponent<DogRoomAgent>();
        }

        if (walkerCatAgent == null)
        {
            walkerCatAgent = walker.GetComponent<PawPalCatRoomAgent>();
            if (walkerCatAgent == null)
            {
                walkerCatAgent = walker.GetComponentInChildren<PawPalCatRoomAgent>(true);
            }
        }

        NavMeshAgent navMeshAgent = walker.GetComponent<NavMeshAgent>();
        if (walkerRoomAgent != null)
        {
            walkerRoomAgent.SetExternalWalkControl(true);
            walkerRoomAgent.SetExternalWalkPace(DogMovementPace.Walk);
            walkMovementProfile = walkerRoomAgent.MovementProfile;
        }
        else if (walkerCatAgent != null)
        {
            walkerCatAgent.SetExternalWalkControl(true);
            walkerCatAgent.SetExternalWalkPace(DogMovementPace.Walk);
            walkMovementProfile = PawPalPetMovementProfiles.Resolve(
                runtimeDogSelection != null ? runtimeDogSelection.Definition : null,
                runtimeDog != null ? runtimeDog.Breed : null,
                walker.name);
        }
        else if (runtimeDog != null)
        {
            walkMovementProfile = PawPalPetMovementProfiles.Resolve(
                runtimeDogSelection != null ? runtimeDogSelection.Definition : null,
                runtimeDog.Breed,
                walker.name);
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.enabled = false;
        }

        DisableConflictingComponents(walker);
        routeSpeedScale = Mathf.Max(0.05f, sceneBindings.RunSpeedMultiplier);
    }

    private void ConfigureWalkLeashDirector()
    {
        PawPalLeashRig leashRig = Object.FindFirstObjectByType<PawPalLeashRig>(FindObjectsInactive.Include);
        PawPalLeashDragController leashDrag = Object.FindFirstObjectByType<PawPalLeashDragController>(FindObjectsInactive.Include);
        if (leashDrag == null && leashRig != null)
        {
            leashDrag = leashRig.GetComponent<PawPalLeashDragController>();
        }

        if (leashDrag == null || walker == null)
        {
            return;
        }

        leashDirector = GetComponent<PawPalWalkLeashDirector>();
        if (leashDirector == null)
        {
            leashDirector = gameObject.AddComponent<PawPalWalkLeashDirector>();
        }

        leashDirector.Configure(
            this,
            leashDrag,
            walkerAnimator,
            walkerRoomAgent,
            walkerCatAgent,
            activePetSpecies,
            runtimeDogSelection != null ? runtimeDogSelection.Definition : null,
            runtimeDog != null ? runtimeDog.Breed : string.Empty,
            walker.name);
    }

    private void ConfigureSceneCamera()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera authoredCamera = null;
        Camera existingRuntimeCamera = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name == "WalkSideCameraRuntime")
            {
                existingRuntimeCamera = candidate;
                continue;
            }

            if (authoredCamera == null)
            {
                authoredCamera = candidate;
            }
        }

        GameObject cameraObject = existingRuntimeCamera != null
            ? existingRuntimeCamera.gameObject
            : new GameObject("WalkSideCameraRuntime");
        sceneCamera = cameraObject.GetComponent<Camera>();
        if (sceneCamera == null)
        {
            sceneCamera = cameraObject.AddComponent<Camera>();
        }

        DogCycleCamera dogCycleCamera = cameraObject.GetComponent<DogCycleCamera>();
        if (dogCycleCamera != null)
        {
            dogCycleCamera.enabled = false;
            Destroy(dogCycleCamera);
        }

        CameraFollow legacyCameraFollow = cameraObject.GetComponent<CameraFollow>();
        if (legacyCameraFollow != null)
        {
            legacyCameraFollow.enabled = false;
            Destroy(legacyCameraFollow);
        }

        if (authoredCamera != null)
        {
            cameraObject.transform.SetPositionAndRotation(authoredCamera.transform.position, authoredCamera.transform.rotation);
            sceneCamera.CopyFrom(authoredCamera);
        }

        cameraObject.transform.SetParent(null, false);
        cameraObject.tag = "MainCamera";
        sceneCamera.enabled = true;
        sceneCamera.fieldOfView = 50f;
        sceneCamera.nearClipPlane = 0.03f;
        sceneCamera.farClipPlane = 180f;
        sceneCamera.clearFlags = CameraClearFlags.Skybox;
        sceneCamera.depth = 100f;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || candidate == sceneCamera)
            {
                continue;
            }

            candidate.enabled = false;
            candidate.tag = "Untagged";
            AudioListener otherListener = candidate.GetComponent<AudioListener>();
            if (otherListener != null)
            {
                otherListener.enabled = false;
            }

            candidate.gameObject.SetActive(false);
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.gameObject != sceneCamera.gameObject)
            {
                listener.enabled = false;
            }
        }

        AudioListener sceneListener = sceneCamera.GetComponent<AudioListener>();
        if (sceneListener == null)
        {
            sceneListener = sceneCamera.gameObject.AddComponent<AudioListener>();
        }

        sceneListener.enabled = true;

        PositionCamera(true);
    }

    private void ConfigureWalkPetGraphics()
    {
        if (walker == null)
        {
            return;
        }

        petGraphicsEnhancer = GetComponent<PawPalWalkPetGraphicsEnhancer>();
        if (petGraphicsEnhancer == null)
        {
            petGraphicsEnhancer = gameObject.AddComponent<PawPalWalkPetGraphicsEnhancer>();
        }

        petGraphicsEnhancer.Configure(walker, sceneCamera, allowedRoads);
    }

    private void PositionCamera(bool immediate)
    {
        if (sceneCamera == null || walker == null || sceneBindings == null)
        {
            return;
        }

        Vector3 desiredPosition = walker.position + sceneBindings.CameraFollowWorldOffset;
        Quaternion desiredRotation = Quaternion.Euler(sceneBindings.CameraFixedEulerAngles);
        sceneCamera.transform.SetPositionAndRotation(desiredPosition, desiredRotation);
    }

    private void MoveWalkerToDistance(float routeDistance, bool immediate)
    {
        if (walker == null)
        {
            return;
        }

        Vector3 position = GetRoutePositionAtDistance(routeDistance);
        Vector3 direction = GetRouteDirectionAtDistance(routeDistance);
        position = ProjectToAllowedRoad(position, walker.position.y, !HasGraphBackedRoute(session));
        walker.position = position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            walker.rotation = immediate
                ? targetRotation
                : Quaternion.Slerp(walker.rotation, targetRotation, Time.deltaTime * 10f);
        }

        if (walkerRoomAgent == null
            && walkerCatAgent == null
            && (leashDirector == null || !leashDirector.IsPlayingPetOneShot))
        {
            SetFallbackAnimatorLocomotion(state == WalkState.Running);
        }
    }

    private Vector3 GetRoutePositionAtDistance(float routeDistance)
    {
        if (routePoints.Count == 0)
        {
            return walker != null ? walker.position : Vector3.zero;
        }

        if (routePoints.Count == 1)
        {
            return routePoints[0];
        }

        float clampedDistance = Mathf.Clamp(routeDistance, 0f, totalRouteDistance);
        int segmentIndex = FindSegmentIndex(clampedDistance);
        float segmentStartDistance = cumulativeRouteDistances[segmentIndex];
        float segmentEndDistance = cumulativeRouteDistances[segmentIndex + 1];
        float segmentLength = Mathf.Max(0.001f, segmentEndDistance - segmentStartDistance);
        float segmentT = Mathf.Clamp01((clampedDistance - segmentStartDistance) / segmentLength);
        return Vector3.Lerp(routePoints[segmentIndex], routePoints[segmentIndex + 1], segmentT);
    }

    private Vector3 GetRouteDirectionAtDistance(float routeDistance)
    {
        if (routePoints.Count < 2)
        {
            return Vector3.right;
        }

        int segmentIndex = FindSegmentIndex(Mathf.Clamp(routeDistance, 0f, totalRouteDistance));
        Vector3 direction = routePoints[segmentIndex + 1] - routePoints[segmentIndex];
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
    }

    private int FindSegmentIndex(float routeDistance)
    {
        if (cumulativeRouteDistances == null || cumulativeRouteDistances.Length < 2)
        {
            return 0;
        }

        for (int i = 0; i < cumulativeRouteDistances.Length - 1; i++)
        {
            if (routeDistance <= cumulativeRouteDistances[i + 1])
            {
                return i;
            }
        }

        return cumulativeRouteDistances.Length - 2;
    }

    private PawPalWalkGeneratedEventState GetNextPendingEvent()
    {
        if (session == null || session.GeneratedEvents == null)
        {
            return null;
        }

        PawPalWalkGeneratedEventState next = null;
        for (int i = 0; i < session.GeneratedEvents.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = session.GeneratedEvents[i];
            if (walkEvent == null || walkEvent.Resolved)
            {
                continue;
            }

            if (next == null || walkEvent.Progress < next.Progress)
            {
                next = walkEvent;
            }
        }

        return next;
    }

    private IEnumerator PlayEncounterRoutine(PawPalWalkGeneratedEventState walkEvent)
    {
        if (walkEvent != null
            && walkEvent.EventType == PawPalWalkEventType.PresentFound
            && activePetSpecies == IntroPetSpecies.Dog
            && walker != null
            && sceneCamera != null)
        {
            yield return PlayPresentFoundEncounterRoutine(walkEvent);
            yield break;
        }

        if (walkEvent != null
            && walkEvent.EventType == PawPalWalkEventType.DogEncounter
            && walker != null
            && sceneCamera != null)
        {
            yield return PlayPetEncounterRoutine(walkEvent);
            yield break;
        }

        yield return PlayStandardEncounterRoutine(walkEvent);
    }

    private IEnumerator PlayStandardEncounterRoutine(PawPalWalkGeneratedEventState walkEvent)
    {
        state = WalkState.PausedForStop;
        SetFallbackAnimatorLocomotion(false);
        float pauseDuration = Random.Range(sceneBindings.PauseDurationRange.x, sceneBindings.PauseDurationRange.y);
        PawPalWalkStopType stopType = walkEvent != null ? walkEvent.StopType : PawPalWalkStopType.Sniff;

        if (walkerRoomAgent != null)
        {
            yield return walkerRoomAgent.PlayWalkStop(stopType, pauseDuration);
            walkerRoomAgent.SetExternalWalkPace(DogMovementPace.Walk);
        }
        else
        {
            yield return new WaitForSeconds(pauseDuration);
        }

        if (runtime != null && walkEvent != null)
        {
            runtime.MarkActiveWalkEventResolved(walkEvent.EventId);
        }

        stateRoutine = null;
        state = WalkState.Running;
    }

    private IEnumerator PlayPresentFoundEncounterRoutine(PawPalWalkGeneratedEventState walkEvent)
    {
        state = WalkState.PausedForStop;
        SetEncounterPaused(true);
        LockEncounterCamera();
        SetFallbackAnimatorLocomotion(false);

        Vector3 originalRoutePosition = walker.position;
        Quaternion originalRouteRotation = walker.rotation;
        Vector3 offscreenPoint = GetPresentRunOffscreenPoint();
        Vector3 returnPoint = GetPresentReturnPoint();

        yield return MoveWalkerCinematic(offscreenPoint, sceneBindings.PresentCinematicRunSpeed, DogMovementPace.Run, false);

        GameObject present = SpawnPresentForEncounter();
        if (present != null)
        {
            AttachPresentToWalker(present);
        }

        yield return MoveWalkerCinematic(returnPoint, sceneBindings.PresentCinematicApproachSpeed, DogMovementPace.Trot, true);
        FaceWalkerToward(sceneCamera.transform.position, true);
        SetFallbackAnimatorLocomotion(false);

        bool overlayCompleted = false;
        PawPalWalkFoundItemOverlayView.Show(runtime, walkEvent, delegate
        {
            overlayCompleted = true;
        });

        while (!overlayCompleted)
        {
            ApplyLockedEncounterCamera();
            yield return null;
        }

        if (runtime != null && walkEvent != null)
        {
            runtime.MarkActiveWalkEventResolved(walkEvent.EventId);
        }

        walker.SetPositionAndRotation(originalRoutePosition, originalRouteRotation);
        MoveWalkerToDistance(currentDistance, true);
        UnlockEncounterCamera();
        SetEncounterPaused(false);
        stateRoutine = null;
        state = WalkState.Running;
    }

    private IEnumerator PlayPetEncounterRoutine(PawPalWalkGeneratedEventState walkEvent)
    {
        Vector3 routeDirection = GetRouteDirectionAtDistance(currentDistance);
        routeDirection.y = 0f;
        if (routeDirection.sqrMagnitude <= 0.001f)
        {
            routeDirection = GetCameraPlanarRight();
        }

        routeDirection = routeDirection.sqrMagnitude > 0.001f ? routeDirection.normalized : Vector3.right;
        Vector3 encounterCenter = walker.position;
        Vector3 playerMeetPoint = ProjectToAllowedRoad(
            encounterCenter - routeDirection * (sceneBindings.PetEncounterMeetDistance * 0.5f),
            walker.position.y,
            false);
        Vector3 visitorMeetPoint = ProjectToAllowedRoad(
            encounterCenter + routeDirection * (sceneBindings.PetEncounterMeetDistance * 0.5f),
            walker.position.y,
            false);
        Vector3 visitorSpawnPoint = ProjectToAllowedRoad(
            encounterCenter + routeDirection * sceneBindings.PetEncounterVisitorSpawnDistance,
            walker.position.y,
            false);

        if (!TrySpawnEncounterVisitor(walkEvent, visitorSpawnPoint, Quaternion.LookRotation(-routeDirection, Vector3.up), out EncounterVisitorPet visitor))
        {
            yield return PlayStandardEncounterRoutine(walkEvent);
            yield break;
        }

        state = WalkState.PausedForStop;
        SetEncounterPaused(true);
        SetFallbackAnimatorLocomotion(false);
        LockPetEncounterCamera(encounterCenter, routeDirection);

        Vector3 originalRoutePosition = walker.position;
        Quaternion originalRouteRotation = walker.rotation;
        float interactionDuration = Random.Range(
            sceneBindings.PetEncounterInteractionDurationRange.x,
            sceneBindings.PetEncounterInteractionDurationRange.y);

        yield return MoveWalkerCinematic(playerMeetPoint, sceneBindings.PetEncounterRunSpeed, DogMovementPace.Trot, false);
        FaceWalkerToward(visitorMeetPoint, true);
        yield return MoveEncounterVisitor(visitor, visitorMeetPoint, sceneBindings.PetEncounterRunSpeed, DogMovementPace.Run);
        FaceWalkerToward(visitor.Root.position, true);
        FaceEncounterPetToward(visitor, walker.position, true);
        SetWalkerCinematicPace(DogMovementPace.Walk);
        SetEncounterPetPace(visitor, DogMovementPace.Walk);

        bool overlayCompleted = false;
        PawPalWalkEncounterInfoOverlayView.Show(walkEvent, activePetSpecies, ResolveEncounterAdviceText(walkEvent), delegate
        {
            overlayCompleted = true;
        });

        float minimumInteractionEndTime = Time.time + interactionDuration;
        Coroutine interactionRoutine = StartCoroutine(PlayPetEncounterInteractionLoop(visitor, delegate
        {
            return overlayCompleted && Time.time >= minimumInteractionEndTime;
        }));

        while (!overlayCompleted || Time.time < minimumInteractionEndTime)
        {
            FaceWalkerToward(visitor.Root.position, false);
            FaceEncounterPetToward(visitor, walker.position, false);
            ApplyLockedEncounterCamera();
            yield return null;
        }

        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
        }

        Vector3 visitorExitPoint = ProjectToAllowedRoad(
            encounterCenter - routeDirection * sceneBindings.PetEncounterVisitorExitDistance,
            visitor.Root.position.y,
            false);
        yield return MoveEncounterVisitor(visitor, visitorExitPoint, sceneBindings.PetEncounterRunSpeed, DogMovementPace.Run);
        DestroyEncounterVisitor(visitor);

        if (runtime != null && walkEvent != null)
        {
            runtime.MarkActiveWalkEventResolved(walkEvent.EventId);
        }

        walker.SetPositionAndRotation(originalRoutePosition, originalRouteRotation);
        MoveWalkerToDistance(currentDistance, true);
        UnlockEncounterCamera();
        SetEncounterPaused(false);
        stateRoutine = null;
        state = WalkState.Running;
    }

    private void LockPetEncounterCamera(Vector3 encounterCenter, Vector3 routeDirection)
    {
        if (sceneCamera == null)
        {
            return;
        }

        Vector3 horizontal = routeDirection;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude <= 0.001f)
        {
            horizontal = Vector3.right;
        }

        horizontal.Normalize();
        Vector3 cameraForward = Vector3.Cross(Vector3.up, horizontal);
        if (cameraForward.sqrMagnitude <= 0.001f)
        {
            cameraForward = GetCameraPlanarForward();
        }

        cameraForward.Normalize();
        Vector3 offset = sceneBindings.PetEncounterCameraOffset;
        Vector3 cameraPosition = encounterCenter
            + horizontal * offset.x
            + Vector3.up * offset.y
            - cameraForward * Mathf.Max(0.1f, Mathf.Abs(offset.z));
        Vector3 lookTarget = encounterCenter + sceneBindings.PetEncounterLookAtOffset;
        lockedCameraPosition = cameraPosition;
        lockedCameraRotation = Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up);
        cameraLockedForEncounter = true;
        ApplyLockedEncounterCamera();
    }

    private IEnumerator MoveEncounterVisitor(EncounterVisitorPet visitor, Vector3 target, float speed, DogMovementPace pace)
    {
        if (visitor == null || visitor.Root == null)
        {
            yield break;
        }

        SetEncounterPetPace(visitor, pace);
        float timeout = Mathf.Clamp(Vector3.Distance(visitor.Root.position, target) / Mathf.Max(0.05f, speed) + 1.5f, 1.5f, 7f);
        float startTime = Time.time;
        while (visitor.Root != null && Vector3.Distance(visitor.Root.position, target) > 0.035f && Time.time - startTime < timeout)
        {
            visitor.Root.position = Vector3.MoveTowards(visitor.Root.position, target, Mathf.Max(0.05f, speed) * Time.deltaTime);
            FaceEncounterPetToward(visitor, target, false);
            ApplyLockedEncounterCamera();
            yield return null;
        }

        if (visitor.Root != null)
        {
            visitor.Root.position = target;
        }
    }

    private IEnumerator PlayPetEncounterInteractionLoop(EncounterVisitorPet visitor, System.Func<bool> shouldStop)
    {
        float nextPulseAt = 0f;
        while (shouldStop == null || !shouldStop())
        {
            if (visitor != null && visitor.Root != null)
            {
                FaceWalkerToward(visitor.Root.position, false);
                FaceEncounterPetToward(visitor, walker != null ? walker.position : visitor.Root.position, false);
            }

            if (Time.time >= nextPulseAt)
            {
                float pulseDuration = Random.Range(0.75f, 1.25f);
                StartCoroutine(PlayWalkerPetEncounterPulse(pulseDuration));
                StartCoroutine(PlayVisitorPetEncounterPulse(visitor, pulseDuration));
                nextPulseAt = Time.time + Random.Range(1.4f, 2.4f);
            }

            ApplyLockedEncounterCamera();
            yield return null;
        }
    }

    private IEnumerator PlayWalkerPetEncounterPulse(float duration)
    {
        if (walkerRoomAgent != null)
        {
            yield return walkerRoomAgent.PlayWalkStop(PawPalWalkStopType.Bark, duration);
            yield break;
        }

        if (walkerCatAgent != null)
        {
            yield return walkerCatAgent.PlayBark(duration);
            yield break;
        }

        yield return PlayAnimatorEncounterPulse(walkerAnimator, runtimeDogSelection != null ? runtimeDogSelection.Definition : null, activePetSpecies, walker != null ? walker.name : string.Empty, duration);
    }

    private IEnumerator PlayVisitorPetEncounterPulse(EncounterVisitorPet visitor, float duration)
    {
        if (visitor == null)
        {
            yield break;
        }

        if (visitor.DogAgent != null)
        {
            yield return visitor.DogAgent.PlayWalkStop(PawPalWalkStopType.Bark, duration);
            yield break;
        }

        if (visitor.CatAgent != null)
        {
            yield return visitor.CatAgent.PlayBark(duration);
            yield break;
        }

        yield return PlayAnimatorEncounterPulse(visitor.Animator, visitor.Definition, visitor.Species, visitor.RuntimeBreed, duration);
    }

    private IEnumerator PlayAnimatorEncounterPulse(Animator animator, IntroPetDefinition definition, IntroPetSpecies species, string runtimeBreed, float duration)
    {
        if (animator == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            yield break;
        }

        if (species == IntroPetSpecies.Cat)
        {
            yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
                this,
                animator,
                definition,
                runtimeBreed,
                animator.gameObject.name,
                duration,
                "CatSimple_Bark_IP",
                "Arm_Cat|Bark_IP",
                "Bark_IP",
                "Idle_2_IP");
            yield break;
        }

        yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
            this,
            animator,
            definition,
            runtimeBreed,
            animator.gameObject.name,
            duration,
            "Bark",
            "Bark_IP",
            "Arm_Labrador|Bark_IP",
            "Idle");
    }

    private void SetEncounterPetPace(EncounterVisitorPet visitor, DogMovementPace pace)
    {
        if (visitor == null)
        {
            return;
        }

        if (visitor.DogAgent != null)
        {
            visitor.DogAgent.SetExternalWalkPace(pace);
        }

        if (visitor.CatAgent != null)
        {
            visitor.CatAgent.SetExternalWalkPace(pace);
        }

        if (visitor.Animator != null)
        {
            PawPalWalkPetAnimationPlayer.ForceLocomotionForPace(visitor.Animator, visitor.MovementProfile, visitor.Species, pace);
        }
    }

    private void FaceEncounterPetToward(EncounterVisitorPet visitor, Vector3 target, bool immediate)
    {
        if (visitor == null || visitor.Root == null)
        {
            return;
        }

        Vector3 direction = target - visitor.Root.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        visitor.Root.rotation = immediate
            ? targetRotation
            : Quaternion.Slerp(visitor.Root.rotation, targetRotation, Time.deltaTime * 12f);
    }

    private string ResolveEncounterAdviceText(PawPalWalkGeneratedEventState walkEvent)
    {
        if (walkEvent != null && !string.IsNullOrWhiteSpace(walkEvent.BodyText))
        {
            return walkEvent.BodyText.Trim();
        }

        string[] fallbackTexts = sceneBindings != null ? sceneBindings.PetEncounterAdviceTexts : null;
        if (fallbackTexts != null && fallbackTexts.Length > 0)
        {
            int start = Random.Range(0, fallbackTexts.Length);
            for (int i = 0; i < fallbackTexts.Length; i++)
            {
                string candidate = fallbackTexts[(start + i) % fallbackTexts.Length];
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }
        }

        return "Friendly city walks are easier when your pet has space to greet, sniff, and move on calmly.";
    }

    private bool TrySpawnEncounterVisitor(PawPalWalkGeneratedEventState walkEvent, Vector3 position, Quaternion rotation, out EncounterVisitorPet visitor)
    {
        visitor = null;
        IntroPetDefinition definition = ResolveEncounterVisitorDefinition(walkEvent);
        if (definition == null)
        {
            Debug.LogWarning("PawPalWalkSceneController could not resolve a visitor pet definition for the walk encounter.");
            return false;
        }

        SelectedPetSessionData selection = BuildEncounterVisitorSelection(definition, walkEvent);
        GameObject preferredPrefab = PetVariantApplier.GetPrefabForVariant(definition, selection.FurVariant);
        GameObject prefab = preferredPrefab != null ? preferredPrefab : definition.BasePrefab;
        string errorMessage;
        if (!PetVariantApplier.TryInstantiatePrefab(prefab, position, rotation, null, out GameObject visitorObject, out errorMessage))
        {
            Debug.LogWarning("PawPalWalkSceneController could not spawn encounter visitor '" + definition.PetId + "'. " + errorMessage);
            return false;
        }

        visitorObject.name = "WalkEncounterVisitor_" + selection.SafeName;
        ApplySpawnedDogPresentation(selection, visitorObject);

        DogRoomAgent dogAgent = visitorObject.GetComponent<DogRoomAgent>();
        PawPalCatRoomAgent catAgent = visitorObject.GetComponent<PawPalCatRoomAgent>();
        if (catAgent == null)
        {
            catAgent = visitorObject.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        if (definition.Species == IntroPetSpecies.Dog)
        {
            PawPalCatRoomAgent[] catAgents = visitorObject.GetComponentsInChildren<PawPalCatRoomAgent>(true);
            for (int i = 0; i < catAgents.Length; i++)
            {
                if (catAgents[i] != null)
                {
                    Destroy(catAgents[i]);
                }
            }

            if (dogAgent == null)
            {
                dogAgent = visitorObject.AddComponent<DogRoomAgent>();
            }

            dogAgent.SetRuntimeDogId(selection.RuntimePetId);
            dogAgent.ApplySelectedPetPresentation(selection);
            dogAgent.ConfigureSelectedPetRuntime(selection);
            dogAgent.SetExternalWalkControl(true);
            dogAgent.SetExternalWalkPace(DogMovementPace.Walk);
            catAgent = null;
        }
        else
        {
            if (dogAgent != null)
            {
                Destroy(dogAgent);
                dogAgent = null;
            }

            if (catAgent == null)
            {
                catAgent = visitorObject.AddComponent<PawPalCatRoomAgent>();
            }

            catAgent.Initialize(selection);
            catAgent.SetExternalWalkControl(true);
            catAgent.SetExternalWalkPace(DogMovementPace.Walk);
        }

        Animator animator = visitorObject.GetComponent<Animator>();
        if (animator == null)
        {
            animator = visitorObject.GetComponentInChildren<Animator>(true);
        }

        PawPalPetMovementProfile movementProfile = dogAgent != null
            ? dogAgent.MovementProfile
            : PawPalPetMovementProfiles.Resolve(definition, definition.BreedName, visitorObject.name);

        visitor = new EncounterVisitorPet
        {
            GameObject = visitorObject,
            Root = visitorObject.transform,
            Animator = animator,
            DogAgent = dogAgent,
            CatAgent = catAgent,
            Definition = definition,
            MovementProfile = movementProfile,
            Species = definition.Species,
            RuntimeBreed = definition.BreedName
        };

        return true;
    }

    private IntroPetDefinition ResolveEncounterVisitorDefinition(PawPalWalkGeneratedEventState walkEvent)
    {
        string key = walkEvent != null ? walkEvent.VisitorPetDefinitionKey : string.Empty;
        IntroPetDefinition definition = ResolveIntroPetDefinitionByKey(key, null);
        if (definition != null)
        {
            return definition;
        }

        string[] configuredKeys = sceneBindings != null ? sceneBindings.PetEncounterVisitorDefinitionKeys : null;
        if (configuredKeys != null && configuredKeys.Length > 0)
        {
            int start = Random.Range(0, configuredKeys.Length);
            for (int i = 0; i < configuredKeys.Length; i++)
            {
                string candidateKey = configuredKeys[(start + i) % configuredKeys.Length];
                definition = ResolveIntroPetDefinitionByKey(candidateKey, null);
                if (definition != null)
                {
                    return definition;
                }
            }
        }

        IntroPetSpecies preferredSpecies = walkEvent != null ? walkEvent.VisitorSpecies : IntroPetSpecies.Dog;
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        if (definitions == null)
        {
            return null;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition candidate = definitions[i];
            if (candidate != null && candidate.Species == preferredSpecies && candidate.BasePrefab != null)
            {
                return candidate;
            }
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition candidate = definitions[i];
            if (candidate != null && candidate.BasePrefab != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static IntroPetDefinition ResolveIntroPetDefinitionByKey(string key, IntroPetSpecies? species)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string normalizedKey = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            return null;
        }

        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        if (definitions == null || definitions.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || (species.HasValue && definition.Species != species.Value))
            {
                continue;
            }

            string petId = NormalizeKey(definition.PetId);
            string breedName = NormalizeKey(definition.BreedName);
            string displayName = NormalizeKey(definition.DisplayName);
            if (normalizedKey == petId || normalizedKey == breedName || normalizedKey == displayName)
            {
                return definition;
            }
        }

        return null;
    }

    private SelectedPetSessionData BuildEncounterVisitorSelection(IntroPetDefinition definition, PawPalWalkGeneratedEventState walkEvent)
    {
        string displayName = walkEvent != null && !string.IsNullOrWhiteSpace(walkEvent.VisitorDisplayName)
            ? walkEvent.VisitorDisplayName.Trim()
            : null;
        if (string.IsNullOrWhiteSpace(displayName) && walkEvent != null && !string.IsNullOrWhiteSpace(walkEvent.DisplayName))
        {
            displayName = walkEvent.DisplayName.Trim();
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : "Friendly Pet";
        }

        FurVariantDefinition furVariant = definition != null ? definition.GetDefaultFurVariant() : null;
        return new SelectedPetSessionData
        {
            Definition = definition,
            FurVariant = furVariant,
            FurIndex = definition != null ? definition.GetFurVariantIndex(furVariant) : 0,
            Gender = PawPalDogGender.Female,
            Personality = PawPalDogPersonality.Playful,
            PetName = displayName,
            RuntimePetId = "walk_encounter_" + NormalizeKey(displayName)
        };
    }

    private void DestroyEncounterVisitor(EncounterVisitorPet visitor)
    {
        if (visitor != null && visitor.GameObject != null)
        {
            Destroy(visitor.GameObject);
        }
    }

    private void SetEncounterPaused(bool paused)
    {
        if (leashDirector != null)
        {
            leashDirector.SetEncounterPaused(paused);
        }

        if (!paused)
        {
            if (walkerRoomAgent != null)
            {
                walkerRoomAgent.SetExternalWalkPace(DogMovementPace.Walk);
            }

            if (walkerCatAgent != null)
            {
                walkerCatAgent.SetExternalWalkPace(DogMovementPace.Walk);
            }
        }
    }

    private void LockEncounterCamera()
    {
        if (sceneCamera == null)
        {
            return;
        }

        lockedCameraPosition = sceneCamera.transform.position;
        lockedCameraRotation = sceneCamera.transform.rotation;
        cameraLockedForEncounter = true;
        ApplyLockedEncounterCamera();
    }

    private void UnlockEncounterCamera()
    {
        cameraLockedForEncounter = false;
        PositionCamera(true);
    }

    private void ApplyLockedEncounterCamera()
    {
        if (sceneCamera != null)
        {
            sceneCamera.transform.SetPositionAndRotation(lockedCameraPosition, lockedCameraRotation);
        }
    }

    private Vector3 GetPresentRunOffscreenPoint()
    {
        Vector3 right = GetCameraPlanarRight();
        Vector3 forward = GetCameraPlanarForward();
        return walker.position
            + right * sceneBindings.PresentRunOffscreenDistance
            + forward * Mathf.Max(0.5f, sceneBindings.PresentRunOffscreenDistance * 0.35f);
    }

    private Vector3 GetPresentReturnPoint()
    {
        Vector3 forward = GetCameraPlanarForward();
        Vector3 target = sceneCamera.transform.position + forward * sceneBindings.PresentApproachDistanceFromCamera;
        target.y = walker.position.y;
        return ProjectToAllowedRoad(target, walker.position.y, false);
    }

    private Vector3 GetCameraPlanarForward()
    {
        if (sceneCamera == null)
        {
            return GetRouteDirectionAtDistance(currentDistance);
        }

        Vector3 forward = sceneCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = GetRouteDirectionAtDistance(currentDistance);
        }

        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 GetCameraPlanarRight()
    {
        if (sceneCamera == null)
        {
            Vector3 routeDirection = GetRouteDirectionAtDistance(currentDistance);
            Vector3 fallbackRight = Vector3.Cross(Vector3.up, routeDirection);
            return fallbackRight.sqrMagnitude > 0.001f ? fallbackRight.normalized : Vector3.right;
        }

        Vector3 right = sceneCamera.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude <= 0.001f)
        {
            right = Vector3.right;
        }

        return right.normalized;
    }

    private IEnumerator MoveWalkerCinematic(Vector3 target, float speed, DogMovementPace pace, bool faceCamera)
    {
        if (walker == null)
        {
            yield break;
        }

        SetWalkerCinematicPace(pace);
        float timeout = Mathf.Clamp(Vector3.Distance(walker.position, target) / Mathf.Max(0.05f, speed) + 1.5f, 1.5f, 7f);
        float startTime = Time.time;
        while (walker != null && Vector3.Distance(walker.position, target) > 0.035f && Time.time - startTime < timeout)
        {
            Vector3 next = Vector3.MoveTowards(walker.position, target, Mathf.Max(0.05f, speed) * Time.deltaTime);
            walker.position = next;
            if (faceCamera && sceneCamera != null)
            {
                FaceWalkerToward(sceneCamera.transform.position, false);
            }
            else
            {
                FaceWalkerToward(target, false);
            }

            ApplyLockedEncounterCamera();
            yield return null;
        }

        if (walker != null)
        {
            walker.position = target;
        }
    }

    private void SetWalkerCinematicPace(DogMovementPace pace)
    {
        if (walkerRoomAgent != null)
        {
            walkerRoomAgent.SetExternalWalkPace(pace);
        }

        if (walkerCatAgent != null)
        {
            walkerCatAgent.SetExternalWalkPace(pace);
        }

        if (walkerAnimator != null)
        {
            PawPalWalkPetAnimationPlayer.ForceLocomotionForPace(walkerAnimator, walkMovementProfile, activePetSpecies, pace);
        }
    }

    private void FaceWalkerToward(Vector3 target, bool immediate)
    {
        if (walker == null)
        {
            return;
        }

        Vector3 direction = target - walker.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        walker.rotation = immediate
            ? targetRotation
            : Quaternion.Slerp(walker.rotation, targetRotation, Time.deltaTime * 12f);
    }

    private GameObject SpawnPresentForEncounter()
    {
        GameObject presentPrefab = ResolvePresentEncounterPrefab();
        GameObject present;
        if (presentPrefab != null)
        {
            present = Instantiate(presentPrefab, walker.position, walker.rotation);
        }
        else
        {
            present = GameObject.CreatePrimitive(PrimitiveType.Cube);
            present.transform.SetPositionAndRotation(walker.position, walker.rotation);
            present.transform.localScale = Vector3.one * 0.12f;
        }

        present.name = "WalkFoundPresent_02";
        return present;
    }

    private GameObject ResolvePresentEncounterPrefab()
    {
        if (sceneBindings != null && sceneBindings.PresentEncounterPrefab != null)
        {
            return sceneBindings.PresentEncounterPrefab;
        }

#if UNITY_EDITOR
        if (sceneBindings != null)
        {
            GameObject prefab = LoadEditorPresentAsset(sceneBindings.EditorPresentPrefabAssetPath);
            if (prefab != null)
            {
                return prefab;
            }

            return LoadEditorPresentAsset(sceneBindings.EditorPresentModelAssetPath);
        }
#endif

        return null;
    }

#if UNITY_EDITOR
    private static GameObject LoadEditorPresentAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath.Trim());
    }
#endif

    private void AttachPresentToWalker(GameObject present)
    {
        if (walker == null || present == null)
        {
            return;
        }

        ToyAttach attach = walker.GetComponent<ToyAttach>();
        if (attach == null)
        {
            attach = walker.gameObject.AddComponent<ToyAttach>();
        }

        if (attach == null || !attach.TryAttachToy(present))
        {
            Debug.LogWarning("PawPalWalkSceneController could not attach the found present to the walk dog's mouth.");
            return;
        }

        ApplyPresentMouthTransform(present);
    }

    private void ApplyPresentMouthTransform(GameObject present)
    {
        if (present == null || sceneBindings == null)
        {
            return;
        }

        Transform presentTransform = present.transform;
        presentTransform.localPosition = sceneBindings.PresentLocalPosition;
        presentTransform.localRotation = Quaternion.Euler(sceneBindings.PresentLocalEulerAngles);
        presentTransform.localScale = sceneBindings.PresentLocalScale;
    }

    private void BeginCompleteRoutine()
    {
        if (state == WalkState.Completing)
        {
            return;
        }

        state = WalkState.Completing;
        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
        }

        stateRoutine = StartCoroutine(CompleteWalkRoutine());
    }

    private IEnumerator CompleteWalkRoutine()
    {
        currentDistance = totalRouteDistance;
        progress = 1f;
        MoveWalkerToDistance(currentDistance, false);
        PushProgressToRuntime();
        SetFallbackAnimatorLocomotion(false);

        if (runtime != null)
        {
            runtime.CompleteActiveWalkSession();
        }

        yield return new WaitForSeconds(sceneBindings.EndHoldDuration);
        stateRoutine = null;
        PawPalWalkSceneFlow.ReturnHome(returnSceneName);
    }

    private void PushProgressToRuntime()
    {
        if (runtime != null)
        {
            runtime.UpdateActiveWalkSessionProgress(progress);
        }
    }

    private Vector3 ProjectToAllowedRoad(Vector3 candidate, float fallbackY, bool allowClosestPointFallback)
    {
        if (allowedRoads.Count == 0)
        {
            return candidate;
        }

        Vector3 rayOrigin = candidate + Vector3.up * GroundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, GroundProbeDistance);
        float bestDistance = float.MaxValue;
        bool found = false;
        Vector3 bestPoint = candidate;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !allowedRoads.Contains(hit.collider) || IsWalkerHit(hit.transform))
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestPoint = hit.point;
            found = true;
        }

        if (found)
        {
            return bestPoint;
        }

        if (!allowClosestPointFallback)
        {
            return candidate;
        }

        float bestScore = float.MaxValue;
        for (int i = 0; i < allowedRoads.Count; i++)
        {
            Collider collider = allowedRoads[i];
            if (collider == null || !SupportsClosestPoint(collider))
            {
                continue;
            }

            Vector3 closest = collider.ClosestPoint(candidate);
            float score = (closest - candidate).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = closest;
                found = true;
            }
        }

        if (found)
        {
            return bestPoint;
        }

        return candidate;
    }

    private static bool SupportsClosestPoint(Collider collider)
    {
        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            return true;
        }

        MeshCollider meshCollider = collider as MeshCollider;
        return meshCollider != null && meshCollider.convex;
    }

    private bool IsWalkerHit(Transform candidate)
    {
        return walker != null
            && candidate != null
            && (candidate == walker || candidate.IsChildOf(walker));
    }

    private Vector3 GetSpawnPosition()
    {
        if (routePoints.Count == 0)
        {
            return sceneBindings != null && sceneBindings.SpawnPoint != null
                ? sceneBindings.SpawnPoint.position
                : Vector3.zero;
        }

        return routePoints[0];
    }

    private void RemoveDuplicateRoutePoints()
    {
        for (int i = routePoints.Count - 1; i > 0; i--)
        {
            if (Vector3.Distance(routePoints[i], routePoints[i - 1]) <= 0.05f)
            {
                routePoints.RemoveAt(i);
            }
        }
    }

    private void DisableConflictingComponents(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Behaviour[] behaviours = target.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null
                || behaviour is Animator
                || behaviour == this)
            {
                continue;
            }

            if (behaviour is NavMeshAgent navMeshAgent)
            {
                if (navMeshAgent.enabled)
                {
                    navMeshAgent.enabled = false;
                }

                continue;
            }

            if (behaviour == walkerRoomAgent || behaviour == walkerCatAgent || behaviour is DogCameraAttention)
            {
                continue;
            }

            if (behaviour is MonoBehaviour)
            {
                behaviour.enabled = false;
            }
        }

        if (walkerAnimator != null)
        {
            walkerAnimator.applyRootMotion = false;
        }
    }

    private void DeactivateForeignScenePets()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        DogRoomAgent[] dogs = Object.FindObjectsByType<DogRoomAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null || dog.gameObject.scene != activeScene)
            {
                continue;
            }

            dog.gameObject.SetActive(false);
        }

        PawPalCatRoomAgent[] cats = Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent cat = cats[i];
            if (cat == null || cat.gameObject.scene != activeScene)
            {
                continue;
            }

            cat.gameObject.SetActive(false);
        }
    }

    private static bool HasGraphBackedRoute(PawPalWalkSessionSaveData walkSession)
    {
        return walkSession != null
            && walkSession.RouteEdgeIds != null
            && walkSession.RouteEdgeIds.Count > 0;
    }

    private static bool HasRequestedVisitRoute(PawPalWalkSessionSaveData walkSession)
    {
        return walkSession != null
            && walkSession.RequestedVisitNodeIds != null
            && walkSession.RequestedVisitNodeIds.Count > 0;
    }

    private void SetFallbackAnimatorLocomotion(bool moving)
    {
        if (walkerAnimator == null || walkerRoomAgent != null || walkerCatAgent != null)
        {
            return;
        }

        if (!moving)
        {
            if (HasAnimatorParameter(walkerAnimator, MoveHash, AnimatorControllerParameterType.Bool))
            {
                walkerAnimator.SetBool(MoveHash, false);
            }

            if (HasAnimatorParameter(walkerAnimator, SpeedHash, AnimatorControllerParameterType.Float))
            {
                walkerAnimator.SetFloat(SpeedHash, 0f);
            }

            if (HasAnimatorParameter(walkerAnimator, DirectionHash, AnimatorControllerParameterType.Float))
            {
                walkerAnimator.SetFloat(DirectionHash, 0f);
            }

            if (HasAnimatorParameter(walkerAnimator, IdleIndexHash, AnimatorControllerParameterType.Int))
            {
                walkerAnimator.SetInteger(IdleIndexHash, 99);
            }

            return;
        }

        PawPalWalkPetAnimationPlayer.ForceLocomotionForPace(
            walkerAnimator,
            walkMovementProfile,
            activePetSpecies,
            leashDirector != null ? leashDirector.CurrentPace : DogMovementPace.Walk);
    }

    private float ResolveCurrentRouteSpeed()
    {
        DogMovementPace pace = leashDirector != null ? leashDirector.CurrentPace : DogMovementPace.Walk;
        float paceSpeed = Mathf.Max(0.05f, walkMovementProfile.GetSpeed(pace));
        float paceMultiplier = leashDirector != null ? leashDirector.RouteSpeedMultiplier : 1f;
        return paceSpeed * Mathf.Max(0.05f, routeSpeedScale) * Mathf.Max(0.05f, paceMultiplier);
    }

    private static bool HasAnimatorParameter(Animator animator, int parameterHash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                buffer[count++] = char.ToLowerInvariant(character);
            }
        }

        return count > 0 ? new string(buffer, 0, count) : string.Empty;
    }

    private void FailStartup(string message)
    {
        startupFailed = true;
        startupFailureMessage = string.IsNullOrWhiteSpace(message) ? "Walking scene startup failed." : message.Trim();
        Debug.LogError("PawPalWalkSceneController startup failed: " + startupFailureMessage);
        PawPalWalkSceneFlow.SetAppShellVisible(true);
        ShowStartupFailureOverlay(startupFailureMessage);
    }

    private void ShowStartupFailureOverlay(string message)
    {
        GameObject canvasObject = new GameObject("WalkStartupFailureCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.78f);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("Message");
        textObject.transform.SetParent(panelObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "Walk setup failed\n\n" + message + "\n\nCheck the Unity Console for the exact startup failure.";
        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(900f, 300f);
        textRect.anchoredPosition = Vector2.zero;
    }
}

public sealed class PawPalWalkFoundItemOverlayView : MonoBehaviour
{
    private static readonly Color32 PanelFill = new Color32(252, 248, 232, 255);
    private static readonly Color32 CardBorder = new Color32(231, 215, 188, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);

    private const float FrameWidth = UiTheme.ReferenceWidth;
    private const float FrameHeight = UiTheme.ReferenceHeight;

    private PawPalGameRuntime runtime;
    private PawPalWalkGeneratedEventState walkEvent;
    private System.Action completed;
    private RectTransform exactFrame;

    public static void Show(PawPalGameRuntime runtime, PawPalWalkGeneratedEventState walkEvent, System.Action completed)
    {
        GameObject overlayObject = new GameObject("PawPalWalkFoundItemOverlay");
        PawPalWalkFoundItemOverlayView overlay = overlayObject.AddComponent<PawPalWalkFoundItemOverlayView>();
        overlay.Initialize(runtime, walkEvent, completed);
    }

    private void Initialize(PawPalGameRuntime runtimeInstance, PawPalWalkGeneratedEventState eventState, System.Action onCompleted)
    {
        runtime = runtimeInstance;
        walkEvent = eventState;
        completed = onCompleted;
        BuildCanvas();
        ShowFoundStep();
    }

    private void BuildCanvas()
    {
        RectTransform canvasRect = gameObject.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            canvasRect = gameObject.AddComponent<RectTransform>();
        }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        exactFrame = UiFactory.CreateRect("ExactFrame", canvasRect);
        exactFrame.anchorMin = new Vector2(0.5f, 0.5f);
        exactFrame.anchorMax = new Vector2(0.5f, 0.5f);
        exactFrame.pivot = new Vector2(0.5f, 0.5f);
        exactFrame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        Image blocker = UiFactory.CreateImage("Blocker", exactFrame, UiTheme.WhiteSprite, new Color(0f, 0f, 0f, 0.18f));
        blocker.type = Image.Type.Simple;
        blocker.preserveAspect = false;
        blocker.raycastTarget = true;
        Stretch(blocker.rectTransform);
    }

    private void ShowFoundStep()
    {
        ClearFrame();
        BuildModalPanel(54f, 340f, 285f, 160f);

        TextMeshProUGUI title = CreateText("Title", exactFrame, "Your dog found something!", 22, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, 74f, 371f, 245f, 54f);
        title.enableAutoSizing = true;
        title.fontSizeMin = 14f;
        title.fontSizeMax = 22f;
        title.textWrappingMode = TextWrappingModes.Normal;

        CreateActionButton("FoundContinue", exactFrame, "Continue", 142.5f, 445f, 108f, 30f, ShowRewardStep);
    }

    private void ShowRewardStep()
    {
        ClearFrame();
        BuildModalPanel(54f, 292f, 285f, 265f);

        TextMeshProUGUI title = CreateText("Title", exactFrame, "Found item", 18, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, 74f, 314f, 245f, 26f);

        PawPalCatalogItemDefinition item = ResolveRewardItem();
        BuildRewardCard(exactFrame, item, 139f, 358f);

        CreateActionButton("RewardContinue", exactFrame, "Continue", 142.5f, 511f, 108f, 30f, Complete);
    }

    private PawPalCatalogItemDefinition ResolveRewardItem()
    {
        if (runtime == null || walkEvent == null)
        {
            return null;
        }

        PawPalCatalogItemDefinition item = runtime.GetCatalogItem(walkEvent.RewardItemId);
        if (item != null)
        {
            return item;
        }

        return runtime.GetCatalogItem("toy_bone_1");
    }

    private void BuildRewardCard(RectTransform parent, PawPalCatalogItemDefinition item, float x, float y)
    {
        RectTransform card = CreateNode("RewardCard", parent, x, y, 115f, 115f);

        Image fill = UiFactory.CreateImage("Field", card, UiTheme.RoundedTenSprite, Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, 115f, 115f);

        Image border = UiFactory.CreateImage("Frame", card, UiTheme.RoundedTenOutlineSprite, CardBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, 115f, 115f);

        Image titleBar = UiFactory.CreateImage("TitleBar", card, UiTheme.InventoryItemTitleBarSprite, CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, 115f, 15f);

        string itemName = item != null && !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : "Present";
        TextMeshProUGUI label = CreateText("ItemName", card, itemName, 12, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 12f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        SetTopLeft(label.rectTransform, 0f, 0f, 115f, 15f);

        Sprite artSprite = ResolveItemSprite(item);
        if (artSprite != null)
        {
            Image art = UiFactory.CreateImage("Art", card, artSprite, Color.white);
            art.type = Image.Type.Simple;
            art.preserveAspect = true;
            art.raycastTarget = false;
            SetTopLeft(art.rectTransform, 15.5f, 17f, 84f, 84f);
        }
    }

    private static Sprite ResolveItemSprite(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return null;
        }

        Sprite sprite = LoadSprite(item.GeneratedShopSpritePath);
        if (sprite != null)
        {
            return sprite;
        }

        sprite = LoadSprite(item.ShopSpritePath);
        if (sprite != null)
        {
            return sprite;
        }

        sprite = LoadSprite(item.PreviewSpritePath);
        if (sprite != null)
        {
            return sprite;
        }

        return LoadSprite(item.InventorySpritePath);
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        return Resources.Load<Sprite>(resourcePath.Trim());
    }

    private void Complete()
    {
        System.Action callback = completed;
        completed = null;
        if (callback != null)
        {
            callback();
        }

        Destroy(gameObject);
    }

    private void BuildModalPanel(float x, float y, float width, float height)
    {
        RectTransform panel = CreateNode("Panel", exactFrame, x, y, width, height);

        Image fill = UiFactory.CreateImage("Fill", panel, UiTheme.RoundedTenSprite, PanelFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image border = UiFactory.CreateImage("Border", panel, UiTheme.RoundedTenOutlineSprite, UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);
    }

    private void CreateActionButton(string name, RectTransform parent, string label, float x, float y, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, height);
        Image fill = UiFactory.CreateImage("Fill", buttonRect, UiTheme.RoundedTenSprite, CtaBlue);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = true;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image border = UiFactory.CreateImage("Border", buttonRect, UiTheme.RoundedTenOutlineSprite, CtaBlueDark);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);

        TextMeshProUGUI text = CreateText("Label", buttonRect, label, 14, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(text.rectTransform, 0f, -1f, width, height);
        UiFactory.AddButton(buttonRect.gameObject, onClick);
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font != null ? font : UiTheme.DefaultFont;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        SetTopLeft(rect, x, y, width, height);
        return rect;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearFrame()
    {
        for (int i = exactFrame.childCount - 1; i >= 0; i--)
        {
            Transform child = exactFrame.GetChild(i);
            if (child != null && child.name != "Blocker")
            {
                Destroy(child.gameObject);
            }
        }
    }
}

public sealed class PawPalWalkEncounterInfoOverlayView : MonoBehaviour
{
    private static readonly Color32 PanelFill = new Color32(252, 248, 232, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);

    private const float FrameWidth = UiTheme.ReferenceWidth;
    private const float FrameHeight = UiTheme.ReferenceHeight;

    private System.Action completed;
    private RectTransform exactFrame;

    public static void Show(PawPalWalkGeneratedEventState walkEvent, IntroPetSpecies playerSpecies, string adviceText, System.Action completed)
    {
        GameObject overlayObject = new GameObject("PawPalWalkEncounterInfoOverlay");
        PawPalWalkEncounterInfoOverlayView overlay = overlayObject.AddComponent<PawPalWalkEncounterInfoOverlayView>();
        overlay.Initialize(walkEvent, playerSpecies, adviceText, completed);
    }

    private void Initialize(PawPalWalkGeneratedEventState walkEvent, IntroPetSpecies playerSpecies, string adviceText, System.Action onCompleted)
    {
        completed = onCompleted;
        BuildCanvas();
        BuildContent(walkEvent, playerSpecies, adviceText);
    }

    private void BuildCanvas()
    {
        RectTransform canvasRect = gameObject.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            canvasRect = gameObject.AddComponent<RectTransform>();
        }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        exactFrame = UiFactory.CreateRect("ExactFrame", canvasRect);
        exactFrame.anchorMin = new Vector2(0.5f, 0.5f);
        exactFrame.anchorMax = new Vector2(0.5f, 0.5f);
        exactFrame.pivot = new Vector2(0.5f, 0.5f);
        exactFrame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        Image blocker = UiFactory.CreateImage("Blocker", exactFrame, UiTheme.WhiteSprite, new Color(0f, 0f, 0f, 0.18f));
        blocker.type = Image.Type.Simple;
        blocker.preserveAspect = false;
        blocker.raycastTarget = true;
        Stretch(blocker.rectTransform);
    }

    private void BuildContent(PawPalWalkGeneratedEventState walkEvent, IntroPetSpecies playerSpecies, string adviceText)
    {
        BuildModalPanel(42f, 312f, 309f, 222f);

        string titleText = playerSpecies == IntroPetSpecies.Cat ? "Your cat met someone!" : "Your dog met someone!";
        TextMeshProUGUI title = CreateText("Title", exactFrame, titleText, 21, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        title.enableAutoSizing = true;
        title.fontSizeMin = 14f;
        title.fontSizeMax = 21f;
        title.textWrappingMode = TextWrappingModes.Normal;
        SetTopLeft(title.rectTransform, 64f, 337f, 265f, 42f);

        string visitorName = ResolveVisitorName(walkEvent);
        TextMeshProUGUI visitor = CreateText("VisitorName", exactFrame, visitorName, 15, UiTheme.NavBrand, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        visitor.enableAutoSizing = true;
        visitor.fontSizeMin = 10f;
        visitor.fontSizeMax = 15f;
        visitor.textWrappingMode = TextWrappingModes.NoWrap;
        SetTopLeft(visitor.rectTransform, 68f, 383f, 257f, 24f);

        TextMeshProUGUI body = CreateText("Advice", exactFrame, string.IsNullOrWhiteSpace(adviceText) ? "A calm greeting makes city walks feel friendly." : adviceText.Trim(), 14, UiTheme.BodyText, UiTheme.NavRegularFont, TextAlignmentOptions.Top);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.enableAutoSizing = true;
        body.fontSizeMin = 10f;
        body.fontSizeMax = 14f;
        SetTopLeft(body.rectTransform, 68f, 417f, 257f, 68f);

        CreateActionButton("Continue", exactFrame, "Continue", 142.5f, 493f, 108f, 30f, Complete);
    }

    private static string ResolveVisitorName(PawPalWalkGeneratedEventState walkEvent)
    {
        if (walkEvent != null && !string.IsNullOrWhiteSpace(walkEvent.VisitorDisplayName))
        {
            return "Met " + walkEvent.VisitorDisplayName.Trim();
        }

        if (walkEvent != null && !string.IsNullOrWhiteSpace(walkEvent.DisplayName))
        {
            return "Met " + walkEvent.DisplayName.Trim();
        }

        return "Met a friendly pet";
    }

    private void Complete()
    {
        System.Action callback = completed;
        completed = null;
        if (callback != null)
        {
            callback();
        }

        Destroy(gameObject);
    }

    private void BuildModalPanel(float x, float y, float width, float height)
    {
        RectTransform panel = CreateNode("Panel", exactFrame, x, y, width, height);

        Image fill = UiFactory.CreateImage("Fill", panel, UiTheme.RoundedTenSprite, PanelFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image border = UiFactory.CreateImage("Border", panel, UiTheme.RoundedTenOutlineSprite, UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);
    }

    private void CreateActionButton(string name, RectTransform parent, string label, float x, float y, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, height);
        Image fill = UiFactory.CreateImage("Fill", buttonRect, UiTheme.RoundedTenSprite, CtaBlue);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = true;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image border = UiFactory.CreateImage("Border", buttonRect, UiTheme.RoundedTenOutlineSprite, CtaBlueDark);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);

        TextMeshProUGUI text = CreateText("Label", buttonRect, label, 14, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(text.rectTransform, 0f, -1f, width, height);
        UiFactory.AddButton(buttonRect.gameObject, onClick);
    }

    private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font != null ? font : UiTheme.DefaultFont;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        SetTopLeft(rect, x, y, width, height);
        return rect;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
