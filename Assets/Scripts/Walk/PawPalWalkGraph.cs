using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalWalkGraph : MonoBehaviour
{
    [SerializeField] private string graphId = "walking_city";
    [SerializeField] private PawPalWalkNode defaultStartNode;
    [SerializeField] private PawPalWalkNode defaultEndNode;

    public string GraphId => string.IsNullOrWhiteSpace(graphId) ? "walking_city" : graphId.Trim();
    public PawPalWalkNode DefaultStartNode => defaultStartNode;
    public PawPalWalkNode DefaultEndNode => defaultEndNode;

    public void GetNodes(List<PawPalWalkNode> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        GetComponentsInChildren(true, destination);
    }

    public void GetEdges(List<PawPalWalkEdge> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        GetComponentsInChildren(true, destination);
    }

    public void GetEncounterPoints(List<PawPalWalkEncounterPoint> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        GetComponentsInChildren(true, destination);
    }
}
