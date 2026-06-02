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
    [SerializeField] private Vector3 brushSpawnOffset = new Vector3(0.34f, -0.16f, 0.15f);
    [SerializeField] private Vector3 brushSpawnEuler = new Vector3(12f, 200f, 82f);
    [SerializeField] private Vector3 brushDragOffset = new Vector3(0f, -0.06f, 0.02f);
    [SerializeField] private float brushPickupRadiusPixels = 132f;
    [SerializeField] private float brushPointerDepthOffset = 0.18f;
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

    [Header("Placement")]
    [SerializeField] private float spawnDistanceFromCamera = 0.92f;
    [SerializeField] private float groundRayHeight = 3f;
    [SerializeField] private float groundRayDistance = 8f;
    [SerializeField] private float floorPadding = 0.03f;
    [SerializeField] private float safePointRadius = 1.25f;
    [SerializeField] private bool presentRightSideToCamera = true;
    [SerializeField] private Vector3 cameraFocusOffset = new Vector3(0f, 0.35f, 0f);

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
    private RectTransform rewardBurstRoot;
    private RectTransform[] rewardBurstSymbols;
    private Image[] rewardBurstImages;
    private RectTransform inputBlockerRoot;
    private Image inputBlocker;
    private bool brushHeld;
    private bool brushingActive;
    private float brushingElapsed;
    private float nextBurstAt;
    private Vector2 lastPointerPosition;
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
        int cameraFocusId = 0;
        DogCycleCamera resolvedDogCamera = ResolveDogCamera();

        try
        {
            pet.PauseForSocial(false);
            yield return pet.PutDownHeldToyForInteraction();

            Vector3 presentationPoint = ResolvePresentationPoint(pet);
            yield return pet.MoveNearPrecise(presentationPoint, moveTimeout, DogMovementPace.Walk, preciseArrivalDistance);
            yield return RotatePetSideOn(pet);

            activeBrush = Instantiate(ResolveBrushPrefab());
            activeBrush.name = ResolveBrushPrefab().name;
            PositionBrush(pet);
            EnsureScratchAudioSource();

            if (pet.IsDog && resolvedDogCamera != null)
            {
                cameraFocusId = resolvedDogCamera.BeginPairFocus(
                    pet.RootTransform,
                    activeBrush.transform,
                    DogCameraFocusPriority.NeedInteraction,
                    cameraFocusOffset);
            }
            else
            {
                FocusCatInteractionCamera(pet);
            }

            ShowInputBlocker();
            EnsureRewardBurstOverlay();

            brushingElapsed = 0f;
            nextBurstAt = 0f;
            brushHeld = false;
            brushingActive = false;
            lastPointerPosition = Input.mousePosition;

            if (vocalRoutine != null)
            {
                StopCoroutine(vocalRoutine);
            }

            vocalRoutine = StartCoroutine(PlayPeriodicVocals(pet));

            while (brushingElapsed < Mathf.Max(0.1f, targetBrushingSeconds))
            {
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
            if (resolvedDogCamera != null && cameraFocusId != 0)
            {
                resolvedDogCamera.EndFocus(cameraFocusId);
            }

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

        Plane dragPlane = BuildBrushDragPlane(pet, camera);
        Ray pointerRay = camera.ScreenPointToRay(pointerPosition);
        float distance;
        if (!dragPlane.Raycast(pointerRay, out distance))
        {
            brushingActive = false;
            return;
        }

        Vector3 unclampedWorldPoint = pointerRay.GetPoint(distance) + ResolveCameraTangentOffset(camera);
        Vector3 clampedWorldPoint = ClampBrushWorldPoint(pet, unclampedWorldPoint);
        activeBrush.transform.position = clampedWorldPoint;

        float pointerSpeed = (pointerPosition - lastPointerPosition).magnitude / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        lastPointerPosition = pointerPosition;

        Vector3 localPoint = pet.RootTransform.InverseTransformPoint(activeBrush.transform.position);
        brushingActive = pointerSpeed >= Mathf.Max(1f, minimumBrushPixelsPerSecond) && IsWithinGroomingZone(pet, localPoint);
    }

    private Plane BuildBrushDragPlane(PawPalRoomPetHandle pet, Camera camera)
    {
        Vector3 planeNormal = camera.transform.forward;
        Vector3 planePoint = pet.FocusTransform != null ? pet.FocusTransform.position : pet.RootTransform.position;
        planePoint += planeNormal * brushPointerDepthOffset;
        return new Plane(-planeNormal, planePoint);
    }

    private Vector3 ResolveCameraTangentOffset(Camera camera)
    {
        return (camera.transform.right * brushDragOffset.x)
            + (camera.transform.up * brushDragOffset.y)
            + (camera.transform.forward * brushDragOffset.z);
    }

    private Vector3 ClampBrushWorldPoint(PawPalRoomPetHandle pet, Vector3 worldPoint)
    {
        Vector3 localPoint = pet.RootTransform.InverseTransformPoint(worldPoint);
        Vector3 dragExtents = pet.IsDog ? dogDragHalfExtents : catDragHalfExtents;
        Vector3 zoneCenter = pet.IsDog ? dogZoneCenter : catZoneCenter;
        localPoint.x = Mathf.Clamp(localPoint.x, zoneCenter.x - dragExtents.x, zoneCenter.x + dragExtents.x);
        localPoint.y = Mathf.Clamp(localPoint.y, zoneCenter.y - dragExtents.y, zoneCenter.y + dragExtents.y);
        localPoint.z = Mathf.Clamp(localPoint.z, zoneCenter.z - dragExtents.z, zoneCenter.z + dragExtents.z);
        return pet.RootTransform.TransformPoint(localPoint);
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

        Vector3 screenPoint = camera.WorldToScreenPoint(activeBrush.transform.position);
        if (screenPoint.z <= 0f)
        {
            return false;
        }

        return Vector2.Distance(pointerPosition, screenPoint) <= Mathf.Max(16f, brushPickupRadiusPixels);
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

    private void PositionBrush(PawPalRoomPetHandle pet)
    {
        if (activeBrush == null || pet == null || !pet.IsValid)
        {
            return;
        }

        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            camera = Camera.main;
        }

        Transform focus = pet.FocusTransform != null ? pet.FocusTransform : pet.RootTransform;
        Vector3 worldPosition = focus.position;
        if (camera != null)
        {
            worldPosition += (camera.transform.right * brushSpawnOffset.x)
                + (camera.transform.up * brushSpawnOffset.y)
                + (camera.transform.forward * brushSpawnOffset.z);
        }
        else
        {
            worldPosition += brushSpawnOffset;
        }

        activeBrush.transform.position = ResolveFloorPosition(activeBrush, worldPosition);
        activeBrush.transform.rotation = camera != null
            ? camera.transform.rotation * Quaternion.Euler(brushSpawnEuler)
            : Quaternion.Euler(brushSpawnEuler);
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

        return camera.transform.position + forward.normalized * Mathf.Max(0.2f, spawnDistanceFromCamera);
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
