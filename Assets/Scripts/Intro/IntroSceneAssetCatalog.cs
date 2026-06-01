using System;
using UnityEngine;

[Serializable]
public struct IntroBackdropScreenLayout
{
    public Vector3 LocalPosition;
    public Vector3 LocalEulerAngles;
    public Vector2 Size;
}

[CreateAssetMenu(menuName = "PawFriends/Intro/Scene Asset Catalog", fileName = "IntroSceneAssetCatalog")]
public sealed class IntroSceneAssetCatalog : ScriptableObject
{
    public GameObject FencePrefab;
    public GameObject BirchTreePrefab;
    public GameObject AshTreePrefab;
    public GameObject BigBallPrefab;
    public GameObject BonePrefab;
    public Texture2D DayBackdropTexture;
    public Texture2D NightBackdropTexture;
    public AudioClip DayThemeClip;
    public AudioClip NightAmbienceClip;
    public AudioClip BirdsAmbientClip;
    public IntroBackdropScreenLayout[] BackdropScreens;
}
