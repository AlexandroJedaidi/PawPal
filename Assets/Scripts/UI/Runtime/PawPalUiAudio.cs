using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PawPalUiClickSoundKind
{
    Auto,
    Button,
    Menu,
    Toggle,
    Whistle
}

public sealed class PawPalUiAudio : MonoBehaviour
{
    private const string ButtonClickResourcePath = "Audio/UI/click_button";
    private const string MenuClickResourcePath = "Audio/UI/click_menu";
    private const string ToggleClickResourcePath = "Audio/UI/click_toggle";
    private const string SuccessPopupResourcePath = "Audio/UI/success_popup";
    private const string WhistleResourcePath = "Audio/UI/whistle";
    private const string SparkleResourcePath = "Audio/UI/sparkle";
    private const string HeartsResourcePath = "Audio/hearts";

    private const float ClickVolume = 0.8f;
    private const float SuccessVolume = 1f;
    private const float WhistleVolume = 0.95f;
    private const float SparkleVolume = 0.9f;
    private const float HeartsVolume = 0.92f;

    private static PawPalUiAudio instance;

    private AudioSource uiAudioSource;
    private AudioClip buttonClickClip;
    private AudioClip menuClickClip;
    private AudioClip toggleClickClip;
    private AudioClip successPopupClip;
    private AudioClip whistleClip;
    private AudioClip sparkleClip;
    private AudioClip heartsClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInstall()
    {
        EnsureInstalled();
    }

    public static void EnsureInstalled()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("PawFriendsUiAudio");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<PawPalUiAudio>();
    }

    public static void AttachTo(Button button, PawPalUiClickSoundKind soundKind)
    {
        EnsureInstalled();
        if (button == null)
        {
            return;
        }

        PawPalUiButtonSound buttonSound = button.GetComponent<PawPalUiButtonSound>();
        if (buttonSound == null)
        {
            buttonSound = button.gameObject.AddComponent<PawPalUiButtonSound>();
        }

        buttonSound.Configure(soundKind);
    }

    public static void AttachTo(Button button)
    {
        AttachTo(button, PawPalUiClickSoundKind.Auto);
    }

    public static void PlayClick(PawPalUiClickSoundKind soundKind, GameObject source)
    {
        EnsureInstalled();
        if (instance == null)
        {
            return;
        }

        instance.PlayClickInternal(soundKind, source);
    }

    public static void PlaySuccessPopup()
    {
        EnsureInstalled();
        if (instance == null)
        {
            return;
        }

        instance.PlayClip(instance.successPopupClip, SuccessVolume);
    }

    public static void PlayWhistle()
    {
        EnsureInstalled();
        if (instance == null)
        {
            return;
        }

        instance.PlayClip(instance.whistleClip, WhistleVolume);
    }

    public static void PlaySparkle()
    {
        EnsureInstalled();
        if (instance == null)
        {
            return;
        }

        instance.PlayClip(instance.sparkleClip, SparkleVolume);
    }

    public static void PlayHearts()
    {
        EnsureInstalled();
        if (instance == null)
        {
            return;
        }

        instance.PlayClip(instance.heartsClip, HeartsVolume);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        uiAudioSource = gameObject.GetComponent<AudioSource>();
        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;

        EnsureAudioListener();
        LoadAudioClips();
        StartCoroutine(BindButtonsNextFrame());
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
        EnsureAudioListener();
        StartCoroutine(BindButtonsNextFrame());
    }

    private IEnumerator BindButtonsNextFrame()
    {
        yield return null;
        BindAllSceneButtons();
    }

    private void BindAllSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            AttachTo(buttons[i]);
        }
    }

    private void LoadAudioClips()
    {
        buttonClickClip = LoadClip(ButtonClickResourcePath);
        menuClickClip = LoadClip(MenuClickResourcePath);
        toggleClickClip = LoadClip(ToggleClickResourcePath);
        successPopupClip = LoadClip(SuccessPopupResourcePath);
        whistleClip = LoadClip(WhistleResourcePath);
        sparkleClip = LoadClip(SparkleResourcePath);
        heartsClip = LoadClip(HeartsResourcePath);
    }

    private AudioClip LoadClip(string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning("PawPalUiAudio could not load audio clip at Resources/" + resourcePath + ".");
        }

        return clip;
    }

    private void PlayClickInternal(PawPalUiClickSoundKind soundKind, GameObject source)
    {
        PawPalUiClickSoundKind resolvedKind = soundKind == PawPalUiClickSoundKind.Auto
            ? InferClickSoundKind(source)
            : soundKind;

        AudioClip clip = buttonClickClip;
        switch (resolvedKind)
        {
            case PawPalUiClickSoundKind.Menu:
                clip = menuClickClip != null ? menuClickClip : buttonClickClip;
                break;
            case PawPalUiClickSoundKind.Toggle:
                clip = toggleClickClip != null ? toggleClickClip : buttonClickClip;
                break;
            case PawPalUiClickSoundKind.Whistle:
                clip = whistleClip != null ? whistleClip : buttonClickClip;
                break;
            default:
                clip = buttonClickClip;
                break;
        }

        PlayClip(clip, resolvedKind == PawPalUiClickSoundKind.Whistle ? WhistleVolume : ClickVolume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null || uiAudioSource == null)
        {
            return;
        }

        float effectiveVolume = PawPalAudioSettings.ApplySoundEffectsVolume(volume);
        if (effectiveVolume <= 0f)
        {
            return;
        }

        uiAudioSource.PlayOneShot(clip, effectiveVolume);
    }

    private static PawPalUiClickSoundKind InferClickSoundKind(GameObject source)
    {
        string searchText = BuildSearchText(source);
        if (ContainsAny(
                searchText,
                "toggle",
                "switch",
                "favorite",
                "favourite",
                "decrease",
                "increase",
                "minus",
                "plus",
                "release",
                "adopt"))
        {
            return PawPalUiClickSoundKind.Toggle;
        }

        if (ContainsAny(
                searchText,
                "back",
                "close",
                "menu",
                "nav",
                "inventory",
                "camera",
                "cam",
                "mic",
                "category",
                "tab",
                "settings",
                "shop",
                "profile",
                "map",
                "home",
                "language",
                "notification",
                "details",
                "social",
                "chat",
                "join",
                "points",
                "next",
                "previous",
                "forward"))
        {
            return PawPalUiClickSoundKind.Menu;
        }

        return PawPalUiClickSoundKind.Button;
    }

    private static string BuildSearchText(GameObject source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        string searchText = source.name;
        TMP_Text[] textComponents = source.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text text = textComponents[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            searchText += " " + text.text;
        }

        return searchText.ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (text.Contains(tokens[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureAudioListener()
    {
        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                {
                    targetCamera = cameras[i];
                    break;
                }
            }
        }

        AudioListener preferredListener = null;
        if (targetCamera != null)
        {
            preferredListener = targetCamera.GetComponent<AudioListener>();
            if (preferredListener == null)
            {
                preferredListener = targetCamera.gameObject.AddComponent<AudioListener>();
            }
        }
        else if (instance != null)
        {
            preferredListener = instance.GetComponent<AudioListener>();
            if (preferredListener == null)
            {
                preferredListener = instance.gameObject.AddComponent<AudioListener>();
            }
        }

        if (preferredListener == null)
        {
            return;
        }

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
            {
                continue;
            }

            listener.enabled = listener == preferredListener;
        }
    }
}
