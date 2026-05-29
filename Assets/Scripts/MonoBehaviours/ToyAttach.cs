using UnityEngine;

public class ToyAttach : MonoBehaviour
{
    [SerializeField] private Transform mouthSocket;
    [SerializeField] private Vector3 carriedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 carriedLocalEulerAngles = Vector3.zero;
    [SerializeField] private float generatedMouthForwardOffset = 0.055f;
    [SerializeField] private float generatedMouthDownOffset = 0.035f;
    [SerializeField] private float generatedMouthHeightOffset = 0.015f;
    [SerializeField] private float anatomicalMouthForwardOffset = 0.04f;
    [SerializeField] private float anatomicalMouthDownOffset = 0.006f;

    private GameObject currentToy;
    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Rigidbody currentToyRigidbody;
    private Collider[] currentToyColliders;
    private bool[] currentToyColliderStates;
    private Transform generatedCarryAnchor;

    public bool HasToy => currentToy != null;
    public GameObject CurrentToy => currentToy;
    public Transform MouthSocket
    {
        get
        {
            ResolveMouthSocketIfNeeded();
            return mouthSocket;
        }
    }

    public Vector3 GetAnchorWorldPosition()
    {
        Transform carryAnchor = GetCarryAnchor();
        return carryAnchor != null ? carryAnchor.position : transform.position;
    }

    public void AttachToy(string toyName)
    {
        if (currentToy != null)
        {
            return;
        }

        GameObject toy = null;
        if (!string.IsNullOrEmpty(toyName))
        {
            toy = GameObject.Find(toyName);
        }

        if (toy == null)
        {
            try
            {
                GameObject[] candidates = GameObject.FindGameObjectsWithTag("Toy");
                if (candidates.Length > 0)
                {
                    toy = candidates[0];
                }
            }
            catch (UnityException)
            {
                toy = null;
            }
        }

        TryAttachToy(toy);
    }

    public bool TryAttachToy(GameObject toy)
    {
        if (currentToy != null || toy == null)
        {
            return false;
        }

        ResolveMouthSocketIfNeeded();
        Transform carryAnchor = GetCarryAnchor();
        if (carryAnchor == null)
        {
            Debug.LogWarning(name + " cannot pick up " + toy.name + " because no mouth socket was found.");
            return false;
        }

        currentToy = toy;
        Transform toyTransform = currentToy.transform;
        originalParent = toyTransform.parent;
        originalLocalScale = toyTransform.localScale;
        Transform carryParent = GetCarryParent(carryAnchor);
        Vector3 carryLocalPosition = GetCarryLocalPosition(carryParent, carryAnchor);
        Quaternion carryLocalRotation = GetCarryLocalRotation();

        currentToyRigidbody = currentToy.GetComponent<Rigidbody>();
        if (currentToyRigidbody != null)
        {
            if (!currentToyRigidbody.isKinematic)
            {
                currentToyRigidbody.linearVelocity = Vector3.zero;
                currentToyRigidbody.angularVelocity = Vector3.zero;
            }

            currentToyRigidbody.isKinematic = true;
        }

        currentToyColliders = currentToy.GetComponentsInChildren<Collider>(true);
        currentToyColliderStates = new bool[currentToyColliders.Length];
        for (int i = 0; i < currentToyColliders.Length; i++)
        {
            currentToyColliderStates[i] = currentToyColliders[i].enabled;
            currentToyColliders[i].enabled = false;
        }

        toyTransform.SetParent(carryParent, worldPositionStays: false);
        toyTransform.localPosition = carryLocalPosition;
        toyTransform.localRotation = carryLocalRotation;
        toyTransform.localScale = originalLocalScale;
        AlignCarriedToyVisualToAnchor(toyTransform, carryAnchor);
        return true;
    }

    public void ReleaseToy()
    {
        ReleaseToy(currentToy != null ? currentToy.transform.position : transform.position, transform.rotation);
    }

    public GameObject ReleaseToy(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (currentToy == null)
        {
            return null;
        }

        GameObject releasedToy = currentToy;
        Transform toyTransform = releasedToy.transform;
        toyTransform.SetParent(originalParent, worldPositionStays: true);
        toyTransform.position = worldPosition;
        toyTransform.rotation = worldRotation;
        toyTransform.localScale = originalLocalScale;

        RestoreColliderStates();
        SnapReleasedToyToSupportHeight(releasedToy, worldPosition.y);

        if (currentToyRigidbody != null)
        {
            currentToyRigidbody.isKinematic = false;
            currentToyRigidbody.linearVelocity = Vector3.zero;
            currentToyRigidbody.angularVelocity = Vector3.zero;
        }

        currentToy = null;
        originalParent = null;
        currentToyRigidbody = null;
        currentToyColliders = null;
        currentToyColliderStates = null;
        return releasedToy;
    }

