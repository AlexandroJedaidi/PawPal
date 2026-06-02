using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

internal enum DogSocialInteractionType
{
    PlayfulGreeting,
    BouncePast,
    ChaseInvite,
    CompanionRest,
    GroupChase
}

[DisallowMultipleComponent]
public class DogSocialDirector : MonoBehaviour
{
    private enum BackgroundAudioMode
    {
        DayTheme,
        NightAmbience
    }

    private const string DavidTestSceneName = "David_Test";
    private const string IntroPetSelectionSceneName = "IntroPetSelection";
    private const string HomeThemeAssetPath = "Assets/Audio/pawfriends_home.mp3";
    private const string NightAmbienceEditorAssetPath = "Assets/Resources/Audio/night-ambience.mp3";
    private const string NightAmbienceResourcePath = "Audio/night-ambience";
    private const string LabradorBarkAssetPath = "Assets/Audio/bark_light.mp3";
    private const string CorgiBarkAssetPath = "Assets/Audio/bark_dark.mp3";
    private const string AmbientCarAssetPath = "Assets/Audio/ambient_car.mp3";
    private const string AmbientBirdsAssetPath = "Assets/Audio/birds_chirping.mp3";
    private const string NightBackgroundLayerSourceName = "NightBackgroundLayerAudio";
    private const string NightBackgroundLayerSourceSecondaryName = "NightBackgroundLayerAudio_Secondary";
    private const float AmbientCarVolumeMultiplier = 1.5f;
    private const float DefaultSunriseHour = 7f;
    private const float DefaultSunsetHour = 20f;
    private const float DefaultTransitionHours = 1f;
    private const float CozySocialMeetDistance = 0.46f;
    private const float MaximumProximityInteractionStartDistance = 1.35f;
    private const float SocialApproachReachedDistance = 0.1f;
    private const float MinimumSocialInteractionDelay = 120f;
    private const float MaximumSocialInteractionDelay = 180f;
    private const float MinimumPairCooldownSeconds = 150f;

    private static readonly List<DogRoomAgent> RegisteredAgents = new List<DogRoomAgent>();
    private static DogSocialDirector runtimeDirector;
    private static DogRoomAgent preferredInteractionDog;

    [SerializeField] private DogRoomAgent dogA;
    [SerializeField] private DogRoomAgent dogB;
    [SerializeField] private float minDelayBetweenInteractions = 5f;
    [SerializeField] private float maxDelayBetweenInteractions = 10f;
    [SerializeField] private Vector2 initialInteractionDelayRange = new Vector2(60f, 140f);
    [SerializeField] private float meetDistance = CozySocialMeetDistance;
    [SerializeField] private float minimumDogSpacing = CozySocialMeetDistance;
    [SerializeField] private float maxProximityInteractionStartDistance = MaximumProximityInteractionStartDistance;
    [SerializeField] private float approachTimeout = 5f;
    [SerializeField] private float faceDuration = 0.75f;
    [SerializeField] private float preAnimationHeadLookSettleDuration = 0.25f;
    [SerializeField] private float pairCooldownSeconds = 7f;
    [SerializeField, Range(0, 4)] private int interactionRepeatBlockCount = 1;
    [SerializeField] private float playfulOffsetRadius = 0.9f;
    [SerializeField] private float socialRestDuration = 3.75f;
    [SerializeField] private float playfulGreetingWeight = 1f;
    [SerializeField] private float bouncePastWeight = 0.95f;
    [SerializeField] private float chaseInviteWeight = 1.1f;
    [SerializeField] private float companionRestWeight = 0.35f;
    [SerializeField] private bool buildRuntimeNavMeshIfMissing = true;
    [SerializeField] private Vector3 runtimeNavMeshCenter = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 runtimeNavMeshSize = new Vector3(6.4f, 0.12f, 5.6f);
    [SerializeField] private bool carveGroundedObstacleFootprintsInRuntimeNavMesh = true;
    [SerializeField] private float runtimeNavMeshObstaclePadding = 0.04f;
    [SerializeField] private float runtimeNavMeshGroundedObstacleClearance = 0.18f;
    [SerializeField] private float runtimeNavMeshMinimumObstacleHeight = 0.2f;

    [Header("Camera Focus")]
    [SerializeField] private bool focusCameraDuringSocialInteractions = true;
    [SerializeField] private Vector3 socialCameraFocusOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip labradorBarkClip;
    [SerializeField] private AudioClip corgiBarkClip;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip nightBackgroundClip;
    [SerializeField] private AudioClip ambientCarClip;
    [SerializeField] private AudioClip ambientBirdsClip;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float nightBackgroundVolume = 0.3f;
    [SerializeField] private float backgroundMusicSwapFadeDuration = 1.1f;
    [SerializeField, Range(0f, 1f)] private float ambientCarVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float ambientBirdsVolume = 0.14f;
    [SerializeField] private float ambientCarIntervalSeconds = 60f;
    [SerializeField] private float ambientBirdsCooldownSeconds = 15f;
    [SerializeField, Range(0f, 1f)] private float barkVolume = 1f;
    [SerializeField] private float barkSpacing = 0.18f;

    private Coroutine socialRoutine;
    private Coroutine backgroundMusicRoutine;
    private Coroutine ambientCarRoutine;
    private Coroutine ambientBirdsRoutine;
    private bool attemptedRuntimeNavMeshBuild;
    private NavMeshData runtimeNavMeshData;
    private NavMeshDataInstance runtimeNavMeshInstance;
    private AudioSource backgroundMusicSource;
    private AudioSource nightBackgroundLayerSource;
    private AudioSource nightBackgroundLayerSecondarySource;
    private AudioSource ambientCarSource;
    private AudioSource ambientBirdsSource;
    private bool ambientBirdsSuppressed;
    private BackgroundAudioMode activeBackgroundAudioMode;
    private int lastBackgroundMinuteStamp = int.MinValue;
    private DogCycleCamera resolvedDogCamera;
    private LivingRoomGraphicsEnhancer livingRoomGraphicsEnhancer;
    private readonly Queue<DogSocialInteractionType> recentInteractionTypes = new Queue<DogSocialInteractionType>();
    private readonly Dictionary<string, float> pairCooldownUntilByKey = new Dictionary<string, float>();
    private DogRoomAgent lastInteractingDogA;
    private DogRoomAgent lastInteractingDogB;

    private struct SocialPairCandidate
    {
        public DogRoomAgent First;
        public DogRoomAgent Second;
        public float Weight;
    }

    private struct GroupChasePlan
    {
        public DogRoomAgent Leader;
        public List<DogRoomAgent> Followers;
        public List<Vector3> FollowerPoints;
        public Vector3 LeaderPoint;
    }

    public static DogSocialDirector Instance => runtimeDirector;

    public static void SetPreferredInteractionDog(DogRoomAgent agent)
    {
        preferredInteractionDog = agent;
    }

    public static void Register(DogRoomAgent agent)
    {
        if (agent == null || RegisteredAgents.Contains(agent) || !IsSupportedScene())
        {
            return;
        }

        RegisteredAgents.Add(agent);
        EnsureRuntimeDirector();
        runtimeDirector.RefreshAgents();
    }

    public static void Unregister(DogRoomAgent agent)
    {
        if (agent == null)
        {
            return;
        }

        RegisteredAgents.Remove(agent);

        if (runtimeDirector != null)
        {
            runtimeDirector.RefreshAgents();
        }
    }

    private static void EnsureRuntimeDirector()
    {
        if (!IsSupportedScene())
        {
            return;
        }

        if (runtimeDirector != null)
        {
            return;
        }

        DogSocialDirector existingDirector = FindFirstObjectByType<DogSocialDirector>();
        if (existingDirector != null)
        {
            runtimeDirector = existingDirector;
            return;
        }

        GameObject directorObject = new GameObject("DogSocialDirector");
        runtimeDirector = directorObject.AddComponent<DogSocialDirector>();
    }

