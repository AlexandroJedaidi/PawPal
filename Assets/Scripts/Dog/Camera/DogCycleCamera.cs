using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DogCycleCamera : MonoBehaviour
{
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
    private bool interactionFocusActive;
    private Transform interactionFocusTarget;
    private Transform interactionFocusSecondTarget;
    private Vector3 interactionFocusOffset;
    private bool zoomFocusActive;
    private readonly Dictionary<DogRoomAgent, Transform> headTargetsByDog = new Dictionary<DogRoomAgent, Transform>();

    public Transform ActiveTarget
    {
        get
        {
            DogRoomAgent dog = GetActiveDog();
            return dog != null ? dog.transform : null;
        }
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

        if (interactionFocusActive)
        {
            UpdateInteractionFocus();
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

        bool usingRuntimeSelection = runtime != null && runtime.ActiveDog != null && Time.time < runtimeSelectionHoldUntil;
        if (!usingRuntimeSelection)
        {
            switchTimer += Time.deltaTime;
            if (switchTimer >= switchInterval)
            {
                switchTimer = 0f;
                SwitchToIndex(NextValidIndex(activeIndex + 1), true);
                TryStartZoom();
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
        if (target == null)
        {
            return;
        }

        BeginInteractionFocusInternal(target, null, offset);
    }

    public void BeginPairInteractionFocus(Transform firstTarget, Transform secondTarget, Vector3 offset)
    {
        if (firstTarget == null || secondTarget == null)
        {
            return;
        }

        BeginInteractionFocusInternal(firstTarget, secondTarget, offset);
    }

    private void BeginInteractionFocusInternal(Transform target, Transform secondTarget, Vector3 offset)
    {
        interactionFocusActive = true;
        interactionFocusTarget = target;
        interactionFocusSecondTarget = secondTarget;
        interactionFocusOffset = offset;
        manualLookHoldUntil = 0f;
        mouseDragActive = false;
        activeTouchFingerId = -1;
        switchTimer = 0f;

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

    public void EndInteractionFocus()
    {
        if (!interactionFocusActive)
        {
            return;
        }

        interactionFocusActive = false;
        interactionFocusTarget = null;
        interactionFocusSecondTarget = null;
        switchTimer = 0f;
        SyncToRuntimeActiveDog(true);
    }

    private void UpdateInteractionFocus()
    {
        if (interactionFocusTarget == null && interactionFocusSecondTarget == null)
        {
            EndInteractionFocus();
            return;
        }

        if (keepInitialPosition)
        {
            transform.position = fixedPosition;
        }

        Vector3 focusPoint = GetInteractionFocusPoint() + interactionFocusOffset;
        Vector3 toTarget = focusPoint - transform.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmooth);
    }

    private Vector3 GetInteractionFocusPoint()
    {
        if (interactionFocusTarget != null && interactionFocusSecondTarget != null)
        {
            return Vector3.Lerp(interactionFocusTarget.position, interactionFocusSecondTarget.position, 0.5f);
        }

        if (interactionFocusTarget != null)
        {
            return interactionFocusTarget.position;
        }

        return interactionFocusSecondTarget.position;
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
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi())
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
                if (touch.phase != TouchPhase.Began || IsPointerOverUi(touch.fingerId))
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
        EnsureManualLookAnglesInitialized();
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

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
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
