using UnityEngine;

[DisallowMultipleComponent]
public sealed class PetSelectionCameraController : MonoBehaviour
{
    internal const float HomeSceneCameraHeight = 0.834f;
    internal static readonly Vector3 IntroSceneCameraPosition = new Vector3(-2.186f, 0.825f, 5.909f);
    internal static readonly Vector3 IntroSceneCameraEulerAngles = new Vector3(0f, 0f, 0f);

    [SerializeField] private float positionSmooth = 4.2f;
    [SerializeField] private float rotationSmooth = 6.5f;
    [SerializeField] private float switchBlendDuration = 1.25f;
    [SerializeField] private Vector3 fallbackOffset = new Vector3(0f, 1.35f, -3.2f);
    [SerializeField] private float initialPoseHoldSeconds = 0.85f;
    [SerializeField] private bool allowManualLook = true;
    [SerializeField] private float touchLookSensitivity = 90f;
    [SerializeField] private float mouseLookSensitivity = 0.18f;
    [SerializeField] private float pinchZoomSensitivity = 0.004f;
    [SerializeField] private float minDistanceScale = 0.7f;
    [SerializeField] private float maxDistanceScale = 1.45f;
    [SerializeField] private float minPitch = -18f;
    [SerializeField] private float maxPitch = 30f;

    private Camera controlledCamera;
    private IntroPetAgent targetAgent;
    private bool snapNextFrame = true;
    private Vector3 fixedPosition;
    private bool hasFocusPoint;
    private Vector3 currentFocusPoint;
    private Vector3 switchStartFocusPoint;
    private float switchBlendTimer;
    private float holdInitialPoseUntil;
    private float orbitYaw;
    private float orbitPitch = 12f;
    private float distanceScale = 1f;
    private bool orbitInitialized;
    private bool mouseLookActive;
    private int activeTouchFingerId = -1;
    private Vector2 lastPointerPosition;
    private float lastPinchDistance;

    public Camera ControlledCamera
    {
        get { return controlledCamera; }
    }

    public void Initialize(Camera camera)
    {
        controlledCamera = camera != null ? camera : Camera.main;
        if (controlledCamera == null)
        {
            controlledCamera = gameObject.AddComponent<Camera>();
            controlledCamera.tag = "MainCamera";
        }

        controlledCamera.transform.SetPositionAndRotation(
            IntroSceneCameraPosition,
            Quaternion.Euler(IntroSceneCameraEulerAngles));
        fixedPosition = controlledCamera.transform.position;
        holdInitialPoseUntil = Time.unscaledTime + Mathf.Max(0f, initialPoseHoldSeconds);
    }

    public void Focus(IntroPetAgent agent, bool snap)
    {
        targetAgent = agent;
        snapNextFrame = snap;
        orbitInitialized = false;
        switchStartFocusPoint = hasFocusPoint ? currentFocusPoint : GetCameraFocusPoint(agent);
        switchBlendTimer = snap ? switchBlendDuration : 0f;
        ApplyFocus();
    }

    private void LateUpdate()
    {
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        if (controlledCamera == null || targetAgent == null)
        {
            return;
        }

        if (Time.unscaledTime < holdInitialPoseUntil)
        {
            controlledCamera.transform.SetPositionAndRotation(
                IntroSceneCameraPosition,
                Quaternion.Euler(IntroSceneCameraEulerAngles));
            fixedPosition = controlledCamera.transform.position;
            return;
        }

        if (allowManualLook)
        {
            HandleManualLookInput();
        }

        controlledCamera.transform.position = fixedPosition;

        Vector3 focusPoint = snapNextFrame
            ? GetCameraFocusPoint(targetAgent)
            : GetSmoothedFocusPoint(GetCameraFocusPoint(targetAgent));
        Vector3 desiredPosition = fixedPosition;
        Vector3 toTarget = focusPoint - desiredPosition;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

        if (snapNextFrame)
        {
            controlledCamera.transform.rotation = desiredRotation;
            currentFocusPoint = focusPoint;
            switchStartFocusPoint = focusPoint;
            hasFocusPoint = true;
            switchBlendTimer = switchBlendDuration;
            snapNextFrame = false;
            return;
        }

        controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, desiredRotation, Time.deltaTime * rotationSmooth);
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

