using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IntroPetSelectionController : MonoBehaviour
{
    private const int IntroDogSpawnCount = 3;
    private const int IntroCatSpawnCount = 2;

    private readonly List<IntroPetRuntimeSelection> selections = new List<IntroPetRuntimeSelection>();
    private IntroPetSpawner spawner;
    private DogCycleCamera cameraController;
    private Camera controlledCamera;
    private IntroPetUIController uiController;
    private IntroPetPreviewVocalDirector previewVocalDirector;
    private IntroPetInteractionOverlayController interactionOverlayController;
    private int currentIndex;
    private bool initialized;
    private IntroPetUiState uiState = IntroPetUiState.Selection;

    public IntroPetAgent CurrentAgent
    {
        get
        {
            if (spawner == null || spawner.Agents.Count == 0)
            {
                return null;
            }

            return spawner.Agents[Mathf.Clamp(currentIndex, 0, spawner.Agents.Count - 1)];
        }
    }

    public void Initialize(Bounds fieldBounds, Camera sceneCamera)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        EnsureEventSystem();
        controlledCamera = sceneCamera != null ? sceneCamera : Camera.main;
        GameObject cameraObject = controlledCamera != null ? controlledCamera.gameObject : new GameObject("IntroCamera");
        if (controlledCamera == null)
        {
            controlledCamera = cameraObject.AddComponent<Camera>();
            controlledCamera.tag = "MainCamera";
        }

        cameraController = cameraObject.GetComponent<DogCycleCamera>();
        if (cameraController == null)
        {
            cameraController = cameraObject.AddComponent<DogCycleCamera>();
        }

        spawner = gameObject.AddComponent<IntroPetSpawner>();
        spawner.Initialize(this, fieldBounds);
        BuildSelections(BuildSpawnRoster(spawner.LoadDefinitions()));
        ApplySpawnResults(spawner.Spawn(selections));
        RegisterCameraRoster();

        uiController = new GameObject("IntroPetSelectionCanvas", typeof(RectTransform)).AddComponent<IntroPetUIController>();
        uiController.Build();
        BindUi();
        InitializePreviewSystems();
        RefreshUi();

        if (spawner.Agents.Count > 0)
        {
            SelectPet(0, false);
        }
        else if (selections.Count > 0)
        {
            Debug.LogError("IntroPetSelection failed to spawn any pet agents for the intro roster.");
        }
    }

    public void SelectAgent(IntroPetAgent agent)
    {
        if (agent == null || uiState != IntroPetUiState.Selection || IsPreviewInteractionActive())
        {
            return;
        }

        SelectPet(agent.SelectionIndex, false);
    }

    private void Update()
    {
        if (!initialized || uiState != IntroPetUiState.Selection || IsPreviewInteractionActive())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            StepPet(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            StepPet(1);
        }
    }

    private void BuildSelections(IntroPetDefinition[] sourceDefinitions)
    {
        selections.Clear();
        if (sourceDefinitions == null)
        {
            return;
        }

        HashSet<string> usedNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourceDefinitions.Length; i++)
        {
            IntroPetDefinition definition = sourceDefinitions[i];
            string generatedName = IntroPetRandomNameGenerator.GenerateName(definition, usedNames);
            selections.Add(IntroPetRuntimeSelection.Create(definition, i, generatedName));
        }
    }

    private static IntroPetDefinition[] BuildSpawnRoster(IntroPetDefinition[] sourceDefinitions)
    {
        if (sourceDefinitions == null || sourceDefinitions.Length == 0)
        {
            return new IntroPetDefinition[0];
        }

        List<IntroPetDefinition> dogs = new List<IntroPetDefinition>();
        List<IntroPetDefinition> cats = new List<IntroPetDefinition>();
        for (int i = 0; i < sourceDefinitions.Length; i++)
        {
            IntroPetDefinition definition = sourceDefinitions[i];
            if (definition == null)
            {
                continue;
            }

            if (definition.Species == IntroPetSpecies.Cat)
            {
                cats.Add(definition);
            }
            else
            {
                dogs.Add(definition);
            }
        }

        Shuffle(dogs);
        Shuffle(cats);

        List<IntroPetDefinition> roster = new List<IntroPetDefinition>();
        AddSpeciesRoster(roster, dogs, IntroDogSpawnCount, "dog");
        AddSpeciesRoster(roster, cats, IntroCatSpawnCount, "cat");
        return roster.ToArray();
    }

    private static void AddSpeciesRoster(List<IntroPetDefinition> roster, List<IntroPetDefinition> source, int count, string speciesLabel)
    {
        if (source == null)
        {
            return;
        }

        int targetCount = Mathf.Min(count, source.Count);
        for (int i = 0; i < targetCount; i++)
        {
            roster.Add(source[i]);
        }

        if (source.Count < count)
        {
            Debug.LogWarning("IntroPetSelection only found " + source.Count + " " + speciesLabel + " definition(s). Spawning all available unique breeds.");
        }
    }

    private static void Shuffle(List<IntroPetDefinition> definitionsToShuffle)
    {
        if (definitionsToShuffle == null)
        {
            return;
        }

        for (int i = definitionsToShuffle.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            IntroPetDefinition temp = definitionsToShuffle[i];
            definitionsToShuffle[i] = definitionsToShuffle[swapIndex];
            definitionsToShuffle[swapIndex] = temp;
        }
    }

    private void BindUi()
    {
        if (uiController == null)
        {
            return;
        }

        uiController.PetStepRequested += StepPet;
        uiController.FurIndexRequested += SetFurIndex;
        uiController.GenderChanged += SetGender;
        uiController.NameChanged += SetName;
        uiController.ContinueRequested += ShowCustomizeCard;
        uiController.BackRequested += ShowSelectionCard;
        uiController.ConfirmAccepted += ConfirmSelection;
        uiController.WhistleRequested += EnterPreviewInteraction;
    }

    private void StepPet(int direction)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Selection || IsPreviewInteractionActive())
        {
            return;
        }

        int nextIndex = ((currentIndex + direction) % selections.Count + selections.Count) % selections.Count;
        SelectPet(nextIndex, false);
    }

    private void SelectPet(int index, bool snapCamera)
    {
        if (selections.Count == 0)
        {
            Debug.LogWarning("IntroPetSelection has no pet definitions to display. Add IntroPetDefinition assets under Resources/PawPal/IntroPets/Definitions.");
            return;
        }

        if (spawner == null || spawner.Agents.Count == 0)
        {
            currentIndex = Mathf.Clamp(index, 0, selections.Count - 1);
            RefreshUi();
            return;
        }

        IntroPetAgent previous = CurrentAgent;
        if (previous != null)
        {
            previous.SetSelected(false, controlledCamera);
        }

        currentIndex = Mathf.Clamp(index, 0, selections.Count - 1);
        IntroPetAgent current = CurrentAgent;
        if (current != null)
        {
            current.SetSelected(true, controlledCamera);
            DogSocialDirector.SetPreferredInteractionDog(current.RuntimeDogAgent);
            FocusCameraOnAgent(current, snapCamera);
        }

        RefreshUi();
    }

    private void SetFurIndex(int furIndex)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Customize)
        {
            return;
        }

        IntroPetRuntimeSelection selection = selections[currentIndex];
        if (selection.Definition == null || selection.Definition.FurVariants == null || furIndex >= selection.Definition.FurVariants.Length)
        {
            return;
        }

        selection.SetFurIndex(furIndex);
        ReplaceCurrentAgentVariant(selection);
        RefreshUi();
    }

    private void ReplaceCurrentAgentVariant(IntroPetRuntimeSelection selection)
    {
        if (spawner == null || CurrentAgent == null)
        {
            return;
        }

        IntroPetAgent replacement = spawner.ReplaceAgentVariant(CurrentAgent, selection);
        if (replacement != null)
        {
            currentIndex = replacement.SelectionIndex;
            RegisterCameraRoster();
            RefreshPreviewRoster();
            replacement.SetSelected(true, controlledCamera);
            FocusCameraOnAgent(replacement, false);
        }
    }

    private void SetGender(PawPalDogGender gender)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Customize)
        {
            return;
        }

        selections[currentIndex].SetGender(gender);
        RefreshUi();
    }

    private void SetName(string name)
    {
        if (selections.Count == 0)
        {
            return;
        }

        selections[currentIndex].SetName(name);
        RefreshUi();
    }

    private void ShowCustomizeCard()
    {
        if (uiController == null || selections.Count == 0)
        {
            return;
        }

        if (IsPreviewInteractionActive() && interactionOverlayController != null)
        {
            interactionOverlayController.ExitInteraction();
        }

        uiState = IntroPetUiState.Customize;
        RefreshUi();
    }

    private void ShowSelectionCard()
    {
        if (uiController == null)
        {
            return;
        }

        uiState = IntroPetUiState.Selection;
        RefreshUi();
    }

    private void ConfirmSelection()
    {
        if (selections.Count == 0)
        {
            return;
        }

        IntroPetRuntimeSelection selection = selections[currentIndex];
        selection.SetName(selection.PetName);
        SelectedPetSessionContext.SetPendingSelection(selection);
        PawPalIntroSceneFlow.LoadHomeScene();
    }

    private void RefreshUi()
    {
        if (uiController == null || selections.Count == 0)
        {
            return;
        }

        uiController.SetState(uiState);
        uiController.Refresh(selections[currentIndex], currentIndex, selections.Count);
    }

    private void ApplySpawnResults(IReadOnlyList<IntroPetSpawnOutcome> results)
    {
        if (results == null)
        {
            selections.Clear();
            return;
        }

        List<IntroPetRuntimeSelection> usableSelections = new List<IntroPetRuntimeSelection>();
        for (int i = 0; i < results.Count; i++)
        {
            IntroPetSpawnOutcome result = results[i];
            if (result != null && result.IsUsable)
            {
                usableSelections.Add(result.Selection);
            }
        }

        selections.Clear();
        selections.AddRange(usableSelections);
        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, selections.Count - 1));
    }

    private void RegisterCameraRoster()
    {
        if (cameraController == null || spawner == null)
        {
            return;
        }

        cameraController.ConfigureIntroSelectionMode(spawner.GetOrderedDogAgents(), false, true);
    }

    private void FocusCameraOnAgent(IntroPetAgent agent, bool snapCamera)
    {
        if (cameraController == null || agent == null || agent.RuntimeDogAgent == null)
        {
            return;
        }

        cameraController.FocusIntroSelectionDog(agent.RuntimeDogAgent, snapCamera);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void InitializePreviewSystems()
    {
        previewVocalDirector = gameObject.GetComponent<IntroPetPreviewVocalDirector>();
        if (previewVocalDirector == null)
        {
            previewVocalDirector = gameObject.AddComponent<IntroPetPreviewVocalDirector>();
        }

        RefreshPreviewRoster();

        interactionOverlayController = gameObject.GetComponent<IntroPetInteractionOverlayController>();
        if (interactionOverlayController == null)
        {
            interactionOverlayController = gameObject.AddComponent<IntroPetInteractionOverlayController>();
        }

        interactionOverlayController.Initialize(this, uiController, previewVocalDirector);
    }

    private void RefreshPreviewRoster()
    {
        if (previewVocalDirector != null && spawner != null)
        {
            previewVocalDirector.SetAgents(spawner.Agents);
        }
    }

    private void EnterPreviewInteraction()
    {
        if (uiState != IntroPetUiState.Selection || interactionOverlayController == null)
        {
            return;
        }

        IntroPetAgent agent = CurrentAgent;
        if (agent == null)
        {
            return;
        }

        interactionOverlayController.TryEnterPreviewInteraction(agent.RoomPet);
    }

    private bool IsPreviewInteractionActive()
    {
        return interactionOverlayController != null && interactionOverlayController.IsInteractionActive;
    }
}

