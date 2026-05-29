using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class PawPalBallBounceAudio : MonoBehaviour
{
#if UNITY_EDITOR
    private const string EditorBallBounceAudioAssetPath = "Assets/Audio/ball_bounce.mp3";
#endif

    [SerializeField] private AudioClip bounceClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [SerializeField] private float minCollisionSpeed = 0.25f;
    [SerializeField] private float minSecondsBetweenSounds = 0.18f;

    private AudioSource source;
    private float nextAllowedSoundTime;

    public static PawPalBallBounceAudio EnsureOn(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PawPalBallBounceAudio audio = target.GetComponent<PawPalBallBounceAudio>();
        if (audio == null)
        {
            audio = target.AddComponent<PawPalBallBounceAudio>();
        }

        audio.AutoAssignEditorAudioClip();
        audio.EnsureAudioSource();
        return audio;
    }

    private void Awake()
    {
        AutoAssignEditorAudioClip();
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        AutoAssignEditorAudioClip();
        EnsureAudioSource();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryPlayCollisionSound(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryPlayCollisionSound(collision);
    }

    private void TryPlayCollisionSound(Collision collision)
    {
        if (bounceClip == null
            || collision == null
            || collision.relativeVelocity.magnitude < Mathf.Max(0.01f, minCollisionSpeed)
            || Time.time < nextAllowedSoundTime)
        {
            return;
        }

        EnsureAudioSource();
        if (source == null)
        {
            return;
        }

        nextAllowedSoundTime = Time.time + Mathf.Max(0f, minSecondsBetweenSounds);
        source.PlayOneShot(bounceClip, Mathf.Clamp01(volume));
    }

    private void EnsureAudioSource()
    {
        if (bounceClip == null)
        {
            return;
        }

        if (source == null)
        {
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.25f;
        source.maxDistance = 6f;
    }

    private void AutoAssignEditorAudioClip()
    {
#if UNITY_EDITOR
        if (bounceClip == null)
        {
            bounceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorBallBounceAudioAssetPath);
        }
#endif
    }
}
