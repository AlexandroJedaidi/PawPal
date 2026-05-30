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
}

[DisallowMultipleComponent]
public sealed class PawPalVoiceInputController : MonoBehaviour
{
    private const PawPalVoiceTrick V1Trick = PawPalVoiceTrick.Sit;

    [SerializeField] private int requiredSamples = 3;
    [SerializeField] private int maxStoredSamples = 5;
    [SerializeField] private int recordingFrequency = 16000;
    [SerializeField] private float recordingSeconds = 1.25f;
    [SerializeField, Range(0.1f, 1f)] private float nameMatchThreshold = 0.68f;
    [SerializeField, Range(0.1f, 1f)] private float trickMatchThreshold = 0.66f;

    private readonly PawPalVoiceInputSnapshot snapshot = new PawPalVoiceInputSnapshot();
    private PawPalVoiceProfileStore profileStore;
    private DogVoiceCommandDirector commandDirector;
    private Coroutine activeRoutine;
    private string activeDeviceName;
    private bool isRecording;

    public event Action StateChanged;

    public PawPalVoiceInputSnapshot Snapshot
    {
        get
        {
            RefreshSnapshotCounts();
            return snapshot;
        }
    }

    public void Initialize(DogVoiceCommandDirector voiceCommandDirector)
    {
        commandDirector = voiceCommandDirector;
        EnsureStore();
        SetSnapshot(PawPalVoiceInputMode.Idle, "Voice is ready.", false, 0f);
    }

    private void Awake()
    {
        EnsureStore();
    }

    private void OnDisable()
    {
        CancelActiveOperation(false);
    }

    public void RefreshForActiveDog()
    {
        string message = snapshot.Message;
        if (string.IsNullOrEmpty(message))
        {
            message = "Voice is ready.";
        }

        SetSnapshot(snapshot.Mode, message, snapshot.IsBusy, snapshot.LastConfidence);
    }

    public void BeginTeachName()
    {
        if (activeRoutine != null)
        {
            return;
        }

        activeRoutine = StartCoroutine(TeachNameRoutine());
    }

    public void BeginTeachSit()
    {
        if (activeRoutine != null)
        {
            return;
        }

        activeRoutine = StartCoroutine(TeachTrickRoutine(V1Trick));
    }

    public void BeginListen()
    {
        if (activeRoutine != null)
        {
            return;
        }

        activeRoutine = StartCoroutine(ListenRoutine());
    }

    public void CancelActiveOperation()
    {
        CancelActiveOperation(true);
    }

