using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public sealed class HomeSelectedPetSpawner : MonoBehaviour
{
    private static HomeSelectedPetSpawner instance;

    private struct SpawnAnchor
    {
        public bool HasValue;
        public Vector3 Position;
        public Quaternion Rotation;
    }

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

    private IEnumerator Start()
    {
        yield return null;
        Scene activeScene = SceneManager.GetActiveScene();
        if (PawPalIntroSceneFlow.IsHomeScene(activeScene) && !SelectedPetSessionContext.HasPendingSelection)
        {
            InitializeHandPlacedHomeCats();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PawPalIntroSceneFlow.IsHomeScene(scene))
        {
            return;
        }

        if (SelectedPetSessionContext.HasPendingSelection)
        {
            StartCoroutine(SpawnAfterSceneReady());
            return;
        }

        StartCoroutine(InitializeHandPlacedHomeCatsAfterSceneReady());
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
            PawPalSceneTransitionController.MarkActiveTransitionReady("Home scene finished without a pending intro pet selection.");
            yield break;
        }

        SpawnAnchor defaultDogAnchor = DisableDefaultSceneDogs();
        if (selection.Species == IntroPetSpecies.Cat)
        {
            SpawnCat(selection, defaultDogAnchor, runtime);
        }
        else
        {
            SpawnDog(selection, defaultDogAnchor, runtime);
        }

        InitializeHandPlacedHomeCats();
        PawPalSceneTransitionController.MarkActiveTransitionReady("Selected intro pet finished spawning in the home scene.");
    }

    private IEnumerator InitializeHandPlacedHomeCatsAfterSceneReady()
    {
        yield return null;
        InitializeHandPlacedHomeCats();
    }

    private static SpawnAnchor DisableDefaultSceneDogs()
    {
        SpawnAnchor anchor = new SpawnAnchor
        {
            HasValue = false,
            Position = Vector3.zero,
            Rotation = Quaternion.Euler(0f, 180f, 0f)
        };

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null)
            {
                continue;
            }

            if (!anchor.HasValue)
            {
                anchor.HasValue = true;
                anchor.Position = dog.transform.position;
                anchor.Rotation = dog.transform.rotation;
            }

            dog.gameObject.SetActive(false);
            Destroy(dog.gameObject);
        }

        return anchor;
    }

    private static void SpawnDog(SelectedPetSessionData selection, SpawnAnchor defaultDogAnchor, PawPalGameRuntime runtime)
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
        roomAgent.ApplySelectedPetPresentation(selection);
        roomAgent.ConfigureSelectedPetRuntime(selection);

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

    private static void SpawnCat(SelectedPetSessionData selection, SpawnAnchor defaultDogAnchor, PawPalGameRuntime runtime)
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

        DogRoomAgent legacyDogAgent = petObject.GetComponent<DogRoomAgent>();
        if (legacyDogAgent != null)
        {
            UnityEngine.Object.Destroy(legacyDogAgent);
        }

        PawPalCatRoomAgent catAgent = petObject.GetComponent<PawPalCatRoomAgent>();
        if (catAgent == null)
        {
            catAgent = petObject.GetComponentInChildren<PawPalCatRoomAgent>(true);
        }

        if (catAgent == null)
        {
            catAgent = petObject.AddComponent<PawPalCatRoomAgent>();
        }

        catAgent.Initialize(selection);
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

    private static void InitializeHandPlacedHomeCats()
    {
        IntroPetDefinition[] definitions = Resources.LoadAll<IntroPetDefinition>("PawPal/IntroPets/Definitions");
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int initializedCount = 0;
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            GameObject petObject = animator.gameObject;
            PawPalCatRoomAgent existingCatAgent = petObject.GetComponent<PawPalCatRoomAgent>();
            if (existingCatAgent != null && !string.IsNullOrWhiteSpace(existingCatAgent.RuntimePetId))
            {
                continue;
            }

            PawPalCatRoomAgent parentCatAgent = petObject.GetComponentInParent<PawPalCatRoomAgent>();
            if (parentCatAgent != null && parentCatAgent != existingCatAgent)
            {
                continue;
            }

            IntroPetDefinition definition = ResolveHandPlacedCatDefinition(petObject, animator, definitions);
            if (definition == null)
            {
                continue;
            }

            NavMeshAgent navMeshAgent = petObject.GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = petObject.AddComponent<NavMeshAgent>();
            }

            navMeshAgent.radius = Mathf.Max(0.18f, navMeshAgent.radius);
            navMeshAgent.height = Mathf.Max(0.45f, navMeshAgent.height);
            navMeshAgent.speed = Mathf.Max(0.55f, navMeshAgent.speed);
            navMeshAgent.angularSpeed = Mathf.Max(360f, navMeshAgent.angularSpeed);
            navMeshAgent.acceleration = Mathf.Max(5f, navMeshAgent.acceleration);

            DogRoomAgent legacyDogAgent = petObject.GetComponent<DogRoomAgent>();
            if (legacyDogAgent != null)
            {
                Destroy(legacyDogAgent);
            }

            PawPalCatRoomAgent catAgent = existingCatAgent != null ? existingCatAgent : petObject.AddComponent<PawPalCatRoomAgent>();
            catAgent.Initialize(BuildHandPlacedCatSession(definition, petObject.name, initializedCount));
            initializedCount++;
        }
    }

    private static SelectedPetSessionData BuildHandPlacedCatSession(IntroPetDefinition definition, string objectName, int index)
    {
        string safeName = !string.IsNullOrWhiteSpace(objectName) ? objectName : (definition != null ? definition.DisplayName : "Cat");
        return new SelectedPetSessionData
        {
            Definition = definition,
            FurVariant = null,
            FurIndex = 0,
            Gender = index % 2 == 0 ? PawPalDogGender.Female : PawPalDogGender.Male,
            Personality = PawPalDogPersonality.Curious,
            PetName = safeName,
            RuntimePetId = "placed_cat_" + NormalizeIdentity(safeName) + "_" + index.ToString()
        };
    }

    private static IntroPetDefinition ResolveHandPlacedCatDefinition(GameObject petObject, Animator animator, IntroPetDefinition[] definitions)
    {
        string candidateIdentity = NormalizeIdentity(
            (petObject != null ? petObject.name : string.Empty)
            + " "
            + (animator != null ? animator.name : string.Empty));
        if (string.IsNullOrEmpty(candidateIdentity))
        {
            return null;
        }

        for (int i = 0; i < definitions.Length; i++)
        {
            IntroPetDefinition definition = definitions[i];
            if (definition == null || definition.Species != IntroPetSpecies.Cat)
            {
                continue;
            }

            if (IdentityMatchesDefinition(candidateIdentity, definition))
            {
                return definition;
            }
        }

        return null;
    }

    private static bool IdentityMatchesDefinition(string candidateIdentity, IntroPetDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        string petId = NormalizeIdentity(definition.PetId);
        string displayName = NormalizeIdentity(definition.DisplayName);
        string breedName = NormalizeIdentity(definition.BreedName);
        return MatchesIdentity(candidateIdentity, petId)
            || MatchesIdentity(candidateIdentity, displayName)
            || MatchesIdentity(candidateIdentity, breedName)
            || (candidateIdentity.Contains("catsimple") && string.Equals(petId, "catsimple", System.StringComparison.Ordinal))
            || (candidateIdentity.Contains("kitten") && petId.Contains("kitten"))
            || (candidateIdentity.Contains("catstray") && petId.Contains("stray"))
            || (candidateIdentity.Contains("catfat") && petId.Contains("chubby"));
    }

    private static bool MatchesIdentity(string candidateIdentity, string value)
    {
        return !string.IsNullOrEmpty(value) && candidateIdentity.Contains(value);
    }

    private static string NormalizeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static GameObject InstantiateSelectedPet(SelectedPetSessionData selection, SpawnAnchor defaultDogAnchor, string namePrefix)
    {
        GameObject preferredPrefab = PetVariantApplier.GetPrefabForVariant(selection.Definition, selection.FurVariant);
        GameObject basePrefab = selection.Definition != null ? selection.Definition.BasePrefab : null;
        if (preferredPrefab == null && basePrefab == null)
        {
            Debug.LogWarning("HomeSelectedPetSpawner could not spawn " + selection.SafeName + " because the selected intro prefab is missing.");
            return null;
        }

        Vector3 position = defaultDogAnchor.HasValue ? defaultDogAnchor.Position : Vector3.zero;
        Quaternion rotation = defaultDogAnchor.HasValue ? defaultDogAnchor.Rotation : Quaternion.Euler(0f, 180f, 0f);
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
        if (selection.FurVariant != null && selection.FurVariant.ReplacementMaterial != null)
        {
            PetVariantApplier.ApplyMaterial(petObject, selection.FurVariant);
        }

        if (selection.Definition.HomeScale != Vector3.zero)
        {
            petObject.transform.localScale = selection.Definition.HomeScale;
        }

        return petObject;
    }

}
