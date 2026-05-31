using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

public enum PawPalVoiceCommandFailureReason
{
    None,
    PermissionDenied,
    NoMicrophone,
    Timeout,
    UnclearCommand,
    DuplicateCommand,
    Busy,
    Unsupported
}

public enum PawPalResolvedVoiceCommandType
{
    None,
    CallDog,
    PerformTrick
}

public sealed class PawPalVoiceRecognizedPhrase
{
    public string Transcript;
    public float Confidence01;
}

public sealed class PawPalResolvedVoiceCommand
{
    public PawPalResolvedVoiceCommandType Type;
    public string DogId;
    public string DogName;
    public PawPalTrickId TrickId;
    public string TrickLabel;

    public static PawPalResolvedVoiceCommand None()
    {
        return new PawPalResolvedVoiceCommand
        {
            Type = PawPalResolvedVoiceCommandType.None,
            DogId = string.Empty,
            DogName = string.Empty,
            TrickId = PawPalTrickId.Sit,
            TrickLabel = string.Empty
        };
    }
}

public sealed class PawPalVoiceCommandExecutionResult
{
    public bool Handled;
    public bool Success;
    public string Message;

    public static PawPalVoiceCommandExecutionResult Unhandled()
    {
        return new PawPalVoiceCommandExecutionResult
        {
            Handled = false,
            Success = false,
            Message = string.Empty
        };
    }

    public static PawPalVoiceCommandExecutionResult HandledResult(bool success, string message)
    {
        return new PawPalVoiceCommandExecutionResult
        {
            Handled = true,
            Success = success,
            Message = message ?? string.Empty
        };
    }
}

public sealed class PawPalVoiceCommandCatalog
{
    private readonly List<string> phrases = new List<string>();
    private readonly Dictionary<string, PawPalResolvedVoiceCommand> dogAndTrickCommands = new Dictionary<string, PawPalResolvedVoiceCommand>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalResolvedVoiceCommand> dogOnlyCommands = new Dictionary<string, PawPalResolvedVoiceCommand>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalResolvedVoiceCommand> activeDogTrickCommands = new Dictionary<string, PawPalResolvedVoiceCommand>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> unlearnedTrickFeedback = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> Phrases
    {
        get { return phrases; }
    }

    public string ActiveDogId { get; set; }

    public void AddDogCommand(string phrase, string dogId, string dogName)
    {
        string normalized = NormalizePhrase(phrase);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        AddPhrase(normalized);
        if (!dogOnlyCommands.ContainsKey(normalized))
        {
            dogOnlyCommands.Add(normalized, new PawPalResolvedVoiceCommand
            {
                Type = PawPalResolvedVoiceCommandType.CallDog,
                DogId = dogId ?? string.Empty,
                DogName = dogName ?? string.Empty,
                TrickId = PawPalTrickId.Sit,
                TrickLabel = string.Empty
            });
        }
    }

    public void AddDogAndTrickCommand(string phrase, string dogId, string dogName, PawPalTrickId trickId, string trickLabel)
    {
        string normalized = NormalizePhrase(phrase);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        AddPhrase(normalized);
        if (!dogAndTrickCommands.ContainsKey(normalized))
        {
            dogAndTrickCommands.Add(normalized, new PawPalResolvedVoiceCommand
            {
                Type = PawPalResolvedVoiceCommandType.PerformTrick,
                DogId = dogId ?? string.Empty,
                DogName = dogName ?? string.Empty,
                TrickId = trickId,
                TrickLabel = trickLabel ?? string.Empty
            });
        }
    }

    public void AddActiveDogTrickCommand(string phrase, string dogId, string dogName, PawPalTrickId trickId, string trickLabel)
    {
        string normalized = NormalizePhrase(phrase);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        AddPhrase(normalized);
        if (!activeDogTrickCommands.ContainsKey(normalized))
        {
            activeDogTrickCommands.Add(normalized, new PawPalResolvedVoiceCommand
            {
                Type = PawPalResolvedVoiceCommandType.PerformTrick,
                DogId = dogId ?? string.Empty,
                DogName = dogName ?? string.Empty,
                TrickId = trickId,
                TrickLabel = trickLabel ?? string.Empty
            });
        }
    }

