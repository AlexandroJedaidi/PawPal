using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum DogCameraFocusPriority
{
    AmbientToy = 10,
    Fetch = 20,
    NeedInteraction = 30,
    SocialInteraction = 40
}

[DisallowMultipleComponent]
public class DogCycleCamera : MonoBehaviour
{
    private const float MinimumSmartAutoSwitchInterval = 12f;
    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

    [SerializeField] private DogRoomAgent[] dogs;
    [SerializeField] private float switchInterval = 5f;
    [SerializeField] private float runtimeSelectionHoldDuration = 5f;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Switching")]
    [SerializeField] private float switchBlendDuration = 1.25f;
    [SerializeField] private float rotationSmooth = 6f;
    [SerializeField] private bool keepInitialPosition = true;
    [SerializeField] private float manualLookHoldDuration = 5f;

    [Header("Manual Look")]
    [SerializeField] private bool allowManualLook = true;
    [SerializeField] private float manualLookSensitivity = 0.18f;
    [SerializeField] private float manualPitchMin = -35f;
    [SerializeField] private float manualPitchMax = 35f;

    [Header("Slow Zoom")]
    [SerializeField] private bool useFovZoom = true;
    [SerializeField] private float baseFieldOfView = 0f;
    [SerializeField] private float zoomFieldOfView = 30f;
    [SerializeField] private float minimumZoomFovDrop = 30f;
    [SerializeField] private float minimumZoomFieldOfView = 24f;
    [SerializeField, Range(0f, 1f)] private float zoomChanceOnSwitch = 0.45f;
    [SerializeField] private float zoomInDuration = 1.75f;
    [SerializeField] private float zoomHoldDuration = 0.75f;
    [SerializeField] private float zoomOutDuration = 2.25f;
    [SerializeField] private float minimumZoomCooldown = 8f;
    [SerializeField] private Vector3 zoomHeadFocusOffset = new Vector3(0f, 0.03f, 0f);

    [Header("Smart Focus")]
    [SerializeField] private float minimumAutoSwitchInterval = 12f;
    [SerializeField] private float focusReleaseLingerDuration = 0.35f;
    [SerializeField] private float postFocusSwitchCooldown = 3f;

    private Camera attachedCamera;
    private Vector3 fixedPosition;
    private int activeIndex;
    private float switchTimer;
    private bool hasFocusPoint;
    private Vector3 currentFocusPoint;
    private Vector3 switchStartFocusPoint;
    private float switchBlendTimer;
    private float nextAllowedZoomTime;
    private float runtimeSelectionHoldUntil;
    private Coroutine zoomRoutine;
    private PawPalGameRuntime runtime;
    private int lastObservedRuntimeDogIndex = -1;
    private bool manualLookInitialized;
    private float manualLookYaw;
    private float manualLookPitch;
    private float manualLookHoldUntil;
    private bool mouseDragActive;
    private int activeTouchFingerId = -1;
    private Vector2 lastManualPointerPosition;
    private bool zoomFocusActive;
    private int legacyInteractionFocusId;
    private int nextFocusClaimId = 1;
    private int activeFocusClaimId;
    private float postFocusHoldUntil;
    private readonly List<DogCameraFocusClaim> focusClaims = new List<DogCameraFocusClaim>();
    private readonly Dictionary<DogRoomAgent, Transform> headTargetsByDog = new Dictionary<DogRoomAgent, Transform>();

    private sealed class DogCameraFocusClaim
    {
        public int Id;
        public Transform Target;
        public Transform SecondTarget;
        public Vector3 Offset;
        public DogCameraFocusPriority Priority;
        public DogRoomAgent PrimaryDog;
        public float StartedAt;
        public bool Released;
        public float ReleaseUntil;
    }

    public Transform ActiveTarget
    {
        get
        {
            DogRoomAgent dog = GetActiveDog();
            return dog != null ? dog.transform : null;
        }
    }

