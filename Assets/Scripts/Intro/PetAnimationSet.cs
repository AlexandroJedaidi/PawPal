using UnityEngine;

[CreateAssetMenu(menuName = "PawFriends/Intro/Pet Animation Set", fileName = "PetAnimationSet")]
public sealed class PetAnimationSet : ScriptableObject
{
    public RuntimeAnimatorController RuntimeController;
    public AnimationClip IdleClip;
    public AnimationClip WalkClip;
    public AnimationClip SelectedClip;
    public string IdleStateName = "Idle";
    public string WalkStateName = "Locomotion";
    public string SelectedStateName = string.Empty;
    public string MoveBoolParameter = "Move";
    public string SpeedFloatParameter = "Speed";
    public string IdleIndexParameter = "IdleIndex";
    public int SelectedIdleIndex = 1;
    public string SelectedTriggerParameter = string.Empty;

    public void ApplyTo(Animator animator)
    {
        ApplyTo(animator, null);
    }

    public void ApplyTo(Animator animator, IntroPetDefinition definition)
    {
        if (animator == null)
        {
            return;
        }

        RuntimeAnimatorController resolvedController = PetAnimationControllerResolver.Resolve(RuntimeController, animator, definition);
        if (resolvedController != null && animator.runtimeAnimatorController != resolvedController)
        {
            animator.runtimeAnimatorController = resolvedController;
        }
    }

    public bool TrySetMoving(Animator animator, bool moving, float speed)
    {
        if (animator == null)
        {
            return false;
        }

        bool applied = false;
        if (!string.IsNullOrEmpty(MoveBoolParameter))
        {
            animator.SetBool(MoveBoolParameter, moving);
            applied = true;
        }

        if (!string.IsNullOrEmpty(SpeedFloatParameter))
        {
            animator.SetFloat(SpeedFloatParameter, Mathf.Max(0f, speed));
            applied = true;
        }

        return applied;
    }

    public bool TryPlaySelectedReaction(Animator animator)
    {
        if (animator == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(SelectedTriggerParameter))
        {
            animator.SetTrigger(SelectedTriggerParameter);
            return true;
        }

        int stateHash = ResolveStateHash(animator, SelectedStateName);
        if (stateHash != 0)
        {
            animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
            return true;
        }

        int selectedIdleIndex = SelectedIdleIndex > 0 ? SelectedIdleIndex : 1;
        if (TrySetIdleIndex(animator, IdleIndexParameter, selectedIdleIndex))
        {
            return true;
        }

        string[] fallbackStates =
        {
            "Idle2",
            "Idle1",
            "Idle3",
            "CatSimple_Idle_2",
            "CatSimple_Idle_1"
        };

        for (int i = 0; i < fallbackStates.Length; i++)
        {
            stateHash = ResolveStateHash(animator, fallbackStates[i]);
            if (stateHash != 0)
            {
                animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
                return true;
            }
        }

        if (TrySetIdleIndex(animator, "IdleIndex", selectedIdleIndex))
        {
            return true;
        }

        return false;
    }

    private static bool TrySetIdleIndex(Animator animator, string parameterName, int idleIndex)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName) || !HasIntegerParameter(animator, parameterName))
        {
            return false;
        }

        animator.SetInteger(parameterName, idleIndex);
        return true;
    }

    private static bool HasIntegerParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Int
                && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveStateHash(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return 0;
        }

        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(0, hash))
        {
            return hash;
        }

        string layerName = animator.GetLayerName(0);
        string[] candidates =
        {
            layerName + "." + stateName,
            "Base Layer." + stateName,
            layerName + ".IdleSM." + stateName,
            "Base Layer.IdleSM." + stateName
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            hash = Animator.StringToHash(candidates[i]);
            if (animator.HasState(0, hash))
            {
                return hash;
            }
        }

        return 0;
    }
}
