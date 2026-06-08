using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PawPalWalkEncounterTemplate
{
    public string EventTemplateId = "encounter";
    public PawPalWalkEventType EventType = PawPalWalkEventType.PersonalityMoment;
    public string DisplayName;
    [TextArea(2, 4)] public string BodyText;
    public string RewardItemId;
    public string EncounterDogName;
    public IntroPetSpecies VisitorSpecies = IntroPetSpecies.Dog;
    public string VisitorPetDefinitionKey;
    public string VisitorDisplayName;
    public PawPalWalkStopType StopType = PawPalWalkStopType.Sniff;
    [Min(0.01f)] public float Weight = 1f;
    public bool OneShotPerSession = true;
}

[DisallowMultipleComponent]
public sealed class PawPalWalkEncounterPoint : MonoBehaviour
{
    [SerializeField] private string encounterPointId = "encounter";
    [SerializeField] private Transform anchor;
    [SerializeField] private PawPalWalkNode sourceNode;
    [SerializeField, Min(0.05f)] private float triggerRadius = 0.45f;
    [SerializeField] private List<PawPalWalkEncounterTemplate> encounterPool = new List<PawPalWalkEncounterTemplate>();

    public string EncounterPointId => string.IsNullOrWhiteSpace(encounterPointId) ? gameObject.name : encounterPointId.Trim();
    public Transform Anchor => anchor != null ? anchor : transform;
    public PawPalWalkNode SourceNode => sourceNode;
    public float TriggerRadius => Mathf.Max(0.05f, triggerRadius);
    public IList<PawPalWalkEncounterTemplate> EncounterPool => encounterPool;
    public Vector3 WorldPosition => Anchor.position;
}
