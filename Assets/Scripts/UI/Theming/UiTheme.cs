using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum InventoryCardTheme
{
    Bone,
    Ball,
    Roll,
    Band,
    Loop,
    Charm,
    GetMoreBlue
}

public static class UiTheme
{
    public const float ReferenceWidth = 393f;
    public const float ReferenceHeight = 852f;
    public const float ReferenceNavHeight = 66f;
    public const float ReferenceContentHeight = ReferenceHeight - ReferenceNavHeight;
    public const float MinimumHudVisualScale = 0.9f;
    public const float MaximumHudVisualScale = 1f;

    public static readonly Color32 BackgroundCream = new Color32(251, 246, 232, 255);
    public static readonly Color32 CardWhite = new Color32(255, 255, 255, 255);
    public static readonly Color32 Brand = new Color32(228, 125, 102, 255);
    public static readonly Color32 BrandDark = new Color32(166, 97, 79, 255);
    public static readonly Color32 Divider = new Color32(238, 226, 214, 255);
    public static readonly Color32 BodyText = new Color32(33, 33, 33, 255);
    public static readonly Color32 SubtleText = new Color32(138, 138, 138, 255);
    public static readonly Color32 White = new Color32(255, 255, 255, 255);
    public static readonly Color32 NavBackgroundCream = new Color32(252, 248, 232, 255);
    public static readonly Color32 NavBrand = new Color32(223, 120, 97, 255);
    public static readonly Color32 NavBrandDark = new Color32(138, 75, 60, 255);
    public static readonly Color32 NavShadow = new Color32(219, 194, 189, 255);
    public static readonly Color32 NavTopBorder = new Color32(138, 75, 60, 255);

    private static TMP_FontAsset defaultFont;
    private static TMP_FontAsset navRegularFont;
    private static TMP_FontAsset navMediumFont;
    private static TMP_FontAsset navBoldFont;
    private static TMP_FontAsset navExtraBoldFont;
    private static TMP_FontAsset multilingualFallbackFont;
    private static TMP_FontAsset legacyBaseFont;
    private static Sprite whiteSprite;
    private static Sprite roundedSprite;
    private static Sprite roundedOutlineSprite;
    private static Sprite circleOutlineSprite;
    private static Sprite circleSprite;
    private static Sprite navTabActiveSprite;
    private static Sprite navMapCircleOutlineSprite;
    private static Sprite navTopShadowSprite;
    private static Sprite trainerLevelCardSprite;
    private static Sprite trainerLevelProgressSprite;
    private static Sprite trainerLevelStarSprite;
    private static Sprite topRoundedPanelSprite;
    private static Sprite dogDetailsFieldFillSprite;
    private static Sprite dogDetailsFieldOutlineSprite;
    private static Sprite inventoryNameBarSprite;
    private static Sprite inventorySectionHeaderSprite;
    private static Sprite inventoryItemTitleBarSprite;
    private static Sprite inventoryCardOutlineSprite;
    private static Sprite profileOverviewSprite;
    private static Sprite roundedFiveSprite;
    private static Sprite roundedFiveOutlineSprite;
    private static Sprite roundedTenSprite;
    private static Sprite roundedTenOutlineSprite;
    private static Sprite progressPillSprite;
    private static Sprite inventoryCardBoneSprite;
    private static Sprite inventoryCardBallSprite;
    private static Sprite inventoryCardRollSprite;
    private static Sprite inventoryCardBandSprite;
    private static Sprite inventoryCardLoopSprite;
    private static Sprite inventoryCardCharmSprite;
    private static Sprite inventoryCardGetMoreSprite;

    public static float GetHudVisualScale(float logicalWidth)
    {
        float widthScale = logicalWidth > 0f ? logicalWidth / ReferenceWidth : 1f;
        return Mathf.Clamp(widthScale, MinimumHudVisualScale, MaximumHudVisualScale);
    }

    public static TMP_FontAsset DefaultFont
    {
        get
        {
            if (defaultFont == null)
            {
                defaultFont = LoadStyledFont(
                    "Fonts & Materials/Roboto-Bold SDF",
                    "Fonts & Materials/LiberationSans SDF");
            }

            return defaultFont;
        }
    }

    public static TMP_FontAsset NavRegularFont
    {
        get
        {
            if (navRegularFont == null)
            {
                navRegularFont = LoadStyledFont(
                    "Fonts & Materials/Roboto-Bold SDF",
                    "Fonts & Materials/LiberationSans SDF");
            }

            return navRegularFont != null ? navRegularFont : DefaultFont;
        }
    }