    public void AddUnlearnedTrickFeedback(string phrase, string message)
    {
        string normalized = NormalizePhrase(phrase);
        if (string.IsNullOrEmpty(normalized) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!unlearnedTrickFeedback.ContainsKey(normalized))
        {
            unlearnedTrickFeedback.Add(normalized, message);
        }
    }

    public PawPalResolvedVoiceCommand Resolve(string transcript)
    {
        string normalized = NormalizePhrase(transcript);
        if (string.IsNullOrEmpty(normalized))
        {
            return PawPalResolvedVoiceCommand.None();
        }

        PawPalResolvedVoiceCommand command;
        if (dogAndTrickCommands.TryGetValue(normalized, out command))
        {
            return command;
        }

        if (dogOnlyCommands.TryGetValue(normalized, out command))
        {
            return command;
        }

        if (activeDogTrickCommands.TryGetValue(normalized, out command))
        {
            return command;
        }

        return PawPalResolvedVoiceCommand.None();
    }

    public string GetFeedbackForUnresolvedPhrase(string transcript)
    {
        string normalized = NormalizePhrase(transcript);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        string feedback;
        return unlearnedTrickFeedback.TryGetValue(normalized, out feedback) ? feedback : string.Empty;
    }

    public static string NormalizePhrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        bool previousWasSpace = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private void AddPhrase(string normalizedPhrase)
    {
        if (!phrases.Contains(normalizedPhrase))
        {
            phrases.Add(normalizedPhrase);
        }
    }
}

public static class PawPalVoiceCommandCatalogBuilder
{
    public static PawPalVoiceCommandCatalog Build(IReadOnlyList<PawPalDogState> dogs, string activeDogId)
    {
        return Build(dogs, activeDogId, false);
    }

    public static PawPalVoiceCommandCatalog Build(IReadOnlyList<PawPalDogState> dogs, string activeDogId, bool includeUnlearnedActiveDogTricks)
    {
        PawPalVoiceCommandCatalog catalog = new PawPalVoiceCommandCatalog
        {
            ActiveDogId = activeDogId ?? string.Empty
        };

        if (dogs == null)
        {
            return catalog;
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog == null || string.IsNullOrWhiteSpace(dog.Id) || string.IsNullOrWhiteSpace(dog.DisplayName))
            {
                continue;
            }

            string dogPhrase = PawPalVoiceCommandCatalog.NormalizePhrase(dog.DisplayName);
            if (string.IsNullOrEmpty(dogPhrase))
            {
                continue;
            }

            catalog.AddDogCommand(dogPhrase, dog.Id, dog.DisplayName);

            PawPalTrickCatalog.EnsureDogTrickData(dog);
            for (int progressIndex = 0; progressIndex < dog.Tricks.Count; progressIndex++)
            {
                PawPalDogTrickProgress progress = dog.Tricks[progressIndex];
                if (progress == null)
                {
                    continue;
                }

                PawPalTrickId trickId = progress.ParsedTrickId;
                string trickLabel = ResolveSpokenTrickLabel(progress, trickId);
                if (string.IsNullOrEmpty(trickLabel))
                {
                    continue;
                }

                string normalizedTrick = PawPalVoiceCommandCatalog.NormalizePhrase(trickLabel);
                if (string.IsNullOrEmpty(normalizedTrick))
                {
                    continue;
                }

                if (progress.IsLearned)
                {
                    catalog.AddDogAndTrickCommand(dogPhrase + " " + normalizedTrick, dog.Id, dog.DisplayName, trickId, trickLabel);
                    if (string.Equals(dog.Id, activeDogId, StringComparison.OrdinalIgnoreCase))
                    {
                        catalog.AddActiveDogTrickCommand(normalizedTrick, dog.Id, dog.DisplayName, trickId, trickLabel);
                    }
                }
                else
                {
                    if (includeUnlearnedActiveDogTricks && string.Equals(dog.Id, activeDogId, StringComparison.OrdinalIgnoreCase))
                    {
                        catalog.AddDogAndTrickCommand(dogPhrase + " " + normalizedTrick, dog.Id, dog.DisplayName, trickId, trickLabel);
                        catalog.AddActiveDogTrickCommand(normalizedTrick, dog.Id, dog.DisplayName, trickId, trickLabel);
                        continue;
                    }

                    catalog.AddUnlearnedTrickFeedback(
                        dogPhrase + " " + normalizedTrick,
                        dog.DisplayName + " has not learned " + trickLabel + " yet.");
                    if (string.Equals(dog.Id, activeDogId, StringComparison.OrdinalIgnoreCase))
                    {
                        catalog.AddUnlearnedTrickFeedback(
                            normalizedTrick,
                            dog.DisplayName + " does not know " + trickLabel + " yet.");
                    }
                }
            }
        }

