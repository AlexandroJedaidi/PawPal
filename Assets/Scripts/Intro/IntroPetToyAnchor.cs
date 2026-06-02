using UnityEngine;

[DisallowMultipleComponent]
public sealed class IntroPetToyAnchor : MonoBehaviour
{
    [SerializeField] private string toyId = "toy";
    [SerializeField] private float playRadius = 0.45f;

    private IntroPetAgent reservedBy;

    public string ToyId
    {
        get { return toyId; }
    }

    public float PlayRadius
    {
        get { return Mathf.Max(0.1f, playRadius); }
    }

    public bool TryReserve(IntroPetAgent agent)
    {
        if (agent == null)
        {
            return false;
        }

        if (reservedBy != null && reservedBy != agent)
        {
            return false;
        }

        reservedBy = agent;
        return true;
    }

    public void Release(IntroPetAgent agent)
    {
        if (reservedBy == agent)
        {
            reservedBy = null;
        }
    }
}
