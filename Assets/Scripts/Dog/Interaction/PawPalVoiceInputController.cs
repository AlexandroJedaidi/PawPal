using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public enum PawPalVoiceInputMode
{
    Idle,
    TeachingName,
    TeachingTrick,
    ListeningName,
    ListeningTrick,
    Unavailable
}

public sealed class PawPalVoiceInputSnapshot
{
    public PawPalVoiceInputMode Mode;
    public string ActiveDogName;
    public string Message;
    public int RequiredSamples;
    public int NameSampleCount;
    public int SitSampleCount;
    public bool NameLearned;
    public bool SitLearned;
    public bool IsBusy;
    public bool HasMicrophone;
    public float LastConfidence;
    public bool IsMicEnabled;
    public int LearnedTrickCount;
}

public delegate PawPalVoiceCommandExecutionResult PawPalVoiceCommandExecutionHandler(PawPalResolvedVoiceCommand command);

[DisallowMultipleComponent]
public sealed class PawPalVoiceInputController : MonoBehaviour
{
    private readonly PawPalVoiceInputSnapshot snapshot = new PawPalVoiceInputSnapshot();

    private DogVoiceCommandDirector commandDirector;
    private IPawPalVoiceCommandService voiceService;
    private PawPalVoiceCommandCatalog commandCatalog;
    private Coroutine stateRoutine;
    private bool includeUnlearnedActiveDogTricks;

    public event Action StateChanged;
    public event Action<string> FeedbackRequested;
    public event PawPalVoiceCommandExecutionHandler CommandExecutionRequested;

    public PawPalVoiceInputSnapshot Snapshot
    {
        get
        {
            RefreshSnapshotCounts();
            return snapshot;
        }
    }

    public bool IsMicEnabled
    {
        get { return snapshot.IsMicEnabled; }
    }

    public void Initialize(DogVoiceCommandDirector voiceCommandDirector)
    {
        commandDirector = voiceCommandDirector;
        voiceService = PawPalVoiceCommandServiceFactory.Create(this);
        RefreshCommandCatalog();
        SetSnapshot(PawPalVoiceInputMode.Idle, "Microphone is off.", false, 0f, false);
    }

    private void Update()
    {
        if (voiceService != null)
        {
            voiceService.Tick();
        }
    }

    private void OnDisable()
    {
        CancelActiveOperation(false);
    }

    public void ToggleMic()
    {
        SetMicEnabled(!snapshot.IsMicEnabled);
    }

    public void SetMicEnabled(bool enabled)
    {
        if (enabled == snapshot.IsMicEnabled)
        {
            if (enabled)
            {
                RefreshCommandCatalog();
            }

            return;
        }

        if (enabled)
        {
            if (stateRoutine != null)
            {
                return;
            }

            SetSnapshot(PawPalVoiceInputMode.ListeningName, "Starting microphone.", true, snapshot.LastConfidence, true);
            stateRoutine = StartCoroutine(EnableMicRoutine());
            return;
        }

        CancelActiveOperation();
    }

    public void RefreshCommandCatalog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        string activeDogId = runtime != null && runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
        commandCatalog = PawPalVoiceCommandCatalogBuilder.Build(runtime != null ? runtime.Dogs : null, activeDogId, includeUnlearnedActiveDogTricks);

        if (voiceService != null)
        {
            voiceService.ConfigurePhrases(commandCatalog != null ? commandCatalog.Phrases : new string[0]);
        }

        RefreshSnapshotCounts();
        RaiseStateChanged();
    }

    public void SetInteractionTrainingCommandsEnabled(bool enabled)
    {
        if (includeUnlearnedActiveDogTricks == enabled)
        {
            return;
        }

        includeUnlearnedActiveDogTricks = enabled;
        RefreshCommandCatalog();
    }

    public void RefreshForActiveDog()
    {
        RefreshCommandCatalog();
    }

    public void BeginTeachName()
    {
        SetMicEnabled(true);
    }

    public void BeginTeachSit()
    {
        SetMicEnabled(true);
    }

    public void BeginTeachTrick(PawPalTrickId trickId)
    {
        SetMicEnabled(true);
    }

    public void BeginListen()
    {
        SetMicEnabled(true);
    }

    public void CancelActiveOperation()
    {
        CancelActiveOperation(true);
    }

#if UNITY_IOS && !UNITY_EDITOR
    public void OnPawPalIosVoicePermission(string payload)
    {
        PawPalIosVoiceCommandService iosService = voiceService as PawPalIosVoiceCommandService;
        if (iosService != null)
        {
            iosService.HandleNativePermissionResult(payload);
        }
    }

    public void OnPawPalIosVoiceRecognized(string transcript)
    {
        PawPalIosVoiceCommandService iosService = voiceService as PawPalIosVoiceCommandService;
        if (iosService != null)
        {
            iosService.HandleNativeRecognizedPhrase(transcript);
        }
    }

    public void OnPawPalIosVoiceFailure(string payload)
    {
        PawPalIosVoiceCommandService iosService = voiceService as PawPalIosVoiceCommandService;
        if (iosService != null)
        {
            iosService.HandleNativeFailure(payload);
        }
    }
