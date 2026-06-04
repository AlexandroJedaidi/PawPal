using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PawPalLeashDragController : MonoBehaviour
{
    [SerializeField] private PawPalLeashRig leashRig;
    [SerializeField] private Transform handleGrabTarget;
    [SerializeField] private Transform dragTargetRoot;
    [SerializeField] private float maxRayDistance = 250f;
    [SerializeField] private bool ignorePointerWhenOverUi = true;

    private Camera sceneCamera;
    private Plane dragPlane;
    private bool dragging;
    private int activeTouchId = -1;
    private Vector3 dragOffset;
    private Vector3 lastDragPosition;
    private Vector3 lastDragDelta;
    private Vector3 lastDragVelocity;
    private bool hasDragPosition;

    public bool IsDragging => dragging;
    public Vector3 LastDragPosition => lastDragPosition;
    public Vector3 LastDragDelta => lastDragDelta;
    public Vector3 LastDragVelocity => lastDragVelocity;

    public void Configure(PawPalLeashRig rig, Transform grabTarget)
    {
        leashRig = rig;
        handleGrabTarget = grabTarget;
        dragTargetRoot = rig != null ? rig.DragInteractionRoot : grabTarget;
    }

    private void Update()
    {
        if (leashRig == null || handleGrabTarget == null)
        {
            return;
        }

        if (leashRig != null && dragTargetRoot != leashRig.DragInteractionRoot)
        {
            dragTargetRoot = leashRig.DragInteractionRoot;
        }

        sceneCamera = sceneCamera != null ? sceneCamera : Camera.main;
        if (sceneCamera == null)
        {
            return;
        }

        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (!dragging)
        {
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi(-1))
            {
                TryBeginDrag(Input.mousePosition, -1);
            }

            return;
        }

        if (Input.GetMouseButton(0))
        {
            UpdateDrag(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    private void HandleTouchInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (!dragging)
            {
                if (touch.phase == TouchPhase.Began && !IsPointerOverUi(touch.fingerId))
                {
                    TryBeginDrag(touch.position, touch.fingerId);
                }

                continue;
            }

            if (touch.fingerId != activeTouchId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                UpdateDrag(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndDrag();
            }
        }
    }

    private void TryBeginDrag(Vector2 screenPosition, int touchId)
    {
        if (!TryRaycastHandle(screenPosition, out RaycastHit hit))
        {
            return;
        }

        Transform dragRoot = leashRig.DragRoot;
        if (dragRoot == null)
        {
            return;
        }

        dragPlane = new Plane(-sceneCamera.transform.forward, dragRoot.position);
        Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        dragging = true;
        activeTouchId = touchId;
        leashRig.SetHandleDragActive(true);
        Vector3 worldHit = ray.GetPoint(enter);
        dragOffset = dragRoot.position - worldHit;
        lastDragPosition = dragRoot.position;
        lastDragDelta = Vector3.zero;
        lastDragVelocity = Vector3.zero;
        hasDragPosition = true;
        UpdateDrag(screenPosition);
    }

    private void UpdateDrag(Vector2 screenPosition)
    {
        if (!dragging)
        {
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 newPosition = ray.GetPoint(enter) + dragOffset;
        if (hasDragPosition)
        {
            lastDragDelta = newPosition - lastDragPosition;
            lastDragVelocity = Time.deltaTime > 0.0001f ? lastDragDelta / Time.deltaTime : Vector3.zero;
        }
        else
        {
            lastDragDelta = Vector3.zero;
            lastDragVelocity = Vector3.zero;
            hasDragPosition = true;
        }

        lastDragPosition = newPosition;
        leashRig.SetDragRootPosition(newPosition);
    }

    private void EndDrag()
    {
        dragging = false;
        activeTouchId = -1;
        hasDragPosition = false;
        lastDragDelta = Vector3.zero;
        lastDragVelocity = Vector3.zero;
        if (leashRig != null)
        {
            leashRig.SetHandleDragActive(false);
        }
    }

    private bool TryRaycastHandle(Vector2 screenPosition, out RaycastHit hit)
    {
        Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance);
        if (hits == null || hits.Length == 0)
        {
            hit = default;
            return false;
        }

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidateHit = hits[i];
            Transform hitTransform = candidateHit.transform;
            if (hitTransform == null)
            {
                continue;
            }

            if (hitTransform == handleGrabTarget || (handleGrabTarget != null && hitTransform.IsChildOf(handleGrabTarget)))
            {
                hit = candidateHit;
                return true;
            }

            if (dragTargetRoot != null && (hitTransform == dragTargetRoot || hitTransform.IsChildOf(dragTargetRoot)))
            {
                hit = candidateHit;
                return true;
            }
        }

        hit = default;
        return false;
    }

    private bool IsPointerOverUi(int pointerId)
    {
        if (!ignorePointerWhenOverUi || EventSystem.current == null)
        {
            return false;
        }

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }
}
