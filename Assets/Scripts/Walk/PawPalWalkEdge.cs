using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalWalkEdge : MonoBehaviour
{
    [SerializeField] private string edgeId = "edge";
    [SerializeField] private PawPalWalkNode startNode;
    [SerializeField] private PawPalWalkNode endNode;
    [SerializeField] private int laneIndex;
    [SerializeField] private List<Transform> pathPoints = new List<Transform>();

    public string EdgeId => string.IsNullOrWhiteSpace(edgeId) ? gameObject.name : edgeId.Trim();
    public PawPalWalkNode StartNode => startNode;
    public PawPalWalkNode EndNode => endNode;
    public int LaneIndex => laneIndex;

    public bool TryGetWorldPath(List<Vector3> destination)
    {
        if (destination == null || startNode == null || endNode == null)
        {
            return false;
        }

        destination.Clear();
        AddPointIfDistinct(destination, startNode.WorldPosition);
        if (pathPoints != null && pathPoints.Count > 0)
        {
            for (int i = 0; i < pathPoints.Count; i++)
            {
                Transform point = pathPoints[i];
                if (point != null)
                {
                    AddPointIfDistinct(destination, point.position);
                }
            }
        }
        else
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform point = transform.GetChild(i);
                if (point != null)
                {
                    AddPointIfDistinct(destination, point.position);
                }
            }
        }

        AddPointIfDistinct(destination, endNode.WorldPosition);
        return destination.Count >= 2;
    }

    private static void AddPointIfDistinct(List<Vector3> destination, Vector3 point)
    {
        if (destination.Count == 0 || Vector3.Distance(destination[destination.Count - 1], point) > 0.05f)
        {
            destination.Add(point);
        }
    }
}