    private void RestoreColliderStates()
    {
        if (currentToyColliders == null || currentToyColliderStates == null)
        {
            return;
        }

        for (int i = 0; i < currentToyColliders.Length; i++)
        {
            if (currentToyColliders[i] != null)
            {
                currentToyColliders[i].enabled = currentToyColliderStates[i];
            }
        }
    }

    private void SnapReleasedToyToSupportHeight(GameObject releasedToy, float supportHeight)
    {
        if (releasedToy == null)
        {
            return;
        }

        Bounds bounds;
        if (!TryGetReleasedToyBounds(releasedToy, out bounds))
        {
            return;
        }

        float adjustment = supportHeight - bounds.min.y;
        if (Mathf.Abs(adjustment) <= 0.0001f)
        {
            return;
        }

        releasedToy.transform.position += Vector3.up * adjustment;
    }

    private bool TryGetReleasedToyBounds(GameObject releasedToy, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (currentToyColliders != null)
        {
            for (int i = 0; i < currentToyColliders.Length; i++)
            {
                Collider collider = currentToyColliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = releasedToy.GetComponentsInChildren<Renderer>(true);
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
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private void AlignCarriedToyVisualToAnchor(Transform toyTransform, Transform carryAnchor)
    {
        if (toyTransform == null || carryAnchor == null)
        {
            return;
        }

        Bounds visualBounds;
        if (!TryGetToyVisualBounds(toyTransform.gameObject, out visualBounds))
        {
            return;
        }

        toyTransform.position += carryAnchor.position - visualBounds.center;
    }

    private bool TryGetToyVisualBounds(GameObject toy, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (toy == null)
        {
            return false;
        }

        Renderer[] renderers = toy.GetComponentsInChildren<Renderer>(true);
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
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = toy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }

    private void ResolveMouthSocketIfNeeded()
    {
        if (mouthSocket != null)
        {
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string lowerName = children[i].name.ToLowerInvariant();
            if (lowerName == "mouth" || lowerName.Contains("mouthsocket"))
            {
                mouthSocket = children[i];
                return;
            }
        }
    }

    private Transform GetCarryParent(Transform carryAnchor)
    {
        if (ShouldNeutralizeAnchorRotation(carryAnchor) && carryAnchor.parent != null)
        {
            return carryAnchor.parent;
        }

        return carryAnchor;
    }

    private Vector3 GetCarryLocalPosition(Transform carryParent, Transform carryAnchor)
    {
        if (carryParent == carryAnchor || carryAnchor == null)
        {
            return carriedLocalPosition + GetAnatomicalMouthLocalOffset(carryAnchor);
        }

        return carryAnchor.localPosition + carriedLocalPosition;
    }

    private Quaternion GetCarryLocalRotation()
    {
        return Quaternion.Euler(carriedLocalEulerAngles);
    }

    private Transform GetCarryAnchor()
    {
        ResolveMouthSocketIfNeeded();
        if (mouthSocket == null)
        {
            return null;
        }

        Transform overrideAnchor = FindCarryAnchorOverride();
        if (overrideAnchor != null && IsUsefulCarryAnchorOverride(overrideAnchor))
        {
            return overrideAnchor;
        }

        if (IsSuspiciousMouthSocket(mouthSocket))
        {
            return GetOrCreateGeneratedCarryAnchor();
        }

        return mouthSocket;
    }

    private Transform FindCarryAnchorOverride()
    {
        if (!IsSuspiciousMouthSocket(mouthSocket))
        {
            return null;
        }

        Transform searchRoot = mouthSocket.parent != null ? mouthSocket.parent : transform;

        Transform candidate = FindAnchorByExactName(searchRoot, "mouth");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByExactName(searchRoot, "Mouth.R");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByContainingName(searchRoot, "mouth");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByExactName(searchRoot, "nose");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByContainingName(searchRoot, "nose");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByContainingName(searchRoot, "muzzle");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByContainingName(searchRoot, "jaw");
        if (candidate != null)
        {
            return candidate;
        }

        candidate = FindAnchorByContainingName(searchRoot, "snout");
        if (candidate != null)
        {
            return candidate;
        }

        return null;
    }

    private bool IsUsefulCarryAnchorOverride(Transform candidate)
    {
        if (candidate == null || mouthSocket == null)
        {
            return false;
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        float candidateProjection = Vector3.Dot(candidate.position, forward);
        float socketProjection = Vector3.Dot(mouthSocket.position, forward);
        if (IsAnatomicalMouthAnchor(candidate))
        {
            return true;
        }

        return candidateProjection - socketProjection >= 0.02f;
    }

    private Vector3 GetAnatomicalMouthLocalOffset(Transform carryAnchor)
    {
        if (!IsAnatomicalMouthAnchor(carryAnchor))
        {
            return Vector3.zero;
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        float scale = EstimateDogCarryScale();
        float forwardOffset = Mathf.Clamp(scale * 0.08f, 0.018f, Mathf.Max(0.018f, anatomicalMouthForwardOffset));
        float downOffset = Mathf.Clamp(scale * 0.012f, 0f, Mathf.Max(0f, anatomicalMouthDownOffset));
        return carryAnchor.InverseTransformDirection(forward) * forwardOffset
            + carryAnchor.InverseTransformDirection(Vector3.down) * downOffset;
    }

    private float EstimateDogCarryScale()
    {
        Bounds bounds;
        if (!TryGetDogVisualBounds(out bounds))
        {
            return 0.45f;
        }

        return Mathf.Max(0.2f, bounds.size.y);
    }

    private bool IsAnatomicalMouthAnchor(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "mouth" || lowerName == "mouth.r" || lowerName == "mouth.l";
    }

    private Transform FindAnchorByExactName(Transform searchRoot, string targetName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        string targetLowerName = targetName.ToLowerInvariant();
        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || candidate == mouthSocket)
            {
                continue;
            }

            if (candidate.name.ToLowerInvariant() == targetLowerName)
            {
                return candidate;
            }
        }

        return null;
    }

    private Transform FindAnchorByContainingName(Transform searchRoot, string partialName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(partialName))
        {
            return null;
        }

        string partialLowerName = partialName.ToLowerInvariant();
        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null || candidate == mouthSocket)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (!lowerName.Contains(partialLowerName) || lowerName.Contains("socket"))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private bool ShouldNeutralizeAnchorRotation(Transform carryAnchor)
    {
        return carryAnchor == mouthSocket && IsSuspiciousMouthSocket(carryAnchor) && carryAnchor.parent != null;
    }

    private Transform GetOrCreateGeneratedCarryAnchor()
    {
        if (generatedCarryAnchor != null)
        {
            UpdateGeneratedCarryAnchor();
            return generatedCarryAnchor;
        }

        Transform parent = mouthSocket != null && mouthSocket.parent != null ? mouthSocket.parent : transform;
        GameObject anchorObject = new GameObject(name + "_GeneratedToyMouthAnchor");
        anchorObject.hideFlags = HideFlags.DontSave;
        generatedCarryAnchor = anchorObject.transform;
        generatedCarryAnchor.SetParent(parent, worldPositionStays: false);
        UpdateGeneratedCarryAnchor();
        return generatedCarryAnchor;
    }

    private void UpdateGeneratedCarryAnchor()
    {
        if (generatedCarryAnchor == null)
        {
            return;
        }

        Transform parent = generatedCarryAnchor.parent != null ? generatedCarryAnchor.parent : transform;
        Vector3 targetWorldPosition = ResolveGeneratedMouthWorldPosition(parent);

        generatedCarryAnchor.position = targetWorldPosition;
        generatedCarryAnchor.rotation = transform.rotation;
        generatedCarryAnchor.localScale = Vector3.one;
    }

    private Vector3 ResolveGeneratedMouthWorldPosition(Transform fallbackParent)
    {
        Vector3 referencePosition = mouthSocket != null ? mouthSocket.position : fallbackParent.position;
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        Bounds visualBounds;
        if (!TryGetDogVisualBounds(out visualBounds))
        {
            return referencePosition
                + forward * Mathf.Max(0.01f, generatedMouthForwardOffset)
                + Vector3.down * Mathf.Max(0f, generatedMouthDownOffset);
        }

        float frontProjection = ProjectBoundsFront(visualBounds, forward);
        float referenceProjection = Vector3.Dot(referencePosition, forward);
        float forwardDistance = Mathf.Max(0.02f, frontProjection - referenceProjection + generatedMouthForwardOffset);
        float headHeight = EstimateHeadHeight(visualBounds);

        return referencePosition
            + forward * forwardDistance
            + Vector3.down * Mathf.Clamp(generatedMouthDownOffset, 0f, headHeight * 0.35f)
            + Vector3.up * generatedMouthHeightOffset;
    }

    private bool TryGetDogVisualBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || IsCarriedToyRenderer(renderer))
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

        return hasBounds;
    }

    private bool IsCarriedToyRenderer(Renderer renderer)
    {
        return currentToy != null && renderer.transform != null && renderer.transform.IsChildOf(currentToy.transform);
    }

    private static float ProjectBoundsFront(Bounds bounds, Vector3 forward)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float front = float.NegativeInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    front = Mathf.Max(front, Vector3.Dot(corner, forward));
                }
            }
        }

        return front;
    }

    private static float EstimateHeadHeight(Bounds bounds)
    {
        return Mathf.Clamp(bounds.size.y * 0.22f, 0.06f, 0.28f);
    }

    private bool IsSuspiciousMouthSocket(Transform socket)
    {
        if (socket == null || socket.parent == null)
        {
            return false;
        }

        string lowerName = socket.name.ToLowerInvariant();
        if (!lowerName.Contains("mouthsocket"))
        {
            return false;
        }

        string parentLowerName = socket.parent.name.ToLowerInvariant();
        return parentLowerName == "head" || parentLowerName.Contains("head");
    }
}
