using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PawPalObedienceTrialSceneFlow
{
    public const string TrialSceneName = "TrialObedience";
    public const string TrialScenePath = "Assets/Scenes/TrialObedience.unity";
    public const string HomeSceneName = "David_Test";
    public const string HomeScenePath = "Assets/Scenes/David_Test.unity";

    public static bool LoadTrialScene()
    {
        PawPalSceneTransitionRequest request = new PawPalSceneTransitionRequest
        {
            DisplayText = "Preparing the obedience ring",
            MinimumVisibleDuration = 0.25f,
            WaitForExplicitReady = true,
            ExplicitReadyTimeoutSeconds = 8f
        };

        return PawPalSceneTransitionController.LoadSceneWithTransition(TrialSceneName, TrialScenePath, request);
    }

    public static bool ReturnHome()
    {
        PawPalSceneTransitionRequest request = new PawPalSceneTransitionRequest
        {
            DisplayText = "Returning home",
            MinimumVisibleDuration = 0.2f,
            WaitForExplicitReady = false
        };

        return PawPalSceneTransitionController.LoadSceneWithTransition(HomeSceneName, HomeScenePath, request);
    }
}

public static class PawPalObedienceTrialBootstrap
{
    private static bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (installed)
        {
            return;
        }

        installed = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstallForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallForScene(scene);
    }

    private static void TryInstallForScene(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, PawPalObedienceTrialSceneFlow.TrialSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<PawPalObedienceTrialController>() != null)
        {
            return;
        }

        GameObject root = new GameObject("PawPalObedienceTrialController");
        root.AddComponent<PawPalObedienceTrialController>();
    }
}

[DisallowMultipleComponent]
public sealed class PawPalObedienceTrialController : MonoBehaviour
{
    private const float StageZ = 0f;
    private const float StageY = 0f;
    private const float StagePetZ = -0.3f;

    private readonly List<GameObject> viewObjects = new List<GameObject>();
    private readonly List<Button> commandButtons = new List<Button>();
    private readonly HashSet<PawPalTrickId> freestyleUsedTricks = new HashSet<PawPalTrickId>();

    private PawPalGameRuntime runtime;
    private PawPalDogState activePet;
    private IntroPetSpecies activeSpecies;
    private PawPalRoomPetHandle roomPet;
    private Camera trialCamera;
    private Canvas canvas;
    private RectTransform screenRoot;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private TextMeshProUGUI feedbackLabel;
    private TextMeshProUGUI currencyLabel;
    private TextMeshProUGUI timerLabel;
    private RectTransform contentRoot;
    private RectTransform commandRoot;
    private PawPalGestureRecognizer gestureRecognizer;
    private PawPalVoiceInputController voiceInput;
    private PawPalObedienceTrialLevelDefinition selectedLevel;
    private PawPalObedienceTrialRun activeRun;
    private PawPalObedienceTrialRoundDefinition activeRound;
    private float roundStartedAt;
    private int currentRoundIndex;
    private int currentSequenceIndex;
    private int currentMistakes;
    private int currentScoreSeed;
    private bool roundActive;
    private bool waitingForAnimation;
    private bool holdInProgress;

