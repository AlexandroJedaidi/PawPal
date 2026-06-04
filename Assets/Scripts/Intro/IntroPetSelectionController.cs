using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class IntroPetSelectionController : MonoBehaviour
{
    private static readonly string[] TestSpawnRosterPetIds =
    {
        "husky",
        "corgi",
        "puppy_labrador",
        "kitten_simple",
        "cat_simple",
        "cat_chubby",
        "cat_stray"
    };

#if UNITY_EDITOR
    private const string LabradorPuppyPrefabPath = "Assets/Plug'n'Play Folder/Labrador Puppy.prefab";
#endif

    private readonly List<IntroPetRuntimeSelection> selections = new List<IntroPetRuntimeSelection>();
    private IntroPetSpawner spawner;
    private DogCycleCamera cameraController;
    private PetSelectionCameraController selectionCameraController;
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

        selectionCameraController = cameraObject.GetComponent<PetSelectionCameraController>();
        if (selectionCameraController == null)
        {
            selectionCameraController = cameraObject.AddComponent<PetSelectionCameraController>();
        }

        selectionCameraController.Initialize(controlledCamera);
        selectionCameraController.enabled = false;

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

        Dictionary<string, IntroPetDefinition> definitionsById = new Dictionary<string, IntroPetDefinition>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sourceDefinitions.Length; i++)
        {
            IntroPetDefinition definition = sourceDefinitions[i];
            if (definition != null && !string.IsNullOrWhiteSpace(definition.PetId) && !definitionsById.ContainsKey(definition.PetId))
            {
                definitionsById.Add(definition.PetId, definition);
            }
        }

        List<IntroPetDefinition> roster = new List<IntroPetDefinition>();
        for (int i = 0; i < TestSpawnRosterPetIds.Length; i++)
        {
            IntroPetDefinition definition = ResolveTestRosterDefinition(TestSpawnRosterPetIds[i], definitionsById);
            if (definition != null)
            {
                roster.Add(definition);
            }
        }

        return roster.ToArray();
    }

    private static IntroPetDefinition ResolveTestRosterDefinition(string petId, Dictionary<string, IntroPetDefinition> definitionsById)
    {
        if (string.IsNullOrWhiteSpace(petId) || definitionsById == null)
        {
            return null;
        }

        IntroPetDefinition definition;
        if (definitionsById.TryGetValue(petId, out definition))
        {
            return CreateSpawnOnlyDefinition(definition);
        }

        if (string.Equals(petId, "puppy_labrador", System.StringComparison.OrdinalIgnoreCase))
        {
            return CreateLabradorPuppyDefinition(definitionsById);
        }

        Debug.LogWarning("IntroPetSelection test roster could not find definition for pet id '" + petId + "'.");
        return null;
    }

    private static IntroPetDefinition CreateSpawnOnlyDefinition(IntroPetDefinition template)
    {
        if (template == null)
        {
            return null;
        }

        IntroPetDefinition clone = CloneDefinition(template, template.PetId, template.DisplayName, template.Species);
        if (clone == null)
        {
            return null;
        }

        clone.BreedName = template.BreedName;
        clone.Description = template.Description;
        clone.BasePrefab = template.BasePrefab;
        clone.IntroScale = template.IntroScale;
        clone.HomeScale = template.HomeScale;
        clone.IntroCameraOffset = template.IntroCameraOffset;
        clone.HomeCameraOffset = template.HomeCameraOffset;
        clone.RoamRadius = template.RoamRadius;
        clone.AnimationSet = template.AnimationSet;

        if (clone.Species == IntroPetSpecies.Cat)
        {
            clone.FurVariants = BuildCatFurVariants(clone, template);
        }

        return clone;
    }

    private static IntroPetDefinition CreateLabradorPuppyDefinition(Dictionary<string, IntroPetDefinition> definitionsById)
    {
        IntroPetDefinition labrador = FindDefinition(definitionsById, "labrador");
        IntroPetDefinition definition = CloneDefinition(labrador, "puppy_labrador", "Labrador Puppy", IntroPetSpecies.Dog);
        if (definition == null)
        {
            return null;
        }

        definition.BreedName = "Labrador Puppy";
        definition.IntroCameraOffset = new Vector3(0f, 0.92f, -2.45f);
        definition.HomeCameraOffset = new Vector3(0f, 0.78f, -2.35f);
        definition.RoamRadius = 2.8f;
        definition.FurVariants = new FurVariantDefinition[0];
#if UNITY_EDITOR
        definition.BasePrefab = LoadEditorGameObject(LabradorPuppyPrefabPath, definition.BasePrefab);
#endif
        return definition;
    }

    private static IntroPetDefinition CreateKittenDefinition(Dictionary<string, IntroPetDefinition> definitionsById)
    {
        IntroPetDefinition simpleCat = FindDefinition(definitionsById, "cat_simple");
        IntroPetDefinition definition = CloneDefinition(simpleCat, "kitten_simple", "Small Kitten", IntroPetSpecies.Cat);
        if (definition == null)
        {
            return null;
        }

        definition.BreedName = "Small Kitten";
        definition.IntroScale = simpleCat.IntroScale * 0.68f;
        definition.HomeScale = simpleCat.HomeScale * 0.68f;
        definition.IntroCameraOffset = new Vector3(0f, 0.68f, -1.95f);
        definition.HomeCameraOffset = new Vector3(0f, 0.62f, -2.05f);
        definition.RoamRadius = 2.45f;
        definition.FurVariants = BuildCatFurVariants(definition, simpleCat);
        return definition;
    }

    private static IntroPetDefinition CreateStrayCatDefinition(Dictionary<string, IntroPetDefinition> definitionsById)
    {
        IntroPetDefinition simpleCat = FindDefinition(definitionsById, "cat_simple");
        IntroPetDefinition definition = CloneDefinition(simpleCat, "cat_stray", "Stray Cat", IntroPetSpecies.Cat);
        if (definition == null)
        {
            return null;
        }

        definition.BreedName = "Stray Cat";
        definition.Description = "Lean, watchful, and quick to move, this stray cat uses the simple cat rig so selector locomotion clips can be tested.";
        definition.IntroScale = simpleCat.IntroScale * 0.92f;
        definition.HomeScale = simpleCat.HomeScale * 0.92f;
        definition.FurVariants = BuildCatFurVariants(definition, simpleCat);
        return definition;
    }

    private static FurVariantDefinition[] BuildCatFurVariants(IntroPetDefinition definition, IntroPetDefinition fallback)
    {
#if UNITY_EDITOR
        FurVariantDefinition[] editorVariants = BuildEditorCatFurVariants(definition);
        if (editorVariants != null && editorVariants.Length > 0)
        {
            return editorVariants;
        }
#endif

        if (fallback != null && fallback.FurVariants != null)
        {
            return fallback.FurVariants;
        }

        return new FurVariantDefinition[0];
    }

