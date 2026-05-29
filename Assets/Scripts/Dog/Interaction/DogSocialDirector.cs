using System.Collections;
using System.Collections.Generic;
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
    CompanionRest
}

[DisallowMultipleComponent]
public class DogSocialDirector : MonoBehaviour
{
    private const string DavidTestSceneName = "David_Test";
    private const string HomeThemeAssetPath = "Assets/Audio/pawfriends_home.mp3";
    private const string LabradorBarkAssetPath = "Assets/Audio/bark_light.mp3";
    private const string CorgiBarkAssetPath = "Assets/Audio/bark_dark.mp3";
    private const string AmbientCarAssetPath = "Assets/Audio/ambient_car.mp3";
    private const float CozySocialMeetDistance = 0.55f;
    private const float MinimumSocialInteractionDelay = 120f;
    private const float MaximumSocialInteractionDelay = 180f;
    private const float MinimumPairCooldownSeconds = 150f;

    private static readonly List<DogRoomAgent> RegisteredAgents = new List<DogRoomAgent>();
    private static DogSocialDirector runtimeDirector;

    [SerializeField] private DogRoomAgent dogA;
    [SerializeField] private DogRoomAgent dogB;
    [SerializeField] private float minDelayBetweenInteractions = 5f;
    [SerializeField] private float maxDelayBetweenInteractions = 10f;
    [SerializeField] private Vector2 initialInteractionDelayRange = new Vector2(60f, 140f);
    [SerializeField] private float meetDistance = CozySocialMeetDistance;
    [SerializeField] private float minimumDogSpacing = CozySocialMeetDistance;
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

