using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class DogPettingAnimationImportPostprocessor : AssetPostprocessor
{
    private const string PuppyPettingAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Anim/Puppy_Labrador_petting.fbx";
    private const string PuppyModelAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Puppy_Labrador.fbx";
    private const string PuppyPettingTakeName = "Arm_Labrador|Scene";
    private const string PuppyPettingClipName = "Arm_Labrador|Petting";
    private const float PuppyPettingFirstFrame = 1f;
    private const float PuppyPettingLastFrame = 130f;

    static DogPettingAnimationImportPostprocessor()
    {
        if (AssetDatabase.LoadMainAssetAtPath(PuppyPettingAssetPath) != null)
        {
            AssetDatabase.ImportAsset(PuppyPettingAssetPath, ImportAssetOptions.ForceUpdate);
        }
    }

    private void OnPreprocessModel()
    {
        if (!string.Equals(assetPath, PuppyPettingAssetPath, StringComparison.Ordinal))
        {
            return;
        }

        ModelImporter modelImporter = assetImporter as ModelImporter;
        if (modelImporter == null)
        {
            return;
        }

        modelImporter.importAnimation = true;
        modelImporter.animationType = ModelImporterAnimationType.Generic;
        modelImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        modelImporter.resampleCurves = true;
        modelImporter.animationCompression = ModelImporterAnimationCompression.Off;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
        modelImporter.importCameras = false;
        modelImporter.importLights = false;
        modelImporter.importBlendShapes = false;
        modelImporter.clipAnimations = new[] { CreatePettingClip() };

        Avatar sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(PuppyModelAssetPath);
        if (sourceAvatar != null)
        {
            modelImporter.sourceAvatar = sourceAvatar;
        }
        else
        {
            Debug.LogWarning("DogPettingAnimationImportPostprocessor could not find the Labrador puppy avatar at " + PuppyModelAssetPath + ".");
        }
    }

    private static ModelImporterClipAnimation CreatePettingClip()
    {
        return new ModelImporterClipAnimation
        {
            name = PuppyPettingClipName,
            takeName = PuppyPettingTakeName,
            firstFrame = PuppyPettingFirstFrame,
            lastFrame = PuppyPettingLastFrame,
            loopTime = false,
            keepOriginalOrientation = true,
            keepOriginalPositionY = true,
            keepOriginalPositionXZ = true,
            heightFromFeet = false,
            mirror = false
        };
    }
}
