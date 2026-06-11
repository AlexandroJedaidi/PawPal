using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class PawPalPlayerToyThrowController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string EditorToyPickupAudioAssetPath = "Assets/Resources/Audio/toy_pickup.mp3";
#endif

    private enum PointerMode
    {
        None,
        Mouse,
        Touch
    }

    [Header("Pickup")]
    [SerializeField] private float pickRayDistance = 100f;
    [SerializeField] private string[] toyNameKeywords = { "ball", "bone", "cattoy" };

    [Header("Audio")]
    [SerializeField] private AudioClip toyPickupClip;
    [SerializeField, Range(0f, 1f)] private float toyPickupVolume = 0.7f;

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
    [SerializeField] private float thrownToyLinearDrag = 0.55f;
    [SerializeField] private float thrownToyAngularDrag = 3.75f;
    [SerializeField] private float minThrownToySpinImpulse = 0.01f;
    [SerializeField] private float maxThrownToySpinImpulse = 0.05f;
    [SerializeField] private bool showAimPreview;
    [SerializeField] private Color aimPreviewColor = new Color(1f, 0.42f, 0.28f, 0.85f);

    [Header("Fetch Return")]
    [SerializeField] private Vector3 returnViewportPosition = new Vector3(0.5f, 0.12f, 1.4f);
    [SerializeField] private float returnNavMeshSampleRadius = 1.8f;

    [Header("Dog Anticipation")]
    [SerializeField] private float dogAnticipationApproachDistance = 1.15f;
    [SerializeField] private float dogAnticipationLateralSpacing = 0.58f;
    [SerializeField] private float dogAnticipationSampleRadius = 1.6f;
    [SerializeField] private float dogAnticipationApproachTimeout = 5.5f;
    [SerializeField] private float dogAnticipationFaceDuration = 0.45f;
    [SerializeField] private float dogAnticipationBarkDuration = 0.55f;
    [SerializeField] private float dogAnticipationLookRefreshDuration = 0.7f;
    [SerializeField] private float dogAnticipationWaitTick = 0.25f;

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
    private NavMeshObstacle[] heldNavMeshObstacles;
    private bool[] heldNavMeshObstacleStates;
    private PawPalToyRuntimeMetadata heldToyMetadata;
    private bool heldToyOriginalBlocksDogNavigation;
    private float heldToyOriginalDogNavigationRadius;
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
    private readonly List<Coroutine> dogAnticipationRoutines = new List<Coroutine>();
    private readonly List<DogRoomAgent> anticipatingDogs = new List<DogRoomAgent>();

    public static bool IsPointerInteractionActive => activeInteractionController != null;
    public bool HasHeldToy => heldToy != null;
    public GameObject HeldToy => heldToy;

    private void Awake()
    {
        showAimPreview = false;
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera == null)
        {
            attachedCamera = Camera.main;
        }

        EnsureAnchors();
        AutoAssignEditorAudioClips();
        DisableAimPreview();
    }

    private void OnEnable()
    {
        showAimPreview = false;
        AutoAssignEditorAudioClips();
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
        if (heldToy == null && anticipatingDogs.Count > 0)
        {
            StopDogAnticipation(true);
        }

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

        return TryHoldToyForThrow(bestToy.gameObject, true);
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
            return metadata.transform;
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

    public bool TryHoldToyForThrow(GameObject toy, bool inviteDogs = true)
    {
        if (toy == null || heldToy != null)
        {
            return false;
        }

        HoldToy(toy, inviteDogs);
        return heldToy == toy;
    }

    private void HoldToy(GameObject toy, bool inviteDogs)
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
        PlayToyPickupAudio(toyTransform.position);

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

        heldNavMeshObstacles = heldToy.GetComponentsInChildren<NavMeshObstacle>(true);
        heldNavMeshObstacleStates = new bool[heldNavMeshObstacles.Length];
        for (int i = 0; i < heldNavMeshObstacles.Length; i++)
        {
            heldNavMeshObstacleStates[i] = heldNavMeshObstacles[i] != null && heldNavMeshObstacles[i].enabled;
            if (heldNavMeshObstacles[i] != null)
            {
                heldNavMeshObstacles[i].enabled = false;
            }
        }

        heldToyMetadata = heldToy.GetComponent<PawPalToyRuntimeMetadata>();
        if (heldToyMetadata != null)
        {
            heldToyOriginalBlocksDogNavigation = heldToyMetadata.BlocksDogNavigation;
            heldToyOriginalDogNavigationRadius = heldToyMetadata.DogNavigationBlockRadius;
            if (heldToyOriginalBlocksDogNavigation)
            {
                heldToyMetadata.SetBlocksDogNavigation(false, heldToyOriginalDogNavigationRadius);
            }
        }
        else
        {
            heldToyOriginalBlocksDogNavigation = false;
            heldToyOriginalDogNavigationRadius = 0.45f;
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

        if (inviteDogs)
        {
            StartDogAnticipation();
        }
    }

    private void ThrowHeldToy(Vector2 screenPosition)
    {
        if (heldToy == null)
        {
            ClearPointerCapture();
            return;
        }

        GameObject thrownToy = heldToy;
        PawPalToyInteractionMode interactionMode = GetToyInteractionMode(thrownToy);
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
        body.linearDamping = Mathf.Max(0f, thrownToyLinearDrag);
        body.angularDamping = Mathf.Max(0f, thrownToyAngularDrag);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        EnsureThrownToyBounceAudio(thrownToy);

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
            body.AddTorque(
                torqueAxis.normalized * Mathf.Lerp(minThrownToySpinImpulse, maxThrownToySpinImpulse, throwPower),
                ForceMode.Impulse);
        }

        ArmThrownToyRecovery(thrownToy);
        ClearPointerCapture();
        if (interactionMode == PawPalToyInteractionMode.CarryInMouth)
        {
            QueueFetch(thrownToy);
        }
        else
        {
            CancelFetchDispatch();
        }
    }

    private static PawPalToyInteractionMode GetToyInteractionMode(GameObject toy)
    {
        PawPalToyRuntimeMetadata metadata = toy != null ? toy.GetComponentInParent<PawPalToyRuntimeMetadata>() : null;
        return metadata != null ? metadata.InteractionMode : PawPalToyInteractionMode.CarryInMouth;
    }

    private static void EnsureThrownToyBounceAudio(GameObject toy)
    {
        if (toy == null || !ShouldUseBallBounceAudio(toy))
        {
            return;
        }

        PawPalBallBounceAudio.EnsureOn(toy);
    }

    private static bool ShouldUseBallBounceAudio(GameObject toy)
    {
        if (toy == null)
        {
            return false;
        }

        PawPalToyRuntimeMetadata metadata = toy.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata != null && ContainsBallName(metadata.ItemId))
        {
            return true;
        }

        return ContainsBallName(toy.name);
    }

    private static bool ContainsBallName(string value)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf("ball", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
            StopDogAnticipation(true);
            return;
        }

        StopDogAnticipation(true);
        Transform toyTransform = heldToy.transform;
        toyTransform.SetParent(originalParent, worldPositionStays: true);
        toyTransform.localScale = originalLocalScale;

        if (restoreColliderState)
        {
            RestoreHeldColliderStates();
        }

        RestoreHeldNavMeshObstacles();

        if (heldToyMetadata != null && heldToyOriginalBlocksDogNavigation)
        {
            heldToyMetadata.SetBlocksDogNavigation(true, heldToyOriginalDogNavigationRadius);
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
        heldNavMeshObstacles = null;
        heldNavMeshObstacleStates = null;
        heldToyMetadata = null;
        heldToyOriginalBlocksDogNavigation = false;
        heldToyOriginalDogNavigationRadius = 0.45f;
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

    private void RestoreHeldNavMeshObstacles()
    {
        if (heldNavMeshObstacles == null || heldNavMeshObstacleStates == null)
        {
            return;
        }

        for (int i = 0; i < heldNavMeshObstacles.Length; i++)
        {
            if (heldNavMeshObstacles[i] != null)
            {
                heldNavMeshObstacles[i].enabled = heldNavMeshObstacleStates[i];
            }
        }
    }

    private void StartDogAnticipation()
    {
        StopDogAnticipation(false);
        if (heldToy == null || attachedCamera == null)
        {
            return;
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            return;
        }

        GameObject toySnapshot = heldToy;
        int invitedIndex = 0;
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (!CanInviteDogToToyAnticipation(dog))
            {
                continue;
            }

            anticipatingDogs.Add(dog);
            dogAnticipationRoutines.Add(StartCoroutine(DogToyAnticipationRoutine(dog, invitedIndex, toySnapshot)));
            invitedIndex++;
        }
    }

    private void StopDogAnticipation(bool resumeRoaming)
    {
        for (int i = 0; i < dogAnticipationRoutines.Count; i++)
        {
            if (dogAnticipationRoutines[i] != null)
            {
                StopCoroutine(dogAnticipationRoutines[i]);
            }
        }

        dogAnticipationRoutines.Clear();

        if (resumeRoaming)
        {
            for (int i = 0; i < anticipatingDogs.Count; i++)
            {
                DogRoomAgent dog = anticipatingDogs[i];
                if (dog != null && dog.isActiveAndEnabled)
                {
                    dog.StartRoaming();
                }
            }
        }

        anticipatingDogs.Clear();
    }

    private static bool CanInviteDogToToyAnticipation(DogRoomAgent dog)
    {
        return dog != null
            && dog.isActiveAndEnabled
            && !dog.IsBusy
            && !dog.IsResting
            && !dog.IsSleeping
            && !dog.IsPlayingOneShotAnimation
            && !dog.HasHeldToy;
    }

    private IEnumerator DogToyAnticipationRoutine(DogRoomAgent dog, int dogIndex, GameObject toySnapshot)
    {
        if (dog == null || toySnapshot == null)
        {
            yield break;
        }

        dog.PauseForSocial(true);
        RequestDogCameraAttention(dog, dogAnticipationLookRefreshDuration);

        Vector3 waitPoint;
        if (TryResolveDogAnticipationPoint(dog, dogIndex, out waitPoint))
        {
            yield return dog.MoveNear(waitPoint, Mathf.Max(0.1f, dogAnticipationApproachTimeout), DogMovementPace.Run);
        }

        if (dog != null && heldToy == toySnapshot)
        {
            RequestDogCameraAttention(dog, dogAnticipationLookRefreshDuration);
            yield return dog.FaceTarget(attachedCamera != null ? attachedCamera.transform : transform, Mathf.Max(0.05f, dogAnticipationFaceDuration));
            yield return PlayDogAnticipationBark(dog);
        }

        while (dog != null && heldToy == toySnapshot)
        {
            RequestDogCameraAttention(dog, dogAnticipationLookRefreshDuration);
            yield return dog.FaceTarget(attachedCamera != null ? attachedCamera.transform : transform, Mathf.Max(0.05f, dogAnticipationFaceDuration));
            float elapsed = 0f;
            float waitDuration = Mathf.Max(0.05f, dogAnticipationWaitTick);
            while (elapsed < waitDuration && dog != null && heldToy == toySnapshot)
            {
                RequestDogCameraAttention(dog, dogAnticipationLookRefreshDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    private bool TryResolveDogAnticipationPoint(DogRoomAgent dog, int dogIndex, out Vector3 point)
    {
        point = dog != null ? dog.transform.position : Vector3.zero;
        if (dog == null || attachedCamera == null)
        {
            return false;
        }

        Transform cameraTransform = attachedCamera.transform;
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = cameraTransform.up;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        int lane = dogIndex == 0 ? 0 : ((dogIndex + 1) / 2) * (dogIndex % 2 == 0 ? -1 : 1);
        Vector3 candidate = cameraTransform.position
            + forward * Mathf.Max(0.2f, dogAnticipationApproachDistance)
            + right * lane * Mathf.Max(0f, dogAnticipationLateralSpacing);
        candidate.y = dog.transform.position.y;

        return dog.TryGetRoomSafePoint(candidate, Mathf.Max(0.05f, dogAnticipationSampleRadius), out point);
    }

    private static void RequestDogCameraAttention(DogRoomAgent dog, float duration)
    {
        if (dog == null)
        {
            return;
        }

        DogCameraAttention attention = dog.GetComponent<DogCameraAttention>();
        if (attention == null)
        {
            attention = dog.gameObject.AddComponent<DogCameraAttention>();
        }

        attention.RequestCameraAttention(duration);
    }

    private IEnumerator PlayDogAnticipationBark(DogRoomAgent dog)
    {
        if (dog == null)
        {
            yield break;
        }

        DogSocialDirector barkDirector = DogSocialDirector.Instance;
        if (barkDirector != null)
        {
            yield return barkDirector.PlayAmbientBarkForAgent(dog);
        }
        else
        {
            yield return dog.PlayBark(Mathf.Max(0.05f, dogAnticipationBarkDuration));
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
            GameObject holdObject = new GameObject("PawFriendsPlayerToyHoldAnchor");
            holdObject.hideFlags = HideFlags.HideAndDontSave;
            holdAnchor = holdObject.transform;
            holdAnchor.SetParent(attachedCamera.transform, worldPositionStays: false);
        }

        if (returnAnchor == null)
        {
            GameObject returnObject = new GameObject("PawFriendsPlayerFetchReturnAnchor");
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
            GameObject lineObject = new GameObject("PawFriendsToyThrowAimLine");
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
            markerObject.name = "PawFriendsToyThrowAimMarker";
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

        DestroyNamedPreviewObject("PawFriendsToyThrowAimLine");
        DestroyNamedPreviewObject("PawFriendsToyThrowAimMarker");
    }

    private static void DestroyNamedPreviewObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        GameObject[] loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < loadedObjects.Length; i++)
        {
            GameObject previewObject = loadedObjects[i];
            if (previewObject == null
                || previewObject.hideFlags != HideFlags.None
                || !previewObject.scene.IsValid()
                || !string.Equals(previewObject.name, objectName, System.StringComparison.Ordinal))
            {
                continue;
            }

            Destroy(previewObject);
            break;
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

    private void PlayToyPickupAudio(Vector3 position)
    {
        PlaySpatialOneShot(toyPickupClip, position, toyPickupVolume);
    }

    private void PlaySpatialOneShot(AudioClip clip, Vector3 position, float volume)
    {
        float effectiveVolume = PawPalAudioSettings.ApplySoundEffectsVolume(volume);
        if (clip == null || effectiveVolume <= 0f)
        {
            return;
        }

        GameObject audioObject = new GameObject("PawFriendsPlayerToyPickupAudio");
        audioObject.hideFlags = HideFlags.DontSave;
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.25f;
        source.maxDistance = 6f;
        source.PlayOneShot(clip, effectiveVolume);
        Destroy(audioObject, Mathf.Max(0.1f, clip.length + 0.15f));
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

    private void AutoAssignEditorAudioClips()
    {
#if UNITY_EDITOR
        if (toyPickupClip == null)
        {
            toyPickupClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorToyPickupAudioAssetPath);
        }
#endif
        PawPalAudioResources.AssignIfMissing(ref toyPickupClip, PawPalAudioResources.ToyPickup);
    }
}