#if UNITY_EDITOR
    private static FurVariantDefinition[] BuildEditorCatFurVariants(IntroPetDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.PetId))
        {
            return new FurVariantDefinition[0];
        }

        switch (definition.PetId)
        {
            case "kitten_simple":
                return BuildEditorCatVariantSet(
                    "KittenSimple",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Kittens/KittenSimple/Kitten_Simple/Prefabs",
                    "KittenSimple_C",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Kittens/KittenSimple/Kitten_Simple/Materials",
                    "KittenSim",
                    5,
                    "KittenSim_color_",
                    "KittenSim_mobile_");
            case "cat_simple":
                return BuildEditorCatVariantSet(
                    "Cat_Simple",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Simple/Cat/Prefabs",
                    "Cat_Simple_C",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Simple/Cat/Materials",
                    "CatSim_color",
                    6,
                    "CatSim_color_",
                    "CatSim_color_M");
            case "cat_stray":
                return BuildEditorCatVariantSet(
                    "CatStray",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Stray/CatStray/Prefabs",
                    "CatStray_C",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Stray/CatStray/Materials",
                    "StrayCat",
                    5,
                    "StrayCat_color_",
                    "StrayCat_Mobile_");
            case "cat_chubby":
                return BuildEditorCatVariantSet(
                    "CatFat",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Fat/CatFat/Prefabs",
                    "CatFat_C",
                    "Assets/3rd Party Packs/Dogs (Red Deer)/CatFamily/Cats/Cat_Fat/CatFat/Materials",
                    "CatFat",
                    5,
                    "CatFat_color_",
                    "CatFat_mobile_");
            default:
                return new FurVariantDefinition[0];
        }
    }

    private static FurVariantDefinition[] BuildEditorCatVariantSet(
        string displayPrefix,
        string prefabDirectory,
        string prefabPrefix,
        string materialDirectory,
        string materialNameContains,
        int variantCount,
        params string[] materialPrefixes)
    {
        List<FurVariantDefinition> variants = new List<FurVariantDefinition>(variantCount);
        for (int variantNumber = 1; variantNumber <= variantCount; variantNumber++)
        {
            string variantNumberText = variantNumber.ToString();
            string variantAssetName = displayPrefix + "_C" + variantNumberText;
            GameObject prefab = LoadEditorVariantPrefab(prefabDirectory, prefabPrefix + variantNumberText);
            if (prefab == null)
            {
                continue;
            }

            FurVariantDefinition variant = ScriptableObject.CreateInstance<FurVariantDefinition>();
            variant.hideFlags = HideFlags.DontSave;
            variant.name = variantAssetName + "_RuntimeVariant";
            variant.VariantId = NormalizeVariantId(variantAssetName);
            variant.DisplayName = variantNumberText;
            variant.VariantPrefab = prefab;
            variant.ReplacementMaterial = LoadEditorVariantMaterial(materialDirectory, variantNumber, materialPrefixes);
            variant.MaterialNameContains = materialNameContains;
            variants.Add(variant);
        }

        return variants.ToArray();
    }

    private static GameObject LoadEditorVariantPrefab(string directory, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(expectedName) || !Directory.Exists(directory))
        {
            return null;
        }

        string normalizedExpected = NormalizeVariantFileName(expectedName);
        string[] paths = Directory.GetFiles(directory, "*.prefab");
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = NormalizeAssetPath(paths[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (!string.Equals(NormalizeVariantFileName(fileName), normalizedExpected, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        return null;
    }

    private static Material LoadEditorVariantMaterial(string directory, int variantNumber, params string[] materialPrefixes)
    {
        if (string.IsNullOrWhiteSpace(directory) || materialPrefixes == null || materialPrefixes.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < materialPrefixes.Length; i++)
        {
            string assetPath = NormalizeAssetPath(Path.Combine(directory, materialPrefixes[i] + variantNumber.ToString() + ".mat"));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                return material;
            }
        }

        return null;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static string NormalizeVariantFileName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\u0421', 'C').Replace("_c", "_C");
    }

    private static string NormalizeVariantId(string value)
    {
        return NormalizeVariantFileName(value).Replace(" ", string.Empty).ToLowerInvariant();
    }
#endif

    private static IntroPetDefinition FindDefinition(Dictionary<string, IntroPetDefinition> definitionsById, string petId)
    {
        IntroPetDefinition definition;
        return definitionsById != null && definitionsById.TryGetValue(petId, out definition) ? definition : null;
    }

    private static IntroPetDefinition CloneDefinition(
        IntroPetDefinition template,
        string petId,
        string displayName,
        IntroPetSpecies species)
    {
        if (template == null)
        {
            Debug.LogWarning("IntroPetSelection test roster could not create '" + displayName + "' because its template definition was missing.");
            return null;
        }

        IntroPetDefinition clone = ScriptableObject.CreateInstance<IntroPetDefinition>();
        clone.hideFlags = HideFlags.DontSave;
        clone.name = displayName.Replace(" ", string.Empty) + "_RuntimeTestDefinition";
        clone.PetId = petId;
        clone.DisplayName = displayName;
        clone.Species = species;
        clone.BreedName = displayName;
        clone.Description = template.Description;
        clone.BasePrefab = template.BasePrefab;
        clone.IntroScale = template.IntroScale;
        clone.HomeScale = template.HomeScale;
        clone.IntroCameraOffset = template.IntroCameraOffset;
        clone.HomeCameraOffset = template.HomeCameraOffset;
        clone.RoamRadius = template.RoamRadius;
        clone.AnimationSet = template.AnimationSet;
        clone.FurVariants = template.FurVariants;
        return clone;
    }

#if UNITY_EDITOR
    private static GameObject LoadEditorGameObject(string assetPath, GameObject fallback)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return fallback;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning("IntroPetSelection test roster could not load asset at '" + assetPath + "'. Using template prefab fallback.");
            return fallback;
        }

        return asset;
    }