[DisallowMultipleComponent]
public sealed class IntroPetPreviewVocalDirector : MonoBehaviour
{
    private const float RetryDelayWhenBlocked = 2.2f;
    private const float MinimumDelayBetweenPreviewVocals = 7f;
    private const float MaximumDelayBetweenPreviewVocals = 11.5f;

    private readonly List<PawPalRoomPetHandle> pets = new List<PawPalRoomPetHandle>();
    private bool interactionSuspended;
    private float nextAttemptTime;

    public void SetAgents(IReadOnlyList<IntroPetAgent> agents)
    {
        pets.Clear();
        if (agents == null)
        {
            return;
        }

        for (int i = 0; i < agents.Count; i++)
        {
            IntroPetAgent agent = agents[i];
            if (agent == null || agent.RoomPet == null || !agent.RoomPet.IsValid)
            {
                continue;
            }

            pets.Add(agent.RoomPet);
        }

        nextAttemptTime = Time.time + Random.Range(1.2f, 2.8f);
    }

    public void SetInteractionSuspended(bool suspended)
    {
        interactionSuspended = suspended;
        if (!interactionSuspended)
        {
            nextAttemptTime = Time.time + Random.Range(2f, 3.5f);
        }
    }

    private void Update()
    {
        if (interactionSuspended || Time.time < nextAttemptTime || pets.Count == 0)
        {
            return;
        }

        List<PawPalRoomPetHandle> eligiblePets = new List<PawPalRoomPetHandle>();
        for (int i = 0; i < pets.Count; i++)
        {
            PawPalRoomPetHandle pet = pets[i];
            if (!IsEligible(pet))
            {
                continue;
            }

            eligiblePets.Add(pet);
        }

        if (eligiblePets.Count == 0)
        {
            nextAttemptTime = Time.time + RetryDelayWhenBlocked;
            return;
        }

        PawPalRoomPetHandle chosen = eligiblePets[Random.Range(0, eligiblePets.Count)];
        if (chosen != null && chosen.TryPlayPreviewVocal())
        {
            nextAttemptTime = Time.time + Random.Range(MinimumDelayBetweenPreviewVocals, MaximumDelayBetweenPreviewVocals);
            return;
        }

        nextAttemptTime = Time.time + RetryDelayWhenBlocked;
    }