    public static bool TryFocusRuntimeActiveDogFromSelection()
    {
        PawPalGameRuntime runtimeInstance = PawPalGameRuntime.Instance;
        Camera mainCamera = Camera.main;
        DogCycleCamera dogCamera = null;

        if (mainCamera != null)
        {
            dogCamera = mainCamera.GetComponent<DogCycleCamera>();
        }

        if (dogCamera == null)
        {
            dogCamera = FindFirstObjectByType<DogCycleCamera>();
        }

        if (dogCamera == null && mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null && HasSceneDogs())
        {
            dogCamera = mainCamera.gameObject.AddComponent<DogCycleCamera>();
        }

        if (dogCamera != null)
        {
            dogCamera.FocusRuntimeActiveDogFromSelection();
            return true;
        }

        CameraFollow legacyCamera = mainCamera != null ? mainCamera.GetComponent<CameraFollow>() : null;
        if (legacyCamera != null && runtimeInstance != null && legacyCamera.target != null && legacyCamera.target.Length > 0)
        {
            legacyCamera.index = Mathf.Clamp(runtimeInstance.ActiveDogIndex, 0, legacyCamera.target.Length - 1);
            return true;
        }

        return false;
    }

    public void FocusRuntimeActiveDogFromSelection()
    {
        if (runtime == null)
        {
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime == null || runtime.ActiveDog == null)
        {
            return;
        }

        focusClaims.Clear();
        activeFocusClaimId = 0;
        legacyInteractionFocusId = 0;
        manualLookHoldUntil = 0f;
        mouseDragActive = false;
        activeTouchFingerId = -1;
        switchTimer = 0f;
        postFocusHoldUntil = Time.time + Mathf.Max(0f, postFocusSwitchCooldown);
        runtimeSelectionHoldUntil = Time.time + Mathf.Max(runtimeSelectionHoldDuration, switchBlendDuration + 0.25f);
        StopZoomForFocus();
        SyncToRuntimeActiveDog(true);
    }