    private void Awake()
    {
        if (runtimeDirector != null && runtimeDirector != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeDirector = this;
        AutoAssignDavidTestAudio();
        EnsureActiveAudioListener();
        EnsureBackgroundMusicSource();
        EnsureAmbientCarSource();
        EnsureAmbientBirdsSource();
    }

    private void OnEnable()
    {
        PawPalAudioSettings.MusicVolumeChanged += HandleAudioSettingsChanged;
        PawPalAudioSettings.SoundEffectsVolumeChanged += HandleAudioSettingsChanged;
        if (!IsSupportedScene())
        {
            return;
        }

        AutoAssignDavidTestAudio();
        EnsureActiveAudioListener();
        RefreshAgents();
        if (IsDavidTestScene())
        {
            EnsureBackgroundMusicPlayback();
            EnsureAmbientCarPlayback();
        }
    }

    private void Update()
    {
        if (!isActiveAndEnabled || !IsDavidTestScene())
        {
            return;
        }

        UpdateBackgroundMusicForTimeOfDay(false);
    }

    private void OnDisable()
    {
        if (socialRoutine != null)
        {
            StopCoroutine(socialRoutine);
            socialRoutine = null;
        }

        if (backgroundMusicRoutine != null)
        {
            StopCoroutine(backgroundMusicRoutine);
            backgroundMusicRoutine = null;
        }

        if (ambientCarRoutine != null)
        {
            StopCoroutine(ambientCarRoutine);
            ambientCarRoutine = null;
        }

        if (ambientBirdsRoutine != null)
        {
            StopCoroutine(ambientBirdsRoutine);
            ambientBirdsRoutine = null;
        }

        if (runtimeNavMeshInstance.valid)
        {
            runtimeNavMeshInstance.Remove();
        }

        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (nightBackgroundLayerSource != null)
        {
            nightBackgroundLayerSource.Stop();
        }

        if (nightBackgroundLayerSecondarySource != null)
        {
            nightBackgroundLayerSecondarySource.Stop();
        }

        if (ambientCarSource != null)
        {
            ambientCarSource.Stop();
        }

        if (ambientBirdsSource != null)
        {
            ambientBirdsSource.Stop();
        }

        PawPalAudioSettings.MusicVolumeChanged -= HandleAudioSettingsChanged;
        PawPalAudioSettings.SoundEffectsVolumeChanged -= HandleAudioSettingsChanged;
    }

    private void RefreshAgents()
    {
        if (!IsSupportedScene())
        {
            return;
        }

        if (dogA == null || !RegisteredAgents.Contains(dogA))
        {
            dogA = FindAgentByName("Labrador");
        }

        if (dogB == null || !RegisteredAgents.Contains(dogB) || dogB == dogA)
        {
            dogB = FindAgentByName("Corgi");
        }

        if ((dogA == null || dogB == null || dogA == dogB) && RegisteredAgents.Count >= 2)
        {
            dogA = RegisteredAgents[0];
            dogB = RegisteredAgents[1];
        }

        if (isActiveAndEnabled && socialRoutine == null && RegisteredAgents.Count >= 2)
        {
            socialRoutine = StartCoroutine(SocialRoutine());
        }
    }

    private DogRoomAgent FindAgentByName(string namePart)
    {
        for (int i = 0; i < RegisteredAgents.Count; i++)
        {
            DogRoomAgent agent = RegisteredAgents[i];
            if (agent != null && agent.name.Contains(namePart))
            {
                return agent;
            }
        }

        return null;
    }

    private IEnumerator SocialRoutine()
    {
        EnsureNavMeshAvailable();
        yield return new WaitForSeconds(GetInitialInteractionDelay());

        while (isActiveAndEnabled)
        {
            DogRoomAgent firstDog = null;
            DogRoomAgent secondDog = null;
            while (isActiveAndEnabled && !TrySelectInteractionPair(out firstDog, out secondDog))
            {
                yield return null;
            }

            if (!isActiveAndEnabled)
            {
                break;
            }

            DogSocialInteractionType interactionType = SelectInteractionType(firstDog, secondDog);
            yield return RunInteraction(interactionType, firstDog, secondDog);
            RememberInteraction(firstDog, secondDog, interactionType);

            firstDog.StartRoaming();
            secondDog.StartRoaming();
            yield return new WaitForSeconds(GetNextInteractionDelay());
        }

        socialRoutine = null;
    }

    private bool TrySelectInteractionPair(out DogRoomAgent firstDog, out DogRoomAgent secondDog)
    {
        firstDog = null;
        secondDog = null;

        List<DogRoomAgent> availableDogs = GetAvailableDogs();
        if (availableDogs.Count < 2)
        {
            return false;
        }

        List<SocialPairCandidate> candidates = new List<SocialPairCandidate>();
        for (int firstIndex = 0; firstIndex < availableDogs.Count - 1; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < availableDogs.Count; secondIndex++)
            {
                DogRoomAgent firstCandidate = availableDogs[firstIndex];
                DogRoomAgent secondCandidate = availableDogs[secondIndex];
                if (IsPairOnCooldown(firstCandidate, secondCandidate))
                {
                    continue;
                }

                float distance = Vector3.Distance(firstCandidate.transform.position, secondCandidate.transform.position);
                if (distance > GetEffectiveProximityInteractionStartDistance())
                {
                    continue;
                }

                float weight = 1f;
                if (UsesRecentDogs(firstCandidate, secondCandidate))
                {
                    weight *= 0.35f;
                }

                if (MatchesPreferredPair(firstCandidate, secondCandidate))
                {
                    weight *= 1.15f;
                }

                if (preferredInteractionDog != null)
                {
                    if (firstCandidate == preferredInteractionDog || secondCandidate == preferredInteractionDog)
                    {
                        weight *= 3.25f;
                    }
                    else if (IsIntroSelectionScene())
                    {
                        weight *= 0.55f;
                    }
                }

                weight *= GetSocialPairWeight(firstCandidate) * GetSocialPairWeight(secondCandidate);
                candidates.Add(new SocialPairCandidate
                {
                    First = firstCandidate,
                    Second = secondCandidate,
                    Weight = weight
                });
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        SocialPairCandidate selectedCandidate = SelectWeightedPairCandidate(candidates);
        firstDog = selectedCandidate.First;
        secondDog = selectedCandidate.Second;
        return firstDog != null && secondDog != null && firstDog != secondDog;
    }

    private float GetEffectiveProximityInteractionStartDistance()
    {
        float requestedDistance = maxProximityInteractionStartDistance > 0f
            ? maxProximityInteractionStartDistance
            : MaximumProximityInteractionStartDistance;
        return Mathf.Clamp(requestedDistance, GetEffectiveMeetDistance(), 2.5f);
    }

    private List<DogRoomAgent> GetAvailableDogs()
    {
        List<DogRoomAgent> availableDogs = new List<DogRoomAgent>();
        for (int i = 0; i < RegisteredAgents.Count; i++)
        {
            DogRoomAgent candidate = RegisteredAgents[i];
            if (candidate == null || !candidate.CanJoinSocialInteraction)
            {
                continue;
            }

            availableDogs.Add(candidate);
        }

        return availableDogs;
    }

    private SocialPairCandidate SelectWeightedPairCandidate(List<SocialPairCandidate> candidates)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0.01f, candidates[i].Weight);
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= Mathf.Max(0.01f, candidates[i].Weight);
            if (roll <= 0f)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    private bool UsesRecentDogs(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        return firstDog == lastInteractingDogA
            || firstDog == lastInteractingDogB
            || secondDog == lastInteractingDogA
            || secondDog == lastInteractingDogB;
    }

    private bool MatchesPreferredPair(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (dogA == null || dogB == null)
        {
            return false;
        }

        return (firstDog == dogA && secondDog == dogB)
            || (firstDog == dogB && secondDog == dogA);
    }

    private bool IsPairOnCooldown(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        float cooldownUntil;
        return pairCooldownUntilByKey.TryGetValue(GetPairKey(firstDog, secondDog), out cooldownUntil)
            && Time.time < cooldownUntil;
    }

    private void RememberInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog, DogSocialInteractionType interactionType)
    {
        lastInteractingDogA = firstDog;
        lastInteractingDogB = secondDog;
        pairCooldownUntilByKey[GetPairKey(firstDog, secondDog)] = Time.time + GetEffectivePairCooldownSeconds();

        if (interactionRepeatBlockCount <= 0)
        {
            return;
        }

        recentInteractionTypes.Enqueue(interactionType);
        while (recentInteractionTypes.Count > interactionRepeatBlockCount)
        {
            recentInteractionTypes.Dequeue();
        }
    }

    private float GetInitialInteractionDelay()
    {
        if (IsIntroSelectionScene())
        {
            return Random.Range(8f, 16f);
        }

        float minDelay = Mathf.Max(0f, initialInteractionDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, initialInteractionDelayRange.y);
        return Random.Range(minDelay, maxDelay);
    }

    private float GetNextInteractionDelay()
    {
        if (IsIntroSelectionScene())
        {
            return Random.Range(22f, 38f);
        }

        float minDelay = Mathf.Max(MinimumSocialInteractionDelay, minDelayBetweenInteractions);
        float maxDelay = Mathf.Max(Mathf.Max(MaximumSocialInteractionDelay, maxDelayBetweenInteractions), minDelay);
        return Random.Range(minDelay, maxDelay);
    }

    private float GetEffectivePairCooldownSeconds()
    {
        if (IsIntroSelectionScene())
        {
            return 18f;
        }

        return Mathf.Max(MinimumPairCooldownSeconds, pairCooldownSeconds);
    }

    private DogSocialInteractionType SelectInteractionType(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        List<DogSocialInteractionType> blockedInteractions = new List<DogSocialInteractionType>(recentInteractionTypes);
        DogSocialInteractionType interactionType;
        if (TrySelectWeightedInteraction(blockedInteractions, firstDog, secondDog, out interactionType))
        {
            return interactionType;
        }

        blockedInteractions.Clear();
        if (TrySelectWeightedInteraction(blockedInteractions, firstDog, secondDog, out interactionType))
        {
            return interactionType;
        }

        return DogSocialInteractionType.PlayfulGreeting;
    }

