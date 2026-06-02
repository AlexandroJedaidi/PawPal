using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PawPalGestureSnapshot
{
    public PawPalGestureType Type;
    public PawPalDogBodyZone StartZone;
    public PawPalDogBodyZone EndZone;
    public Vector2 StartScreenPosition;
    public Vector2 EndScreenPosition;
    public float DurationSeconds;
    public PawPalTrickId CandidateTrick;
    public PawPalTrickFailureReason RejectionReason;
    public string DebugText;
}

[DisallowMultipleComponent]
public sealed class PawPalGestureRecognizer : MonoBehaviour
{
    private const float PetStrokeMinPixels = 12f;
    private const float PetStrokeMaxSeconds = 0.9f;
    private const float DogRectHorizontalPadding01 = 0.18f;
    private const float DogRectBottomPadding01 = 0.14f;
    private const float DogRectTopPadding01 = 0.28f;
    private const float MinimumDogRectHorizontalPaddingPixels = 20f;
    private const float MinimumDogRectBottomPaddingPixels = 18f;
    private const float MinimumDogRectTopPaddingPixels = 28f;

    [SerializeField] private float minSwipePixels = 58f;
    [SerializeField] private float maxTapPixels = 28f;
    [SerializeField] private float maxTapSeconds = 0.32f;
    [SerializeField] private float minHoldSeconds = 0.48f;
    [SerializeField] private float circularMinAngleDegrees = 255f;
    [SerializeField] private float circularMinRadiusPixels = 26f;
    [SerializeField, Range(0.1f, 2f)] private float leniency = 1f;
    [SerializeField, Range(1f, 3f)] private float bodyZonePaddingMultiplier = 1f;

    private readonly List<Vector2> gesturePoints = new List<Vector2>();

    private Transform targetRoot;
    private Camera targetCamera;
    private bool recognizerActive;
    private bool tracking;
    private Vector2 startPosition;
    private float startTime;
    private PawPalDogBodyZone startZone;

    public event Action<PawPalGestureSnapshot> GestureRecognized;

    public PawPalGestureSnapshot LastGesture { get; private set; }
    public PawPalTrickFailureReason LastRejectionReason { get; private set; }

    public void Configure(DogRoomAgent dog, Camera camera, float gestureLeniency)
    {
        Configure(dog != null ? dog.transform : null, camera, gestureLeniency, 1f);
    }

    public void Configure(Transform petRoot, Camera camera, float gestureLeniency)
    {
        Configure(petRoot, camera, gestureLeniency, 1f);
    }

    public void Configure(Transform petRoot, Camera camera, float gestureLeniency, float paddingMultiplier)
    {
        targetRoot = petRoot;
        targetCamera = camera != null ? camera : Camera.main;
        leniency = Mathf.Clamp(gestureLeniency, 0.1f, 2f);
        bodyZonePaddingMultiplier = Mathf.Clamp(paddingMultiplier, 1f, 3f);
    }

    public void SetActive(bool active)
    {
        recognizerActive = active;
        if (!active)
        {
            tracking = false;
            gesturePoints.Clear();
        }
    }

    private void Update()
    {
        if (!recognizerActive)
        {
            return;
        }

        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            ProcessPointer(touch.position, touch.phase == TouchPhase.Began, touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary, touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled, touch.fingerId);
            return;
        }

