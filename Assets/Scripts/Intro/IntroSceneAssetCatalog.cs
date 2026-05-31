using UnityEngine;

[CreateAssetMenu(menuName = "PawPal/Intro/Scene Asset Catalog", fileName = "IntroSceneAssetCatalog")]
public sealed class IntroSceneAssetCatalog : ScriptableObject
{
    public GameObject FencePrefab;
    public GameObject BirchTreePrefab;
    public GameObject AshTreePrefab;
    public GameObject BigBallPrefab;
    public GameObject BonePrefab;
    public AudioClip BirdsAmbientClip;
}
