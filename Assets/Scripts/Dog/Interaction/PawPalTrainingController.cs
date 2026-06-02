using System;
using System.Collections;
using UnityEngine;

public enum PawPalTrainingState
{
    Idle,
    Attention,
    GestureDetected,
    AttemptingTrick,
    TrickSucceeded,
    TrickFailed,
    AwaitingCommandTeaching,
    ListeningForVoice,
    Practicing,
    Learned,
    Praise,
    BreakNeeded
}

[DisallowMultipleComponent]
public sealed class PawPalTrainingController : MonoBehaviour
{
    private const string DefaultFeedbackText = "";

    private enum TrainingDogPosture
    {
        Standing,
        Sitting,
        Lying
    }

    private PawPalTrainingModeView view;
    private PawPalGestureRecognizer gestureRecognizer;
    private PawPalTrainingConfig config;
    private Coroutine attemptRoutine;
    private PawPalTrickId selectedTrick = PawPalTrickId.Sit;
    private PawPalTrainingState state = PawPalTrainingState.Idle;
    private TrainingDogPosture lastPosture = TrainingDogPosture.Standing;
    private string feedbackText = string.Empty;
    private string gestureDebug = string.Empty;
    private bool canPraise;

    public bool IsOpen
    {
        get { return view != null && view.IsVisible; }
    }

    public PawPalTrainingState State
    {
        get { return state; }
    }

    public bool CanPraiseSelectedTrick
    {
        get { return canPraise; }
    }

    public void Initialize(PawPalTrainingModeView trainingView)
    {
        view = trainingView;
        config = PawPalTrainingConfig.CreateRuntimeDefault();

        gestureRecognizer = GetComponent<PawPalGestureRecognizer>();
        if (gestureRecognizer == null)
        {
            gestureRecognizer = gameObject.AddComponent<PawPalGestureRecognizer>();
        }

        gestureRecognizer.GestureRecognized += HandleGestureRecognized;
        if (view != null)
        {
            view.CloseRequested += Close;
            view.TrickSelected += HandleTrickSelected;
        }
    }

    private void OnDisable()
    {
        if (gestureRecognizer != null)
        {
            gestureRecognizer.SetActive(false);
        }
    }

    public void Open(PawPalTrickId initialTrick)
    {
        selectedTrick = initialTrick;
        feedbackText = DefaultFeedbackText;
        gestureDebug = string.Empty;
        state = PawPalTrainingState.Attention;
        canPraise = false;

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        if (dog != null)
        {
            PawPalTrickCatalog.EnsureDogTrickData(dog);
        }

        ConfigureGestureRecognizer();
        if (gestureRecognizer != null)
        {
            gestureRecognizer.SetActive(true);
        }

        if (view != null)
        {
            view.Show(dog, selectedTrick);
            view.Refresh(dog, selectedTrick, feedbackText, gestureDebug, canPraise);
            view.SetBusy(false);
        }
    }

    public void Close()
    {
        if (attemptRoutine != null)
        {
            StopCoroutine(attemptRoutine);
            attemptRoutine = null;
        }

        if (gestureRecognizer != null)
        {
            gestureRecognizer.SetActive(false);
        }

        state = PawPalTrainingState.Idle;
        if (view != null)
        {
            view.Hide();
        }
    }