    private void Awake()
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, PawPalObedienceTrialSceneFlow.TrialSceneName, StringComparison.OrdinalIgnoreCase))
        {
            Destroy(gameObject);
            return;
        }

        runtime = PawPalGameRuntime.Instance;
        BuildStage();
        ResolveSelectedPet();
        SpawnSelectedPetIfNeeded();
        ResolveRoomPet();
        BuildInputControllers();
        BuildUi();
        ShowLevelSelect();
        PawPalSceneTransitionController.MarkActiveTransitionReady("Obedience trial scene initialized.");
    }

    private void OnDestroy()
    {
        if (gestureRecognizer != null)
        {
            gestureRecognizer.GestureRecognized -= HandleGestureRecognized;
        }

        if (voiceInput != null)
        {
            voiceInput.CommandExecutionRequested -= HandleVoiceCommand;
        }
    }

    private void Update()
    {
        if (!roundActive || activeRound == null)
        {
            return;
        }

        float elapsed = Time.unscaledTime - roundStartedAt;
        float remaining = Mathf.Max(0f, activeRound.TimeLimitSeconds - elapsed);
        if (timerLabel != null)
        {
            timerLabel.text = Mathf.CeilToInt(remaining).ToString();
        }

        if (remaining <= 0f)
        {
            CompleteRound(Mathf.Clamp(currentScoreSeed - currentMistakes * 12, 0, 100), BuildTimeoutFeedback());
        }
    }

    private void BuildStage()
    {
        trialCamera = Camera.main;
        if (trialCamera == null)
        {
            GameObject cameraObject = new GameObject("ObedienceTrialCamera");
            trialCamera = cameraObject.AddComponent<Camera>();
            trialCamera.tag = "MainCamera";
        }

        trialCamera.transform.position = new Vector3(0f, 1.7f, -4f);
        trialCamera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        trialCamera.clearFlags = CameraClearFlags.SolidColor;
        trialCamera.backgroundColor = new Color32(243, 235, 217, 255);
        BuildParkBackdrop();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    private void BuildParkBackdrop()
    {
        PawPalObedienceTrialBackdropController existing = FindFirstObjectByType<PawPalObedienceTrialBackdropController>();
        if (existing != null)
        {
            existing.Configure(PawPalObedienceTrialBackdropController.ResolveSceneBounds(SceneManager.GetActiveScene()));
            return;
        }

        PawPalObedienceTrialBackdropController backdrop = GetComponent<PawPalObedienceTrialBackdropController>();
        if (backdrop == null)
        {
            backdrop = gameObject.AddComponent<PawPalObedienceTrialBackdropController>();
        }

        backdrop.Configure(PawPalObedienceTrialBackdropController.ResolveSceneBounds(SceneManager.GetActiveScene()));
    }

    private void ResolveSelectedPet()
    {
        activePet = runtime != null ? runtime.ActiveDog : null;
        activeSpecies = runtime != null ? runtime.ActivePetSpecies : IntroPetSpecies.Dog;
        if (activePet != null)
        {
            PawPalTrickCatalog.EnsureDogTrickData(activePet);
            PawPalObedienceTrialDatabase.EnsureProgress(activePet);
        }
    }

    private void SpawnSelectedPetIfNeeded()
    {
        if (activePet == null)
        {
            return;
        }

        roomPet = PawPalRoomPetRuntime.ResolvePetById(activePet.Id, activeSpecies);
        if (roomPet != null && roomPet.IsValid)
        {
            PositionPet(roomPet);
            return;
        }

        SelectedPetSessionData selection = BuildSessionData(activePet, activeSpecies);
        if (selection == null || selection.Definition == null)
        {
            Debug.LogWarning("Obedience Trial could not resolve an intro pet definition for the selected pet.");
            return;
        }

        GameObject preferredPrefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        GameObject basePrefab = selection.Definition.BasePrefab;
        GameObject petObject;
        string preferredError;
        if (!PetVariantApplier.TryInstantiatePrefab(preferredPrefab != null ? preferredPrefab : basePrefab, new Vector3(0f, StageY, StagePetZ), Quaternion.Euler(0f, 180f, 0f), null, out petObject, out preferredError))
        {
            if (basePrefab == null || basePrefab == preferredPrefab)
            {
                Debug.LogWarning("Obedience Trial could not spawn selected pet. " + preferredError);
                return;
            }

            string baseError;
            if (!PetVariantApplier.TryInstantiatePrefab(basePrefab, new Vector3(0f, StageY, StagePetZ), Quaternion.Euler(0f, 180f, 0f), null, out petObject, out baseError))
            {
                Debug.LogWarning("Obedience Trial could not spawn selected pet. " + preferredError + " " + baseError);
                return;
            }

            if (selection.FurVariant != null)
            {
                PetVariantApplier.ApplyMaterial(petObject, selection.FurVariant);
            }
        }

        petObject.name = "ObedienceTrialPet_" + selection.SafeName;
        if (selection.Definition.HomeScale != Vector3.zero)
        {
            petObject.transform.localScale = selection.Definition.HomeScale;
        }

        ConfigureSpawnedPet(selection, petObject);
    }

    private void ResolveRoomPet()
    {
        if (roomPet == null || !roomPet.IsValid)
        {
            roomPet = activePet != null
                ? PawPalRoomPetRuntime.ResolvePetById(activePet.Id, activeSpecies)
                : PawPalRoomPetRuntime.ResolveActivePet();
        }

        if (roomPet != null && roomPet.IsValid)
        {
            PositionPet(roomPet);
        }
    }

    private void BuildInputControllers()
    {
        if (roomPet != null && roomPet.IsValid)
        {
            gestureRecognizer = gameObject.AddComponent<PawPalGestureRecognizer>();
            gestureRecognizer.Configure(roomPet.RootTransform, trialCamera, 1f, 1.2f);
            gestureRecognizer.GestureRecognized += HandleGestureRecognized;
            gestureRecognizer.SetActive(true);
        }

        voiceInput = gameObject.AddComponent<PawPalVoiceInputController>();
        voiceInput.Initialize(null);
        voiceInput.CommandExecutionRequested += HandleVoiceCommand;
        voiceInput.SetInteractionTrainingCommandsEnabled(false);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("ObedienceTrialCanvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(393f, 786f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        screenRoot = canvasObject.GetComponent<RectTransform>();
        Image background = canvasObject.AddComponent<Image>();
        background.color = new Color32(250, 245, 234, 210);

        RectTransform top = UiFactory.CreateRect("Header", screenRoot);
        UiFactory.AnchorTopStretch(top, 16f, 16f, 18f, 110f);

        titleLabel = CreateLabel(top, "Title", "Obedience Trial", 24, FontStyles.Bold, TextAlignmentOptions.Left);
        UiFactory.AnchorTopStretch(titleLabel.rectTransform, 0f, 0f, 0f, 34f);

        subtitleLabel = CreateLabel(top, "Subtitle", string.Empty, 14, FontStyles.Normal, TextAlignmentOptions.Left);
        UiFactory.AnchorTopStretch(subtitleLabel.rectTransform, 0f, 0f, 36f, 44f);

        currencyLabel = CreateLabel(top, "Currency", string.Empty, 14, FontStyles.Bold, TextAlignmentOptions.Right);
        currencyLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
        currencyLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        currencyLabel.rectTransform.pivot = new Vector2(1f, 1f);
        currencyLabel.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        currencyLabel.rectTransform.sizeDelta = new Vector2(120f, 24f);

        timerLabel = CreateLabel(top, "Timer", string.Empty, 22, FontStyles.Bold, TextAlignmentOptions.Center);
        timerLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        timerLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        timerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        timerLabel.rectTransform.anchoredPosition = new Vector2(0f, -58f);
        timerLabel.rectTransform.sizeDelta = new Vector2(80f, 30f);

        contentRoot = UiFactory.CreateRect("Content", screenRoot);
        UiFactory.Stretch(contentRoot, 16f, 132f, 16f, 170f);

        commandRoot = UiFactory.CreateRect("Commands", screenRoot);
        UiFactory.AnchorBottomStretch(commandRoot, 16f, 16f, 74f, 92f);

        feedbackLabel = CreateLabel(screenRoot, "Feedback", string.Empty, 14, FontStyles.Normal, TextAlignmentOptions.Center);
        UiFactory.AnchorBottomStretch(feedbackLabel.rectTransform, 16f, 16f, 42f, 30f);

        RectTransform nav = UiFactory.CreateRect("BottomNav", screenRoot);
        UiFactory.AnchorBottomStretch(nav, 16f, 16f, 8f, 32f);
        CreateButton(nav, "BackButton", "Back", delegate
        {
            if (activeRun != null)
            {
                ShowLevelSelect();
            }
            else
            {
                PawPalObedienceTrialSceneFlow.ReturnHome();
            }
        });

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BuildDebugPanel(screenRoot);
#endif
    }

    private void ShowLevelSelect()
    {
        activeRun = null;
        activeRound = null;
        roundActive = false;
        Clear(contentRoot);
        Clear(commandRoot);
        titleLabel.text = "Obedience Trial";
        subtitleLabel.text = activePet != null
            ? activePet.DisplayName + " - " + activeSpecies + " - Buttons ready, gestures and mic optional"
            : "No selected pet found.";
        feedbackLabel.text = string.Empty;
        timerLabel.text = string.Empty;
        UpdateCurrency();

        RectTransform list = UiFactory.CreateRect("LevelList", contentRoot);
        UiFactory.Stretch(list, 0f, 0f, 0f, 0f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        IReadOnlyList<PawPalObedienceTrialLevelDefinition> levels = PawPalObedienceTrialDatabase.Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            CreateLevelRow(list, levels[i]);
        }
    }

    private void CreateLevelRow(RectTransform parent, PawPalObedienceTrialLevelDefinition level)
    {
        RectTransform row = UiFactory.CreateRect(level.DisplayName + "Row", parent);
        UiFactory.EnsureLayoutElement(row.gameObject, -1f, 88f, -1f, 88f);
        Image fill = row.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = level.Playable ? new Color32(255, 253, 248, 255) : new Color32(230, 226, 218, 255);

        string reason;
        bool enterable = PawPalObedienceTrialDatabase.CanEnterLevel(activePet, activeSpecies, level, out reason);
        PawPalObedienceTrialLevelProgress levelProgress = activePet != null
            ? PawPalObedienceTrialDatabase.GetOrCreateLevelProgress(PawPalObedienceTrialDatabase.EnsureProgress(activePet), level.LevelId)
            : null;

        TextMeshProUGUI title = CreateLabel(row, "Title", level.DisplayName, 18, FontStyles.Bold, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.offsetMin = new Vector2(12f, -32f);
        title.rectTransform.offsetMax = new Vector2(-100f, -8f);

        string detail = BuildLevelDetail(level, levelProgress, enterable, reason);
        TextMeshProUGUI info = CreateLabel(row, "Info", detail, 12, FontStyles.Normal, TextAlignmentOptions.Left);
        info.rectTransform.anchorMin = new Vector2(0f, 0f);
        info.rectTransform.anchorMax = new Vector2(1f, 1f);
        info.rectTransform.pivot = new Vector2(0f, 1f);
        info.rectTransform.offsetMin = new Vector2(12f, 10f);
        info.rectTransform.offsetMax = new Vector2(-100f, -34f);
        info.textWrappingMode = TextWrappingModes.Normal;

        Button button = CreateButton(row, "Select", enterable ? "Enter" : "View", delegate
        {
            selectedLevel = level;
            ShowRequirements(level);
        });
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-10f, 0f);
        buttonRect.sizeDelta = new Vector2(78f, 36f);
    }

    private void ShowRequirements(PawPalObedienceTrialLevelDefinition level)
    {
        Clear(contentRoot);
        Clear(commandRoot);
        selectedLevel = level;
        string reason;
        bool canEnter = PawPalObedienceTrialDatabase.CanEnterLevel(activePet, activeSpecies, level, out reason);
        titleLabel.text = level.DisplayName + " Trial";
        subtitleLabel.text = canEnter ? "Ready to enter" : reason;
        timerLabel.text = string.Empty;
        UpdateCurrency();

        RectTransform panel = CreatePanel(contentRoot, "RequirementsPanel");
        AddParagraph(panel, "Entry fee: " + level.Rewards.EntryFee + " basic currency");
        AddParagraph(panel, "Required: " + BuildTrickList(level.RequiredTricks));
        AddParagraph(panel, "Readiness: mood, energy, and care at " + Mathf.RoundToInt(level.MinimumNeedAverage01 * 100f) + "%+");
        AddParagraph(panel, level.Playable ? "Rounds: single command, hold, sequence, freestyle" : level.LockedReason);

        if (!canEnter && !string.IsNullOrWhiteSpace(reason))
        {
            AddParagraph(panel, reason);
        }

        RectTransform buttons = UiFactory.CreateRect("RequirementButtons", commandRoot);
        UiFactory.Stretch(buttons, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 8f;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = true;

        CreateButton(buttons, "Levels", "Levels", ShowLevelSelect);
        Button enterButton = CreateButton(buttons, "Start", "Start", delegate { TryStartTrial(level); });
        enterButton.interactable = canEnter;
    }

    private void TryStartTrial(PawPalObedienceTrialLevelDefinition level)
    {
        string reason;
        if (!PawPalObedienceTrialDatabase.CanEnterLevel(activePet, activeSpecies, level, out reason))
        {
            feedbackLabel.text = reason;
            return;
        }

        if (runtime == null || !runtime.TrySpendBasicCurrency(level.Rewards.EntryFee))
        {
            feedbackLabel.text = "Not enough basic currency for the entry fee.";
            UpdateCurrency();
            return;
        }

        activeRun = new PawPalObedienceTrialRun
        {
            Pet = activePet,
            Species = activeSpecies,
            Level = level
        };
        currentRoundIndex = 0;
        UpdateCurrency();
        StartRound();
    }

    private void StartRound()
    {
        if (activeRun == null || activeRun.Level == null || currentRoundIndex >= activeRun.Level.Rounds.Count)
        {
            CompleteTrial();
            return;
        }

        activeRound = activeRun.Level.Rounds[currentRoundIndex];
        currentMistakes = 0;
        currentSequenceIndex = 0;
        currentScoreSeed = 0;
        roundStartedAt = Time.unscaledTime;
        holdInProgress = false;
        freestyleUsedTricks.Clear();
        roundActive = true;
        Clear(contentRoot);
        Clear(commandRoot);

        titleLabel.text = activeRound.DisplayName;
        subtitleLabel.text = BuildRoundPrompt(activeRound);
        feedbackLabel.text = "Use the buttons, a learned gesture, or the mic.";
        timerLabel.text = Mathf.CeilToInt(activeRound.TimeLimitSeconds).ToString();

        BuildRoundCommandButtons();
    }

    private void BuildRoundCommandButtons()
    {
        commandButtons.Clear();
        RectTransform row = UiFactory.CreateRect("CommandButtons", commandRoot);
        UiFactory.Stretch(row, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        List<PawPalTrickId> tricks = activeRound.RoundType == PawPalObedienceTrialRoundType.Freestyle
            ? PawPalObedienceTrialDatabase.GetLearnedFreestyleTricks(activePet, activeSpecies, activeRun.Level)
            : new List<PawPalTrickId>(activeRun.Level.RequiredTricks);

        for (int i = 0; i < tricks.Count; i++)
        {
            PawPalTrickId trick = tricks[i];
            Button button = CreateButton(row, trick.ToString(), FirstUpper(PawPalObedienceTrialDatabase.GetTrickLabel(trick)), delegate
            {
                HandleCommand(trick, PawPalObedienceTrialInputSource.Button);
            });
            commandButtons.Add(button);
        }

        Button micButton = CreateButton(row, "Mic", "Mic", delegate
        {
            if (voiceInput != null)
            {
                voiceInput.ToggleMic();
                feedbackLabel.text = voiceInput.IsMicEnabled ? "Mic shortcut on." : "Mic shortcut off.";
            }
        });
        commandButtons.Add(micButton);
    }

    private void HandleCommand(PawPalTrickId trickId, PawPalObedienceTrialInputSource source)
    {
        if (!roundActive || activeRound == null || waitingForAnimation)
        {
            return;
        }

        if (!PawPalObedienceTrialDatabase.IsFreestyleEligible(activeSpecies, trickId))
        {
            RegisterMistake(PawPalObedienceTrialDatabase.GetMappingIssue(activeSpecies, trickId));
            return;
        }

        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(activePet, trickId);
        if (progress == null || !progress.IsLearned)
        {
            RegisterMistake("That trick has not been learned yet.");
            return;
        }

        if (roomPet == null || !roomPet.IsValid || !roomPet.CanPerformTrainingAnimation())
        {
            RegisterMistake("Your pet is not ready to perform.");
            return;
        }

        StartCoroutine(PlayCommandAndScore(trickId, source));
    }

    private IEnumerator PlayCommandAndScore(PawPalTrickId trickId, PawPalObedienceTrialInputSource source)
    {
        waitingForAnimation = true;
        yield return PlayTrick(trickId);
        waitingForAnimation = false;

        if (!roundActive || activeRound == null)
        {
            yield break;
        }

        switch (activeRound.RoundType)
        {
            case PawPalObedienceTrialRoundType.SingleCommand:
                ScoreSingleCommand(trickId);
                break;
            case PawPalObedienceTrialRoundType.Hold:
                ScoreHoldCommand(trickId);
                break;
            case PawPalObedienceTrialRoundType.Sequence:
                ScoreSequenceCommand(trickId);
                break;
            case PawPalObedienceTrialRoundType.Freestyle:
                ScoreFreestyleCommand(trickId);
                break;
        }
    }

    private IEnumerator PlayTrick(PawPalTrickId trickId)
    {
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        if (definition != null && roomPet != null)
        {
            yield return roomPet.PlayTrainingTrick(definition, false, trialCamera);
        }
    }

    private void ScoreSingleCommand(PawPalTrickId trickId)
    {
        PawPalTrickId expected = activeRound.RequestedTricks.Count > 0 ? activeRound.RequestedTricks[0] : PawPalTrickId.Sit;
        if (trickId != expected)
        {
            RegisterMistake("Wrong command. The judge asked for " + PawPalObedienceTrialDatabase.GetTrickLabel(expected) + ".");
            return;
        }

        int score = Mathf.Clamp(76 + SpeedBonus() + MasteryBonus(trickId) - currentMistakes * 12, 0, 100);
        CompleteRound(score, "Clean response to " + PawPalObedienceTrialDatabase.GetTrickLabel(trickId) + ".");
    }

    private void ScoreHoldCommand(PawPalTrickId trickId)
    {
        PawPalTrickId expected = activeRound.RequestedTricks.Count > 0 ? activeRound.RequestedTricks[0] : PawPalTrickId.Sit;
        if (trickId != expected)
        {
            RegisterMistake("Wrong hold command. The judge asked for " + PawPalObedienceTrialDatabase.GetTrickLabel(expected) + ".");
            return;
        }

        if (!holdInProgress)
        {
            holdInProgress = true;
            feedbackLabel.text = "Hold...";
            StartCoroutine(CompleteHoldAfterDelay(trickId));
        }
    }

    private IEnumerator CompleteHoldAfterDelay(PawPalTrickId trickId)
    {
        float holdUntil = Time.unscaledTime + activeRound.HoldDurationSeconds;
        while (roundActive && Time.unscaledTime < holdUntil)
        {
            yield return null;
        }

        if (!roundActive)
        {
            yield break;
        }

        int score = Mathf.Clamp(72 + ConditionBonus() + MasteryBonus(trickId) - currentMistakes * 12, 0, 100);
        CompleteRound(score, "Held for " + activeRound.HoldDurationSeconds.ToString("0.0") + " seconds.");
    }

    private void ScoreSequenceCommand(PawPalTrickId trickId)
    {
        if (currentSequenceIndex >= activeRound.RequestedTricks.Count)
        {
            return;
        }

        PawPalTrickId expected = activeRound.RequestedTricks[currentSequenceIndex];
        if (trickId != expected)
        {
            RegisterMistake("Sequence break. Next command was " + PawPalObedienceTrialDatabase.GetTrickLabel(expected) + ".");
            return;
        }

        currentSequenceIndex++;
        currentScoreSeed += 28 + MasteryBonus(trickId);
        if (currentSequenceIndex >= activeRound.RequestedTricks.Count)
        {
            CompleteRound(Mathf.Clamp(62 + currentScoreSeed / 2 + SpeedBonus() - currentMistakes * 10, 0, 100), "Sequence completed in order.");
        }
        else
        {
            feedbackLabel.text = "Good. Next: " + PawPalObedienceTrialDatabase.GetTrickLabel(activeRound.RequestedTricks[currentSequenceIndex]) + ".";
        }
    }

    private void ScoreFreestyleCommand(PawPalTrickId trickId)
    {
        if (freestyleUsedTricks.Contains(trickId))
        {
            currentMistakes++;
            currentScoreSeed = Mathf.Max(0, currentScoreSeed - 8);
            feedbackLabel.text = "Repetition penalty.";
        }
        else
        {
            freestyleUsedTricks.Add(trickId);
            currentScoreSeed += 28 + MasteryBonus(trickId);
            feedbackLabel.text = "Freestyle trick counted.";
        }

        if (freestyleUsedTricks.Count >= 3)
        {
            CompleteRound(Mathf.Clamp(58 + currentScoreSeed + ConditionBonus() - currentMistakes * 8, 0, 100), "Freestyle set completed.");
        }
    }

    private void RegisterMistake(string message)
    {
        currentMistakes++;
        currentScoreSeed = Mathf.Max(0, currentScoreSeed - 10);
        feedbackLabel.text = message;
    }

    private void CompleteRound(int score, string feedback)
    {
        if (!roundActive)
        {
            return;
        }

        roundActive = false;
        activeRun.RoundResults.Add(new PawPalObedienceTrialRoundResult
        {
            Definition = activeRound,
            Score = Mathf.Clamp(score, 0, 100),
            Mistakes = currentMistakes,
            Feedback = feedback
        });

        ShowRoundResult(activeRun.RoundResults[activeRun.RoundResults.Count - 1]);
    }

    private void ShowRoundResult(PawPalObedienceTrialRoundResult result)
    {
        Clear(contentRoot);
        Clear(commandRoot);
        titleLabel.text = result.Definition.DisplayName + " Result";
        subtitleLabel.text = result.Score + "/100";
        timerLabel.text = string.Empty;
        feedbackLabel.text = result.Feedback;

        RectTransform panel = CreatePanel(contentRoot, "RoundResultPanel");
        AddParagraph(panel, "Score: " + result.Score + "/100");
        AddParagraph(panel, "Mistakes: " + result.Mistakes);
        AddParagraph(panel, result.Feedback);

        CreateButton(commandRoot, "NextRound", currentRoundIndex + 1 >= activeRun.Level.Rounds.Count ? "Summary" : "Next", delegate
        {
            currentRoundIndex++;
            StartRound();
        });
    }

    private void CompleteTrial()
    {
        if (activeRun == null)
        {
            ShowLevelSelect();
            return;
        }

        activeRun.FinalScore = PawPalObedienceTrialDatabase.CalculateFinalScore(activeRun.RoundResults);
        activeRun.Medal = PawPalObedienceTrialDatabase.GetMedal(activeRun.FinalScore);
        activeRun.RewardId = PawPalObedienceTrialDatabase.BuildRewardId(activePet, activeRun.Level, activeRun.Medal);

        bool unlockedNext;
        PawPalObedienceTrialDatabase.ApplyCompletedRun(activeRun, out unlockedNext);

        int payout = 0;
        PawPalObedienceTrialProgress progress = PawPalObedienceTrialDatabase.EnsureProgress(activePet);
        if (activeRun.Medal != PawPalObedienceTrialMedal.None && progress != null && !progress.GrantedRewardIds.Contains(activeRun.RewardId))
        {
            payout = PawPalObedienceTrialDatabase.GetRewardCurrency(activeRun.Level, activeRun.Medal);
            if (payout > 0 && runtime != null)
            {
                runtime.TrainerState.BasicCurrency += payout;
                runtime.TrainerState.TotalBasicCurrencyEarned += payout;
            }

            progress.GrantedRewardIds.Add(activeRun.RewardId);
        }

        if (runtime != null)
        {
            runtime.RecordObedienceTrialResult(activeRun.Medal);
            runtime.CommitObedienceTrialProgress();
        }

        ShowFinalSummary(payout, unlockedNext);
    }

    private void ShowFinalSummary(int payout, bool unlockedNext)
    {
        Clear(contentRoot);
        Clear(commandRoot);
        titleLabel.text = "Final Summary";
        subtitleLabel.text = activeRun.FinalScore + "/100 - " + activeRun.Medal;
        timerLabel.text = string.Empty;
        feedbackLabel.text = unlockedNext ? "Next playable level unlocked." : string.Empty;
        UpdateCurrency();

        RectTransform panel = CreatePanel(contentRoot, "FinalSummaryPanel");
        AddParagraph(panel, "Medal: " + activeRun.Medal);
        AddParagraph(panel, "Score: " + activeRun.FinalScore + "/100");
        AddParagraph(panel, "Reward: " + payout + " basic currency");
        AddParagraph(panel, unlockedNext ? "Unlocked the next playable obedience level." : "Bronze grants rewards; Silver or Gold unlocks the next playable level.");

        RectTransform row = UiFactory.CreateRect("SummaryButtons", commandRoot);
        UiFactory.Stretch(row, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        CreateButton(row, "Levels", "Levels", ShowLevelSelect);
        CreateButton(row, "Home", "Home", delegate { PawPalObedienceTrialSceneFlow.ReturnHome(); });
    }

    private void HandleGestureRecognized(PawPalGestureSnapshot snapshot)
    {
        if (!roundActive || activeRound == null)
        {
            return;
        }

        PawPalTrickId selected = activeRound.RequestedTricks.Count > currentSequenceIndex
            ? activeRound.RequestedTricks[currentSequenceIndex]
            : PawPalTrickId.Sit;
        PawPalTrickId trick = PawPalTrickGestureMapper.MapGestureToTrick(snapshot, selected, false);
        HandleCommand(trick, PawPalObedienceTrialInputSource.Gesture);
    }

    private PawPalVoiceCommandExecutionResult HandleVoiceCommand(PawPalResolvedVoiceCommand command)
    {
        if (command == null)
        {
            return PawPalVoiceCommandExecutionResult.HandledResult(false, "No trial command recognized.");
        }

        HandleCommand(command.TrickId, PawPalObedienceTrialInputSource.Voice);
        return PawPalVoiceCommandExecutionResult.HandledResult(true, "Trial voice shortcut received.");
    }

    private int SpeedBonus()
    {
        if (activeRound == null)
        {
            return 0;
        }

        float elapsed = Time.unscaledTime - roundStartedAt;
        float remaining01 = Mathf.Clamp01(1f - elapsed / Mathf.Max(1f, activeRound.TimeLimitSeconds));
        return Mathf.RoundToInt(remaining01 * 12f);
    }

    private int ConditionBonus()
    {
        if (activePet == null)
        {
            return 0;
        }

        float care = (activePet.Food01 + activePet.Water01 + activePet.Hygiene01 + activePet.Activity01 + activePet.Energy01 + activePet.Mood01) / 6f;
        return Mathf.RoundToInt(Mathf.Clamp01(care) * 12f);
    }

    private int MasteryBonus(PawPalTrickId trickId)
    {
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(activePet, trickId);
        return progress != null ? Mathf.Clamp(progress.MasteryLevel * 4, 0, 8) : 0;
    }

    private string BuildRoundPrompt(PawPalObedienceTrialRoundDefinition round)
    {
        if (round == null)
        {
            return string.Empty;
        }

        switch (round.RoundType)
        {
            case PawPalObedienceTrialRoundType.SingleCommand:
                return "Command: " + BuildTrickList(round.RequestedTricks);
            case PawPalObedienceTrialRoundType.Hold:
                return "Hold " + BuildTrickList(round.RequestedTricks) + " for " + round.HoldDurationSeconds.ToString("0.0") + " seconds";
            case PawPalObedienceTrialRoundType.Sequence:
                return "Sequence: " + BuildTrickList(round.RequestedTricks);
            case PawPalObedienceTrialRoundType.Freestyle:
                return "Perform learned eligible tricks without repeating.";
            default:
                return string.Empty;
        }
    }

    private string BuildTimeoutFeedback()
    {
        if (activeRound != null && activeRound.RoundType == PawPalObedienceTrialRoundType.Freestyle && freestyleUsedTricks.Count > 0)
        {
            return "Freestyle time ended.";
        }

        return "Time expired.";
    }

    private string BuildLevelDetail(PawPalObedienceTrialLevelDefinition level, PawPalObedienceTrialLevelProgress progress, bool enterable, string reason)
    {
        string best = progress != null && progress.BestMedal != PawPalObedienceTrialMedal.None
            ? "Best " + progress.BestMedal + " " + progress.BestScore
            : "No medal yet";
        if (enterable)
        {
            return best + " - Fee " + level.Rewards.EntryFee;
        }

        return best + " - " + reason;
    }

    private string BuildTrickList(List<PawPalTrickId> tricks)
    {
        if (tricks == null || tricks.Count == 0)
        {
            return "none";
        }

        List<string> labels = new List<string>();
        for (int i = 0; i < tricks.Count; i++)
        {
            labels.Add(PawPalObedienceTrialDatabase.GetTrickLabel(tricks[i]));
        }

        return string.Join(", ", labels.ToArray());
    }

    private SelectedPetSessionData BuildSessionData(PawPalDogState pet, IntroPetSpecies species)
    {
        IntroPetDefinition definition = ResolveIntroPetDefinition(pet, species);
        if (definition == null)
        {
            return null;
        }

        return new SelectedPetSessionData
        {
            Definition = definition,
            FurVariant = definition.GetDefaultFurVariant(),
            FurIndex = 0,
            Gender = pet.Gender,
            Personality = pet.Personality,
            PetName = pet.DisplayName,
            RuntimePetId = pet.Id
        };
    }

    private IntroPetDefinition ResolveIntroPetDefinition(PawPalDogState pet, IntroPetSpecies species)
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        IntroPetDefinition fallback = null;
        string breed = pet != null && !string.IsNullOrWhiteSpace(pet.Breed) ? Normalize(pet.Breed) : string.Empty;
        string id = pet != null && !string.IsNullOrWhiteSpace(pet.Id) ? Normalize(pet.Id) : string.Empty;
        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || definition.Species != species)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = definition;
            }

            string definitionId = Normalize(definition.PetId);
            string breedName = Normalize(definition.BreedName);
            string displayName = Normalize(definition.DisplayName);
            if ((!string.IsNullOrEmpty(breed) && (breedName.Contains(breed) || breed.Contains(breedName) || displayName.Contains(breed)))
                || (!string.IsNullOrEmpty(id) && definitionId.Contains(id)))
            {
                return definition;
            }
        }

        return fallback;
    }

    private void ConfigureSpawnedPet(SelectedPetSessionData selection, GameObject petObject)
    {
        if (selection == null || petObject == null)
        {
            return;
        }

        Bounds bounds = new Bounds(Vector3.zero, new Vector3(4f, 2f, 4f));
        DogRoomAgent dog = petObject.GetComponent<DogRoomAgent>();
        if (dog == null)
        {
            dog = petObject.GetComponentInChildren<DogRoomAgent>(true);
        }

        PawPalCatRoomAgent cat = petObject.GetComponent<PawPalCatRoomAgent>();
        if (cat == null)
        {
            cat = petObject.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        if (selection.Species == IntroPetSpecies.Dog && dog != null)
        {
            dog.SetRuntimeDogId(selection.RuntimePetId);
            dog.ConfigureRoomBounds(bounds);
            dog.ApplySelectedPetPresentation(selection);
            dog.ConfigureSelectedPetRuntime(selection);
            roomPet = new PawPalRoomPetHandle(dog);
        }
        else
        {
            if (dog != null)
            {
                Destroy(dog);
            }

            if (cat == null)
            {
                cat = petObject.AddComponent<PawPalCatRoomAgent>();
            }

            cat.Initialize(selection);
            cat.ConfigureRoomBounds(bounds);
            roomPet = new PawPalRoomPetHandle(cat);
        }
    }

    private void PositionPet(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid || pet.RootTransform == null)
        {
            return;
        }

        pet.RootTransform.position = new Vector3(0f, StageY, StagePetZ);
        pet.RootTransform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private RectTransform CreatePanel(RectTransform parent, string name)
    {
        RectTransform panel = UiFactory.CreateRect(name, parent);
        UiFactory.Stretch(panel, 0f, 0f, 0f, 0f);
        Image image = panel.gameObject.AddComponent<Image>();
        image.sprite = UiTheme.RoundedTenSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color32(255, 253, 248, 245);
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private void AddParagraph(RectTransform parent, string text)
    {
        TextMeshProUGUI label = CreateLabel(parent, "Text", text, 14, FontStyles.Normal, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.EnsureLayoutElement(label.gameObject, -1f, 34f, -1f, 34f);
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int size, FontStyles style, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, size, UiTheme.BodyText, style, alignment);
        label.font = style == FontStyles.Bold ? UiTheme.NavExtraBoldFont : UiTheme.NavRegularFont;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Button CreateButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction action)
    {
        RectTransform buttonRect = UiFactory.CreateRect(name, parent);
        UiFactory.EnsureLayoutElement(buttonRect.gameObject, 82f, 38f, 82f, 38f);
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.sprite = UiTheme.RoundedFiveSprite;
        image.type = Image.Type.Sliced;
        image.color = UiTheme.NavBrand;
        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        TextMeshProUGUI label = CreateLabel(buttonRect, "Label", text, 13, FontStyles.Bold, TextAlignmentOptions.Center);
        label.color = Color.white;
        UiFactory.Stretch(label.rectTransform, 6f, 3f, 6f, 3f);
        return button;
    }

    private void Clear(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void UpdateCurrency()
    {
        currencyLabel.text = runtime != null ? runtime.GetBasicCurrencyText() : string.Empty;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
    }

    private static string FirstUpper(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BuildDebugPanel(RectTransform root)
    {
        RectTransform panel = UiFactory.CreateRect("TrialDebugPanel", root);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-8f, 8f);
        panel.sizeDelta = new Vector2(148f, 172f);
        Image image = panel.gameObject.AddComponent<Image>();
        image.sprite = UiTheme.RoundedFiveSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color32(35, 35, 35, 210);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 4f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddDebugButton(panel, "Learn V1", delegate
        {
            SetDebugTrickReady(PawPalTrickId.Sit);
            SetDebugTrickReady(PawPalTrickId.Lie);
            SetDebugTrickReady(PawPalTrickId.Jump);
            runtime.CommitObedienceTrialProgress();
            ShowLevelSelect();
        });
        AddDebugButton(panel, "Ready Pet", delegate
        {
            activePet.Food01 = 1f;
            activePet.Water01 = 1f;
            activePet.Hygiene01 = 1f;
            activePet.Activity01 = 1f;
            activePet.Energy01 = 1f;
            activePet.Mood01 = 1f;
            activePet.Focus = Mathf.Max(activePet.Focus, 3);
            runtime.CommitObedienceTrialProgress();
            ShowLevelSelect();
        });
        AddDebugButton(panel, "Unlock Amateur", delegate
        {
            PawPalObedienceTrialDatabase.EnsureProgress(activePet).HighestUnlockedLevel = PawPalObedienceTrialLevelId.Amateur.ToString();
            runtime.CommitObedienceTrialProgress();
            ShowLevelSelect();
        });
        AddDebugButton(panel, "Log Mappings", LogMappings);
        AddDebugButton(panel, "Reset Trial", delegate
        {
            activePet.ObedienceTrialProgress = new PawPalObedienceTrialProgress();
            runtime.CommitObedienceTrialProgress();
            ShowLevelSelect();
        });
    }

    private void AddDebugButton(RectTransform parent, string text, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, text.Replace(" ", string.Empty), text, action);
        UiFactory.EnsureLayoutElement(button.gameObject, -1f, 25f, -1f, 25f);
    }

    private void SetDebugTrickReady(PawPalTrickId trickId)
    {
        if (activePet == null)
        {
            return;
        }

        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(activePet, trickId);
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        progress.IsDiscovered = true;
        progress.IsLearned = true;
        progress.MasteryLevel = 2;
        progress.MasteryXp = definition != null ? Mathf.Max(definition.MasteryRequiredXp, 120f) : 120f;
    }

    private void LogMappings()
    {
        List<PawPalObedienceTrialAnimationMapping> mappings = PawPalObedienceTrialDatabase.Config.AnimationMappings;
        for (int i = 0; i < mappings.Count; i++)
        {
            PawPalObedienceTrialAnimationMapping mapping = mappings[i];
            Debug.Log("Obedience mapping " + mapping.Species + " " + mapping.TrickId + " trial=" + mapping.TrialEligible + " freestyle=" + mapping.OptionalFreestyleEligible + " " + mapping.MissingReason);
        }
    }
#endif
}
