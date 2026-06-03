using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalWalkNode : MonoBehaviour
{
    [SerializeField] private string nodeId = "node";
    [SerializeField] private PawPalWalkNodeType nodeType = PawPalWalkNodeType.Corner;
    [SerializeField] private Transform anchor;
    [SerializeField] private bool hasMapPosition;
    [SerializeField] private Vector2 mapPosition;

    public string NodeId => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId.Trim();
    public PawPalWalkNodeType NodeType => nodeType;
    public Transform Anchor => anchor != null ? anchor : transform;
    public bool HasMapPosition => hasMapPosition;
    public Vector2 MapPosition => mapPosition;
    public Vector3 WorldPosition => Anchor.position;
}