        return catalog;
    }

    public static string ResolveSpokenTrickLabel(PawPalDogTrickProgress progress, PawPalTrickId trickId)
    {
        if (progress != null && !string.IsNullOrWhiteSpace(progress.CustomVoiceCommand))
        {
            return progress.CustomVoiceCommand.Trim().ToLowerInvariant();
        }

        return PawPalTrickCatalog.GetCommandLabel(trickId);
    }
}

public interface IPawPalVoiceCommandService
{
    bool IsAvailable { get; }
    bool HasPermission { get; }
    bool IsListening { get; }
    PawPalVoiceCommandFailureReason LastFailureReason { get; }
    IEnumerator RequestPermission(Action<bool, string> onCompleted);
    void ConfigurePhrases(IReadOnlyList<string> phrases);
    bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure);
    void StopListening();
    void Tick();
}

public sealed class PawPalUnavailableVoiceCommandService : IPawPalVoiceCommandService
{
    private readonly PawPalVoiceCommandFailureReason reason;
    private readonly string message;

    public PawPalUnavailableVoiceCommandService(PawPalVoiceCommandFailureReason unavailableReason, string unavailableMessage)
    {
        reason = unavailableReason;
        message = unavailableMessage;
    }

    public bool IsAvailable
    {
        get { return false; }
    }

    public bool HasPermission
    {
        get { return false; }
    }

    public bool IsListening
    {
        get { return false; }
    }

    public PawPalVoiceCommandFailureReason LastFailureReason
    {
        get { return reason; }
    }

    public IEnumerator RequestPermission(Action<bool, string> onCompleted)
    {
        if (onCompleted != null)
        {
            onCompleted(false, string.IsNullOrEmpty(message) ? "Voice is unavailable." : message);
        }

        yield break;
    }

    public void ConfigurePhrases(IReadOnlyList<string> phrases)
    {
    }

    public bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure)
    {
        if (onFailure != null)
        {
            onFailure(reason, string.IsNullOrEmpty(message) ? "Voice is unavailable." : message);
        }

        return false;
    }

    public void StopListening()
    {
    }

    public void Tick()
    {
    }
}

public sealed class PawPalButtonFallbackVoiceCommandService : IPawPalVoiceCommandService
{
    private readonly Queue<PawPalVoiceRecognizedPhrase> pendingRecognitions = new Queue<PawPalVoiceRecognizedPhrase>();
    private IReadOnlyList<string> phrases = new string[0];
    private Action<PawPalVoiceRecognizedPhrase> recognizedHandler;

    public bool IsAvailable
    {
        get { return true; }
    }

    public bool HasPermission
    {
        get { return true; }
    }

    public bool IsListening { get; private set; }

    public PawPalVoiceCommandFailureReason LastFailureReason
    {
        get { return PawPalVoiceCommandFailureReason.None; }
    }

    public IEnumerator RequestPermission(Action<bool, string> onCompleted)
    {
        if (onCompleted != null)
        {
            onCompleted(true, string.Empty);
        }

        yield break;
    }

    public void ConfigurePhrases(IReadOnlyList<string> configuredPhrases)
    {
        phrases = configuredPhrases ?? new string[0];
    }