    private bool TrySelectWeightedInteraction(List<DogSocialInteractionType> blockedInteractions, DogRoomAgent firstDog, DogRoomAgent secondDog, out DogSocialInteractionType interactionType)
    {
        interactionType = DogSocialInteractionType.PlayfulGreeting;
        DogSocialInteractionType[] interactionTypes =
        {
            DogSocialInteractionType.PlayfulGreeting,
            DogSocialInteractionType.BouncePast,
            DogSocialInteractionType.ChaseInvite,
            DogSocialInteractionType.CompanionRest,
            DogSocialInteractionType.GroupChase
        };

        float totalWeight = 0f;
        float[] weights = new float[interactionTypes.Length];
        for (int i = 0; i < interactionTypes.Length; i++)
        {
            if (blockedInteractions.Contains(interactionTypes[i]))
            {
                weights[i] = 0f;
                continue;
            }

            weights[i] = GetInteractionWeight(interactionTypes[i], firstDog, secondDog);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = Random.value * totalWeight;
        for (int i = 0; i < interactionTypes.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                interactionType = interactionTypes[i];
                return true;
            }
        }

        interactionType = interactionTypes[interactionTypes.Length - 1];
        return true;
    }

    private float GetInteractionWeight(DogSocialInteractionType interactionType, DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        float baseWeight;
        switch (interactionType)
        {
            case DogSocialInteractionType.BouncePast:
                baseWeight = Mathf.Max(0f, bouncePastWeight);
                break;
            case DogSocialInteractionType.ChaseInvite:
                baseWeight = Mathf.Max(0f, chaseInviteWeight);
                break;
            case DogSocialInteractionType.CompanionRest:
                baseWeight = Mathf.Max(0f, companionRestWeight);
                break;
            case DogSocialInteractionType.GroupChase:
                if (!IsIntroSelectionScene() || !CanBuildGroupChase(firstDog, secondDog))
                {
                    return 0f;
                }

                baseWeight = 2.6f;
                break;
            default:
                baseWeight = Mathf.Max(0f, playfulGreetingWeight);
                break;
        }

        return baseWeight
            * GetInteractionPersonalityWeight(firstDog, interactionType)
            * GetInteractionPersonalityWeight(secondDog, interactionType);
    }

    private static float GetSocialPairWeight(DogRoomAgent agent)
    {
        return PawPalDogPersonalityProfiles.GetSocialPairWeightMultiplier(GetAgentPersonality(agent));
    }

