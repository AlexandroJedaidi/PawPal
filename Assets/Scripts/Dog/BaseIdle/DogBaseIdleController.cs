using UnityEngine;

[DisallowMultipleComponent]
public class DogBaseIdleController : MonoBehaviour
{
    public enum BaseIdleState
    {
        Initializing,
        ChoosingDestination,
        MovingToDestination,
        Pausing,
        Sitting,
        Barking
    }

    [Header("References")]
    [SerializeField] private DogBaseIdleSettings settings;
    [SerializeField] private Transform roamCenter;
    [SerializeField] private DogWanderNavigator navigator;
    [SerializeField] private DogAnimationBridge animationBridge;

    [Header("Debug")]
    [SerializeField] private BaseIdleState currentState = BaseIdleState.Initializing;
    [SerializeField] private Vector3 currentDestination;
    [SerializeField] private float currentStateEndsAt;
    [SerializeField] private float lastSitTime = float.NegativeInfinity;
    [SerializeField] private float lastBarkTime = float.NegativeInfinity;
    [SerializeField] private bool navMeshWarningShown;
    [SerializeField] private bool awaitingActionResolution;
    [SerializeField] private bool externalPauseRequested;
    [SerializeField] private bool actionStateObserved;
    [SerializeField] private bool sitExitRequested;

    private Vector3 fallbackRoamCenter;
    private float moveStateStartedAt;

    private void Reset()
    {
        navigator = GetComponent<DogWanderNavigator>();
        animationBridge = GetComponent<DogAnimationBridge>();
    }

    private void Awake()
    {
        if (navigator == null)
        {
            navigator = GetComponent<DogWanderNavigator>();
        }

        if (animationBridge == null)
        {
            animationBridge = GetComponent<DogAnimationBridge>();
        }

        fallbackRoamCenter = transform.position;
    }

    private void Start()
    {
        if (settings != null && navigator != null)
        {
            navigator.ApplySettings(settings);
        }

        if (animationBridge != null)
        {
            animationBridge.SetIdle();
        }

        currentState = BaseIdleState.ChoosingDestination;
    }

    private void Update()
    {
        if (externalPauseRequested)
        {
            HoldExternalPause();
            return;
        }

        if (!ValidateRuntimeDependencies())
        {
            return;
        }

        switch (currentState)
        {
            case BaseIdleState.ChoosingDestination:
                ChooseDestination();
                break;
            case BaseIdleState.MovingToDestination:
                UpdateMovementState();
                break;
            case BaseIdleState.Pausing:
                UpdatePauseState();
                break;
            case BaseIdleState.Sitting:
                UpdateSittingState();
                break;
            case BaseIdleState.Barking:
                UpdateBarkingState();
                break;
        }
    }

    private bool ValidateRuntimeDependencies()
    {
        if (settings == null || navigator == null || animationBridge == null)
        {
            return false;
        }

        navigator.ApplySettings(settings);

        if (!navigator.HasUsableNavMesh)
        {
            if (!navMeshWarningShown)
            {
                navMeshWarningShown = true;
                Debug.LogWarning(
                    $"DogBaseIdleController on '{name}' could not find a usable baked NavMesh. " +
                    "Bake the living room NavMesh before expecting wander movement.");
            }

            animationBridge.SetIdle();
            currentState = BaseIdleState.Initializing;
            return false;
        }

        if (currentState == BaseIdleState.Initializing)
        {
            currentState = BaseIdleState.ChoosingDestination;
        }

        return true;
    }

    private void ChooseDestination()
    {
        Vector3 center = roamCenter != null ? roamCenter.position : fallbackRoamCenter;

        bool foundDestination = navigator.TryPickRandomDestination(
            center,
            settings.WanderRadius,
            settings.MinWanderDistance,
            settings.DestinationSampleRadius,
            settings.MaxDestinationAttempts,
            settings.MaxDistanceFromRoamCenter,
            out Vector3 destination);

        if (!foundDestination)
        {
            BeginPause();
            return;
        }

        navigator.Resume();
        if (!navigator.MoveTo(destination))
        {
            BeginPause();
            return;
        }

        currentDestination = destination;
        moveStateStartedAt = Time.time;
        currentState = BaseIdleState.MovingToDestination;
        animationBridge.SetMoving(true);
    }

