using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PawPalWalkSceneController : MonoBehaviour
{
    private enum WalkState
    {
        Running,
        PausedForStop,
        Completing
    }

    private const float GroundProbeHeight = 8f;
    private const float GroundProbeDistance = 24f;
    private const float WalkMusicVolume = 0.375f;
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
    private Animator walkerAnimator;
    private Camera sceneCamera;
    private AudioSource walkMusicSource;
    private AudioClip walkMusicClip;
    private float[] cumulativeRouteDistances = new float[0];
    private PawPalPetMovementProfile walkMovementProfile = PawPalPetMovementProfiles.DefaultProfile;
    private float totalRouteDistance = 1f;
    private float currentDistance;
    private float progress;
    private float runSpeed = 1f;
    private string returnSceneName = PawPalWalkSceneFlow.HomeSceneName;
    private WalkState state;
    private Coroutine stateRoutine;
    private string startupFailureMessage = string.Empty;
    private bool startupFailed;

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
        PawPalLeashRig.TryInstallSceneRig();
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

        currentDistance = Mathf.Min(totalRouteDistance, currentDistance + runSpeed * Time.deltaTime);
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

        if (!PawPalWalkGraphService.TryBuildDefaultRoutePlan(sceneBindings, out PawPalWalkRoutePlan routePlan, out string failureMessage))
        {
            startupFailureMessage = failureMessage;
            Debug.LogWarning("PawPalWalkSceneController could not resolve the default scene walk route. " + failureMessage);
            return false;
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
        walkerRoomAgent = preparedDog.GetComponent<DogRoomAgent>();
        walkerAnimator = preparedDog.GetComponent<Animator>();
        if (walkerAnimator == null)
        {
            walkerAnimator = preparedDog.GetComponentInChildren<Animator>(true);
        }

        if (walkerRoomAgent != null && runtimeDog != null)
        {
            walkerRoomAgent.SetRuntimeDogId(runtimeDog.Id);
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
        walkerRoomAgent = dogObject.GetComponent<DogRoomAgent>();
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

        dogObject.name = "WalkDog_" + selection.SafeName;
        DogRoomAgent roomAgent = dogObject.GetComponent<DogRoomAgent>();
        PawPalCatRoomAgent[] catAgents = dogObject.GetComponentsInChildren<PawPalCatRoomAgent>(true);
        for (int i = 0; i < catAgents.Length; i++)
        {
            if (catAgents[i] != null)
            {
                Destroy(catAgents[i]);
            }
        }

        ApplySpawnedDogPresentation(selection, dogObject);

        if (roomAgent != null)
        {
            roomAgent.SetRuntimeDogId(selection.RuntimePetId);
            roomAgent.ApplySelectedPetPresentation(selection);
            roomAgent.ConfigureSelectedPetRuntime(selection);
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

        IntroPetDefinition definition = ResolveIntroPetDefinition(dog);
        if (definition == null)
        {
            startupFailureMessage = "Could not match runtime dog '" + dog.DisplayName + "' / breed '" + dog.Breed + "' to an IntroPetDefinition.";
            Debug.LogWarning("PawPalWalkSceneController could not resolve an IntroPetDefinition for runtime dog '" + dog.Id + "' / breed '" + dog.Breed + "'.");
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
            PetName = string.IsNullOrWhiteSpace(dog.DisplayName) ? "Dog" : dog.DisplayName,
            RuntimePetId = dog.Id
        };
    }

    private static IntroPetDefinition ResolveIntroPetDefinition(PawPalDogState dog)
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
            if (definition == null || definition.Species != IntroPetSpecies.Dog)
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
            if (definition == null || definition.Species != IntroPetSpecies.Dog)
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

        if (walkerRoomAgent == null)
        {
            walkerRoomAgent = walker.GetComponent<DogRoomAgent>();
        }

        NavMeshAgent navMeshAgent = walker.GetComponent<NavMeshAgent>();
        if (walkerRoomAgent != null)
        {
            walkerRoomAgent.SetExternalWalkControl(true);
            walkerRoomAgent.SetExternalWalkPace(DogMovementPace.Walk);
            walkMovementProfile = walkerRoomAgent.MovementProfile;
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
        runSpeed = Mathf.Max(0.05f, walkMovementProfile.WalkSpeed * sceneBindings.RunSpeedMultiplier);
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

        if (walkerRoomAgent == null)
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

            if (behaviour == walkerRoomAgent)
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

    private void SetFallbackAnimatorLocomotion(bool moving)
    {
        if (walkerAnimator == null || walkerRoomAgent != null)
        {
            return;
        }

        if (HasAnimatorParameter(walkerAnimator, MoveHash, AnimatorControllerParameterType.Bool))
        {
            walkerAnimator.SetBool(MoveHash, moving);
        }

        if (HasAnimatorParameter(walkerAnimator, SpeedHash, AnimatorControllerParameterType.Float))
        {
            walkerAnimator.SetFloat(SpeedHash, moving ? walkMovementProfile.RunAnimatorSpeed : 0f);
        }

        if (HasAnimatorParameter(walkerAnimator, DirectionHash, AnimatorControllerParameterType.Float))
        {
            walkerAnimator.SetFloat(DirectionHash, 0f);
        }

        if (HasAnimatorParameter(walkerAnimator, IdleIndexHash, AnimatorControllerParameterType.Int))
        {
            walkerAnimator.SetInteger(IdleIndexHash, moving ? -1 : 99);
        }
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
