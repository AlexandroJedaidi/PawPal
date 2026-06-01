using System;
using UnityEngine;

public struct PawPalTimeOfDayState
{
    public float LocalHour;
    public float Daylight01;
}

public struct PawPalPetCircadianState
{
    public float LocalHour;
    public float Activity01;
    public float Sleepiness01;
    public bool ForceSleep;
}

public static class PawPalTimeOfDayEvaluator
{
    public static PawPalTimeOfDayState Evaluate(
        bool useDeviceLocalTime,
        bool usePreviewHour,
        float previewHour,
        float sunriseHour,
        float sunsetHour,
        float transitionHours)
    {
        float resolvedHour = usePreviewHour || !useDeviceLocalTime
            ? NormalizeHour(previewHour)
            : NormalizeHour((float)DateTime.Now.TimeOfDay.TotalHours);

        return new PawPalTimeOfDayState
        {
            LocalHour = resolvedHour,
            Daylight01 = EvaluateDaylight01(resolvedHour, sunriseHour, sunsetHour, transitionHours)
        };
    }

    public static int ToMinuteStamp(PawPalTimeOfDayState state)
    {
        return Mathf.RoundToInt(NormalizeHour(state.LocalHour) * 60f);
    }

    public static float NormalizeHour(float hour)
    {
        float normalized = hour % 24f;
        if (normalized < 0f)
        {
            normalized += 24f;
        }

        return normalized;
    }

    public static float EvaluateDaylight01(float localHour, float sunriseHour, float sunsetHour, float transitionHours)
    {
        float sunrise = NormalizeHour(sunriseHour);
        float sunset = NormalizeHour(sunsetHour);
        float transition = Mathf.Clamp(transitionHours, 0.05f, 6f);
        float hour = NormalizeHour(localHour);

        if (sunset <= sunrise)
        {
            sunset = sunrise + 12f;
        }

        float dawnStart = sunrise - transition;
        float duskEnd = sunset + transition;
        float continuousHour = hour;
        if (continuousHour < dawnStart)
        {
            continuousHour += 24f;
        }

        if (continuousHour < dawnStart)
        {
            return 0f;
        }

        if (continuousHour < sunrise)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnStart, sunrise, continuousHour));
        }

        if (continuousHour <= sunset)
        {
            return 1f;
        }

        if (continuousHour < duskEnd)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sunset, duskEnd, continuousHour));
        }

        return 0f;
    }

    public static PawPalPetCircadianState EvaluatePetCircadian(
        float localHour,
        float morningStartHour,
        float daySteadyStartHour,
        float eveningStartHour,
        float nightStartHour,
        float wakeTransitionHours,
        float eveningTransitionHours)
    {
        float hour = NormalizeHour(localHour);
        float morningStart = NormalizeHour(morningStartHour);
        float daySteadyStart = NormalizeHour(daySteadyStartHour);
        float eveningStart = NormalizeHour(eveningStartHour);
        float nightStart = NormalizeHour(nightStartHour);
        float wakeTransition = Mathf.Clamp(wakeTransitionHours, 0.05f, 6f);
        float eveningTransition = Mathf.Clamp(eveningTransitionHours, 0.05f, 6f);
        float wakeStart = NormalizeHour(morningStart - wakeTransition);
        float eveningSettleStart = NormalizeHour(Mathf.Min(nightStart, eveningStart + eveningTransition));

        PawPalPetCircadianState state = new PawPalPetCircadianState
        {
            LocalHour = hour,
            Activity01 = 0.2f,
            Sleepiness01 = 0.9f,
            ForceSleep = false
        };

        if (IsHourInWrappedRange(hour, nightStart, wakeStart))
        {
            state.Activity01 = 0.02f;
            state.Sleepiness01 = 1f;
            state.ForceSleep = true;
            return state;
        }

        if (IsHourInWrappedRange(hour, wakeStart, morningStart))
        {
            float t = InverseLerpWrapped(wakeStart, morningStart, hour);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            state.Activity01 = Mathf.Lerp(0.08f, 0.62f, eased);
            state.Sleepiness01 = Mathf.Lerp(0.98f, 0.46f, eased);
            return state;
        }

        if (IsHourInWrappedRange(hour, morningStart, daySteadyStart))
        {
            float t = InverseLerpWrapped(morningStart, daySteadyStart, hour);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            state.Activity01 = Mathf.Lerp(0.74f, 1f, eased);
            state.Sleepiness01 = Mathf.Lerp(0.34f, 0.16f, eased);
            return state;
        }

        if (IsHourInWrappedRange(hour, daySteadyStart, eveningStart))
        {
            float t = InverseLerpWrapped(daySteadyStart, eveningStart, hour);
            state.Activity01 = Mathf.Lerp(0.96f, 0.76f, t);
            state.Sleepiness01 = Mathf.Lerp(0.16f, 0.36f, t);
            return state;
        }

        if (IsHourInWrappedRange(hour, eveningStart, eveningSettleStart))
        {
            float t = InverseLerpWrapped(eveningStart, eveningSettleStart, hour);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            state.Activity01 = Mathf.Lerp(0.72f, 0.42f, eased);
            state.Sleepiness01 = Mathf.Lerp(0.42f, 0.72f, eased);
            return state;
        }

        float lateEveningT = InverseLerpWrapped(eveningSettleStart, nightStart, hour);
        float lateEveningEased = Mathf.SmoothStep(0f, 1f, lateEveningT);
        state.Activity01 = Mathf.Lerp(0.4f, 0.1f, lateEveningEased);
        state.Sleepiness01 = Mathf.Lerp(0.72f, 0.96f, lateEveningEased);
        return state;
    }

    private static bool IsHourInWrappedRange(float hour, float start, float end)
    {
        hour = NormalizeHour(hour);
        start = NormalizeHour(start);
        end = NormalizeHour(end);

        if (Mathf.Approximately(start, end))
        {
            return false;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }

    private static float InverseLerpWrapped(float start, float end, float hour)
    {
        start = NormalizeHour(start);
        end = NormalizeHour(end);
        hour = NormalizeHour(hour);

        if (end <= start)
        {
            end += 24f;
        }

        if (hour < start)
        {
            hour += 24f;
        }

        if (Mathf.Approximately(start, end))
        {
            return 1f;
        }

        return Mathf.Clamp01(Mathf.InverseLerp(start, end, hour));
    }
}