    private void UpdateMovementState()
    {
        if (navigator.HasReachedDestination)
        {
            BeginPause();
            return;
        }

        if (Time.time - moveStateStartedAt >= settings.ArrivalTimeout)
        {
            navigator.Stop();
            BeginPause();
        }
    }

    private void BeginPause()
    {
        navigator.Stop();
        animationBridge.SetIdle();
        awaitingActionResolution = true;
        currentState = BaseIdleState.Pausing;
        currentStateEndsAt = Time.time + Random.Range(settings.MinPauseDuration, settings.MaxPauseDuration);
    }

    public void BeginExternalPause()
    {
        externalPauseRequested = true;
        HoldExternalPause();
    }

    public void EndExternalPause()
    {
        externalPauseRequested = false;
        currentState = BaseIdleState.ChoosingDestination;
        currentStateEndsAt = 0f;
        awaitingActionResolution = false;
        navMeshWarningShown = false;
    }

    private void HoldExternalPause()
    {
        if (navigator != null)
        {
            navigator.Stop();
        }

        if (animationBridge != null)
        {
            animationBridge.SetIdle();
        }
    }

    private void UpdatePauseState()
    {
        if (Time.time < currentStateEndsAt)
        {
            return;
        }

        if (!awaitingActionResolution)
        {
            currentState = BaseIdleState.ChoosingDestination;
            return;
        }

        awaitingActionResolution = false;

        bool wantsSit = CanSitNow() && Random.value <= settings.SitChanceDuringPause;
        bool wantsBark = CanBarkNow() && Random.value <= settings.BarkChanceDuringPause;

        if (wantsSit && wantsBark)
        {
            if (Random.value < 0.5f)
            {
                wantsBark = false;
            }
            else
            {
                wantsSit = false;
            }
        }

        if (wantsSit)
        {
            lastSitTime = Time.time;
            currentState = BaseIdleState.Sitting;
            currentStateEndsAt = 0f;
            actionStateObserved = false;
            sitExitRequested = false;
            animationBridge.TriggerSit();
            return;
        }

        if (wantsBark)
        {
            lastBarkTime = Time.time;
            currentState = BaseIdleState.Barking;
            currentStateEndsAt = 0f;
            actionStateObserved = false;
            animationBridge.TriggerBark();
            return;
        }

        currentState = BaseIdleState.ChoosingDestination;
    }

    private void UpdateSittingState()
    {
        if (!actionStateObserved)
        {
            if (animationBridge.IsInSitState())
            {
                actionStateObserved = true;
                currentStateEndsAt = Time.time + settings.SitDuration;
            }

            return;
        }

        if (!sitExitRequested)
        {
            if (Time.time < currentStateEndsAt)
            {
                return;
            }

            sitExitRequested = true;
            animationBridge.EndSit();
            return;
        }

        if (animationBridge.IsInSitState() || animationBridge.IsTransitioning())
        {
            return;
        }

        actionStateObserved = false;
        sitExitRequested = false;
        currentState = BaseIdleState.ChoosingDestination;
    }

    private void UpdateBarkingState()
    {
        if (!actionStateObserved)
        {
            if (animationBridge.IsInBarkState())
            {
                actionStateObserved = true;
            }

            return;
        }

        if (animationBridge.IsInBarkState() || animationBridge.IsTransitioning())
        {
            return;
        }

        actionStateObserved = false;
        animationBridge.SetIdle();
        currentState = BaseIdleState.ChoosingDestination;
    }

    private bool CanSitNow()
    {
        return Time.time >= lastSitTime + settings.SitCooldown;
    }

    private bool CanBarkNow()
    {
        return Time.time >= lastBarkTime + settings.BarkCooldown;
    }
}
