using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalWalkLeashDirector : MonoBehaviour
{
    private enum WalkAmbientAction
    {
        Idle2,
        Idle7,
        Pissing,
        Bark
    }

    [SerializeField] private PawPalWalkLeashGestureSettings gestureSettings = new PawPalWalkLeashGestureSettings();
    [SerializeField, Min(0.1f)] private float walkTrotCycleSeconds = 4.5f;
    [SerializeField, Min(1f)] private float timedIdleIntervalSeconds = 14f;
    [SerializeField] private Vector2 ambientActionIntervalRange = new Vector2(8f, 16f);
    [SerializeField] private Vector2 ambientActionDurationRange = new Vector2(1.05f, 2.35f);
    [SerializeField] private Vector2 cameraLookIntervalRange = new Vector2(3f, 6f);
    [SerializeField] private Vector2 cameraLookDurationRange = new Vector2(1.6f, 2.6f);
    [SerializeField, Range(1f, 80f)] private float walkCameraLookMaxYaw = 40f;
    [SerializeField, Range(0f, 1f)] private float ambientVocalVolume = 0.7f;
    [SerializeField] private float walkSpeedMultiplier = 1f;
    [SerializeField] private float trotSpeedMultiplier = 1f;
    [SerializeField] private float runSpeedMultiplier = 1f;

    private PawPalWalkSceneController walkController;
    private PawPalLeashDragController leashDragController;
    private Animator animator;
    private DogRoomAgent dogAgent;
    private PawPalCatRoomAgent catAgent;
    private DogCameraAttention dogCameraAttention;
    private IntroPetDefinition petDefinition;
    private IntroPetSpecies species = IntroPetSpecies.Dog;
    private Transform walkerRoot;
    private Transform headTransform;
    private AudioSource ambientAudioSource;
    private AudioClip lightBarkClip;
    private AudioClip darkBarkClip;
    private AudioClip catMeowClip;
    private string runtimeBreed = string.Empty;
    private string objectName = string.Empty;
    private float runBurstUntil;
    private float nextReactionAllowedAt;
    private float nextTimedIdleAt;
    private float nextCameraLookAt;
    private float fallbackHeadLookUntil;
    private Coroutine reactionRoutine;
    private Coroutine timedIdleRoutine;
    private bool routeProgressBlockedByReaction;
    private DogMovementPace lastAppliedPace = (DogMovementPace)(-1);

    public bool BlocksRouteProgress => routeProgressBlockedByReaction || timedIdleRoutine != null;
    public bool IsPlayingPetOneShot => reactionRoutine != null || timedIdleRoutine != null;
    public DogMovementPace CurrentPace => ResolveCurrentPace();

    public float RouteSpeedMultiplier
    {
        get
        {
            DogMovementPace pace = ResolveCurrentPace();
            if (pace == DogMovementPace.Run)
            {
                return Mathf.Max(0.05f, runSpeedMultiplier);
            }

            return pace == DogMovementPace.Trot
                ? Mathf.Max(0.05f, trotSpeedMultiplier)
                : Mathf.Max(0.05f, walkSpeedMultiplier);
        }
    }

    public void Configure(
        PawPalWalkSceneController controller,
        PawPalLeashDragController leashDrag,
        Animator targetAnimator,
        DogRoomAgent targetDogAgent,
        PawPalCatRoomAgent targetCatAgent,
        IntroPetSpecies petSpecies,
        IntroPetDefinition definition,
        string breed,
        string petObjectName)
    {
        walkController = controller;
        leashDragController = leashDrag;
        animator = targetAnimator;
        dogAgent = targetDogAgent;
        catAgent = targetCatAgent;
        species = petSpecies;
        petDefinition = definition;
        runtimeBreed = breed ?? string.Empty;
        objectName = petObjectName ?? string.Empty;
        walkerRoot = ResolveWalkerRoot();
        headTransform = walkerRoot != null ? PawPalRoomPetRuntime.ResolveHeadTransform(walkerRoot) : null;
        if (headTransform == walkerRoot)
        {
            headTransform = null;
        }

        dogCameraAttention = targetDogAgent != null ? targetDogAgent.GetComponentInChildren<DogCameraAttention>(true) : null;
        ScheduleNextTimedIdle(true);
        nextCameraLookAt = Time.time + 0.75f;
        ApplyCurrentPace(true);
    }

    private void Update()
    {
        if (walkController == null || animator == null)
        {
            return;
        }

        ApplyCurrentPace(false);
        UpdateFallbackAnimatorAssist();
        TryHandleLeashGesture();
        TryStartTimedIdle();
        TryRequestCameraLook();
    }

    private void LateUpdate()
    {
        UpdateFallbackHeadLook();
    }

    private void TryHandleLeashGesture()
    {
        if (leashDragController == null
            || !leashDragController.IsDragging
            || !PawPalWalkLeashGestureClassifier.IsReactionAllowed(Time.time, nextReactionAllowedAt))
        {
            return;
        }

        PawPalWalkLeashGestureType gesture = PawPalWalkLeashGestureClassifier.Classify(
            leashDragController.LastDragDelta,
            leashDragController.LastDragVelocity,
            walkController.CurrentRouteDirection,
            gestureSettings);

        switch (gesture)
        {
            case PawPalWalkLeashGestureType.RunForward:
                runBurstUntil = Time.time + gestureSettings.RunBurstDuration;
                nextReactionAllowedAt = Time.time + gestureSettings.ReactionCooldown;
                ApplyCurrentPace(true);
                break;
            case PawPalWalkLeashGestureType.StopBackward:
                StartReaction(PlayStopReaction(), true);
                break;
            case PawPalWalkLeashGestureType.JumpUp:
                StartReaction(PlayJumpReaction(), false);
                break;
        }
    }

    private void TryStartTimedIdle()
    {
        if (BlocksRouteProgress || Time.time < nextTimedIdleAt)
        {
            return;
        }

        timedIdleRoutine = StartCoroutine(PlayTimedIdleRoutine());
    }

    private void TryRequestCameraLook()
    {
        if (Time.time < nextCameraLookAt || walkController.CurrentWalkCamera == null)
        {
            return;
        }

        if (dogCameraAttention != null)
        {
            dogCameraAttention.RequestWalkCameraAttention(
                walkController.CurrentWalkCamera.transform,
                Random.Range(cameraLookDurationRange.x, cameraLookDurationRange.y),
                walkCameraLookMaxYaw);
        }
        else
        {
            RequestFallbackHeadLook(Random.Range(cameraLookDurationRange.x, cameraLookDurationRange.y));
        }

        ScheduleNextCameraLook();
    }

    private void UpdateFallbackAnimatorAssist()
    {
        if (BlocksRouteProgress || reactionRoutine != null || dogAgent != null || catAgent != null)
        {
            return;
        }

        PawPalWalkPetAnimationPlayer.ForceLocomotionForPace(
            animator,
            ResolveMovementProfile(),
            species,
            ResolveCurrentPace());
    }

    private void StartReaction(IEnumerator routine, bool blockRouteProgress)
    {
        if (reactionRoutine != null)
        {
            return;
        }

        nextReactionAllowedAt = Time.time + gestureSettings.ReactionCooldown;
        routeProgressBlockedByReaction = blockRouteProgress;
        reactionRoutine = StartCoroutine(routine);
    }

    private IEnumerator PlayStopReaction()
    {
        ApplyStoppedPace();
        yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
            this,
            animator,
            petDefinition,
            runtimeBreed,
            objectName,
            gestureSettings.StopReactionDuration,
            species == IntroPetSpecies.Cat ? "CatSimple_Hit_F" : "Hit_F",
            "Hit_F",
            "Arm_Cat|Hit_F");

        routeProgressBlockedByReaction = false;
        reactionRoutine = null;
        ApplyCurrentPace(true);
    }

    private IEnumerator PlayJumpReaction()
    {
        yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
            this,
            animator,
            petDefinition,
            runtimeBreed,
            objectName,
            gestureSettings.JumpReactionDuration,
            PawPalWalkPetAnimationPlayer.GetJumpReactionStateNames(species));

        routeProgressBlockedByReaction = false;
        reactionRoutine = null;
        ApplyCurrentPace(true);
    }

    private IEnumerator PlayTimedIdleRoutine()
    {
        ApplyStoppedPace();
        WalkAmbientAction action = ChooseAmbientAction();
        float duration = ResolveAmbientActionDuration(action);
        yield return PlayAmbientAction(action, duration);

        ScheduleNextTimedIdle(false);
        timedIdleRoutine = null;
        ApplyCurrentPace(true);
    }

    private IEnumerator PlayAmbientAction(WalkAmbientAction action, float duration)
    {
        if (dogAgent != null)
        {
            switch (action)
            {
                case WalkAmbientAction.Bark:
                    yield return dogAgent.PlayWalkStop(PawPalWalkStopType.Bark, duration);
                    yield break;
                case WalkAmbientAction.Pissing:
                    yield return dogAgent.PlayWalkStop(PawPalWalkStopType.Pissing, duration);
                    yield break;
                default:
                    yield return dogAgent.PlayPhotoPose(ToPhotoPose(action), duration);
                    yield break;
            }
        }

        if (catAgent != null)
        {
            if (action == WalkAmbientAction.Bark)
            {
                yield return catAgent.PlayBark(duration);
                yield break;
            }

            yield return catAgent.PlayPhotoPose(ToPhotoPose(action), duration);
            yield break;
        }

        if (action == WalkAmbientAction.Bark)
        {
            PlayAmbientVocalAudio();
        }

        yield return PawPalWalkPetAnimationPlayer.PlayOneShot(
            this,
            animator,
            petDefinition,
            runtimeBreed,
            objectName,
            duration,
            GetAmbientStateNames(action));
    }

    private WalkAmbientAction ChooseAmbientAction()
    {
        if (species == IntroPetSpecies.Cat)
        {
            int catChoice = Random.Range(0, 3);
            if (catChoice == 0)
            {
                return WalkAmbientAction.Bark;
            }

            return catChoice == 1 ? WalkAmbientAction.Idle2 : WalkAmbientAction.Idle7;
        }

        int dogChoice = Random.Range(0, 4);
        switch (dogChoice)
        {
            case 0:
                return WalkAmbientAction.Bark;
            case 1:
                return WalkAmbientAction.Pissing;
            case 2:
                return WalkAmbientAction.Idle2;
            default:
                return WalkAmbientAction.Idle7;
        }
    }

    private float ResolveAmbientActionDuration(WalkAmbientAction action)
    {
        float min = Mathf.Max(0.25f, ambientActionDurationRange.x);
        float max = Mathf.Max(min, ambientActionDurationRange.y);
        float duration = Random.Range(min, max);
        switch (action)
        {
            case WalkAmbientAction.Bark:
                return Mathf.Clamp(duration, 0.45f, 1.25f);
            case WalkAmbientAction.Pissing:
                return Mathf.Max(duration, 1.75f);
            default:
                return duration;
        }
    }

    private void ScheduleNextTimedIdle(bool initial)
    {
        float fallback = Mathf.Max(1f, timedIdleIntervalSeconds);
        float min = Mathf.Max(1f, ambientActionIntervalRange.x);
        float max = Mathf.Max(min, ambientActionIntervalRange.y);
        float delay = Random.Range(min, max);
        if (initial)
        {
            delay = Mathf.Min(delay, fallback);
        }

        nextTimedIdleAt = Time.time + delay;
    }

    private DogMovementPace ResolveCurrentPace()
    {
        if (Time.time < runBurstUntil)
        {
            return DogMovementPace.Run;
        }

        float cycle = Mathf.Max(0.1f, walkTrotCycleSeconds);
        return Mathf.FloorToInt(Time.time / cycle) % 2 == 0
            ? DogMovementPace.Walk
            : DogMovementPace.Trot;
    }

    private void ApplyCurrentPace(bool force)
    {
        if (BlocksRouteProgress || (!force && reactionRoutine != null))
        {
            return;
        }

        DogMovementPace pace = ResolveCurrentPace();
        if (!force && lastAppliedPace == pace)
        {
            return;
        }

        lastAppliedPace = pace;
        if (dogAgent != null)
        {
            dogAgent.SetExternalWalkPace(pace);
        }

        if (catAgent != null)
        {
            catAgent.SetExternalWalkPace(pace);
        }
    }

    private void ApplyStoppedPace()
    {
        lastAppliedPace = (DogMovementPace)(-1);
        if (dogAgent != null)
        {
            dogAgent.SetExternalWalkPace(DogMovementPace.Walk);
        }

        if (catAgent != null)
        {
            catAgent.SetExternalWalkPace(DogMovementPace.Walk);
        }
    }

    private Transform ResolveWalkerRoot()
    {
        if (dogAgent != null)
        {
            return dogAgent.transform;
        }

        if (catAgent != null)
        {
            return catAgent.transform;
        }

        return animator != null ? animator.transform : transform;
    }

    private void RequestFallbackHeadLook(float duration)
    {
        if (headTransform == null || walkController == null || walkController.CurrentWalkCamera == null)
        {
            return;
        }

        fallbackHeadLookUntil = Time.time + Mathf.Max(0.1f, duration);
    }

    private void UpdateFallbackHeadLook()
    {
        if (Time.time > fallbackHeadLookUntil
            || headTransform == null
            || walkController == null
            || walkController.CurrentWalkCamera == null)
        {
            return;
        }

        Transform root = walkerRoot != null ? walkerRoot : transform;
        Vector3 rootForward = root.forward;
        rootForward.y = 0f;
        if (rootForward.sqrMagnitude < 0.001f)
        {
            rootForward = Vector3.forward;
        }

        rootForward.Normalize();
        Vector3 toCamera = walkController.CurrentWalkCamera.transform.position - headTransform.position;
        Vector3 flatToCamera = toCamera;
        flatToCamera.y = 0f;
        if (flatToCamera.sqrMagnitude < 0.001f)
        {
            return;
        }

        flatToCamera.Normalize();
        float yaw = Vector3.SignedAngle(rootForward, flatToCamera, Vector3.up);
        float clampedYaw = Mathf.Clamp(yaw, -walkCameraLookMaxYaw, walkCameraLookMaxYaw);
        Vector3 clampedDirection = Quaternion.AngleAxis(clampedYaw, Vector3.up) * rootForward;
        float verticalOffset = Mathf.Clamp(toCamera.y, -0.25f, 0.25f);
        Vector3 lookDirection = (clampedDirection + Vector3.up * verticalOffset).normalized;
        ApplyFallbackHeadLookRotation(lookDirection);
    }

    private void ApplyFallbackHeadLookRotation(Vector3 lookDirection)
    {
        if (headTransform == null || lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 aimAxis = GetBestHeadAimAxis();
        Quaternion delta = Quaternion.FromToRotation(aimAxis, lookDirection);
        Quaternion targetRotation = delta * headTransform.rotation;
        headTransform.rotation = Quaternion.Slerp(headTransform.rotation, targetRotation, Time.deltaTime * 4.5f);
    }

    private Vector3 GetBestHeadAimAxis()
    {
        Vector3 petForward = walkerRoot != null ? walkerRoot.forward : transform.forward;
        petForward.y = 0f;
        if (petForward.sqrMagnitude < 0.001f)
        {
            petForward = Vector3.forward;
        }

        petForward.Normalize();

        Vector3 bestAxis = headTransform.forward;
        float bestDot = Vector3.Dot(FlattenAxis(bestAxis), petForward);
        TestHeadAimAxis(headTransform.forward, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.forward, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(headTransform.up, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.up, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(headTransform.right, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.right, petForward, ref bestAxis, ref bestDot);
        return bestAxis;
    }

    private static void TestHeadAimAxis(Vector3 candidateAxis, Vector3 petForward, ref Vector3 bestAxis, ref float bestDot)
    {
        Vector3 flatAxis = FlattenAxis(candidateAxis);
        float dot = Vector3.Dot(flatAxis, petForward);
        if (dot > bestDot)
        {
            bestDot = dot;
            bestAxis = candidateAxis;
        }
    }

    private static Vector3 FlattenAxis(Vector3 axis)
    {
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        return axis.normalized;
    }

    private void PlayAmbientVocalAudio()
    {
        AudioClip clip = ResolveAmbientVocalClip();
        if (clip == null)
        {
            return;
        }

        EnsureAmbientAudioSource();
        if (ambientAudioSource == null)
        {
            return;
        }

        ambientAudioSource.PlayOneShot(clip, PawPalAudioSettings.ApplySoundEffectsVolume(ambientVocalVolume));
    }

    private AudioClip ResolveAmbientVocalClip()
    {
        if (species == IntroPetSpecies.Cat)
        {
            PawPalAudioResources.AssignIfMissing(ref catMeowClip, PawPalAudioResources.CatMeow);
            return catMeowClip;
        }

        PawPalAudioResources.AssignIfMissing(ref lightBarkClip, PawPalAudioResources.BarkLight);
        PawPalAudioResources.AssignIfMissing(ref darkBarkClip, PawPalAudioResources.BarkDark);
        if (lightBarkClip == null)
        {
            return darkBarkClip;
        }

        if (darkBarkClip == null)
        {
            return lightBarkClip;
        }

        return Random.value < 0.5f ? lightBarkClip : darkBarkClip;
    }

    private void EnsureAmbientAudioSource()
    {
        if (ambientAudioSource != null)
        {
            return;
        }

        ambientAudioSource = gameObject.AddComponent<AudioSource>();
        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.loop = false;
        ambientAudioSource.spatialBlend = 0f;
    }

    private static PawPalPhotoPoseId ToPhotoPose(WalkAmbientAction action)
    {
        return action == WalkAmbientAction.Idle2 ? PawPalPhotoPoseId.Idle2 : PawPalPhotoPoseId.Idle7;
    }

    private string[] GetAmbientStateNames(WalkAmbientAction action)
    {
        if (species == IntroPetSpecies.Cat)
        {
            switch (action)
            {
                case WalkAmbientAction.Bark:
                    return new[] { "CatSimple_Vocal", "Vocal", "Arm_Cat|Vocal", "CatSimple_Idle_2", "Idle_2", "Arm_Cat|Idle_2" };
                case WalkAmbientAction.Idle2:
                    return new[] { "CatSimple_Idle_2", "Idle_2", "Arm_Cat|Idle_2", "Idle2" };
                default:
                    return new[] { "CatSimple_Idle_7", "Idle_7", "Arm_Cat|Idle_7", "Idle7" };
            }
        }

        switch (action)
        {
            case WalkAmbientAction.Bark:
                return new[] { "Bark", "Arm_Labrador|Bark", "Idle_2", "Idle2" };
            case WalkAmbientAction.Pissing:
                return new[] { "Pissing", "Pee", "Peeing", "Urinate", "Idle_7", "Idle7" };
            case WalkAmbientAction.Idle2:
                return new[] { "Idle_2", "Idle2", "Arm_Labrador|Idle_2" };
            default:
                return new[] { "Idle_7", "Idle7", "Arm_Labrador|Idle_7" };
        }
    }

    private void ScheduleNextCameraLook()
    {
        float min = Mathf.Max(0.1f, cameraLookIntervalRange.x);
        float max = Mathf.Max(min, cameraLookIntervalRange.y);
        nextCameraLookAt = Time.time + Random.Range(min, max);
    }

    private PawPalPetMovementProfile ResolveMovementProfile()
    {
        if (dogAgent != null)
        {
            return dogAgent.MovementProfile;
        }

        if (catAgent != null)
        {
            return catAgent.MovementProfile;
        }

        return PawPalPetMovementProfiles.Resolve(petDefinition, runtimeBreed, objectName);
    }
}