#endif

    private IEnumerator EnableMicRoutine()
    {
        RefreshCommandCatalog();
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.Dogs == null || runtime.Dogs.Count == 0)
        {
            HandleTerminalFailure(PawPalVoiceCommandFailureReason.Unsupported, "No dogs are available for voice commands.");
            stateRoutine = null;
            yield break;
        }

        if (voiceService == null || !voiceService.IsAvailable)
        {
            HandleTerminalFailure(PawPalVoiceCommandFailureReason.Unsupported, "Voice commands are unavailable on this platform.");
            stateRoutine = null;
            yield break;
        }

        bool hasMicrophoneAccess = false;
        string microphoneFailure = string.Empty;
        yield return EnsureMicrophoneAccess(delegate(bool granted, string message)
        {
            hasMicrophoneAccess = granted;
            microphoneFailure = message;
        });

        if (!hasMicrophoneAccess)
        {
            HandleTerminalFailure(
                string.Equals(microphoneFailure, "No microphone found.", StringComparison.Ordinal)
                    ? PawPalVoiceCommandFailureReason.NoMicrophone
                    : PawPalVoiceCommandFailureReason.PermissionDenied,
                microphoneFailure);
            stateRoutine = null;
            yield break;
        }

        bool permissionGranted = false;
        string permissionFailure = string.Empty;
        yield return voiceService.RequestPermission(delegate(bool granted, string message)
        {
            permissionGranted = granted;
            permissionFailure = message;
        });

        if (!permissionGranted)
        {
            HandleTerminalFailure(PawPalVoiceCommandFailureReason.PermissionDenied, string.IsNullOrEmpty(permissionFailure) ? "Speech recognition permission is needed." : permissionFailure);
            stateRoutine = null;
            yield break;
        }

        RefreshCommandCatalog();
        if (commandCatalog == null || commandCatalog.Phrases.Count == 0)
        {
            HandleTerminalFailure(PawPalVoiceCommandFailureReason.UnclearCommand, "No voice commands are available yet.");
            stateRoutine = null;
            yield break;
        }

        if (!voiceService.StartListening(HandleRecognizedPhrase, HandleServiceFailure))
        {
            if (snapshot.Mode != PawPalVoiceInputMode.Unavailable)
            {
                HandleTerminalFailure(voiceService.LastFailureReason, "Voice commands could not start.");
            }

            stateRoutine = null;
            yield break;
        }

        SetSnapshot(PawPalVoiceInputMode.ListeningName, "Listening for dog names and tricks.", false, snapshot.LastConfidence, true);
        stateRoutine = null;
    }

    private IEnumerator EnsureMicrophoneAccess(Action<bool, string> onCompleted)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            float timeoutAt = Time.realtimeSinceStartup + 5f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (onCompleted != null)
            {
                onCompleted(false, "Microphone permission is needed.");
            }

            yield break;
        }
#else
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            if (onCompleted != null)
            {
                onCompleted(false, "Microphone permission is needed.");
            }

            yield break;
        }
#endif

#if !(UNITY_IOS && !UNITY_EDITOR)
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            if (onCompleted != null)
            {
                onCompleted(false, "No microphone found.");
            }

            yield break;
        }