    [Header("Camera Focus")]
    [SerializeField] private bool focusCameraDuringSocialInteractions = true;
    [SerializeField] private Vector3 socialCameraFocusOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip labradorBarkClip;
    [SerializeField] private AudioClip corgiBarkClip;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField] private AudioClip ambientCarClip;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float ambientCarVolume = 0.35f;
    [SerializeField] private float ambientCarIntervalSeconds = 60f;
    [SerializeField, Range(0f, 1f)] private float barkVolume = 1f;
    [SerializeField] private float barkSpacing = 0.18f;
    [SerializeField, Range(1, 5)] private int barkAudioRepeatCount = 3;
    [SerializeField] private float barkAudioRepeatGap = 0.04f;

    private Coroutine socialRoutine;
    private Coroutine backgroundMusicRoutine;
    private Coroutine ambientCarRoutine;
    private bool attemptedRuntimeNavMeshBuild;
    private NavMeshData runtimeNavMeshData;
    private NavMeshDataInstance runtimeNavMeshInstance;
    private AudioSource backgroundMusicSource;
    private AudioSource ambientCarSource;
    private DogCycleCamera resolvedDogCamera;
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

    public static DogSocialDirector Instance => runtimeDirector;

    public static void Register(DogRoomAgent agent)
    {
        if (agent == null || RegisteredAgents.Contains(agent))
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
    }

    private void OnEnable()
    {
        AutoAssignDavidTestAudio();
        EnsureActiveAudioListener();
        RefreshAgents();
        EnsureBackgroundMusicPlayback();
        EnsureAmbientCarPlayback();
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

        if (runtimeNavMeshInstance.valid)
        {
            runtimeNavMeshInstance.Remove();
        }

        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (ambientCarSource != null)
        {
            ambientCarSource.Stop();
        }
    }

    private void RefreshAgents()
    {
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

            DogSocialInteractionType interactionType = SelectInteractionType();
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

                float weight = 1f;
                if (UsesRecentDogs(firstCandidate, secondCandidate))
                {
                    weight *= 0.35f;
                }

                if (MatchesPreferredPair(firstCandidate, secondCandidate))
                {
                    weight *= 1.15f;
                }

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
        float minDelay = Mathf.Max(0f, initialInteractionDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, initialInteractionDelayRange.y);
        return Random.Range(minDelay, maxDelay);
    }

    private float GetNextInteractionDelay()
    {
        float minDelay = Mathf.Max(MinimumSocialInteractionDelay, minDelayBetweenInteractions);
        float maxDelay = Mathf.Max(Mathf.Max(MaximumSocialInteractionDelay, maxDelayBetweenInteractions), minDelay);
        return Random.Range(minDelay, maxDelay);
    }

    private float GetEffectivePairCooldownSeconds()
    {
        return Mathf.Max(MinimumPairCooldownSeconds, pairCooldownSeconds);
    }

    private DogSocialInteractionType SelectInteractionType()
    {
        List<DogSocialInteractionType> blockedInteractions = new List<DogSocialInteractionType>(recentInteractionTypes);
        DogSocialInteractionType interactionType;
        if (TrySelectWeightedInteraction(blockedInteractions, out interactionType))
        {
            return interactionType;
        }

        blockedInteractions.Clear();
        if (TrySelectWeightedInteraction(blockedInteractions, out interactionType))
        {
            return interactionType;
        }

        return DogSocialInteractionType.PlayfulGreeting;
    }

    private bool TrySelectWeightedInteraction(List<DogSocialInteractionType> blockedInteractions, out DogSocialInteractionType interactionType)
    {
        interactionType = DogSocialInteractionType.PlayfulGreeting;
        DogSocialInteractionType[] interactionTypes =
        {
            DogSocialInteractionType.PlayfulGreeting,
            DogSocialInteractionType.BouncePast,
            DogSocialInteractionType.ChaseInvite,
            DogSocialInteractionType.CompanionRest
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

            weights[i] = GetInteractionWeight(interactionTypes[i]);
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

    private float GetInteractionWeight(DogSocialInteractionType interactionType)
    {
        switch (interactionType)
        {
            case DogSocialInteractionType.BouncePast:
                return Mathf.Max(0f, bouncePastWeight);
            case DogSocialInteractionType.ChaseInvite:
                return Mathf.Max(0f, chaseInviteWeight);
            case DogSocialInteractionType.CompanionRest:
                return Mathf.Max(0f, companionRestWeight);
            default:
                return Mathf.Max(0f, playfulGreetingWeight);
        }
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

        bool cameraFocusStarted = BeginSocialCameraFocus(firstDog, secondDog);
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
                default:
                    yield return RunPlayfulGreetingInteraction(firstDog, secondDog);
                    break;
            }
        }
        finally
        {
            EndSocialHeadLook(firstDog, secondDog);
            if (cameraFocusStarted && resolvedDogCamera != null)
            {
                resolvedDogCamera.EndInteractionFocus();
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
        yield return FaceDogs(firstDog, secondDog);
        yield return PlayGreetingBarks(firstDog, secondDog);

        DogRoomAgent leadingDog = Random.value < 0.5f ? firstDog : secondDog;
        DogRoomAgent followingDog = leadingDog == firstDog ? secondDog : firstDog;
        if (TryGetPlayfulPairPoints(leadingDog, followingDog, out Vector3 leadingPoint, out Vector3 followingPoint))
        {
            yield return MoveDogsToPoints(leadingDog, leadingPoint, DogMovementPace.Run, followingDog, followingPoint, DogMovementPace.Trot);
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
        yield return FaceDogs(firstDog, secondDog);

        DogRoomAgent invitingDog = Random.value < 0.5f ? firstDog : secondDog;
        DogRoomAgent respondingDog = invitingDog == firstDog ? secondDog : firstDog;
        Coroutine barkRoutine = StartCoroutine(PlayBarkAndWag(invitingDog));
        yield return barkRoutine;

        if (TryGetPlayfulFollowPoints(invitingDog, respondingDog, out Vector3 respondingPoint, out Vector3 invitingPoint))
        {
            Coroutine moveResponder = StartCoroutine(respondingDog.MoveNear(respondingPoint, approachTimeout, DogMovementPace.Run));
            yield return moveResponder;

            Coroutine moveInviter = StartCoroutine(invitingDog.MoveNear(invitingPoint, approachTimeout, DogMovementPace.Trot));
            yield return moveInviter;
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
        Coroutine moveFirst = StartCoroutine(firstDog.MoveNear(firstPoint, approachTimeout, firstPace));
        Coroutine moveSecond = StartCoroutine(secondDog.MoveNear(secondPoint, approachTimeout, secondPace));
        yield return moveFirst;
        yield return moveSecond;
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
        return Mathf.Clamp(requestedDistance, 0.35f, 1.25f);
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

    private bool BeginSocialCameraFocus(DogRoomAgent firstDog, DogRoomAgent secondDog)
    {
        if (!focusCameraDuringSocialInteractions)
        {
            return false;
        }

        DogCycleCamera dogCamera = ResolveDogCamera();
        if (dogCamera == null)
        {
            return false;
        }

        dogCamera.BeginPairInteractionFocus(firstDog.transform, secondDog.transform, socialCameraFocusOffset);
        return true;
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

        NavMeshHit hit;
        if (NavMesh.SamplePosition(dogA.transform.position, out hit, 1.5f, NavMesh.AllAreas))
        {
            return;
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

        if (runtimeNavMeshInstance.valid)
        {
            runtimeNavMeshInstance.Remove();
        }

        runtimeNavMeshInstance = NavMesh.AddNavMeshData(runtimeNavMeshData);
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
        AudioClip barkClip = GetBarkClipForAgent(agent);
        if (agent == null)
        {
            yield break;
        }

        float barkDuration = 0f;
        if (barkClip != null)
        {
            barkDuration = GetRepeatedBarkAudioDuration(barkClip);
            StartCoroutine(PlayRepeatedBarkAudio(agent, barkClip));
        }

        yield return StartCoroutine(agent.PlayBark(barkDuration));
    }

    public IEnumerator PlayAmbientBarkForAgent(DogRoomAgent agent)
    {
        AutoAssignDavidTestAudio();
        yield return PlayBarkForAgent(agent);
    }

    private IEnumerator PlayRepeatedBarkAudio(DogRoomAgent agent, AudioClip barkClip)
    {
        if (agent == null || barkClip == null)
        {
            yield break;
        }

        GameObject barkAudioObject = new GameObject(agent.name + "_BarkAudio");
        barkAudioObject.transform.SetParent(agent.transform, false);
        barkAudioObject.transform.localPosition = Vector3.zero;

        AudioSource barkSource = barkAudioObject.AddComponent<AudioSource>();
        barkSource.volume = barkVolume;
        barkSource.spatialBlend = 0f;
        barkSource.loop = false;
        barkSource.playOnAwake = false;

        int repeatCount = Mathf.Max(1, barkAudioRepeatCount);
        float repeatDelay = Mathf.Max(0.01f, barkClip.length + barkAudioRepeatGap);
        for (int i = 0; i < repeatCount; i++)
        {
            barkSource.PlayOneShot(barkClip, barkVolume);
            if (i < repeatCount - 1)
            {
                yield return new WaitForSeconds(repeatDelay);
            }
        }

        Destroy(barkAudioObject, barkClip.length + 0.1f);
    }

    private float GetRepeatedBarkAudioDuration(AudioClip barkClip)
    {
        if (barkClip == null)
        {
            return 0f;
        }

        int repeatCount = Mathf.Max(1, barkAudioRepeatCount);
        return barkClip.length * repeatCount + Mathf.Max(0f, barkAudioRepeatGap) * Mathf.Max(0, repeatCount - 1);
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

    private void EnsureBackgroundMusicPlayback()
    {
        AutoAssignDavidTestAudio();
        EnsureActiveAudioListener();
        if (backgroundMusicClip == null)
        {
            Debug.LogWarning("DogSocialDirector could not find background music clip for scene '" + SceneManager.GetActiveScene().name + "'.");
            return;
        }

        EnsureBackgroundMusicSource();
        if (backgroundMusicSource == null)
        {
            return;
        }

        ConfigureBackgroundMusicSource();
        backgroundMusicSource.clip = backgroundMusicClip;

        if (backgroundMusicRoutine != null)
        {
            StopCoroutine(backgroundMusicRoutine);
        }

        backgroundMusicRoutine = StartCoroutine(PlayBackgroundMusicWhenReady());
    }

    private IEnumerator PlayBackgroundMusicWhenReady()
    {
        if (backgroundMusicClip == null || backgroundMusicSource == null)
        {
            yield break;
        }

        if (backgroundMusicClip.loadState == AudioDataLoadState.Unloaded)
        {
            backgroundMusicClip.LoadAudioData();
        }

        while (backgroundMusicClip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }

        if (backgroundMusicClip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogWarning("DogSocialDirector could not load background music clip '" + backgroundMusicClip.name + "'.");
            backgroundMusicRoutine = null;
            yield break;
        }

        ConfigureBackgroundMusicSource();
        backgroundMusicSource.clip = backgroundMusicClip;

        if (!backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Play();
        }

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
        backgroundMusicSource.volume = Mathf.Clamp01(backgroundMusicVolume);
    }

    private void EnsureAmbientCarPlayback()
    {
        AutoAssignDavidTestAudio();
        if (ambientCarClip == null)
        {
            return;
        }

        EnsureAmbientCarSource();
        if (ambientCarSource == null)
        {
            return;
        }

        ConfigureAmbientCarSource();
        if (ambientCarRoutine != null)
        {
            StopCoroutine(ambientCarRoutine);
        }

        ambientCarRoutine = StartCoroutine(PlayAmbientCarRoutine());
    }

    private IEnumerator PlayAmbientCarRoutine()
    {
        while (isActiveAndEnabled && ambientCarClip != null && ambientCarSource != null)
        {
            yield return new WaitForSeconds(Mathf.Max(1f, ambientCarIntervalSeconds));
            ConfigureAmbientCarSource();
            ambientCarSource.PlayOneShot(ambientCarClip, Mathf.Clamp01(ambientCarVolume));
        }

        ambientCarRoutine = null;
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
        ambientCarSource.volume = Mathf.Clamp01(ambientCarVolume);
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

        if (labradorBarkClip == null)
        {
            labradorBarkClip = LoadEditorAudioClip(LabradorBarkAssetPath);
        }

        if (corgiBarkClip == null)
        {
            corgiBarkClip = LoadEditorAudioClip(CorgiBarkAssetPath);
        }

        if (ambientCarClip == null)
        {
            ambientCarClip = LoadEditorAudioClip(AmbientCarAssetPath);
        }
    }

    private bool IsDavidTestScene()
    {
        return SceneManager.GetActiveScene().name == DavidTestSceneName;
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

    private static AudioClip LoadEditorAudioClip(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#else
        return null;
#endif
    }
}