    public bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure)
    {
        recognizedHandler = onRecognized;
        IsListening = true;
        if (phrases.Count > 0)
        {
            pendingRecognitions.Enqueue(new PawPalVoiceRecognizedPhrase
            {
                Transcript = phrases[0],
                Confidence01 = 1f
            });
        }

        return true;
    }

    public void StopListening()
    {
        IsListening = false;
        pendingRecognitions.Clear();
    }

    public void Tick()
    {
        if (!IsListening || pendingRecognitions.Count == 0 || recognizedHandler == null)
        {
            return;
        }

        recognizedHandler(pendingRecognitions.Dequeue());
    }
}

internal static class PawPalVoiceCommandServiceFactory
{
    public static IPawPalVoiceCommandService Create(PawPalVoiceInputController controller)
    {
#if UNITY_IOS && !UNITY_EDITOR
        return new PawPalIosVoiceCommandService(controller);
#elif UNITY_ANDROID && !UNITY_EDITOR
        return new PawPalAndroidVoiceCommandService();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return new PawPalWindowsKeywordVoiceCommandService();
#else
        return new PawPalUnavailableVoiceCommandService(PawPalVoiceCommandFailureReason.Unsupported, "Voice commands are unavailable on this platform.");
#endif
    }
}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
internal sealed class PawPalWindowsKeywordVoiceCommandService : IPawPalVoiceCommandService
{
    private readonly List<string> phrases = new List<string>();
    private KeywordRecognizer recognizer;
    private Action<PawPalVoiceRecognizedPhrase> recognizedHandler;
    private Action<PawPalVoiceCommandFailureReason, string> failureHandler;

    public bool IsAvailable
    {
        get { return true; }
    }

    public bool HasPermission
    {
        get { return true; }
    }

    public bool IsListening
    {
        get { return recognizer != null && recognizer.IsRunning; }
    }

    public PawPalVoiceCommandFailureReason LastFailureReason { get; private set; }

    public IEnumerator RequestPermission(Action<bool, string> onCompleted)
    {
        if (onCompleted != null)
        {
            onCompleted(true, string.Empty);
        }

        yield break;
    }

    public void ConfigurePhrases(IReadOnlyList<string> configuredPhrases)
    {
        phrases.Clear();
        if (configuredPhrases != null)
        {
            for (int i = 0; i < configuredPhrases.Count; i++)
            {
                string phrase = configuredPhrases[i];
                if (!string.IsNullOrWhiteSpace(phrase) && !phrases.Contains(phrase))
                {
                    phrases.Add(phrase);
                }
            }
        }

        if (IsListening)
        {
            RestartRecognizer();
        }
    }

    public bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure)
    {
        recognizedHandler = onRecognized;
        failureHandler = onFailure;

        if (phrases.Count == 0)
        {
            ReportFailure(PawPalVoiceCommandFailureReason.UnclearCommand, "No voice commands are available yet.");
            return false;
        }

        try
        {
            RestartRecognizer();
            return recognizer != null && recognizer.IsRunning;
        }
        catch (Exception exception)
        {
            ReportFailure(PawPalVoiceCommandFailureReason.Unsupported, "Windows speech recognition failed: " + exception.Message);
            return false;
        }
    }

    public void StopListening()
    {
        DisposeRecognizer();
    }

    public void Tick()
    {
    }

    private void RestartRecognizer()
    {
        DisposeRecognizer();
        if (phrases.Count == 0)
        {
            return;
        }

        recognizer = new KeywordRecognizer(phrases.ToArray(), ConfidenceLevel.Medium);
        recognizer.OnPhraseRecognized += HandlePhraseRecognized;
        recognizer.Start();
    }

    private void DisposeRecognizer()
    {
        if (recognizer == null)
        {
            return;
        }

        recognizer.OnPhraseRecognized -= HandlePhraseRecognized;
        if (recognizer.IsRunning)
        {
            recognizer.Stop();
        }

        recognizer.Dispose();
        recognizer = null;
    }

    private void HandlePhraseRecognized(PhraseRecognizedEventArgs args)
    {
        if (recognizedHandler == null)
        {
            return;
        }

        recognizedHandler(new PawPalVoiceRecognizedPhrase
        {
            Transcript = args.text,
            Confidence01 = MapConfidence(args.confidence)
        });
    }

    private void ReportFailure(PawPalVoiceCommandFailureReason reason, string message)
    {
        LastFailureReason = reason;
        if (failureHandler != null)
        {
            failureHandler(reason, message);
        }
    }

    private static float MapConfidence(ConfidenceLevel confidence)
    {
        switch (confidence)
        {
            case ConfidenceLevel.High:
                return 0.95f;
            case ConfidenceLevel.Medium:
                return 0.75f;
            case ConfidenceLevel.Low:
                return 0.45f;
            default:
                return 0.25f;
        }
    }
}
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
internal sealed class PawPalAndroidVoiceCommandService : IPawPalVoiceCommandService
{
    private const string SpeechRecognizerClass = "android.speech.SpeechRecognizer";
    private const string RecognizerIntentClass = "android.speech.RecognizerIntent";
    private const string ArrayListClass = "java.util.ArrayList";
    private const int ErrorAudio = 3;
    private const int ErrorClient = 5;
    private const int ErrorInsufficientPermissions = 9;
    private const int ErrorNetwork = 2;
    private const int ErrorNetworkTimeout = 1;
    private const int ErrorNoMatch = 7;
    private const int ErrorRecognizerBusy = 8;
    private const int ErrorServer = 4;
    private const int ErrorSpeechTimeout = 6;

