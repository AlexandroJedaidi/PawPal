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
    private const float PettingRewardCooldown = 0.5f;
    private const float PettingGestureIgnoreSeconds = 0.15f;
    private const float TugBondGain = 0.006f;
    private const float TugMoodGain = 0.008f;
    private const float TugActivityGain = 0.01f;
    private const float TugRewardInterval = 1.35f;
    private const float TugMinimumHoldDuration = 0.3f;
    private const int TugRoundsBeforeToyDrop = 2;
    private const float ToyScreenPadding = 28f;
    private const float UnexpectedExitGuardSeconds = 10f;
    private const float FailedTeachReactionDelaySeconds = 0.38f;
    private const float FailedTeachHeadTiltAngle = 14f;
    private const float FailedTeachHeadTiltDuration = 0.6f;
    private const string InteractionDebugBuildMarker = "dog-interaction-guard-v1";

    private enum InteractionTrickStartResult
    {
        Rejected,
        StartedAttempt,
        PlayedFailureReaction
    }

    private AppShellController shell;
    private PawPalDogInteractionModeView view;
    private PawPalDogInteractionDirector interactionDirector;
    private PawPalGestureRecognizer gestureRecognizer;
    private PawPalTrainingConfig trainingConfig;
    private DogCycleCamera dogCamera;
    private PawPalRoomPetHandle activePet;
    private DogRoomAgent activeDog;
    private PawPalTrickId selectedTrick = PawPalTrickId.Sit;
    private Coroutine attemptRoutine;
    private float timeoutSeconds = 15f;
    private float lastActivityTime;
    private float nextPettingRewardTime;
    private float nextTugRewardTime;
    private float ignoreGestureUntil;
    private float tugStartedAt;
    private float interactionModeEnteredAt;
    private bool active;
    private bool micListening;
    private bool dogIsSitting;
    private bool tugActive;
    private bool tugRoundCounted;
    private int tugPointerId = -1;
    private int completedTugRounds;
    private string pendingExitReason = "unknown";

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
            view.CloseRequested += HandleCloseRequested;
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
            ConfigureGestureRecognizer();
        }

        HandleToyTugInput();
        float remaining = timeoutSeconds - (Time.unscaledTime - lastActivityTime);
        if (view != null)
        {
            view.SetTimeout(remaining);
        }

        if (remaining <= 0f)
        {
            pendingExitReason = "timeout";
            ExitDogInteractionMode();
        }
    }

    private void OnDisable()
    {
        pendingExitReason = "controller_disabled";
        ExitDogInteractionMode();
    }

    public bool TryEnterDogInteractionMode(string dogId, bool keepMicListening)
    {
        activePet = ResolvePet(dogId);
        activeDog = activePet != null ? activePet.DogAgent : null;
        if (activePet == null || !activePet.IsValid)
        {
            ShowToast("No active pet for interaction mode.");
            return false;
        }

        dogCamera = ResolveDogCamera();
        if (activeDog != null)
        {
            if (dogCamera == null || !dogCamera.EnterDogInteractionMode(activeDog))
            {
                ShowToast("Dog interaction needs a dog camera.");
                return false;
            }
        }
        else
        {
            FocusCatInteractionCamera();
        }

        micListening = keepMicListening;
        dogIsSitting = false;
        tugRoundCounted = false;
        completedTugRounds = 0;
        tugStartedAt = 0f;
        interactionModeEnteredAt = Time.unscaledTime;
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

        Debug.Log(
            "[DogInteractionMode] Enter marker=" + InteractionDebugBuildMarker
            + " dog=" + activePet.DisplayName
            + " timeout=" + timeoutSeconds.ToString("F2"),
            this);

        active = true;
        ApplyInteractionVocalContext(activeDog, DogVocalContext.InteractionBoosted);
        if (view != null)
        {
            view.Show(dogState, micListening, timeoutSeconds);
            if (activeDog != null)
            {
                view.SetTrackedDog(activeDog, ResolveInteractionCamera());
            }
            else
            {
                view.SetTrackedPet(activePet.RootTransform, activePet.FocusTransform, ResolveInteractionCamera());
            }
        }

        if (gestureRecognizer != null)
        {
            ConfigureGestureRecognizer();
            gestureRecognizer.SetActive(true);
        }

        string failureReason;
        if (activeDog != null && interactionDirector != null && !interactionDirector.TryCallDogToInteraction(activeDog, ResolveInteractionCamera(), out failureReason))
        {
            SetStatus(string.IsNullOrWhiteSpace(failureReason) ? "Your dog is busy right now." : failureReason);
            pendingExitReason = "director_start_failed";
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

            PawPalRoomPetHandle resolvedPet = ResolvePet(command.DogId);
            if (resolvedPet != null && resolvedPet.IsValid)
            {
                if (activeDog != resolvedPet.DogAgent)
                {
                    ApplyInteractionVocalContext(activeDog, DogVocalContext.Ambient);
                }

                activePet = resolvedPet;
                activeDog = resolvedPet.DogAgent;
                ApplyInteractionVocalContext(activeDog, DogVocalContext.InteractionBoosted);
                if (dogCamera != null && activeDog != null)
                {
                    dogCamera.EnterDogInteractionMode(activeDog);
                }
                else if (activeDog == null)
                {
                    FocusCatInteractionCamera();
                }

                if (view != null)
                {
                    if (activeDog != null)
                    {
                        view.SetTrackedDog(activeDog, ResolveInteractionCamera());
                    }
                    else
                    {
                        view.SetTrackedPet(activePet.RootTransform, activePet.FocusTransform, ResolveInteractionCamera());
                    }
                }
            }
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(command.TrickId);
        string trickLabel = definition != null ? definition.DisplayName : command.TrickLabel;
        InteractionTrickStartResult startResult = StartTrickAttempt(command.TrickId, true, out message);
        if (startResult == InteractionTrickStartResult.StartedAttempt)
        {
            message = "Training " + (string.IsNullOrWhiteSpace(trickLabel) ? command.TrickId.ToString() : trickLabel) + ".";
            return true;
        }

        if (startResult == InteractionTrickStartResult.PlayedFailureReaction)
        {
            message = string.Empty;
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

        bool unknownExitReason = string.IsNullOrWhiteSpace(pendingExitReason) || pendingExitReason == "unknown";
        bool withinUnexpectedExitGuardWindow = active
            && (Time.unscaledTime - interactionModeEnteredAt) < UnexpectedExitGuardSeconds;
        bool directorRunning = interactionDirector != null && interactionDirector.IsRunning;
        if (unknownExitReason)
        {
            string unexpectedExitStack = new System.Diagnostics.StackTrace(1, true).ToString();
            if (directorRunning || withinUnexpectedExitGuardWindow)
            {
                Debug.LogWarning(
                    "[DogInteractionMode] Ignoring unexpected exit."
                    + " directorRunning=" + directorRunning
                    + " enteredAgo=" + (Time.unscaledTime - interactionModeEnteredAt).ToString("F2")
                    + "\n" + unexpectedExitStack,
                    this);
                return;
            }

            Debug.LogWarning("[DogInteractionMode] Unexpected exit stack:\n" + unexpectedExitStack, this);
        }

        Debug.Log("[DogInteractionMode] Exit reason=" + pendingExitReason
            + " active=" + active
            + " dog=" + (activeDog != null ? activeDog.name : "null")
            + " directorRunning=" + directorRunning, this);

        active = false;
        tugActive = false;
        tugRoundCounted = false;
        tugPointerId = -1;
        completedTugRounds = 0;
        tugStartedAt = 0f;
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

        if (activePet != null && activePet.IsValid)
        {
            activePet.StopHeldToyTugAnimation();
        }

        ApplyInteractionVocalContext(activeDog, DogVocalContext.Ambient);
        activePet = null;
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

        pendingExitReason = "unknown";
    }

    public void RequestShellExit(string reason)
    {
        pendingExitReason = string.IsNullOrWhiteSpace(reason) ? "shell_request" : reason;
        ExitDogInteractionMode();
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
        if (StartTrickAttempt(trickId, false, out message) == InteractionTrickStartResult.Rejected)
        {
            TrySetStatus(message);
        }
    }

    private InteractionTrickStartResult StartTrickAttempt(PawPalTrickId trickId, bool fromVoice, out string message)
    {
        message = string.Empty;
        if (!active)
        {
            return InteractionTrickStartResult.Rejected;
        }

        if (attemptRoutine != null)
        {
            return InteractionTrickStartResult.Rejected;
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        if (definition == null || dogState == null || activePet == null || !activePet.IsValid)
        {
            message = "Training needs an active pet.";
            return InteractionTrickStartResult.Rejected;
        }

        if (activePet.HasHeldToy)
        {
            message = "Play tug first; your pet is holding a toy.";
            return InteractionTrickStartResult.Rejected;
        }

        PawPalTrickFailureReason failureReason = GetInteractionAttemptFailure(dogState, definition, fromVoice);
        if (failureReason != PawPalTrickFailureReason.None)
        {
            selectedTrick = trickId;
            attemptRoutine = StartCoroutine(PlayFailedTeachReactionRoutine(failureReason));
            message = string.Empty;
            ResetActivity();
            return InteractionTrickStartResult.PlayedFailureReaction;
        }

        selectedTrick = trickId;
        attemptRoutine = StartCoroutine(TrickAttemptRoutine(definition, fromVoice));
        message = "Training " + definition.DisplayName + ".";
        ResetActivity();
        return InteractionTrickStartResult.StartedAttempt;
    }

    private IEnumerator TrickAttemptRoutine(PawPalTrickDefinition definition, bool fromVoice)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        bool posePrerequisite = definition.Id != PawPalTrickId.Lie || dogIsSitting;
        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dogState, definition, trainingConfig, fromVoice, false, posePrerequisite);
        if (runtime != null)
        {
            runtime.RecordTrickTrainingResult(result);
        }

        if (result != null && result.Success && activePet != null && activePet.IsValid)
        {
            yield return StartCoroutine(activePet.PlayInteractionTrainingTrick(definition, true, ResolveInteractionCamera()));
            UpdatePosture(definition.Id);

            if (view != null)
            {
                if (ShouldShowProgressCue(result))
                {
                    view.ShowTrainingProgressCue(definition.DisplayName, result.PreviousProgress01, result.CurrentProgress01);
                    view.ShowProgressBurst();
                }
                else if (ShouldShowUnderstoodCue(result))
                {
                    view.ShowUnderstoodCue(definition.DisplayName);
                }
            }
        }
        else
        {
            if (result != null && IsFrustrationEligibleFailureReason(result.Reason))
            {
                yield return StartCoroutine(PlayFailedTeachReactionRoutine(result.Reason));
            }
        }

        ResetActivity();
        attemptRoutine = null;
    }

    private static bool IsFrustrationEligibleFailureReason(PawPalTrickFailureReason reason)
    {
        return reason == PawPalTrickFailureReason.RandomMiss
            || reason == PawPalTrickFailureReason.LowFocus
            || reason == PawPalTrickFailureReason.LowBond
            || reason == PawPalTrickFailureReason.LowMood
            || reason == PawPalTrickFailureReason.LowEnergy
            || reason == PawPalTrickFailureReason.Hungry
            || reason == PawPalTrickFailureReason.Thirsty
            || reason == PawPalTrickFailureReason.MissingPrerequisite;
    }

    private static bool ShouldPlayInteractionMissIdle(PawPalTrickAttemptResult result, DogRoomAgent dog)
    {
        return result != null
            && dog != null
            && !result.Success
            && result.Reason == PawPalTrickFailureReason.RandomMiss;
    }

    private static bool ShouldShowProgressCue(PawPalTrickAttemptResult result)
    {
        return result != null
            && result.Success
            && result.MadeProgress;
    }

    private static bool ShouldShowUnderstoodCue(PawPalTrickAttemptResult result)
    {
        return result != null
            && result.Success
            && !result.MadeProgress;
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
        ignoreGestureUntil = Mathf.Max(ignoreGestureUntil, Time.unscaledTime + PettingGestureIgnoreSeconds);
        if (Time.unscaledTime < nextPettingRewardTime)
        {
            return;
        }

        nextPettingRewardTime = Time.unscaledTime + PettingRewardCooldown;
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.ApplyActiveDogInteractionBond(PettingBondGain, PettingMoodGain, PettingActivityGain);
        }

        if (activePet != null && activePet.IsValid)
        {
            activePet.TryPlayImmediateInteractionPantingVocal();
            activePet.TryPlayPettingReaction();
        }

        if (view != null)
        {
            view.ShowPettingBurst();
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

    private IEnumerator PlayFailedTeachReactionRoutine(PawPalTrickFailureReason reason)
    {
        if (activePet != null && activePet.IsValid)
        {
            activePet.TryPlayImmediateInteractionAnnoyedVocal();

            DogCameraAttention attention = activeDog != null ? ResolveDogAttention(activeDog) : null;
            if (attention != null)
            {
                attention.RequestHeadTilt(FailedTeachHeadTiltAngle, FailedTeachHeadTiltDuration);
            }
        }

        if (view != null)
        {
            view.ShowFailedTeachBurst();
        }

        yield return new WaitForSecondsRealtime(FailedTeachReactionDelaySeconds);
        ResetActivity();
        attemptRoutine = null;
    }

    private void HandleToyTugInput()
    {
        if (activePet == null || !activePet.IsValid || !activePet.HasHeldToy)
        {
            // Only stop tug visuals when a tug interaction was actually active.
            // Calling StopHeldToyTugAnimation() every frame on the no-toy path forces Move=false
            // and breaks whistle locomotion by kicking the dog back to idle mid-approach.
            if (activePet != null && activePet.IsValid && (tugActive || tugPointerId != -1 || tugRoundCounted || completedTugRounds > 0))
            {
                activePet.StopHeldToyTugAnimation();
            }

            tugActive = false;
            tugRoundCounted = false;
            tugPointerId = -1;
            tugStartedAt = 0f;
            completedTugRounds = 0;
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
        tugStartedAt = Time.unscaledTime;
        nextTugRewardTime = Time.unscaledTime + 0.35f;
        ignoreGestureUntil = Time.unscaledTime + 0.25f;
        tugRoundCounted = false;
        if (activePet != null && activePet.IsValid)
        {
            activePet.StartHeldToyTugAnimation();
        }

        ResetActivity();
        if (view != null)
        {
            view.ShowNeutralCue("Tug " + (completedTugRounds + 1) + "/" + TugRoundsBeforeToyDrop);
        }
    }

    private void ContinueTug()
    {
        ResetActivity();
        if (!tugRoundCounted && Time.unscaledTime - tugStartedAt >= TugMinimumHoldDuration)
        {
            tugRoundCounted = true;
            completedTugRounds++;
            if (completedTugRounds >= TugRoundsBeforeToyDrop)
            {
                completedTugRounds = 0;
                tugStartedAt = 0f;
                tugActive = false;
                tugPointerId = -1;
                ignoreGestureUntil = Time.unscaledTime + 0.35f;
                if (activePet != null && activePet.IsValid)
                {
                    activePet.StopHeldToyTugAnimation();
                }

                if (attemptRoutine == null)
                {
                    attemptRoutine = StartCoroutine(FinishTugSequenceRoutine());
                }

                ResetActivity();
                return;
            }

            if (view != null)
            {
                view.ShowNeutralCue("Tug " + (completedTugRounds + 1) + "/" + TugRoundsBeforeToyDrop);
            }
        }

        if (Time.unscaledTime >= nextTugRewardTime)
        {
            nextTugRewardTime = Time.unscaledTime + TugRewardInterval;
            ApplyTugReward();
        }
    }

    private void EndTug()
    {
        tugActive = false;
        tugRoundCounted = false;
        tugPointerId = -1;
        ignoreGestureUntil = Time.unscaledTime + 0.35f;
        if (activePet != null && activePet.IsValid)
        {
            activePet.StopHeldToyTugAnimation();
        }

        tugStartedAt = 0f;
        ResetActivity();
    }

    private IEnumerator FinishTugSequenceRoutine()
    {
        if (view != null)
        {
            view.ShowNeutralCue("Drop it");
        }

        if (activePet != null && activePet.IsValid)
        {
            yield return StartCoroutine(activePet.PutDownHeldToyForInteraction());
        }

        if (view != null && activePet != null && activePet.IsValid && !activePet.HasHeldToy)
        {
            view.ShowUnderstoodCue("Toy down");
        }

        ResetActivity();
        attemptRoutine = null;
    }

    private PawPalTrickFailureReason GetInteractionAttemptFailure(PawPalDogState dogState, PawPalTrickDefinition definition, bool fromVoice)
    {
        if (dogState == null || definition == null)
        {
            return PawPalTrickFailureReason.Busy;
        }

        if (PawPalTrainingModeView.IsLockedByTrainingUiSequence(dogState, definition.Id))
        {
            return PawPalTrickFailureReason.MissingPrerequisite;
        }

        PawPalTrickFailureReason availabilityFailure = PawPalTrickProgressionService.GetAvailabilityFailure(dogState, definition, false);
        if (availabilityFailure != PawPalTrickFailureReason.None)
        {
            return availabilityFailure;
        }

        if (!fromVoice && definition.Id == PawPalTrickId.Lie && !dogIsSitting)
        {
            return PawPalTrickFailureReason.MissingPrerequisite;
        }

        return PawPalTrickFailureReason.None;
    }

    private bool IsPointerOverHeldToy(Vector2 screenPosition)
    {
        Transform toy;
        if (activePet == null || !activePet.IsValid || !activePet.TryGetHeldToyTransform(out toy) || toy == null)
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

        return snapshot.Type == PawPalGestureType.PetStroke
            && IsDogBodyZone(snapshot.StartZone)
            && IsDogBodyZone(snapshot.EndZone);
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

    private void TrySetStatus(string message)
    {
        if (!ShouldShowInteractionStatus(message))
        {
            return;
        }

        SetStatus(message);
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

    private void HandleCloseRequested()
    {
        pendingExitReason = "close_button";
        ExitDogInteractionMode();
    }

    private void ConfigureGestureRecognizer()
    {
        if (gestureRecognizer == null)
        {
            return;
        }

        if (activeDog != null)
        {
            gestureRecognizer.Configure(activeDog, ResolveInteractionCamera(), trainingConfig != null ? trainingConfig.GestureLeniency : 1f);
            return;
        }

        gestureRecognizer.Configure(activePet != null ? activePet.RootTransform : null, ResolveInteractionCamera(), trainingConfig != null ? trainingConfig.GestureLeniency : 1f);
    }

    private void FocusCatInteractionCamera()
    {
        if (activePet == null || !activePet.IsValid || activePet.RootTransform == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        DogCycleCamera runtimeDogCamera = mainCamera.GetComponent<DogCycleCamera>();
        if (runtimeDogCamera != null)
        {
            runtimeDogCamera.enabled = false;
        }

        PawPalPetFollowCamera followCamera = mainCamera.GetComponent<PawPalPetFollowCamera>();
        if (followCamera == null)
        {
            followCamera = mainCamera.gameObject.AddComponent<PawPalPetFollowCamera>();
        }

        followCamera.enabled = true;
        followCamera.Focus(activePet.FocusTransform, activePet.HomeCameraOffset, true);
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

    private static DogCameraAttention ResolveDogAttention(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return null;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            return attention;
        }

        return dog.gameObject.AddComponent<DogCameraAttention>();
    }

    private PawPalRoomPetHandle ResolvePet(string dogId)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (!string.IsNullOrWhiteSpace(dogId))
        {
            if (runtime != null)
            {
                runtime.SelectDogById(dogId, false);
            }
        }

        IntroPetSpecies preferredSpecies = runtime != null && runtime.ActiveDog != null
            ? runtime.ActivePetSpecies
            : IntroPetSpecies.Dog;
        return PawPalRoomPetRuntime.ResolvePetById(dogId, preferredSpecies);
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

    private static bool ShouldShowInteractionStatus(string message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && !string.Equals(message, "Training is busy.", StringComparison.Ordinal);
    }
}
