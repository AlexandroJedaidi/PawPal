using UnityEngine;

[DisallowMultipleComponent]
public sealed class PetSelectionCameraController : MonoBehaviour
{
    internal const float HomeSceneCameraHeight = 0.834f;
    internal static readonly Vector3 IntroSceneCameraPosition = new Vector3(-2.434f, 0.847f, 2.799f);
    internal static readonly Vector3 IntroSceneCameraEulerAngles = new Vector3(5.475f, -12.758f, 0f);

    [SerializeField] private float positionSmooth = 4.2f;
    [SerializeField] private float rotationSmooth = 6.5f;
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
        holdInitialPoseUntil = Time.unscaledTime + Mathf.Max(0f, initialPoseHoldSeconds);
    }

    public void Focus(IntroPetAgent agent, bool snap)
    {
        targetAgent = agent;
        snapNextFrame = snap;
        orbitInitialized = false;
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
            return;
        }

        if (allowManualLook)
        {
            HandleManualLookInput();
        }

        IntroPetDefinition definition = targetAgent.Definition;
        Vector3 offset = definition != null ? definition.IntroCameraOffset : fallbackOffset;
        Transform target = targetAgent.FocusTransform;
        Vector3 focusPoint = target.position + Vector3.up * 0.55f;
        Vector3 desiredOffset = BuildOrbitOffset(offset);
        Vector3 desiredPosition = target.position + desiredOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up);

        if (snapNextFrame)
        {
            controlledCamera.transform.position = desiredPosition;
            controlledCamera.transform.rotation = desiredRotation;
            snapNextFrame = false;
            return;
        }

        controlledCamera.transform.position = Vector3.Lerp(controlledCamera.transform.position, desiredPosition, Time.deltaTime * positionSmooth);
        controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, desiredRotation, Time.deltaTime * rotationSmooth);
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
