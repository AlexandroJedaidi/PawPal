using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum PawPalVoiceTrick
{
    Sit
}

public struct PawPalVoiceMatchResult
{
    public PawPalVoiceMatchResult(bool isMatch, float confidence, string label)
    {
        IsMatch = isMatch;
        Confidence = confidence;
        Label = label;
    }

    public bool IsMatch;
    public float Confidence;
    public string Label;
}

[Serializable]
public sealed class PawPalVoiceTemplate
{
    public int FeatureVersion;
    public float DurationSeconds;
    public float Rms;
    public List<float> Features = new List<float>();
}

[Serializable]
public sealed class PawPalVoiceTrickProfile
{
    public string TrickId;
    public bool RewardGranted;
    public List<PawPalVoiceTemplate> Samples = new List<PawPalVoiceTemplate>();
}

[Serializable]
public sealed class PawPalVoiceDogProfile
{
    public string DogId;
    public string LearnedDisplayName;
    public List<PawPalVoiceTemplate> NameSamples = new List<PawPalVoiceTemplate>();
    public List<PawPalVoiceTrickProfile> Tricks = new List<PawPalVoiceTrickProfile>();
}

[Serializable]
public sealed class PawPalVoiceProfileSaveData
{
    public int Version = 1;
    public List<PawPalVoiceDogProfile> Dogs = new List<PawPalVoiceDogProfile>();
}

public sealed class PawPalVoiceProfileStore
{
    private const string SaveFileName = "pawpal_voice_profiles_v1.json";
    private readonly string savePath;
    private PawPalVoiceProfileSaveData saveData = new PawPalVoiceProfileSaveData();

    public PawPalVoiceProfileStore()
    {
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        Load();
    }

    public string SavePath
    {
        get { return savePath; }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(savePath))
            {
                saveData = new PawPalVoiceProfileSaveData();
                return;
            }