    private IEnumerator TeachNameRoutine()
    {
        PawPalDogState dog;
        if (!TryGetActiveDog(out dog))
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "No active dog.", false, 0f);
            activeRoutine = null;
            yield break;
        }

        PawPalVoiceTemplate template = null;
        string failureReason = string.Empty;
        yield return CaptureTemplateRoutine(
            PawPalVoiceInputMode.TeachingName,
            "Say " + dog.DisplayName + ".",
            delegate(PawPalVoiceTemplate capturedTemplate, string captureFailure)
            {
                template = capturedTemplate;
                failureReason = captureFailure;
            });

        if (template == null)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, failureReason, false, 0f);
            activeRoutine = null;
            yield break;
        }

        profileStore.AddNameSample(dog.Id, dog.DisplayName, template, maxStoredSamples);
        int count = profileStore.GetNameSampleCount(dog.Id, dog.DisplayName);
        bool learned = profileStore.IsDogNameLearned(dog.Id, dog.DisplayName, requiredSamples);
        string message = learned
            ? dog.DisplayName + " knows its name."
            : "Good. " + count + "/" + requiredSamples + " name samples.";
        SetSnapshot(PawPalVoiceInputMode.Idle, message, false, 0f);
        activeRoutine = null;
    }

    private IEnumerator TeachTrickRoutine(PawPalVoiceTrick trick)
    {
        PawPalDogState dog;
        if (!TryGetActiveDog(out dog))
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "No active dog.", false, 0f);
            activeRoutine = null;
            yield break;
        }

        if (!profileStore.IsDogNameLearned(dog.Id, dog.DisplayName, requiredSamples))
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "Teach " + dog.DisplayName + " first.", false, 0f);
            activeRoutine = null;
            yield break;
        }

        bool wasLearned = profileStore.IsTrickLearned(dog.Id, trick, requiredSamples);
        PawPalVoiceTemplate template = null;
        string failureReason = string.Empty;
        yield return CaptureTemplateRoutine(
            PawPalVoiceInputMode.TeachingTrick,
            "Say " + PawPalVoiceProfileStore.GetTrickLabel(trick) + ".",
            delegate(PawPalVoiceTemplate capturedTemplate, string captureFailure)
            {
                template = capturedTemplate;
                failureReason = captureFailure;
            });

        if (template == null)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, failureReason, false, 0f);
            activeRoutine = null;
            yield break;
        }

        profileStore.AddTrickSample(dog.Id, trick, template, maxStoredSamples);
        int count = profileStore.GetTrickSampleCount(dog.Id, trick);
        bool learned = profileStore.IsTrickLearned(dog.Id, trick, requiredSamples);
        if (!wasLearned && learned && !profileStore.HasTrickRewardGranted(dog.Id, trick))
        {
            PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
            if (runtime != null)
            {
                runtime.TrainActiveDog();
            }

            profileStore.MarkTrickRewardGranted(dog.Id, trick);
        }

        string message = learned
            ? dog.DisplayName + " learned " + PawPalVoiceProfileStore.GetTrickLabel(trick) + "."
            : "Good. " + count + "/" + requiredSamples + " sit samples.";
        SetSnapshot(PawPalVoiceInputMode.Idle, message, false, 0f);
        activeRoutine = null;
    }

    private IEnumerator ListenRoutine()
    {
        PawPalDogState dog;
        if (!TryGetActiveDog(out dog))
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "No active dog.", false, 0f);
            activeRoutine = null;
            yield break;
        }

        if (!profileStore.IsDogNameLearned(dog.Id, dog.DisplayName, requiredSamples))
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "Teach " + dog.DisplayName + " first.", false, 0f);
            activeRoutine = null;
            yield break;
        }

        PawPalVoiceTemplate nameTemplate = null;
        string failureReason = string.Empty;
        yield return CaptureTemplateRoutine(
            PawPalVoiceInputMode.ListeningName,
            "Say " + dog.DisplayName + ".",
            delegate(PawPalVoiceTemplate capturedTemplate, string captureFailure)
            {
                nameTemplate = capturedTemplate;
                failureReason = captureFailure;
            });

        if (nameTemplate == null)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, failureReason, false, 0f);
            activeRoutine = null;
            yield break;
        }

        PawPalVoiceMatchResult nameMatch = profileStore.MatchName(dog.Id, dog.DisplayName, nameTemplate, requiredSamples, nameMatchThreshold);
        if (!nameMatch.IsMatch)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, dog.DisplayName + " did not catch that.", false, nameMatch.Confidence);
            activeRoutine = null;
            yield break;
        }

        bool dogCalledToCamera = commandDirector != null && commandDirector.TryCallActiveDogToCamera();

        if (!profileStore.IsTrickLearned(dog.Id, V1Trick, requiredSamples))
        {
            string nameOnlyMessage = dogCalledToCamera
                ? dog.DisplayName + " is coming. Teach sit next."
                : dog.DisplayName + " heard you. Teach sit next.";
            SetSnapshot(PawPalVoiceInputMode.Idle, nameOnlyMessage, false, nameMatch.Confidence);
            activeRoutine = null;
            yield break;
        }

        if (dogCalledToCamera)
        {
            SetSnapshot(PawPalVoiceInputMode.ListeningName, dog.DisplayName + " is coming.", true, nameMatch.Confidence);
            yield return WaitForVoiceCommandDirector(8f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.2f);
        }

        PawPalVoiceTemplate trickTemplate = null;
        yield return CaptureTemplateRoutine(
            PawPalVoiceInputMode.ListeningTrick,
            "Say sit.",
            delegate(PawPalVoiceTemplate capturedTemplate, string captureFailure)
            {
                trickTemplate = capturedTemplate;
                failureReason = captureFailure;
            });

        if (trickTemplate == null)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, failureReason, false, nameMatch.Confidence);
            activeRoutine = null;
            yield break;
        }

        PawPalVoiceMatchResult trickMatch = profileStore.MatchTrick(dog.Id, V1Trick, trickTemplate, requiredSamples, trickMatchThreshold);
        if (!trickMatch.IsMatch)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, dog.DisplayName + " did not understand sit.", false, trickMatch.Confidence);
            activeRoutine = null;
            yield break;
        }

        bool performed = commandDirector != null && commandDirector.TryPerformTrick(V1Trick);
        string finalMessage = performed ? dog.DisplayName + " sits." : dog.DisplayName + " is busy.";
        SetSnapshot(PawPalVoiceInputMode.Idle, finalMessage, false, trickMatch.Confidence);
        activeRoutine = null;
    }

    private IEnumerator WaitForVoiceCommandDirector(float timeout)
    {
        float elapsed = 0f;
        while (commandDirector != null && commandDirector.IsCommandRunning && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator CaptureTemplateRoutine(PawPalVoiceInputMode mode, string prompt, Action<PawPalVoiceTemplate, string> onCompleted)
    {
        SetSnapshot(mode, prompt, true, snapshot.LastConfidence);

        string unavailableReason = string.Empty;
        yield return EnsureMicrophoneAvailable(delegate(bool available, string reason)
        {
            unavailableReason = available ? string.Empty : reason;
        });

        if (!string.IsNullOrEmpty(unavailableReason))
        {
            SetSnapshot(PawPalVoiceInputMode.Unavailable, unavailableReason, false, 0f);
            if (onCompleted != null)
            {
                onCompleted(null, unavailableReason);
            }
            yield break;
        }

        AudioClip clip = null;
        int recordedPosition = 0;
        string captureFailure = string.Empty;

        try
        {
            activeDeviceName = Microphone.devices[0];
            int clipLengthSeconds = Mathf.CeilToInt(Mathf.Max(1f, recordingSeconds + 0.25f));
            clip = Microphone.Start(activeDeviceName, false, clipLengthSeconds, recordingFrequency);
            isRecording = clip != null;
        }
        catch (Exception exception)
        {
            captureFailure = "Microphone failed: " + exception.Message;
        }

        if (clip == null || !string.IsNullOrEmpty(captureFailure))
        {
            isRecording = false;
            if (onCompleted != null)
            {
                onCompleted(null, string.IsNullOrEmpty(captureFailure) ? "Microphone failed." : captureFailure);
            }
            yield break;
        }

        float startTimeout = Time.realtimeSinceStartup + 1f;
        while (Microphone.GetPosition(activeDeviceName) <= 0 && Time.realtimeSinceStartup < startTimeout)
        {
            yield return null;
        }

        float finishTime = Time.realtimeSinceStartup + recordingSeconds;
        while (Time.realtimeSinceStartup < finishTime)
        {
            yield return null;
        }

        if (isRecording)
        {
            recordedPosition = Microphone.GetPosition(activeDeviceName);
            Microphone.End(activeDeviceName);
            isRecording = false;
        }

        if (recordedPosition <= 0)
        {
            if (onCompleted != null)
            {
                onCompleted(null, "No voice was recorded.");
            }
            yield break;
        }

        int channels = Mathf.Max(1, clip.channels);
        float[] samples = new float[recordedPosition * channels];
        clip.GetData(samples, 0);

        PawPalVoiceTemplate template;
        string featureFailure;
        if (!PawPalVoiceFeatureExtractor.TryCreateTemplate(samples, channels, clip.frequency, out template, out featureFailure))
        {
            if (onCompleted != null)
            {
                onCompleted(null, featureFailure);
            }
            yield break;
        }

        if (onCompleted != null)
        {
            onCompleted(template, string.Empty);
        }
    }

    private IEnumerator EnsureMicrophoneAvailable(Action<bool, string> onCompleted)
    {
#if !(UNITY_ANDROID && !UNITY_EDITOR)
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && Time.realtimeSinceStartup < timeout)
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
#endif

#if !(UNITY_ANDROID && !UNITY_EDITOR)
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            if (onCompleted != null)
            {
                onCompleted(false, "Microphone permission is needed.");
            }
            yield break;
        }
#endif

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            if (onCompleted != null)
            {
                onCompleted(false, "No microphone found.");
            }
            yield break;
        }

        if (onCompleted != null)
        {
            onCompleted(true, string.Empty);
        }
    }

    private void CancelActiveOperation(bool updateSnapshot)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (isRecording && !string.IsNullOrEmpty(activeDeviceName))
        {
            Microphone.End(activeDeviceName);
        }

        isRecording = false;
        activeDeviceName = null;

        if (updateSnapshot)
        {
            SetSnapshot(PawPalVoiceInputMode.Idle, "Voice is ready.", false, snapshot.LastConfidence);
        }
    }

    private bool TryGetActiveDog(out PawPalDogState dog)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        dog = runtime != null ? runtime.ActiveDog : null;
        return dog != null && !string.IsNullOrEmpty(dog.Id);
    }

    private void EnsureStore()
    {
        if (profileStore == null)
        {
            profileStore = new PawPalVoiceProfileStore();
        }
    }

    private void SetSnapshot(PawPalVoiceInputMode mode, string message, bool isBusy, float confidence)
    {
        snapshot.Mode = mode;
        snapshot.Message = message;
        snapshot.IsBusy = isBusy;
        snapshot.LastConfidence = confidence;
        RefreshSnapshotCounts();
        Action handler = StateChanged;
        if (handler != null)
        {
            handler();
        }
    }

    private void RefreshSnapshotCounts()
    {
        EnsureStore();
        snapshot.RequiredSamples = Mathf.Max(1, requiredSamples);
        snapshot.HasMicrophone = Microphone.devices != null && Microphone.devices.Length > 0;

        PawPalDogState dog;
        if (!TryGetActiveDog(out dog))
        {
            snapshot.ActiveDogName = "Dog";
            snapshot.NameSampleCount = 0;
            snapshot.SitSampleCount = 0;
            snapshot.NameLearned = false;
            snapshot.SitLearned = false;
            return;
        }

        snapshot.ActiveDogName = dog.DisplayName;
        snapshot.NameSampleCount = profileStore.GetNameSampleCount(dog.Id, dog.DisplayName);
        snapshot.SitSampleCount = profileStore.GetTrickSampleCount(dog.Id, V1Trick);
        snapshot.NameLearned = profileStore.IsDogNameLearned(dog.Id, dog.DisplayName, requiredSamples);
        snapshot.SitLearned = profileStore.IsTrickLearned(dog.Id, V1Trick, requiredSamples);
    }
}