        ProcessPointer(Input.mousePosition, Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), -1);
    }

    private void ProcessPointer(Vector2 screenPosition, bool began, bool held, bool ended, int pointerId)
    {
        if (began)
        {
            if (IsPointerOverUi(pointerId))
            {
                return;
            }

            BeginGesture(screenPosition);
            return;
        }

        if (!tracking)
        {
            return;
        }

        if (held)
        {
            if (gesturePoints.Count == 0 || Vector2.Distance(gesturePoints[gesturePoints.Count - 1], screenPosition) > 4f)
            {
                gesturePoints.Add(screenPosition);
            }
        }

        if (ended)
        {
            EndGesture(screenPosition);
        }
    }

    private void BeginGesture(Vector2 screenPosition)
    {
        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        tracking = true;
        startPosition = screenPosition;
        startTime = Time.unscaledTime;
        startZone = ResolveBodyZone(screenPosition);
        gesturePoints.Clear();
        gesturePoints.Add(screenPosition);
    }

    private void EndGesture(Vector2 screenPosition)
    {
        tracking = false;
        gesturePoints.Add(screenPosition);

        PawPalGestureSnapshot snapshot = ClassifyGesture(screenPosition);
        LastGesture = snapshot;
        LastRejectionReason = snapshot.RejectionReason;

        Action<PawPalGestureSnapshot> handler = GestureRecognized;
        if (handler != null)
        {
            handler(snapshot);
        }
    }

    private PawPalGestureSnapshot ClassifyGesture(Vector2 endPosition)
    {
        float duration = Mathf.Max(0f, Time.unscaledTime - startTime);
        Vector2 delta = endPosition - startPosition;
        float distance = delta.magnitude;
        PawPalDogBodyZone endZone = ResolveBodyZone(endPosition);

        PawPalGestureSnapshot snapshot = new PawPalGestureSnapshot
        {
            Type = PawPalGestureType.None,
            StartZone = startZone,
            EndZone = endZone,
            StartScreenPosition = startPosition,
            EndScreenPosition = endPosition,
            DurationSeconds = duration,
            CandidateTrick = PawPalTrickId.Sit,
            RejectionReason = PawPalTrickFailureReason.InvalidGesture,
            DebugText = string.Empty
        };

        float swipeThreshold = minSwipePixels / leniency;
        float tapThreshold = maxTapPixels * leniency;
        if (IsCircularGesture())
        {
            snapshot.Type = PawPalGestureType.CircularSwipe;
            snapshot.CandidateTrick = PawPalTrickId.Spin;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Circular swipe";
            return snapshot;
        }

        if ((startZone == PawPalDogBodyZone.PawLeft || startZone == PawPalDogBodyZone.PawRight) && distance >= swipeThreshold * 0.55f)
        {
            snapshot.Type = PawPalGestureType.DragFromBodyPart;
            snapshot.CandidateTrick = PawPalTrickId.Shake;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Paw drag";
            return snapshot;
        }

        if (IsPetStrokeGesture(startZone, endZone, duration, swipeThreshold, gesturePoints))
        {
            snapshot.Type = PawPalGestureType.PetStroke;
            snapshot.CandidateTrick = PawPalTrickId.Sit;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Pet stroke";
            return snapshot;
        }

        if (distance <= tapThreshold && duration >= minHoldSeconds)
        {
            snapshot.Type = PawPalGestureType.Hold;
            snapshot.CandidateTrick = startZone == PawPalDogBodyZone.AirAboveDog ? PawPalTrickId.Jump : PawPalTrickId.Sit;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Hold";
            return snapshot;
        }

        if (distance <= tapThreshold && duration <= maxTapSeconds)
        {
            snapshot.Type = PawPalGestureType.Tap;
            snapshot.CandidateTrick = startZone == PawPalDogBodyZone.AirAboveDog ? PawPalTrickId.Jump : PawPalTrickId.Sit;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Tap";
            return snapshot;
        }

        if (Mathf.Abs(delta.y) >= swipeThreshold && Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 1.15f)
        {
            snapshot.Type = delta.y < 0f ? PawPalGestureType.SwipeDown : PawPalGestureType.SwipeUp;
            snapshot.CandidateTrick = snapshot.Type == PawPalGestureType.SwipeDown ? PawPalTrickId.Sit : PawPalTrickId.Jump;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = snapshot.Type == PawPalGestureType.SwipeDown ? "Swipe down" : "Swipe up";
            return snapshot;
        }

        if (Mathf.Abs(delta.x) >= swipeThreshold && Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.2f)
        {
            snapshot.Type = PawPalGestureType.HorizontalSwipe;
            snapshot.CandidateTrick = PawPalTrickId.Spin;
            snapshot.RejectionReason = PawPalTrickFailureReason.None;
            snapshot.DebugText = "Horizontal swipe";
            return snapshot;
        }

        snapshot.DebugText = "Gesture too small or off target";
        return snapshot;
    }

    private static bool IsPetStrokeGesture(
        PawPalDogBodyZone gestureStartZone,
        PawPalDogBodyZone gestureEndZone,
        float durationSeconds,
        float swipeThreshold,
        IList<Vector2> points)
    {
        if (!IsPettableDogBodyZone(gestureStartZone)
            || !IsPettableDogBodyZone(gestureEndZone)
            || points == null
            || points.Count < 2)
        {
            return false;
        }

        if (durationSeconds > PetStrokeMaxSeconds)
        {
            return false;
        }

        float pathLength = GetGesturePathLength(points);
        float directDistance = Vector2.Distance(points[0], points[points.Count - 1]);
        if (pathLength < PetStrokeMinPixels
            || directDistance < PetStrokeMinPixels * 0.55f
            || directDistance >= swipeThreshold * 0.92f)
        {
            return false;
        }

        return pathLength <= swipeThreshold * 1.35f;
    }

    private static float GetGesturePathLength(IList<Vector2> points)
    {
        if (points == null || points.Count < 2)
        {
            return 0f;
        }

        float pathLength = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            pathLength += Vector2.Distance(points[i - 1], points[i]);
        }

        return pathLength;
    }

    private static bool IsPettableDogBodyZone(PawPalDogBodyZone zone)
    {
        return zone == PawPalDogBodyZone.Head
            || zone == PawPalDogBodyZone.Chest
            || zone == PawPalDogBodyZone.Back
            || zone == PawPalDogBodyZone.Belly
            || zone == PawPalDogBodyZone.PawLeft
            || zone == PawPalDogBodyZone.PawRight
            || zone == PawPalDogBodyZone.Tail;
    }

    private bool IsCircularGesture()
    {
        if (gesturePoints.Count < 6 || targetRoot == null || targetCamera == null)
        {
            return false;
        }

        Vector2 center;
        if (!TryGetDogScreenCenter(out center))
        {
            center = startPosition;
        }

        float minRadius = circularMinRadiusPixels / leniency;
        float totalAngle = 0f;
        Vector2 previous = gesturePoints[0] - center;
        if (previous.magnitude < minRadius)
        {
            return false;
        }

        for (int i = 1; i < gesturePoints.Count; i++)
        {
            Vector2 current = gesturePoints[i] - center;
            if (current.magnitude < minRadius)
            {
                continue;
            }

            totalAngle += Mathf.Abs(Vector2.SignedAngle(previous, current));
            previous = current;
        }

        return totalAngle >= circularMinAngleDegrees / leniency;
    }

    private PawPalDogBodyZone ResolveBodyZone(Vector2 screenPosition)
    {
        Rect dogRect;
        if (!TryGetDogScreenRect(out dogRect))
        {
            return PawPalDogBodyZone.Any;
        }

        float airHeight = Mathf.Max(42f, dogRect.height * 0.45f);
        Rect airRect = new Rect(dogRect.xMin, dogRect.yMax, dogRect.width, airHeight);
        if (airRect.Contains(screenPosition))
        {
            return PawPalDogBodyZone.AirAboveDog;
        }

        float groundHeight = Mathf.Max(26f, dogRect.height * 0.18f);
        Rect groundRect = new Rect(dogRect.xMin - dogRect.width * 0.2f, dogRect.yMin - groundHeight, dogRect.width * 1.4f, groundHeight);
        if (groundRect.Contains(screenPosition))
        {
            return PawPalDogBodyZone.GroundNearDog;
        }

        if (!dogRect.Contains(screenPosition))
        {
            return PawPalDogBodyZone.Any;
        }

        float normalizedX = Mathf.InverseLerp(dogRect.xMin, dogRect.xMax, screenPosition.x);
        float normalizedY = Mathf.InverseLerp(dogRect.yMin, dogRect.yMax, screenPosition.y);
        if (normalizedY > 0.72f)
        {
            return PawPalDogBodyZone.Head;
        }

        if (normalizedY < 0.35f)
        {
            return normalizedX < 0.5f ? PawPalDogBodyZone.PawLeft : PawPalDogBodyZone.PawRight;
        }

        if (normalizedX < 0.22f)
        {
            return PawPalDogBodyZone.Tail;
        }

        if (normalizedX > 0.78f)
        {
            return PawPalDogBodyZone.Chest;
        }

        return normalizedY < 0.5f ? PawPalDogBodyZone.Belly : PawPalDogBodyZone.Back;
    }

    private bool TryGetDogScreenCenter(out Vector2 center)
    {
        Rect rect;
        if (TryGetDogScreenRect(out rect))
        {
            center = rect.center;
            return true;
        }

        center = Vector2.zero;
        return false;
    }

    private bool TryGetDogScreenRect(out Rect rect)
    {
        rect = new Rect();
        if (targetRoot == null)
        {
            return false;
        }

        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screen = targetCamera.WorldToScreenPoint(corners[i]);
            if (screen.z < 0f)
            {
                continue;
            }

            screenMin = Vector2.Min(screenMin, screen);
            screenMax = Vector2.Max(screenMax, screen);
        }

        if (screenMin.x == float.MaxValue)
        {
            return false;
        }

        rect = Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
        float horizontalPadding = Mathf.Max(MinimumDogRectHorizontalPaddingPixels, rect.width * DogRectHorizontalPadding01) * bodyZonePaddingMultiplier;
        float bottomPadding = Mathf.Max(MinimumDogRectBottomPaddingPixels, rect.height * DogRectBottomPadding01) * bodyZonePaddingMultiplier;
        float topPadding = Mathf.Max(MinimumDogRectTopPaddingPixels, rect.height * DogRectTopPadding01) * bodyZonePaddingMultiplier;
        rect.xMin -= horizontalPadding;
        rect.xMax += horizontalPadding;
        rect.yMin -= bottomPadding;
        rect.yMax += topPadding;
        return rect.width > 1f && rect.height > 1f;
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