    private static float GetInteractionPersonalityWeight(DogRoomAgent agent, DogSocialInteractionType interactionType)
    {
        PawPalDogPersonality personality = GetAgentPersonality(agent);
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return interactionType == DogSocialInteractionType.CompanionRest ? 1.35f : interactionType == DogSocialInteractionType.ChaseInvite ? 0.72f : 0.92f;
            case PawPalDogPersonality.Gentle:
                return interactionType == DogSocialInteractionType.CompanionRest ? 1.25f : interactionType == DogSocialInteractionType.BouncePast || interactionType == DogSocialInteractionType.ChaseInvite ? 0.78f : 1f;
            case PawPalDogPersonality.Energetic:
                return interactionType == DogSocialInteractionType.BouncePast || interactionType == DogSocialInteractionType.ChaseInvite ? 1.28f : interactionType == DogSocialInteractionType.CompanionRest ? 0.72f : 1.08f;
            case PawPalDogPersonality.Loyal:
                return interactionType == DogSocialInteractionType.ChaseInvite ? 0.82f : 0.96f;
            case PawPalDogPersonality.Social:
                return interactionType == DogSocialInteractionType.PlayfulGreeting ? 1.35f : interactionType == DogSocialInteractionType.CompanionRest ? 0.88f : 1.08f;
            case PawPalDogPersonality.Playful:
                return interactionType == DogSocialInteractionType.PlayfulGreeting || interactionType == DogSocialInteractionType.ChaseInvite || interactionType == DogSocialInteractionType.BouncePast ? 1.25f : 0.82f;
            case PawPalDogPersonality.Mischievous:
                return interactionType == DogSocialInteractionType.BouncePast || interactionType == DogSocialInteractionType.ChaseInvite ? 1.18f : 0.9f;
            default:
                return 1f;
        }
    }

    private static PawPalDogPersonality GetAgentPersonality(DogRoomAgent agent)
    {
        if (agent == null)
        {
            return PawPalDogPersonality.Relaxed;
        }

        return PawPalDogPersonalityProfiles.GetPersonalityForDogId(agent.DogId, agent.name);
    }

    private IEnumerator RunInteraction(DogSocialInteractionType interactionType, DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (firstDog == null || secondDog == null || firstDog == secondDog)
        {
            yield break;
        }

        EnsureNavMeshAvailable();
        firstDog.PauseForSocial();
        secondDog.PauseForSocial();

        int cameraFocusId = BeginSocialCameraFocus(firstDog, secondDog);
        try
        {
            switch (interactionType)
            {
                case DogSocialInteractionType.BouncePast:
                    yield return RunBouncePastInteraction(firstDog, secondDog);
                    break;
                case DogSocialInteractionType.ChaseInvite:
                    yield return RunChaseInviteInteraction(firstDog, secondDog);
                    break;
                case DogSocialInteractionType.CompanionRest:
                    yield return RunCompanionRestInteraction(firstDog, secondDog);
                    break;
                case DogSocialInteractionType.GroupChase:
                    yield return RunGroupChaseInteraction(firstDog, secondDog);
                    break;
                default:
                    yield return RunPlayfulGreetingInteraction(firstDog, secondDog);
                    break;
            }
        }
        finally
        {
            EndSocialHeadLook(firstDog, secondDog);
            if (cameraFocusId != 0 && resolvedDogCamera != null)
            {
                resolvedDogCamera.EndFocus(cameraFocusId);
            }
        }
    }

    private IEnumerator RunPlayfulGreetingInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!TryGetMeetingPoints(firstDog, secondDog, out Vector3 pointA, out Vector3 pointB))
        {
            yield break;
        }

        yield return MoveDogsToPoints(firstDog, pointA, secondDog, pointB);
        if (!WereSocialMovesSuccessful(firstDog, secondDog))
        {
            yield break;
        }

        yield return FaceDogs(firstDog, secondDog);
        yield return PlayGreetingBarks(firstDog, secondDog);
        yield return WagDogs(firstDog, secondDog);
    }

    private IEnumerator RunBouncePastInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!TryGetMeetingPoints(firstDog, secondDog, out Vector3 pointA, out Vector3 pointB))
        {
            yield break;
        }

        yield return MoveDogsToPoints(firstDog, pointA, secondDog, pointB);
        if (!WereSocialMovesSuccessful(firstDog, secondDog))
        {
            yield break;
        }

        yield return FaceDogs(firstDog, secondDog);
        yield return PlayGreetingBarks(firstDog, secondDog);

        DogRoomAgent leadingDog = Random.value < 0.5f ? firstDog : secondDog;
        DogRoomAgent followingDog = leadingDog == firstDog ? secondDog : firstDog;
        if (TryGetPlayfulPairPoints(leadingDog, followingDog, out Vector3 leadingPoint, out Vector3 followingPoint))
        {
            yield return MoveDogsToPoints(leadingDog, leadingPoint, DogMovementPace.Run, followingDog, followingPoint, DogMovementPace.Trot);
            if (!WereSocialMovesSuccessful(leadingDog, followingDog))
            {
                yield break;
            }

            yield return FaceDogs(leadingDog, followingDog);
        }

        yield return WagDogs(firstDog, secondDog);
    }

    private IEnumerator RunChaseInviteInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!TryGetMeetingPoints(firstDog, secondDog, out Vector3 pointA, out Vector3 pointB))
        {
            yield break;
        }

        yield return MoveDogsToPoints(firstDog, pointA, secondDog, pointB);
        if (!WereSocialMovesSuccessful(firstDog, secondDog))
        {
            yield break;
        }

        yield return FaceDogs(firstDog, secondDog);

        DogRoomAgent invitingDog = Random.value < 0.5f ? firstDog : secondDog;
        DogRoomAgent respondingDog = invitingDog == firstDog ? secondDog : firstDog;
        Coroutine barkRoutine = StartCoroutine(PlayBarkAndWag(invitingDog));
        yield return barkRoutine;

        if (TryGetPlayfulFollowPoints(invitingDog, respondingDog, out Vector3 respondingPoint, out Vector3 invitingPoint))
        {
            Coroutine moveResponder = StartCoroutine(respondingDog.MoveNearSocial(respondingPoint, approachTimeout, DogMovementPace.Run, SocialApproachReachedDistance));
            yield return moveResponder;
            if (!WasSocialMoveSuccessful(respondingDog))
            {
                yield break;
            }

            Coroutine moveInviter = StartCoroutine(invitingDog.MoveNearSocial(invitingPoint, approachTimeout, DogMovementPace.Trot, SocialApproachReachedDistance));
            yield return moveInviter;
            if (!WasSocialMoveSuccessful(invitingDog))
            {
                yield break;
            }
        }

        yield return FaceDogs(invitingDog, respondingDog);
        yield return PlayGreetingBarks(invitingDog, respondingDog);
        yield return WagDogs(invitingDog, respondingDog);
    }

    private IEnumerator RunCompanionRestInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!TryGetMeetingPoints(firstDog, secondDog, out Vector3 pointA, out Vector3 pointB))
        {
            yield break;
        }

        yield return MoveDogsToPoints(firstDog, pointA, secondDog, pointB);
        if (!WereSocialMovesSuccessful(firstDog, secondDog))
        {
            yield break;
        }

        yield return FaceDogs(firstDog, secondDog);
        yield return PlayGreetingBarks(firstDog, secondDog);

        bool bothRest = Random.value < 0.6f;
        Coroutine firstRest = StartCoroutine(firstDog.PlayChillRest(socialRestDuration));
        Coroutine secondRoutine = bothRest
            ? StartCoroutine(secondDog.PlayChillRest(socialRestDuration))
            : StartCoroutine(secondDog.PlaySit());

        yield return firstRest;
        yield return secondRoutine;
    }

    private IEnumerator RunGroupChaseInteraction(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!TryBuildGroupChasePlan(firstDog, secondDog, out GroupChasePlan plan))
        {
            yield return RunChaseInviteInteraction(firstDog, secondDog);
            yield break;
        }

        for (int i = 1; i < plan.Followers.Count; i++)
        {
            plan.Followers[i].PauseForSocial();
        }

        try
        {
            yield return FaceDogs(plan.Leader, plan.Followers[0]);
            yield return PlayGreetingBarks(plan.Leader, plan.Followers[0]);

            Coroutine leaderMove = StartCoroutine(plan.Leader.MoveNearSocial(plan.LeaderPoint, approachTimeout, DogMovementPace.Run, SocialApproachReachedDistance));
            List<Coroutine> followerMoves = new List<Coroutine>(plan.Followers.Count);
            for (int i = 0; i < plan.Followers.Count; i++)
            {
                DogMovementPace pace = i == 0 ? DogMovementPace.Run : DogMovementPace.Trot;
                followerMoves.Add(StartCoroutine(plan.Followers[i].MoveNearSocial(plan.FollowerPoints[i], approachTimeout, pace, SocialApproachReachedDistance)));
            }

            yield return leaderMove;
            for (int i = 0; i < followerMoves.Count; i++)
            {
                yield return followerMoves[i];
            }

            if (!WasSocialMoveSuccessful(plan.Leader))
            {
                yield break;
            }

            for (int i = 0; i < plan.Followers.Count; i++)
            {
                if (!WasSocialMoveSuccessful(plan.Followers[i]))
                {
                    yield break;
                }
            }

            yield return FaceDogs(plan.Leader, plan.Followers[0]);
            yield return WagDogs(plan.Leader, plan.Followers[0]);
        }
        finally
        {
            for (int i = 1; i < plan.Followers.Count; i++)
            {
                if (plan.Followers[i] != null)
                {
                    plan.Followers[i].StartRoaming();
                }
            }
        }
    }

    private IEnumerator MoveDogsToPoints(DogRoomAgent firstDog, Vector3 firstPoint, DogRoomAgent secondDog, Vector3 secondPoint)
    {
        yield return MoveDogsToPoints(firstDog, firstPoint, DogMovementPace.Walk, secondDog, secondPoint, DogMovementPace.Walk);
    }

    private IEnumerator MoveDogsToPoints(
        DogRoomAgent firstDog,
        Vector3 firstPoint,
        DogMovementPace firstPace,
        DogRoomAgent secondDog,
        Vector3 secondPoint,
        DogMovementPace secondPace)
    {
        Coroutine moveFirst = StartCoroutine(firstDog.MoveNearSocial(firstPoint, approachTimeout, firstPace, SocialApproachReachedDistance));
        Coroutine moveSecond = StartCoroutine(secondDog.MoveNearSocial(secondPoint, approachTimeout, secondPace, SocialApproachReachedDistance));
        yield return moveFirst;
        yield return moveSecond;
    }

    private static bool WasSocialMoveSuccessful(DogRoomAgent dog)
    {
        return dog != null && dog.WasLastTravelSuccessful;
    }

    private static bool WereSocialMovesSuccessful(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        return WasSocialMoveSuccessful(firstDog) && WasSocialMoveSuccessful(secondDog);
    }

    private IEnumerator FaceDogs(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        BeginSocialHeadLook(firstDog, secondDog);
        try
        {
            Coroutine faceFirst = StartCoroutine(firstDog.FaceTarget(secondDog.transform, faceDuration));
            Coroutine faceSecond = StartCoroutine(secondDog.FaceTarget(firstDog.transform, faceDuration));
            yield return faceFirst;
            yield return faceSecond;

            float settleDuration = Mathf.Max(0f, preAnimationHeadLookSettleDuration);
            if (settleDuration > 0f)
            {
                yield return new WaitForSeconds(settleDuration);
            }
        }
        finally
        {
            EndSocialHeadLook(firstDog, secondDog);
        }
    }

    private IEnumerator WagDogs(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        Coroutine wagFirst = StartCoroutine(firstDog.PlayTailWag());
        Coroutine wagSecond = StartCoroutine(secondDog.PlayTailWag());
        yield return wagFirst;
        yield return wagSecond;
    }

    private void BeginSocialHeadLook(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        DogCameraAttention firstAttention = GetDogAttention(firstDog);
        DogCameraAttention secondAttention = GetDogAttention(secondDog);
        Transform firstHead = firstAttention != null ? firstAttention.GetOwnHeadLookTarget() : FindDogHead(firstDog);
        Transform secondHead = secondAttention != null ? secondAttention.GetOwnHeadLookTarget() : FindDogHead(secondDog);

        if (firstAttention != null)
        {
            firstAttention.BeginSocialDogLook(secondDog.transform, secondHead);
        }

        if (secondAttention != null)
        {
            secondAttention.BeginSocialDogLook(firstDog.transform, firstHead);
        }
    }

    private void EndSocialHeadLook(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        DogCameraAttention firstAttention = GetDogAttention(firstDog);
        if (firstAttention != null)
        {
            firstAttention.EndSocialDogLook(secondDog.transform);
        }

        DogCameraAttention secondAttention = GetDogAttention(secondDog);
        if (secondAttention != null)
        {
            secondAttention.EndSocialDogLook(firstDog.transform);
        }
    }

    private static DogCameraAttention GetDogAttention(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return null;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            return attention;
        }

        return dog.gameObject.AddComponent<DogCameraAttention>();
    }

    private static Transform FindDogHead(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return null;
        }

        Transform[] children = dog.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null && string.Equals(candidate.name, "head", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("head") && !lowerName.Contains("aim") && !lowerName.Contains("target") && !lowerName.Contains("helper"))
            {
                return candidate;
            }
        }

        return dog.transform;
    }

    private IEnumerator PlayBarkAndWag(DogRoomAgent dog)
    {
        if (dog == null)
        {
            yield break;
        }

        yield return PlayBarkForAgent(dog);
        yield return StartCoroutine(dog.PlayTailWag());
    }

    private bool TryGetMeetingPoints(DogRoomAgent firstDog, DogRoomAgent secondDog, out Vector3 pointA, out Vector3 pointB)
    {
        Vector3 midpoint = Vector3.Lerp(firstDog.transform.position, secondDog.transform.position, 0.5f);
        Vector3 between = secondDog.transform.position - firstDog.transform.position;
        between.y = 0f;

        if (between.sqrMagnitude < 0.01f)
        {
            between = firstDog.transform.forward;
        }

        between.Normalize();
        float effectiveMeetDistance = GetEffectiveMeetDistance();
        pointA = midpoint - between * (effectiveMeetDistance * 0.5f);
        pointB = midpoint + between * (effectiveMeetDistance * 0.5f);

        bool foundA = firstDog.TryGetRoomSafePoint(pointA, 1.25f, out pointA);
        bool foundB = secondDog.TryGetRoomSafePoint(pointB, 1.25f, out pointB);

        if (!foundA || !foundB)
        {
            Debug.LogWarning("DogSocialDirector could not find valid NavMesh meeting points for the dogs.");
            return false;
        }

        TryRebalanceMeetingPoints(firstDog, secondDog, effectiveMeetDistance, ref pointA, ref pointB);
        return true;
    }

    private float GetEffectiveMeetDistance()
    {
        float requestedDistance = meetDistance > 0f ? meetDistance : CozySocialMeetDistance;
        if (minimumDogSpacing > 0f)
        {
            requestedDistance = Mathf.Min(requestedDistance, minimumDogSpacing);
        }

        requestedDistance = Mathf.Min(requestedDistance, CozySocialMeetDistance);
        return Mathf.Clamp(requestedDistance, 0.22f, 1.25f);
    }

    private void TryRebalanceMeetingPoints(DogRoomAgent firstDog, DogRoomAgent secondDog, float targetDistance, ref Vector3 pointA, ref Vector3 pointB)
    {
        Vector3 midpoint = Vector3.Lerp(pointA, pointB, 0.5f);
        Vector3 between = pointB - pointA;
        between.y = 0f;
        if (between.sqrMagnitude < 0.001f)
        {
            between = secondDog.transform.position - firstDog.transform.position;
            between.y = 0f;
        }

        if (between.sqrMagnitude < 0.001f)
        {
            between = firstDog.transform.forward;
        }

        between.Normalize();
        Vector3 candidateA = midpoint - between * (targetDistance * 0.5f);
        Vector3 candidateB = midpoint + between * (targetDistance * 0.5f);

        Vector3 rebalancedA;
        Vector3 rebalancedB;
        bool foundA = firstDog.TryGetRoomSafePoint(candidateA, 0.65f, out rebalancedA);
        bool foundB = secondDog.TryGetRoomSafePoint(candidateB, 0.65f, out rebalancedB);
        if (foundA && foundB && Vector3.Distance(rebalancedA, rebalancedB) <= targetDistance + 0.25f)
        {
            pointA = rebalancedA;
            pointB = rebalancedB;
        }
    }

    private int BeginSocialCameraFocus(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!focusCameraDuringSocialInteractions || IsIntroSelectionScene())
        {
            return 0;
        }

        DogCycleCamera dogCamera = ResolveDogCamera();
        if (dogCamera == null)
        {
            return 0;
        }

        return dogCamera.BeginPairFocus(firstDog.transform, secondDog.transform, DogCameraFocusPriority.SocialInteraction, socialCameraFocusOffset);
    }

    private DogCycleCamera ResolveDogCamera()
    {
        if (resolvedDogCamera != null)
        {
            return resolvedDogCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            resolvedDogCamera = mainCamera.GetComponent<DogCycleCamera>();
            if (resolvedDogCamera != null)
            {
                return resolvedDogCamera;
            }
        }

        resolvedDogCamera = FindFirstObjectByType<DogCycleCamera>();
        return resolvedDogCamera;
    }

    private bool TryGetPlayfulPairPoints(DogRoomAgent leadingDog, DogRoomAgent followingDog, out Vector3 leadingPoint, out Vector3 followingPoint)
    {
        Vector3 direction = leadingDog.transform.position - followingDog.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            direction = new Vector3(randomDirection.x, 0f, randomDirection.y);
        }

        Vector3 lateral = Vector3.Cross(Vector3.up, direction.normalized);
        if (Random.value < 0.5f)
        {
            lateral = -lateral;
        }

        Vector3 leadCandidate = leadingDog.transform.position + (direction.normalized * playfulOffsetRadius) + (lateral * (playfulOffsetRadius * 0.35f));
        Vector3 followCandidate = Vector3.Lerp(leadingDog.transform.position, leadCandidate, 0.55f);

        bool foundLeading = leadingDog.TryGetRoomSafePoint(leadCandidate, 1.25f, out leadingPoint);
        bool foundFollowing = followingDog.TryGetRoomSafePoint(followCandidate, 1.25f, out followingPoint);
        return foundLeading && foundFollowing;
    }

    private bool TryGetPlayfulFollowPoints(DogRoomAgent invitingDog, DogRoomAgent respondingDog, out Vector3 respondingPoint, out Vector3 invitingPoint)
    {
        Vector3 direction = respondingDog.transform.position - invitingDog.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = invitingDog.transform.forward;
        }

        Vector3 fleeCandidate = respondingDog.transform.position + direction.normalized * playfulOffsetRadius;
        bool foundResponding = respondingDog.TryGetRoomSafePoint(fleeCandidate, 1.25f, out respondingPoint);
        Vector3 followCandidate = Vector3.Lerp(invitingDog.transform.position, respondingPoint, 0.7f);
        bool foundInviting = invitingDog.TryGetRoomSafePoint(followCandidate, 1.25f, out invitingPoint);
        return foundResponding && foundInviting;
    }

    private bool CanBuildGroupChase(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!IsIntroSelectionScene())
        {
            return false;
        }

        int nearbyCount = 0;
        List<DogRoomAgent> availableDogs = GetAvailableDogs();
        for (int i = 0; i < availableDogs.Count; i++)
        {
            DogRoomAgent candidate = availableDogs[i];
            if (candidate == null || candidate == firstDog || candidate == secondDog)
            {
                continue;
            }

            float distance = Mathf.Min(
                Vector3.Distance(candidate.transform.position, firstDog.transform.position),
                Vector3.Distance(candidate.transform.position, secondDog.transform.position));
            if (distance <= GetEffectiveProximityInteractionStartDistance() * 1.3f)
            {
                nearbyCount++;
            }
        }

        return nearbyCount > 0;
    }

    private bool TryBuildGroupChasePlan(DogRoomAgent firstDog, DogRoomAgent secondDog, out GroupChasePlan plan)
    {
        plan = default(GroupChasePlan);
        if (!IsIntroSelectionScene() || firstDog == null || secondDog == null)
        {
            return false;
        }

        DogRoomAgent leader = preferredInteractionDog != null && (preferredInteractionDog == firstDog || preferredInteractionDog == secondDog)
            ? preferredInteractionDog
            : (Random.value < 0.5f ? firstDog : secondDog);
        DogRoomAgent firstFollower = leader == firstDog ? secondDog : firstDog;

        List<DogRoomAgent> availableDogs = GetAvailableDogs();
        List<DogRoomAgent> followers = new List<DogRoomAgent> { firstFollower };
        for (int i = 0; i < availableDogs.Count; i++)
        {
            DogRoomAgent candidate = availableDogs[i];
            if (candidate == null || candidate == leader || candidate == firstFollower)
            {
                continue;
            }

            if (Vector3.Distance(candidate.transform.position, leader.transform.position) > GetEffectiveProximityInteractionStartDistance() * 1.45f)
            {
                continue;
            }

            followers.Add(candidate);
            if (followers.Count >= 3)
            {
                break;
            }
        }

        if (followers.Count == 0)
        {
            return false;
        }

        Vector3 direction = leader.transform.position - firstFollower.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            direction = new Vector3(randomDirection.x, 0f, randomDirection.y);
        }

        direction.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, direction).normalized;
        if (Random.value < 0.5f)
        {
            lateral = -lateral;
        }

        Vector3 leaderCandidate = leader.transform.position + direction * (playfulOffsetRadius * 1.55f) + lateral * (playfulOffsetRadius * 0.4f);
        if (!leader.TryGetRoomSafePoint(leaderCandidate, 1.5f, out Vector3 leaderPoint))
        {
            return false;
        }

        List<Vector3> followerPoints = new List<Vector3>(followers.Count);
        for (int i = 0; i < followers.Count; i++)
        {
            float trailingDistance = 0.42f + i * 0.34f;
            Vector3 followerCandidate = leaderPoint - direction * trailingDistance + lateral * ((i % 2 == 0 ? -1f : 1f) * 0.14f);
            if (!followers[i].TryGetRoomSafePoint(followerCandidate, 1.1f, out Vector3 followerPoint))
            {
                return false;
            }

            followerPoints.Add(followerPoint);
        }

        plan = new GroupChasePlan
        {
            Leader = leader,
            Followers = followers,
            FollowerPoints = followerPoints,
            LeaderPoint = leaderPoint
        };
        return true;
    }

    private string GetPairKey(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        int firstId = firstDog.GetInstanceID();
        int secondId = secondDog.GetInstanceID();
        if (firstId > secondId)
        {
            int swap = firstId;
            firstId = secondId;
            secondId = swap;
        }

        return firstId + ":" + secondId;
    }

    private void EnsureNavMeshAvailable()
    {
        if (!buildRuntimeNavMeshIfMissing || attemptedRuntimeNavMeshBuild || dogA == null)
        {
            return;
        }

        if (!ShouldPreferRuntimeHomeNavMesh())
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(dogA.transform.position, out hit, 1.5f, NavMesh.AllAreas))
            {
                return;
            }
        }

        attemptedRuntimeNavMeshBuild = true;

        BuildProceduralRuntimeNavMesh();
    }

    private void BuildProceduralRuntimeNavMesh()
    {
        NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
        buildSettings.agentRadius = 0.26f;
        buildSettings.agentHeight = 0.6f;
        buildSettings.agentClimb = 0.2f;
        buildSettings.agentSlope = 45f;
        buildSettings.minRegionArea = 0.1f;

        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        Vector3 navMeshCenter = runtimeNavMeshCenter;
        Vector3 navMeshSize = runtimeNavMeshSize;
        Bounds dogRoomBounds;
        if (dogA != null && dogA.TryGetRoomBounds(out dogRoomBounds))
        {
            navMeshCenter = dogRoomBounds.center;
            navMeshSize = new Vector3(dogRoomBounds.size.x, runtimeNavMeshSize.y, dogRoomBounds.size.z);
        }

        NavMeshBuildSource floorSource = new NavMeshBuildSource();
        floorSource.shape = NavMeshBuildSourceShape.Box;
        floorSource.transform = Matrix4x4.TRS(navMeshCenter, Quaternion.identity, Vector3.one);
        floorSource.size = navMeshSize;
        floorSource.area = 0;
        sources.Add(floorSource);

        float runtimeFloorY = dogA != null && dogA.TryGetRoomBounds(out dogRoomBounds)
            ? dogRoomBounds.min.y
            : navMeshCenter.y;
        if (carveGroundedObstacleFootprintsInRuntimeNavMesh)
        {
            AddGroundedObstacleFootprintSources(sources, runtimeFloorY, new Bounds(navMeshCenter, navMeshSize));
        }

        Bounds bounds = new Bounds(
            navMeshCenter + Vector3.up,
            new Vector3(navMeshSize.x + 1f, 3f, navMeshSize.z + 1f));

        runtimeNavMeshData = NavMeshBuilder.BuildNavMeshData(
            buildSettings,
            sources,
            bounds,
            Vector3.zero,
            Quaternion.identity);

        if (runtimeNavMeshData == null)
        {
            Debug.LogWarning("DogSocialDirector could not build its procedural runtime NavMesh fallback.");
            return;
        }

        runtimeNavMeshData.name = "Runtime Dog Procedural NavMesh";

        if (ShouldPreferRuntimeHomeNavMesh())
        {
            // The earlier working home-scene behavior came from this carved runtime navmesh,
            // so remove stale baked surface data before registering the runtime replacement.
            RemoveBakedHomeNavMeshSurfaceData();
        }

        if (runtimeNavMeshInstance.valid)
        {
            runtimeNavMeshInstance.Remove();
        }

        runtimeNavMeshInstance = NavMesh.AddNavMeshData(runtimeNavMeshData);
    }

    private bool ShouldPreferRuntimeHomeNavMesh()
    {
        return gameObject.scene.IsValid()
            && gameObject.scene.name == DavidTestSceneName
            && carveGroundedObstacleFootprintsInRuntimeNavMesh;
    }

    private void RemoveBakedHomeNavMeshSurfaceData()
    {
        NavMeshSurface surface = GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            return;
        }

        surface.RemoveData();
    }

    private void AddGroundedObstacleFootprintSources(List<NavMeshBuildSource> sources, float floorY, Bounds navMeshBounds)
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!ShouldUseRuntimeNavMeshObstacle(collider, floorY, navMeshBounds))
            {
                continue;
            }

            Bounds obstacleBounds = collider.bounds;
            float obstacleHeight = Mathf.Max(
                Mathf.Max(0f, obstacleBounds.max.y - floorY),
                Mathf.Max(0.05f, runtimeNavMeshMinimumObstacleHeight));

            NavMeshBuildSource obstacleSource = new NavMeshBuildSource();
            obstacleSource.shape = NavMeshBuildSourceShape.Box;
            obstacleSource.transform = Matrix4x4.TRS(
                new Vector3(obstacleBounds.center.x, floorY + obstacleHeight * 0.5f, obstacleBounds.center.z),
                Quaternion.identity,
                Vector3.one);
            obstacleSource.size = new Vector3(
                Mathf.Max(0.05f, obstacleBounds.size.x + runtimeNavMeshObstaclePadding * 2f),
                obstacleHeight,
                Mathf.Max(0.05f, obstacleBounds.size.z + runtimeNavMeshObstaclePadding * 2f));
            obstacleSource.area = 1;
            sources.Add(obstacleSource);
        }
    }

    private bool ShouldUseRuntimeNavMeshObstacle(Collider collider, float floorY, Bounds navMeshBounds)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
        {
            return false;
        }

        if (!collider.bounds.Intersects(navMeshBounds))
        {
            return false;
        }

        if (collider.bounds.min.y > floorY + Mathf.Max(0f, runtimeNavMeshGroundedObstacleClearance))
        {
            return false;
        }

        if (collider.bounds.size.y < Mathf.Max(0.01f, runtimeNavMeshMinimumObstacleHeight))
        {
            return false;
        }

        if (collider.bounds.size.x < 0.05f || collider.bounds.size.z < 0.05f)
        {
            return false;
        }

        if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
        {
            return false;
        }

        if (collider.GetComponentInParent<DogRoomAgent>() != null
            || collider.GetComponentInParent<PawPalToyRuntimeMetadata>() != null)
        {
            return false;
        }

        return true;
    }

    private IEnumerator PlayGreetingBarks(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        AutoAssignDavidTestAudio();

        bool playedFirst = false;
        if (firstDog != null)
        {
            yield return PlayBarkForAgent(firstDog);
            playedFirst = true;
        }

        if (playedFirst && barkSpacing > 0f)
        {
            yield return new WaitForSeconds(barkSpacing);
        }

        if (secondDog != null)
        {
            yield return PlayBarkForAgent(secondDog);
        }
    }

    private IEnumerator PlayBarkForAgent(DogRoomAgent agent)
    {
        if (agent == null || agent.HasHeldToy)
        {
            yield break;
        }

        if (IsIntroSelectionScene())
        {
            yield return StartCoroutine(agent.PlayIntroPreviewVocal(0f));
            yield break;
        }

        yield return StartCoroutine(agent.PlayBark(0f));
    }

    public IEnumerator PlayAmbientBarkForAgent(DogRoomAgent agent)
    {
        AutoAssignDavidTestAudio();
        yield return PlayBarkForAgent(agent);
    }

    private void PlayBarkAudio(DogRoomAgent agent, AudioClip barkClip)
    {
        if (agent == null || barkClip == null)
        {
            return;
        }

        float effectiveSourceVolume = PawPalAudioSettings.ApplySoundEffectsVolume(barkVolume);
        if (effectiveSourceVolume <= 0f)
        {
            return;
        }

        GameObject barkAudioObject = new GameObject(agent.name + "_BarkAudio");
        barkAudioObject.transform.SetParent(agent.transform, false);
        barkAudioObject.transform.localPosition = Vector3.zero;

        AudioSource barkSource = barkAudioObject.AddComponent<AudioSource>();
        barkSource.volume = effectiveSourceVolume;
        barkSource.spatialBlend = 0f;
        barkSource.loop = false;
        barkSource.playOnAwake = false;

        if (!agent.HasHeldToy)
        {
            barkSource.PlayOneShot(barkClip, barkVolume);
        }

        Destroy(barkAudioObject, barkClip.length + 0.1f);
    }

    private float GetBarkAudioDuration(AudioClip barkClip)
    {
        if (barkClip == null)
        {
            return 0f;
        }

        return barkClip.length;
    }

    private AudioClip GetBarkClipForAgent(DogRoomAgent agent)
    {
        if (agent == null)
        {
            return null;
        }

        string lowerName = agent.name.ToLowerInvariant();
        if (lowerName.Contains("labrador"))
        {
            return labradorBarkClip;
        }

        if (lowerName.Contains("corgi"))
        {
            return corgiBarkClip;
        }

        return null;
    }

    private void EnsureBackgroundMusicSource()
    {
        if (backgroundMusicSource != null)
        {
            return;
        }

        backgroundMusicSource = GetComponent<AudioSource>();
        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureBackgroundMusicSource();
    }

    private void EnsureAmbientCarSource()
    {
        if (ambientCarSource != null)
        {
            return;
        }

        GameObject audioObject = new GameObject("AmbientCarAudio");
        audioObject.transform.SetParent(transform, false);
        ambientCarSource = audioObject.AddComponent<AudioSource>();
        ConfigureAmbientCarSource();
    }

    private void EnsureNightBackgroundLayerSource()
    {
        if (nightBackgroundLayerSource != null)
        {
            return;
        }

        Transform existingChild = transform.Find(NightBackgroundLayerSourceName);
        if (existingChild != null)
        {
            nightBackgroundLayerSource = existingChild.GetComponent<AudioSource>();
        }

        if (nightBackgroundLayerSource == null)
        {
            GameObject audioObject = new GameObject(NightBackgroundLayerSourceName);
            audioObject.transform.SetParent(transform, false);
            nightBackgroundLayerSource = audioObject.AddComponent<AudioSource>();
        }

        Transform existingSecondaryChild = transform.Find(NightBackgroundLayerSourceSecondaryName);
        if (existingSecondaryChild != null)
        {
            nightBackgroundLayerSecondarySource = existingSecondaryChild.GetComponent<AudioSource>();
        }

        if (nightBackgroundLayerSecondarySource == null)
        {
            GameObject audioObject = new GameObject(NightBackgroundLayerSourceSecondaryName);
            audioObject.transform.SetParent(transform, false);
            nightBackgroundLayerSecondarySource = audioObject.AddComponent<AudioSource>();
        }

        ConfigureNightBackgroundLayerSource(0f);
    }

    private void EnsureAmbientBirdsSource()
    {
        if (ambientBirdsSource != null)
        {
            return;
        }

        GameObject audioObject = new GameObject("AmbientBirdsAudio");
        audioObject.transform.SetParent(transform, false);
        ambientBirdsSource = audioObject.AddComponent<AudioSource>();
        ConfigureAmbientBirdsSource();
    }

    private void EnsureBackgroundMusicPlayback()
    {
        if (!IsDavidTestScene())
        {
            return;
        }

        AutoAssignDavidTestAudio();
        EnsureActiveAudioListener();
        AudioClip initialClip = ResolveTargetBackgroundClip(GetDesiredBackgroundAudioMode());
        if (initialClip == null)
        {
            Debug.LogWarning("DogSocialDirector could not find background music clip for scene '" + SceneManager.GetActiveScene().name + "'.");
            return;
        }

        EnsureBackgroundMusicSource();
        if (backgroundMusicSource == null)
        {
            return;
        }

        if (backgroundMusicRoutine != null)
        {
            StopCoroutine(backgroundMusicRoutine);
        }

        UpdateBackgroundMusicForTimeOfDay(true);
    }

    private IEnumerator PlayBackgroundMusicWhenReady(AudioClip targetClip, float targetVolume, bool immediate)
    {
        if (targetClip == null || backgroundMusicSource == null)
        {
            backgroundMusicRoutine = null;
            yield break;
        }

        if (targetClip.loadState == AudioDataLoadState.Unloaded)
        {
            targetClip.LoadAudioData();
        }

        while (targetClip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }

        if (targetClip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogWarning("DogSocialDirector could not load background music clip '" + targetClip.name + "'.");
            backgroundMusicRoutine = null;
            yield break;
        }

        float currentVolume = backgroundMusicSource.volume;
        ConfigureBackgroundMusicSource();
        bool clipChanged = backgroundMusicSource.clip != targetClip;
        bool shouldFade = !immediate
            && backgroundMusicSource.isPlaying
            && backgroundMusicSource.clip != null
            && clipChanged;

        if (shouldFade)
        {
            backgroundMusicSource.volume = currentVolume;
            float fadeDuration = Mathf.Max(0.01f, backgroundMusicSwapFadeDuration);
            yield return FadeBackgroundMusic(backgroundMusicSource.volume, 0f, fadeDuration);
            backgroundMusicSource.Stop();
            StopNightBackgroundLayerSource();
        }
        else if (clipChanged && backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Stop();
            StopNightBackgroundLayerSource();
        }

        backgroundMusicSource.clip = targetClip;
        backgroundMusicSource.volume = shouldFade && !immediate ? 0f : targetVolume;
        if (clipChanged || !backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Play();
        }

        if (shouldFade && !immediate)
        {
            yield return FadeBackgroundMusic(0f, targetVolume, Mathf.Max(0.01f, backgroundMusicSwapFadeDuration));
        }
        else
        {
            backgroundMusicSource.volume = targetVolume;
        }

        SyncNightBackgroundLayerSource(targetClip, targetVolume);

        backgroundMusicRoutine = null;
    }

    private void ConfigureBackgroundMusicSource()
    {
        if (backgroundMusicSource == null)
        {
            return;
        }

        backgroundMusicSource.playOnAwake = false;
        backgroundMusicSource.loop = true;
        backgroundMusicSource.spatialBlend = 0f;
        backgroundMusicSource.volume = GetActiveBackgroundMusicTargetVolume();
    }

    private void ConfigureNightBackgroundLayerSource(float volume)
    {
        if (nightBackgroundLayerSource == null)
        {
            return;
        }

        nightBackgroundLayerSource.playOnAwake = false;
        nightBackgroundLayerSource.loop = true;
        nightBackgroundLayerSource.spatialBlend = 0f;
        nightBackgroundLayerSource.volume = volume;

        if (nightBackgroundLayerSecondarySource != null)
        {
            nightBackgroundLayerSecondarySource.playOnAwake = false;
            nightBackgroundLayerSecondarySource.loop = true;
            nightBackgroundLayerSecondarySource.spatialBlend = 0f;
            nightBackgroundLayerSecondarySource.volume = volume;
        }
    }

    private void EnsureAmbientCarPlayback()
    {
        AutoAssignDavidTestAudio();
        if (ambientCarClip == null && ambientBirdsClip == null)
        {
            return;
        }

        if (ambientCarClip != null)
        {
            EnsureAmbientCarSource();
        }

        if (ambientBirdsClip != null)
        {
            EnsureAmbientBirdsSource();
        }

        ApplyAmbientTimeOfDayAudioState();
        EnsureAmbientBirdsPlayback();
        if (ambientCarRoutine != null)
        {
            StopCoroutine(ambientCarRoutine);
        }

        ambientCarRoutine = StartCoroutine(PlayAmbientCarRoutine());
    }

    private IEnumerator PlayAmbientCarRoutine()
    {
        if (ambientCarClip == null || ambientCarSource == null)
        {
            ambientCarRoutine = null;
            yield break;
        }

        while (isActiveAndEnabled && ambientCarClip != null && ambientCarSource != null)
        {
            yield return new WaitForSeconds(Mathf.Max(1f, ambientCarIntervalSeconds));
            ConfigureAmbientCarSource();
            if (ambientCarSource.volume > 0f)
            {
                ambientBirdsSuppressed = true;
                StopAmbientBirdsPlayback();
                ambientCarSource.PlayOneShot(ambientCarClip, Mathf.Clamp01(ambientCarVolume * AmbientCarVolumeMultiplier));
                yield return new WaitForSeconds(Mathf.Max(0.1f, ambientCarClip.length));
                ambientBirdsSuppressed = false;
            }
        }

        ambientCarRoutine = null;
    }

    private void EnsureAmbientBirdsPlayback()
    {
        if (ambientBirdsClip == null)
        {
            return;
        }

        EnsureAmbientBirdsSource();
        if (ambientBirdsSource == null)
        {
            return;
        }

        ConfigureAmbientBirdsSource();
        if (ambientBirdsRoutine != null)
        {
            StopCoroutine(ambientBirdsRoutine);
        }

        ambientBirdsRoutine = StartCoroutine(PlayAmbientBirdsRoutine());
    }

    private IEnumerator PlayAmbientBirdsRoutine()
    {
        while (isActiveAndEnabled && ambientBirdsClip != null && ambientBirdsSource != null)
        {
            while (ambientBirdsSuppressed)
            {
                yield return null;
            }

            ConfigureAmbientBirdsSource();
            if (ambientBirdsSource.volume > 0f)
            {
                ambientBirdsSource.Play();
                yield return new WaitForSeconds(Mathf.Max(0.1f, ambientBirdsClip.length));
                ambientBirdsSource.Stop();
            }

            yield return new WaitForSeconds(Mathf.Max(0f, ambientBirdsCooldownSeconds));
        }

        ambientBirdsRoutine = null;
    }

    private void ConfigureAmbientCarSource()
    {
        if (ambientCarSource == null)
        {
            return;
        }

        ambientCarSource.playOnAwake = false;
        ambientCarSource.loop = false;
        ambientCarSource.spatialBlend = 0f;
        ambientCarSource.volume = ShouldPlayDaytimeAmbientSceneEffects()
            ? PawPalAudioSettings.ApplySoundEffectsVolume(ambientCarVolume)
            : 0f;
    }

    private void ConfigureAmbientBirdsSource()
    {
        if (ambientBirdsSource == null)
        {
            return;
        }

        ambientBirdsSource.playOnAwake = false;
        ambientBirdsSource.loop = false;
        ambientBirdsSource.spatialBlend = 0f;
        ambientBirdsSource.volume = ShouldPlayDaytimeAmbientSceneEffects()
            ? PawPalAudioSettings.ApplySoundEffectsVolume(ambientBirdsVolume)
            : 0f;
        ambientBirdsSource.clip = ambientBirdsClip;
    }

    private void StopAmbientBirdsPlayback()
    {
        if (ambientBirdsSource == null || !ambientBirdsSource.isPlaying)
        {
            return;
        }

        ambientBirdsSource.Stop();
    }

    private void HandleAudioSettingsChanged()
    {
        ConfigureBackgroundMusicSource();
        SyncNightBackgroundLayerSource(backgroundMusicSource != null ? backgroundMusicSource.clip : null, GetActiveBackgroundMusicTargetVolume());
        ApplyAmbientTimeOfDayAudioState();
    }

    private void AutoAssignDavidTestAudio()
    {
        if (!IsDavidTestScene())
        {
            return;
        }

        if (backgroundMusicClip == null)
        {
            backgroundMusicClip = LoadEditorAudioClip(HomeThemeAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref backgroundMusicClip, PawPalAudioResources.PawFriendsHome);

        if (nightBackgroundClip == null)
        {
            nightBackgroundClip = LoadEditorAudioClip(NightAmbienceEditorAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref nightBackgroundClip, PawPalAudioResources.NightAmbience);

        if (labradorBarkClip == null)
        {
            labradorBarkClip = LoadEditorAudioClip(LabradorBarkAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref labradorBarkClip, PawPalAudioResources.BarkLight);

        if (corgiBarkClip == null)
        {
            corgiBarkClip = LoadEditorAudioClip(CorgiBarkAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref corgiBarkClip, PawPalAudioResources.BarkDark);

        if (ambientCarClip == null)
        {
            ambientCarClip = LoadEditorAudioClip(AmbientCarAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref ambientCarClip, PawPalAudioResources.AmbientCar);

        if (ambientBirdsClip == null)
        {
            ambientBirdsClip = LoadEditorAudioClip(AmbientBirdsAssetPath);
        }

        PawPalAudioResources.AssignIfMissing(ref ambientBirdsClip, PawPalAudioResources.AmbientBirds);
    }

    private bool IsDavidTestScene()
    {
        return SceneManager.GetActiveScene().name == DavidTestSceneName;
    }

    private bool IsIntroSelectionScene()
    {
        return SceneManager.GetActiveScene().name == IntroPetSelectionSceneName;
    }

    private static bool IsSupportedScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == DavidTestSceneName || sceneName == IntroPetSelectionSceneName;
    }

    private void EnsureActiveAudioListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled)
            {
                return;
            }
        }

        AudioListener listenerToEnable = null;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            listenerToEnable = mainCamera.GetComponent<AudioListener>();
            if (listenerToEnable == null)
            {
                listenerToEnable = mainCamera.gameObject.AddComponent<AudioListener>();
            }
        }
        else if (listeners.Length > 0)
        {
            listenerToEnable = listeners[0];
        }

        if (listenerToEnable != null)
        {
            listenerToEnable.enabled = true;
            Debug.Log("DogSocialDirector enabled AudioListener on '" + listenerToEnable.gameObject.name + "'.");
        }
        else
        {
            Debug.LogWarning("DogSocialDirector could not find or create an AudioListener.");
        }
    }

    private void UpdateBackgroundMusicForTimeOfDay(bool immediate)
    {
        if (!IsDavidTestScene())
        {
            return;
        }

        AutoAssignDavidTestAudio();
        EnsureBackgroundMusicSource();
        if (backgroundMusicSource == null)
        {
            return;
        }

        PawPalTimeOfDayState timeState = ResolveBackgroundMusicTimeOfDayState();
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(timeState);
        BackgroundAudioMode targetMode = timeState.Daylight01 <= 0.001f
            ? BackgroundAudioMode.NightAmbience
            : BackgroundAudioMode.DayTheme;

        if (!immediate && minuteStamp == lastBackgroundMinuteStamp && targetMode == activeBackgroundAudioMode)
        {
            return;
        }

        lastBackgroundMinuteStamp = minuteStamp;
        AudioClip targetClip = ResolveTargetBackgroundClip(targetMode);
        if (targetClip == null)
        {
            if (targetMode == BackgroundAudioMode.DayTheme)
            {
                Debug.LogWarning("DogSocialDirector could not resolve the day background music clip.");
            }

            return;
        }

        activeBackgroundAudioMode = targetMode;
        ApplyAmbientTimeOfDayAudioState();

        if (!immediate
            && backgroundMusicSource.clip == targetClip
            && backgroundMusicSource.isPlaying)
        {
            ConfigureBackgroundMusicSource();
            SyncNightBackgroundLayerSource(targetClip, GetActiveBackgroundMusicTargetVolume());
            return;
        }

        if (backgroundMusicRoutine != null)
        {
            StopCoroutine(backgroundMusicRoutine);
        }

        backgroundMusicRoutine = StartCoroutine(PlayBackgroundMusicWhenReady(
            targetClip,
            GetActiveBackgroundMusicTargetVolume(),
            immediate));
    }

    private BackgroundAudioMode GetDesiredBackgroundAudioMode()
    {
        return ResolveBackgroundMusicTimeOfDayState().Daylight01 <= 0.001f
            ? BackgroundAudioMode.NightAmbience
            : BackgroundAudioMode.DayTheme;
    }

    private PawPalTimeOfDayState ResolveBackgroundMusicTimeOfDayState()
    {
        if (livingRoomGraphicsEnhancer == null)
        {
            livingRoomGraphicsEnhancer = FindFirstObjectByType<LivingRoomGraphicsEnhancer>();
        }

        if (livingRoomGraphicsEnhancer != null && livingRoomGraphicsEnhancer.gameObject.scene == gameObject.scene)
        {
            return livingRoomGraphicsEnhancer.EvaluateTimeOfDayState();
        }

        return PawPalTimeOfDayEvaluator.Evaluate(
            true,
            false,
            12f,
            DefaultSunriseHour,
            DefaultSunsetHour,
            DefaultTransitionHours);
    }

    private AudioClip ResolveTargetBackgroundClip(BackgroundAudioMode mode)
    {
        return mode == BackgroundAudioMode.NightAmbience ? ResolveNightBackgroundClip() : backgroundMusicClip;
    }

    private AudioClip ResolveNightBackgroundClip()
    {
        if (nightBackgroundClip == null)
        {
            nightBackgroundClip = PawPalAudioResources.LoadClip(PawPalAudioResources.NightAmbience);
        }

        return nightBackgroundClip;
    }

    private float GetActiveBackgroundMusicTargetVolume()
    {
        float configuredVolume = activeBackgroundAudioMode == BackgroundAudioMode.NightAmbience
            ? nightBackgroundVolume
            : backgroundMusicVolume;
        return PawPalAudioSettings.ApplyMusicVolume(configuredVolume);
    }

    private void SyncNightBackgroundLayerSource(AudioClip activeClip, float targetVolume)
    {
        if (activeBackgroundAudioMode != BackgroundAudioMode.NightAmbience || activeClip == null || targetVolume <= 0f)
        {
            StopNightBackgroundLayerSource();
            return;
        }

        EnsureNightBackgroundLayerSource();
        if (nightBackgroundLayerSource == null)
        {
            return;
        }

        ConfigureNightBackgroundLayerSource(targetVolume);
        bool clipChanged = nightBackgroundLayerSource.clip != activeClip;
        nightBackgroundLayerSource.clip = activeClip;
        bool secondaryClipChanged = nightBackgroundLayerSecondarySource != null && nightBackgroundLayerSecondarySource.clip != activeClip;
        if (nightBackgroundLayerSecondarySource != null)
        {
            nightBackgroundLayerSecondarySource.clip = activeClip;
        }
        if (clipChanged && backgroundMusicSource != null && backgroundMusicSource.clip == activeClip)
        {
            nightBackgroundLayerSource.time = backgroundMusicSource.time;
            if (nightBackgroundLayerSecondarySource != null)
            {
                nightBackgroundLayerSecondarySource.time = backgroundMusicSource.time;
            }
        }
        else if (secondaryClipChanged && backgroundMusicSource != null && backgroundMusicSource.clip == activeClip && nightBackgroundLayerSecondarySource != null)
        {
            nightBackgroundLayerSecondarySource.time = backgroundMusicSource.time;
        }

        if (!nightBackgroundLayerSource.isPlaying)
        {
            nightBackgroundLayerSource.Play();
        }

        if (nightBackgroundLayerSecondarySource != null && !nightBackgroundLayerSecondarySource.isPlaying)
        {
            nightBackgroundLayerSecondarySource.Play();
        }
    }

    private void StopNightBackgroundLayerSource()
    {
        if (nightBackgroundLayerSource == null)
        {
            return;
        }

        if (nightBackgroundLayerSource.isPlaying)
        {
            nightBackgroundLayerSource.Stop();
        }

        nightBackgroundLayerSource.clip = null;
        nightBackgroundLayerSource.volume = 0f;

        if (nightBackgroundLayerSecondarySource == null)
        {
            return;
        }

        if (nightBackgroundLayerSecondarySource.isPlaying)
        {
            nightBackgroundLayerSecondarySource.Stop();
        }

        nightBackgroundLayerSecondarySource.clip = null;
        nightBackgroundLayerSecondarySource.volume = 0f;
    }

    private bool ShouldPlayDaytimeAmbientSceneEffects()
    {
        return GetDesiredBackgroundAudioMode() == BackgroundAudioMode.DayTheme;
    }

    private void ApplyAmbientTimeOfDayAudioState()
    {
        bool allowDaytimeAmbient = ShouldPlayDaytimeAmbientSceneEffects();
        ambientBirdsSuppressed = !allowDaytimeAmbient;

        ConfigureAmbientCarSource();
        ConfigureAmbientBirdsSource();

        if (allowDaytimeAmbient)
        {
            return;
        }

        if (ambientCarSource != null)
        {
            ambientCarSource.Stop();
        }

        StopAmbientBirdsPlayback();
    }

    private IEnumerator FadeBackgroundMusic(float from, float to, float duration)
    {
        if (backgroundMusicSource == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            backgroundMusicSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }

        backgroundMusicSource.volume = to;
    }

    private static AudioClip LoadEditorAudioClip(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#else
        return null;
#endif
    }
}