    private static bool HasSceneDogs()
    {
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        return agents != null && agents.Length > 0;
    }

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null && baseFieldOfView <= 0f)
        {
            baseFieldOfView = attachedCamera.fieldOfView;
        }

        fixedPosition = transform.position;
        runtime = PawPalGameRuntime.Instance;
        RefreshDogsIfNeeded();
        SyncToRuntimeActiveDog(false);
    }

    private void OnEnable()
    {
        runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
        }

        SyncToRuntimeActiveDog(false);
    }

    private void OnDisable()
    {
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }

        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }

        focusClaims.Clear();
        activeFocusClaimId = 0;
        legacyInteractionFocusId = 0;
        zoomFocusActive = false;

        if (attachedCamera != null && baseFieldOfView > 0f)
        {
            attachedCamera.fieldOfView = baseFieldOfView;
        }
    }

    private void LateUpdate()
    {
        RefreshDogsIfNeeded();

        if (dogs == null || dogs.Length == 0)
        {
            return;
        }

        HandleManualLookInput();
        if (IsManualLookActive())
        {
            if (keepInitialPosition)
            {
                transform.position = fixedPosition;
            }

            Quaternion manualTargetRotation = Quaternion.Euler(manualLookPitch, manualLookYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, manualTargetRotation, Time.deltaTime * rotationSmooth);
            return;
        }

        DogCameraFocusClaim focusClaim = GetBestFocusClaim();
        if (focusClaim != null)
        {
            UpdateClaimedFocus(focusClaim);
            return;
        }

        if (activeFocusClaimId != 0)
        {
            ClearActiveFocusClaim();
        }

        bool usingRuntimeSelection = runtime != null && runtime.ActiveDog != null && Time.time < runtimeSelectionHoldUntil;
        if (!usingRuntimeSelection)
        {
            if (Time.time >= postFocusHoldUntil)
            {
                switchTimer += Time.deltaTime;
                if (switchTimer >= GetEffectiveSwitchInterval())
                {
                    switchTimer = 0f;
                    SwitchToIndex(NextValidIndex(activeIndex + 1), true);
                    TryStartZoom();
                }
            }
        }
        else
        {
            SyncToRuntimeActiveDog(true);
        }

        DogRoomAgent activeDog = GetActiveDog();
        if (activeDog == null)
        {
            return;
        }

        if (keepInitialPosition)
        {
            transform.position = fixedPosition;
        }

        Vector3 focusPoint = GetSmoothedFocusPoint(GetCameraFocusPoint(activeDog));
        Vector3 toTarget = focusPoint - transform.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
    }

    public void BeginInteractionFocus(Transform target, Vector3 offset)
    {
        if (legacyInteractionFocusId != 0)
        {
            EndFocus(legacyInteractionFocusId);
        }

        legacyInteractionFocusId = BeginFocus(target, DogCameraFocusPriority.NeedInteraction, offset);
    }

    public void BeginPairInteractionFocus(Transform firstTarget, Transform secondTarget, Vector3 offset)
    {
        if (legacyInteractionFocusId != 0)
        {
            EndFocus(legacyInteractionFocusId);
        }

        legacyInteractionFocusId = BeginPairFocus(firstTarget, secondTarget, DogCameraFocusPriority.SocialInteraction, offset);
    }

    public int BeginFocus(Transform target, DogCameraFocusPriority priority, Vector3 offset)
    {
        if (target == null)
        {
            return 0;
        }

        return BeginFocusInternal(target, null, priority, offset);
    }

    public int BeginPairFocus(Transform firstTarget, Transform secondTarget, DogCameraFocusPriority priority, Vector3 offset)
    {
        if (firstTarget == null || secondTarget == null)
        {
            return 0;
        }

        return BeginFocusInternal(firstTarget, secondTarget, priority, offset);
    }

    private int BeginFocusInternal(Transform target, Transform secondTarget, DogCameraFocusPriority priority, Vector3 offset)
    {
        DogCameraFocusClaim claim = new DogCameraFocusClaim
        {
            Id = nextFocusClaimId++,
            Target = target,
            SecondTarget = secondTarget,
            Offset = offset,
            Priority = priority,
            PrimaryDog = ResolveFocusDog(target, secondTarget),
            StartedAt = Time.time
        };

        if (nextFocusClaimId == int.MaxValue)
        {
            nextFocusClaimId = 1;
        }
        else if (nextFocusClaimId <= 0)
        {
            nextFocusClaimId = 1;
        }

        focusClaims.Add(claim);
        manualLookHoldUntil = 0f;
        mouseDragActive = false;
        activeTouchFingerId = -1;
        switchTimer = 0f;
        postFocusHoldUntil = Time.time + Mathf.Max(0f, postFocusSwitchCooldown);
        StopZoomForFocus();

        if (claim.PrimaryDog != null)
        {
            SelectRuntimeDogForAgent(claim.PrimaryDog);
        }

        return claim.Id;
    }

    public void EndFocus(int focusId)
    {
        if (focusId <= 0)
        {
            return;
        }

        for (int i = focusClaims.Count - 1; i >= 0; i--)
        {
            DogCameraFocusClaim claim = focusClaims[i];
            if (claim == null || claim.Id != focusId)
            {
                continue;
            }

            if (focusReleaseLingerDuration <= 0f || !IsFocusClaimValid(claim))
            {
                focusClaims.RemoveAt(i);
            }
            else
            {
                claim.Released = true;
                claim.ReleaseUntil = Time.time + Mathf.Max(0f, focusReleaseLingerDuration);
            }

            switchTimer = 0f;
            postFocusHoldUntil = Time.time + Mathf.Max(0f, postFocusSwitchCooldown);
            return;
        }
    }

    public void EndInteractionFocus()
    {
        if (legacyInteractionFocusId <= 0)
        {
            return;
        }

        EndFocus(legacyInteractionFocusId);
        legacyInteractionFocusId = 0;
    }

    private void UpdateClaimedFocus(DogCameraFocusClaim claim)
    {
        if (!IsFocusClaimValid(claim))
        {
            return;
        }

        if (activeFocusClaimId != claim.Id)
        {
            PrepareFocusClaimBlend(claim);
            activeFocusClaimId = claim.Id;
        }

        if (keepInitialPosition)
        {
            transform.position = fixedPosition;
        }

        Vector3 focusPoint = GetSmoothedFocusPoint(GetFocusClaimPoint(claim) + claim.Offset);
        Vector3 toTarget = focusPoint - transform.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
    }

    private void PrepareFocusClaimBlend(DogCameraFocusClaim claim)
    {
        if (!hasFocusPoint)
        {
            DogRoomAgent activeDog = GetActiveDog();
            currentFocusPoint = activeDog != null ? GetCameraFocusPoint(activeDog) : GetFocusClaimPoint(claim) + claim.Offset;
            hasFocusPoint = true;
        }

        switchStartFocusPoint = currentFocusPoint;
        switchBlendTimer = 0f;
    }

    private void ClearActiveFocusClaim()
    {
        activeFocusClaimId = 0;
        switchTimer = 0f;
        postFocusHoldUntil = Time.time + Mathf.Max(0f, postFocusSwitchCooldown);
        SyncToRuntimeActiveDog(true);
    }

    private Vector3 GetFocusClaimPoint(DogCameraFocusClaim claim)
    {
        if (claim.Target != null && claim.SecondTarget != null)
        {
            return Vector3.Lerp(claim.Target.position, claim.SecondTarget.position, 0.5f);
        }

        if (claim.Target != null)
        {
            return claim.Target.position;
        }

        return claim.SecondTarget.position;
    }

    private DogCameraFocusClaim GetBestFocusClaim()
    {
        RemoveExpiredFocusClaims();

        DogCameraFocusClaim bestActiveClaim = null;
        DogCameraFocusClaim bestReleasedClaim = null;
        for (int i = 0; i < focusClaims.Count; i++)
        {
            DogCameraFocusClaim claim = focusClaims[i];
            if (!IsFocusClaimValid(claim))
            {
                continue;
            }

            if (claim.Released)
            {
                if (IsBetterFocusClaim(claim, bestReleasedClaim))
                {
                    bestReleasedClaim = claim;
                }

                continue;
            }

            if (IsBetterFocusClaim(claim, bestActiveClaim))
            {
                bestActiveClaim = claim;
            }
        }

        return bestActiveClaim != null ? bestActiveClaim : bestReleasedClaim;
    }

    private void RemoveExpiredFocusClaims()
    {
        for (int i = focusClaims.Count - 1; i >= 0; i--)
        {
            DogCameraFocusClaim claim = focusClaims[i];
            if (claim == null || !IsFocusClaimValid(claim) || (claim.Released && Time.time > claim.ReleaseUntil))
            {
                focusClaims.RemoveAt(i);
            }
        }
    }

    private static bool IsFocusClaimValid(DogCameraFocusClaim claim)
    {
        return claim != null && (claim.Target != null || claim.SecondTarget != null);
    }

    private static bool IsBetterFocusClaim(DogCameraFocusClaim candidate, DogCameraFocusClaim currentBest)
    {
        if (candidate == null)
        {
            return false;
        }

        if (currentBest == null)
        {
            return true;
        }

        if (candidate.Priority != currentBest.Priority)
        {
            return candidate.Priority > currentBest.Priority;
        }

        return candidate.StartedAt >= currentBest.StartedAt;
    }

    private DogRoomAgent ResolveFocusDog(Transform target, Transform secondTarget)
    {
        DogRoomAgent dog = ResolveDogFromTransform(target);
        return dog != null ? dog : ResolveDogFromTransform(secondTarget);
    }

    private static DogRoomAgent ResolveDogFromTransform(Transform target)
    {
        return target != null ? target.GetComponentInParent<DogRoomAgent>() : null;
    }

    private void StopZoomForFocus()
    {
        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }

        zoomFocusActive = false;

        if (attachedCamera != null && baseFieldOfView > 0f)
        {
            attachedCamera.fieldOfView = baseFieldOfView;
        }
    }

    private float GetEffectiveSwitchInterval()
    {
        return Mathf.Max(Mathf.Max(0.1f, switchInterval), Mathf.Max(MinimumSmartAutoSwitchInterval, minimumAutoSwitchInterval));
    }

    private void HandleManualLookInput()
    {
        if (!allowManualLook)
        {
            return;
        }

        HandleMouseLookInput();
        HandleTouchLookInput();
    }

    private void HandleMouseLookInput()
    {
        if (PawPalPlayerToyThrowController.IsPointerInteractionActive)
        {
            mouseDragActive = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverBlockingUi(Input.mousePosition))
            {
                mouseDragActive = false;
                return;
            }

            mouseDragActive = true;
            BeginManualLook(Input.mousePosition);
        }

        if (!mouseDragActive)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            UpdateManualLook(Input.mousePosition);
            return;
        }

        mouseDragActive = false;
    }

    private void HandleTouchLookInput()
    {
        if (PawPalPlayerToyThrowController.IsPointerInteractionActive)
        {
            activeTouchFingerId = -1;
            return;
        }

        if (Input.touchCount <= 0)
        {
            activeTouchFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (activeTouchFingerId < 0)
            {
                if (touch.phase != TouchPhase.Began || IsPointerOverBlockingUi(touch.position))
                {
                    continue;
                }

                activeTouchFingerId = touch.fingerId;
                BeginManualLook(touch.position);
                return;
            }

            if (touch.fingerId != activeTouchFingerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                UpdateManualLook(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                activeTouchFingerId = -1;
            }

            return;
        }
    }

    private void BeginManualLook(Vector2 pointerPosition)
    {
        CaptureManualLookAnglesFromTransform();
        lastManualPointerPosition = pointerPosition;
        manualLookHoldUntil = Time.time + Mathf.Max(0f, manualLookHoldDuration);
        switchTimer = 0f;
    }

    private void UpdateManualLook(Vector2 pointerPosition)
    {
        EnsureManualLookAnglesInitialized();

        Vector2 delta = pointerPosition - lastManualPointerPosition;
        lastManualPointerPosition = pointerPosition;
        if (delta.sqrMagnitude <= 0f)
        {
            return;
        }

        manualLookYaw += delta.x * manualLookSensitivity;
        manualLookPitch = Mathf.Clamp(manualLookPitch - (delta.y * manualLookSensitivity), manualPitchMin, manualPitchMax);
        manualLookHoldUntil = Time.time + Mathf.Max(0f, manualLookHoldDuration);
        switchTimer = 0f;
    }

    private void EnsureManualLookAnglesInitialized()
    {
        if (manualLookInitialized)
        {
            return;
        }

        CaptureManualLookAnglesFromTransform();
    }

    private void CaptureManualLookAnglesFromTransform()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        manualLookYaw = euler.y;
        manualLookPitch = NormalizePitchAngle(euler.x);
        manualLookInitialized = true;
    }

    private bool IsManualLookActive()
    {
        return allowManualLook && manualLookInitialized && Time.time < manualLookHoldUntil;
    }

    private static float NormalizePitchAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static bool IsPointerOverBlockingUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(eventData, UiRaycastResults);
        for (int i = 0; i < UiRaycastResults.Count; i++)
        {
            GameObject target = UiRaycastResults[i].gameObject;
            if (IsBlockingUiTarget(target))
            {
                UiRaycastResults.Clear();
                return true;
            }
        }

        UiRaycastResults.Clear();
        return false;
    }

    private static bool IsBlockingUiTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.GetComponentInParent<Selectable>() != null)
        {
            return true;
        }

        if (target.GetComponentInParent<ScrollRect>() != null)
        {
            return true;
        }

        if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null
            || ExecuteEvents.GetEventHandler<IBeginDragHandler>(target) != null
            || ExecuteEvents.GetEventHandler<IDragHandler>(target) != null
            || ExecuteEvents.GetEventHandler<IScrollHandler>(target) != null)
        {
            return true;
        }

        return IsModalBlocker(target.transform);
    }

    private static bool IsModalBlocker(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name.IndexOf("ModalBlocker", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void SwitchToIndex(int nextIndex)
    {
        SwitchToIndex(nextIndex, false);
    }

    private void SwitchToIndex(int nextIndex, bool updateRuntimeSelection)
    {
        if (nextIndex == activeIndex)
        {
            return;
        }

        DogRoomAgent currentDog = GetActiveDog();
        if (!hasFocusPoint && currentDog != null)
        {
            currentFocusPoint = GetCameraFocusPoint(currentDog);
            hasFocusPoint = true;
        }

        switchStartFocusPoint = currentFocusPoint;
        switchBlendTimer = 0f;
        activeIndex = nextIndex;

        if (updateRuntimeSelection)
        {
            SelectRuntimeDogForCameraIndex(activeIndex);
        }
    }

    private void SelectRuntimeDogForCameraIndex(int dogIndex)
    {
        if (runtime == null)
        {
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime == null)
        {
            return;
        }

        DogRoomAgent[] resolvedDogs = GetResolvedDogs();
        if (resolvedDogs == null || dogIndex < 0 || dogIndex >= resolvedDogs.Length)
        {
            return;
        }

        DogRoomAgent dog = resolvedDogs[dogIndex];
        if (dog == null)
        {
            return;
        }

        if (dog.HasExplicitDogId && runtime.SelectDogById(dog.DogId, false))
        {
            return;
        }

        runtime.SelectDogIndex(dogIndex, false);
    }

    private void SelectRuntimeDogForAgent(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return;
        }

        if (runtime == null)
        {
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime == null)
        {
            return;
        }

        if (dog.HasExplicitDogId && runtime.SelectDogById(dog.DogId, false))
        {
            return;
        }

        DogRoomAgent[] resolvedDogs = GetResolvedDogs();
        if (resolvedDogs == null)
        {
            return;
        }

        for (int i = 0; i < resolvedDogs.Length; i++)
        {
            if (resolvedDogs[i] == dog)
            {
                runtime.SelectDogIndex(i, false);
                return;
            }
        }
    }

    private Vector3 GetSmoothedFocusPoint(Vector3 targetFocusPoint)
    {
        if (!hasFocusPoint)
        {
            currentFocusPoint = targetFocusPoint;
            switchStartFocusPoint = targetFocusPoint;
            hasFocusPoint = true;
            return currentFocusPoint;
        }

        if (switchBlendTimer < switchBlendDuration)
        {
            switchBlendTimer += Time.deltaTime;
            float t = switchBlendDuration <= 0f ? 1f : Mathf.Clamp01(switchBlendTimer / switchBlendDuration);
            currentFocusPoint = Vector3.Lerp(switchStartFocusPoint, targetFocusPoint, Mathf.SmoothStep(0f, 1f, t));
            return currentFocusPoint;
        }

        currentFocusPoint = Vector3.Lerp(currentFocusPoint, targetFocusPoint, Time.deltaTime * rotationSmooth);
        return currentFocusPoint;
    }

    private void TryStartZoom()
    {
        if (!useFovZoom || attachedCamera == null || Time.time < nextAllowedZoomTime || zoomRoutine != null)
        {
            return;
        }

        if (Random.value > zoomChanceOnSwitch)
        {
            return;
        }

        nextAllowedZoomTime = Time.time + minimumZoomCooldown;
        zoomRoutine = StartCoroutine(ZoomRoutine());
    }

    private IEnumerator ZoomRoutine()
    {
        zoomFocusActive = true;
        float lowestAllowedFov = Mathf.Clamp(minimumZoomFieldOfView, 10f, Mathf.Max(11f, baseFieldOfView - 1f));
        float requestedTargetFov = Mathf.Clamp(zoomFieldOfView, lowestAllowedFov, Mathf.Max(lowestAllowedFov, baseFieldOfView - 1f));
        float dropTargetFov = Mathf.Max(lowestAllowedFov, baseFieldOfView - Mathf.Max(0f, minimumZoomFovDrop));
        float targetFov = Mathf.Min(requestedTargetFov, dropTargetFov);

        yield return AnimateFieldOfView(attachedCamera.fieldOfView, targetFov, zoomInDuration);
        yield return new WaitForSeconds(zoomHoldDuration);
        yield return AnimateFieldOfView(attachedCamera.fieldOfView, baseFieldOfView, zoomOutDuration);

        zoomFocusActive = false;
        zoomRoutine = null;
    }

    private Vector3 GetCameraFocusPoint(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return transform.position + transform.forward;
        }

        if (zoomFocusActive)
        {
            Transform headTarget = ResolveDogHeadTarget(dog);
            if (headTarget != null)
            {
                return headTarget.position + zoomHeadFocusOffset;
            }
        }

        return dog.transform.position + focusOffset;
    }

    private Transform ResolveDogHeadTarget(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return null;
        }

        Transform cachedTarget;
        if (headTargetsByDog.TryGetValue(dog, out cachedTarget) && cachedTarget != null)
        {
            return cachedTarget;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        cachedTarget = attention != null ? attention.GetOwnHeadLookTarget() : FindDogHeadTransform(dog.transform);
        if (cachedTarget != null)
        {
            headTargetsByDog[dog] = cachedTarget;
        }

        return cachedTarget;
    }

    private static Transform FindDogHeadTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null && string.Equals(candidate.name, "head", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("head") && !lowerName.Contains("aim") && !lowerName.Contains("target") && !lowerName.Contains("helper"))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerator AnimateFieldOfView(float fromFov, float toFov, float duration)
    {
        if (duration <= 0f)
        {
            attachedCamera.fieldOfView = toFov;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            attachedCamera.fieldOfView = Mathf.Lerp(fromFov, toFov, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        attachedCamera.fieldOfView = toFov;
    }

    private DogRoomAgent GetActiveDog()
    {
        DogRoomAgent[] resolvedDogs = GetResolvedDogs();
        if (resolvedDogs == null || resolvedDogs.Length == 0)
        {
            return null;
        }

        activeIndex = Mathf.Clamp(activeIndex, 0, resolvedDogs.Length - 1);
        if (resolvedDogs[activeIndex] != null)
        {
            return resolvedDogs[activeIndex];
        }

        activeIndex = NextValidIndex(activeIndex + 1);
        return resolvedDogs[activeIndex];
    }

    private int NextValidIndex(int startIndex)
    {
        DogRoomAgent[] resolvedDogs = GetResolvedDogs();
        if (resolvedDogs == null || resolvedDogs.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < resolvedDogs.Length; i++)
        {
            int index = (startIndex + i) % resolvedDogs.Length;
            if (resolvedDogs[index] != null)
            {
                return index;
            }
        }

        return 0;
    }

    private void RefreshDogsIfNeeded()
    {
        if (dogs != null && dogs.Length > 0)
        {
            return;
        }

        dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
    }

    private DogRoomAgent[] GetResolvedDogs()
    {
        RefreshDogsIfNeeded();

        DogRoomAgent[] sceneDogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (sceneDogs == null || sceneDogs.Length == 0)
        {
            return dogs;
        }

        bool hasExplicitIds = false;
        for (int i = 0; i < sceneDogs.Length; i++)
        {
            if (sceneDogs[i] != null && sceneDogs[i].HasExplicitDogId)
            {
                hasExplicitIds = true;
                break;
            }
        }

        if (!hasExplicitIds)
        {
            return sceneDogs;
        }

        List<DogRoomAgent> resolvedDogs = new List<DogRoomAgent>(sceneDogs.Length);
        HashSet<DogRoomAgent> usedAgents = new HashSet<DogRoomAgent>();

        if (runtime != null)
        {
            for (int i = 0; i < runtime.Dogs.Count; i++)
            {
                PawPalDogState dogState = runtime.Dogs[i];
                if (dogState == null || string.IsNullOrEmpty(dogState.Id))
                {
                    continue;
                }

                for (int j = 0; j < sceneDogs.Length; j++)
                {
                    DogRoomAgent candidate = sceneDogs[j];
                    if (candidate == null || usedAgents.Contains(candidate) || !candidate.HasExplicitDogId)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.DogId, dogState.Id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedDogs.Add(candidate);
                        usedAgents.Add(candidate);
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < sceneDogs.Length; i++)
        {
            DogRoomAgent candidate = sceneDogs[i];
            if (candidate != null && !usedAgents.Contains(candidate))
            {
                resolvedDogs.Add(candidate);
            }
        }

        return resolvedDogs.ToArray();
    }

    private void HandleRuntimeStateChanged()
    {
        if (runtime == null)
        {
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime == null || runtime.ActiveDog == null)
        {
            return;
        }

        int runtimeDogIndex = runtime.ActiveDogIndex;
        if (runtimeDogIndex != lastObservedRuntimeDogIndex)
        {
            lastObservedRuntimeDogIndex = runtimeDogIndex;
            runtimeSelectionHoldUntil = Time.time + Mathf.Max(0f, runtimeSelectionHoldDuration);
            SyncToRuntimeActiveDog(true);
        }
    }

    private void SyncToRuntimeActiveDog(bool animateSwitch)
    {
        if (runtime == null)
        {
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime == null || runtime.ActiveDog == null)
        {
            return;
        }

        lastObservedRuntimeDogIndex = runtime.ActiveDogIndex;
        RefreshDogsIfNeeded();
        int runtimeIndex = ResolveRuntimeActiveDogIndex();
        if (runtimeIndex < 0)
        {
            return;
        }

        switchTimer = 0f;
        if (animateSwitch)
        {
            SwitchToIndex(runtimeIndex);
        }
        else
        {
            activeIndex = runtimeIndex;
            DogRoomAgent currentDog = GetActiveDog();
            if (currentDog != null)
            {
                currentFocusPoint = GetCameraFocusPoint(currentDog);
                switchStartFocusPoint = currentFocusPoint;
                hasFocusPoint = true;
                switchBlendTimer = switchBlendDuration;
            }
        }
    }

    private int ResolveRuntimeActiveDogIndex()
    {
        DogRoomAgent[] resolvedDogs = GetResolvedDogs();
        if (resolvedDogs == null || resolvedDogs.Length == 0 || runtime == null || runtime.ActiveDog == null)
        {
            return -1;
        }

        string activeDogId = runtime.ActiveDog.Id;
        if (!string.IsNullOrEmpty(activeDogId))
        {
            for (int i = 0; i < resolvedDogs.Length; i++)
            {
                DogRoomAgent dog = resolvedDogs[i];
                if (dog != null && dog.HasExplicitDogId && string.Equals(dog.DogId, activeDogId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return Mathf.Clamp(runtime.ActiveDogIndex, 0, resolvedDogs.Length - 1);
    }
}