#endif

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
        if (agent == null)
        {
            return;
        }

        DisableTemporaryPetFollowCamera();

        if (agent.RoomPet != null && agent.RoomPet.IsDog && cameraController != null && agent.RuntimeDogAgent != null)
        {
            if (selectionCameraController != null)
            {
                selectionCameraController.enabled = false;
            }

            cameraController.enabled = true;
            cameraController.FocusIntroSelectionDog(agent.RuntimeDogAgent, snapCamera);
            return;
        }

        if (selectionCameraController == null)
        {
            return;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        selectionCameraController.enabled = true;
        selectionCameraController.Focus(agent, snapCamera);
    }

    internal void RestoreCurrentSelectionCamera()
    {
        IntroPetAgent current = CurrentAgent;
        if (current != null)
        {
            FocusCameraOnAgent(current, false);
        }
    }

    private void DisableTemporaryPetFollowCamera()
    {
        if (controlledCamera == null)
        {
            return;
        }

        PawPalPetFollowCamera followCamera = controlledCamera.GetComponent<PawPalPetFollowCamera>();
        if (followCamera != null)
        {
            followCamera.enabled = false;
        }
    }

    private static void EnsureEventSystem()
    {
        PawPalEventSystemUtility.EnsureSingleEventSystem(false);
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
    private IntroPetSelectionController owner;
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
        this.owner = owner;
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

        DogSocialDirector.SetIntroPreviewInteractionSuspended(true);

        SetIntroPreviewInteractionPresentation(true);
        if (!interactionController.TryEnterDogInteractionMode(pet, false, PawPalDogInteractionModeOptions.PreviewOnlyDefault))
        {
            if (previewVocalDirector != null)
            {
                previewVocalDirector.SetInteractionSuspended(false);
            }

            DogSocialDirector.SetIntroPreviewInteractionSuspended(false);

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

        DogSocialDirector.SetIntroPreviewInteractionSuspended(false);

        SetIntroPreviewInteractionPresentation(false);
        if (owner != null)
        {
            owner.RestoreCurrentSelectionCamera();
        }
    }

    private void SetIntroPreviewInteractionPresentation(bool active)
    {
        if (introUi != null)
        {
            introUi.SetPreviewInteractionPresentation(active);
        }
    }
}
