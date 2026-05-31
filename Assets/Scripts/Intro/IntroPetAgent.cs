using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class IntroPetAgent : MonoBehaviour
{
    private const float DestinationReachDistance = 0.18f;
    private const float TurnSpeed = 8f;
    private const float SelectedTurnSpeed = 10f;

    private IntroPetSelectionController controller;
    private IntroPetDefinition definition;
    private FurVariantDefinition activeVariant;
    private GameObject sourcePrefab;
    private Animator animator;
    private Bounds fieldBounds;
    private Vector3 destination;
    private Vector3 homePosition;
    private float moveSpeed;
    private float nextRoamAt;
    private bool selected;
    private bool warnedMissingVariantMaterial;
    private bool warnedMissingSelectedAnimation;

    public int SelectionIndex { get; private set; }

    public IntroPetDefinition Definition
    {
        get { return definition; }
    }

    public FurVariantDefinition ActiveVariant
    {
        get { return activeVariant; }
    }

    public GameObject SourcePrefab
    {
        get { return sourcePrefab; }
    }

    public Transform FocusTransform
    {
        get { return transform; }
    }

    public void Initialize(
        IntroPetSelectionController owner,
        IntroPetDefinition petDefinition,
        FurVariantDefinition variant,
        GameObject prefab,
        int index,
        Bounds roamBounds)
    {
        controller = owner;
        definition = petDefinition;
        activeVariant = variant;
        sourcePrefab = prefab;
        SelectionIndex = index;
        fieldBounds = roamBounds;
        homePosition = transform.position;
        moveSpeed = definition != null && definition.Species == IntroPetSpecies.Cat ? 0.72f : 0.58f;
        animator = GetComponentInChildren<Animator>(true);

        if (definition != null && definition.IntroScale != Vector3.zero)
        {
            transform.localScale = definition.IntroScale;
        }

        if (definition != null && definition.AnimationSet != null)
        {
            definition.AnimationSet.ApplyTo(animator);
        }

        if (variant != null && variant.ReplacementMaterial != null && !PetVariantApplier.ApplyMaterial(gameObject, variant))
        {
            WarnMissingVariantMaterial();
        }

        PetVariantApplier.EnsureTapCollider(gameObject);
        PickNextDestination(true);
    }

    public void SetSelectionIndex(int index)
    {
        SelectionIndex = index;
    }

    public void SetSelected(bool isSelected, Camera camera)
    {
        selected = isSelected;

        if (selected)
        {
            FaceCamera(camera, SelectedTurnSpeed);
            PlaySelectedReaction();
        }
    }

    public void ApplyFurVariant(FurVariantDefinition variant)
    {
        activeVariant = variant;
        if (variant != null && variant.ReplacementMaterial != null && !PetVariantApplier.ApplyMaterial(gameObject, variant))
        {
            WarnMissingVariantMaterial();
        }
    }

    private void Update()
    {
        if (selected)
        {
            FaceCamera(Camera.main, SelectedTurnSpeed);
            SetAnimatorMoving(false, 0f);
            return;
        }

        Roam();
    }

    private void OnMouseDown()
    {
        if (IsPointerOverUi())
        {
            return;
        }

        if (controller != null)
        {
            controller.SelectAgent(this);
        }
    }

    private void Roam()
    {
        if (Time.time < nextRoamAt)
        {
            SetAnimatorMoving(false, 0f);
            return;
        }

        Vector3 delta = destination - transform.position;
        delta.y = 0f;
        if (delta.magnitude <= DestinationReachDistance)
        {
            PickNextDestination(false);
            SetAnimatorMoving(false, 0f);
            return;
        }

        Vector3 direction = delta.normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.position = ClampToField(transform.position);
        FaceDirection(direction, TurnSpeed);
        SetAnimatorMoving(true, moveSpeed);
    }

    private void PickNextDestination(bool immediate)
    {
        Vector3 center = fieldBounds.center;
        float radius = definition != null ? Mathf.Max(1.2f, definition.RoamRadius) : 3.2f;
        Vector2 offset = Random.insideUnitCircle * radius;
        destination = ClampToField(homePosition + new Vector3(offset.x, 0f, offset.y));
        nextRoamAt = immediate ? Time.time : Time.time + Random.Range(0.9f, 2.5f);
    }

    private Vector3 ClampToField(Vector3 position)
    {
        if (fieldBounds.size == Vector3.zero)
        {
            return position;
        }

        position.x = Mathf.Clamp(position.x, fieldBounds.min.x, fieldBounds.max.x);
        position.z = Mathf.Clamp(position.z, fieldBounds.min.z, fieldBounds.max.z);
        return position;
    }

    private void FaceCamera(Camera camera, float speed)
    {
        if (camera == null)
        {
            return;
        }

        Vector3 direction = camera.transform.position - transform.position;
        direction.y = 0f;
        FaceDirection(direction, speed);
    }

    private void FaceDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
    }

    private void SetAnimatorMoving(bool moving, float speed)
    {
        if (definition != null && definition.AnimationSet != null && definition.AnimationSet.TrySetMoving(animator, moving, speed))
        {
            return;
        }

        if (animator != null)
        {
            animator.speed = moving ? 1f : 0.85f;
        }
    }

    private void PlaySelectedReaction()
    {
        if (definition == null || definition.AnimationSet == null || animator == null)
        {
            WarnMissingSelectedAnimation();
            return;
        }

        if (!definition.AnimationSet.TryPlaySelectedReaction(animator))
        {
            WarnMissingSelectedAnimation();
        }
    }

    private void WarnMissingVariantMaterial()
    {
        if (warnedMissingVariantMaterial)
        {
            return;
        }

        warnedMissingVariantMaterial = true;
        string petName = definition != null ? definition.DisplayName : name;
        string variantName = activeVariant != null ? activeVariant.SafeDisplayName : "selected";
        Debug.LogWarning("IntroPetSelection could not apply material variant '" + variantName + "' to " + petName + ". Keeping the prefab's existing materials.");
    }

    private void WarnMissingSelectedAnimation()
    {
        if (warnedMissingSelectedAnimation)
        {
            return;
        }

        warnedMissingSelectedAnimation = true;
        string petName = definition != null ? definition.DisplayName : name;
        Debug.LogWarning("IntroPetSelection has no selected reaction animation mapping for " + petName + ". The pet will face the camera instead.");
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return eventSystem.IsPointerOverGameObject();
    }
}
