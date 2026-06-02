using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class IntroPetSelectionController : MonoBehaviour
{
    private const int IntroDogSpawnCount = 3;
    private const int IntroCatSpawnCount = 2;

    private readonly List<IntroPetRuntimeSelection> selections = new List<IntroPetRuntimeSelection>();
    private IntroPetSpawner spawner;
    private PetSelectionCameraController cameraController;
    private IntroPetUIController uiController;
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

        cameraController = sceneCamera != null
            ? sceneCamera.gameObject.GetComponent<PetSelectionCameraController>()
            : null;
        if (cameraController == null)
        {
            GameObject cameraObject = sceneCamera != null ? sceneCamera.gameObject : new GameObject("IntroCamera");
            cameraController = cameraObject.AddComponent<PetSelectionCameraController>();
        }

        cameraController.Initialize(sceneCamera);

        spawner = gameObject.AddComponent<IntroPetSpawner>();
        spawner.Initialize(this, fieldBounds);
        BuildSelections(BuildSpawnRoster(spawner.LoadDefinitions()));
        ApplySpawnResults(spawner.Spawn(selections));

        uiController = new GameObject("IntroPetSelectionCanvas", typeof(RectTransform)).AddComponent<IntroPetUIController>();
        uiController.Build();
        BindUi();
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
        if (agent == null || uiState != IntroPetUiState.Selection)
        {
            return;
        }

        SelectPet(agent.SelectionIndex, false);
    }

    private void Update()
    {
        if (!initialized || uiState != IntroPetUiState.Selection)
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

        for (int i = 0; i < sourceDefinitions.Length; i++)
        {
            selections.Add(IntroPetRuntimeSelection.Create(sourceDefinitions[i], i));
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
            int swapIndex = Random.Range(0, i + 1);
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
        uiController.ContinueRequested += ShowConfirmCard;
        uiController.BackRequested += ShowSelectionCard;
        uiController.ConfirmAccepted += ConfirmSelection;
    }

    private void StepPet(int direction)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Selection)
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
            previous.SetSelected(false, cameraController != null ? cameraController.ControlledCamera : Camera.main);
        }

        currentIndex = Mathf.Clamp(index, 0, selections.Count - 1);
        IntroPetAgent current = CurrentAgent;
        if (current != null)
        {
            current.SetSelected(true, cameraController != null ? cameraController.ControlledCamera : Camera.main);
            cameraController.Focus(current, snapCamera);
        }

        RefreshUi();
    }

    private void SetFurIndex(int furIndex)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Selection)
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
            replacement.SetSelected(true, cameraController != null ? cameraController.ControlledCamera : Camera.main);
            cameraController.Focus(replacement, false);
        }
    }

    private void SetGender(PawPalDogGender gender)
    {
        if (selections.Count == 0 || uiState != IntroPetUiState.Selection)
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

    private void ShowConfirmCard()
    {
        if (uiController == null || selections.Count == 0)
        {
            return;
        }

        uiState = IntroPetUiState.Confirm;
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

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