            string json = File.ReadAllText(savePath);
            PawPalVoiceProfileSaveData loaded = JsonUtility.FromJson<PawPalVoiceProfileSaveData>(json);
            saveData = loaded != null ? loaded : new PawPalVoiceProfileSaveData();
            SanitizeSaveData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalVoiceProfileStore could not load voice profiles: " + exception.Message);
            saveData = new PawPalVoiceProfileSaveData();
        }
    }

    public void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SanitizeSaveData();
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalVoiceProfileStore could not save voice profiles: " + exception.Message);
        }
    }

    public int GetNameSampleCount(string dogId, string displayName)
    {
        PawPalVoiceDogProfile profile = FindDogProfile(dogId);
        if (profile == null || !IsSameDisplayName(profile, displayName))
        {
            return 0;
        }

        EnsureProfileLists(profile);
        return profile.NameSamples.Count;
    }

    public int GetTrickSampleCount(string dogId, PawPalVoiceTrick trick)
    {
        PawPalVoiceTrickProfile trickProfile = FindTrickProfile(FindDogProfile(dogId), trick);
        return trickProfile != null && trickProfile.Samples != null ? trickProfile.Samples.Count : 0;
    }

    public bool IsDogNameLearned(string dogId, string displayName, int requiredSamples)
    {
        return GetNameSampleCount(dogId, displayName) >= Mathf.Max(1, requiredSamples);
    }

    public bool IsTrickLearned(string dogId, PawPalVoiceTrick trick, int requiredSamples)
    {
        return GetTrickSampleCount(dogId, trick) >= Mathf.Max(1, requiredSamples);
    }

    public void AddNameSample(string dogId, string displayName, PawPalVoiceTemplate template, int maxSamples)
    {
        if (template == null)
        {
            return;
        }

        PawPalVoiceDogProfile profile = GetOrCreateDogProfile(dogId);
        EnsureProfileLists(profile);

        if (!IsSameDisplayName(profile, displayName))
        {
            profile.NameSamples.Clear();
            profile.LearnedDisplayName = displayName;
        }

        AddTemplate(profile.NameSamples, template, maxSamples);
        Save();
    }

    public void AddTrickSample(string dogId, PawPalVoiceTrick trick, PawPalVoiceTemplate template, int maxSamples)
    {
        if (template == null)
        {
            return;
        }

        PawPalVoiceDogProfile profile = GetOrCreateDogProfile(dogId);
        PawPalVoiceTrickProfile trickProfile = GetOrCreateTrickProfile(profile, trick);
        AddTemplate(trickProfile.Samples, template, maxSamples);
        Save();
    }

    public bool HasTrickRewardGranted(string dogId, PawPalVoiceTrick trick)
    {
        PawPalVoiceTrickProfile trickProfile = FindTrickProfile(FindDogProfile(dogId), trick);
        return trickProfile != null && trickProfile.RewardGranted;
    }

    public void MarkTrickRewardGranted(string dogId, PawPalVoiceTrick trick)
    {
        PawPalVoiceDogProfile profile = GetOrCreateDogProfile(dogId);
        PawPalVoiceTrickProfile trickProfile = GetOrCreateTrickProfile(profile, trick);
        if (!trickProfile.RewardGranted)
        {
            trickProfile.RewardGranted = true;
            Save();
        }
    }

    public PawPalVoiceMatchResult MatchName(string dogId, string displayName, PawPalVoiceTemplate input, int requiredSamples, float threshold)
    {
        PawPalVoiceDogProfile profile = FindDogProfile(dogId);
        if (profile == null || !IsDogNameLearned(dogId, displayName, requiredSamples))
        {
            return new PawPalVoiceMatchResult(false, 0f, displayName);
        }

        float confidence = GetBestScore(profile.NameSamples, input);
        return new PawPalVoiceMatchResult(confidence >= threshold, confidence, profile.LearnedDisplayName);
    }

    public PawPalVoiceMatchResult MatchTrick(string dogId, PawPalVoiceTrick trick, PawPalVoiceTemplate input, int requiredSamples, float threshold)
    {
        PawPalVoiceTrickProfile trickProfile = FindTrickProfile(FindDogProfile(dogId), trick);
        if (trickProfile == null || !IsTrickLearned(dogId, trick, requiredSamples))
        {
            return new PawPalVoiceMatchResult(false, 0f, GetTrickLabel(trick));
        }

        float confidence = GetBestScore(trickProfile.Samples, input);
        return new PawPalVoiceMatchResult(confidence >= threshold, confidence, GetTrickLabel(trick));
    }

    public static string GetTrickLabel(PawPalVoiceTrick trick)
    {
        switch (trick)
        {
            case PawPalVoiceTrick.Sit:
                return "sit";
            default:
                return trick.ToString();
        }
    }

    private PawPalVoiceDogProfile GetOrCreateDogProfile(string dogId)
    {
        string normalizedDogId = NormalizeDogId(dogId);
        PawPalVoiceDogProfile profile = FindDogProfile(normalizedDogId);
        if (profile != null)
        {
            return profile;
        }

        profile = new PawPalVoiceDogProfile
        {
            DogId = normalizedDogId
        };
        saveData.Dogs.Add(profile);
        return profile;
    }

    private PawPalVoiceDogProfile FindDogProfile(string dogId)
    {
        if (saveData == null || saveData.Dogs == null)
        {
            return null;
        }

        string normalizedDogId = NormalizeDogId(dogId);
        for (int i = 0; i < saveData.Dogs.Count; i++)
        {
            PawPalVoiceDogProfile profile = saveData.Dogs[i];
            if (profile != null && string.Equals(profile.DogId, normalizedDogId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureProfileLists(profile);
                return profile;
            }
        }

        return null;
    }

    private PawPalVoiceTrickProfile GetOrCreateTrickProfile(PawPalVoiceDogProfile profile, PawPalVoiceTrick trick)
    {
        EnsureProfileLists(profile);
        PawPalVoiceTrickProfile trickProfile = FindTrickProfile(profile, trick);
        if (trickProfile != null)
        {
            return trickProfile;
        }

        trickProfile = new PawPalVoiceTrickProfile
        {
            TrickId = trick.ToString(),
            Samples = new List<PawPalVoiceTemplate>()
        };
        profile.Tricks.Add(trickProfile);
        return trickProfile;
    }

    private PawPalVoiceTrickProfile FindTrickProfile(PawPalVoiceDogProfile profile, PawPalVoiceTrick trick)
    {
        if (profile == null)
        {
            return null;
        }

        EnsureProfileLists(profile);
        string trickId = trick.ToString();
        for (int i = 0; i < profile.Tricks.Count; i++)
        {
            PawPalVoiceTrickProfile trickProfile = profile.Tricks[i];
            if (trickProfile == null)
            {
                continue;
            }

            if (string.Equals(trickProfile.TrickId, trickId, StringComparison.OrdinalIgnoreCase))
            {
                if (trickProfile.Samples == null)
                {
                    trickProfile.Samples = new List<PawPalVoiceTemplate>();
                }

                return trickProfile;
            }
        }

        return null;
    }

    private static void AddTemplate(List<PawPalVoiceTemplate> templates, PawPalVoiceTemplate template, int maxSamples)
    {
        if (templates == null)
        {
            return;
        }

        templates.Add(template);
        int limit = Mathf.Max(1, maxSamples);
        while (templates.Count > limit)
        {
            templates.RemoveAt(0);
        }
    }

    private static float GetBestScore(List<PawPalVoiceTemplate> templates, PawPalVoiceTemplate input)
    {
        if (templates == null || input == null)
        {
            return 0f;
        }

        float bestScore = 0f;
        for (int i = 0; i < templates.Count; i++)
        {
            bestScore = Mathf.Max(bestScore, PawPalVoiceFeatureExtractor.Compare(templates[i], input));
        }

        return bestScore;
    }

    private void SanitizeSaveData()
    {
        if (saveData == null)
        {
            saveData = new PawPalVoiceProfileSaveData();
        }

        if (saveData.Dogs == null)
        {
            saveData.Dogs = new List<PawPalVoiceDogProfile>();
        }

        for (int i = 0; i < saveData.Dogs.Count; i++)
        {
            EnsureProfileLists(saveData.Dogs[i]);
        }
    }

    private static void EnsureProfileLists(PawPalVoiceDogProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        if (profile.NameSamples == null)
        {
            profile.NameSamples = new List<PawPalVoiceTemplate>();
        }

        if (profile.Tricks == null)
        {
            profile.Tricks = new List<PawPalVoiceTrickProfile>();
        }

        for (int i = 0; i < profile.Tricks.Count; i++)
        {
            if (profile.Tricks[i] != null && profile.Tricks[i].Samples == null)
            {
                profile.Tricks[i].Samples = new List<PawPalVoiceTemplate>();
            }
        }
    }

    private static bool IsSameDisplayName(PawPalVoiceDogProfile profile, string displayName)
    {
        return profile != null
            && !string.IsNullOrWhiteSpace(profile.LearnedDisplayName)
            && string.Equals(profile.LearnedDisplayName, displayName, StringComparison.Ordinal);
    }

    private static string NormalizeDogId(string dogId)
    {
        return string.IsNullOrWhiteSpace(dogId) ? "active_dog" : dogId.Trim();
    }
}

