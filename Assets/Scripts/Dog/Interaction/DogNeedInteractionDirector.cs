using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class DogNeedInteractionDirector : MonoBehaviour
{
    private const string EditorFoodBowlAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bowl_1_food_1.prefab";
    private const string EditorWaterBowlAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bowl_2_water.prefab";
    private const string EditorEatingAudioAssetPath = "Assets/Resources/Audio/dog_eating.mp3";
    private const string EditorDrinkingAudioAssetPath = "Assets/Resources/Audio/dog_drinking.mp3";
    private const float MinRuntimeBowlApproachDistance = 0.14f;
    private const float MaxRuntimeBowlApproachDistance = 0.5f;
    private const string StarBurstResourcePath = "UI/Interaction/star";
    private const float SymbolBurstDuration = 1.2f;
    private const float SymbolBurstScreenLift = 68f;
    private const float SymbolBurstRise = 44f;
    private const float SymbolBurstSpacing = 24f;
    private const float SymbolBurstIconSize = 34f;
    private const float SymbolBurstHeadOffset = 0.08f;

    [Header("Bowl Prefabs")]
    [SerializeField] private GameObject foodBowlPrefab;
    [SerializeField] private GameObject waterBowlPrefab;

    [Header("Scene References")]
    [SerializeField] private Camera roomCamera;
    [SerializeField] private DogCycleCamera dogCamera;

    [Header("Placement")]
    [SerializeField] private float spawnDistanceFromCamera = 1f;
    [SerializeField] private float foodBowlSpawnY = 0.052f;
    [SerializeField] private float waterBowlSpawnY = 0.062f;
    [SerializeField] private float groundRayHeight = 3f;
    [SerializeField] private float groundRayDistance = 8f;
    [SerializeField] private float bowlFloorPadding = 0.025f;
    [SerializeField] private float dogApproachDistance = 0.28f;
    [SerializeField] private float smallDogApproachDistance = 0.2f;
    [SerializeField] private float largeDogApproachDistance = 0.38f;
    [SerializeField] private Vector2 dogSizeForBowlApproachRange = new Vector2(0.75f, 1.55f);
    [SerializeField] private float preciseArrivalDistance = 0.04f;
    [SerializeField] private float maxBowlUsePlanarDistance = 0.48f;
    [SerializeField] private float moveTimeout = 7f;
    [SerializeField] private float faceDuration = 0.45f;
    [SerializeField] private float useLoopDuration = 5f;
    [SerializeField] private Vector3 cameraFocusOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float otherDogClearanceRadius = 0.85f;
    [SerializeField] private int approachCandidateCount = 12;

    [Header("Audio")]
    [SerializeField] private AudioClip eatingClip;
    [SerializeField] private AudioClip drinkingClip;
    [SerializeField, Range(0f, 1f)] private float bowlUseVolume = 0.75f;

    private Coroutine activeRoutine;
    private Coroutine rewardBurstRoutine;
    private GameObject activeBowl;
    private RectTransform rewardBurstRoot;
    private RectTransform[] rewardBurstSymbols;
    private Image[] rewardBurstImages;
    private readonly Vector2[] rewardBurstBaseOffsets =
    {
        new Vector2(-SymbolBurstSpacing, 2f),
        new Vector2(0f, -8f),
        new Vector2(SymbolBurstSpacing, 6f)
    };

    public static DogNeedInteractionDirector Instance { get; private set; }

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

        AutoAssignEditorAudioClips();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoAssignEditorAudioClips();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        CleanupActiveBowl();
    }

    public bool TryStartInteraction(PawPalDogNeed need, Action onCompleted)
    {
        if (activeRoutine != null)
        {
            return false;
        }

        if (need != PawPalDogNeed.Food && need != PawPalDogNeed.Water)
        {
            return false;
        }

        PawPalRoomPetHandle pet = ResolveActivePet();
        if (pet == null || !pet.IsValid)
        {
            Debug.LogWarning("DogNeedInteractionDirector could not start because no active room pet was found.");
            return false;
        }

        GameObject prefab = ResolveBowlPrefab(need);
        if (prefab == null)
        {
            Debug.LogWarning("DogNeedInteractionDirector has no " + need + " bowl prefab assigned.");
            return false;
        }

        pet.WakeForPlayerInteraction();
        if (!pet.PrepareForPlayerInteraction(true))
        {
            return false;
        }

        activeRoutine = StartCoroutine(InteractionRoutine(pet, prefab, need, onCompleted));
        return true;
    }

    private GameObject ResolveBowlPrefab(PawPalDogNeed need)
    {
        GameObject assignedPrefab = need == PawPalDogNeed.Food ? foodBowlPrefab : waterBowlPrefab;
        if (assignedPrefab != null)
        {
            return assignedPrefab;
        }

#if UNITY_EDITOR
        string assetPath = need == PawPalDogNeed.Food ? EditorFoodBowlAssetPath : EditorWaterBowlAssetPath;
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (editorPrefab != null)
        {
            if (need == PawPalDogNeed.Food)
            {
                foodBowlPrefab = editorPrefab;
            }
            else
            {
                waterBowlPrefab = editorPrefab;
            }

            return editorPrefab;
        }
#endif

        return null;
    }

    private IEnumerator InteractionRoutine(PawPalRoomPetHandle pet, GameObject bowlPrefab, PawPalDogNeed need, Action onCompleted)
    {
        bool completed = false;
        int cameraFocusId = 0;
        try
        {
            pet.PauseForSocial(false);

            activeBowl = Instantiate(bowlPrefab);
            activeBowl.name = bowlPrefab.name;
            PositionBowl(activeBowl, pet, need);

            DogCycleCamera resolvedDogCamera = ResolveDogCamera();
            if (resolvedDogCamera != null && pet.RootTransform != null)
            {
                cameraFocusId = resolvedDogCamera.BeginPairFocus(pet.RootTransform, activeBowl.transform, DogCameraFocusPriority.NeedInteraction, cameraFocusOffset);
            }

            Vector3 approachPoint = ResolveApproachPoint(pet, activeBowl.transform.position);
            yield return pet.MoveNearPlayerInteraction(approachPoint, moveTimeout, DogMovementPace.Trot, preciseArrivalDistance);
            if (!IsPetCloseEnoughForBowlUse(pet, activeBowl.transform.position, approachPoint))
            {
                approachPoint = ResolveApproachPoint(pet, activeBowl.transform.position);
                yield return pet.MoveNearPlayerInteraction(
                    approachPoint,
                    Mathf.Max(1f, moveTimeout * 0.45f),
                    DogMovementPace.Trot,
                    preciseArrivalDistance);

                if (!IsPetCloseEnoughForBowlUse(pet, activeBowl.transform.position, approachPoint))
                {
                    Debug.LogWarning("DogNeedInteractionDirector: " + pet.DisplayName + " could not reach a natural bowl-use position.");
                    yield break;
                }
            }

            yield return pet.FaceTarget(activeBowl.transform, faceDuration);
            StartCoroutine(PlayBowlUseAudio(pet.RootTransform, activeBowl.transform, need, useLoopDuration));
            yield return pet.PlayBowlUse(need == PawPalDogNeed.Water, useLoopDuration);
            ShowNeedCompletionBurst(pet);

            completed = true;
            if (onCompleted != null)
            {
                onCompleted();
            }
        }
        finally
        {
            DogCycleCamera resolvedDogCamera = ResolveDogCamera();
            if (resolvedDogCamera != null && cameraFocusId != 0)
            {
                resolvedDogCamera.EndFocus(cameraFocusId);
            }

            CleanupActiveBowl();

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

    private PawPalRoomPetHandle ResolveActivePet()
    {
        return PawPalRoomPetRuntime.ResolveActivePet();
    }

    private void PositionBowl(GameObject bowl, PawPalRoomPetHandle pet, PawPalDogNeed need)
    {
        Vector3 requestedPosition = ResolveCameraSpawnPosition(pet);
        Vector3 roomPoint;
        if (pet.TryGetRoomSafePoint(requestedPosition, 1.25f, out roomPoint))
        {
            requestedPosition = roomPoint;
        }

        Vector3 floorPosition = ResolveFloorPosition(bowl, requestedPosition);
        floorPosition.y = ResolveBowlSpawnY(need);
        bowl.transform.position = floorPosition;

        Vector3 lookDirection = pet.RootTransform.position - floorPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            bowl.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private float ResolveBowlSpawnY(PawPalDogNeed need)
    {
        return need == PawPalDogNeed.Water ? waterBowlSpawnY : foodBowlSpawnY;
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

    private Vector3 ResolveFloorPosition(GameObject bowl, Vector3 requestedPosition)
    {
        float bottomOffset = GetBottomOffset(bowl);
        RaycastHit hit;
        Vector3 rayOrigin = requestedPosition + Vector3.up * Mathf.Max(0.1f, groundRayHeight);
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, Mathf.Max(0.1f, groundRayDistance), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * (bottomOffset + bowlFloorPadding);
        }

        return requestedPosition + Vector3.up * (bottomOffset + bowlFloorPadding);
    }

    private Vector3 ResolveApproachPoint(PawPalRoomPetHandle pet, Vector3 bowlPosition)
    {
        float approachDistance = GetEffectiveBowlApproachDistance(pet);
        Vector3 preferredDirection = ResolvePreferredApproachDirection(pet, bowlPosition);
        Vector3 preferredPoint;
        if (TryResolveApproachCandidate(pet, bowlPosition, preferredDirection, approachDistance, out preferredPoint))
        {
            return preferredPoint;
        }

        int candidateCount = Mathf.Max(4, approachCandidateCount);
        float preferredYaw = Mathf.Atan2(preferredDirection.x, preferredDirection.z) * Mathf.Rad2Deg;
        for (int i = 0; i < candidateCount; i++)
        {
            int side = i % 2 == 0 ? 1 : -1;
            int step = (i / 2) + 1;
            float angle = preferredYaw + side * step * (180f / candidateCount);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 point;
            if (TryResolveApproachCandidate(pet, bowlPosition, direction, approachDistance, out point))
            {
                return point;
            }
        }

        Vector3 requested = bowlPosition + preferredDirection * approachDistance;
        Vector3 safePoint;
        if (pet.TryGetRoomSafePoint(requested, 0.25f, out safePoint))
        {
            return safePoint;
        }

        return requested;
    }

    private bool TryResolveApproachCandidate(PawPalRoomPetHandle pet, Vector3 bowlPosition, Vector3 direction, float approachDistance, out Vector3 point)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            point = bowlPosition;
            return false;
        }

        Vector3 requested = bowlPosition + direction.normalized * approachDistance;
        if (!pet.TryGetRoomSafePoint(requested, 0.25f, out point))
        {
            return false;
        }

        return IsPointClearOfOtherPets(point, pet.RootTransform, Mathf.Max(0.25f, otherDogClearanceRadius * 0.55f));
    }

    private static bool IsPointClearOfOtherPets(Vector3 point, Transform ignoredRoot, float radius)
    {
        float sqrRadius = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        Vector3 flatPoint = point;
        flatPoint.y = 0f;

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent otherDog = dogs[i];
            if (otherDog == null || otherDog.transform == ignoredRoot)
            {
                continue;
            }

            Vector3 otherPosition = otherDog.transform.position;
            otherPosition.y = 0f;
            if ((otherPosition - flatPoint).sqrMagnitude < sqrRadius)
            {
                return false;
            }
        }

        PawPalCatRoomAgent[] cats = FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent cat = cats[i];
            if (cat == null || cat.transform == ignoredRoot)
            {
                continue;
            }

            Vector3 otherPosition = cat.transform.position;
            otherPosition.y = 0f;
            if ((otherPosition - flatPoint).sqrMagnitude < sqrRadius)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 ResolvePreferredApproachDirection(PawPalRoomPetHandle pet, Vector3 bowlPosition)
    {
        Camera camera = ResolveRoomCamera();
        if (camera != null)
        {
            Vector3 fromCameraToBowl = bowlPosition - camera.transform.position;
            fromCameraToBowl.y = 0f;
            if (fromCameraToBowl.sqrMagnitude > 0.001f)
            {
                return fromCameraToBowl.normalized;
            }
        }

        Vector3 fromBowlToDog = pet.RootTransform.position - bowlPosition;
        fromBowlToDog.y = 0f;
        if (fromBowlToDog.sqrMagnitude > 0.001f)
        {
            return fromBowlToDog.normalized;
        }

        Vector3 fallback = -pet.RootTransform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.back;
    }

    private bool IsPetCloseEnoughForBowlUse(PawPalRoomPetHandle pet, Vector3 bowlPosition, Vector3 approachPoint)
    {
        Vector3 dogPosition = pet.RootTransform.position;
        dogPosition.y = 0f;
        Vector3 flatBowlPosition = bowlPosition;
        flatBowlPosition.y = 0f;
        Vector3 flatApproachPoint = approachPoint;
        flatApproachPoint.y = 0f;

        float approachDistance = Vector3.Distance(dogPosition, flatApproachPoint);
        float bowlDistance = Vector3.Distance(dogPosition, flatBowlPosition);
        return approachDistance <= Mathf.Max(0.08f, preciseArrivalDistance * 3f)
            && bowlDistance <= Mathf.Max(GetEffectiveBowlApproachDistance(pet) + 0.18f, maxBowlUsePlanarDistance);
    }

    private float GetEffectiveBowlApproachDistance(PawPalRoomPetHandle pet)
    {
        float mediumDistance = dogApproachDistance > 0f ? dogApproachDistance : 0.28f;
        float smallDistance = Mathf.Min(mediumDistance, smallDogApproachDistance > 0f ? smallDogApproachDistance : 0.2f);
        float largeDistance = Mathf.Max(mediumDistance, largeDogApproachDistance > 0f ? largeDogApproachDistance : 0.38f);

        float size01;
        if (!TryGetPetBowlApproachSize01(pet, out size01))
        {
            return Mathf.Clamp(mediumDistance, MinRuntimeBowlApproachDistance, MaxRuntimeBowlApproachDistance);
        }

        float distance = size01 <= 0.5f
            ? Mathf.Lerp(smallDistance, mediumDistance, size01 * 2f)
            : Mathf.Lerp(mediumDistance, largeDistance, (size01 - 0.5f) * 2f);

        return Mathf.Clamp(distance, MinRuntimeBowlApproachDistance, MaxRuntimeBowlApproachDistance);
    }

    private bool TryGetPetBowlApproachSize01(PawPalRoomPetHandle pet, out float size01)
    {
        size01 = 0.5f;
        if (pet == null || !pet.IsValid || pet.RootTransform == null)
        {
            return false;
        }

        Bounds bounds;
        if (TryGetRendererBounds(pet.RootTransform.gameObject, out bounds))
        {
            float planarSize = Mathf.Max(bounds.size.x, bounds.size.z);
            float heightAdjustedSize = bounds.size.y * 1.15f;
            float sizeMetric = Mathf.Max(planarSize, heightAdjustedSize);
            float minSize = Mathf.Min(dogSizeForBowlApproachRange.x, dogSizeForBowlApproachRange.y);
            float maxSize = Mathf.Max(dogSizeForBowlApproachRange.x, dogSizeForBowlApproachRange.y);
            size01 = Mathf.InverseLerp(minSize, maxSize, sizeMetric);
        }

        string dogName = pet.DisplayName;
        if (ContainsIgnoreCase(dogName, "puppy"))
        {
            size01 = Mathf.Min(size01, 0.18f);
        }
        else if (ContainsIgnoreCase(dogName, "corgi"))
        {
            size01 = Mathf.Clamp(size01, 0.45f, 0.75f);
        }

        return true;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        segment.y = 0f;
        if (segment.sqrMagnitude < 0.001f)
        {
            return Vector3.Distance(point, start);
        }

        Vector3 toPoint = point - start;
        toPoint.y = 0f;
        float t = Mathf.Clamp01(Vector3.Dot(toPoint, segment) / segment.sqrMagnitude);
        return Vector3.Distance(point, start + segment * t);
    }

    private IEnumerator PlayBowlUseAudio(Transform petRoot, Transform bowl, PawPalDogNeed need, float duration)
    {
        AudioClip clip = need == PawPalDogNeed.Water ? drinkingClip : eatingClip;
        if (clip == null || petRoot == null)
        {
            yield break;
        }

        GameObject audioObject = new GameObject(petRoot.name + "_" + need + "Audio");
        audioObject.transform.position = bowl != null ? bowl.position : petRoot.position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = PawPalAudioSettings.ApplySoundEffectsVolume(bowlUseVolume);
        source.spatialBlend = 0f;
        source.loop = true;
        source.playOnAwake = false;
        source.Play();

        float targetDuration = Mathf.Max(0.1f, duration);
        float elapsed = 0f;
        while (elapsed < targetDuration && source != null)
        {
            source.volume = PawPalAudioSettings.ApplySoundEffectsVolume(bowlUseVolume);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(audioObject);
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

        Canvas hostCanvas = ResolveRewardBurstCanvas();
        if (hostCanvas == null)
        {
            GameObject canvasObject = new GameObject("NeedRewardBurstCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hostCanvas = canvasObject.GetComponent<Canvas>();
            hostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hostCanvas.sortingOrder = 550;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }

        rewardBurstRoot = UiFactory.CreateRect("NeedRewardBurstRoot", hostCanvas.transform);
        UiFactory.Stretch(rewardBurstRoot, 0f, 0f, 0f, 0f);

        rewardBurstSymbols = new RectTransform[3];
        rewardBurstImages = new Image[3];
        for (int i = 0; i < rewardBurstSymbols.Length; i++)
        {
            RectTransform symbolRoot = UiFactory.CreateRect("NeedRewardSymbol_" + i, rewardBurstRoot);
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

    private static Canvas ResolveRewardBurstCanvas()
    {
        AppShellController shell = FindFirstObjectByType<AppShellController>(FindObjectsInactive.Include);
        if (shell != null)
        {
            return shell.GetComponent<Canvas>();
        }

        return FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    private Camera ResolveRoomCamera()
    {
        if (roomCamera != null)
        {
            return roomCamera;
        }

        roomCamera = Camera.main;
        return roomCamera;
    }

    private DogCycleCamera ResolveDogCamera()
    {
        if (dogCamera != null)
        {
            return dogCamera;
        }

        Camera camera = ResolveRoomCamera();
        if (camera != null)
        {
            dogCamera = camera.GetComponent<DogCycleCamera>();
        }

        if (dogCamera == null)
        {
            dogCamera = FindFirstObjectByType<DogCycleCamera>();
        }

        return dogCamera;
    }

    private void AutoAssignEditorAudioClips()
    {
#if UNITY_EDITOR
        if (eatingClip == null)
        {
            eatingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorEatingAudioAssetPath);
        }

        if (drinkingClip == null)
        {
            drinkingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorDrinkingAudioAssetPath);
        }
#endif
        PawPalAudioResources.AssignIfMissing(ref eatingClip, PawPalAudioResources.DogEating);
        PawPalAudioResources.AssignIfMissing(ref drinkingClip, PawPalAudioResources.DogDrinking);
    }

    private void CleanupActiveBowl()
    {
        if (activeBowl != null)
        {
            Destroy(activeBowl);
            activeBowl = null;
        }
    }

    private static float GetBottomOffset(GameObject instance)
    {
        if (instance == null)
        {
            return 0.05f;
        }

        Bounds bounds;
        if (!TryGetRendererBounds(instance, out bounds))
        {
            return 0.05f;
        }

        return Mathf.Max(0.02f, instance.transform.position.y - bounds.min.y);
    }

    private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
    {
        bounds = instance != null ? new Bounds(instance.transform.position, Vector3.zero) : new Bounds();
        if (instance == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
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

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrEmpty(value)
            && !string.IsNullOrEmpty(fragment)
            && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