    public void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        ConfigureGestureRecognizer();
        if (view != null)
        {
            view.Refresh(dog, selectedTrick, feedbackText, gestureDebug, canPraise);
        }
    }

    private void HandleTrickSelected(PawPalTrickId trickId)
    {
        selectedTrick = trickId;
        canPraise = false;
        feedbackText = DefaultFeedbackText;
        Refresh();
    }

    private void HandlePraiseRequested()
    {
        if (!canPraise)
        {
            return;
        }

        canPraise = false;
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.ActiveDog == null)
        {
            Refresh();
            return;
        }

        state = PawPalTrainingState.Praise;
        runtime.PraiseActiveDogForTrick(selectedTrick);
        feedbackText = runtime.ActiveDog.DisplayName + " liked the praise.";
        Refresh();
    }

    private void HandleGestureRecognized(PawPalGestureSnapshot snapshot)
    {
        if (!IsOpen || snapshot == null || attemptRoutine != null)
        {
            return;
        }

        gestureDebug = snapshot.DebugText + " (" + snapshot.StartZone + " -> " + snapshot.EndZone + ")";
        if (snapshot.Type == PawPalGestureType.PetStroke)
        {
            state = PawPalTrainingState.TrickFailed;
            feedbackText = "Use a training cue instead of petting.";
            canPraise = false;
            Refresh();
            return;
        }

        if (snapshot.RejectionReason != PawPalTrickFailureReason.None)
        {
            state = PawPalTrainingState.TrickFailed;
            feedbackText = "Try a clearer cue near your dog.";
            canPraise = false;
            Refresh();
            return;
        }

        PawPalTrickId trickId = PawPalTrickGestureMapper.MapGestureToTrick(snapshot, selectedTrick, lastPosture == TrainingDogPosture.Sitting);
        selectedTrick = trickId;
        state = PawPalTrainingState.GestureDetected;
        StartAttempt(trickId, true);
    }

    private void StartAttempt(PawPalTrickId trickId, bool fromGesture)
    {
        if (attemptRoutine != null)
        {
            return;
        }

        attemptRoutine = StartCoroutine(AttemptRoutine(trickId, fromGesture));
    }

    private IEnumerator AttemptRoutine(PawPalTrickId trickId, bool fromGesture)
    {
        selectedTrick = trickId;
        state = fromGesture ? PawPalTrainingState.GestureDetected : PawPalTrainingState.Practicing;
        SetBusy(true);

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dogState = runtime != null ? runtime.ActiveDog : null;
        DogRoomAgent dogAgent = ResolveActiveDogAgent();
        PawPalCatRoomAgent catAgent = ResolveActiveCatAgent();
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        if (dogState == null || definition == null)
        {
            state = PawPalTrainingState.TrickFailed;
            feedbackText = "Training needs an active pet.";
            canPraise = false;
            SetBusy(false);
            Refresh();
            attemptRoutine = null;
            yield break;
        }

        if (PawPalTrainingModeView.IsLockedByTrainingUiSequence(dogState, trickId))
        {
            state = PawPalTrainingState.TrickFailed;
            feedbackText = PawPalTrickProgressionService.GetFailureText(
                PawPalTrickFailureReason.MissingPrerequisite,
                dogState,
                definition,
                false);
            canPraise = false;
            SetBusy(false);
            Refresh();
            attemptRoutine = null;
            yield break;
        }

        if (dogAgent != null && !CanStartDogAnimation(dogAgent))
        {
            state = PawPalTrainingState.BreakNeeded;
            feedbackText = dogState.DisplayName + " is busy right now.";
            canPraise = false;
            SetBusy(false);
            Refresh();
            attemptRoutine = null;
            yield break;
        }

        if (catAgent != null && !catAgent.CanPerformTrainingAnimation())
        {
            state = PawPalTrainingState.BreakNeeded;
            feedbackText = dogState.DisplayName + " is busy right now.";
            canPraise = false;
            SetBusy(false);
            Refresh();
            attemptRoutine = null;
            yield break;
        }

        bool posePrerequisite = trickId != PawPalTrickId.Lie || lastPosture == TrainingDogPosture.Sitting || runtime.IsActiveDogTrickLearned(PawPalTrickId.Sit);
        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dogState, definition, config, false, false, posePrerequisite);
        if (runtime != null)
        {
            runtime.RecordTrickTrainingResult(result);
        }

        ApplyAttemptResult(result);
        Refresh();

        if (result.Success)
        {
            if (dogAgent != null)
            {
                state = PawPalTrainingState.AttemptingTrick;
                yield return StartCoroutine(dogAgent.PlayTrainingTrick(definition, true));
            }
            else if (catAgent != null)
            {
                state = PawPalTrainingState.AttemptingTrick;
                yield return StartCoroutine(catAgent.PlayTrainingTrick(definition, Camera.main));
            }

            UpdatePostureAfterSuccess(trickId);

            if (result.MadeProgress && view != null)
            {
                if (dogAgent != null)
                {
                    view.ShowTrainingProgressCue(dogAgent, Camera.main, definition.DisplayName, result.PreviousProgress01, result.CurrentProgress01);
                }
                else if (catAgent != null)
                {
                    view.ShowTrainingProgressCue(catAgent.transform, catAgent.FocusTransform, Camera.main, definition.DisplayName, result.PreviousProgress01, result.CurrentProgress01);
                }
            }
        }

        if (!result.Success && dogState.TrainingFatigue01 > 0.74f)
        {
            state = PawPalTrainingState.BreakNeeded;
        }

        SetBusy(false);
        Refresh();
        attemptRoutine = null;
    }

    private void ApplyAttemptResult(PawPalTrickAttemptResult result)
    {
        canPraise = result != null && result.Success;
        state = result != null && result.Success ? PawPalTrainingState.TrickSucceeded : PawPalTrainingState.TrickFailed;
        if (result != null && result.LearnedNow)
        {
            state = PawPalTrainingState.Learned;
        }

        feedbackText = result != null && !string.IsNullOrWhiteSpace(result.FeedbackText)
            ? result.FeedbackText
            : DefaultFeedbackText;
    }

    private void UpdatePostureAfterSuccess(PawPalTrickId trickId)
    {
        switch (trickId)
        {
            case PawPalTrickId.Sit:
                lastPosture = TrainingDogPosture.Sitting;
                break;
            case PawPalTrickId.Lie:
                lastPosture = TrainingDogPosture.Lying;
                break;
            default:
                lastPosture = TrainingDogPosture.Standing;
                break;
        }
    }

    private void ConfigureGestureRecognizer()
    {
        if (gestureRecognizer == null)
        {
            return;
        }

        DogRoomAgent dog = ResolveActiveDogAgent();
        if (dog != null)
        {
            gestureRecognizer.Configure(dog, Camera.main, config != null ? config.GestureLeniency : 1f);
            return;
        }

        PawPalCatRoomAgent cat = ResolveActiveCatAgent();
        gestureRecognizer.Configure(cat != null ? cat.transform : null, Camera.main, config != null ? config.GestureLeniency : 1f);
    }

    private void SetBusy(bool busy)
    {
        if (view != null)
        {
            view.SetBusy(busy);
        }
    }

    private static bool CanStartDogAnimation(DogRoomAgent dog)
    {
        return dog != null
            && dog.isActiveAndEnabled
            && !dog.IsBusy
            && !dog.IsPlayingOneShotAnimation
            && !dog.IsResting
            && !dog.IsSleeping
            && !dog.HasHeldToy;
    }

    private static DogRoomAgent ResolveActiveDogAgent()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            return null;
        }

        if (runtime != null && runtime.ActiveDog != null && !string.IsNullOrEmpty(runtime.ActiveDog.Id))
        {
            string activeDogId = runtime.ActiveDog.Id;
            for (int i = 0; i < dogs.Length; i++)
            {
                DogRoomAgent candidate = dogs[i];
                if (candidate != null
                    && candidate.HasExplicitDogId
                    && string.Equals(candidate.DogId, activeDogId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        if (runtime != null)
        {
            int index = Mathf.Clamp(runtime.ActiveDogIndex, 0, dogs.Length - 1);
            if (dogs[index] != null)
            {
                return dogs[index];
            }
        }

        for (int i = 0; i < dogs.Length; i++)
        {
            if (dogs[i] != null)
            {
                return dogs[i];
            }
        }

        return null;
    }

    private static PawPalCatRoomAgent ResolveActiveCatAgent()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.ActivePetSpecies != IntroPetSpecies.Cat)
        {
            return null;
        }

        PawPalCatRoomAgent[] cats = FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        if (cats == null || cats.Length == 0)
        {
            return null;
        }

        string activePetId = runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent candidate = cats[i];
            if (candidate != null && string.Equals(candidate.RuntimePetId, activePetId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return cats[0];
    }
}