public static class PawPalVoiceFeatureExtractor
{
    public const int FeatureVersion = 1;
    private const int FeatureBinCount = 32;
    private const float MinimumPeakAmplitude = 0.025f;
    private const float MinimumRms = 0.006f;
    private const float MinimumTrimmedSeconds = 0.16f;
    private const float TrimPeakRatio = 0.12f;
    private const float MinimumTrimThreshold = 0.012f;

    public static bool TryCreateTemplate(float[] interleavedSamples, int channels, int sampleRate, out PawPalVoiceTemplate template, out string failureReason)
    {
        template = null;
        failureReason = string.Empty;

        if (interleavedSamples == null || interleavedSamples.Length == 0)
        {
            failureReason = "No voice was recorded.";
            return false;
        }

        channels = Mathf.Max(1, channels);
        sampleRate = Mathf.Max(1, sampleRate);
        int frameCount = interleavedSamples.Length / channels;
        if (frameCount <= 0)
        {
            failureReason = "No voice was recorded.";
            return false;
        }

        float[] monoSamples = new float[frameCount];
        float peak = 0f;
        double totalSquares = 0d;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int sourceIndex = frame * channels;
            float value = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                value += interleavedSamples[sourceIndex + channel];
            }

            value /= channels;
            monoSamples[frame] = value;
            float absolute = Mathf.Abs(value);
            peak = Mathf.Max(peak, absolute);
            totalSquares += value * value;
        }

        float rms = Mathf.Sqrt((float)(totalSquares / frameCount));
        if (peak < MinimumPeakAmplitude || rms < MinimumRms)
        {
            failureReason = "I could barely hear that.";
            return false;
        }

        float trimThreshold = Mathf.Max(MinimumTrimThreshold, peak * TrimPeakRatio);
        int startFrame = 0;
        while (startFrame < frameCount && Mathf.Abs(monoSamples[startFrame]) < trimThreshold)
        {
            startFrame++;
        }

        int endFrame = frameCount - 1;
        while (endFrame > startFrame && Mathf.Abs(monoSamples[endFrame]) < trimThreshold)
        {
            endFrame--;
        }

        int trimmedFrameCount = endFrame - startFrame + 1;
        float trimmedSeconds = trimmedFrameCount / (float)sampleRate;
        if (trimmedFrameCount <= 0 || trimmedSeconds < MinimumTrimmedSeconds)
        {
            failureReason = "That was too short.";
            return false;
        }

        List<float> features = new List<float>(FeatureBinCount * 2);
        for (int bin = 0; bin < FeatureBinCount; bin++)
        {
            int binStart = startFrame + Mathf.FloorToInt(trimmedFrameCount * (bin / (float)FeatureBinCount));
            int binEnd = startFrame + Mathf.FloorToInt(trimmedFrameCount * ((bin + 1f) / FeatureBinCount));
            binEnd = Mathf.Clamp(binEnd, binStart + 1, endFrame + 1);

            double energy = 0d;
            int count = Mathf.Max(1, binEnd - binStart);
            for (int i = binStart; i < binEnd; i++)
            {
                float value = monoSamples[i];
                energy += value * value;
            }

            float binRms = Mathf.Sqrt((float)(energy / count));
            features.Add(Mathf.Clamp01(binRms / peak));
        }

        for (int bin = 0; bin < FeatureBinCount; bin++)
        {
            int binStart = startFrame + Mathf.FloorToInt(trimmedFrameCount * (bin / (float)FeatureBinCount));
            int binEnd = startFrame + Mathf.FloorToInt(trimmedFrameCount * ((bin + 1f) / FeatureBinCount));
            binEnd = Mathf.Clamp(binEnd, binStart + 1, endFrame + 1);

            int crossings = 0;
            float previous = monoSamples[binStart];
            for (int i = binStart + 1; i < binEnd; i++)
            {
                float current = monoSamples[i];
                if ((previous < 0f && current >= 0f) || (previous >= 0f && current < 0f))
                {
                    crossings++;
                }

                previous = current;
            }

            int denominator = Mathf.Max(1, binEnd - binStart - 1);
            features.Add(Mathf.Clamp01(crossings / (float)denominator));
        }

        Normalize(features);
        template = new PawPalVoiceTemplate
        {
            FeatureVersion = FeatureVersion,
            DurationSeconds = trimmedSeconds,
            Rms = rms,
            Features = features
        };
        return true;
    }

    public static float Compare(PawPalVoiceTemplate a, PawPalVoiceTemplate b)
    {
        if (a == null
            || b == null
            || a.FeatureVersion != b.FeatureVersion
            || a.Features == null
            || b.Features == null
            || a.Features.Count != b.Features.Count)
        {
            return 0f;
        }

        float dot = 0f;
        for (int i = 0; i < a.Features.Count; i++)
        {
            dot += a.Features[i] * b.Features[i];
        }

        float durationRatio = Mathf.Min(a.DurationSeconds, b.DurationSeconds) / Mathf.Max(0.01f, Mathf.Max(a.DurationSeconds, b.DurationSeconds));
        float durationWeight = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(durationRatio));
        return Mathf.Clamp01(dot * durationWeight);
    }

    private static void Normalize(List<float> features)
    {
        if (features == null || features.Count == 0)
        {
            return;
        }

        double sumSquares = 0d;
        for (int i = 0; i < features.Count; i++)
        {
            sumSquares += features[i] * features[i];
        }

        float magnitude = Mathf.Sqrt((float)sumSquares);
        if (magnitude <= 0.0001f)
        {
            return;
        }

        for (int i = 0; i < features.Count; i++)
        {
            features[i] = features[i] / magnitude;
        }
    }
}
