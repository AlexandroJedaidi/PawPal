using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PawPalDogInteractionModeOptions
{
    public static PawPalDogInteractionModeOptions RuntimeDefault
    {
        get
        {
            return new PawPalDogInteractionModeOptions
            {
                AllowMic = true,
                AllowTrainingGestures = true,
                AllowRuntimeProgressionRewards = true,
                ShowMicButton = true,
                ShowTrainingProgressCues = true,
                PreviewOnly = false
            };
        }
    }

    public static PawPalDogInteractionModeOptions PreviewOnlyDefault
    {
        get
        {
            return new PawPalDogInteractionModeOptions
            {
                AllowMic = false,
                AllowTrainingGestures = false,
                AllowRuntimeProgressionRewards = false,
                ShowMicButton = false,
                ShowTrainingProgressCues = false,
                PreviewOnly = true
            };
        }
    }

    public bool AllowMic { get; set; }
    public bool AllowTrainingGestures { get; set; }
    public bool AllowRuntimeProgressionRewards { get; set; }
    public bool ShowMicButton { get; set; }
    public bool ShowTrainingProgressCues { get; set; }
    public bool PreviewOnly { get; set; }
}

[DisallowMultipleComponent]
public sealed class PawPalDogInteractionModeController : MonoBehaviour
{
    private const float PettingBondGain = 0.008f;
    private const float PettingMoodGain = 0.012f;
    private const float PettingActivityGain = 0.006f;
    private const float PettingRewardCooldown = 5f;
    private const float PettingGestureIgnoreSeconds = 0.15f;
    private const float PettingHoldMinDuration = 0.45f;
    private const float PettingStrokeMinPathPixels = 24f;
    private const float PettingScreenPadding = 18f;
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
    private const float PreviewPettingGestureLeniencyMultiplier = 1.35f;
    private const float PreviewPettingBoundsPaddingMultiplier = 1.75f;
    private const float InteractionFaceCameraDuration = 0.4f;
    private const float InteractionFaceCameraMaxWaitSeconds = 6f;
    private const float InteractionLookRefreshIntervalSeconds = 0.2f;
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
    private PawPalDogInteractionModeOptions modeOptions;
    private PawPalTrickId selectedTrick = PawPalTrickId.Sit;
    private Coroutine attemptRoutine;
    private Coroutine faceCameraRoutine;
    private float timeoutSeconds = 15f;
    private float lastActivityTime;
    private float nextPettingRewardTime;
    private float nextInteractionLookRefreshTime;
    private float nextTugRewardTime;
    private float ignoreGestureUntil;
    private float tugStartedAt;
    private float interactionModeEnteredAt;
    private float pettingStartedAt;
    private float pettingPathPixels;
    private bool active;
    private bool micListening;
    private bool dogIsSitting;
    private bool pettingPointerActive;
    private bool tugActive;
    private bool tugRoundCounted;
    private int pettingPointerId = -2;
    private int tugPointerId = -1;
    private int completedTugRounds;
    private string pendingExitReason = "unknown";
    private Vector2 lastPettingScreenPosition;

    public event Action InteractionExited;

    public bool IsActive
    {
        get { return active; }
    }

    public void Initialize(AppShellController ownerShell, PawPalDogInteractionModeView interactionView)
    {
        shell = ownerShell;
        view = interactionView;
        trainingConfig = PawPalTrainingConfig.CreateRuntimeDefault();
        modeOptions = PawPalDogInteractionModeOptions.RuntimeDefault;

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

        HandleContinuousPettingInput();
        HandleToyTugInput();
        RefreshInteractionCameraLookIfNeeded();
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
        PawPalRoomPetHandle resolvedPet = ResolvePet(dogId, true);
        return TryEnterDogInteractionMode(resolvedPet, keepMicListening, PawPalDogInteractionModeOptions.RuntimeDefault);
    }

    public bool TryEnterDogInteractionMode(PawPalRoomPetHandle pet, bool keepMicListening, PawPalDogInteractionModeOptions options)
    {
        modeOptions = options ?? PawPalDogInteractionModeOptions.RuntimeDefault;
        activePet = pet;
        activeDog = activePet != null ? activePet.DogAgent : null;
        if (activePet == null || !activePet.IsValid)
        {
            ShowToast("No active pet for interaction mode.");
            return false;
        }

        activePet.PrepareForPlayerInteraction(true);

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

        micListening = modeOptions.AllowMic && keepMicListening;
        dogIsSitting = false;
        tugRoundCounted = false;
        completedTugRounds = 0;
        tugStartedAt = 0f;
        interactionModeEnteredAt = Time.unscaledTime;
        ResetActivity();

        PawPalGameRuntime runtime = modeOptions.PreviewOnly ? null : PawPalGameRuntime.Instance;
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
            view.Show(dogState, micListening, timeoutSeconds, modeOptions);
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

        if (faceCameraRoutine != null)
        {
            StopCoroutine(faceCameraRoutine);
        }

        faceCameraRoutine = StartCoroutine(EnsurePetFacesInteractionCameraRoutine(activePet, activeDog));

        return true;
    }