    public static TMP_FontAsset NavExtraBoldFont
    {
        get
        {
            if (navExtraBoldFont == null)
            {
                navExtraBoldFont = LoadStyledFont(
                    "Fonts & Materials/Roboto-Bold SDF",
                    "Fonts & Materials/LiberationSans SDF");
            }

            return navExtraBoldFont != null ? navExtraBoldFont : NavRegularFont;
        }
    }

    public static TMP_FontAsset NavMediumFont
    {
        get
        {
            if (navMediumFont == null)
            {
                navMediumFont = LoadStyledFont(
                    "Fonts & Materials/Roboto-Bold SDF",
                    "Fonts & Materials/LiberationSans SDF");
            }

            return navMediumFont != null ? navMediumFont : NavRegularFont;
        }
    }

    public static TMP_FontAsset NavBoldFont
    {
        get
        {
            if (navBoldFont == null)
            {
                navBoldFont = LoadStyledFont(
                    "Fonts & Materials/Roboto-Bold SDF",
                    "Fonts & Materials/LiberationSans SDF");
            }

            return navBoldFont != null ? navBoldFont : NavExtraBoldFont;
        }
    }

    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                whiteSprite = BuildSprite("UiTheme_White", 2, 2, delegate { return White; });
            }

            return whiteSprite;
        }
    }

    public static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite == null)
            {
                roundedSprite = BuildRoundedRectSprite("UiTheme_Rounded", 64, 14, 0f, true);
            }

            return roundedSprite;
        }
    }

    public static Sprite RoundedOutlineSprite
    {
        get
        {
            if (roundedOutlineSprite == null)
            {
                roundedOutlineSprite = BuildRoundedRectSprite("UiTheme_RoundedOutline", 64, 14, 2.5f, false);
            }

            return roundedOutlineSprite;
        }
    }

    public static Sprite CircleOutlineSprite
    {
        get
        {
            if (circleOutlineSprite == null)
            {
                circleOutlineSprite = BuildCircleSprite("UiTheme_CircleOutline", 72, 2.5f, false);
            }

            return circleOutlineSprite;
        }
    }

    public static Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null)
            {
                circleSprite = BuildCircleSprite("UiTheme_CircleFill", 72, 0f, true);
            }

            return circleSprite;
        }
    }

    public static Sprite NavTabActiveSprite
    {
        get
        {
            if (navTabActiveSprite == null)
            {
                navTabActiveSprite = BuildBottomRoundedSprite("UiTheme_NavTabActive", 120, 104, 14f);
            }

            return navTabActiveSprite;
        }
    }

    public static Sprite NavMapCircleOutlineSprite
    {
        get
        {
            if (navMapCircleOutlineSprite == null)
            {
                navMapCircleOutlineSprite = BuildCircleSprite("UiTheme_NavCircleOutline", 120, 2f, false);
            }

            return navMapCircleOutlineSprite;
        }
    }

    public static Sprite NavTopShadowSprite
    {
        get
        {
            if (navTopShadowSprite == null)
            {
                navTopShadowSprite = BuildVerticalGradientSprite("UiTheme_NavTopShadow", 2, 12, 0.32f, 0f);
            }

            return navTopShadowSprite;
        }
    }

    public static Sprite TrainerLevelCardSprite
    {
        get
        {
            if (trainerLevelCardSprite == null)
            {
                trainerLevelCardSprite = BuildBottomRoundedSprite("UiTheme_TrainerLevelCard", 175, 76, 20f);
            }

            return trainerLevelCardSprite;
        }
    }

    public static Sprite TrainerLevelProgressSprite
    {
        get
        {
            if (trainerLevelProgressSprite == null)
            {
                trainerLevelProgressSprite = BuildHorizontalGradientSprite(
                    "UiTheme_TrainerLevelProgress",
                    97,
                    11,
                    NavBrand,
                    new Color32(255, 255, 255, 255));
            }

            return trainerLevelProgressSprite;
        }
    }

    public static Sprite TrainerLevelStarSprite
    {
        get
        {
            if (trainerLevelStarSprite == null)
            {
                trainerLevelStarSprite = BuildStarSprite("UiTheme_TrainerLevelStar", 38, 34, 5, 0.48f);
            }

            return trainerLevelStarSprite;
        }
    }

    public static Sprite TopRoundedPanelSprite
    {
        get
        {
            if (topRoundedPanelSprite == null)
            {
                topRoundedPanelSprite = BuildTopRoundedSprite("UiTheme_TopRoundedPanel", 246, 117, 10f);
            }

            return topRoundedPanelSprite;
        }
    }

    public static Sprite DogDetailsFieldFillSprite
    {
        get
        {
            if (dogDetailsFieldFillSprite == null)
            {
                dogDetailsFieldFillSprite = BuildRoundedRectSprite("UiTheme_DogDetailsFieldFill", 182, 24, 10f, 0f, true);
            }

            return dogDetailsFieldFillSprite;
        }
    }

    public static Sprite DogDetailsFieldOutlineSprite
    {
        get
        {
            if (dogDetailsFieldOutlineSprite == null)
            {
                dogDetailsFieldOutlineSprite = BuildRoundedRectSprite("UiTheme_DogDetailsFieldOutline", 182, 24, 10f, 1.5f, false);
            }

            return dogDetailsFieldOutlineSprite;
        }
    }

    public static Sprite InventoryNameBarSprite
    {
        get
        {
            if (inventoryNameBarSprite == null)
            {
                inventoryNameBarSprite = BuildTopRoundedSprite("UiTheme_InventoryNameBar", 255, 44, 10f);
            }

            return inventoryNameBarSprite;
        }
    }

    public static Sprite InventorySectionHeaderSprite
    {
        get
        {
            if (inventorySectionHeaderSprite == null)
            {
                inventorySectionHeaderSprite = BuildRoundedRectSprite("UiTheme_InventorySectionHeader", 209, 24, 5f, 0f, true);
            }

            return inventorySectionHeaderSprite;
        }
    }

    public static Sprite InventoryItemTitleBarSprite
    {
        get
        {
            if (inventoryItemTitleBarSprite == null)
            {
                inventoryItemTitleBarSprite = BuildTopRoundedSprite("UiTheme_InventoryItemTitle", 92, 15, 10f);
            }

            return inventoryItemTitleBarSprite;
        }
    }

    public static Sprite InventoryCardOutlineSprite
    {
        get
        {
            if (inventoryCardOutlineSprite == null)
            {
                inventoryCardOutlineSprite = BuildRoundedRectSprite("UiTheme_InventoryCardOutline", 92, 92, 10f, 2f, false);
            }

            return inventoryCardOutlineSprite;
        }
    }

    public static Sprite ProfileOverviewSprite
    {
        get
        {
            if (profileOverviewSprite == null)
            {
                profileOverviewSprite = BuildBottomRoundedSprite("UiTheme_ProfileOverview", 379, 786, 10f);
            }

            return profileOverviewSprite;
        }
    }

    public static Sprite RoundedFiveSprite
    {
        get
        {
            if (roundedFiveSprite == null)
            {
                roundedFiveSprite = BuildRoundedRectSprite("UiTheme_RoundedFive", 32, 16, 5f, 0f, true);
            }

            return roundedFiveSprite;
        }
    }

    public static Sprite RoundedFiveOutlineSprite
    {
        get
        {
            if (roundedFiveOutlineSprite == null)
            {
                roundedFiveOutlineSprite = BuildRoundedRectSprite("UiTheme_RoundedFiveOutline", 32, 16, 5f, 1.5f, false);
            }

            return roundedFiveOutlineSprite;
        }
    }

    public static Sprite RoundedTenOutlineSprite
    {
        get
        {
            if (roundedTenOutlineSprite == null)
            {
                roundedTenOutlineSprite = BuildRoundedRectSprite("UiTheme_RoundedTenOutline", 32, 16, 10f, 1.5f, false);
            }

            return roundedTenOutlineSprite;
        }
    }

    public static Sprite RoundedTenSprite
    {
        get
        {
            if (roundedTenSprite == null)
            {
                roundedTenSprite = BuildRoundedRectSprite("UiTheme_RoundedTen", 32, 16, 10f, 0f, true);
            }

            return roundedTenSprite;
        }
    }

    public static Sprite ProgressPillSprite
    {
        get
        {
            if (progressPillSprite == null)
            {
                progressPillSprite = BuildRoundedRectSprite("UiTheme_ProgressPill", 32, 14, 7f, 0f, true);
            }

            return progressPillSprite;
        }
    }

    public static Sprite GetInventoryCardSprite(InventoryCardTheme theme)
    {
        switch (theme)
        {
            case InventoryCardTheme.Bone:
                if (inventoryCardBoneSprite == null)
                {
                    inventoryCardBoneSprite = BuildInventoryCardSprite("UiTheme_InventoryBone", new Color32(232, 187, 204, 129), new Color32(197, 101, 133, 255), false);
                }
                return inventoryCardBoneSprite;
            case InventoryCardTheme.Ball:
                if (inventoryCardBallSprite == null)
                {
                    inventoryCardBallSprite = BuildInventoryCardSprite("UiTheme_InventoryBall", new Color32(222, 226, 159, 129), new Color32(168, 176, 31, 255), false);
                }
                return inventoryCardBallSprite;
            case InventoryCardTheme.Roll:
                if (inventoryCardRollSprite == null)
                {
                    inventoryCardRollSprite = BuildInventoryCardSprite("UiTheme_InventoryRoll", new Color32(168, 184, 223, 129), new Color32(43, 99, 159, 255), false);
                }
                return inventoryCardRollSprite;
            case InventoryCardTheme.Band:
                if (inventoryCardBandSprite == null)
                {
                    inventoryCardBandSprite = BuildInventoryCardSprite("UiTheme_InventoryBand", new Color32(156, 169, 194, 129), new Color32(62, 90, 142, 255), false);
                }
                return inventoryCardBandSprite;
            case InventoryCardTheme.Loop:
                if (inventoryCardLoopSprite == null)
                {
                    inventoryCardLoopSprite = BuildInventoryCardSprite("UiTheme_InventoryLoop", new Color32(227, 210, 181, 129), new Color32(203, 171, 116, 255), false);
                }
                return inventoryCardLoopSprite;
            case InventoryCardTheme.Charm:
                if (inventoryCardCharmSprite == null)
                {
                    inventoryCardCharmSprite = BuildInventoryCardSprite("UiTheme_InventoryCharm", new Color32(197, 155, 154, 129), new Color32(144, 62, 62, 255), false);
                }
                return inventoryCardCharmSprite;
            default:
                if (inventoryCardGetMoreSprite == null)
                {
                    inventoryCardGetMoreSprite = BuildHorizontalGradientSprite("UiTheme_InventoryGetMore", 92, 92, new Color32(255, 255, 255, 255), new Color32(50, 187, 255, 255));
                }
                return inventoryCardGetMoreSprite;
        }
    }

    private static Sprite BuildRoundedRectSprite(string name, int size, float radius, float border, bool fill)
    {
        return BuildRoundedRectSprite(name, size, size, radius, border, fill);
    }

    private static Sprite BuildRoundedRectSprite(string name, int width, int height, float radius, float border, bool fill)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float clampedX = Mathf.Clamp(x, radius, width - radius - 1f);
                float clampedY = Mathf.Clamp(y, radius, height - radius - 1f);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, clampedY));
                bool inside = distance <= radius;
                bool borderPixel = border > 0f && inside && distance >= radius - border;

                Color32 pixel = new Color32(255, 255, 255, 0);
                if (fill && inside)
                {
                    pixel = White;
                }
                else if (!fill && borderPixel)
                {
                    pixel = White;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        Vector4 borderVector = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, borderVector);
    }

    private static Sprite BuildCircleSprite(string name, int size, float border, bool fill)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1f) * 0.5f, (size - 1f) * 0.5f);
        float radius = (size - 2f) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool inside = distance <= radius;
                bool borderPixel = border > 0f && inside && distance >= radius - border;
                Color32 pixel = new Color32(255, 255, 255, 0);
                if (fill && inside)
                {
                    pixel = White;
                }
                else if (!fill && borderPixel)
                {
                    pixel = White;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildBottomRoundedSprite(string name, int width, int height, float bottomRadius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;

                if (x < bottomRadius && y < bottomRadius)
                {
                    Vector2 center = new Vector2(bottomRadius - 1f, bottomRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= bottomRadius;
                }
                else if (x >= width - bottomRadius && y < bottomRadius)
                {
                    Vector2 center = new Vector2(width - bottomRadius, bottomRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= bottomRadius;
                }

                texture.SetPixel(x, y, inside ? White : new Color32(255, 255, 255, 0));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildTopRoundedSprite(string name, int width, int height, float topRadius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;
                int topY = height - 1 - y;

                if (x < topRadius && topY < topRadius)
                {
                    Vector2 center = new Vector2(topRadius - 1f, topRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, topY), center) <= topRadius;
                }
                else if (x >= width - topRadius && topY < topRadius)
                {
                    Vector2 center = new Vector2(width - topRadius, topRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, topY), center) <= topRadius;
                }

                texture.SetPixel(x, y, inside ? White : new Color32(255, 255, 255, 0));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildVerticalGradientSprite(string name, int width, int height, float topAlpha, float bottomAlpha)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            float t = height <= 1 ? 0f : (float)y / (height - 1f);
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(bottomAlpha, topAlpha, t) * 255f);
            Color32 pixel = new Color32(255, 255, 255, alpha);
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
    }

    private static Sprite BuildHorizontalGradientSprite(string name, int width, int height, Color32 leftColor, Color32 rightColor)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
        {
            float t = width <= 1 ? 0f : (float)x / (width - 1f);
            Color pixel = Color.Lerp(leftColor, rightColor, t);
            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite BuildInventoryCardSprite(string name, Color32 edgeTint, Color32 shadowColor, bool border)
    {
        int width = 92;
        int height = 92;
        float radius = 10f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(34.286f, 58.571f);
        float maxDistance = 46f;
        Color clear = new Color32(255, 255, 255, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float clampedX = Mathf.Clamp(x, radius, width - radius - 1f);
                float clampedY = Mathf.Clamp(y, radius, height - radius - 1f);
                float cornerDistance = Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, clampedY));
                bool inside = cornerDistance <= radius;
                if (!inside)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                Color pixel = Color.Lerp(new Color32(250, 248, 245, 255), edgeTint, Mathf.Clamp01(distance));
                if (border && (x < 2 || y < 2 || x > width - 3 || y > height - 3))
                {
                    pixel = NavBrand;
                }

                if (!border && (x == 0 || y == 0 || x == width - 1 || y == height - 1))
                {
                    pixel = shadowColor;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Sprite BuildStarSprite(string name, int width, int height, int points, float innerRadiusRatio)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((width - 1f) * 0.5f, (height - 1f) * 0.5f);
        float outerRadius = Mathf.Min(width, height) * 0.5f - 2f;
        float innerRadius = outerRadius * innerRadiusRatio;
        Vector2[] polygon = new Vector2[points * 2];
        float startAngle = -90f * Mathf.Deg2Rad;
        for (int i = 0; i < polygon.Length; i++)
        {
            float angle = startAngle + i * Mathf.PI / points;
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            polygon[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        Color32 transparent = new Color32(255, 255, 255, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), polygon) ? White : transparent);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool intersects = ((a.y > point.y) != (b.y > point.y)) &&
                              (point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(b.y - a.y, 0.0001f) + a.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static Sprite BuildSprite(string name, int width, int height, System.Func<Color32> pixelFactory)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, pixelFactory());
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static TMP_FontAsset LoadFirstFont(params string[] resourcePaths)
    {
        for (int i = 0; i < resourcePaths.Length; i++)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(resourcePaths[i]);
            if (font != null)
            {
                return font;
            }
        }

        return null;
    }

    private static TMP_FontAsset LoadStyledFont(params string[] resourcePaths)
    {
        TMP_FontAsset font = LoadFirstFont(resourcePaths);
        AttachFallbackFonts(font);
        return font;
    }

    private static void AttachFallbackFonts(TMP_FontAsset font)
    {
        if (font == null)
        {
            return;
        }

        TMP_FontAsset multilingualFallback = GetMultilingualFallbackFont();
        TMP_FontAsset baseFallback = GetLegacyBaseFont();

        if (font.fallbackFontAssetTable == null)
        {
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        TryAddFallbackFont(font, multilingualFallback);
        TryAddFallbackFont(font, baseFallback);
    }

    private static TMP_FontAsset GetMultilingualFallbackFont()
    {
        if (multilingualFallbackFont == null)
        {
            multilingualFallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        }

        return multilingualFallbackFont;
    }

    private static TMP_FontAsset GetLegacyBaseFont()
    {
        if (legacyBaseFont == null)
        {
            legacyBaseFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        return legacyBaseFont;
    }

    private static void TryAddFallbackFont(TMP_FontAsset font, TMP_FontAsset fallback)
    {
        if (font == null || fallback == null || font == fallback)
        {
            return;
        }

        List<TMP_FontAsset> table = font.fallbackFontAssetTable;
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i] == fallback)
            {
                return;
            }
        }

        table.Add(fallback);
    }
}
