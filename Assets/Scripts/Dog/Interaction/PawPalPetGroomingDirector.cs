using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class PawPalPetGroomingDirector : MonoBehaviour
{
    private const string EditorBrushPrefabAssetPath = "Assets/BrushHair/BrushHair Variant.prefab";
    private const string StarBurstResourcePath = "UI/Interaction/star";
    private const float SymbolBurstDuration = 1.2f;
    private const float SymbolBurstScreenLift = 68f;
    private const float SymbolBurstRise = 44f;
    private const float SymbolBurstSpacing = 24f;
    private const float SymbolBurstIconSize = 34f;
    private const float SymbolBurstHeadOffset = 0.08f;

    [Header("Brush")]
    [SerializeField] private GameObject brushPrefab;
    [SerializeField] private Vector3 heldViewportPosition = new Vector3(0.66f, 0.42f, 0.68f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(12f, 200f, 82f);
    [SerializeField] private float heldVisualScaleMultiplier = 1.1f;
    [SerializeField] private bool moveHeldBrushWithPointer = true;
    [SerializeField] private float heldPointerMoveSensitivity = 1f;
    [SerializeField] private Vector2 heldViewportMin = new Vector2(0.36f, 0.24f);
    [SerializeField] private Vector2 heldViewportMax = new Vector2(0.84f, 0.74f);
    [SerializeField] private float heldMoveFollowSharpness = 18f;
    [SerializeField] private float brushPickupRadiusPixels = 132f;
    [SerializeField] private float minimumBrushPixelsPerSecond = 42f;

    [Header("Scene References")]
    [SerializeField] private Camera roomCamera;
    [SerializeField] private DogCycleCamera dogCamera;

    [Header("Interaction Timing")]
    [SerializeField] private float targetBrushingSeconds = 10f;
    [SerializeField] private float moveTimeout = 7f;
    [SerializeField] private float preciseArrivalDistance = 0.05f;
    [SerializeField] private float sideFacingDuration = 0.3f;
    [SerializeField] private float starBurstInterval = 0.55f;
    [SerializeField] private float barkIntervalMin = 2.8f;
    [SerializeField] private float barkIntervalMax = 4.8f;
    [SerializeField] private float dogApproachSampleRadius = 1.6f;
    [SerializeField] private float dogApproachDistance = 0.82f;
    [SerializeField] private float dogApproachSideOffset = 0.58f;
    [SerializeField] private float dogApproachWideSideOffset = 0.92f;

    [Header("Placement")]
    [SerializeField] private float catSpawnDistanceFromCamera = 0.92f;
    [SerializeField] private float groundRayHeight = 3f;
    [SerializeField] private float groundRayDistance = 8f;
    [SerializeField] private float floorPadding = 0.03f;
    [SerializeField] private float safePointRadius = 1.25f;
    [SerializeField] private bool presentRightSideToCamera = true;

    [Header("Grooming Zones")]
    [SerializeField] private Vector3 dogZoneCenter = new Vector3(0f, 0.46f, 0.02f);
    [SerializeField] private Vector3 dogZoneHalfExtents = new Vector3(0.52f, 0.52f, 0.42f);
    [SerializeField] private Vector3 catZoneCenter = new Vector3(0f, 0.28f, 0.04f);
    [SerializeField] private Vector3 catZoneHalfExtents = new Vector3(0.38f, 0.34f, 0.34f);
    [SerializeField] private Vector3 dogDragHalfExtents = new Vector3(0.72f, 0.7f, 0.42f);
    [SerializeField] private Vector3 catDragHalfExtents = new Vector3(0.58f, 0.5f, 0.34f);

    [Header("Audio")]
    [SerializeField] private AudioClip scratchingClip;
    [SerializeField, Range(0f, 1f)] private float scratchingVolume = 0.8f;

    private Coroutine activeRoutine;
    private Coroutine rewardBurstRoutine;
    private Coroutine vocalRoutine;
    private GameObject activeBrush;
    private AudioSource scratchAudioSource;
    private Transform holdAnchor;
    private RectTransform rewardBurstRoot;
    private RectTransform[] rewardBurstSymbols;
    private Image[] rewardBurstImages;
    private RectTransform inputBlockerRoot;
    private Image inputBlocker;
    private bool brushHeld;
    private bool brushingActive;
    private float brushingElapsed;
    private float nextBurstAt;
    private Vector2 pickupPointerPosition;
    private Vector2 lastPointerPosition;
    private Vector3 currentHeldViewportPosition;
    private readonly Vector2[] rewardBurstBaseOffsets =
    {
        new Vector2(-SymbolBurstSpacing, 2f),
        new Vector2(0f, -8f),
        new Vector2(SymbolBurstSpacing, 6f)
    };

    public static PawPalPetGroomingDirector Instance { get; private set; }

    public bool IsInteractionActive
    {
        get { return activeRoutine != null; }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoAssignEditorAssets();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoAssignEditorAssets();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        CleanupInteractionArtifacts();
    }

    public bool TryStartInteraction(Action onCompleted)
    {
        if (activeRoutine != null)
        {
            return false;
        }

        PawPalRoomPetHandle pet = PawPalRoomPetRuntime.ResolveActivePet();
        if (pet == null || !pet.IsValid || pet.RootTransform == null)
        {
            Debug.LogWarning("PawPalPetGroomingDirector could not start because no active room pet was found.");
            return false;
        }

        if (ResolveBrushPrefab() == null)
        {
            Debug.LogWarning("PawPalPetGroomingDirector has no brush prefab assigned.");
            return false;
        }

        pet.WakeForPlayerInteraction();
        if (!pet.PrepareForPlayerInteraction(true))
        {
            return false;
        }

        activeRoutine = StartCoroutine(GroomingRoutine(pet, onCompleted));
        return true;
    }

    private IEnumerator GroomingRoutine(PawPalRoomPetHandle pet, Action onCompleted)
    {
        bool completed = false;

        try
        {
            pet.PauseForSocial(false);
            yield return pet.PutDownHeldToyForInteraction();

            yield return MovePetIntoGroomingPresentation(pet);
            if (pet.IsDog && pet.DogAgent != null && !pet.DogAgent.WasLastTravelSuccessful)
            {
                yield break;
            }

            yield return RotatePetSideOn(pet);

            activeBrush = Instantiate(ResolveBrushPrefab());
            activeBrush.name = ResolveBrushPrefab().name;
            PrepareHeldBrush();
            EnsureScratchAudioSource();

            if (!pet.IsDog)
            {
                FocusCatInteractionCamera(pet);
            }

            ShowInputBlocker();
            EnsureRewardBurstOverlay();

            brushingElapsed = 0f;
            nextBurstAt = 0f;
            brushHeld = false;
            brushingActive = false;
            pickupPointerPosition = Input.mousePosition;
            lastPointerPosition = Input.mousePosition;
            currentHeldViewportPosition = heldViewportPosition;
            UpdateHoldAnchor();
            ApplyHeldBrushTransform();

            if (vocalRoutine != null)
            {
                StopCoroutine(vocalRoutine);
            }

            vocalRoutine = StartCoroutine(PlayPeriodicVocals(pet));

            while (brushingElapsed < Mathf.Max(0.1f, targetBrushingSeconds))
            {
                UpdateHoldAnchor();
                ApplyHeldBrushTransform();
                UpdateBrushInteraction(pet);

                if (brushingActive)
                {
                    brushingElapsed += Time.deltaTime;
                    if (Time.unscaledTime >= nextBurstAt)
                    {
                        ShowNeedCompletionBurst(pet);
                        nextBurstAt = Time.unscaledTime + Mathf.Max(0.15f, starBurstInterval);
                    }
                }

                UpdateScratchAudio();
                RefreshCatCameraLookIfNeeded(pet);
                yield return null;
            }

            ShowNeedCompletionBurst(pet);
            completed = true;
            if (onCompleted != null)
            {
                onCompleted();
            }
        }
        finally
        {
            if (vocalRoutine != null)
            {
                StopCoroutine(vocalRoutine);
                vocalRoutine = null;
            }

            CleanupInteractionArtifacts();

            if (pet != null && pet.IsValid)
            {
                pet.StartRoaming();
            }

            activeRoutine = null;
        }

        if (!completed)
        {
            yield break;
        }
    }

    private GameObject ResolveBrushPrefab()
    {
        if (brushPrefab != null)
        {
            return brushPrefab;
        }

#if UNITY_EDITOR
        brushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorBrushPrefabAssetPath);
#endif
        return brushPrefab;
    }

    private void UpdateBrushInteraction(PawPalRoomPetHandle pet)
    {
        if (activeBrush == null || pet == null || !pet.IsValid)
        {
            brushHeld = false;
            brushingActive = false;
            return;
        }

        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera == null)
        {
            brushHeld = false;
            brushingActive = false;
            return;
        }

        Vector2 pointerPosition = Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && IsPointerNearBrush(camera, pointerPosition))
        {
            brushHeld = true;
            pickupPointerPosition = pointerPosition;
            lastPointerPosition = pointerPosition;
        }

        if (!Input.GetMouseButton(0))
        {
            brushHeld = false;
            brushingActive = false;
            return;
        }

        if (!brushHeld)
        {
            brushingActive = false;
            return;
        }

        float pointerSpeed = (pointerPosition - lastPointerPosition).magnitude / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        lastPointerPosition = pointerPosition;

        Vector3 localPoint = pet.RootTransform.InverseTransformPoint(GetBrushInteractionPoint());
        brushingActive = pointerSpeed >= Mathf.Max(1f, minimumBrushPixelsPerSecond) && IsWithinGroomingZone(pet, localPoint);
    }

    private bool IsWithinGroomingZone(PawPalRoomPetHandle pet, Vector3 localPoint)
    {
        Vector3 center = pet.IsDog ? dogZoneCenter : catZoneCenter;
        Vector3 extents = pet.IsDog ? dogZoneHalfExtents : catZoneHalfExtents;
        return Mathf.Abs(localPoint.x - center.x) <= extents.x
            && Mathf.Abs(localPoint.y - center.y) <= extents.y
            && Mathf.Abs(localPoint.z - center.z) <= extents.z;
    }

    private bool IsPointerNearBrush(Camera camera, Vector2 pointerPosition)
    {
        if (activeBrush == null)
        {
            return false;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(GetBrushInteractionPoint());
        if (screenPoint.z <= 0f)
        {
            return false;
        }

        return Vector2.Distance(pointerPosition, screenPoint) <= Mathf.Max(16f, brushPickupRadiusPixels);
    }

    private IEnumerator MovePetIntoGroomingPresentation(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid || pet.RootTransform == null)
        {
            yield break;
        }

        Camera camera = ResolveRoomCamera();
        Vector3 presentationPoint;
        if (pet.IsDog && pet.DogAgent != null && camera != null && TryResolveDogCameraApproachPoint(pet.DogAgent, camera, out presentationPoint))
        {
            yield return pet.MoveNearPlayerInteraction(presentationPoint, moveTimeout, DogMovementPace.Trot, preciseArrivalDistance);
            if (pet.DogAgent.WasLastTravelSuccessful)
            {
                yield break;
            }
        }

        presentationPoint = ResolvePresentationPoint(pet);
        yield return pet.MoveNearPlayerInteraction(
            presentationPoint,
            moveTimeout,
            pet.IsDog ? DogMovementPace.Trot : DogMovementPace.Walk,
            preciseArrivalDistance);
    }

    private IEnumerator PlayPeriodicVocals(PawPalRoomPetHandle pet)
    {
        while (pet != null && pet.IsValid && activeRoutine != null)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(Mathf.Max(0.5f, barkIntervalMin), Mathf.Max(barkIntervalMin + 0.1f, barkIntervalMax)));
            if (pet == null || !pet.IsValid || activeRoutine == null)
            {
                yield break;
            }

            yield return pet.PlayBark(0.32f);
        }
    }

    private IEnumerator RotatePetSideOn(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid || pet.RootTransform == null)
        {
            yield break;
        }

        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            camera = Camera.main;
        }

        Vector3 cameraForward = camera != null ? camera.transform.forward : Vector3.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.001f)
        {
            cameraForward = pet.RootTransform.forward;
        }

        cameraForward.Normalize();
        Vector3 sideways = presentRightSideToCamera
            ? Vector3.Cross(Vector3.up, cameraForward)
            : Vector3.Cross(cameraForward, Vector3.up);
        if (sideways.sqrMagnitude < 0.001f)
        {
            sideways = pet.RootTransform.right;
        }

        Quaternion startRotation = pet.RootTransform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(sideways.normalized, Vector3.up);
        float duration = Mathf.Max(0.05f, sideFacingDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            pet.RootTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        pet.RootTransform.rotation = targetRotation;
    }

    private void PrepareHeldBrush()
    {
        if (activeBrush == null)
        {
            return;
        }

        EnsureHoldAnchor();
        Rigidbody body = activeBrush.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        Collider[] colliders = activeBrush.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        if (activeBrush.GetComponent<PawPalPlayerHeldToyMarker>() == null)
        {
            activeBrush.AddComponent<PawPalPlayerHeldToyMarker>();
        }

        activeBrush.transform.localScale *= Mathf.Max(0.01f, heldVisualScaleMultiplier);
    }

    private void EnsureScratchAudioSource()
    {
        if (activeBrush == null || scratchAudioSource != null)
        {
            return;
        }

        scratchAudioSource = activeBrush.GetComponent<AudioSource>();
        if (scratchAudioSource == null)
        {
            scratchAudioSource = activeBrush.AddComponent<AudioSource>();
        }

        scratchAudioSource.clip = scratchingClip;
        scratchAudioSource.playOnAwake = false;
        scratchAudioSource.loop = true;
        scratchAudioSource.spatialBlend = 0f;
        scratchAudioSource.volume = 0f;
    }

    private void UpdateScratchAudio()
    {
        if (scratchAudioSource == null || scratchingClip == null)
        {
            return;
        }

        float targetVolume = brushingActive ? PawPalAudioSettings.ApplySoundEffectsVolume(scratchingVolume) : 0f;
        scratchAudioSource.volume = targetVolume;
        if (brushingActive)
        {
            if (!scratchAudioSource.isPlaying)
            {
                scratchAudioSource.Play();
            }
        }
        else if (scratchAudioSource.isPlaying)
        {
            scratchAudioSource.Pause();
        }
    }

    private Vector3 ResolvePresentationPoint(PawPalRoomPetHandle pet)
    {
        Vector3 requestedPosition = ResolveCameraSpawnPosition(pet);
        Vector3 roomPoint;
        if (pet.TryGetRoomSafePoint(requestedPosition, safePointRadius, out roomPoint))
        {
            requestedPosition = roomPoint;
        }

        return ResolveFloorPosition(null, requestedPosition);
    }

    private Vector3 ResolveCameraSpawnPosition(PawPalRoomPetHandle pet)
    {
        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            return pet.RootTransform.position + pet.RootTransform.forward * 0.85f;
        }

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = pet.RootTransform.position - camera.transform.position;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = pet.RootTransform.forward;
        }

        return camera.transform.position + forward.normalized * Mathf.Max(0.2f, catSpawnDistanceFromCamera);
    }

    private Vector3 ResolveFloorPosition(GameObject target, Vector3 requestedPosition)
    {
        float bottomOffset = target != null ? GetBottomOffset(target) : 0f;
        RaycastHit hit;
        Vector3 rayOrigin = requestedPosition + Vector3.up * Mathf.Max(0.1f, groundRayHeight);
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, Mathf.Max(0.1f, groundRayDistance), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * (bottomOffset + floorPadding);
        }

        return requestedPosition + Vector3.up * (bottomOffset + floorPadding);
    }

    private static float GetBottomOffset(GameObject target)
    {
        if (target == null)
        {
            return 0f;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return 0f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return Mathf.Max(0f, target.transform.position.y - bounds.min.y);
    }

    private void FocusCatInteractionCamera(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        DogCycleCamera runtimeDogCamera = mainCamera.GetComponent<DogCycleCamera>();
        if (runtimeDogCamera != null)
        {
            runtimeDogCamera.enabled = false;
        }

        PawPalPetFollowCamera followCamera = mainCamera.GetComponent<PawPalPetFollowCamera>();
        if (followCamera == null)
        {
            followCamera = mainCamera.gameObject.AddComponent<PawPalPetFollowCamera>();
        }

        followCamera.enabled = true;
        followCamera.Focus(pet.FocusTransform != null ? pet.FocusTransform : pet.RootTransform, pet.HomeCameraOffset, true);
    }

    private void RefreshCatCameraLookIfNeeded(PawPalRoomPetHandle pet)
    {
        if (pet == null || pet.IsDog || pet.CatAgent == null)
        {
            return;
        }

        Camera camera = ResolveRoomCamera();
        if (camera != null)
        {
            pet.CatAgent.RequestInteractionCameraLook(camera.transform, 0.45f);
        }
    }

    private Camera ResolveRoomCamera()
    {
        if (roomCamera != null)
        {
            return roomCamera;
        }

        if (dogCamera != null)
        {
            Camera camera = dogCamera.GetComponent<Camera>();
            if (camera != null)
            {
                return camera;
            }
        }

        return Camera.main;
    }

    private DogCycleCamera ResolveDogCamera()
    {
        if (dogCamera != null)
        {
            return dogCamera;
        }

        Camera mainCamera = Camera.main;
        DogCycleCamera resolvedCamera = mainCamera != null ? mainCamera.GetComponent<DogCycleCamera>() : null;
        if (resolvedCamera != null)
        {
            dogCamera = resolvedCamera;
            return resolvedCamera;
        }

        resolvedCamera = FindFirstObjectByType<DogCycleCamera>();
        if (resolvedCamera != null)
        {
            dogCamera = resolvedCamera;
        }

        return dogCamera;
    }

    private void EnsureHoldAnchor()
    {
        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            return;
        }

        if (holdAnchor == null)
        {
            GameObject holdObject = new GameObject("PawFriendsPlayerBrushHoldAnchor");
            holdObject.hideFlags = HideFlags.HideAndDontSave;
            holdAnchor = holdObject.transform;
            holdAnchor.SetParent(camera.transform, false);
        }
    }

    private void UpdateHoldAnchor()
    {
        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            return;
        }

        EnsureHoldAnchor();
        if (holdAnchor == null)
        {
            return;
        }

        Vector3 targetViewportPoint = GetTargetHeldViewportPosition();
        if (activeBrush != null && heldMoveFollowSharpness > 0f)
        {
            float t = 1f - Mathf.Exp(-heldMoveFollowSharpness * Time.deltaTime);
            currentHeldViewportPosition = Vector3.Lerp(currentHeldViewportPosition, targetViewportPoint, t);
        }
        else
        {
            currentHeldViewportPosition = targetViewportPoint;
        }

        Vector3 viewportPoint = currentHeldViewportPosition;
        viewportPoint.z = Mathf.Max(0.05f, viewportPoint.z);
        holdAnchor.position = camera.ViewportToWorldPoint(viewportPoint);
        holdAnchor.rotation = camera.transform.rotation * Quaternion.Euler(heldLocalEulerAngles);
    }

    private Vector3 GetTargetHeldViewportPosition()
    {
        if (!moveHeldBrushWithPointer || activeBrush == null || !brushHeld)
        {
            return heldViewportPosition;
        }

        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Vector2 pointerDelta = (Vector2)Input.mousePosition - pickupPointerPosition;
        Vector2 viewportDelta = new Vector2(pointerDelta.x / screenSize.x, pointerDelta.y / screenSize.y)
            * Mathf.Max(0f, heldPointerMoveSensitivity);

        Vector2 viewport = new Vector2(heldViewportPosition.x, heldViewportPosition.y) + viewportDelta;
        viewport.x = Mathf.Clamp(viewport.x, Mathf.Min(heldViewportMin.x, heldViewportMax.x), Mathf.Max(heldViewportMin.x, heldViewportMax.x));
        viewport.y = Mathf.Clamp(viewport.y, Mathf.Min(heldViewportMin.y, heldViewportMax.y), Mathf.Max(heldViewportMin.y, heldViewportMax.y));
        return new Vector3(viewport.x, viewport.y, heldViewportPosition.z);
    }

    private void ApplyHeldBrushTransform()
    {
        if (activeBrush == null || holdAnchor == null)
        {
            return;
        }

        activeBrush.transform.SetPositionAndRotation(holdAnchor.position, holdAnchor.rotation);
        AlignHeldBrushVisualToAnchor();
    }

    private void AlignHeldBrushVisualToAnchor()
    {
        if (activeBrush == null || holdAnchor == null)
        {
            return;
        }

        Bounds bounds;
        if (!TryGetVisualBounds(activeBrush, out bounds))
        {
            return;
        }

        activeBrush.transform.position += holdAnchor.position - bounds.center;
    }

    private Vector3 GetBrushInteractionPoint()
    {
        if (activeBrush == null)
        {
            return Vector3.zero;
        }

        Bounds bounds;
        if (TryGetVisualBounds(activeBrush, out bounds))
        {
            return bounds.center;
        }

        return activeBrush.transform.position;
    }

    private static bool TryGetVisualBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private bool TryResolveDogCameraApproachPoint(DogRoomAgent dog, Camera camera, out Vector3 approachPoint)
    {
        approachPoint = dog != null ? dog.transform.position : Vector3.zero;
        if (dog == null || camera == null)
        {
            return false;
        }

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = dog.transform.position - camera.transform.position;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = -dog.transform.forward;
        }

        Vector3 right = camera.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }
        else
        {
            right.Normalize();
        }

        float bestScore = float.MaxValue;
        bool foundCandidate = false;
        float[] sideOffsets =
        {
            0f,
            -dogApproachSideOffset,
            dogApproachSideOffset,
            -dogApproachWideSideOffset,
            dogApproachWideSideOffset
        };

        for (int i = 0; i < sideOffsets.Length; i++)
        {
            Vector3 candidate = camera.transform.position
                + forward.normalized * Mathf.Max(0.2f, dogApproachDistance)
                + right * sideOffsets[i];
            candidate.y = dog.transform.position.y;

            Vector3 safePoint;
            if (!dog.TryGetRoomSafePoint(candidate, dogApproachSampleRadius, out safePoint))
            {
                continue;
            }

            if (dog.IsToyBlockingPathTo(safePoint, 0.14f))
            {
                continue;
            }

            float pathDistance;
            float score = dog.TryEstimateRoomPathDistance(safePoint, out pathDistance)
                ? pathDistance + Mathf.Abs(sideOffsets[i]) * 0.08f
                : Vector3.Distance(dog.transform.position, safePoint);

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            approachPoint = safePoint;
            foundCandidate = true;
        }

        return foundCandidate;
    }

    private void ShowNeedCompletionBurst(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            return;
        }

        EnsureRewardBurstOverlay();
        if (rewardBurstRoot == null || rewardBurstSymbols == null || rewardBurstImages == null)
        {
            return;
        }

        if (rewardBurstRoutine != null)
        {
            StopCoroutine(rewardBurstRoutine);
            rewardBurstRoutine = null;
        }

        rewardBurstRoutine = StartCoroutine(PlayNeedCompletionBurstRoutine(pet));
    }

    private IEnumerator PlayNeedCompletionBurstRoutine(PawPalRoomPetHandle pet)
    {
        Sprite burstSprite = Resources.Load<Sprite>(StarBurstResourcePath);
        if (burstSprite == null)
        {
            yield break;
        }

        for (int i = 0; i < rewardBurstSymbols.Length; i++)
        {
            if (rewardBurstSymbols[i] == null || rewardBurstImages[i] == null)
            {
                continue;
            }

            rewardBurstImages[i].sprite = burstSprite;
            rewardBurstImages[i].color = Color.white;
            rewardBurstSymbols[i].localScale = Vector3.one;
            rewardBurstSymbols[i].gameObject.SetActive(true);
        }

        PawPalUiAudio.PlayHearts();

        float shownAt = Time.unscaledTime;
        float visibleUntil = shownAt + SymbolBurstDuration;
        while (Time.unscaledTime < visibleUntil)
        {
            UpdateRewardBurstPosition(pet, shownAt);
            yield return null;
        }

        HideRewardBurst();
        rewardBurstRoutine = null;
    }

    private void EnsureRewardBurstOverlay()
    {
        if (rewardBurstRoot != null)
        {
            return;
        }

        Canvas hostCanvas = ResolveOverlayCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        rewardBurstRoot = UiFactory.CreateRect("GroomingRewardBurstRoot", hostCanvas.transform);
        UiFactory.Stretch(rewardBurstRoot, 0f, 0f, 0f, 0f);

        rewardBurstSymbols = new RectTransform[3];
        rewardBurstImages = new Image[3];
        for (int i = 0; i < rewardBurstSymbols.Length; i++)
        {
            RectTransform symbolRoot = UiFactory.CreateRect("GroomingRewardSymbol_" + i, rewardBurstRoot);
            symbolRoot.anchorMin = new Vector2(0.5f, 0.5f);
            symbolRoot.anchorMax = new Vector2(0.5f, 0.5f);
            symbolRoot.pivot = new Vector2(0.5f, 0.5f);
            symbolRoot.sizeDelta = new Vector2(SymbolBurstIconSize, SymbolBurstIconSize);

            Image image = UiFactory.CreateImage("Icon", symbolRoot, UiTheme.WhiteSprite, Color.white);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            UiFactory.Stretch(image.rectTransform, 0f, 0f, 0f, 0f);

            Shadow shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;

            symbolRoot.gameObject.SetActive(false);
            rewardBurstSymbols[i] = symbolRoot;
            rewardBurstImages[i] = image;
        }
    }

    private void UpdateRewardBurstPosition(PawPalRoomPetHandle pet, float shownAt)
    {
        if (rewardBurstRoot == null || rewardBurstSymbols == null)
        {
            return;
        }

        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            camera = Camera.main;
        }

        Transform focus = pet != null ? pet.FocusTransform : null;
        if (focus == null)
        {
            focus = pet != null ? pet.RootTransform : null;
        }

        if (focus == null)
        {
            HideRewardBurst();
            return;
        }

        Vector3 worldPosition = focus.position + Vector3.up * SymbolBurstHeadOffset;
        Vector3 screenPoint = camera != null ? camera.WorldToScreenPoint(worldPosition) : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
        if (screenPoint.z <= 0f)
        {
            HideRewardBurst();
            return;
        }

        Vector2 anchoredBase;
        RectTransform canvasRect = rewardBurstRoot;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out anchoredBase))
        {
            return;
        }

        float progress = Mathf.Clamp01((Time.unscaledTime - shownAt) / SymbolBurstDuration);
        float eased = 1f - Mathf.Pow(1f - progress, 2f);
        float rise = SymbolBurstScreenLift + (SymbolBurstRise * eased);
        float alpha = 1f - eased;
        float scale = Mathf.Lerp(1f, 1.1f, eased);

        for (int i = 0; i < rewardBurstSymbols.Length; i++)
        {
            RectTransform symbol = rewardBurstSymbols[i];
            Image image = rewardBurstImages[i];
            if (symbol == null || image == null)
            {
                continue;
            }

            symbol.anchoredPosition = anchoredBase + rewardBurstBaseOffsets[i] + new Vector2(0f, rise);
            symbol.localScale = new Vector3(scale, scale, 1f);
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }

    private void HideRewardBurst()
    {
        if (rewardBurstSymbols == null)
        {
            return;
        }

        for (int i = 0; i < rewardBurstSymbols.Length; i++)
        {
            if (rewardBurstSymbols[i] == null)
            {
                continue;
            }

            rewardBurstSymbols[i].gameObject.SetActive(false);
            rewardBurstSymbols[i].localScale = Vector3.one;
        }
    }

    private void ShowInputBlocker()
    {
        Canvas hostCanvas = ResolveOverlayCanvas();
        if (hostCanvas == null)
        {
            return;
        }

        if (inputBlockerRoot == null)
        {
            inputBlockerRoot = UiFactory.CreateRect("GroomingInputBlocker", hostCanvas.transform);
            UiFactory.Stretch(inputBlockerRoot, 0f, 0f, 0f, 0f);
            inputBlocker = UiFactory.CreateImage("Blocker", inputBlockerRoot, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
            UiFactory.Stretch(inputBlocker.rectTransform, 0f, 0f, 0f, 0f);
            inputBlocker.raycastTarget = true;
        }

        inputBlockerRoot.gameObject.SetActive(true);
    }

    private void HideInputBlocker()
    {
        if (inputBlockerRoot != null)
        {
            inputBlockerRoot.gameObject.SetActive(false);
        }
    }

    private Canvas ResolveOverlayCanvas()
    {
        AppShellController shell = FindFirstObjectByType<AppShellController>(FindObjectsInactive.Include);
        if (shell != null)
        {
            return shell.GetComponent<Canvas>();
        }

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null)
        {
            return canvas;
        }

        GameObject canvasObject = new GameObject("GroomingOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void CleanupInteractionArtifacts()
    {
        brushHeld = false;
        brushingActive = false;
        brushingElapsed = 0f;
        nextBurstAt = 0f;

        if (scratchAudioSource != null)
        {
            scratchAudioSource.Stop();
            scratchAudioSource = null;
        }

        if (activeBrush != null)
        {
            Destroy(activeBrush);
            activeBrush = null;
        }

        if (holdAnchor != null)
        {
            Destroy(holdAnchor.gameObject);
            holdAnchor = null;
        }

        HideInputBlocker();
        HideRewardBurst();
    }

    private void AutoAssignEditorAssets()
    {
        PawPalAudioResources.AssignIfMissing(ref scratchingClip, PawPalAudioResources.ScratchAndShaking);

#if UNITY_EDITOR
        if (brushPrefab == null)
        {
            brushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorBrushPrefabAssetPath);
        }
#endif
    }
}