    private readonly Queue<Action> callbackQueue = new Queue<Action>();
    private readonly object callbackLock = new object();

    private AndroidJavaObject recognizer;
    private AndroidJavaObject activity;
    private AndroidJavaObject intent;
    private RecognitionListenerProxy listenerProxy;
    private readonly List<string> phrases = new List<string>();
    private Action<PawPalVoiceRecognizedPhrase> recognizedHandler;
    private Action<PawPalVoiceCommandFailureReason, string> failureHandler;
    private bool shouldListen;
    private bool isStarting;

    public bool IsAvailable
    {
        get
        {
            try
            {
                EnsureActivity();
                using (AndroidJavaClass recognizerClass = new AndroidJavaClass(SpeechRecognizerClass))
                {
                    return recognizerClass.CallStatic<bool>("isRecognitionAvailable", activity);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    public bool HasPermission { get; private set; } = true;

    public bool IsListening { get; private set; }

    public PawPalVoiceCommandFailureReason LastFailureReason { get; private set; }

    public IEnumerator RequestPermission(Action<bool, string> onCompleted)
    {
        if (onCompleted != null)
        {
            onCompleted(true, string.Empty);
        }

        yield break;
    }

    public void ConfigurePhrases(IReadOnlyList<string> configuredPhrases)
    {
        phrases.Clear();
        if (configuredPhrases != null)
        {
            for (int i = 0; i < configuredPhrases.Count; i++)
            {
                string phrase = configuredPhrases[i];
                if (!string.IsNullOrWhiteSpace(phrase) && !phrases.Contains(phrase))
                {
                    phrases.Add(phrase);
                }
            }
        }

        if (recognizer != null)
        {
            if (intent != null)
            {
                intent.Dispose();
            }

            intent = CreateRecognizerIntent();
        }

        if (shouldListen)
        {
            RestartListening();
        }
    }

    public bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure)
    {
        recognizedHandler = onRecognized;
        failureHandler = onFailure;
        shouldListen = true;

        if (!IsAvailable)
        {
            ReportFailure(PawPalVoiceCommandFailureReason.Unsupported, "Android speech recognition is unavailable.");
            return false;
        }

        EnsureRecognizer();
        RestartListening();
        return true;
    }

    public void StopListening()
    {
        shouldListen = false;
        IsListening = false;
        isStarting = false;

        if (recognizer != null)
        {
            recognizer.Call("cancel");
            recognizer.Call("destroy");
            recognizer.Dispose();
            recognizer = null;
        }

        listenerProxy = null;
        if (intent != null)
        {
            intent.Dispose();
            intent = null;
        }
    }

    public void Tick()
    {
        while (true)
        {
            Action callback = null;
            lock (callbackLock)
            {
                if (callbackQueue.Count == 0)
                {
                    break;
                }

                callback = callbackQueue.Dequeue();
            }

            if (callback != null)
            {
                callback();
            }
        }
    }

    private void RestartListening()
    {
        if (!shouldListen)
        {
            return;
        }

        EnsureRecognizer();
        if (recognizer == null || intent == null || isStarting)
        {
            return;
        }

        isStarting = true;
        try
        {
            recognizer.Call("cancel");
            recognizer.Call("startListening", intent);
            IsListening = true;
        }
        catch (Exception exception)
        {
            IsListening = false;
            ReportFailure(PawPalVoiceCommandFailureReason.Unsupported, "Android speech recognition failed to start: " + exception.Message);
        }
        finally
        {
            isStarting = false;
        }
    }

    private void EnsureRecognizer()
    {
        if (recognizer != null)
        {
            return;
        }

        EnsureActivity();
        using (AndroidJavaClass recognizerClass = new AndroidJavaClass(SpeechRecognizerClass))
        {
            recognizer = recognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
            listenerProxy = new RecognitionListenerProxy(this);
            recognizer.Call("setRecognitionListener", listenerProxy);
        }

        intent = CreateRecognizerIntent();
    }

    private void EnsureActivity()
    {
        if (activity != null)
        {
            return;
        }

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }

    private AndroidJavaObject CreateRecognizerIntent()
    {
        using (AndroidJavaClass intentClass = new AndroidJavaClass(RecognizerIntentClass))
        {
            AndroidJavaObject recognizerIntent = new AndroidJavaObject("android.content.Intent", intentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH"));
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"), intentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), false);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_PREFER_OFFLINE"), true);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_MAX_RESULTS"), 3);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS"), 900L);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS"), 700L);

            using (AndroidJavaObject biasList = new AndroidJavaObject(ArrayListClass))
            {
                for (int i = 0; i < phrases.Count; i++)
                {
                    biasList.Call<bool>("add", phrases[i]);
                }

                try
                {
                    recognizerIntent.Call<AndroidJavaObject>("putStringArrayListExtra", "android.speech.extra.BIASING_STRINGS", biasList);
                }
                catch
                {
                    // Biasing strings are not available on every Android build; free-form recognition still works.
                }
            }

            return recognizerIntent;
        }
    }

