using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PawPalDogInteractionModeController : MonoBehaviour
{
    private const float PettingBondGain = 0.008f;
    private const float PettingMoodGain = 0.012f;
    private const float PettingActivityGain = 0.006f;
    private const float PettingRewardCooldown = 1.1f;
    private const float TugBondGain = 0.006f;
    private const float TugMoodGain = 0.008f;
    private const float TugActivityGain = 0.01f;
    private const float TugRewardInterval = 1.35f;
    private const float ToyScreenPadding = 28f;

    private AppShellController shell;
    private PawPalDogInteractionModeView view;
    private PawPalDogInteractionDirector interactionDirector;
    private PawPalGestureRecognizer gestureRecognizer;
    private PawPalTrainingConfig trainingConfig;
    private DogCycleCamera dogCamera;
    private DogRoomAgent activeDog;
    private PawPalTrickId selectedTrick = PawPalTrickId.Sit;
    private Coroutine attemptRoutine;
    private float timeoutSeconds = 15f;
    private float lastActivityTime;
    private float nextPettingRewardTime;
    private float nextTugRewardTime;
    private float ignoreGestureUntil;
    private bool active;
    private bool micListening;
    private bool dogIsSitting;
    private bool tugActive;
    private int tugPointerId = -1;

    public bool IsActive
    {
        get { return active; }
    }

    public void Initialize(AppShellController ownerShell, PawPalDogInteractionModeView interactionView)
    {
        shell = ownerShell;
        view = interactionView;
        trainingConfig = PawPalTrainingConfig.CreateRuntimeDefault();

        interactionDirector = GetComponent<PawPalDogInteractionDirector>();
        if (interactionDirector == null)
        {
            interactionDirector = gameObject.AddComponent<PawPalDogInteractionDirector>();
        }

        gestureRecognizer = GetComponent<PawPalGestureRecognizer>();
        if (gestureRecognizer == null)
        {
            gestureRecognizer = gameObject.AddComponent<PawPalGestureRecognizer>();
        }

        gestureRecognizer.GestureRecognized += HandleGestureRecognized;

        if (view != null)
        {
            view.CloseRequested += ExitDogInteractionMode;
            view.MicRequested += HandleMicRequested;
            view.Hide();
        }
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        if (gestureRecognizer != null)
        {
            gestureRecognizer.Configure(activeDog, ResolveInteractionCamera(), trainingConfig != null ? trainingConfig.GestureLeniency : 1f);
        }

        HandleToyTugInput();
        float remaining = timeoutSeconds - (Time.unscaledTime - lastActivityTime);
        if (view != null)
        {
            view.SetTimeout(remaining);
        }

        if (remaining <= 0f)
        {
            ExitDogInteractionMode();
        }
    }

    private void OnDisable()
    {
        ExitDogInteractionMode();
    }

    public bool TryEnterDogInteractionMode(string dogId, bool keepMicListening)
    {
        DogRoomAgent dog = ResolveDog(dogId);
        if (dog == null)
        {
            ShowToast("No active dog for interaction mode.");
            return false;
        }

        dogCamera = ResolveDogCamera();
        if (dogCamera == null || !dogCamera.EnterDogInteractionMode(dog))
        {
            ShowToast("Dog interaction needs a dog camera.");
            return false;
        }

        activeDog = dog;
        micListening = keepMicListening;
        dogIsSitting = false;
        ResetActivity();

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        if (dogState != null)
        {
            PawPalDogPersonalityProfiles.EnsureProfile(dogState);
            timeoutSeconds = PawPalDogInteractionTuning.GetInactivityTimeoutSeconds(dogState.Personality);
            PawPalTrickCatalog.EnsureDogTrickData(dogState);
        }
        else
        {
            timeoutSeconds = 15f;
        }

        active = true;
        ApplyInteractionVocalContext(activeDog, DogVocalContext.InteractionBoosted);
        if (view != null)
        {
            view.Show(dogState, micListening, timeoutSeconds);
            view.SetTrackedDog(activeDog, ResolveInteractionCamera());
        }

        if (gestureRecognizer != null)
        {
            gestureRecognizer.Configure(activeDog, ResolveInteractionCamera(), trainingConfig != null ? trainingConfig.GestureLeniency : 1f);
            gestureRecognizer.SetActive(true);
        }

        string failureReason;
        if (interactionDirector != null && !interactionDirector.TryCallDogToInteraction(activeDog, ResolveInteractionCamera(), out failureReason))
        {
            SetStatus(string.IsNullOrWhiteSpace(failureReason) ? "Your dog is busy right now." : failureReason);
            ExitDogInteractionMode();
            return false;
        }

        return true;
    }

    public void SetMicListening(bool listening)
    {
        micListening = listening;
        if (view != null)
        {
            view.SetMicListening(listening);
        }
    }

    public bool TryPerformVoiceTrick(PawPalResolvedVoiceCommand command, out string message)
    {
        message = "Interaction mode is not active.";
        if (!active || command == null || command.Type != PawPalResolvedVoiceCommandType.PerformTrick)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(command.DogId))
        {
            PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
            if (runtime != null)
            {
                runtime.SelectDogById(command.DogId, false);
            }

            DogRoomAgent resolvedDog = ResolveDog(command.DogId);
            if (resolvedDog != null)
            {
                if (activeDog != resolvedDog)
                {
                    ApplyInteractionVocalContext(activeDog, DogVocalContext.Ambient);
                }

                activeDog = resolvedDog;
                ApplyInteractionVocalContext(activeDog, DogVocalContext.InteractionBoosted);
                if (dogCamera != null)
                {
                    dogCamera.EnterDogInteractionMode(activeDog);
                }

                if (view != null)
                {
                    view.SetTrackedDog(activeDog, ResolveInteractionCamera());
                }
            }
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(command.TrickId);
        string trickLabel = definition != null ? definition.DisplayName : command.TrickLabel;
        if (StartTrickAttempt(command.TrickId, true, out message))
        {
            message = "Training " + (string.IsNullOrWhiteSpace(trickLabel) ? command.TrickId.ToString() : trickLabel) + ".";
            return true;
        }

        return false;
    }

    public void ExitDogInteractionMode()
    {
        if (!active && view != null && !view.IsVisible)
        {
            return;
        }

        active = false;
        tugActive = false;
        tugPointerId = -1;
        if (attemptRoutine != null)
        {
            StopCoroutine(attemptRoutine);
            attemptRoutine = null;
        }

        if (gestureRecognizer != null)
        {
            gestureRecognizer.SetActive(false);
        }

        if (interactionDirector != null)
        {
            interactionDirector.Cancel(true);
        }

        if (dogCamera != null)
        {
            dogCamera.ExitDogInteractionMode();
            dogCamera = null;
        }

        if (activeDog != null)
        {
            activeDog.StopHeldToyTugAnimation();
        }

        ApplyInteractionVocalContext(activeDog, DogVocalContext.Ambient);
        activeDog = null;
        if (view != null)
        {
            view.SetTrackedDog(null, null);
            view.Hide();
        }

        if (shell != null)
        {
            shell.HandleDogInteractionModeExitedFromController();
        }
    }

    private void HandleGestureRecognized(PawPalGestureSnapshot snapshot)
    {
        if (!active || snapshot == null || Time.unscaledTime < ignoreGestureUntil)
        {
            return;
        }

        ResetActivity();
        if (IsPettingGesture(snapshot))
        {
            ApplyPettingReward();
            return;
        }

        if (snapshot.RejectionReason != PawPalTrickFailureReason.None)
        {
            SetStatus("Try a clearer cue near your dog.");
            return;
        }

        PawPalTrickId trickId = PawPalTrickGestureMapper.MapGestureToTrick(snapshot, selectedTrick, dogIsSitting);
        string message;
        if (!StartTrickAttempt(trickId, false, out message))
        {
            SetStatus(message);
        }
    }

    private bool StartTrickAttempt(PawPalTrickId trickId, bool fromVoice, out string message)
    {
        message = "Training is busy.";
        if (!active || attemptRoutine != null)
        {
            return false;
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        if (definition == null || dogState == null || activeDog == null)
        {
            message = "Training needs an active dog.";
            return false;
        }

        if (activeDog.HasHeldToy)
        {
            message = "Play tug first; your dog is holding a toy.";
            return false;
        }

        selectedTrick = trickId;
        attemptRoutine = StartCoroutine(TrickAttemptRoutine(definition, fromVoice));
        message = "Training " + definition.DisplayName + ".";
        ResetActivity();
        return true;
    }

    private IEnumerator TrickAttemptRoutine(PawPalTrickDefinition definition, bool fromVoice)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        bool posePrerequisite = definition.Id != PawPalTrickId.Lie || dogIsSitting || (runtime != null && runtime.IsActiveDogTrickLearned(PawPalTrickId.Sit));
        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dogState, definition, trainingConfig, fromVoice, false, posePrerequisite);
        if (runtime != null)
        {
            runtime.RecordTrickTrainingResult(result);
        }

        string feedbackText = result != null && !string.IsNullOrWhiteSpace(result.FeedbackText) ? result.FeedbackText : "Try again.";
        if (result != null && result.Success && activeDog != null)
        {
            yield return StartCoroutine(activeDog.PlayInteractionTrainingTrick(definition, true));
            UpdatePosture(definition.Id);

            if (view != null)
            {
                view.ShowUnderstoodCue(definition.DisplayName);
                if (result.MadeProgress)
                {
                    view.ShowTrainingProgressCue(definition.DisplayName, result.PreviousProgress01, result.CurrentProgress01);
                }
            }
        }
        else
        {
            if (result != null && activeDog != null && IsFrustrationEligibleFailureReason(result.Reason))
            {
                activeDog.TryPlayInteractionAnnoyedVocal();
            }

            SetStatus(feedbackText);
        }

        ResetActivity();
        attemptRoutine = null;
    }

    private static bool IsFrustrationEligibleFailureReason(PawPalTrickFailureReason reason)
    {
        return reason == PawPalTrickFailureReason.RandomMiss
            || reason == PawPalTrickFailureReason.LowFocus
            || reason == PawPalTrickFailureReason.MissingPrerequisite;
    }

    private static void ApplyInteractionVocalContext(DogRoomAgent dog, DogVocalContext context)
    {
        if (dog != null)
        {
            dog.SetVocalContext(context);
        }
    }

    private void ApplyPettingReward()
    {
        if (Time.unscaledTime < nextPettingRewardTime)
        {
            SetStatus("Gentle scratches.");
            return;
        }

        nextPettingRewardTime = Time.unscaledTime + PettingRewardCooldown;
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.ApplyActiveDogInteractionBond(PettingBondGain, PettingMoodGain, PettingActivityGain);
        }

        if (view != null)
        {
            view.ShowBondCue("Bond +");
            view.SetBondPulse(true);
        }
    }

    private void ApplyTugReward()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.ApplyActiveDogInteractionBond(TugBondGain, TugMoodGain, TugActivityGain);
        }

        if (view != null)
        {
            view.ShowBondCue("Tug");
            view.SetBondPulse(true);
        }
    }

    private void HandleToyTugInput()
    {
        if (activeDog == null || !activeDog.HasHeldToy)
        {
            if (activeDog != null)
            {
                activeDog.StopHeldToyTugAnimation();
            }

            tugActive = false;
            tugPointerId = -1;
            return;
        }

        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleToyTugTouch();
            return;
        }

        HandleToyTugMouse();
    }

    private void HandleToyTugMouse()
    {
        Vector2 position = Input.mousePosition;
        if (Input.GetMouseButtonDown(0) && IsPointerOverHeldToy(position) && !IsPointerOverUi(-1))
        {
            BeginTug(-1);
        }

        if (!tugActive || tugPointerId != -1)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            ContinueTug();
        }
        else
        {
            EndTug();
        }
    }

    private void HandleToyTugTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (!tugActive && touch.phase == TouchPhase.Began && IsPointerOverHeldToy(touch.position) && !IsPointerOverUi(touch.fingerId))
            {
                BeginTug(touch.fingerId);
                return;
            }

            if (!tugActive || touch.fingerId != tugPointerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                ContinueTug();
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndTug();
            }

            return;
        }
    }

    private void BeginTug(int pointerId)
    {
        tugActive = true;
        tugPointerId = pointerId;
        nextTugRewardTime = Time.unscaledTime + 0.35f;
        ignoreGestureUntil = Time.unscaledTime + 0.25f;
        if (activeDog != null)
        {
            activeDog.StartHeldToyTugAnimation();
        }

        ResetActivity();
        if (view != null)
        {
            view.ShowNeutralCue("Hold to tug");
        }
    }

    private void ContinueTug()
    {
        ResetActivity();
        if (Time.unscaledTime >= nextTugRewardTime)
        {
            nextTugRewardTime = Time.unscaledTime + TugRewardInterval;
            ApplyTugReward();
        }
    }

    private void EndTug()
    {
        tugActive = false;
        tugPointerId = -1;
        ignoreGestureUntil = Time.unscaledTime + 0.35f;
        if (activeDog != null)
        {
            activeDog.StopHeldToyTugAnimation();
        }

        ResetActivity();
    }

    private bool IsPointerOverHeldToy(Vector2 screenPosition)
    {
        Transform toy;
        if (activeDog == null || !activeDog.TryGetHeldToyTransform(out toy) || toy == null)
        {
            return false;
        }

        Rect toyRect;
        return TryGetScreenRect(toy, ResolveInteractionCamera(), ToyScreenPadding, out toyRect) && toyRect.Contains(screenPosition);
    }

    private static bool TryGetScreenRect(Transform root, Camera camera, float padding, out Rect rect)
    {
        rect = new Rect();
        if (root == null || camera == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screen = camera.WorldToScreenPoint(corners[i]);
            if (screen.z < 0f)
            {
                continue;
            }

            screenMin = Vector2.Min(screenMin, screen);
            screenMax = Vector2.Max(screenMax, screen);
        }

        if (screenMin.x == float.MaxValue)
        {
            return false;
        }

        rect = Rect.MinMaxRect(screenMin.x - padding, screenMin.y - padding, screenMax.x + padding, screenMax.y + padding);
        return rect.width > 1f && rect.height > 1f;
    }

    private static bool IsPettingGesture(PawPalGestureSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        if (!IsDogBodyZone(snapshot.StartZone))
        {
            return false;
        }

        return snapshot.Type == PawPalGestureType.Hold
            || snapshot.Type == PawPalGestureType.Tap
            || snapshot.RejectionReason == PawPalTrickFailureReason.InvalidGesture;
    }

    private static bool IsDogBodyZone(PawPalDogBodyZone zone)
    {
        return zone == PawPalDogBodyZone.Head
            || zone == PawPalDogBodyZone.Chest
            || zone == PawPalDogBodyZone.Back
            || zone == PawPalDogBodyZone.Belly
            || zone == PawPalDogBodyZone.PawLeft
            || zone == PawPalDogBodyZone.PawRight
            || zone == PawPalDogBodyZone.Tail;
    }

    private void UpdatePosture(PawPalTrickId trickId)
    {
        dogIsSitting = trickId == PawPalTrickId.Sit;
        if (trickId != PawPalTrickId.Lie && trickId != PawPalTrickId.Sit)
        {
            dogIsSitting = false;
        }
    }

    private void ResetActivity()
    {
        lastActivityTime = Time.unscaledTime;
        if (view != null)
        {
            view.SetBondPulse(false);
        }
    }

    private void SetStatus(string message)
    {
        if (view != null)
        {
            view.SetStatus(message);
        }
    }

    private void ShowToast(string message)
    {
        if (view != null)
        {
            view.SetStatus(message);
        }
    }

    private void HandleMicRequested()
    {
        ResetActivity();
        if (shell != null)
        {
            shell.ToggleDogInteractionMic();
        }
    }

    private Camera ResolveInteractionCamera()
    {
        if (dogCamera != null)
        {
            Camera camera = dogCamera.GetComponent<Camera>();
            if (camera != null)
            {
                return camera;
            }
        }

        return Camera.main;
    }

    private DogCycleCamera ResolveDogCamera()
    {
        Camera mainCamera = Camera.main;
        DogCycleCamera resolvedCamera = mainCamera != null ? mainCamera.GetComponent<DogCycleCamera>() : null;
        if (resolvedCamera != null)
        {
            return resolvedCamera;
        }

        resolvedCamera = FindFirstObjectByType<DogCycleCamera>();
        if (resolvedCamera != null)
        {
            return resolvedCamera;
        }

        if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null && HasSceneDogs())
        {
            return mainCamera.gameObject.AddComponent<DogCycleCamera>();
        }

        return null;
    }

    private DogRoomAgent ResolveDog(string dogId)
    {
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (!string.IsNullOrWhiteSpace(dogId))
        {
            if (runtime != null)
            {
                runtime.SelectDogById(dogId, false);
            }

            for (int i = 0; agents != null && i < agents.Length; i++)
            {
                DogRoomAgent agent = agents[i];
                if (agent != null && agent.HasExplicitDogId && string.Equals(agent.DogId, dogId, StringComparison.OrdinalIgnoreCase))
                {
                    return agent;
                }
            }
        }

        if (runtime != null && runtime.ActiveDog != null && agents != null)
        {
            string activeDogId = runtime.ActiveDog.Id;
            for (int i = 0; i < agents.Length; i++)
            {
                DogRoomAgent agent = agents[i];
                if (agent != null && agent.HasExplicitDogId && string.Equals(agent.DogId, activeDogId, StringComparison.OrdinalIgnoreCase))
                {
                    return agent;
                }
            }

            if (runtime.ActiveDogIndex >= 0 && runtime.ActiveDogIndex < agents.Length)
            {
                return agents[runtime.ActiveDogIndex];
            }
        }

        return agents != null && agents.Length > 0 ? agents[0] : null;
    }

    private static bool HasSceneDogs()
    {
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.None);
        return agents != null && agents.Length > 0;
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
