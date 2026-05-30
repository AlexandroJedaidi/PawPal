using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PawPalWalkSceneController : MonoBehaviour
{
    private static readonly Color32 Cream = new Color32(252, 248, 232, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);

    private const float MinimumWalkDuration = 18f;
    private const float MaximumWalkDuration = 55f;
    private const float MinimumWorldPathDistance = 12f;
    private const float MaximumWorldPathDistance = 34f;
    private const float GroundProbeHeight = 8f;
    private const float GroundProbeDistance = 24f;

    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");

    private readonly List<Vector3> worldPath = new List<Vector3>();

    private PawPalWalkSessionSaveData session;
    private PawPalGameRuntime runtime;
    private Transform walker;
    private Transform encounterDog;
    private Animator walkerAnimator;
    private NavMeshAgent walkerAgent;
    private Camera sceneCamera;
    private Canvas hudCanvas;
    private RectTransform hudRoot;
    private Image progressFill;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI detailLabel;
    private RectTransform eventCard;
    private TextMeshProUGUI eventTitleLabel;
    private TextMeshProUGUI eventBodyLabel;
    private RectTransform summaryPanel;
    private TextMeshProUGUI summaryBodyLabel;
    private GameObject activePresent;
    private float[] cumulativePathDistances = new float[0];
    private Vector3 sidewalkDirection = Vector3.back;
    private Vector3 cameraVelocity;
    private int currentSegmentIndex;
    private float totalPathDistance = 1f;
    private float progress;
    private float walkDuration;
    private float walkSpeed = 1f;
    private bool usingAgent;
    private bool paused;
    private bool completed;
    private string returnSceneName = PawPalWalkSceneFlow.HomeSceneName;

    public void Initialize(PawPalWalkSessionSaveData walkSession)
    {
        session = walkSession;
    }

    private void Start()
    {
        runtime = PawPalGameRuntime.Instance;
        if (session == null && runtime != null)
        {
            session = runtime.ActiveWalkSession;
        }

        if (session == null)
        {
            PawPalWalkSceneFlow.ReturnHome(PawPalWalkSceneFlow.HomeSceneName);
            return;
        }

        returnSceneName = string.IsNullOrEmpty(session.ReturnSceneName)
            ? PawPalWalkSceneFlow.HomeSceneName
            : session.ReturnSceneName;

        progress = Mathf.Clamp01(session.LastProgress);
        walkDuration = Mathf.Clamp(session.RouteDistance * 0.55f, MinimumWalkDuration, MaximumWalkDuration);

        PawPalWalkSceneFlow.SetAppShellVisible(false);
        EnsureEventSystem();
        ResolveSceneObjects();
        ConfigureSidewalkPathAndCamera();
        BuildHud();
        BeginPathMotion();
        RefreshHud();
    }

    private void Update()
    {
        if (completed || paused || session == null)
        {
            return;
        }

        UpdateWalkerMovement();
        progress = CalculateProgressFromWalker();
        if (runtime != null)
        {
            runtime.UpdateActiveWalkSessionProgress(progress);
        }

        RefreshHud();

        PawPalWalkGeneratedEventState nextEvent = GetNextEvent();
        if (nextEvent != null && progress >= nextEvent.Progress)
        {
            ShowEvent(nextEvent);
            return;
        }

        if (progress >= 0.999f)
        {
            CompleteWalk();
        }
    }

    private void LateUpdate()
    {
        PositionCamera(false);
    }

    private void OnApplicationPause(bool pausedStatus)
    {
        if (pausedStatus && runtime != null)
        {
            runtime.UpdateActiveWalkSessionProgress(progress);
            runtime.SaveProfile();
        }
    }

    private void ResolveSceneObjects()
    {
        ResolveDogRoomAgentSelection();

        if (walker == null)
        {
            ResolveAnimatorDogSelection();
        }

        if (walker == null)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "FallbackWalkDog";
            fallback.transform.position = new Vector3(1.25f, 0.1f, -8.7f);
            fallback.transform.localScale = new Vector3(0.45f, 0.45f, 0.9f);
            walker = fallback.transform;
        }

        if (walkerAnimator == null && walker != null)
        {
            walkerAnimator = walker.GetComponent<Animator>();
            if (walkerAnimator == null)
            {
                walkerAnimator = walker.GetComponentInChildren<Animator>();
            }
        }

        walkerAgent = walker != null ? walker.GetComponent<NavMeshAgent>() : null;
        DisableConflictingDogComponents(walker);
    }

    private void ResolveDogRoomAgentSelection()
    {
        DogRoomAgent[] agents = Object.FindObjectsByType<DogRoomAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (agents == null || agents.Length == 0)
        {
            return;
        }

        DogRoomAgent selectedAgent = null;
        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (agent != null
                && agent.HasExplicitDogId
                && string.Equals(agent.DogId, session.SelectedDogId, System.StringComparison.OrdinalIgnoreCase))
            {
                selectedAgent = agent;
                break;
            }
        }

        if (selectedAgent == null)
        {
            selectedAgent = agents[0];
        }

        walker = selectedAgent.transform;
        walkerAnimator = selectedAgent.GetComponent<Animator>();
        if (walkerAnimator == null)
        {
            walkerAnimator = selectedAgent.GetComponentInChildren<Animator>();
        }

        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (agent != null && agent != selectedAgent)
            {
                encounterDog = agent.transform;
                break;
            }
        }
    }

    private void ResolveAnimatorDogSelection()
    {
        Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            Transform root = ResolveWalkerRoot(animator.transform);
            string animatorName = animator.name.ToLowerInvariant();
            string rootName = root != null ? root.name.ToLowerInvariant() : string.Empty;
            bool likelyDog = animator.GetComponentInParent<NavMeshAgent>() != null
                || animatorName.Contains("puppy")
                || animatorName.Contains("labrador")
                || animatorName.Contains("dog")
                || rootName.Contains("puppy")
                || rootName.Contains("labrador")
                || rootName.Contains("dog");

            if (!likelyDog)
            {
                continue;
            }

            if (walker == null)
            {
                walker = root;
                walkerAnimator = animator;
            }
            else if (encounterDog == null && root != walker)
            {
                encounterDog = root;
                break;
            }
        }
    }

    private static Transform ResolveWalkerRoot(Transform source)
    {
        if (source == null)
        {
            return null;
        }

        NavMeshAgent agent = source.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = source.GetComponentInParent<NavMeshAgent>();
        }

        return agent != null ? agent.transform : source;
    }

    private void ConfigureSidewalkPathAndCamera()
    {
        ConfigureSceneCamera();
        BuildSidewalkPath();
        walkSpeed = Mathf.Clamp(totalPathDistance / Mathf.Max(1f, walkDuration), 0.9f, 1.35f);
        PositionCamera(true);
    }

    private void BuildSidewalkPath()
    {
        worldPath.Clear();

        Vector3 start = walker != null ? walker.position : new Vector3(1.25f, 0.1f, -8.7f);
        if (!IsFinite(start))
        {
            start = new Vector3(1.25f, 0.1f, -8.7f);
        }

        start = ProjectWalkPoint(start, start.y);
        float travelDistance = Mathf.Clamp(session.RouteDistance * 0.12f, MinimumWorldPathDistance, MaximumWorldPathDistance);
        sidewalkDirection = ChooseSidewalkDirection(start, travelDistance);

        AddPathPoint(start);
        AddPathPoint(start + sidewalkDirection * (travelDistance * 0.35f));
        AddPathPoint(start + sidewalkDirection * (travelDistance * 0.68f));
        AddPathPoint(start + sidewalkDirection * travelDistance);

        RebuildPathDistances();
        currentSegmentIndex = FindSegmentForProgress(progress);
    }

    private void AddPathPoint(Vector3 candidate)
    {
        float fallbackY = worldPath.Count > 0 ? worldPath[worldPath.Count - 1].y : candidate.y;
        Vector3 projected = ProjectWalkPoint(candidate, fallbackY);
        if (worldPath.Count == 0 || Vector3.Distance(worldPath[worldPath.Count - 1], projected) > 0.35f)
        {
            worldPath.Add(projected);
        }
    }

    private Vector3 ChooseSidewalkDirection(Vector3 start, float travelDistance)
    {
        int forwardScore = ScoreDirection(start, Vector3.forward, travelDistance);
        int backScore = ScoreDirection(start, Vector3.back, travelDistance);
        return forwardScore > backScore ? Vector3.forward : Vector3.back;
    }

    private static int ScoreDirection(Vector3 start, Vector3 direction, float travelDistance)
    {
        int score = 0;
        for (int i = 1; i <= 3; i++)
        {
            Vector3 sample = start + direction * (travelDistance * i / 3f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(sample, out hit, 3f, NavMesh.AllAreas))
            {
                score++;
            }
        }

        return score;
    }

    private Vector3 ProjectWalkPoint(Vector3 candidate, float fallbackY)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(candidate, out navHit, 2.6f, NavMesh.AllAreas))
        {
            return navHit.position;
        }

        Vector3 origin = candidate + Vector3.up * GroundProbeHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GroundProbeDistance);
        float bestDistance = float.MaxValue;
        bool foundHit = false;
        Vector3 bestPoint = candidate;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (IsWalkerHit(hit) || hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestPoint = hit.point;
            foundHit = true;
        }

        if (foundHit)
        {
            return bestPoint;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            candidate.y = terrain.SampleHeight(candidate) + terrain.transform.position.y;
            return candidate;
        }

        candidate.y = fallbackY;
        return candidate;
    }

    private bool IsWalkerHit(RaycastHit hit)
    {
        if (walker == null || hit.transform == null)
        {
            return false;
        }

        return hit.transform == walker || hit.transform.IsChildOf(walker);
    }

    private void RebuildPathDistances()
    {
        if (worldPath.Count < 2)
        {
            Vector3 start = worldPath.Count > 0 ? worldPath[0] : new Vector3(1.25f, 0.1f, -8.7f);
            worldPath.Clear();
            worldPath.Add(start);
            worldPath.Add(start + Vector3.back * MinimumWorldPathDistance);
        }

        cumulativePathDistances = new float[worldPath.Count];
        totalPathDistance = 0f;
        cumulativePathDistances[0] = 0f;
        for (int i = 1; i < worldPath.Count; i++)
        {
            totalPathDistance += Vector3.Distance(worldPath[i - 1], worldPath[i]);
            cumulativePathDistances[i] = totalPathDistance;
        }

        if (totalPathDistance <= 0.001f)
        {
            totalPathDistance = 1f;
        }
    }

    private void BeginPathMotion()
    {
        MoveWalkerToProgress(progress, true);

        usingAgent = false;
        if (walkerAgent != null && walkerAgent.gameObject.activeInHierarchy)
        {
            walkerAgent.enabled = true;
            walkerAgent.updatePosition = true;
            walkerAgent.updateRotation = true;
            walkerAgent.speed = walkSpeed;
            walkerAgent.acceleration = 4f;
            walkerAgent.angularSpeed = 360f;
            walkerAgent.stoppingDistance = 0.08f;
            walkerAgent.autoBraking = true;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(walker.position, out hit, 2.5f, NavMesh.AllAreas))
            {
                usingAgent = walkerAgent.Warp(hit.position);
                if (usingAgent)
                {
                    currentSegmentIndex = FindSegmentForProgress(progress);
                    SetAgentDestinationToNextWaypoint();
                }
            }

            if (!usingAgent)
            {
                walkerAgent.enabled = false;
            }
        }

        SetWalkingAnimation(true);
    }

    private void UpdateWalkerMovement()
    {
        if (walker == null || worldPath.Count < 2)
        {
            return;
        }

        if (usingAgent && walkerAgent != null && walkerAgent.enabled && walkerAgent.isOnNavMesh)
        {
            walkerAgent.isStopped = false;
            if (!walkerAgent.pathPending && (!walkerAgent.hasPath || walkerAgent.pathStatus == NavMeshPathStatus.PathInvalid))
            {
                usingAgent = false;
                walkerAgent.enabled = false;
                UpdateDirectMovement();
                return;
            }

            if (!walkerAgent.pathPending && walkerAgent.remainingDistance <= Mathf.Max(0.22f, walkerAgent.stoppingDistance + 0.08f))
            {
                currentSegmentIndex++;
                if (currentSegmentIndex >= worldPath.Count - 1)
                {
                    progress = 1f;
                    return;
                }

                SetAgentDestinationToNextWaypoint();
            }

            SetWalkingAnimation(walkerAgent.velocity.sqrMagnitude > 0.0025f || progress < 0.99f);
            return;
        }

        usingAgent = false;
        UpdateDirectMovement();
    }

    private void UpdateDirectMovement()
    {
        currentSegmentIndex = Mathf.Clamp(currentSegmentIndex, 0, worldPath.Count - 2);
        Vector3 target = worldPath[currentSegmentIndex + 1];
        Vector3 current = walker.position;
        Vector3 next = Vector3.MoveTowards(current, target, walkSpeed * Time.deltaTime);
        next.y = ProjectWalkPoint(next, current.y).y;
        walker.position = next;

        Vector3 forward = target - current;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.001f)
        {
            walker.rotation = Quaternion.Slerp(walker.rotation, Quaternion.LookRotation(forward.normalized, Vector3.up), Time.deltaTime * 10f);
        }

        if (Vector3.Distance(walker.position, target) <= 0.12f)
        {
            currentSegmentIndex++;
        }

        SetWalkingAnimation(true);
    }

    private bool SetAgentDestinationToNextWaypoint()
    {
        if (walkerAgent == null || !walkerAgent.enabled || !walkerAgent.isOnNavMesh || currentSegmentIndex >= worldPath.Count - 1)
        {
            return false;
        }

        Vector3 target = worldPath[currentSegmentIndex + 1];
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas))
        {
            target = hit.position;
        }

        walkerAgent.isStopped = false;
        bool accepted = walkerAgent.SetDestination(target);
        if (!accepted)
        {
            usingAgent = false;
        }

        return accepted;
    }

    private float CalculateProgressFromWalker()
    {
        if (walker == null || worldPath.Count < 2)
        {
            return progress;
        }

        currentSegmentIndex = Mathf.Clamp(currentSegmentIndex, 0, worldPath.Count - 2);
        Vector3 segmentStart = worldPath[currentSegmentIndex];
        Vector3 segmentEnd = worldPath[currentSegmentIndex + 1];
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLength = segment.magnitude;
        if (segmentLength <= 0.001f)
        {
            return progress;
        }

        float alongSegment = Vector3.Dot(walker.position - segmentStart, segment / segmentLength);
        alongSegment = Mathf.Clamp(alongSegment, 0f, segmentLength);
        float traveled = cumulativePathDistances[currentSegmentIndex] + alongSegment;
        return Mathf.Clamp01(Mathf.Max(progress, traveled / totalPathDistance));
    }

    private int FindSegmentForProgress(float normalizedProgress)
    {
        if (cumulativePathDistances == null || cumulativePathDistances.Length < 2)
        {
            return 0;
        }

        float targetDistance = Mathf.Clamp01(normalizedProgress) * totalPathDistance;
        for (int i = 0; i < cumulativePathDistances.Length - 1; i++)
        {
            if (targetDistance <= cumulativePathDistances[i + 1])
            {
                return i;
            }
        }

        return cumulativePathDistances.Length - 2;
    }

    private Vector3 GetPathPosition(float normalizedProgress)
    {
        if (worldPath.Count == 0)
        {
            return walker != null ? walker.position : Vector3.zero;
        }

        if (worldPath.Count == 1)
        {
            return worldPath[0];
        }

        int segmentIndex = FindSegmentForProgress(normalizedProgress);
        float segmentStartDistance = cumulativePathDistances[segmentIndex];
        float segmentEndDistance = cumulativePathDistances[segmentIndex + 1];
        float segmentDistance = Mathf.Max(0.001f, segmentEndDistance - segmentStartDistance);
        float targetDistance = Mathf.Clamp01(normalizedProgress) * totalPathDistance;
        float segmentT = Mathf.Clamp01((targetDistance - segmentStartDistance) / segmentDistance);
        return Vector3.Lerp(worldPath[segmentIndex], worldPath[segmentIndex + 1], segmentT);
    }

    private Vector3 GetPathDirection(float normalizedProgress)
    {
        if (worldPath.Count < 2)
        {
            return sidewalkDirection;
        }

        int segmentIndex = FindSegmentForProgress(normalizedProgress);
        Vector3 direction = worldPath[segmentIndex + 1] - worldPath[segmentIndex];
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : sidewalkDirection;
    }

    private void MoveWalkerToProgress(float normalizedProgress, bool immediate)
    {
        if (walker == null)
        {
            return;
        }

        Vector3 position = GetPathPosition(normalizedProgress);
        Vector3 direction = GetPathDirection(normalizedProgress);

        if (walkerAgent != null && walkerAgent.enabled && walkerAgent.isOnNavMesh)
        {
            walkerAgent.Warp(position);
        }
        else
        {
            walker.position = position;
        }

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            walker.rotation = immediate ? targetRotation : Quaternion.Slerp(walker.rotation, targetRotation, Time.deltaTime * 10f);
        }

        PositionCamera(immediate);
    }

    private void ConfigureSceneCamera()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        sceneCamera = Camera.main;
        if (sceneCamera == null && cameras.Length > 0)
        {
            sceneCamera = cameras[0];
        }

        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("WalkSideCamera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == sceneCamera)
            {
                continue;
            }

            camera.enabled = false;
            AudioListener otherListener = camera.GetComponent<AudioListener>();
            if (otherListener != null)
            {
                otherListener.enabled = false;
            }
        }

        sceneCamera.enabled = true;
        sceneCamera.fieldOfView = 50f;
        sceneCamera.nearClipPlane = 0.03f;
        sceneCamera.farClipPlane = 180f;

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.gameObject != sceneCamera.gameObject)
            {
                listener.enabled = false;
            }
        }

        AudioListener sceneListener = sceneCamera.GetComponent<AudioListener>();
        if (sceneListener == null)
        {
            sceneListener = sceneCamera.gameObject.AddComponent<AudioListener>();
        }

        sceneListener.enabled = true;
    }

    private void PositionCamera(bool immediate)
    {
        if (sceneCamera == null || walker == null)
        {
            return;
        }

        Vector3 side = Vector3.Cross(Vector3.up, sidewalkDirection).normalized;
        if (side.sqrMagnitude <= 0.001f)
        {
            side = Vector3.left;
        }

        if (Vector3.Dot(side, Vector3.left) < 0f)
        {
            side = -side;
        }

        Vector3 desiredPosition = walker.position + side * 3.15f + Vector3.up * 1.35f - sidewalkDirection * 0.35f;
        Vector3 lookTarget = walker.position + Vector3.up * 0.62f + sidewalkDirection * 0.25f;
        if (immediate)
        {
            sceneCamera.transform.position = desiredPosition;
        }
        else
        {
            sceneCamera.transform.position = Vector3.SmoothDamp(sceneCamera.transform.position, desiredPosition, ref cameraVelocity, 0.16f);
        }

        sceneCamera.transform.LookAt(lookTarget);
    }

    private void DisableConflictingDogComponents(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Behaviour[] behaviours = target.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is Animator || behaviour is NavMeshAgent)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "DogIdleController"
                || typeName == "DogRoomAgent"
                || typeName == "DogMovementController"
                || typeName == "DogFreeRoam"
                || typeName == "ClickToMove"
                || typeName == "DogController")
            {
                behaviour.enabled = false;
            }
        }

        if (walkerAnimator != null)
        {
            walkerAnimator.applyRootMotion = false;
        }
    }

    private void BuildHud()
    {
        hudCanvas = new GameObject("WalkHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 700;

        CanvasScaler scaler = hudCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        hudRoot = hudCanvas.GetComponent<RectTransform>();
        UiFactory.Stretch(hudRoot, 0f, 0f, 0f, 0f);

        RectTransform top = CreateNode("TopHud", hudRoot, 14f, 18f, 365f, 66f);
        Image topFill = top.gameObject.AddComponent<Image>();
        topFill.sprite = UiTheme.RoundedTenSprite;
        topFill.type = Image.Type.Sliced;
        topFill.color = new Color32(252, 248, 232, 232);
        CreateOutline(top, Coral, 1f);

        titleLabel = CreateLabel(top, "Title", session.SelectedDogName + "'s walk", 16, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.sizeDelta = new Vector2(180f, 22f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(14f, -8f);

        detailLabel = CreateLabel(top, "Detail", string.Empty, 12, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        detailLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        detailLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        detailLabel.rectTransform.pivot = new Vector2(0f, 1f);
        detailLabel.rectTransform.sizeDelta = new Vector2(230f, 18f);
        detailLabel.rectTransform.anchoredPosition = new Vector2(14f, -31f);

        RectTransform bar = CreateNode("ProgressBar", top, 14f, 50f, 337f, 8f);
        Image barBack = bar.gameObject.AddComponent<Image>();
        barBack.sprite = UiTheme.RoundedTenSprite;
        barBack.type = Image.Type.Sliced;
        barBack.color = new Color32(236, 223, 200, 255);

        progressFill = UiFactory.CreateImage("Fill", bar, UiTheme.RoundedTenSprite, Coral);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        UiFactory.Stretch(progressFill.rectTransform, 0f, 0f, 0f, 0f);

        CreateButton(top, "CancelButton", "Cancel", 286f, 9f, 64f, CancelWalk);

        BuildEventCard();
        BuildSummaryPanel();
        RefreshHud();
    }

    private void BuildEventCard()
    {
        eventCard = CreateCenteredNode("EventCard", hudRoot, 314f, 170f, 0f);
        Image fill = eventCard.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Cream;
        CreateOutline(eventCard, Coral, 1f);

        eventTitleLabel = CreateLabel(eventCard, "Title", "A small surprise", 18, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        eventTitleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        eventTitleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        eventTitleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        eventTitleLabel.rectTransform.offsetMin = new Vector2(12f, -44f);
        eventTitleLabel.rectTransform.offsetMax = new Vector2(-12f, -12f);

        eventBodyLabel = CreateLabel(eventCard, "Body", string.Empty, 14, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        eventBodyLabel.textWrappingMode = TextWrappingModes.Normal;
        eventBodyLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        eventBodyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        eventBodyLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        eventBodyLabel.rectTransform.offsetMin = new Vector2(18f, -105f);
        eventBodyLabel.rectTransform.offsetMax = new Vector2(-18f, -48f);

        CreateButton(eventCard, "Continue", "Continue", 102f, 128f, 110f, ContinueEvent);
        eventCard.gameObject.SetActive(false);
    }

    private void BuildSummaryPanel()
    {
        summaryPanel = CreateCenteredNode("WalkSummary", hudRoot, 330f, 312f, 0f);
        Image fill = summaryPanel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Cream;
        CreateOutline(summaryPanel, Coral, 1f);

        TextMeshProUGUI title = CreateLabel(summaryPanel, "Title", "Walk complete", 20, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(12f, -44f);
        title.rectTransform.offsetMax = new Vector2(-12f, -12f);

        summaryBodyLabel = CreateLabel(summaryPanel, "Body", string.Empty, 14, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        summaryBodyLabel.textWrappingMode = TextWrappingModes.Normal;
        summaryBodyLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        summaryBodyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        summaryBodyLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        summaryBodyLabel.rectTransform.offsetMin = new Vector2(20f, -238f);
        summaryBodyLabel.rectTransform.offsetMax = new Vector2(-20f, -56f);

        CreateButton(summaryPanel, "Done", "Done", 110f, 260f, 110f, delegate
        {
            PawPalWalkSceneFlow.ReturnHome(returnSceneName);
        });
        summaryPanel.gameObject.SetActive(false);
    }

    private void RefreshHud()
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = progress;
        }

        if (detailLabel != null)
        {
            detailLabel.text = Mathf.RoundToInt(progress * 100f) + "%  -  " + Mathf.RoundToInt(session.RouteDistance) + " distance";
        }
    }

    private PawPalWalkGeneratedEventState GetNextEvent()
    {
        if (session == null || session.GeneratedEvents == null)
        {
            return null;
        }

        for (int i = 0; i < session.GeneratedEvents.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = session.GeneratedEvents[i];
            if (walkEvent != null && !walkEvent.Resolved)
            {
                return walkEvent;
            }
        }

        return null;
    }

    private void ShowEvent(PawPalWalkGeneratedEventState walkEvent)
    {
        paused = true;
        PauseWalker(true);
        SetWalkingAnimation(false);

        if (walkEvent.EventType == PawPalWalkEventType.PresentFound)
        {
            string itemName = "a present";
            if (runtime != null)
            {
                PawPalCatalogItemDefinition item = runtime.GetCatalogItem(walkEvent.RewardItemId);
                if (item != null)
                {
                    itemName = item.DisplayName;
                }
            }

            eventTitleLabel.text = "Present found";
            eventBodyLabel.text = session.SelectedDogName + " found " + itemName + ".";
            SpawnPresent();
        }
        else if (walkEvent.EventType == PawPalWalkEventType.DogEncounter)
        {
            eventTitleLabel.text = "Friendly hello";
            eventBodyLabel.text = session.SelectedDogName + " met " + walkEvent.DisplayName + ".";
            MoveEncounterDogNearWalker();
        }
        else
        {
            eventTitleLabel.text = walkEvent.DisplayName;
            eventBodyLabel.text = BuildLocationText(walkEvent);
        }

        eventCard.gameObject.SetActive(true);
    }

    private string BuildLocationText(PawPalWalkGeneratedEventState walkEvent)
    {
        if (walkEvent.LocationId == "dog_park")
        {
            return session.SelectedDogName + " had a happy stop at the park.";
        }

        if (walkEvent.LocationId == "kennel")
        {
            return session.SelectedDogName + " visited the kennel.";
        }

        if (walkEvent.LocationId == "competition_center")
        {
            return session.SelectedDogName + " peeked at the competition center.";
        }

        return session.SelectedDogName + " stopped for a moment.";
    }

    private void ContinueEvent()
    {
        if (activePresent != null)
        {
            Destroy(activePresent);
            activePresent = null;
        }

        PawPalWalkGeneratedEventState nextEvent = GetNextEvent();
        if (runtime != null && nextEvent != null)
        {
            runtime.MarkActiveWalkEventResolved(nextEvent.EventId);
        }

        eventCard.gameObject.SetActive(false);
        paused = false;
        PauseWalker(false);
        SetWalkingAnimation(true);
    }

    private void CancelWalk()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        paused = true;
        PauseWalker(true);
        SetWalkingAnimation(false);
        if (runtime != null)
        {
            runtime.CancelActiveWalkSession();
        }

        PawPalWalkSceneFlow.ReturnHome(PawPalWalkSceneFlow.HomeSceneName);
    }

    private void CompleteWalk()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        paused = true;
        progress = 1f;
        PauseWalker(true);
        MoveWalkerToProgress(1f, false);
        SetWalkingAnimation(false);

        PawPalWalkCompletionResult result = runtime != null
            ? runtime.CompleteActiveWalkSession()
            : new PawPalWalkCompletionResult { DogName = session.SelectedDogName, Distance = session.RouteDistance, StaminaUsed = session.StaminaCost };

        ShowSummary(result);
    }

    private void PauseWalker(bool shouldPause)
    {
        if (walkerAgent != null && walkerAgent.enabled && walkerAgent.isOnNavMesh)
        {
            walkerAgent.isStopped = shouldPause;
            if (!shouldPause)
            {
                SetAgentDestinationToNextWaypoint();
            }
        }
    }

    private void ShowSummary(PawPalWalkCompletionResult result)
    {
        if (summaryPanel == null || summaryBodyLabel == null)
        {
            return;
        }

        string body = result.DogName + " walked " + Mathf.RoundToInt(result.Distance) + " distance.\n";
        body += "Stamina used: " + Mathf.RoundToInt(result.StaminaUsed) + "\n";
        body += "Stops: " + JoinOrNone(result.LocationsVisited) + "\n";
        body += "Presents: " + JoinOrNone(result.ItemsReceived) + "\n";
        body += "Dogs met: " + JoinOrNone(result.DogsMet);
        if (result.IncreasedMaxStamina)
        {
            body += "\nStamina grew to " + Mathf.RoundToInt(result.NewMaxStamina) + ".";
        }

        summaryBodyLabel.text = body;
        summaryPanel.gameObject.SetActive(true);
    }

    private string JoinOrNone(List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        string text = string.Empty;
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                text += ", ";
            }

            text += values[i];
        }

        return text;
    }

    private void SpawnPresent()
    {
        if (walker == null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>("PawPal/Walk/Present");
        activePresent = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        activePresent.name = "WalkPresent";
        activePresent.transform.position = walker.position + sidewalkDirection * 0.95f + Vector3.up * 0.25f;
        activePresent.transform.localScale = Vector3.one * 0.35f;
        Renderer renderer = activePresent.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color32(223, 120, 97, 255);
        }
    }

    private void MoveEncounterDogNearWalker()
    {
        if (encounterDog == null || walker == null)
        {
            return;
        }

        encounterDog.gameObject.SetActive(true);
        encounterDog.position = walker.position + sidewalkDirection * 1.45f + Vector3.right * 0.65f;
        encounterDog.rotation = Quaternion.LookRotation(-sidewalkDirection, Vector3.up);
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (walkerAnimator == null)
        {
            return;
        }

        SetAnimatorBoolIfExists(walkerAnimator, MoveHash, walking);
        SetAnimatorFloatIfExists(walkerAnimator, SpeedHash, walking ? 0.58f : 0f);
        SetAnimatorFloatIfExists(walkerAnimator, DirectionHash, 0f);
        SetAnimatorIntegerIfExists(walkerAnimator, IdleIndexHash, walking ? -1 : 99);
    }

    private static void SetAnimatorBoolIfExists(Animator animator, int parameterHash, bool value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterHash, value);
        }
    }

    private static void SetAnimatorFloatIfExists(Animator animator, int parameterHash, float value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameterHash, value);
        }
    }

    private static void SetAnimatorIntegerIfExists(Animator animator, int parameterHash, int value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(parameterHash, value);
        }
    }

    private static bool HasAnimatorParameter(Animator animator, int parameterHash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect;
    }

    private static RectTransform CreateCenteredNode(string name, RectTransform parent, float width, float height, float yOffset)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, yOffset);
        return rect;
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string labelText, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateNode(name, parent, x, y, width, 28f);
        Image fill = rect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Coral;
        UiFactory.AddButton(rect.gameObject, onClick);

        TextMeshProUGUI label = CreateLabel(rect, "Label", labelText, 13, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 6f, 0f, 6f, 0f);
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void CreateOutline(RectTransform target, Color color, float distance)
    {
        Outline outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }
}
