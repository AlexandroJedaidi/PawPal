using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PawPalAgilityTrialSceneController : MonoBehaviour
{
    private sealed class RuntimeObstacle
    {
        public PawPalAgilityObstacleDefinition Definition;
        public Transform Target;
        public bool Succeeded;
        public bool Faulted;
    }

    private const float GroundProbeHeight = 8f;
    private const float GroundProbeDistance = 24f;
    private const float BaseMoveSpeed = 2.15f;
    private const float RotationSpeed = 10f;
    private const int CanvasSortingOrder = 4500;

    private readonly List<RuntimeObstacle> obstacles = new List<RuntimeObstacle>();

    private PawPalGameRuntime runtime;
    private PawPalAgilityTrialSessionSaveData session;
    private PawPalAgilityTrialConfig config;
    private PawPalAgilityLevelDefinition level;
    private PawPalDogState petState;
    private SelectedPetSessionData petSelection;
    private IntroPetSpecies petSpecies = IntroPetSpecies.Dog;
    private PawPalPetMovementProfile movementProfile = PawPalPetMovementProfiles.DefaultProfile;
    private Transform petRoot;
    private Animator petAnimator;
    private Camera sceneCamera;
    private Canvas hudCanvas;
    private TextMeshProUGUI timerLabel;
    private TextMeshProUGUI faultsLabel;
    private TextMeshProUGUI promptLabel;
    private TextMeshProUGUI modeLabel;
    private int currentObstacleIndex;
    private int faults;
    private int completedObstacles;
    private float startedAt;
    private float flowScore = 1f;
    private bool startupFailed;
    private bool completed;
    private bool inputQueued;
    private Vector2 inputDelta;
    private Vector2 inputStart;
    private Coroutine obstacleRoutine;

    private void Start()
    {
        runtime = PawPalGameRuntime.Instance;
        session = runtime != null ? runtime.ActiveAgilityTrialSession : null;
        if (session == null)
        {
            FailStartup("No active Agility Trial session was found.");
            return;
        }

        config = PawPalAgilityTrialConfig.LoadOrCreateDefault();
        level = config.GetLevel(session.LevelId);
        if (level == null)
        {
            FailStartup("Agility Trial level data is missing.");
            return;
        }

        if (!ResolvePet())
        {
            FailStartup("The selected pet could not be spawned for Agility Trial.");
            return;
        }

        EnsureSceneCamera();
        BuildCourse();
        if (obstacles.Count < 2)
        {
            FailStartup("The Agility Trial course has no usable obstacles.");
            return;
        }

        EnsureParkBackdrop();
        BuildHud();
        PositionPetAtStart();
        startedAt = Time.time;
        SetPromptForCurrentObstacle();
    }

    private void Update()
    {
        if (startupFailed || completed || petRoot == null || obstacles.Count == 0)
        {
            return;
        }

        CaptureInput();
        UpdateHud();
        RuntimeObstacle current = obstacles[Mathf.Clamp(currentObstacleIndex, 0, obstacles.Count - 1)];
        if (current == null || current.Target == null)
        {
            AdvanceObstacle(false);
            return;
        }

        MovePetToward(current.Target.position);
        PositionCamera();

        float distance = Vector3.Distance(ProjectFlat(petRoot.position), ProjectFlat(current.Target.position));
        if (ShouldAcceptInput(current, distance))
        {
            CompleteObstacle(current);
        }
        else if (distance <= Mathf.Max(0.28f, current.Definition.SuccessRadius * 0.35f))
        {
            if (IsGate(current.Definition.Type))
            {
                CompleteObstacle(current);
            }
            else
            {
                AddFault(current);
                AdvanceObstacle(true);
            }
        }
    }

    private bool ResolvePet()
    {
        if (runtime == null)
        {
            return false;
        }

        IReadOnlyList<PawPalDogState> dogs = runtime.Dogs;
        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState candidate = dogs[i];
            if (candidate != null && string.Equals(candidate.Id, session.SelectedDogId, StringComparison.OrdinalIgnoreCase))
            {
                petState = candidate;
                break;
            }
        }

        if (petState == null)
        {
            petState = runtime.ActiveDog;
        }

        if (petState == null)
        {
            return false;
        }

        petSpecies = runtime.GetPetSpecies(petState.Id);
        IntroPetDefinition definition = PawPalWalkSceneController.ResolveIntroPetDefinition(petState, petSpecies);
        if (definition == null)
        {
            return false;
        }

        FurVariantDefinition furVariant = ResolveFurVariant(definition, petState.FurColor);
        petSelection = new SelectedPetSessionData
        {
            Definition = definition,
            FurVariant = furVariant,
            FurIndex = definition.GetFurVariantIndex(furVariant),
            Gender = petState.Gender,
            Personality = petState.Personality,
            PetName = string.IsNullOrWhiteSpace(petState.DisplayName) ? definition.SpeciesLabel : petState.DisplayName,
            RuntimePetId = petState.Id
        };

        GameObject prefab = PetVariantApplier.GetPrefabForVariant(definition, furVariant);
        string error;
        GameObject instance;
        if (!PetVariantApplier.TryInstantiatePrefab(prefab != null ? prefab : definition.BasePrefab, Vector3.zero, Quaternion.identity, null, out instance, out error))
        {
            Debug.LogWarning("PawPalAgilityTrialSceneController could not instantiate pet prefab. " + error);
            return false;
        }

        instance.name = (petSpecies == IntroPetSpecies.Cat ? "AgilityCat_" : "AgilityDog_") + petSelection.SafeName;
        if (furVariant != null)
        {
            PetVariantApplier.ApplyMaterial(instance, furVariant);
        }

        if (definition.AnimationSet != null)
        {
            definition.AnimationSet.ApplyTo(instance.GetComponentInChildren<Animator>(true), definition);
        }

        ConfigureSpawnedPet(instance);
        petRoot = instance.transform;
        petAnimator = instance.GetComponent<Animator>();
        if (petAnimator == null)
        {
            petAnimator = instance.GetComponentInChildren<Animator>(true);
        }

        movementProfile = PawPalPetMovementProfiles.Resolve(definition, petState.Breed, instance.name);
        return true;
    }

    private void ConfigureSpawnedPet(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        NavMeshAgent[] agents = instance.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null && agents[i].enabled)
            {
                agents[i].enabled = false;
            }
        }

        DogRoomAgent dogAgent = instance.GetComponent<DogRoomAgent>();
        if (dogAgent != null)
        {
            dogAgent.SetRuntimeDogId(petState.Id);
            dogAgent.ApplySelectedPetPresentation(petSelection);
            dogAgent.ConfigureSelectedPetRuntime(petSelection);
            dogAgent.SetExternalWalkControl(true);
            dogAgent.SetExternalWalkPace(DogMovementPace.Run);
        }

        PawPalCatRoomAgent catAgent = instance.GetComponent<PawPalCatRoomAgent>();
        if (catAgent == null)
        {
            catAgent = instance.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        if (catAgent != null)
        {
            catAgent.Initialize(petSelection);
            catAgent.SetExternalWalkControl(true);
            catAgent.SetExternalWalkPace(DogMovementPace.Run);
        }
    }

    private static FurVariantDefinition ResolveFurVariant(IntroPetDefinition definition, string furColor)
    {
        if (definition == null)
        {
            return null;
        }

        if (definition.FurVariants != null)
        {
            for (int i = 0; i < definition.FurVariants.Length; i++)
            {
                FurVariantDefinition variant = definition.FurVariants[i];
                if (variant != null && string.Equals(variant.SafeDisplayName, furColor, StringComparison.OrdinalIgnoreCase))
                {
                    return variant;
                }
            }
        }

        return definition.GetDefaultFurVariant();
    }

    private void BuildCourse()
    {
        obstacles.Clear();
        for (int i = 0; i < level.Obstacles.Count; i++)
        {
            PawPalAgilityObstacleDefinition definition = level.Obstacles[i];
            if (definition == null)
            {
                continue;
            }

            Transform target = ResolveObstacleTarget(definition, i);
            obstacles.Add(new RuntimeObstacle { Definition = definition, Target = target });
        }
    }

    private void EnsureParkBackdrop()
    {
        Bounds courseBounds = ResolveCourseBounds();
        PawPalObedienceTrialBackdropController backdrop = FindFirstObjectByType<PawPalObedienceTrialBackdropController>();
        if (backdrop == null)
        {
            GameObject backdropObject = new GameObject("AgilityParkBackdropRing");
            backdrop = backdropObject.AddComponent<PawPalObedienceTrialBackdropController>();
        }

        backdrop.Configure(courseBounds);
    }

    private Bounds ResolveCourseBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        for (int i = 0; i < obstacles.Count; i++)
        {
            RuntimeObstacle obstacle = obstacles[i];
            if (obstacle == null || obstacle.Target == null)
            {
                continue;
            }

            Vector3 position = obstacle.Target.position;
            if (!hasBounds)
            {
                bounds = new Bounds(position, new Vector3(2f, 1f, 2f));
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(position);
            }
        }

        if (!hasBounds)
        {
            return new Bounds(Vector3.zero, new Vector3(18f, 1f, 10f));
        }

        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x + 4f, 18f);
        size.y = Mathf.Max(size.y, 1f);
        size.z = Mathf.Max(size.z + 4f, 10f);
        bounds.size = size;
        return bounds;
    }

    private Transform ResolveObstacleTarget(PawPalAgilityObstacleDefinition definition, int index)
    {
        Transform found = null;
        if (!string.IsNullOrWhiteSpace(definition.SceneObjectName))
        {
            GameObject sceneObject = GameObject.Find(definition.SceneObjectName);
            if (sceneObject != null)
            {
                found = sceneObject.transform;
            }
        }

        if (found != null)
        {
            return found;
        }

        Vector3 position = definition.FallbackPosition;
        if (position == Vector3.zero)
        {
            position = new Vector3(-16f + index * 4f, 0f, (index % 2 == 0) ? -1.8f : 2.1f);
        }

        position = ProjectToGround(position);
        GameObject visual = CreateFallbackObstacleVisual(definition, position);
        return visual.transform;
    }

    private GameObject CreateFallbackObstacleVisual(PawPalAgilityObstacleDefinition definition, Vector3 position)
    {
#if UNITY_EDITOR
        GameObject prefab = LoadApprovedEditorPrefab(definition.Type);
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.name = string.IsNullOrWhiteSpace(definition.SceneObjectName)
                ? "Agility_" + definition.Type
                : definition.SceneObjectName;
            return instance;
        }
