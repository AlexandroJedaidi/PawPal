using System.Collections.Generic;
using UnityEngine;

public class UiSpriteLibrary : MonoBehaviour
{
    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, Sprite> whiteIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Sprite> resourceSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, Texture2D> resourceTextureCache = new Dictionary<string, Texture2D>();

    public Sprite GetIcon(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return UiTheme.WhiteSprite;
        }

        Sprite sprite;
        if (cache.TryGetValue(iconName, out sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>("UI/Icons/" + iconName);
        if (sprite == null)
        {
            Texture2D texture = LoadTexture(iconName);
            if (texture != null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = iconName + "_RuntimeSprite";
            }
        }

        if (sprite == null)
        {
            Debug.LogWarning("UiSpriteLibrary missing icon: " + iconName + ". Add it to Assets/Resources/UI/Icons/.");
            sprite = UiTheme.WhiteSprite;
        }

        cache[iconName] = sprite;
        return sprite;
    }

    public Sprite GetWhiteIcon(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return UiTheme.WhiteSprite;
        }

        Sprite sprite;
        if (whiteIconCache.TryGetValue(iconName, out sprite))
        {
            return sprite;
        }

        Texture2D source = LoadTexture(iconName);
        if (source == null)
        {
            sprite = GetIcon(iconName);
            whiteIconCache[iconName] = sprite;
            return sprite;
        }

        Texture2D readableSource = CreateReadableCopy(source);
        Texture2D generated = new Texture2D(readableSource.width, readableSource.height, TextureFormat.ARGB32, false);
        generated.name = iconName + "_WhiteMask";
        generated.filterMode = FilterMode.Bilinear;
        generated.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = readableSource.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = pixels[i].a;
            pixels[i] = new Color(1f, 1f, 1f, alpha);
        }

        generated.SetPixels(pixels);
        generated.Apply();

        sprite = Sprite.Create(
            generated,
            new Rect(0f, 0f, generated.width, generated.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = iconName + "_WhiteRuntimeSprite";
        whiteIconCache[iconName] = sprite;
        return sprite;
    }

    public Sprite GetResourceSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return UiTheme.WhiteSprite;
        }

        Sprite sprite;
        if (resourceSpriteCache.TryGetValue(resourcePath, out sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = LoadResourceTexture(resourcePath);
            if (texture != null)
            {
                sprite = CreateRuntimeSprite(texture, resourcePath.Replace('/', '_'));
            }
        }

        if (sprite == null)
        {
            Debug.LogWarning("UiSpriteLibrary missing resource sprite: " + resourcePath + ".");
            sprite = UiTheme.WhiteSprite;
        }

        resourceSpriteCache[resourcePath] = sprite;
        return sprite;
    }

    private static Texture2D CreateReadableCopy(Texture source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
        readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return readable;
    }

    private Texture2D LoadTexture(string iconName)
    {
        Texture2D texture;
        if (textureCache.TryGetValue(iconName, out texture))
        {
            return texture;
        }

        texture = Resources.Load<Texture2D>("UI/Icons/" + iconName);
        textureCache[iconName] = texture;
        return texture;
    }

    private Texture2D LoadResourceTexture(string resourcePath)
    {
        Texture2D texture;
        if (resourceTextureCache.TryGetValue(resourcePath, out texture))
        {
            return texture;
        }

        texture = Resources.Load<Texture2D>(resourcePath);
        resourceTextureCache[resourcePath] = texture;
        return texture;
    }

    private static Sprite CreateRuntimeSprite(Texture2D texture, string name)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = name + "_RuntimeSprite";
        return sprite;
    }
}