    private static bool IsEligible(PawPalRoomPetHandle pet)
    {
        return pet != null
            && pet.IsValid
            && !pet.IsBusy
            && !pet.IsResting
            && !pet.IsSleeping
            && !pet.IsPlayingOneShotAnimation;
    }
}

[DisallowMultipleComponent]
public sealed class IntroPetInteractionOverlayController : MonoBehaviour
{
    private IntroPetUIController introUi;
    private IntroPetPreviewVocalDirector previewVocalDirector;
    private PawPalDogInteractionModeController interactionController;
    private PawPalDogInteractionModeView interactionView;

    public bool IsInteractionActive
    {
        get { return interactionController != null && interactionController.IsActive; }
    }

    public void Initialize(
        IntroPetSelectionController owner,
        IntroPetUIController uiController,
        IntroPetPreviewVocalDirector vocalDirector)
    {
        introUi = uiController;
        previewVocalDirector = vocalDirector;

        if (interactionController != null && interactionView != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "IntroPetInteractionOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(UiSpriteLibrary));
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        UiFactory.Stretch(canvasRect, 0f, 0f, 0f, 0f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        UiSpriteLibrary sprites = canvasObject.GetComponent<UiSpriteLibrary>();
        RectTransform interactionRoot = UiFactory.CreateRect("InteractionRoot", canvasObject.transform);
        UiFactory.Stretch(interactionRoot, 0f, 0f, 0f, 0f);

        interactionView = interactionRoot.gameObject.AddComponent<PawPalDogInteractionModeView>();
        interactionView.Initialize(sprites);
        interactionController = interactionRoot.gameObject.AddComponent<PawPalDogInteractionModeController>();
        interactionController.Initialize(null, interactionView);
        interactionController.InteractionExited += HandleInteractionExited;
        interactionView.Hide();
    }

    public void TryEnterPreviewInteraction(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid || interactionController == null || interactionController.IsActive)
        {
            return;
        }

        if (previewVocalDirector != null)
        {
            previewVocalDirector.SetInteractionSuspended(true);
        }

        SetIntroPreviewInteractionPresentation(true);
        if (!interactionController.TryEnterDogInteractionMode(pet, false, PawPalDogInteractionModeOptions.PreviewOnlyDefault))
        {
            if (previewVocalDirector != null)
            {
                previewVocalDirector.SetInteractionSuspended(false);
            }

            SetIntroPreviewInteractionPresentation(false);
        }
    }

    public void ExitInteraction()
    {
        if (interactionController == null || !interactionController.IsActive)
        {
            return;
        }

        interactionController.RequestShellExit("intro_continue");
    }

    private void HandleInteractionExited()
    {
        if (previewVocalDirector != null)
        {
            previewVocalDirector.SetInteractionSuspended(false);
        }

        SetIntroPreviewInteractionPresentation(false);
    }

    private void SetIntroPreviewInteractionPresentation(bool active)
    {
        if (introUi != null)
        {
            introUi.SetPreviewInteractionPresentation(active);
        }
    }
}