#endif

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "AgilityMarker_" + definition.Type;
        marker.transform.position = position + Vector3.up * 0.2f;
        marker.transform.localScale = IsGate(definition.Type) ? new Vector3(1.2f, 0.08f, 1.2f) : new Vector3(1f, 0.4f, 1f);
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = IsGate(definition.Type) ? new Color(0.2f, 0.7f, 1f, 0.8f) : new Color(1f, 0.55f, 0.35f, 0.8f);
        }

        Debug.LogWarning("PawPalAgilityTrialSceneController created a runtime marker for missing obstacle asset '" + definition.DisplayName + "'. Run the Agility setup utility to place approved course prefabs in the scene.");
        return marker;
    }

#if UNITY_EDITOR
    private static GameObject LoadApprovedEditorPrefab(PawPalAgilityObstacleType type)
    {
        string path = null;
        switch (type)
        {
            case PawPalAgilityObstacleType.BarrierRunAround:
                path = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Barrier_1.prefab";
                break;
            case PawPalAgilityObstacleType.BridgeWalkOver:
                path = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bridge.prefab";
                break;
            case PawPalAgilityObstacleType.HighFence:
                path = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Fence_dog.prefab";
                break;
            case PawPalAgilityObstacleType.WheelJump:
                path = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Wheel.prefab";
                break;
            case PawPalAgilityObstacleType.SeeSaw:
                path = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Swing.prefab";
                break;
        }

        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
#endif

    private void PositionPetAtStart()
    {
        RuntimeObstacle start = obstacles[0];
        Vector3 position = start.Target.position - Vector3.right * 1.4f;
        position = ProjectToGround(position);
        petRoot.position = position;
        petRoot.rotation = Quaternion.LookRotation((start.Target.position - position).normalized, Vector3.up);
    }

    private void MovePetToward(Vector3 target)
    {
        Vector3 current = petRoot.position;
        Vector3 groundTarget = ProjectToGround(target);
        Vector3 delta = groundTarget - current;
        delta.y = 0f;
        float speed = ResolveMoveSpeed();
        if (delta.sqrMagnitude > 0.001f)
        {
            Vector3 direction = delta.normalized;
            petRoot.position = Vector3.MoveTowards(current, groundTarget, speed * Time.deltaTime);
            petRoot.rotation = Quaternion.Slerp(petRoot.rotation, Quaternion.LookRotation(direction, Vector3.up), RotationSpeed * Time.deltaTime);
        }

        PawPalWalkPetAnimationPlayer.ForceLocomotionForPace(petAnimator, movementProfile, petSpecies, DogMovementPace.Run);
    }

    private float ResolveMoveSpeed()
    {
        float statBonus = petState != null ? Mathf.Clamp01((petState.Speed + petState.Mobility + petState.Focus) / 30f) : 0.35f;
        PawPalAgilityConditionSnapshot condition = PawPalAgilityScoringService.BuildConditionSnapshot(petState);
        return BaseMoveSpeed * Mathf.Lerp(0.82f, 1.18f, (statBonus + condition.Combined01) * 0.5f);
    }

    private void CaptureInput()
    {
        inputQueued = false;
        inputDelta = Vector2.zero;

        if (Input.GetMouseButtonDown(0))
        {
            inputStart = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            inputQueued = true;
            inputDelta = ((Vector2)Input.mousePosition) - inputStart;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            inputQueued = true;
            inputDelta = Vector2.up * 100f;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                inputStart = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                inputQueued = true;
                inputDelta = touch.position - inputStart;
            }
        }
    }

    private bool ShouldAcceptInput(RuntimeObstacle obstacle, float distance)
    {
        if (obstacle == null || obstacle.Definition == null || IsGate(obstacle.Definition.Type) || !inputQueued)
        {
            return false;
        }

        if (distance > Mathf.Max(obstacle.Definition.SuccessRadius, obstacle.Definition.InputLeadDistance))
        {
            return false;
        }

        if (obstacle.Definition.RequiresJumpCue)
        {
            return inputDelta.y > 24f || inputDelta.sqrMagnitude < 225f;
        }

        if (obstacle.Definition.Type == PawPalAgilityObstacleType.BarrierRunAround)
        {
            return Mathf.Abs(inputDelta.x) > 24f || inputDelta.sqrMagnitude < 225f;
        }

        return true;
    }

    private void CompleteObstacle(RuntimeObstacle obstacle)
    {
        if (obstacle.Succeeded || obstacle.Faulted)
        {
            return;
        }

        obstacle.Succeeded = true;
        completedObstacles++;
        if (obstacleRoutine != null)
        {
            StopCoroutine(obstacleRoutine);
        }

        obstacleRoutine = StartCoroutine(PlayObstacleSuccess(obstacle));
        AdvanceObstacle(false);
    }

    private IEnumerator PlayObstacleSuccess(RuntimeObstacle obstacle)
    {
        if (obstacle.Definition.RequiresJumpCue)
        {
            yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
                this,
                petAnimator,
                petSelection != null ? petSelection.Definition : null,
                petState != null ? petState.Breed : null,
                petRoot != null ? petRoot.name : null,
                0.45f,
                PawPalWalkPetAnimationPlayer.GetJumpReactionStateNames(petSpecies));
        }
        else if (obstacle.Definition.Type == PawPalAgilityObstacleType.SeeSaw)
        {
            yield return TiltSeeSaw(obstacle.Target);
        }
    }

    private IEnumerator TiltSeeSaw(Transform target)
    {
        if (target == null)
        {
            yield break;
        }

        Quaternion start = target.rotation;
        Quaternion end = start * Quaternion.Euler(0f, 0f, -9f);
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            target.rotation = Quaternion.Slerp(start, end, Mathf.Clamp01(elapsed / 0.35f));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            target.rotation = Quaternion.Slerp(end, start, Mathf.Clamp01(elapsed / 0.35f));
            yield return null;
        }
    }

    private void AddFault(RuntimeObstacle obstacle)
    {
        if (obstacle == null || obstacle.Faulted || IsGate(obstacle.Definition.Type))
        {
            return;
        }

        obstacle.Faulted = true;
        faults++;
        flowScore = Mathf.Clamp01(flowScore - 0.12f);
    }

    private void AdvanceObstacle(bool countCompleted)
    {
        if (countCompleted)
        {
            completedObstacles++;
        }

        currentObstacleIndex++;
        if (currentObstacleIndex >= obstacles.Count)
        {
            CompleteTrial();
            return;
        }

        SetPromptForCurrentObstacle();
    }

    private void CompleteTrial()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        PawPalAgilityRunStats stats = new PawPalAgilityRunStats
        {
            ElapsedSeconds = Time.time - startedAt,
            Faults = faults,
            ObstaclesCompleted = completedObstacles,
            TotalObstacles = obstacles.Count,
            Flow01 = flowScore,
            Completed = true
        };

        PawPalAgilityTrialResult result = runtime != null ? runtime.CompleteActiveAgilityTrial(stats) : new PawPalAgilityTrialResult();
        ShowResultOverlay(result);
    }

    private void SetPromptForCurrentObstacle()
    {
        if (promptLabel == null || currentObstacleIndex < 0 || currentObstacleIndex >= obstacles.Count)
        {
            return;
        }

        PawPalAgilityObstacleDefinition obstacle = obstacles[currentObstacleIndex].Definition;
        if (IsGate(obstacle.Type))
        {
            promptLabel.text = obstacle.DisplayName;
        }
        else if (obstacle.RequiresJumpCue)
        {
            promptLabel.text = "Swipe up at " + obstacle.DisplayName;
        }
        else if (obstacle.Type == PawPalAgilityObstacleType.BarrierRunAround)
        {
            promptLabel.text = "Swipe around " + obstacle.DisplayName;
        }
        else
        {
            promptLabel.text = "Tap at " + obstacle.DisplayName;
        }
    }

    private void EnsureSceneCamera()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        PositionCamera(true);
    }

    private void PositionCamera(bool immediate = false)
    {
        if (sceneCamera == null || petRoot == null)
        {
            return;
        }

        Vector3 desired = petRoot.position + new Vector3(0f, 5.2f, -7.2f);
        Quaternion rotation = Quaternion.Euler(35f, 0f, 0f);
        if (immediate)
        {
            sceneCamera.transform.SetPositionAndRotation(desired, rotation);
            return;
        }

        sceneCamera.transform.position = Vector3.Lerp(sceneCamera.transform.position, desired, Time.deltaTime * 4f);
        sceneCamera.transform.rotation = Quaternion.Slerp(sceneCamera.transform.rotation, rotation, Time.deltaTime * 5f);
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("AgilityTrialHud");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = CanvasSortingOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        RectTransform top = UiFactory.CreateRect("TopBar", root);
        UiFactory.AnchorTopStretch(top, 14f, 16f, 14f, 88f);
        Image topFill = top.gameObject.AddComponent<Image>();
        topFill.sprite = UiTheme.RoundedTenSprite;
        topFill.type = Image.Type.Sliced;
        topFill.color = UiTheme.BackgroundCream;

        modeLabel = UiFactory.CreateLabel("Mode", top, level.DisplayName + " " + (session.Mode == PawPalAgilityTrialMode.Practice ? "Practice" : "Trial"), 17, UiTheme.BrandDark, FontStyles.Bold, TextAlignmentOptions.Left);
        UiFactory.Stretch(modeLabel.rectTransform, 16f, 46f, 120f, 12f);
        timerLabel = UiFactory.CreateLabel("Timer", top, "0.0s", 18, UiTheme.BodyText, FontStyles.Bold, TextAlignmentOptions.Right);
        UiFactory.Stretch(timerLabel.rectTransform, 190f, 46f, 16f, 12f);
        faultsLabel = UiFactory.CreateLabel("Faults", top, "Faults: 0", 14, UiTheme.BodyText, FontStyles.Normal, TextAlignmentOptions.Left);
        UiFactory.Stretch(faultsLabel.rectTransform, 16f, 18f, 190f, 42f);
        promptLabel = UiFactory.CreateLabel("Prompt", top, string.Empty, 14, UiTheme.Brand, FontStyles.Bold, TextAlignmentOptions.Right);
        UiFactory.Stretch(promptLabel.rectTransform, 126f, 18f, 16f, 42f);
    }

    private void UpdateHud()
    {
        if (timerLabel != null)
        {
            timerLabel.text = (Time.time - startedAt).ToString("0.0") + "s";
        }

        if (faultsLabel != null)
        {
            faultsLabel.text = "Faults: " + faults;
        }
    }

    private void ShowResultOverlay(PawPalAgilityTrialResult result)
    {
        if (hudCanvas == null)
        {
            BuildHud();
        }

        RectTransform root = hudCanvas.GetComponent<RectTransform>();
        RectTransform panel = UiFactory.CreateRect("ResultPanel", root);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(335f, 272f);
        panel.anchoredPosition = Vector2.zero;

        Image fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = UiTheme.BackgroundCream;

        string title = session.Mode == PawPalAgilityTrialMode.Practice
            ? "Practice complete"
            : result.Medal + " placement";
        TextMeshProUGUI titleLabel = UiFactory.CreateLabel("Title", panel, title, 22, UiTheme.BrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        UiFactory.Stretch(titleLabel.rectTransform, 18f, 214f, 18f, 18f);

        string body = "Time: " + result.ElapsedSeconds.ToString("0.0") + "s\n"
            + "Faults: " + result.Faults + "\n"
            + "Score: " + result.Score + "\n"
            + "Reward: " + result.BasicCurrencyReward;
        if (result.UnlockedNextLevel)
        {
            body += "\nNext level unlocked";
        }

        TextMeshProUGUI bodyLabel = UiFactory.CreateLabel("Body", panel, body, 16, UiTheme.BodyText, FontStyles.Normal, TextAlignmentOptions.Center);
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(bodyLabel.rectTransform, 24f, 78f, 24f, 66f);

        CreateResultButton(panel, "Retry", -92f, -98f, "Retry", RetryTrial);
        CreateResultButton(panel, "Home", 92f, -98f, "Home", ReturnHome);
    }

    private void CreateResultButton(RectTransform parent, string name, float x, float y, string text, UnityEngine.Events.UnityAction action)
    {
        RectTransform buttonRect = UiFactory.CreateRect(name, parent);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(112f, 42f);
        buttonRect.anchoredPosition = new Vector2(x, y);
        Image fill = buttonRect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = UiTheme.Brand;
        UiFactory.AddButton(buttonRect.gameObject, action);
        TextMeshProUGUI label = UiFactory.CreateLabel("Label", buttonRect, text, 16, UiTheme.White, FontStyles.Bold, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 8f, 0f, 8f, 0f);
    }

    private void RetryTrial()
    {
        string failure = "Agility runtime is unavailable.";
        if (runtime != null && runtime.TryStartAgilityTrial(session.SelectedDogId, session.LevelId, session.Mode, session.ReturnSceneName, out failure))
        {
            PawPalAgilityTrialSceneFlow.LoadAgilityScene();
            return;
        }

        Debug.LogWarning("Could not retry Agility Trial: " + failure);
        ReturnHome();
    }

    private void ReturnHome()
    {
        PawPalAgilityTrialSceneFlow.ReturnHome(session != null ? session.ReturnSceneName : PawPalWalkSceneFlow.HomeSceneName);
    }

    private void FailStartup(string message)
    {
        startupFailed = true;
        Debug.LogError("PawPalAgilityTrialSceneController startup failed: " + message);
        PawPalWalkSceneFlow.SetAppShellVisible(true);
        ShowStartupFailureOverlay(message);
    }

    private void ShowStartupFailureOverlay(string message)
    {
        BuildHud();
        RectTransform root = hudCanvas.GetComponent<RectTransform>();
        RectTransform panel = UiFactory.CreateRect("StartupFailurePanel", root);
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = true;
        UiFactory.Stretch(panel, 32f, 300f, 32f, 300f);

        TextMeshProUGUI label = UiFactory.CreateLabel("StartupFailure", panel, message, 16, UiTheme.White, FontStyles.Bold, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(label.rectTransform, 18f, 18f, 18f, 18f);
    }

    private Vector3 ProjectToGround(Vector3 position)
    {
        Ray ray = new Ray(position + Vector3.up * GroundProbeHeight, Vector3.down);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, GroundProbeDistance))
        {
            position.y = hit.point.y;
        }

        return position;
    }

    private static Vector3 ProjectFlat(Vector3 position)
    {
        return new Vector3(position.x, 0f, position.z);
    }

    private static bool IsGate(PawPalAgilityObstacleType type)
    {
        return type == PawPalAgilityObstacleType.StartGate || type == PawPalAgilityObstacleType.FinishGate;
    }
}
