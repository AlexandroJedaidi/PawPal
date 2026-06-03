using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalLeashSocket : MonoBehaviour
{
    [SerializeField] private Transform socket;

    public Transform Socket
    {
        get
        {
            AutoResolveSocketIfNeeded();
            return socket != null ? socket : transform;
        }
    }

    public void SetSocket(Transform socketTransform)
    {
        socket = socketTransform;
    }

    private void Awake()
    {
        AutoResolveSocketIfNeeded();
    }

    private void OnValidate()
    {
        AutoResolveSocketIfNeeded();
    }

    private void AutoResolveSocketIfNeeded()
    {
        if (socket != null)
        {
            return;
        }

        Transform child = transform.Find("LeashSocket");
        if (child != null)
        {
            socket = child;
        }
    }
}
