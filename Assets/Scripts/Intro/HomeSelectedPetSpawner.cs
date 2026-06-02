using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

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
            SpawnCat(selection, defaultDogAnchor, runtime);
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
            runtime.RegisterTemporaryIntroPet(selection.BuildTemporaryDogState(), selection.Species, true);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            PawPalPetFollowCamera petFollowCamera = mainCamera.GetComponent<PawPalPetFollowCamera>();
            if (petFollowCamera != null)
            {
                petFollowCamera.enabled = false;
            }

            DogCycleCamera dogCamera = mainCamera.GetComponent<DogCycleCamera>();
            if (dogCamera != null)
            {
                dogCamera.enabled = true;
            }
        }

        PawPalIntroSceneFlow.SetAppShellVisible(true);
        DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
    }

    private static void SpawnCat(SelectedPetSessionData selection, Transform defaultDogAnchor, PawPalGameRuntime runtime)
    {
        GameObject petObject = InstantiateSelectedPet(selection, defaultDogAnchor, "SelectedIntroCat");
        if (petObject == null)
        {
            return;
        }

        NavMeshAgent navMeshAgent = petObject.GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = petObject.AddComponent<NavMeshAgent>();
        }

        PawPalCatRoomAgent catAgent = petObject.GetComponent<PawPalCatRoomAgent>();
        if (catAgent == null)
        {
            catAgent = petObject.AddComponent<PawPalCatRoomAgent>();
        }

        catAgent.Initialize(selection);
        if (runtime != null)
        {
            runtime.RegisterTemporaryIntroPet(selection.BuildTemporaryDogState(), selection.Species, true);
        }

        PawPalIntroSceneFlow.SetAppShellVisible(true);
        FocusCameraOnCat(catAgent);
    }

    private static GameObject InstantiateSelectedPet(SelectedPetSessionData selection, Transform defaultDogAnchor, string namePrefix)
    {
        GameObject preferredPrefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        GameObject basePrefab = selection.Definition != null ? selection.Definition.BasePrefab : null;
        if (preferredPrefab == null && basePrefab == null)
        {
            Debug.LogWarning("HomeSelectedPetSpawner could not spawn " + selection.SafeName + " because the selected intro prefab is missing.");
            return null;
        }

        Vector3 position = defaultDogAnchor != null ? defaultDogAnchor.position : Vector3.zero;
        Quaternion rotation = defaultDogAnchor != null ? defaultDogAnchor.rotation : Quaternion.Euler(0f, 180f, 0f);
        string preferredError;
        GameObject petObject;
        if (!PetVariantApplier.TryInstantiatePrefab(preferredPrefab != null ? preferredPrefab : basePrefab, position, rotation, null, out petObject, out preferredError))
        {
            if (basePrefab != null && basePrefab != preferredPrefab)
            {
                string baseError;
                if (PetVariantApplier.TryInstantiatePrefab(basePrefab, position, rotation, null, out petObject, out baseError))
                {
                    PetVariantApplier.ApplyMaterial(petObject, selection.FurVariant);
                }
                else
                {
                    Debug.LogWarning("HomeSelectedPetSpawner could not spawn " + selection.SafeName + ". " + preferredError + " " + baseError);
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("HomeSelectedPetSpawner could not spawn " + selection.SafeName + ". " + preferredError);
                return null;
            }
        }

        petObject.name = namePrefix + "_" + selection.SafeName;

        if (selection.Definition.HomeScale != Vector3.zero)
        {
            petObject.transform.localScale = selection.Definition.HomeScale;
        }

        return petObject;
    }

    private static void FocusCameraOnCat(PawPalCatRoomAgent catAgent)
    {
        Camera camera = Camera.main;
        if (camera == null || catAgent == null)
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

        PawPalPetFollowCamera followCamera = camera.GetComponent<PawPalPetFollowCamera>();
        if (followCamera == null)
        {
            followCamera = camera.gameObject.AddComponent<PawPalPetFollowCamera>();
        }

        followCamera.enabled = true;
        followCamera.Focus(catAgent.FocusTransform, catAgent.HomeCameraOffset, true);
    }
}
