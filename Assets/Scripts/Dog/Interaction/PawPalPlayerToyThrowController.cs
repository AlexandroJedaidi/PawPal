using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PawPalPlayerToyThrowController : MonoBehaviour
{
    private enum PointerMode
    {
        None,
        Mouse,
        Touch
    }

    [Header("Pickup")]
    [SerializeField] private float pickRayDistance = 100f;
    [SerializeField] private string[] toyNameKeywords = { "ball", "bone", "cattoy" };

    [Header("Held Toy")]
    [SerializeField] private Vector3 heldViewportPosition = new Vector3(0.5f, 0.42f, 0.65f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(8f, 0f, 0f);
    [SerializeField] private float heldVisualScaleMultiplier = 1.45f;
    [SerializeField] private bool moveHeldToyWithPointer = true;
    [SerializeField] private float heldPointerMoveSensitivity = 1f;
    [SerializeField] private Vector2 heldViewportMin = new Vector2(0.22f, 0.25f);
    [SerializeField] private Vector2 heldViewportMax = new Vector2(0.78f, 0.7f);
    [SerializeField] private float heldMoveFollowSharpness = 18f;

    [Header("Throw")]
    [SerializeField] private float minThrowImpulse = 0.85f;
    [SerializeField] private float throwImpulse = 3.6f;
    [SerializeField] private float minThrowUpImpulse = 0.45f;
    [SerializeField] private float throwUpImpulse = 1.25f;
    [SerializeField] private float throwPowerScreenDistance = 0.65f;
    [SerializeField] private float throwPowerExponent = 1.15f;
    [SerializeField] private float minThrowForwardFallbackDistance = 0.65f;
    [SerializeField] private float throwForwardFallbackDistance = 3.5f;
    [SerializeField] private float minThrowArcHeight = 0.35f;
    [SerializeField] private float throwArcHeight = 1.1f;
    [SerializeField] private float throwTargetNavMeshSampleRadius = 1.4f;
    [SerializeField] private float fetchDispatchDelay = 0.75f;
    [SerializeField] private bool showAimPreview;
    [SerializeField] private Color aimPreviewColor = new Color(1f, 0.42f, 0.28f, 0.85f);

    [Header("Fetch Return")]
    [SerializeField] private Vector3 returnViewportPosition = new Vector3(0.5f, 0.12f, 1.4f);
    [SerializeField] private float returnNavMeshSampleRadius = 1.8f;

    private static PawPalPlayerToyThrowController activeInteractionController;

    private Camera attachedCamera;
    private Transform holdAnchor;
    private Transform returnAnchor;
    private GameObject heldToy;
    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Rigidbody heldBody;
    private bool originalBodyIsKinematic;
    private bool originalBodyUseGravity;
    private CollisionDetectionMode originalCollisionDetectionMode;
    private Collider[] heldColliders;
    private bool[] heldColliderStates;
    private PawPalPlayerHeldToyMarker heldMarker;
    private PointerMode pointerMode;
    private int activeTouchFingerId = -1;
    private bool pointerStartedAsPickupOnly;
    private Vector2 currentPointerPosition;
    private Vector2 pickupPointerPosition;
    private Vector3 currentHeldViewportPosition;
    private Coroutine fetchDispatchRoutine;
    private LineRenderer aimPreviewLine;
    private Transform aimPreviewMarker;
    private Material aimPreviewMaterial;

    public static bool IsPointerInteractionActive => activeInteractionController != null;

    private void Awake()
    {
        showAimPreview = false;
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null)
        {
            attachedCamera = Camera.main;
        }

        EnsureAnchors();
        DisableAimPreview();
    }

    private void OnEnable()
    {
        showAimPreview = false;
        DisableAimPreview();
    }

    private void OnDisable()
    {
        CancelFetchDispatch();
        RestoreHeldToy(true);
        DisableAimPreview();
        ClearPointerCapture();
    }

    private void Update()
    {
        if (attachedCamera == null)
        {
            attachedCamera = Camera.main;
            if (attachedCamera == null)
            {
                return;
            }
        }

        UpdateHoldAnchor();
        if (showAimPreview)
        {
            UpdateAimPreview();
        }
        else
        {
            DisableAimPreview();
        }

        HandleMouseInput();
        HandleTouchInput();
    }

    private void HandleMouseInput()
    {
        if (Input.touchCount > 0)
        {
            return;
        }

        if (pointerMode == PointerMode.None && Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUi())
            {
                return;
            }

            currentPointerPosition = Input.mousePosition;
            if (heldToy != null)
            {
                BeginPointerCapture(PointerMode.Mouse, -1, pickupOnly: false);
            }
            else if (TryPickToy(currentPointerPosition))
            {
                BeginPointerCapture(PointerMode.Mouse, -1, pickupOnly: true);
            }
        }

        if (pointerMode != PointerMode.Mouse)
        {
            return;
        }

        currentPointerPosition = Input.mousePosition;
        if (Input.GetMouseButtonUp(0))
        {
            if (pointerStartedAsPickupOnly)
            {
                ClearPointerCapture();
            }
            else
            {
                ThrowHeldToy(currentPointerPosition);
            }
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount <= 0)
        {
            if (pointerMode == PointerMode.Touch)
            {
                if (pointerStartedAsPickupOnly)
                {
                    ClearPointerCapture();
                }
                else
                {
                    ThrowHeldToy(currentPointerPosition);
                }
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (pointerMode == PointerMode.None)
            {
                if (touch.phase != TouchPhase.Began || IsPointerOverUi(touch.fingerId))
                {
                    continue;
                }

                currentPointerPosition = touch.position;
                if (heldToy != null)
                {
                    BeginPointerCapture(PointerMode.Touch, touch.fingerId, pickupOnly: false);
                }
                else if (TryPickToy(currentPointerPosition))
                {
                    BeginPointerCapture(PointerMode.Touch, touch.fingerId, pickupOnly: true);
                }

                return;
            }

            if (pointerMode != PointerMode.Touch || touch.fingerId != activeTouchFingerId)
            {
                continue;
            }

            currentPointerPosition = touch.position;
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (pointerStartedAsPickupOnly)
                {
                    ClearPointerCapture();
                }
                else
                {
                    ThrowHeldToy(currentPointerPosition);
                }
            }

            return;
        }
    }

    private void BeginPointerCapture(PointerMode mode, int touchFingerId, bool pickupOnly)
    {
        pointerMode = mode;
        activeTouchFingerId = touchFingerId;
        pointerStartedAsPickupOnly = pickupOnly;
        activeInteractionController = this;

        if (!pickupOnly)
        {
            pickupPointerPosition = currentPointerPosition;
            currentHeldViewportPosition = GetCurrentHeldViewportPosition();
        }
    }

    private bool TryPickToy(Vector2 screenPosition)
    {
        Ray ray = attachedCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, pickRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Transform bestToy = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.distance >= bestDistance)
            {
                continue;
            }

            Transform toy = ResolvePickableToyRoot(hit.collider.transform);
            if (toy == null)
            {
                continue;
            }

            bestToy = toy;
            bestDistance = hit.distance;
        }

        if (bestToy == null)
        {
            return false;
        }

        HoldToy(bestToy.gameObject);
        return true;
    }

    private Transform ResolvePickableToyRoot(Transform candidate)
    {
        if (candidate == null || candidate.GetComponentInParent<DogRoomAgent>() != null)
        {
            return null;
        }

        PawPalPlayerHeldToyMarker marker = candidate.GetComponentInParent<PawPalPlayerHeldToyMarker>();
        if (marker != null)
        {
            return null;
        }

        PawPalToyRuntimeMetadata metadata = candidate.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata != null)
        {
            return metadata.InteractionMode == PawPalToyInteractionMode.CarryInMouth ? metadata.transform : null;
        }

        Transform current = candidate;
        Transform bestToyRoot = IsSmallToyCandidate(current) ? current : null;
        while (current.parent != null && current.parent.GetComponentInParent<DogRoomAgent>() == null)
        {
            current = current.parent;
            if (IsSmallToyCandidate(current))
            {
                bestToyRoot = current;
            }
        }

        return bestToyRoot;
    }

    private bool IsSmallToyCandidate(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("ballhole")
            || lowerName.Contains("toyterrier")
            || PawPalToyRuntimeMetadata.IsPawHitRollToyName(candidate.name))
        {
            return false;
        }

        if (candidate.gameObject.tag == "Toy")
        {
            return true;
        }

        for (int i = 0; i < toyNameKeywords.Length; i++)
        {
            string keyword = toyNameKeywords[i];
            if (!string.IsNullOrEmpty(keyword) && lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private void HoldToy(GameObject toy)
    {
        if (toy == null)
        {
            return;
        }

        CancelFetchDispatch();
        EnsureAnchors();
        pickupPointerPosition = currentPointerPosition;
        currentHeldViewportPosition = heldViewportPosition;
        UpdateHoldAnchor();

        heldToy = toy;
        Transform toyTransform = heldToy.transform;
        originalParent = toyTransform.parent;
        originalLocalScale = toyTransform.localScale;

        heldBody = heldToy.GetComponent<Rigidbody>();
        if (heldBody != null)
        {
            originalBodyIsKinematic = heldBody.isKinematic;
            originalBodyUseGravity = heldBody.useGravity;
            originalCollisionDetectionMode = heldBody.collisionDetectionMode;
            heldBody.linearVelocity = Vector3.zero;
            heldBody.angularVelocity = Vector3.zero;
            heldBody.isKinematic = true;
            heldBody.useGravity = false;
        }

        heldColliders = heldToy.GetComponentsInChildren<Collider>(true);
        heldColliderStates = new bool[heldColliders.Length];
        for (int i = 0; i < heldColliders.Length; i++)
        {
            heldColliderStates[i] = heldColliders[i] != null && heldColliders[i].enabled;
            if (heldColliders[i] != null)
            {
                heldColliders[i].enabled = false;
            }
        }

        heldMarker = heldToy.GetComponent<PawPalPlayerHeldToyMarker>();
        if (heldMarker == null)
        {
            heldMarker = heldToy.AddComponent<PawPalPlayerHeldToyMarker>();
        }

        toyTransform.SetParent(holdAnchor, worldPositionStays: false);
        toyTransform.localPosition = Vector3.zero;
        toyTransform.localRotation = Quaternion.identity;
        toyTransform.localScale = originalLocalScale * Mathf.Max(0.01f, heldVisualScaleMultiplier);
        AlignHeldToyVisualToAnchor();
    }

    private void ThrowHeldToy(Vector2 screenPosition)
    {
        if (heldToy == null)
        {
            ClearPointerCapture();
            return;
        }

        GameObject thrownToy = heldToy;
        Vector3 throwOrigin = holdAnchor != null ? holdAnchor.position : thrownToy.transform.position;
        Quaternion throwRotation = holdAnchor != null ? holdAnchor.rotation : thrownToy.transform.rotation;
        float throwPower = CalculateThrowPower(screenPosition);
        Vector3 throwTarget = ResolveThrowTarget(screenPosition, throwOrigin, throwPower);

        RestoreHeldToy(true);
        DisableAimPreview();
        thrownToy.transform.position = throwOrigin;
        thrownToy.transform.rotation = throwRotation;

        Rigidbody body = thrownToy.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = thrownToy.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        Vector3 launchVelocity = CalculateArcLaunchVelocity(throwOrigin, throwTarget, throwPower);
        Vector3 launchDirection = launchVelocity;
        launchDirection.y = 0f;
        if (launchDirection.sqrMagnitude < 0.001f)
        {
            launchDirection = attachedCamera != null ? attachedCamera.transform.forward : transform.forward;
            launchDirection.y = 0f;
        }

        launchDirection.Normalize();
        body.linearVelocity = launchVelocity;
        Vector3 torqueAxis = Vector3.Cross(Vector3.up, launchDirection);
        if (torqueAxis.sqrMagnitude > 0.001f)
        {
            body.AddTorque(torqueAxis.normalized * Mathf.Lerp(0.04f, 0.18f, throwPower), ForceMode.Impulse);
        }

        ArmThrownToyRecovery(thrownToy);
        ClearPointerCapture();
        QueueFetch(thrownToy);
    }

    private void ArmThrownToyRecovery(GameObject thrownToy)
    {
        if (thrownToy == null)
        {
            return;
        }

        System.Type recoveryType = FindThrownToyRecoveryType();
        if (recoveryType == null || !typeof(Component).IsAssignableFrom(recoveryType))
        {
            return;
        }

        Component recovery = thrownToy.GetComponent(recoveryType);
        if (recovery == null)
        {
            recovery = thrownToy.AddComponent(recoveryType);
        }

        if (recovery != null)
        {
            recovery.SendMessage("ResetMonitor", SendMessageOptions.DontRequireReceiver);
        }
    }

    private static System.Type FindThrownToyRecoveryType()
    {
        System.Type recoveryType = System.Type.GetType("PawPalThrownToyRecovery, Assembly-CSharp");
        if (recoveryType != null)
        {
            return recoveryType;
        }

        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            recoveryType = assemblies[i].GetType("PawPalThrownToyRecovery");
            if (recoveryType != null)
            {
                return recoveryType;
            }
        }

        return null;
    }

    private void RestoreHeldToy(bool restoreColliderState)
    {
        if (heldToy == null)
        {
            return;
        }

        Transform toyTransform = heldToy.transform;
        toyTransform.SetParent(originalParent, worldPositionStays: true);
        toyTransform.localScale = originalLocalScale;

        if (restoreColliderState)
        {
            RestoreHeldColliderStates();
        }

        if (heldBody != null)
        {
            heldBody.isKinematic = originalBodyIsKinematic;
            heldBody.useGravity = originalBodyUseGravity;
            heldBody.collisionDetectionMode = originalCollisionDetectionMode;
        }

        if (heldMarker != null)
        {
            Destroy(heldMarker);
        }

        heldToy = null;
        originalParent = null;
        heldBody = null;
        heldColliders = null;
        heldColliderStates = null;
        heldMarker = null;
    }

    private void RestoreHeldColliderStates()
    {
        if (heldColliders == null || heldColliderStates == null)
        {
            return;
        }

        for (int i = 0; i < heldColliders.Length; i++)
        {
            if (heldColliders[i] != null)
            {
                heldColliders[i].enabled = heldColliderStates[i];
            }
        }
    }

    private float CalculateThrowPower(Vector2 screenPosition)
    {
        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        float referenceDistance = Mathf.Min(screenSize.x, screenSize.y) * Mathf.Max(0.05f, throwPowerScreenDistance);
        float dragDistance = (screenPosition - pickupPointerPosition).magnitude;
        float normalizedPower = Mathf.Clamp01(dragDistance / referenceDistance);
        return Mathf.Pow(normalizedPower, Mathf.Max(0.2f, throwPowerExponent));
    }

    private Vector3 CalculateArcLaunchVelocity(Vector3 throwOrigin, Vector3 throwTarget, float throwPower)
    {
        Vector3 horizontalDelta = throwTarget - throwOrigin;
        horizontalDelta.y = 0f;
        if (horizontalDelta.sqrMagnitude < 0.001f)
        {
            Vector3 forward = attachedCamera != null ? attachedCamera.transform.forward : transform.forward;
            forward.y = 0f;
            horizontalDelta = forward.sqrMagnitude > 0.001f ? forward.normalized * 0.25f : Vector3.forward * 0.25f;
        }

        float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
        float heightDelta = throwTarget.y - throwOrigin.y;
        float arcHeight = Mathf.Lerp(Mathf.Max(0.05f, minThrowArcHeight), Mathf.Max(minThrowArcHeight, throwArcHeight), Mathf.Clamp01(throwPower));
        float apexAboveOrigin = Mathf.Max(arcHeight, heightDelta + 0.08f);
        float upwardVelocity = Mathf.Sqrt(2f * gravity * apexAboveOrigin);
        upwardVelocity = Mathf.Max(
            upwardVelocity,
            Mathf.Lerp(Mathf.Max(0f, minThrowUpImpulse), Mathf.Max(minThrowUpImpulse, throwUpImpulse), Mathf.Clamp01(throwPower)));

        float timeUp = upwardVelocity / gravity;
        float fallDistance = Mathf.Max(0.05f, apexAboveOrigin - heightDelta);
        float timeDown = Mathf.Sqrt(2f * fallDistance / gravity);
        float flightTime = Mathf.Max(0.1f, timeUp + timeDown);

        Vector3 horizontalVelocity = horizontalDelta / flightTime;
        float horizontalSpeed = horizontalVelocity.magnitude;
        float maxHorizontalSpeed = Mathf.Lerp(
            Mathf.Max(0.05f, minThrowImpulse),
            Mathf.Max(minThrowImpulse, throwImpulse),
            Mathf.Clamp01(throwPower));

        if (horizontalSpeed > maxHorizontalSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxHorizontalSpeed;
        }

        return horizontalVelocity + Vector3.up * upwardVelocity;
    }

    private Vector3 ResolveThrowTarget(Vector2 screenPosition, Vector3 throwOrigin, float throwPower)
    {
        Ray ray = attachedCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, pickRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = Vector3.zero;
        bool hasPoint = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.distance >= bestDistance || hit.normal.y < 0.35f)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestPoint = hit.point;
            hasPoint = true;
        }

        float dynamicDistance = Mathf.Lerp(
            Mathf.Max(0.1f, minThrowForwardFallbackDistance),
            Mathf.Max(minThrowForwardFallbackDistance, throwForwardFallbackDistance),
            Mathf.Clamp01(throwPower));

        if (!hasPoint)
        {
            Vector3 forward = attachedCamera != null ? attachedCamera.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            bestPoint = throwOrigin + forward.normalized * dynamicDistance;
        }
        else
        {
            Vector3 horizontalOffset = bestPoint - throwOrigin;
            horizontalOffset.y = 0f;
            if (horizontalOffset.magnitude > dynamicDistance)
            {
                bestPoint = throwOrigin + horizontalOffset.normalized * dynamicDistance;
            }
        }

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(bestPoint, out navHit, Mathf.Max(0.01f, throwTargetNavMeshSampleRadius), NavMesh.AllAreas))
        {
            return navHit.position;
        }

        return bestPoint;
    }

    private void QueueFetch(GameObject thrownToy)
    {
        CancelFetchDispatch();
        if (thrownToy == null)
        {
            return;
        }

        fetchDispatchRoutine = StartCoroutine(DispatchFetchAfterDelay(thrownToy));
    }

    private IEnumerator DispatchFetchAfterDelay(GameObject thrownToy)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, fetchDispatchDelay));
        fetchDispatchRoutine = null;

        if (thrownToy == null)
        {
            yield break;
        }

        DogRoomAgent dog = FindNearestFetchDog(thrownToy.transform.position);
        if (dog == null)
        {
            yield break;
        }

        UpdateReturnAnchor(dog);
        dog.TryStartFetchToy(thrownToy, returnAnchor, HandleFetchCompleted);
    }

    private DogRoomAgent FindNearestFetchDog(Vector3 toyPosition)
    {
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        DogRoomAgent bestDog = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null || !dog.CanStartFetchToy)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(dog.transform.position - toyPosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestDog = dog;
        }

        return bestDog;
    }

    private void HandleFetchCompleted()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.PlayWithActiveDog();
        }
    }

    private void EnsureAnchors()
    {
        if (attachedCamera == null)
        {
            return;
        }

        if (holdAnchor == null)
        {
            GameObject holdObject = new GameObject("PawPalPlayerToyHoldAnchor");
            holdObject.hideFlags = HideFlags.HideAndDontSave;
            holdAnchor = holdObject.transform;
            holdAnchor.SetParent(attachedCamera.transform, worldPositionStays: false);
        }

        if (returnAnchor == null)
        {
            GameObject returnObject = new GameObject("PawPalPlayerFetchReturnAnchor");
            returnObject.hideFlags = HideFlags.HideAndDontSave;
            returnAnchor = returnObject.transform;
        }
    }

    private void EnsureAimPreview()
    {
        if (aimPreviewLine != null && aimPreviewMarker != null)
        {
            return;
        }

        if (aimPreviewMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                aimPreviewMaterial = new Material(shader);
                aimPreviewMaterial.hideFlags = HideFlags.HideAndDontSave;
                aimPreviewMaterial.color = aimPreviewColor;
            }
        }

        if (aimPreviewLine == null)
        {
            GameObject lineObject = new GameObject("PawPalToyThrowAimLine");
            lineObject.hideFlags = HideFlags.HideAndDontSave;
            aimPreviewLine = lineObject.AddComponent<LineRenderer>();
            aimPreviewLine.positionCount = 2;
            aimPreviewLine.useWorldSpace = true;
            aimPreviewLine.startWidth = 0.018f;
            aimPreviewLine.endWidth = 0.008f;
            aimPreviewLine.enabled = false;
            if (aimPreviewMaterial != null)
            {
                aimPreviewLine.material = aimPreviewMaterial;
                aimPreviewLine.startColor = aimPreviewColor;
                aimPreviewLine.endColor = aimPreviewColor;
            }
        }

        if (aimPreviewMarker == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "PawPalToyThrowAimMarker";
            markerObject.hideFlags = HideFlags.HideAndDontSave;
            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }

            Renderer markerRenderer = markerObject.GetComponent<Renderer>();
            if (markerRenderer != null && aimPreviewMaterial != null)
            {
                markerRenderer.material = aimPreviewMaterial;
            }

            aimPreviewMarker = markerObject.transform;
            aimPreviewMarker.localScale = Vector3.one * 0.08f;
            markerObject.SetActive(false);
        }
    }

    private void UpdateAimPreview()
    {
        if (heldToy == null || holdAnchor == null)
        {
            SetAimPreviewVisible(false);
            return;
        }

        EnsureAimPreview();
        Vector3 target = ResolveThrowTarget(currentPointerPosition, holdAnchor.position, CalculateThrowPower(currentPointerPosition));
        Vector3 markerPosition = target + Vector3.up * 0.04f;

        if (aimPreviewLine != null)
        {
            aimPreviewLine.enabled = true;
            aimPreviewLine.SetPosition(0, holdAnchor.position);
            aimPreviewLine.SetPosition(1, markerPosition);
        }

        if (aimPreviewMarker != null)
        {
            aimPreviewMarker.gameObject.SetActive(true);
            aimPreviewMarker.position = markerPosition;
        }
    }

    private void SetAimPreviewVisible(bool visible)
    {
        if (aimPreviewLine != null)
        {
            aimPreviewLine.enabled = visible;
        }

        if (aimPreviewMarker != null)
        {
            aimPreviewMarker.gameObject.SetActive(visible);
        }
    }

    private void DisableAimPreview()
    {
        showAimPreview = false;
        SetAimPreviewVisible(false);

        if (aimPreviewLine != null)
        {
            Destroy(aimPreviewLine.gameObject);
            aimPreviewLine = null;
        }

        if (aimPreviewMarker != null)
        {
            Destroy(aimPreviewMarker.gameObject);
            aimPreviewMarker = null;
        }

        DestroyNamedPreviewObject("PawPalToyThrowAimLine");
        DestroyNamedPreviewObject("PawPalToyThrowAimMarker");
    }

    private static void DestroyNamedPreviewObject(string objectName)
    {
        GameObject previewObject = GameObject.Find(objectName);
        if (previewObject != null)
        {
            Destroy(previewObject);
        }
    }

    private void UpdateHoldAnchor()
    {
        if (holdAnchor == null || attachedCamera == null)
        {
            return;
        }

        Vector3 targetViewportPoint = GetTargetHeldViewportPosition();
        if (heldToy != null && heldMoveFollowSharpness > 0f)
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
        holdAnchor.position = attachedCamera.ViewportToWorldPoint(viewportPoint);
        holdAnchor.rotation = attachedCamera.transform.rotation * Quaternion.Euler(heldLocalEulerAngles);
    }

    private Vector3 GetTargetHeldViewportPosition()
    {
        if (!moveHeldToyWithPointer
            || heldToy == null
            || attachedCamera == null
            || pointerMode == PointerMode.None
            || pointerStartedAsPickupOnly)
        {
            return heldViewportPosition;
        }

        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Vector2 pointerDelta = currentPointerPosition - pickupPointerPosition;
        Vector2 viewportDelta = new Vector2(pointerDelta.x / screenSize.x, pointerDelta.y / screenSize.y)
            * Mathf.Max(0f, heldPointerMoveSensitivity);

        Vector2 viewport = new Vector2(heldViewportPosition.x, heldViewportPosition.y) + viewportDelta;
        viewport.x = Mathf.Clamp(viewport.x, Mathf.Min(heldViewportMin.x, heldViewportMax.x), Mathf.Max(heldViewportMin.x, heldViewportMax.x));
        viewport.y = Mathf.Clamp(viewport.y, Mathf.Min(heldViewportMin.y, heldViewportMax.y), Mathf.Max(heldViewportMin.y, heldViewportMax.y));

        return new Vector3(viewport.x, viewport.y, heldViewportPosition.z);
    }

    private Vector3 GetCurrentHeldViewportPosition()
    {
        return currentHeldViewportPosition == Vector3.zero ? heldViewportPosition : currentHeldViewportPosition;
    }

    private void UpdateReturnAnchor(DogRoomAgent dog)
    {
        EnsureAnchors();
        if (returnAnchor == null)
        {
            return;
        }

        Vector3 returnPoint = ResolveReturnPoint();
        if (dog != null)
        {
            Vector3 safePoint;
            if (dog.TryGetRoomSafePoint(returnPoint, returnNavMeshSampleRadius, out safePoint))
            {
                returnPoint = safePoint;
            }
        }

        returnAnchor.position = returnPoint;
        returnAnchor.rotation = attachedCamera != null ? attachedCamera.transform.rotation : Quaternion.identity;
    }

    private Vector3 ResolveReturnPoint()
    {
        if (attachedCamera == null)
        {
            return transform.position;
        }

        Ray ray = attachedCamera.ViewportPointToRay(returnViewportPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, pickRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = attachedCamera.transform.position + attachedCamera.transform.forward * Mathf.Max(0.5f, returnViewportPosition.z);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.distance >= bestDistance || hit.normal.y < 0.35f)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestPoint = hit.point;
        }

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(bestPoint, out navHit, Mathf.Max(0.01f, returnNavMeshSampleRadius), NavMesh.AllAreas))
        {
            return navHit.position;
        }

        return bestPoint;
    }

    private void AlignHeldToyVisualToAnchor()
    {
        if (heldToy == null || holdAnchor == null)
        {
            return;
        }

        Bounds bounds;
        if (!TryGetVisualBounds(heldToy, out bounds))
        {
            return;
        }

        heldToy.transform.position += holdAnchor.position - bounds.center;
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
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void CancelFetchDispatch()
    {
        if (fetchDispatchRoutine == null)
        {
            return;
        }

        StopCoroutine(fetchDispatchRoutine);
        fetchDispatchRoutine = null;
    }

    private void ClearPointerCapture()
    {
        if (activeInteractionController == this)
        {
            activeInteractionController = null;
        }

        pointerMode = PointerMode.None;
        activeTouchFingerId = -1;
        pointerStartedAsPickupOnly = false;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }
}