    private static Vector3 GetCameraFocusPoint(IntroPetAgent agent)
    {
        if (agent == null)
        {
            return Vector3.forward;
        }

        Bounds visualBounds;
        if (TryGetVisualBounds(agent, out visualBounds))
        {
            float verticalBias = Mathf.Clamp(visualBounds.size.y * 0.08f, 0f, 0.12f);
            return visualBounds.center + Vector3.up * verticalBias;
        }

        Transform target = agent != null ? agent.FocusTransform : null;
        return target != null ? target.position : agent.transform.position;
    }

    private static bool TryGetVisualBounds(IntroPetAgent agent, out Bounds bounds)
    {
        bounds = new Bounds();
        if (agent == null)
        {
            return false;
        }

        Renderer[] renderers = agent.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
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

    private Vector3 BuildOrbitOffset(Vector3 baseOffset)
    {
        Vector3 orbitSource = baseOffset == Vector3.zero ? fallbackOffset : baseOffset;
        if (!orbitInitialized)
        {
            Vector3 planar = Vector3.ProjectOnPlane(orbitSource, Vector3.up);
            if (planar.sqrMagnitude > 0.001f)
            {
                orbitYaw = Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg + 180f;
            }

            float distance = Mathf.Max(0.1f, orbitSource.magnitude);
            orbitPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(orbitSource.y / distance, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
            orbitInitialized = true;
        }

        float distanceMagnitude = Mathf.Max(1.8f, orbitSource.magnitude * distanceScale);
        Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        return rotation * Vector3.back * distanceMagnitude;
    }

    private void HandleManualLookInput()
    {
        if (Input.touchCount >= 2)
        {
            activeTouchFingerId = -1;
            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            if (IsPointerOverUi(first.fingerId) || IsPointerOverUi(second.fingerId))
            {
                lastPinchDistance = 0f;
                return;
            }

            float distance = Vector2.Distance(first.position, second.position);
            if (lastPinchDistance > 0f)
            {
                distanceScale = Mathf.Clamp(distanceScale - (distance - lastPinchDistance) * pinchZoomSensitivity, minDistanceScale, maxDistanceScale);
            }

            lastPinchDistance = distance;
            return;
        }

        lastPinchDistance = 0f;
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (activeTouchFingerId < 0)
            {
                if (touch.phase != TouchPhase.Began || IsPointerOverUi(touch.fingerId))
                {
                    return;
                }

                activeTouchFingerId = touch.fingerId;
                lastPointerPosition = touch.position;
                return;
            }

            if (touch.fingerId != activeTouchFingerId)
            {
                return;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                Vector2 delta = touch.position - lastPointerPosition;
                lastPointerPosition = touch.position;
                orbitYaw += delta.x * touchLookSensitivity / Mathf.Max(1f, Screen.width);
                orbitPitch = Mathf.Clamp(orbitPitch - delta.y * touchLookSensitivity / Mathf.Max(1f, Screen.height), minPitch, maxPitch);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                activeTouchFingerId = -1;
            }

            return;
        }

        activeTouchFingerId = -1;
        if (!Input.GetMouseButton(0))
        {
            mouseLookActive = false;
            return;
        }

        if (!mouseLookActive)
        {
            if (IsPointerOverUi())
            {
                return;
            }

            mouseLookActive = true;
            lastPointerPosition = Input.mousePosition;
            return;
        }

        Vector2 mousePosition = Input.mousePosition;
        Vector2 deltaMouse = mousePosition - lastPointerPosition;
        lastPointerPosition = mousePosition;
        orbitYaw += deltaMouse.x * mouseLookSensitivity;
        orbitPitch = Mathf.Clamp(orbitPitch - deltaMouse.y * mouseLookSensitivity, minPitch, maxPitch);
    }

    private static bool IsPointerOverUi(int pointerId = -1)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            return false;
        }

        return pointerId >= 0
            ? UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(pointerId)
            : UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