#endif

        if (onCompleted != null)
        {
            onCompleted(true, string.Empty);
        }
    }

    private void HandleRecognizedPhrase(PawPalVoiceRecognizedPhrase recognizedPhrase)
    {
        if (!snapshot.IsMicEnabled || recognizedPhrase == null)
        {
            return;
        }

        RefreshCommandCatalog();
        float confidence = Mathf.Clamp01(recognizedPhrase.Confidence01);
        PawPalResolvedVoiceCommand resolvedCommand = commandCatalog != null ? commandCatalog.Resolve(recognizedPhrase.Transcript) : PawPalResolvedVoiceCommand.None();
        if (resolvedCommand.Type == PawPalResolvedVoiceCommandType.None)
        {
            string feedback = commandCatalog != null ? commandCatalog.GetFeedbackForUnresolvedPhrase(recognizedPhrase.Transcript) : string.Empty;
            if (string.IsNullOrWhiteSpace(feedback))
            {
                feedback = "That voice command is not available yet.";
            }

            SetSnapshot(PawPalVoiceInputMode.ListeningName, feedback, false, confidence, true);
            RaiseFeedback(feedback);
            return;
        }

        string message;
        bool success = ExecuteResolvedCommand(resolvedCommand, out message);
        SetSnapshot(PawPalVoiceInputMode.ListeningName, message, false, confidence, true);
        if (!success)
        {
            RaiseFeedback(message);
        }
    }

    private bool ExecuteResolvedCommand(PawPalResolvedVoiceCommand resolvedCommand, out string message)
    {
        message = "Voice command failed.";
        if (resolvedCommand == null)
        {
            return false;
        }

        PawPalVoiceCommandExecutionResult customResult = TryExecuteCustomCommand(resolvedCommand);
        if (customResult != null && customResult.Handled)
        {
            message = string.IsNullOrWhiteSpace(customResult.Message) ? message : customResult.Message;
            return customResult.Success;
        }

        switch (resolvedCommand.Type)
        {
            case PawPalResolvedVoiceCommandType.CallDog:
            {
                bool started = commandDirector != null && commandDirector.TryCallDogToCamera(resolvedCommand.DogId);
                message = started
                    ? resolvedCommand.DogName + " is coming."
                    : resolvedCommand.DogName + " is busy right now.";
                return started;
            }
            case PawPalResolvedVoiceCommandType.PerformTrick:
            {
                bool started = commandDirector != null && commandDirector.TryPerformTrick(resolvedCommand.DogId, resolvedCommand.TrickId);
                string trickLabel = string.IsNullOrWhiteSpace(resolvedCommand.TrickLabel)
                    ? PawPalTrickCatalog.GetCommandLabel(resolvedCommand.TrickId)
                    : resolvedCommand.TrickLabel;
                message = started
                    ? resolvedCommand.DogName + " does " + trickLabel + "."
                    : resolvedCommand.DogName + " is busy right now.";
                return started;
            }
            default:
                return false;
        }
    }

    private PawPalVoiceCommandExecutionResult TryExecuteCustomCommand(PawPalResolvedVoiceCommand resolvedCommand)
    {
        PawPalVoiceCommandExecutionHandler handler = CommandExecutionRequested;
        if (handler == null)
        {
            return PawPalVoiceCommandExecutionResult.Unhandled();
        }

        Delegate[] invocationList = handler.GetInvocationList();
        for (int i = invocationList.Length - 1; i >= 0; i--)
        {
            PawPalVoiceCommandExecutionHandler commandHandler = invocationList[i] as PawPalVoiceCommandExecutionHandler;
            if (commandHandler == null)
            {
                continue;
            }

            PawPalVoiceCommandExecutionResult result = commandHandler(resolvedCommand);
            if (result != null && result.Handled)
            {
                return result;
            }
        }

        return PawPalVoiceCommandExecutionResult.Unhandled();
    }

    private void HandleServiceFailure(PawPalVoiceCommandFailureReason reason, string message)
    {
        if (!snapshot.IsMicEnabled && stateRoutine == null)
        {
            return;
        }

        HandleTerminalFailure(reason, message);
    }

    private void HandleTerminalFailure(PawPalVoiceCommandFailureReason reason, string message)
    {
        CancelActiveOperation(false);
        string resolvedMessage = string.IsNullOrWhiteSpace(message) ? "Voice commands are unavailable." : message;
        SetSnapshot(PawPalVoiceInputMode.Unavailable, resolvedMessage, false, snapshot.LastConfidence, false);
        RaiseFeedback(resolvedMessage);
    }

    private void CancelActiveOperation(bool updateSnapshot)
    {
        if (stateRoutine != null)
        {
            StopCoroutine(stateRoutine);
            stateRoutine = null;
        }

        if (voiceService != null)
        {
            voiceService.StopListening();
        }

        if (updateSnapshot)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "Microphone is off.", false, snapshot.LastConfidence, false);
        }
        else
        {
            snapshot.IsMicEnabled = false;
            snapshot.IsBusy = false;
        }
    }

    private void SetSnapshot(PawPalVoiceInputMode mode, string message, bool isBusy, float confidence, bool micEnabled)
    {
        snapshot.Mode = mode;
        snapshot.Message = message;
        snapshot.IsBusy = isBusy;
        snapshot.LastConfidence = confidence;
        snapshot.IsMicEnabled = micEnabled;
        RefreshSnapshotCounts();
        RaiseStateChanged();
    }

    private void RefreshSnapshotCounts()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        snapshot.ActiveDogName = dog != null && !string.IsNullOrWhiteSpace(dog.DisplayName) ? dog.DisplayName : "Dog";
        snapshot.HasMicrophone = (voiceService != null && voiceService.IsAvailable)
#if !(UNITY_IOS && !UNITY_EDITOR)
            || (Microphone.devices != null && Microphone.devices.Length > 0)
#endif
            ;
        snapshot.RequiredSamples = 0;
        snapshot.NameSampleCount = 0;
        snapshot.NameLearned = dog != null;
        snapshot.SitSampleCount = 0;
        snapshot.SitLearned = runtime != null && runtime.IsActiveDogTrickLearned(PawPalTrickId.Sit);
        snapshot.LearnedTrickCount = 0;
        if (dog == null || dog.Tricks == null)
        {
            return;
        }

        for (int i = 0; i < dog.Tricks.Count; i++)
        {
            PawPalDogTrickProgress progress = dog.Tricks[i];
            if (progress != null && progress.IsLearned)
            {
                snapshot.LearnedTrickCount++;
            }
        }
    }

    private void RaiseStateChanged()
    {
        Action handler = StateChanged;
        if (handler != null)
        {
            handler();
        }
    }

    private void RaiseFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Action<string> handler = FeedbackRequested;
        if (handler != null)
        {
            handler(message);
        }
    }
}