    private void EnqueueCallback(Action callback)
    {
        lock (callbackLock)
        {
            callbackQueue.Enqueue(callback);
        }
    }

    private void HandleRecognitionResult(string transcript, float confidence01)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            RestartListening();
            return;
        }

        EnqueueCallback(delegate
        {
            if (recognizedHandler != null)
            {
                recognizedHandler(new PawPalVoiceRecognizedPhrase
                {
                    Transcript = transcript,
                    Confidence01 = confidence01
                });
            }

            RestartListening();
        });
    }

    private void HandleError(int errorCode)
    {
        if (!shouldListen && errorCode == ErrorClient)
        {
            return;
        }

        PawPalVoiceCommandFailureReason reason = MapFailureReason(errorCode);
        string message = BuildFailureMessage(errorCode);
        bool terminal = reason == PawPalVoiceCommandFailureReason.PermissionDenied
            || reason == PawPalVoiceCommandFailureReason.NoMicrophone
            || reason == PawPalVoiceCommandFailureReason.Unsupported;

        EnqueueCallback(delegate
        {
            if (terminal)
            {
                shouldListen = false;
                IsListening = false;
                ReportFailure(reason, message);
                return;
            }

            LastFailureReason = reason;
            RestartListening();
        });
    }

    private void ReportFailure(PawPalVoiceCommandFailureReason reason, string message)
    {
        LastFailureReason = reason;
        if (failureHandler != null)
        {
            failureHandler(reason, message);
        }
    }

    private static PawPalVoiceCommandFailureReason MapFailureReason(int errorCode)
    {
        switch (errorCode)
        {
            case ErrorInsufficientPermissions:
                return PawPalVoiceCommandFailureReason.PermissionDenied;
            case ErrorAudio:
                return PawPalVoiceCommandFailureReason.NoMicrophone;
            case ErrorRecognizerBusy:
                return PawPalVoiceCommandFailureReason.Busy;
            case ErrorNoMatch:
            case ErrorSpeechTimeout:
                return PawPalVoiceCommandFailureReason.UnclearCommand;
            case ErrorNetwork:
            case ErrorNetworkTimeout:
            case ErrorServer:
                return PawPalVoiceCommandFailureReason.Timeout;
            default:
                return PawPalVoiceCommandFailureReason.Unsupported;
        }
    }

    private static string BuildFailureMessage(int errorCode)
    {
        switch (errorCode)
        {
            case ErrorInsufficientPermissions:
                return "Microphone permission is needed.";
            case ErrorAudio:
                return "No microphone input is available.";
            case ErrorRecognizerBusy:
                return "Speech recognition is busy.";
            case ErrorNoMatch:
                return "No voice command was recognized.";
            case ErrorSpeechTimeout:
                return "Voice command listening timed out.";
            case ErrorNetwork:
            case ErrorNetworkTimeout:
            case ErrorServer:
                return "Speech recognition network access failed.";
            case ErrorClient:
                return "Speech recognition was interrupted.";
            default:
                return "Android speech recognition is unavailable.";
        }
    }

    private sealed class RecognitionListenerProxy : AndroidJavaProxy
    {
        private readonly PawPalAndroidVoiceCommandService owner;

        public RecognitionListenerProxy(PawPalAndroidVoiceCommandService service)
            : base("android.speech.RecognitionListener")
        {
            owner = service;
        }

        public void onReadyForSpeech(AndroidJavaObject bundle)
        {
            owner.IsListening = true;
        }

        public void onBeginningOfSpeech()
        {
        }

        public void onRmsChanged(float rmsdB)
        {
        }

        public void onBufferReceived(byte[] buffer)
        {
        }

        public void onEndOfSpeech()
        {
            owner.IsListening = false;
        }

        public void onError(int error)
        {
            owner.IsListening = false;
            owner.HandleError(error);
        }

        public void onResults(AndroidJavaObject results)
        {
            owner.IsListening = false;
            if (results == null)
            {
                owner.HandleRecognitionResult(string.Empty, 0f);
                return;
            }

            using (AndroidJavaClass speechRecognizer = new AndroidJavaClass(SpeechRecognizerClass))
            {
                AndroidJavaObject matches = results.Call<AndroidJavaObject>("getStringArrayList", speechRecognizer.GetStatic<string>("RESULTS_RECOGNITION"));
                float confidence = 0.8f;
                float[] scores = results.Call<float[]>("getFloatArray", speechRecognizer.GetStatic<string>("CONFIDENCE_SCORES"));
                if (scores != null && scores.Length > 0)
                {
                    confidence = Mathf.Clamp01(scores[0]);
                }

                if (matches == null || matches.Call<int>("size") <= 0)
                {
                    owner.HandleRecognitionResult(string.Empty, confidence);
                    return;
                }

                string transcript = matches.Call<string>("get", 0);
                owner.HandleRecognitionResult(transcript, confidence);
            }
        }

        public void onPartialResults(AndroidJavaObject partialResults)
        {
        }

        public void onEvent(int eventType, AndroidJavaObject parameters)
        {
        }
    }
}
#endif

