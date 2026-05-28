using UnityEngine;

public class ToyAttach : MonoBehaviour
{
    [SerializeField] private Transform mouthSocket;
    [SerializeField] private Vector3 carriedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 carriedLocalEulerAngles = Vector3.zero;

    private GameObject currentToy;
    private Transform originalParent;
    private Vector3 originalLocalScale;
    private Rigidbody currentToyRigidbody;
    private Collider[] currentToyColliders;
    private bool[] currentToyColliderStates;

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
            currentToyRigidbody.isKinematic = true;
            currentToyRigidbody.linearVelocity = Vector3.zero;
            currentToyRigidbody.angularVelocity = Vector3.zero;
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
            currentToyRigidbody.linearVelocity = Vector3.zero;
            currentToyRigidbody.angularVelocity = Vector3.zero;
            currentToyRigidbody.isKinematic = false;
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
            return carriedLocalPosition;
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
        return overrideAnchor != null ? overrideAnchor : mouthSocket;
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

        return FindAnchorByExactName(searchRoot, "nose");
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
