using UnityEngine;

public enum PawPalWalkLeashGestureType
{
    None,
    RunForward,
    StopBackward,
    JumpUp
}

[System.Serializable]
public sealed class PawPalWalkLeashGestureSettings
{
    [SerializeField, Min(0.01f)] private float minimumDragVelocity = 1.45f;
    [SerializeField, Min(0.001f)] private float minimumPlanarDelta = 0.08f;
    [SerializeField, Range(0.05f, 1f)] private float forwardDotThreshold = 0.58f;
    [SerializeField, Range(0.05f, 1f)] private float backwardDotThreshold = 0.58f;
    [SerializeField, Min(0.001f)] private float upwardDelta = 0.12f;
    [SerializeField, Min(0.01f)] private float reactionCooldown = 0.85f;
    [SerializeField, Min(0.05f)] private float stopReactionDuration = 0.9f;
    [SerializeField, Min(0.05f)] private float jumpReactionDuration = 0.85f;
    [SerializeField, Min(0.05f)] private float runBurstDuration = 1.35f;

    public float MinimumDragVelocity => Mathf.Max(0.01f, minimumDragVelocity);
    public float MinimumPlanarDelta => Mathf.Max(0.001f, minimumPlanarDelta);
    public float ForwardDotThreshold => Mathf.Clamp(forwardDotThreshold, 0.05f, 1f);
    public float BackwardDotThreshold => Mathf.Clamp(backwardDotThreshold, 0.05f, 1f);
    public float UpwardDelta => Mathf.Max(0.001f, upwardDelta);
    public float ReactionCooldown => Mathf.Max(0.01f, reactionCooldown);
    public float StopReactionDuration => Mathf.Max(0.05f, stopReactionDuration);
    public float JumpReactionDuration => Mathf.Max(0.05f, jumpReactionDuration);
    public float RunBurstDuration => Mathf.Max(0.05f, runBurstDuration);
}

public static class PawPalWalkLeashGestureClassifier
{
    public static bool IsReactionAllowed(float currentTime, float nextReactionAllowedAt)
    {
        return currentTime >= nextReactionAllowedAt;
    }

    public static PawPalWalkLeashGestureType Classify(
        Vector3 dragDelta,
        Vector3 dragVelocity,
        Vector3 routeDirection,
        PawPalWalkLeashGestureSettings settings)
    {
        if (settings == null)
        {
            settings = new PawPalWalkLeashGestureSettings();
        }

        if (dragVelocity.magnitude < settings.MinimumDragVelocity)
        {
            return PawPalWalkLeashGestureType.None;
        }

        if (dragDelta.y >= settings.UpwardDelta)
        {
            return PawPalWalkLeashGestureType.JumpUp;
        }

        Vector3 planarDelta = dragDelta;
        planarDelta.y = 0f;
        if (planarDelta.magnitude < settings.MinimumPlanarDelta)
        {
            return PawPalWalkLeashGestureType.None;
        }

        routeDirection.y = 0f;
        if (routeDirection.sqrMagnitude <= 0.001f)
        {
            return PawPalWalkLeashGestureType.None;
        }

        float routeDot = Vector3.Dot(planarDelta.normalized, routeDirection.normalized);
        if (routeDot >= settings.ForwardDotThreshold)
        {
            return PawPalWalkLeashGestureType.RunForward;
        }

        if (routeDot <= -settings.BackwardDotThreshold)
        {
            return PawPalWalkLeashGestureType.StopBackward;
        }

        return PawPalWalkLeashGestureType.None;
    }
}