#if UNITY_IOS && !UNITY_EDITOR
internal sealed class PawPalIosVoiceCommandService : IPawPalVoiceCommandService
{
    [DllImport("__Internal")]
    private static extern bool PawPalVoice_IsSpeechRecognitionAvailable();

    [DllImport("__Internal")]
    private static extern void PawPalVoice_SetCallbackTarget(string gameObjectName);

    [DllImport("__Internal")]
    private static extern void PawPalVoice_SetContextualPhrases(string joinedPhrases);

    [DllImport("__Internal")]
    private static extern void PawPalVoice_RequestSpeechPermission();

    [DllImport("__Internal")]
    private static extern void PawPalVoice_StartListening();

    [DllImport("__Internal")]
    private static extern void PawPalVoice_StopListening();

    private readonly PawPalVoiceInputController controller;
    private readonly List<string> phrases = new List<string>();
    private Action<PawPalVoiceRecognizedPhrase> recognizedHandler;
    private Action<PawPalVoiceCommandFailureReason, string> failureHandler;
    private bool permissionResolved;
    private bool permissionGranted;
    private string permissionMessage = string.Empty;

    public PawPalIosVoiceCommandService(PawPalVoiceInputController owner)
    {
        controller = owner;
        if (controller != null)
        {
            PawPalVoice_SetCallbackTarget(controller.gameObject.name);
        }
    }

