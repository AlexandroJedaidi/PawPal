using System;
using UnityEngine;

public static class PawPalAudioSettings
{
    public const float VolumeStep = 0.1f;

    private const string MusicVolumeKey = "pawpal_audio_music_volume_v1";
    private const string SoundEffectsVolumeKey = "pawpal_audio_sfx_volume_v1";
    private const float DefaultVolume = 1f;

    private static bool loaded;
    private static float musicVolume = DefaultVolume;
    private static float soundEffectsVolume = DefaultVolume;

    public static event Action MusicVolumeChanged;
    public static event Action SoundEffectsVolumeChanged;

    public static float MusicVolume
    {
        get
        {
            EnsureLoaded();
            return musicVolume;
        }
    }

    public static float SoundEffectsVolume
    {
        get
        {
            EnsureLoaded();
            return soundEffectsVolume;
        }
    }

    public static void AdjustMusicVolume(float delta)
    {
        SetMusicVolume(MusicVolume + delta);
    }

    public static void AdjustSoundEffectsVolume(float delta)
    {
        SetSoundEffectsVolume(SoundEffectsVolume + delta);
    }

    public static void SetMusicVolume(float volume)
    {
        EnsureLoaded();
        float clampedVolume = NormalizeStep(volume);
        if (Mathf.Approximately(musicVolume, clampedVolume))
        {
            return;
        }

        musicVolume = clampedVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.Save();
        MusicVolumeChanged?.Invoke();
    }

    public static void SetSoundEffectsVolume(float volume)
    {
        EnsureLoaded();
        float clampedVolume = NormalizeStep(volume);
        if (Mathf.Approximately(soundEffectsVolume, clampedVolume))
        {
            return;
        }

        soundEffectsVolume = clampedVolume;
        PlayerPrefs.SetFloat(SoundEffectsVolumeKey, soundEffectsVolume);
        PlayerPrefs.Save();
        SoundEffectsVolumeChanged?.Invoke();
    }

    public static float ApplyMusicVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * MusicVolume;
    }

    public static float ApplySoundEffectsVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * SoundEffectsVolume;
    }

    public static int ToPercent(float volume)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        musicVolume = NormalizeStep(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
        soundEffectsVolume = NormalizeStep(PlayerPrefs.GetFloat(SoundEffectsVolumeKey, DefaultVolume));
        loaded = true;
    }

    private static float NormalizeStep(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        return Mathf.Clamp01(Mathf.Round(clampedVolume / VolumeStep) * VolumeStep);
    }
}