    public void SetMicListening(bool listening)
    {
        micListening = (modeOptions == null || modeOptions.AllowMic) && listening;
        if (view != null)
        {
            view.SetMicListening(micListening);
        }
    }

    public void ShowUnrecognizedCommandReaction()
    {
        if (!active)
        {
            return;
        }

        if (view != null && (modeOptions == null || modeOptions.ShowTrainingProgressCues))
        {
            view.ShowFailedTeachBurst();
        }

        if (activePet != null && activePet.IsValid)
        {
            activePet.TryPlayImmediateInteractionAnnoyedVocal();
        }

        ResetActivity();
    }

    public bool TryPerformVoiceTrick(PawPalResolvedVoiceCommand command, out string message)
    {
        message = "Interaction mode is not active.";
        if (!active || command == null || command.Type != PawPalResolvedVoiceCommandType.PerformTrick)
        {
            return false;
        }

        if (modeOptions != null && !modeOptions.AllowTrainingGestures)
        {
            message = "Training is disabled in preview mode.";
            return false;
        }

        if (!string.IsNullOrEmpty(command.DogId))
        {
            PawPalGameRuntime runtime = modeOptions != null && modeOptions.PreviewOnly ? null : PawPalGameRuntime.Instance;
            if (runtime != null)
            {
                runtime.SelectDogById(command.DogId, false);
            }

            PawPalRoomPetHandle resolvedPet = ResolvePet(command.DogId, runtime != null);
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
        CancelContinuousPettingTracking();
        completedTugRounds = 0;
        tugStartedAt = 0f;
        if (attemptRoutine != null)
        {
            StopCoroutine(attemptRoutine);
            attemptRoutine = null;
        }

        if (faceCameraRoutine != null)
        {
            StopCoroutine(faceCameraRoutine);
            faceCameraRoutine = null;
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
        modeOptions = PawPalDogInteractionModeOptions.RuntimeDefault;

        Action exited = InteractionExited;
        if (exited != null)
        {
            exited();
        }
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
        if (IsPettingGesture(snapshot)
            || IsPreviewPettingGesture(snapshot))
        {
            if (snapshot.DurationSeconds >= PettingHoldMinDuration)
            {
                ApplyPettingReward();
            }
            return;
        }

        if (modeOptions != null && !modeOptions.AllowTrainingGestures)
        {
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

        if (modeOptions != null && !modeOptions.AllowTrainingGestures)
        {
            message = "Training is disabled in preview mode.";
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
        PawPalGameRuntime runtime = modeOptions != null && modeOptions.PreviewOnly ? null : PawPalGameRuntime.Instance;
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

            if (view != null && modeOptions != null && modeOptions.ShowTrainingProgressCues)
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
        bool rewardReady = Time.unscaledTime >= nextPettingRewardTime;
        if (!rewardReady)
        {
            return;
        }

        nextPettingRewardTime = Time.unscaledTime + PettingRewardCooldown;
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null && (modeOptions == null || modeOptions.AllowRuntimeProgressionRewards))
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
        if (runtime != null && (modeOptions == null || modeOptions.AllowRuntimeProgressionRewards))
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

        if (view != null && (modeOptions == null || modeOptions.ShowTrainingProgressCues))
        {
            view.ShowFailedTeachBurst();
        }

        yield return new WaitForSecondsRealtime(FailedTeachReactionDelaySeconds);
        ResetActivity();
        attemptRoutine = null;
    }

    private void HandleToyTugInput()
    {
        if (modeOptions != null && !modeOptions.AllowTrainingGestures)
        {
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

    private void HandleContinuousPettingInput()
    {
        if (activePet == null || !activePet.IsValid || Time.unscaledTime < ignoreGestureUntil)
        {
            CancelContinuousPettingTracking();
            return;
        }

        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleContinuousPettingTouch();
            return;
        }

        HandleContinuousPettingMouse();
    }

    private void HandleContinuousPettingMouse()
    {
        Vector2 position = Input.mousePosition;
        if (!pettingPointerActive)
        {
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1) && IsPointerOverActivePet(position))
            {
                BeginContinuousPetting(-1, position);
            }

            return;
        }

        if (pettingPointerId != -1)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            ContinueContinuousPetting(position);
        }
        else
        {
            CancelContinuousPettingTracking();
        }
    }

    private void HandleContinuousPettingTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (!pettingPointerActive)
            {
                if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId) && IsPointerOverActivePet(touch.position))
                {
                    BeginContinuousPetting(touch.fingerId, touch.position);
                    return;
                }

