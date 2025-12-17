using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 提供 Deck 主题资源的便捷访问，避免各个 UI 组件自行查找。
/// </summary>
public static class DeckUiThemeCache
{
    private const string BackgroundAssetPath = "Assets/UI/Images/DeckTheme/deckui_background.jpg";
    private const string BorderAssetPath = "Assets/UI/Images/DeckTheme/deckui_border.jpg";
    private const string MagicCircleAssetPath = "Assets/UI/Images/DeckTheme/deckui_magic_circle.jpg";
    private const string CardBackAssetPath = "Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png";
    private const string HighlightAssetPath = "Assets/UI/Images/DeckTheme/deckui_card_highlight.jpg";

    // 紫银主题默认色：深紫背景 + 银色边框 + 紫色高光 + 金色次强调
    private static readonly Color DefaultBackgroundColor = new Color32(34, 18, 52, 255);
    private static readonly Color DefaultBorderColor = new Color32(224, 214, 242, 255);
    private static readonly Color DefaultAccentColor = new Color32(182, 140, 255, 255);
    private static readonly Color DefaultSecondaryAccentColor = new Color32(242, 198, 122, 255);

    private static DeckUiBootstrapper cachedBootstrapper;
    public static event System.Action ThemeChanged;

    private static Sprite fallbackBackground;
    private static Sprite fallbackBorder;
    private static Sprite fallbackMagicCircle;
    private static Sprite fallbackCardBack;
    private static Sprite fallbackHighlight;

    private static DeckUiBootstrapper Bootstrapper
    {
        get
        {
            if (cachedBootstrapper != null)
            {
                return cachedBootstrapper;
            }

            cachedBootstrapper = Object.FindFirstObjectByType<DeckUiBootstrapper>(FindObjectsInactive.Include);
            return cachedBootstrapper;
        }
    }

    public static Sprite BackgroundSprite => CoalesceSprite(Bootstrapper?.BackgroundSprite, ref fallbackBackground, BackgroundAssetPath);
    public static Sprite BorderSprite => CoalesceSprite(Bootstrapper?.BorderSprite, ref fallbackBorder, BorderAssetPath);
    public static Sprite MagicCircleSprite => CoalesceSprite(Bootstrapper?.MagicCircleSprite, ref fallbackMagicCircle, MagicCircleAssetPath);
    public static Sprite CardBackSprite => CoalesceSprite(Bootstrapper?.CardBackSprite, ref fallbackCardBack, CardBackAssetPath);
    public static Sprite HighlightSprite => CoalesceSprite(Bootstrapper?.CardHighlightSprite, ref fallbackHighlight, HighlightAssetPath);
    public static Color BackgroundColor => Bootstrapper != null ? Bootstrapper.BackgroundColor : DefaultBackgroundColor;
    public static Color BorderColor => Bootstrapper != null ? Bootstrapper.BorderColor : DefaultBorderColor;
    public static Color AccentColor => Bootstrapper != null ? Bootstrapper.AccentColor : DefaultAccentColor;
    public static Color SecondaryAccentColor => Bootstrapper != null ? Bootstrapper.SecondaryAccentColor : DefaultSecondaryAccentColor;

    public static void Invalidate(bool notify = false)
    {
        cachedBootstrapper = null;
        if (notify)
        {
            ThemeChanged?.Invoke();
        }
    }

    public static void NotifyThemeChanged()
    {
        Invalidate();
        ThemeChanged?.Invoke();
    }

#if UNITY_EDITOR
    private static Sprite CoalesceSprite(Sprite primary, ref Sprite cache, string assetPath)
    {
        if (primary != null)
        {
            return primary;
        }

        return GetFallbackSprite(ref cache, assetPath);
    }

    private static Sprite GetFallbackSprite(ref Sprite cache, string assetPath)
    {
        if (cache != null)
        {
            return cache;
        }

        cache = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        return cache;
    }
#else
    private static Sprite CoalesceSprite(Sprite primary, ref Sprite cache, string assetPath)
    {
        return primary;
    }

    private static Sprite GetFallbackSprite(ref Sprite cache, string assetPath)
    {
        return null;
    }
#endif
}
