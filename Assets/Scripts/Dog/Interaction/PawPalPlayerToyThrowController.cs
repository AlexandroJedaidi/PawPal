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
    [SerializeField] private Vector3 heldViewportPosition = new Vector3(0.5f, 0.18f, 0.85f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(12f, 0f, 0f);
    [SerializeField] private float heldVisualScaleMultiplier = 1f;

    [Header("Throw")]
    [SerializeField] private float throwImpulse = 3.75f;
    [SerializeField] private float throwUpImpulse = 1.35f;
    [SerializeField] private float throwForwardFallbackDistance = 3.2f;
    [SerializeField] private float throwTargetNavMeshSampleRadius = 1.4f;
    [SerializeField] private float fetchDispatchDelay = 0.55f;
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
    private Vector2 currentPointerPosition;
    private Coroutine fetchDispatchRoutine;
    private LineRenderer aimPreviewLine;
    private Transform aimPreviewMarker;
    private Material aimPreviewMaterial;

    public static bool IsPointerInteractionActive => activeInteractionController != null;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null)
        {
            attachedCamera = Camera.main;
        }

        EnsureAnchors();
    }

    private void OnDisable()
    {
        CancelFetchDispatch();
        RestoreHeldToy(true);
        SetAimPreviewVisible(false);
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
        UpdateAimPreview();
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
            if (TryPickToy(currentPointerPosition))
            {
                pointerMode = PointerMode.Mouse;
                activeInteractionController = this;
            }
        }

        if (pointerMode != PointerMode.Mouse)
        {
            return;
        }

        currentPointerPosition = Input.mousePosition;
        if (Input.GetMouseButtonUp(0))
        {
            ThrowHeldToy(currentPointerPosition);
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount <= 0)
        {
            if (pointerMode == PointerMode.Touch)
            {
                ThrowHeldToy(currentPointerPosition);
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
                if (TryPickToy(currentPointerPosition))
                {
                    pointerMode = PointerMode.Touch;
                    activeTouchFingerId = touch.fingerId;
                    activeInteractionController = this;
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
                ThrowHeldToy(currentPointerPosition);
            }

            return;
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
        Vector3 throwTarget = ResolveThrowTarget(screenPosition, throwOrigin);

        RestoreHeldToy(true);
        SetAimPreviewVisible(false);
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

        Vector3 launchDirection = throwTarget - throwOrigin;
        launchDirection.y = 0f;
        if (launchDirection.sqrMagnitude < 0.001f)
        {
            launchDirection = attachedCamera != null ? attachedCamera.transform.forward : transform.forward;
            launchDirection.y = 0f;
        }

        launchDirection.Normalize();
        body.AddForce(launchDirection * Mathf.Max(0.05f, throwImpulse) + Vector3.up * Mathf.Max(0f, throwUpImpulse), ForceMode.Impulse);
        body.AddTorque(Vector3.Cross(Vector3.up, launchDirection).normalized * 0.45f, ForceMode.Impulse);

        ClearPointerCapture();
        QueueFetch(thrownToy);
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

    private Vector3 ResolveThrowTarget(Vector2 screenPosition, Vector3 throwOrigin)
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

        if (!hasPoint)
        {
            Vector3 forward = attachedCamera != null ? attachedCamera.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            bestPoint = throwOrigin + forward.normalized * Mathf.Max(0.5f, throwForwardFallbackDistance);
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
        Vector3 target = ResolveThrowTarget(currentPointerPosition, holdAnchor.position);
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

    private void UpdateHoldAnchor()
    {
        if (holdAnchor == null || attachedCamera == null)
        {
            return;
        }

        Vector3 viewportPoint = heldViewportPosition;
        viewportPoint.z = Mathf.Max(0.05f, viewportPoint.z);
        holdAnchor.position = attachedCamera.ViewportToWorldPoint(viewportPoint);
        holdAnchor.rotation = attachedCamera.transform.rotation * Quaternion.Euler(heldLocalEulerAngles);
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
