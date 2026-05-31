using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class HomeSelectedPetSpawner : MonoBehaviour
{
    private static HomeSelectedPetSpawner instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("HomeSelectedPetSpawner");
        DontDestroyOnLoad(bootstrap);
        instance = bootstrap.AddComponent<HomeSelectedPetSpawner>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PawPalIntroSceneFlow.IsHomeScene(scene) || !SelectedPetSessionContext.HasPendingSelection)
        {
            return;
        }

        StartCoroutine(SpawnAfterSceneReady());
    }

    private IEnumerator SpawnAfterSceneReady()
    {
        yield return null;

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            yield return null;
            runtime = PawPalGameRuntime.Instance;
        }

        SelectedPetSessionData selection;
        if (!SelectedPetSessionContext.TryConsumePendingSelection(out selection) || selection == null || selection.Definition == null)
        {
            yield break;
        }

        Transform defaultDogAnchor = DisableDefaultSceneDogs();
        if (selection.Species == IntroPetSpecies.Cat)
        {
            SpawnCat(selection, defaultDogAnchor);
        }
        else
        {
            SpawnDog(selection, defaultDogAnchor, runtime);
        }
    }

    private static Transform DisableDefaultSceneDogs()
    {
        Transform firstAnchor = null;
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null)
            {
                continue;
            }

            if (firstAnchor == null)
            {
                firstAnchor = dog.transform;
            }

            dog.gameObject.SetActive(false);
        }

        return firstAnchor;
    }

    private static void SpawnDog(SelectedPetSessionData selection, Transform defaultDogAnchor, PawPalGameRuntime runtime)
    {
        GameObject petObject = InstantiateSelectedPet(selection, defaultDogAnchor, "SelectedIntroDog");
        if (petObject == null)
        {
            return;
        }

        NavMeshAgent navMeshAgent = petObject.GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = petObject.AddComponent<NavMeshAgent>();
        }

        navMeshAgent.radius = Mathf.Max(0.15f, navMeshAgent.radius);
        navMeshAgent.height = Mathf.Max(0.45f, navMeshAgent.height);

        DogRoomAgent roomAgent = petObject.GetComponent<DogRoomAgent>();
        if (roomAgent == null)
        {
            roomAgent = petObject.AddComponent<DogRoomAgent>();
        }

        roomAgent.SetRuntimeDogId(selection.RuntimePetId);

        if (selection.Definition.AnimationSet != null)
        {
            selection.Definition.AnimationSet.ApplyTo(petObject.GetComponentInChildren<Animator>(true));
        }

        if (selection.FurVariant != null && selection.FurVariant.ReplacementMaterial != null)
        {
            PetVariantApplier.ApplyMaterial(petObject, selection.FurVariant);
        }

        if (runtime != null)
        {
            runtime.RegisterTemporaryIntroDog(selection.BuildTemporaryDogState(), true);
        }

        PawPalIntroSceneFlow.SetAppShellVisible(true);
        DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
    }

    private static void SpawnCat(SelectedPetSessionData selection, Transform defaultDogAnchor)
    {
        GameObject petObject = InstantiateSelectedPet(selection, defaultDogAnchor, "SelectedIntroCat");
        if (petObject == null)
        {
            return;
        }

        PetHomeAgent homeAgent = petObject.GetComponent<PetHomeAgent>();
        if (homeAgent == null)
        {
            homeAgent = petObject.AddComponent<PetHomeAgent>();
        }

        homeAgent.Initialize(selection);
        PawPalIntroSceneFlow.SetAppShellVisible(false);
        FocusCameraOnCat(petObject.transform, selection.Definition.HomeCameraOffset);
        CreateCatHomePresentation(selection);
    }

    private static GameObject InstantiateSelectedPet(SelectedPetSessionData selection, Transform defaultDogAnchor, string namePrefix)
    {
        GameObject prefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        if (prefab == null)
        {
            Debug.LogWarning("HomeSelectedPetSpawner could not spawn " + selection.SafeName + " because the selected intro prefab is missing.");
            return null;
        }

        Vector3 position = defaultDogAnchor != null ? defaultDogAnchor.position : Vector3.zero;
        Quaternion rotation = defaultDogAnchor != null ? defaultDogAnchor.rotation : Quaternion.Euler(0f, 180f, 0f);
        GameObject petObject = Instantiate(prefab, position, rotation);
        petObject.name = namePrefix + "_" + selection.SafeName;

        if (selection.Definition.HomeScale != Vector3.zero)
        {
            petObject.transform.localScale = selection.Definition.HomeScale;
        }

        return petObject;
    }

    private static void FocusCameraOnCat(Transform target, Vector3 offset)
    {
        Camera camera = Camera.main;
        if (camera == null || target == null)
        {
            return;
        }

        DogCycleCamera dogCamera = camera.GetComponent<DogCycleCamera>();
        if (dogCamera != null)
        {
            dogCamera.enabled = false;
        }

        CameraFollow legacyFollow = camera.GetComponent<CameraFollow>();
        if (legacyFollow != null)
        {
            legacyFollow.enabled = false;
        }

        Vector3 focusPoint = target.position + Vector3.up * 0.55f;
        Vector3 cameraOffset = offset == Vector3.zero ? new Vector3(0f, 1.1f, -3.1f) : offset;
        camera.transform.position = target.position + cameraOffset;
        camera.transform.rotation = Quaternion.LookRotation(focusPoint - camera.transform.position, Vector3.up);
    }

    private static void CreateCatHomePresentation(SelectedPetSessionData selection)
    {
        GameObject canvasObject = new GameObject("CatHomePresentationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        RectTransform root = UiFactory.CreateRect("SafeAreaRoot", canvasObject.transform);
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);
        root.gameObject.AddComponent<SafeAreaFitter>();

        Image panel = UiFactory.CreateImage("CatInfoPanel", root, UiTheme.RoundedTenSprite, UiTheme.NavBackgroundCream);
        UiFactory.AnchorBottomStretch(panel.rectTransform, 18f, 18f, 18f, 88f);

        string headline = selection.SafeName + " the " + selection.BreedName;
        TextMeshProUGUI title = UiFactory.CreateLabel("Title", panel.rectTransform, headline, 18, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        title.font = UiTheme.NavExtraBoldFont;
        title.enableAutoSizing = true;
        title.fontSizeMin = 12f;
        UiFactory.Stretch(title.rectTransform, 16f, 37f, 16f, 12f);

        string detailText = IntroPetFormatting.FormatGender(selection.Gender)
            + " - "
            + IntroPetFormatting.FormatPersonality(selection.Personality)
            + " - "
            + (selection.FurVariant != null ? selection.FurVariant.SafeDisplayName : "Default");
        TextMeshProUGUI detail = UiFactory.CreateLabel("Detail", panel.rectTransform, detailText, 12, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Left);
        detail.font = UiTheme.NavRegularFont;
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 9f;
        UiFactory.Stretch(detail.rectTransform, 16f, 12f, 16f, 44f);
    }
}