    public bool IsAvailable
    {
        get { return PawPalVoice_IsSpeechRecognitionAvailable(); }
    }

    public bool HasPermission { get; private set; }

    public bool IsListening { get; private set; }

    public PawPalVoiceCommandFailureReason LastFailureReason { get; private set; }

    public IEnumerator RequestPermission(Action<bool, string> onCompleted)
    {
        permissionResolved = false;
        permissionGranted = false;
        permissionMessage = string.Empty;
        PawPalVoice_RequestSpeechPermission();

        float timeoutAt = Time.realtimeSinceStartup + 8f;
        while (!permissionResolved && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        if (!permissionResolved)
        {
            LastFailureReason = PawPalVoiceCommandFailureReason.Timeout;
            if (onCompleted != null)
            {
                onCompleted(false, "Speech recognition permission timed out.");
            }

            yield break;
        }

        HasPermission = permissionGranted;
        if (!permissionGranted)
        {
            LastFailureReason = PawPalVoiceCommandFailureReason.PermissionDenied;
        }

        if (onCompleted != null)
        {
            onCompleted(permissionGranted, permissionMessage);
        }
    }

    public void ConfigurePhrases(IReadOnlyList<string> configuredPhrases)
    {
        phrases.Clear();
        if (configuredPhrases != null)
        {
            for (int i = 0; i < configuredPhrases.Count; i++)
            {
                string phrase = configuredPhrases[i];
                if (!string.IsNullOrWhiteSpace(phrase) && !phrases.Contains(phrase))
                {
                    phrases.Add(phrase);
                }
            }
        }

        PawPalVoice_SetContextualPhrases(string.Join("\n", phrases.ToArray()));
    }

    public bool StartListening(Action<PawPalVoiceRecognizedPhrase> onRecognized, Action<PawPalVoiceCommandFailureReason, string> onFailure)
    {
        recognizedHandler = onRecognized;
        failureHandler = onFailure;

        if (!IsAvailable)
        {
            ReportFailure(PawPalVoiceCommandFailureReason.Unsupported, "iOS speech recognition is unavailable.");
            return false;
        }

        IsListening = true;
        PawPalVoice_StartListening();
        return true;
    }

    public void StopListening()
    {
        IsListening = false;
        PawPalVoice_StopListening();
    }

    public void Tick()
    {
    }

    public void HandleNativePermissionResult(string payload)
    {
        permissionResolved = true;
        permissionGranted = string.Equals(payload, "granted", StringComparison.OrdinalIgnoreCase);
        permissionMessage = permissionGranted ? string.Empty : "Speech recognition permission is needed.";
    }

    public void HandleNativeRecognizedPhrase(string transcript)
    {
        if (recognizedHandler == null || string.IsNullOrWhiteSpace(transcript))
        {
            return;
        }

        recognizedHandler(new PawPalVoiceRecognizedPhrase
        {
            Transcript = transcript,
            Confidence01 = 0.85f
        });
    }

    public void HandleNativeFailure(string payload)
    {
        string reasonToken = payload ?? string.Empty;
        string message = "iOS speech recognition stopped.";
        int separatorIndex = reasonToken.IndexOf('|');
        if (separatorIndex >= 0)
        {
            message = reasonToken.Substring(separatorIndex + 1);
            reasonToken = reasonToken.Substring(0, separatorIndex);
        }

        PawPalVoiceCommandFailureReason reason;
        if (!Enum.TryParse(reasonToken, true, out reason))
        {
            reason = PawPalVoiceCommandFailureReason.Unsupported;
        }

        if (reason != PawPalVoiceCommandFailureReason.UnclearCommand && reason != PawPalVoiceCommandFailureReason.Timeout)
        {
            IsListening = false;
            ReportFailure(reason, message);
        }
    }

    private void ReportFailure(PawPalVoiceCommandFailureReason reason, string message)
    {
        LastFailureReason = reason;
        if (failureHandler != null)
        {
            failureHandler(reason, message);
        }
    }
}
#endif