                continue;
            }

            if (touch.fingerId != pettingPointerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                ContinueContinuousPetting(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                CancelContinuousPettingTracking();
            }

            return;
        }

        if (pettingPointerActive && pettingPointerId >= 0)
        {
            CancelContinuousPettingTracking();
        }
    }

    private void BeginContinuousPetting(int pointerId, Vector2 screenPosition)
    {
        pettingPointerActive = true;
        pettingPointerId = pointerId;
        pettingStartedAt = Time.unscaledTime;
        pettingPathPixels = 0f;
        lastPettingScreenPosition = screenPosition;
    }

    private void ContinueContinuousPetting(Vector2 screenPosition)
    {
        if (!IsPointerOverActivePet(screenPosition))
        {
            CancelContinuousPettingTracking();
            return;
        }

        pettingPathPixels += Vector2.Distance(lastPettingScreenPosition, screenPosition);
        lastPettingScreenPosition = screenPosition;

        float heldDuration = Time.unscaledTime - pettingStartedAt;
        if (heldDuration >= PettingHoldMinDuration && pettingPathPixels >= PettingStrokeMinPathPixels)
        {
            ApplyPettingReward();
            CancelContinuousPettingTracking();
        }
    }

    private void CancelContinuousPettingTracking()
    {
        pettingPointerActive = false;
        pettingPointerId = -2;
        pettingStartedAt = 0f;
        pettingPathPixels = 0f;
        lastPettingScreenPosition = Vector2.zero;
    }

    private bool IsPointerOverActivePet(Vector2 screenPosition)
    {
        Transform petRoot = activePet != null ? activePet.RootTransform : null;
        Rect petRect;
        return petRoot != null
            && TryGetScreenRect(petRoot, ResolveInteractionCamera(), PettingScreenPadding, out petRect)
            && petRect.Contains(screenPosition);
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

    private static bool IsPreviewPettingGesture(PawPalGestureSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        if (!IsDogBodyZone(snapshot.StartZone) || !IsDogBodyZone(snapshot.EndZone))
        {
            return false;
        }

        return snapshot.Type == PawPalGestureType.HorizontalSwipe
            || snapshot.Type == PawPalGestureType.DragFromBodyPart
            || snapshot.Type == PawPalGestureType.PetStroke;
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
        if (modeOptions != null && !modeOptions.AllowMic)
        {
            return;
        }

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

        float gestureLeniency = trainingConfig != null ? trainingConfig.GestureLeniency : 1f;
        float paddingMultiplier = 1f;
        if (modeOptions != null && modeOptions.PreviewOnly)
        {
            gestureLeniency *= PreviewPettingGestureLeniencyMultiplier;
            paddingMultiplier = PreviewPettingBoundsPaddingMultiplier;
        }

        if (activeDog != null)
        {
            gestureRecognizer.Configure(activeDog.transform, ResolveInteractionCamera(), gestureLeniency, paddingMultiplier);
            return;
        }

        gestureRecognizer.Configure(activePet != null ? activePet.RootTransform : null, ResolveInteractionCamera(), gestureLeniency, paddingMultiplier);
    }

    private IEnumerator EnsurePetFacesInteractionCameraRoutine(PawPalRoomPetHandle pet, DogRoomAgent dog)
    {
        float startedAt = Time.unscaledTime;
        while (active
            && pet == activePet
            && pet != null
            && pet.IsValid
            && dog != null
            && interactionDirector != null
            && interactionDirector.IsRunning
            && Time.unscaledTime - startedAt < InteractionFaceCameraMaxWaitSeconds)
        {
            yield return null;
        }

        if (!active || pet == null || pet != activePet || !pet.IsValid)
        {
            faceCameraRoutine = null;
            yield break;
        }

        Camera interactionCamera = ResolveInteractionCamera();
        if (interactionCamera != null)
        {
            yield return StartCoroutine(pet.FaceTarget(interactionCamera.transform, InteractionFaceCameraDuration));
        }

        if (dog != null)
        {
            DogCameraAttention attention = ResolveDogAttention(dog);
            if (attention != null)
            {
                attention.RequestCameraAttention(Mathf.Max(2f, timeoutSeconds));
            }
        }

        faceCameraRoutine = null;
    }

    private void RefreshInteractionCameraLookIfNeeded()
    {
        if (!active || activePet == null || !activePet.IsValid || Time.unscaledTime < nextInteractionLookRefreshTime)
        {
            return;
        }

        nextInteractionLookRefreshTime = Time.unscaledTime + InteractionLookRefreshIntervalSeconds;
        Camera interactionCamera = ResolveInteractionCamera();
        if (interactionCamera == null)
        {
            return;
        }

        if (activeDog != null)
        {
            DogCameraAttention attention = ResolveDogAttention(activeDog);
            if (attention != null)
            {
                attention.RequestCameraAttention(Mathf.Max(0.4f, InteractionLookRefreshIntervalSeconds * 2f));
            }
        }
        else if (activePet.CatAgent != null)
        {
            activePet.CatAgent.RequestInteractionCameraLook(interactionCamera.transform, Mathf.Max(0.4f, InteractionLookRefreshIntervalSeconds * 2f));
        }
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

    private PawPalRoomPetHandle ResolvePet(string dogId, bool selectRuntimeDog)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (selectRuntimeDog && !string.IsNullOrWhiteSpace(dogId))
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
