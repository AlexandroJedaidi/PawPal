using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DogAnimatorControllerExpander
{
    private const string MenuPath = "PawFriends/Dogs/Expand Dog Animator Controllers";

    private struct ControllerSpec
    {
        public string ControllerPath;
        public string AnimationAssetPath;
        public string ClipPrefix;
        public string RunSuffix;

        public ControllerSpec(string controllerPath, string animationAssetPath, string clipPrefix, string runSuffix)
        {
            ControllerPath = controllerPath;
            AnimationAssetPath = animationAssetPath;
            ClipPrefix = clipPrefix;
            RunSuffix = runSuffix;
        }
    }

    private struct StateSpec
    {
        public string StateName;
        public string ClipSuffix;
        public bool Loops;

        public StateSpec(string stateName, string clipSuffix, bool loops)
        {
            StateName = stateName;
            ClipSuffix = clipSuffix;
            Loops = loops;
        }
    }

    private static readonly ControllerSpec[] ControllerSpecs =
    {
        new ControllerSpec(
            "Assets/Animations/DogAnimations/CorgiAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Corgi/Dog/FBX/Anim/Corgi_anim_IP.fbx",
            "Arm_Corgi",
            "Run_F_IP"),
        new ControllerSpec(
            "Assets/Animations/DogAnimations/FrenchBulldogAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/FrenchBulldog/Dog/FBX/Anim/FrenchBulldog_anim_IP.fbx",
            "Arm_FrBulldog",
            "Run_F_IP"),
        new ControllerSpec(
            "Assets/Animations/DogAnimations/GoldyiAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/GoldenRetriever/Dog/FBX/Anim/Retriever_anim_IP.fbx",
            "Arm_Retriever",
            "Run_F_IP"),
        new ControllerSpec(
            "Assets/Animations/DogAnimations/HuskyiAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Husky/Dog/FBX/Anim/Husky_anim_IP.fbx",
            "Arm_Husky",
            "Run_F_IP"),
        new ControllerSpec(
            "Assets/Animations/DogAnimations/LabradorPuppyiAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Anim/Puppy_Labrador_anim_RM.fbx",
            "Arm_Labrador",
            "Run_F_RM"),
        new ControllerSpec(
            "Assets/Animations/DogAnimations/ShepherdiAnimController.controller",
            "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Shepherd/Dog/FBX/Anim/Shepherd_anim_IP.fbx",
            "Arm_Shepherd",
            "Run_F_IP")
    };

    private static readonly StateSpec[] CommonStates =
    {
        new StateSpec("Idle1", "Idle_1", true),
        new StateSpec("Idle2", "Idle_2", true),
        new StateSpec("Idle3", "Idle_3", true),
        new StateSpec("Idle4", "Idle_4", true),
        new StateSpec("Idle7", "Idle_7", true),
        new StateSpec("LieBellyStart", "Lie_belly_start", false),
        new StateSpec("LieBellyLoop", "Lie_belly_loop_1", true),
        new StateSpec("LieSleepStart", "Lie_belly_sleep_start", false),
        new StateSpec("LieSleepLoop", "Lie_belly_sleep", true),
        new StateSpec("LieSleepEnd", "Lie_belly_sleep_end", false),
        new StateSpec("LieBellyEnd", "Lie_belly_end", false),
        new StateSpec("EatDrinkStart", "EatDrink_start", false),
        new StateSpec("EatLoop", "Eat_loop", true),
        new StateSpec("DrinkLoop", "Drink_loop", true),
        new StateSpec("EatDrinkEnd", "EatDrink_end", false)
    };

    [MenuItem(MenuPath)]
    public static void ExpandAllControllersFromMenu()
    {
        ExpandAllControllers();
    }

    public static void ExpandAllControllers()
    {
        int changedControllerCount = 0;
        for (int i = 0; i < ControllerSpecs.Length; i++)
        {
            if (ExpandController(ControllerSpecs[i]))
            {
                changedControllerCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("DogAnimatorControllerExpander finished. Controllers changed: " + changedControllerCount + ".");
    }

    private static bool ExpandController(ControllerSpec spec)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning("DogAnimatorControllerExpander could not load controller: " + spec.ControllerPath);
            return false;
        }

        Dictionary<string, AnimationClip> clipsByName = LoadClipsByName(spec.AnimationAssetPath);
        if (clipsByName.Count == 0)
        {
            Debug.LogWarning("DogAnimatorControllerExpander found no clips at: " + spec.AnimationAssetPath);
            return false;
        }

        bool changed = false;
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        changed |= EnsureState(stateMachine, "RunForward", spec.ClipPrefix + "|" + spec.RunSuffix, clipsByName, new Vector3(620f, -70f, 0f));

        for (int i = 0; i < CommonStates.Length; i++)
        {
            StateSpec state = CommonStates[i];
            Vector3 position = new Vector3(620f, 20f + i * 60f, 0f);
            changed |= EnsureState(stateMachine, state.StateName, spec.ClipPrefix + "|" + state.ClipSuffix, clipsByName, position);
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
        }

        return changed;
    }

    private static Dictionary<string, AnimationClip> LoadClipsByName(string assetPath)
    {
        Dictionary<string, AnimationClip> clipsByName = new Dictionary<string, AnimationClip>();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || string.IsNullOrEmpty(clip.name) || clip.name.StartsWith("__preview", System.StringComparison.Ordinal))
            {
                continue;
            }

            clipsByName[clip.name] = clip;
        }

        return clipsByName;
    }

    private static bool EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        string clipName,
        Dictionary<string, AnimationClip> clipsByName,
        Vector3 position)
    {
        AnimatorState existingState = FindStateRecursive(stateMachine, stateName);
        if (existingState != null)
        {
            return false;
        }

        AnimationClip clip;
        if (!clipsByName.TryGetValue(clipName, out clip))
        {
            Debug.LogWarning("DogAnimatorControllerExpander missing clip '" + clipName + "'. State '" + stateName + "' was not added.");
            return false;
        }

        AnimatorState state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return true;
    }

    private static AnimatorState FindStateRecursive(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] childStates = stateMachine.states;
        for (int i = 0; i < childStates.Length; i++)
        {
            AnimatorState state = childStates[i].state;
            if (state != null && state.name == stateName)
            {
                return state;
            }
        }

        ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
        for (int i = 0; i < childStateMachines.Length; i++)
        {
            AnimatorState found = FindStateRecursive(childStateMachines[i].stateMachine, stateName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
