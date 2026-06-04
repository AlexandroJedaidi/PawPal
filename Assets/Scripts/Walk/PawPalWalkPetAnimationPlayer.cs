using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class PawPalWalkPetAnimationPlayer
{
    private const int BaseLayerIndex = 0;
    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly HashSet<string> WarnedMissingAnimations = new HashSet<string>();

    public static IEnumerator PlayOneShot(
        MonoBehaviour owner,
        Animator animator,
        IntroPetDefinition definition,
        string runtimeBreed,
        string objectName,
        float duration,
        params string[] stateOrClipNames)
    {
        if (owner == null || animator == null)
        {
            yield break;
        }

        SetLocomotion(animator, false, 0f);
        int stateHash = ResolveAnimatorStateHash(animator, stateOrClipNames);
        if (stateHash != 0)
        {
            animator.CrossFadeInFixedTime(stateHash, 0.08f, BaseLayerIndex, 0f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            yield break;
        }

#if UNITY_EDITOR
        AnimationClip clip = ResolveEditorClip(animator, definition, runtimeBreed, objectName, stateOrClipNames);
        if (clip != null)
        {
            yield return PlayDirectClip(owner, animator, clip, Mathf.Max(duration, clip.length));
            yield break;
        }
#endif

        WarnMissing(animator, stateOrClipNames);
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
    }

    public static void PlayIdleIndex(Animator animator, int idleIndex)
    {
        if (animator == null)
        {
            return;
        }

        SetLocomotion(animator, false, 0f);
        if (HasAnimatorParameter(animator, IdleIndexHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(IdleIndexHash, idleIndex);
        }
    }

    public static void ForceLocomotion(Animator animator, float speed, params string[] preferredStateNames)
    {
        if (animator == null)
        {
            return;
        }

        SetLocomotion(animator, true, Mathf.Max(0f, speed));
        if (HasAnimatorParameter(animator, IdleIndexHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(IdleIndexHash, -1);
        }

        int stateHash = ResolveAnimatorStateHash(animator, preferredStateNames);
        if (stateHash != 0)
        {
            animator.CrossFadeInFixedTime(stateHash, 0.08f, BaseLayerIndex, 0f);
        }
    }

    public static void ForceLocomotionForPace(
        Animator animator,
        PawPalPetMovementProfile movementProfile,
        IntroPetSpecies species,
        DogMovementPace pace)
    {
        if (animator == null)
        {
            return;
        }

        if (species == IntroPetSpecies.Cat)
        {
            string[] catLocomotionStates = GetLocomotionStateNames(species, pace);
            ForceLocomotion(
                animator,
                movementProfile.GetAnimatorSpeed(pace),
                catLocomotionStates[0],
                catLocomotionStates[1],
                catLocomotionStates[2]);
            return;
        }

        SetLocomotion(animator, true, Mathf.Max(0f, movementProfile.GetAnimatorSpeed(pace)));
        if (HasAnimatorParameter(animator, IdleIndexHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(IdleIndexHash, -1);
        }

        int stateHash = ResolveAnimatorStateHash(animator, GetLocomotionStateNames(species, pace));
        if (stateHash != 0)
        {
            animator.CrossFadeInFixedTime(stateHash, 0.08f, BaseLayerIndex, 0f);
            return;
        }

        WarnMissing(animator, GetLocomotionStateNames(species, pace));
    }

    public static string[] GetLocomotionStateNames(IntroPetSpecies species, DogMovementPace pace)
    {
        if (species == IntroPetSpecies.Cat)
        {
            switch (pace)
            {
                case DogMovementPace.Run:
                    return new[] { "CatSimple_Run_F_RM", "Arm_Cat|Run_F_RM", "Run_F_RM" };
                case DogMovementPace.Trot:
                    return new[] { "CatSimple_Trot_F_RM", "Arm_Cat|Trot_F_RM", "Trot_F_RM" };
                default:
                    return new[] { "CatSimple_Walk_F_RM", "Arm_Cat|Walk_F_RM", "Walk_F_RM" };
            }
        }

        switch (pace)
        {
            case DogMovementPace.Run:
                return new[] { "RunForward", "Run_F_RM" };
            case DogMovementPace.Trot:
                return new[] { "Locomotion", "Trot_F_RM" };
            default:
                return new[] { "Locomotion", "Walk_F_RM" };
        }
    }

    public static string[] GetJumpReactionStateNames(IntroPetSpecies species)
    {
        if (species == IntroPetSpecies.Cat)
        {
            return new[] { "CatSimple_JumpPlace_RM", "Arm_Cat|JumpPlace_RM" };
        }

        return new[] { "JumpInPlace", "Jump_Place_RM", "JumpPlace_RM" };
    }

    private static IEnumerator PlayDirectClip(MonoBehaviour owner, Animator animator, AnimationClip clip, float duration)
    {
        PlayableGraph graph = PlayableGraph.Create(animator.name + "_WalkLeashOneShot");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "WalkLeashOneShot", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(true);
        output.SetSourcePlayable(playable);
        graph.Play();

        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));

        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    private static void SetLocomotion(Animator animator, bool moving, float speed)
    {
        if (HasAnimatorParameter(animator, MoveHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(MoveHash, moving);
        }

        if (HasAnimatorParameter(animator, SpeedHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(SpeedHash, speed);
        }

        if (HasAnimatorParameter(animator, DirectionHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(DirectionHash, 0f);
        }
    }

    private static int ResolveAnimatorStateHash(Animator animator, params string[] stateNames)
    {
        if (animator == null || stateNames == null || animator.layerCount <= BaseLayerIndex)
        {
            return 0;
        }

        string layerName = animator.GetLayerName(BaseLayerIndex);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
            {
                continue;
            }

            string[] candidates =
            {
                stateName,
                layerName + "." + stateName,
                "Base Layer." + stateName,
                layerName + ".IdleSM." + stateName,
                "Base Layer.IdleSM." + stateName
            };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                int stateHash = Animator.StringToHash(candidates[candidateIndex]);
                if (animator.HasState(BaseLayerIndex, stateHash))
                {
                    return stateHash;
                }
            }
        }

        return 0;
    }

#if UNITY_EDITOR
    private static AnimationClip ResolveEditorClip(
        Animator animator,
        IntroPetDefinition definition,
        string runtimeBreed,
        string objectName,
        string[] clipNames)
    {
        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntry(animator, definition, runtimeBreed, objectName, out entry))
        {
            return null;
        }

        Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
        if (clips == null || clipNames == null)
        {
            return null;
        }

        for (int i = 0; i < clipNames.Length; i++)
        {
            string clipName = clipNames[i];
            if (string.IsNullOrWhiteSpace(clipName))
            {
                continue;
            }

            AnimationClip clip;
            if (clips.TryGetValue(clipName, out clip) && clip != null)
            {
                return clip;
            }
        }

        return null;
    }
#endif

    private static bool HasAnimatorParameter(Animator animator, int parameterHash, AnimatorControllerParameterType type)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash && parameters[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private static void WarnMissing(Animator animator, string[] names)
    {
        string key = (animator != null ? animator.name : "Animator") + "|" + string.Join(",", names ?? new string[0]);
        if (!WarnedMissingAnimations.Add(key))
        {
            return;
        }

        Debug.LogWarning("PawPal walk leash animation could not resolve any of: " + key);
    }
}
