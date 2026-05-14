using UnityEngine;

public class ToyAttach : MonoBehaviour
{
    [SerializeField] Transform mouthSocket;

    GameObject currentToy;
    public void AttachToy()
    {
        if (currentToy != null) return;
        var toy = GameObject.FindWithTag("Toy"); // besser: reference via name or collider
        if (toy == null) return;
        currentToy = toy;
        var rb = currentToy.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
        currentToy.transform.SetParent(mouthSocket, worldPositionStays: false);
        currentToy.transform.localPosition = Vector3.zero;
        currentToy.transform.localRotation = Quaternion.identity;
    }

    public void ReleaseToy()
    {
        if (currentToy == null) return;
        var rb = currentToy.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = false;
        currentToy.transform.SetParent(null);
        currentToy = null;
    }
}
