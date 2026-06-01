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
    private const float PettingGestureIgnoreSeconds = 0.35f;
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
            + " dog=" + activeDog.name
            + " timeout=" + timeoutSeconds.ToString("F2"),
            this);

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
            SetStatus(message);
        }
    }

    private InteractionTrickStartResult StartTrickAttempt(PawPalTrickId trickId, bool fromVoice, out string message)
    {
        message = "Training is busy.";
        if (!active || attemptRoutine != null)
        {
            return InteractionTrickStartResult.Rejected;
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        if (definition == null || dogState == null || activeDog == null)
        {
            message = "Training needs an active dog.";
            return InteractionTrickStartResult.Rejected;
        }

        if (activeDog.HasHeldToy)
        {
            message = "Play tug first; your dog is holding a toy.";
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

        if (activeDog != null)
        {
            activeDog.TryPlayImmediateInteractionPantingVocal();
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
        if (activeDog != null)
        {
            activeDog.TryPlayImmediateInteractionAnnoyedVocal();

            DogCameraAttention attention = ResolveDogAttention(activeDog);
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
        if (activeDog == null || !activeDog.HasHeldToy)
        {
            // Only stop tug visuals when a tug interaction was actually active.
            // Calling StopHeldToyTugAnimation() every frame on the no-toy path forces Move=false
            // and breaks whistle locomotion by kicking the dog back to idle mid-approach.
            if (activeDog != null && (tugActive || tugPointerId != -1 || tugRoundCounted || completedTugRounds > 0))
            {
                activeDog.StopHeldToyTugAnimation();
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
        if (activeDog != null)
        {
            activeDog.StartHeldToyTugAnimation();
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
                if (activeDog != null)
                {
                    activeDog.StopHeldToyTugAnimation();
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
        if (activeDog != null)
        {
            activeDog.StopHeldToyTugAnimation();
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

        if (activeDog != null)
        {
            yield return StartCoroutine(activeDog.PutDownHeldToyForInteraction());
        }

        if (view != null && activeDog != null && !activeDog.HasHeldToy)
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

    private void HandleCloseRequested()
    {
        pendingExitReason = "close_button";
        ExitDogInteractionMode();
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
