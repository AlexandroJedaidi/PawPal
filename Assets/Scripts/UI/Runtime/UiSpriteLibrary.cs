using System.Collections.Generic;
using UnityEngine;

public class UiSpriteLibrary : MonoBehaviour
{
    private const float IconTrimAlphaThreshold = 0.02f;
    private const int IconTrimPaddingPixels = 2;

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

        Texture2D texture = LoadTexture(iconName);
        if (texture != null)
        {
            sprite = CreateTrimmedIconSprite(texture, iconName + "_RuntimeSprite");
        }

        if (sprite == null)
        {
            Sprite loadedSprite = Resources.Load<Sprite>("UI/Icons/" + iconName);
            sprite = loadedSprite == null ? null : CreateTrimmedIconSprite(loadedSprite, iconName + "_RuntimeSprite");
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

        sprite = CreateTrimmedIconSprite(generated, iconName + "_WhiteRuntimeSprite");
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

        Texture2D texture = LoadResourceTexture(resourcePath);
        if (texture != null && ShouldTrimResourceSprite(resourcePath))
        {
            sprite = CreateTrimmedIconSprite(texture, resourcePath.Replace('/', '_') + "_RuntimeSprite");
        }

        if (sprite == null)
        {
            Sprite loadedSprite = Resources.Load<Sprite>(resourcePath);
            if (loadedSprite != null && ShouldTrimResourceSprite(resourcePath))
            {
                sprite = CreateTrimmedIconSprite(loadedSprite, resourcePath.Replace('/', '_') + "_RuntimeSprite");
            }
            else
            {
                sprite = loadedSprite;
            }
        }

        if (sprite == null && texture != null)
        {
            sprite = CreateRuntimeSprite(texture, resourcePath.Replace('/', '_'));
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

    private static Sprite CreateTrimmedIconSprite(Texture2D texture, string name)
    {
        if (texture == null)
        {
            return null;
        }

        Rect trimRect = GetOpaqueBounds(texture);
        Sprite sprite = Sprite.Create(texture, trimRect, new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }

    private static Sprite CreateTrimmedIconSprite(Sprite source, string name)
    {
        if (source == null || source.texture == null)
        {
            return null;
        }

        Rect trimRect = GetOpaqueBounds(source.texture, source.textureRect);
        Sprite sprite = Sprite.Create(source.texture, trimRect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        sprite.name = name;
        return sprite;
    }

    private static Rect GetOpaqueBounds(Texture2D source)
    {
        return GetOpaqueBounds(source, new Rect(0f, 0f, source.width, source.height));
    }

    private static Rect GetOpaqueBounds(Texture2D source, Rect searchRect)
    {
        Texture2D readable = CreateReadableCopy(source);
        Color32[] pixels = readable.GetPixels32();
        int searchMinX = Mathf.Clamp(Mathf.FloorToInt(searchRect.xMin), 0, readable.width - 1);
        int searchMinY = Mathf.Clamp(Mathf.FloorToInt(searchRect.yMin), 0, readable.height - 1);
        int searchMaxX = Mathf.Clamp(Mathf.CeilToInt(searchRect.xMax) - 1, 0, readable.width - 1);
        int searchMaxY = Mathf.Clamp(Mathf.CeilToInt(searchRect.yMax) - 1, 0, readable.height - 1);
        int minX = searchMaxX + 1;
        int minY = searchMaxY + 1;
        int maxX = -1;
        int maxY = -1;
        byte alphaThreshold = (byte)Mathf.RoundToInt(IconTrimAlphaThreshold * 255f);

        for (int y = searchMinY; y <= searchMaxY; y++)
        {
            int row = y * readable.width;
            for (int x = searchMinX; x <= searchMaxX; x++)
            {
                if (pixels[row + x].a <= alphaThreshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        Destroy(readable);

        if (maxX < minX || maxY < minY)
        {
            return searchRect;
        }

        minX = Mathf.Max(searchMinX, minX - IconTrimPaddingPixels);
        minY = Mathf.Max(searchMinY, minY - IconTrimPaddingPixels);
        maxX = Mathf.Min(searchMaxX, maxX + IconTrimPaddingPixels);
        maxY = Mathf.Min(searchMaxY, maxY + IconTrimPaddingPixels);
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static bool ShouldTrimResourceSprite(string resourcePath)
    {
        return resourcePath.StartsWith("UI/Figma/HomeMain/icon_", System.StringComparison.Ordinal)
            || resourcePath.StartsWith("UI/Figma/HomeMain/button_", System.StringComparison.Ordinal)
            || resourcePath.StartsWith("UI/Figma/HomeStats/icon_", System.StringComparison.Ordinal);
    }
}
