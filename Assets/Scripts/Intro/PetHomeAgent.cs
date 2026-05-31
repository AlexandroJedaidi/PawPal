using UnityEngine;

[DisallowMultipleComponent]
public sealed class PetHomeAgent : MonoBehaviour
{
    private const float ReachDistance = 0.16f;

    [SerializeField] private float roamRadius = 1.35f;
    [SerializeField] private float moveSpeed = 0.48f;
    [SerializeField] private float turnSpeed = 7f;
    [SerializeField] private float minWait = 1.4f;
    [SerializeField] private float maxWait = 3.8f;

    private Animator animator;
    private PetAnimationSet animationSet;
    private Vector3 homePosition;
    private Vector3 destination;
    private float waitUntil;

    public SelectedPetSessionData SessionData { get; private set; }

    public void Initialize(SelectedPetSessionData data)
    {
        SessionData = data;
        name = "SelectedIntroCat_" + (data != null ? data.SafeName : "Buddy");
        animator = GetComponentInChildren<Animator>(true);
        animationSet = data != null && data.Definition != null ? data.Definition.AnimationSet : null;
        if (animationSet != null)
        {
            animationSet.ApplyTo(animator);
        }

        if (data != null && data.Definition != null && data.Definition.HomeScale != Vector3.zero)
        {
            transform.localScale = data.Definition.HomeScale;
        }

        if (data != null && data.FurVariant != null && data.FurVariant.ReplacementMaterial != null)
        {
            PetVariantApplier.ApplyMaterial(gameObject, data.FurVariant);
        }

        PetVariantApplier.EnsureTapCollider(gameObject);
        homePosition = transform.position;
        PickDestination(true);
    }

    private void Update()
    {
        Roam();
    }

    private void Roam()
    {
        if (Time.time < waitUntil)
        {
            SetMoving(false);
            return;
        }

        Vector3 delta = destination - transform.position;
        delta.y = 0f;
        if (delta.magnitude <= ReachDistance)
        {
            PickDestination(false);
            SetMoving(false);
            return;
        }

        Vector3 direction = delta.normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        Face(direction);
        SetMoving(true);
    }

    private void PickDestination(bool immediate)
    {
        Vector2 offset = Random.insideUnitCircle * roamRadius;
        destination = homePosition + new Vector3(offset.x, 0f, offset.y);
        waitUntil = immediate ? Time.time : Time.time + Random.Range(minWait, maxWait);
    }

    private void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private void SetMoving(bool moving)
    {
        if (animationSet != null && animationSet.TrySetMoving(animator, moving, moveSpeed))
        {
            return;
        }

        if (animator != null)
        {
            animator.speed = moving ? 1f : 0.85f;
        }
    }
}
