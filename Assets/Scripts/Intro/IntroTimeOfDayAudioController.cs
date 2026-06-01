using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroTimeOfDayAudioController : MonoBehaviour
{
    private const string DayThemeSourceName = "IntroDayThemeAudio";
    private const string NightAmbienceSourceName = "IntroNightAmbienceAudio";
    private const string BirdsSourceName = "IntroBirdsAudio";

    [Header("Assets")]
    [SerializeField] private AudioClip dayThemeClip;
    [SerializeField] private AudioClip nightAmbienceClip;
    [SerializeField] private AudioClip birdsAmbientClip;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)] private float dayThemeVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float nightAmbienceVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float birdsAmbientVolume = 0.18f;

    [Header("Time")]
    [SerializeField] private IntroBackdropRingController sharedTimeOfDaySource;

    private AudioSource dayThemeSource;
    private AudioSource nightAmbienceSource;
    private AudioSource birdsSource;
    private bool lastAppliedNightState;
    private int lastAppliedMinuteStamp = int.MinValue;

    public void Configure(IntroSceneAssetCatalog catalog, IntroBackdropRingController backdropRingController)
    {
        sharedTimeOfDaySource = backdropRingController;
        dayThemeClip = catalog != null && catalog.DayThemeClip != null
            ? catalog.DayThemeClip
            : PawPalAudioResources.LoadClip(PawPalAudioResources.PawFriendsHome);
        nightAmbienceClip = catalog != null && catalog.NightAmbienceClip != null
            ? catalog.NightAmbienceClip
            : PawPalAudioResources.LoadClip(PawPalAudioResources.NightAmbience);
        birdsAmbientClip = catalog != null && catalog.BirdsAmbientClip != null
            ? catalog.BirdsAmbientClip
            : PawPalAudioResources.LoadClip(PawPalAudioResources.AmbientBirds);

        EnsureSources();
        ApplyAudioState(true);
    }

    private void OnEnable()
    {
        PawPalAudioSettings.MusicVolumeChanged += HandleAudioSettingsChanged;
        PawPalAudioSettings.SoundEffectsVolumeChanged += HandleAudioSettingsChanged;
        EnsureSources();
        ApplyAudioState(true);
    }

    private void Update()
    {
        ApplyAudioState(false);
    }

    private void OnDisable()
    {
        PawPalAudioSettings.MusicVolumeChanged -= HandleAudioSettingsChanged;
        PawPalAudioSettings.SoundEffectsVolumeChanged -= HandleAudioSettingsChanged;

        StopSource(dayThemeSource);
        StopSource(nightAmbienceSource);
        StopSource(birdsSource);
    }

    private void EnsureSources()
    {
        dayThemeSource = EnsureSource(dayThemeSource, DayThemeSourceName);
        nightAmbienceSource = EnsureSource(nightAmbienceSource, NightAmbienceSourceName);
        birdsSource = EnsureSource(birdsSource, BirdsSourceName);
    }

    private AudioSource EnsureSource(AudioSource existingSource, string sourceName)
    {
        if (existingSource != null)
        {
            ConfigureSource(existingSource);
            return existingSource;
        }

        Transform existingChild = transform.Find(sourceName);
        AudioSource source = null;
        if (existingChild != null)
        {
            source = existingChild.GetComponent<AudioSource>();
        }

        if (source == null)
        {
            GameObject sourceObject = existingChild != null ? existingChild.gameObject : new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = sourceObject.AddComponent<AudioSource>();
            }
        }

        ConfigureSource(source);
        return source;
    }

    private static void ConfigureSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
    }

    private void ApplyAudioState(bool force)
    {
        EnsureSources();

        PawPalTimeOfDayState timeState = ResolveTimeOfDayState();
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(timeState);
        bool useNightAudio = timeState.Daylight01 <= 0.001f;
        if (!force && minuteStamp == lastAppliedMinuteStamp && useNightAudio == lastAppliedNightState)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        lastAppliedNightState = useNightAudio;

        if (useNightAudio)
        {
            StopSource(dayThemeSource);
            StopSource(birdsSource);
            PlayLoop(nightAmbienceSource, nightAmbienceClip, PawPalAudioSettings.ApplyMusicVolume(nightAmbienceVolume));
            return;
        }

        StopSource(nightAmbienceSource);
        PlayLoop(dayThemeSource, dayThemeClip, PawPalAudioSettings.ApplyMusicVolume(dayThemeVolume));
        PlayLoop(birdsSource, birdsAmbientClip, PawPalAudioSettings.ApplySoundEffectsVolume(birdsAmbientVolume));
    }

    private PawPalTimeOfDayState ResolveTimeOfDayState()
    {
        if (sharedTimeOfDaySource != null)
        {
            return sharedTimeOfDaySource.EvaluateTimeOfDayState();
        }

        return PawPalTimeOfDayEvaluator.Evaluate(
            true,
            false,
            12f,
            7f,
            20f,
            1f);
    }

    private void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null)
        {
            return;
        }

        if (source.clip != clip)
        {
            source.Stop();
            source.clip = clip;
        }

        source.volume = volume;
        if (clip == null || volume <= 0f)
        {
            source.Stop();
            return;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null || !source.isPlaying)
        {
            return;
        }

        source.Stop();
    }

    private void HandleAudioSettingsChanged()
    {
        ApplyAudioState(true);
    }
}
