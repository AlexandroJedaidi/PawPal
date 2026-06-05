using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
internal static class PawPalCatBaseControllerBuilder
{
    private const string ControllerPath = "Assets/Animations/Cat_BaseController.controller";
    private const string AnimationSetPath = "Assets/Resources/PawPal/IntroPets/Animations/CatIntroAnimationSet.asset";
    private const string BaseClipEntryKey = "cat_chubby";
    private static readonly string[] RequiredStates =
    {
        "Idle1",
        "Idle2",
        "Idle3",
        "CatSimple_Walk_F_RM",
        "CatSimple_Trot_F_RM",
        "CatSimple_Run_F_RM",
        PawPalPetTurnAnimationUtility.TurnLeftStateName,
        PawPalPetTurnAnimationUtility.TurnRightStateName,
        "CatSimple_JumpPlace_RM",
        "CatSimple_Sit_start",
        "CatSimple_Sit_loop_1",
        "CatSimple_Sit_end",
        "CatSimple_Lie_belly_loop_1",
        "CatSimple_Scratching"
    };
    private static readonly Dictionary<string, string> RequiredStateClipKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Idle1", "Idle_1" },
        { "Idle2", "Idle_2" },
        { "Idle3", "Idle_3" },
        { "CatSimple_Walk_F_RM", "Walk_F_RM" },
        { "CatSimple_Trot_F_RM", "Trot_F_RM" },
        { "CatSimple_Run_F_RM", "Run_F_RM" },
        { PawPalPetTurnAnimationUtility.TurnLeftStateName, "Turn_L_IP" },
        { PawPalPetTurnAnimationUtility.TurnRightStateName, "Turn_R_IP" },
        { "CatSimple_JumpPlace_RM", "JumpPlace_RM" },
        { "CatSimple_Sit_start", "Sit_start" },
        { "CatSimple_Sit_loop_1", "Sit_loop_1" },
        { "CatSimple_Sit_end", "Sit_end" },
        { "CatSimple_Lie_belly_loop_1", "Lie_belly_loop_1" },
        { "CatSimple_Scratching", "Scratching" }
    };

    private static bool ensureQueued;
    private static bool ensureRunning;

    static PawPalCatBaseControllerBuilder()
    {
        QueueEnsure();
    }

    [MenuItem("PawPal/Rebuild Cat Base Controller")]
    private static void RebuildFromMenu()
    {
        RebuildNow(true);
    }

    internal static void RebuildNow(bool force)
    {
        EnsureCatController(force);
    }

    private static void QueueEnsure()
    {
        if (ensureQueued)
        {
            return;
        }

        ensureQueued = true;
        EditorApplication.delayCall += EnsureQueuedController;
    }

    private static void EnsureQueuedController()
    {
        ensureQueued = false;
        EnsureCatController(false);
    }

    private static void EnsureCatController(bool force)
    {
        if (ensureRunning || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        ensureRunning = true;
        try
        {
            PawPalPetAnimationRegistry.ClearEditorCaches();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!force && ControllerIsHealthy(controller))
            {
                EnsureAnimationSet(controller);
                return;
            }

            controller = controller ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            RebuildController(controller);
            EnsureAnimationSet(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            ensureRunning = false;
        }
    }

    private static bool ControllerIsHealthy(AnimatorController controller)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            return false;
        }

        if (!string.Equals(controller.name, "Cat_BaseController", StringComparison.Ordinal))
        {
            return false;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        if (stateMachine == null)
        {
            return false;
        }

        HashSet<string> stateNames = new HashSet<string>(StringComparer.Ordinal);
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && !string.IsNullOrEmpty(states[i].state.name))
            {
                stateNames.Add(states[i].state.name);
            }
        }

        if (stateNames.Contains("Locomotion"))
        {
            return false;
        }

        for (int i = 0; i < RequiredStates.Length; i++)
        {
            if (!stateNames.Contains(RequiredStates[i]))
            {
                return false;
            }
        }

        Dictionary<string, AnimationClip> expectedClips = LoadBaseClips();
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state == null)
            {
                continue;
            }

            string expectedClipKey;
            if (!RequiredStateClipKeys.TryGetValue(state.name, out expectedClipKey))
            {
                continue;
            }

            AnimationClip expectedClip = ResolveClip(expectedClips, expectedClipKey);
            if (!ReferenceEquals(state.motion, expectedClip))
            {
                return false;
            }
        }

        return HasParameter(controller, "Move", AnimatorControllerParameterType.Bool)
            && HasParameter(controller, "Speed", AnimatorControllerParameterType.Float);
    }

    private static void RebuildController(AnimatorController controller)
    {
        if (controller == null)
        {
            return;
        }

        controller.name = "Cat_BaseController";

        AnimatorStateMachine stateMachine = new AnimatorStateMachine();
        stateMachine.name = "Base Layer";
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        AnimatorControllerLayer baseLayer = new AnimatorControllerLayer();
        baseLayer.name = "Base Layer";
        baseLayer.defaultWeight = 1f;
        baseLayer.stateMachine = stateMachine;
        controller.layers = new[] { baseLayer };

        AddParameterIfMissing(controller, "Move", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "Direction", AnimatorControllerParameterType.Float);

        Dictionary<string, AnimationClip> clips = LoadBaseClips();
        AnimatorState idle1 = AddState(stateMachine, "Idle1", ResolveClip(clips, "Idle_1"));
        AddState(stateMachine, "Idle2", ResolveClip(clips, "Idle_2"));
        AddState(stateMachine, "Idle3", ResolveClip(clips, "Idle_3"));
        AddState(stateMachine, "Idle4", ResolveClip(clips, "Idle_4"));
        AddState(stateMachine, "CatSimple_Idle_1", ResolveClip(clips, "Idle_1"));
        AddState(stateMachine, "CatSimple_Idle_2", ResolveClip(clips, "Idle_2"));
        AddState(stateMachine, "CatSimple_Idle_3", ResolveClip(clips, "Idle_3"));
        AddState(stateMachine, "CatSimple_Idle_4", ResolveClip(clips, "Idle_4"));
        AddState(stateMachine, "Bark", ResolveClip(clips, "Idle_2"));
        AddState(stateMachine, "CatSimple_Walk_F_RM", ResolveClip(clips, "Walk_F_RM"));
        AddState(stateMachine, "CatSimple_Trot_F_RM", ResolveClip(clips, "Trot_F_RM"));
        AddState(stateMachine, "CatSimple_Run_F_RM", ResolveClip(clips, "Run_F_RM"));
        AddState(stateMachine, PawPalPetTurnAnimationUtility.TurnLeftStateName, ResolveClip(clips, "Turn_L_IP"));
        AddState(stateMachine, PawPalPetTurnAnimationUtility.TurnRightStateName, ResolveClip(clips, "Turn_R_IP"));
        AddState(stateMachine, "CatSimple_JumpPlace_RM", ResolveClip(clips, "JumpPlace_RM"));
        AddState(stateMachine, "CatSimple_Sit_start", ResolveClip(clips, "Sit_start"));
        AddState(stateMachine, "CatSimple_Sit_loop_1", ResolveClip(clips, "Sit_loop_1"));
        AddState(stateMachine, "CatSimple_Sit_end", ResolveClip(clips, "Sit_end"));
        AddState(stateMachine, "CatSimple_Lie_belly_loop_1", ResolveClip(clips, "Lie_belly_loop_1"));
        AddState(stateMachine, "CatSimple_Scratching", ResolveClip(clips, "Scratching"));
        stateMachine.defaultState = idle1;
    }

    private static void EnsureAnimationSet(RuntimeAnimatorController controller)
    {
        PetAnimationSet animationSet = AssetDatabase.LoadAssetAtPath<PetAnimationSet>(AnimationSetPath);
        if (animationSet == null)
        {
            return;
        }

        bool changed = false;
        if (animationSet.RuntimeController != controller)
        {
            animationSet.RuntimeController = controller;
            changed = true;
        }

        if (!string.Equals(animationSet.IdleStateName, "Idle1", StringComparison.Ordinal))
        {
            animationSet.IdleStateName = "Idle1";
            changed = true;
        }

        if (!string.Equals(animationSet.WalkStateName, "CatSimple_Walk_F_RM", StringComparison.Ordinal))
        {
            animationSet.WalkStateName = "CatSimple_Walk_F_RM";
            changed = true;
        }

        if (!string.Equals(animationSet.SelectedStateName, "Idle2", StringComparison.Ordinal))
        {
            animationSet.SelectedStateName = "Idle2";
            changed = true;
        }

        if (!string.IsNullOrEmpty(animationSet.IdleIndexParameter))
        {
            animationSet.IdleIndexParameter = string.Empty;
            changed = true;
        }

        if (!string.Equals(animationSet.MoveBoolParameter, "Move", StringComparison.Ordinal))
        {
            animationSet.MoveBoolParameter = "Move";
            changed = true;
        }

        if (!string.Equals(animationSet.SpeedFloatParameter, "Speed", StringComparison.Ordinal))
        {
            animationSet.SpeedFloatParameter = "Speed";
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(animationSet);
            AssetDatabase.SaveAssets();
        }
    }

    private static Dictionary<string, AnimationClip> LoadBaseClips()
    {
        PawPalPetAnimationEntry entry = FindEntry(BaseClipEntryKey);
        Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
        if (clips == null || clips.Count == 0)
        {
            throw new InvalidOperationException("Could not load base cat clips for controller rebuild.");
        }

        return clips;
    }

    private static PawPalPetAnimationEntry FindEntry(string canonicalKey)
    {
        foreach (PawPalPetAnimationEntry entry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            if (entry != null && string.Equals(entry.CanonicalKey, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static AnimationClip ResolveClip(Dictionary<string, AnimationClip> clips, string key)
    {
        if (clips == null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        AnimationClip clip;
        if (clips.TryGetValue(key, out clip) && clip != null)
        {
            return clip;
        }

        string canonicalKey = key.Trim().ToLowerInvariant();
        if (clips.TryGetValue(canonicalKey, out clip) && clip != null)
        {
            return clip;
        }

        throw new InvalidOperationException("Missing required cat clip '" + key + "' for controller rebuild.");
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion)
    {
        AnimatorState state = stateMachine.AddState(name);
        state.motion = motion;
        state.speed = 1f;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddParameterIfMissing(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (controller == null || string.IsNullOrEmpty(parameterName) || HasParameter(controller, parameterName, parameterType))
        {
            return;
        }

        controller.AddParameter(parameterName, parameterType);
    }

    private static bool HasParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (controller == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType && string.Equals(parameters[i].name, parameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
