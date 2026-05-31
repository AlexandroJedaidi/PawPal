using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class IntroPetSelectionController : MonoBehaviour
{
    private readonly List<IntroPetRuntimeSelection> selections = new List<IntroPetRuntimeSelection>();
    private IntroPetSpawner spawner;
    private PetSelectionCameraController cameraController;
    private IntroPetUIController uiController;
    private IntroPetDefinition[] definitions = new IntroPetDefinition[0];
    private int currentIndex;
    private bool initialized;

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
        definitions = spawner.LoadDefinitions();
        BuildSelections(definitions);
        spawner.Spawn(definitions, selections);

        uiController = new GameObject("IntroPetSelectionCanvas", typeof(RectTransform)).AddComponent<IntroPetUIController>();
        uiController.Build();
        BindUi();

        SelectPet(0, true);
    }

    public void SelectAgent(IntroPetAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        SelectPet(agent.SelectionIndex, false);
    }

    private void Update()
    {
        if (!initialized)
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

    private void BindUi()
    {
        if (uiController == null)
        {
            return;
        }

        uiController.PetStepRequested += StepPet;
        uiController.FurStepRequested += StepFur;
        uiController.FurIndexRequested += SetFurIndex;
        uiController.GenderChanged += SetGender;
        uiController.NameChanged += SetName;
        uiController.ConfirmRequested += ShowConfirmModal;
        uiController.ConfirmAccepted += ConfirmSelection;
    }

    private void StepPet(int direction)
    {
        if (selections.Count == 0)
        {
            return;
        }

        int nextIndex = ((currentIndex + direction) % selections.Count + selections.Count) % selections.Count;
        SelectPet(nextIndex, false);
    }

    private void SelectPet(int index, bool snapCamera)
    {
        if (selections.Count == 0 || spawner == null || spawner.Agents.Count == 0)
        {
            Debug.LogWarning("IntroPetSelection has no pet definitions to display. Add IntroPetDefinition assets under Resources/PawPal/IntroPets/Definitions.");
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

    private void StepFur(int direction)
    {
        if (selections.Count == 0)
        {
            return;
        }

        IntroPetRuntimeSelection selection = selections[currentIndex];
        selection.SetFurIndex(selection.FurIndex + direction);
        ReplaceCurrentAgentVariant(selection);
        RefreshUi();
    }

    private void SetFurIndex(int furIndex)
    {
        if (selections.Count == 0)
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
        if (selections.Count == 0)
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

    private void ShowConfirmModal()
    {
        if (uiController == null || selections.Count == 0)
        {
            return;
        }

        selections[currentIndex].SetName(selections[currentIndex].PetName);
        uiController.ShowConfirmModal(true, selections[currentIndex]);
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

        uiController.Refresh(selections[currentIndex], currentIndex, selections.Count);
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
