using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PawFriendsFlatFoliageMaterialTools
{
    private const string MenuPath = "PawFriends/Graphics/Create Flat Foliage Material For Selected Renderer";
    private const string ShaderName = "PawFriends/Flat Foliage Cutout";
    private const string GeneratedMaterialFolder = "Assets/Materials/Generated/Foliage";

    [MenuItem(MenuPath)]
    public static void CreateFlatFoliageMaterialForSelectedRenderer()
    {
        if (!TryGetSelectedRenderer(out Renderer renderer))
        {
            Debug.LogWarning("Select a GameObject with a Renderer before creating a flat foliage material.");
            return;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("Could not find shader '" + ShaderName + "'.");
            return;
        }

        string materialFolder = EnsureFolderExists(GeneratedMaterialFolder);
        if (string.IsNullOrEmpty(materialFolder))
        {
            Debug.LogError("Could not create the generated foliage material folder.");
            return;
        }

        Material[] sourceMaterials = renderer.sharedMaterials;
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            sourceMaterials = new[] { null as Material };
        }

        Texture2D fallbackAlphaTexture = FindFallbackAlphaTexture(renderer);
        Material[] generatedMaterials = new Material[sourceMaterials.Length];

        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Texture2D alphaTexture = FindAlphaTextureForMaterial(sourceMaterials[i], fallbackAlphaTexture);
            Material generatedMaterial = new Material(shader);
            generatedMaterial.name = BuildMaterialName(renderer, i, sourceMaterials[i]);
            generatedMaterial.SetColor("_BaseColor", GetSuggestedColor(sourceMaterials[i]));
            generatedMaterial.SetFloat("_Cutoff", 0.333f);

            if (alphaTexture != null)
            {
                generatedMaterial.SetTexture("_CutoffMap", alphaTexture);
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                materialFolder + "/" + SanitizeFileName(generatedMaterial.name) + ".mat");

            AssetDatabase.CreateAsset(generatedMaterial, assetPath);
            generatedMaterials[i] = generatedMaterial;
        }

        Undo.RecordObject(renderer, "Assign Flat Foliage Materials");
        renderer.sharedMaterials = generatedMaterials;
        EditorUtility.SetDirty(renderer);

        if (renderer.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = renderer.gameObject;

        Debug.Log("Created and assigned " + generatedMaterials.Length + " flat foliage material(s) for " + renderer.gameObject.name + ".");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCreateFlatFoliageMaterialForSelectedRenderer()
    {
        return TryGetSelectedRenderer(out _);
    }

    private static bool TryGetSelectedRenderer(out Renderer renderer)
    {
        renderer = null;

        if (Selection.activeGameObject == null)
        {
            return false;
        }

        renderer = Selection.activeGameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            return true;
        }

        renderer = Selection.activeGameObject.GetComponentInChildren<Renderer>(true);
        return renderer != null;
    }

    private static Texture2D FindAlphaTextureForMaterial(Material sourceMaterial, Texture2D fallbackAlphaTexture)
    {
        Texture2D alphaTexture = FindSiblingAlphaTexture(sourceMaterial);
        if (alphaTexture != null)
        {
            return alphaTexture;
        }

        if (sourceMaterial != null)
        {
            alphaTexture = GetTextureProperty(sourceMaterial, "_CutoffMap");
            if (alphaTexture != null)
            {
                return alphaTexture;
            }

            alphaTexture = GetTextureProperty(sourceMaterial, "_BaseMap");
            if (alphaTexture != null)
            {
                return alphaTexture;
            }

            alphaTexture = GetTextureProperty(sourceMaterial, "_MainTex");
            if (alphaTexture != null)
            {
                return alphaTexture;
            }
        }

        return fallbackAlphaTexture;
    }

    private static Texture2D FindSiblingAlphaTexture(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return null;
        }

        Texture2D sourceTexture = GetTextureProperty(sourceMaterial, "_BaseMap");
        if (sourceTexture == null)
        {
            sourceTexture = GetTextureProperty(sourceMaterial, "_MainTex");
        }

        if (sourceTexture == null)
        {
            return null;
        }

        string sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrEmpty(sourceTexturePath))
        {
            return null;
        }

        string folder = Path.GetDirectoryName(sourceTexturePath);
        string fileName = Path.GetFileNameWithoutExtension(sourceTexturePath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        string transName = fileName + "_Trans";
        string[] textureGuids = AssetDatabase.FindAssets(transName + " t:Texture2D", new[] { folder });
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            string candidateName = Path.GetFileNameWithoutExtension(texturePath);
            if (!string.IsNullOrEmpty(candidateName) &&
                candidateName.ToLowerInvariant().Contains(transName.ToLowerInvariant()))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
        }

        return null;
    }

    private static Texture2D FindFallbackAlphaTexture(Renderer renderer)
    {
        string modelAssetPath = GetRendererModelAssetPath(renderer);
        if (string.IsNullOrEmpty(modelAssetPath))
        {
            return null;
        }

        string modelFolder = Path.GetDirectoryName(modelAssetPath);
        if (string.IsNullOrEmpty(modelFolder))
        {
            return null;
        }

        string texturesFolder = Path.Combine(modelFolder, "Textures").Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(texturesFolder))
        {
            return null;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });
        List<string> candidatePaths = new List<string>();
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (fileName.Contains("trans"))
            {
                candidatePaths.Add(path);
            }
        }

        if (candidatePaths.Count == 0)
        {
            return null;
        }

        List<string> tokens = BuildSearchTokens(renderer, modelAssetPath);
        string bestPath = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < candidatePaths.Count; i++)
        {
            string candidatePath = candidatePaths[i];
            string candidateName = Path.GetFileNameWithoutExtension(candidatePath).ToLowerInvariant();
            int score = ScoreCandidate(candidateName, tokens);
            if (score > bestScore)
            {
                bestScore = score;
                bestPath = candidatePath;
            }
        }

        return string.IsNullOrEmpty(bestPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(bestPath);
    }

    private static string GetRendererModelAssetPath(Renderer renderer)
    {
        if (renderer == null)
        {
            return null;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            string meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            if (!string.IsNullOrEmpty(meshPath))
            {
                return meshPath;
            }
        }

        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
        {
            Material material = renderer.sharedMaterials[i];
            if (material == null)
            {
                continue;
            }

            string materialPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialPath))
            {
                return materialPath;
            }
        }

        return null;
    }

    private static List<string> BuildSearchTokens(Renderer renderer, string modelAssetPath)
    {
        List<string> tokens = new List<string>();
        AddTokens(tokens, renderer.gameObject.name);
        AddTokens(tokens, renderer.name);
        AddTokens(tokens, Path.GetFileNameWithoutExtension(modelAssetPath));
        return tokens;
    }

    private static void AddTokens(List<string> tokens, string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        string lowerSource = source.ToLowerInvariant();
        MatchCollection matches = Regex.Matches(lowerSource, @"\d+");
        for (int i = 0; i < matches.Count; i++)
        {
            string token = matches[i].Value;
            if (token.Length >= 2 && !tokens.Contains(token))
            {
                tokens.Add(token);
            }
        }

        string collapsed = Regex.Replace(lowerSource, @"(?:_optimized|lod\d+|group)", string.Empty);
        collapsed = Regex.Replace(collapsed, @"[^a-z0-9]+", string.Empty);
        if (!string.IsNullOrEmpty(collapsed) && !tokens.Contains(collapsed))
        {
            tokens.Add(collapsed);
        }
    }

    private static int ScoreCandidate(string candidateName, List<string> tokens)
    {
        int score = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if (candidateName.Contains(token))
            {
                score += token.Length >= 2 ? 100 : 10;
            }
        }

        if (candidateName.Contains("trans"))
        {
            score += 20;
        }

        return score;
    }

    private static Texture2D GetTextureProperty(Material material, string propertyName)
    {
        if (material == null || !material.HasProperty(propertyName))
        {
            return null;
        }

        return material.GetTexture(propertyName) as Texture2D;
    }

    private static Color GetSuggestedColor(Material sourceMaterial)
    {
        if (sourceMaterial != null)
        {
            if (sourceMaterial.HasProperty("_BaseColor"))
            {
                return sourceMaterial.GetColor("_BaseColor");
            }

            if (sourceMaterial.HasProperty("_Color"))
            {
                return sourceMaterial.GetColor("_Color");
            }
        }

        return new Color(0.58f, 0.64f, 0.58f, 1f);
    }

    private static string BuildMaterialName(Renderer renderer, int materialIndex, Material sourceMaterial)
    {
        string baseName = renderer.gameObject.name;
        if (sourceMaterial != null && !string.IsNullOrEmpty(sourceMaterial.name))
        {
            baseName = sourceMaterial.name;
        }

        if (materialIndex == 0)
        {
            return baseName + "_FlatFoliage";
        }

        return baseName + "_" + materialIndex + "_FlatFoliage";
    }

    private static string EnsureFolderExists(string folderPath)
    {
        string normalizedPath = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return normalizedPath;
        }

        string[] parts = normalizedPath.Split('/');
        if (parts.Length < 2)
        {
            return null;
        }

        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }

        return currentPath;
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            fileName = fileName.Replace(invalidChars[i], '_');
        }

        return fileName;
    }
}
