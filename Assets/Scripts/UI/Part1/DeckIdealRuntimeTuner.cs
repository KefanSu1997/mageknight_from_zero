using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class DeckIdealRuntimeTuner
{
    private const string TargetSceneName = "Part1_DeckIdeal";
    private const float NebulaAlpha = 0.015f;
    private const float MagicCircleAlpha = 0.22f;
    private const float CardGlowAlpha = 0.18f;
    private const float FiligreeInset = 52f;
    private const float BottomBandAlpha = 0f;

    private static readonly Vector2 DeckAnchor = new(0.14f, 0.82f);
    private static readonly Vector2 DiscardAnchor = new(0.86f, 0.82f);
    private static readonly Color FrameTint = new(0.92f, 0.9f, 1f, 0.7f);
    private static readonly Color FrameShadowTint = new(0.03f, 0.01f, 0.06f, 0.14f);
    private static readonly Color FiligreeTint = new(0.95f, 0.93f, 1f, 0.26f);
    private static readonly Color GemTint = new(0.94f, 0.96f, 1f, 0.72f);
    private static readonly Color TopCreatureGlowTint = new(1f, 0.9f, 0.7f, 0.12f);
    private static readonly Color CardFiligreeTint = new(1f, 0.88f, 0.62f, 0.28f);

    public static void ApplyIfInIdealScene(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.name != TargetSceneName)
        {
            return;
        }

        ApplyOverlayTint(root, "NebulaOverlay", DeckUiThemeCache.BackgroundColor, NebulaAlpha);
        ApplyOverlayTint(root, "MagicCircleOverlay", DeckUiThemeCache.SecondaryAccentColor, MagicCircleAlpha);
        AdjustAnchor(root, "DeckStackOverlay", DeckAnchor);
        AdjustAnchor(root, "DiscardStackOverlay", DiscardAnchor);
        HideBaseFrame(root);
        BoostCardGlows(root);
        EnsureFrameLayers(root);
        EnsureCornerGems(root);
        EnsureTopCreature(root);
        EnsureMagicAura(root);
        EnsureCenterParticles(root);
        EnsureStackGlowAndAlpha(root);
        EnsureBottomBands(root);
        EnsureBottomLayout(root);
        EnsureHandFiligree(root);
    }

    private static void ApplyOverlayTint(RectTransform root, string name, Color tint, float alpha)
    {
        var image = FindImage(root, name);
        if (image == null)
        {
            return;
        }

        image.color = new Color(tint.r, tint.g, tint.b, alpha);
        AttachBreathing(image.transform, 0.012f);
    }

    private static void AdjustAnchor(RectTransform root, string name, Vector2 anchor)
    {
        var target = root.Find(name) as RectTransform;
        if (target == null)
        {
            return;
        }

        target.anchorMin = anchor;
        target.anchorMax = anchor;
        target.anchoredPosition = Vector2.zero;
    }

    private static void BoostCardGlows(RectTransform root)
    {
        var images = root.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (!image.gameObject.name.StartsWith("CardFaceGlow"))
            {
                continue;
            }

            var color = image.color;
            color.a = CardGlowAlpha;
            image.color = color;
            AttachGlowPulse(image.transform, 1.1f, 0.1f, 0.36f);
        }
    }

    private static void EnsureCenterParticles(RectTransform root)
    {
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (highlight == null)
        {
            return;
        }

        var rect = root.Find("CenterParticleGlow") as RectTransform;
        if (rect == null)
        {
            var go = new GameObject("CenterParticleGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1040f, 1040f);
        }

        rect.localRotation = Quaternion.Euler(0f, 0f, 8f);
        var image = rect.GetComponent<Image>();
        image.sprite = highlight;
        var accent = DeckUiThemeCache.SecondaryAccentColor;
        image.color = new Color(accent.r, accent.g, accent.b, 0.08f);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        AttachSlowRotate(rect, 4.5f);
        AttachBreathing(rect, 0.01f);

        rect.SetSiblingIndex(3);

        var foreground = root.Find("ForegroundSparkle");
        if (foreground != null)
        {
            rect.SetSiblingIndex(foreground.GetSiblingIndex());
        }
        else
        {
            rect.SetAsLastSibling();
        }
    }

    private static void EnsureMagicAura(RectTransform root)
    {
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (highlight == null)
        {
            return;
        }

        var auraRect = root.Find("MagicAura") as RectTransform;
        if (auraRect == null)
        {
            var go = new GameObject("MagicAura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            auraRect = go.GetComponent<RectTransform>();
            auraRect.SetParent(root, false);
            auraRect.anchorMin = new Vector2(0.5f, 0.5f);
            auraRect.anchorMax = new Vector2(0.5f, 0.5f);
            auraRect.pivot = new Vector2(0.5f, 0.5f);
            auraRect.localRotation = Quaternion.Euler(0f, 0f, -6f);
        }

        auraRect.sizeDelta = new Vector2(1160f, 1160f);
        var image = auraRect.GetComponent<Image>();
        image.sprite = highlight;
        var accent = DeckUiThemeCache.AccentColor;
        image.color = new Color(accent.r, accent.g, accent.b, 0.06f);
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;

        AttachBreathing(auraRect, 0.015f);
        AttachSlowRotate(auraRect, -3.5f);
        auraRect.SetSiblingIndex(2);
    }

    private static void EnsureCornerGems(RectTransform root)
    {
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (highlight == null)
        {
            return;
        }

        var anchors = new[]
        {
            (name: "Gem_TopLeft", anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f), pos: new Vector2(96f, -96f), rot: -18f),
            (name: "Gem_TopRight", anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f), pos: new Vector2(-96f, -96f), rot: 18f),
            (name: "Gem_BottomLeft", anchor: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f), pos: new Vector2(96f, 96f), rot: 12f),
            (name: "Gem_BottomRight", anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f), pos: new Vector2(-96f, 96f), rot: -12f)
        };

        foreach (var entry in anchors)
        {
            var rect = root.Find(entry.name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(entry.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(root, false);
            }

            rect.anchorMin = entry.anchor;
            rect.anchorMax = entry.anchor;
            rect.pivot = entry.pivot;
            rect.anchoredPosition = entry.pos;
            rect.localRotation = Quaternion.Euler(0f, 0f, entry.rot);
            rect.sizeDelta = new Vector2(180f, 180f);

            var image = rect.GetComponent<Image>();
            image.sprite = highlight;
            image.color = GemTint;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            AttachGlowPulse(rect, 1.1f, 0.12f, 0.38f);
            rect.SetAsLastSibling();
        }
    }

    private static void EnsureTopCreature(RectTransform root)
    {
        var top = root.Find("TopCreature") as RectTransform;
        if (top == null)
        {
            var card = LoadCardSprite("Assets/GameData/cards/monster_ideal_wraith.png") ??
                       LoadCardSprite("Assets/GameData/cards/monster (20).jpg") ??
                       LoadCardSprite("Assets/GameData/cards/monster (65).jpg") ??
                       LoadCardSprite("Assets/GameData/cards/monster (52).jpg") ??
                       LoadCardSprite("Assets/GameData/cards/monster (3).jpg");
            if (card == null)
            {
                return;
            }

            top = new GameObject("TopCreature", typeof(RectTransform)).GetComponent<RectTransform>();
            top.SetParent(root, false);
            top.anchorMin = new Vector2(0.5f, 1f);
            top.anchorMax = new Vector2(0.5f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.anchoredPosition = new Vector2(0f, -32f);
            top.sizeDelta = new Vector2(720f, 420f);

            var art = top.gameObject.AddComponent<Image>();
            art.sprite = card;
            art.preserveAspect = true;
            art.raycastTarget = false;
        }

        AttachSineFloat(top, new Vector2(6f, 10f), new Vector2(0.06f, 0.05f));
        EnsureTopCreatureGlow(top);
        top.SetAsLastSibling();
    }

    private static void EnsureTopCreatureGlow(RectTransform top)
    {
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (highlight == null)
        {
            return;
        }

        var glowRect = top.Find("TopCreatureGlow") as RectTransform;
        if (glowRect == null)
        {
            var go = new GameObject("TopCreatureGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowRect = go.GetComponent<RectTransform>();
            glowRect.SetParent(top, false);
        }

        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = new Vector2(700f, 400f);
        glowRect.localRotation = Quaternion.identity;

        var image = glowRect.GetComponent<Image>();
        image.sprite = highlight;
        image.color = TopCreatureGlowTint;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        glowRect.SetAsFirstSibling();

        AttachGlowPulse(glowRect, 1.1f, 0.12f, 0.32f);
        AttachBreathing(glowRect, 0.018f);
    }

    private static void EnsureFrameLayers(RectTransform root)
    {
        var border = DeckUiThemeCache.BorderSprite ?? DeckUiThemeCache.HighlightSprite;
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (border == null || highlight == null)
        {
            return;
        }

        var frame = root.Find("SilverFrameOverlay") as RectTransform;
        if (frame == null)
        {
            var go = new GameObject("SilverFrameOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frame = go.GetComponent<RectTransform>();
            frame.SetParent(root, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(36f, 36f);
            frame.offsetMax = new Vector2(-36f, -36f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = border;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
        }

        var frameImg = frame.GetComponent<Image>();
        frameImg.color = FrameTint;

        var shadow = root.Find("SilverFrameShadow") as RectTransform;
        if (shadow == null)
        {
            var go = new GameObject("SilverFrameShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadow = go.GetComponent<RectTransform>();
            shadow.SetParent(root, false);
            shadow.anchorMin = Vector2.zero;
            shadow.anchorMax = Vector2.one;
            shadow.offsetMin = new Vector2(48f, 48f);
            shadow.offsetMax = new Vector2(-48f, -48f);
            shadow.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = border;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
        }

        var shadowImg = shadow.GetComponent<Image>();
        shadowImg.color = FrameShadowTint;

        EnsureFiligree(root, "FrameFiligree", FiligreeInset, FiligreeTint, 0.01f, true);
        EnsureFiligree(root, "FrameFiligreeInner", FiligreeInset + 18f, new Color(FiligreeTint.r, FiligreeTint.g, FiligreeTint.b, 0.18f), 0.014f, false, 4f);
    }

    private static void EnsureFiligree(RectTransform root, string name, float inset, Color color, float scaleAmplitude, bool addGlow, float rotationSpeed = 0f)
    {
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (highlight == null)
        {
            return;
        }

        var filigree = root.Find(name) as RectTransform;
        if (filigree == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            filigree = go.GetComponent<RectTransform>();
            filigree.SetParent(root, false);
            filigree.anchorMin = Vector2.zero;
            filigree.anchorMax = Vector2.one;
            filigree.pivot = new Vector2(0.5f, 0.5f);
        }

        filigree.offsetMin = new Vector2(inset, inset);
        filigree.offsetMax = new Vector2(-inset, -inset);
        var image = filigree.GetComponent<Image>();
        image.sprite = highlight;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        image.color = color;

        if (addGlow)
        {
            AttachGlowPulse(filigree, 1.1f, 0.14f, 0.7f);
        }

        AttachBreathing(filigree, scaleAmplitude);
        if (Mathf.Abs(rotationSpeed) > 0.01f)
        {
            AttachSlowRotate(filigree, rotationSpeed);
        }
    }

    private static void EnsureStackGlowAndAlpha(RectTransform root)
    {
        var deckOverlay = root.Find("DeckStackOverlay") as RectTransform;
        var discardOverlay = root.Find("DiscardStackOverlay") as RectTransform;

        if (deckOverlay != null)
        {
            AttachSineFloat(deckOverlay, new Vector2(8f, 6f), new Vector2(0.12f, 0.14f));
        }

        if (discardOverlay != null)
        {
            AttachSineFloat(discardOverlay, new Vector2(10f, 7f), new Vector2(0.1f, 0.12f));
            var group = discardOverlay.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = discardOverlay.gameObject.AddComponent<CanvasGroup>();
                if (group == null)
                {
                    Debug.LogWarning("[DeckIdealRuntimeTuner] DiscardStackOverlay missing CanvasGroup and could not be created.");
                    return;
                }
            }

            group.alpha = 0.6f;
        }
    }

    private static void EnsureBottomBands(RectTransform root)
    {
        var bottom = root.Find("BottomSection") as RectTransform;
        if (bottom == null)
        {
            return;
        }

        var highlight = DeckUiThemeCache.HighlightSprite;
        var border = DeckUiThemeCache.BorderSprite;

        var boardBand = bottom.Find("BoardBand") as RectTransform ?? NewBand("BoardBand", bottom, highlight);
        ConfigureBand(boardBand, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(6f, 4f), new Vector2(-6f, -2f),
            new Color(DeckUiThemeCache.BackgroundColor.r * 0.7f, DeckUiThemeCache.BackgroundColor.g * 0.7f, DeckUiThemeCache.BackgroundColor.b * 0.7f, BottomBandAlpha));

        var handBand = bottom.Find("HandBand") as RectTransform ?? NewBand("HandBand", bottom, border);
        ConfigureBand(handBand, new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Vector2(6f, 2f), new Vector2(-6f, -2f),
            new Color(DeckUiThemeCache.SecondaryAccentColor.r, DeckUiThemeCache.SecondaryAccentColor.g, DeckUiThemeCache.SecondaryAccentColor.b, BottomBandAlpha * 0.9f));
    }

    private static RectTransform NewBand(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return rect;
    }

    private static void ConfigureBand(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var image = rect.GetComponent<Image>();
        image.color = color;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.SetAsFirstSibling();
    }

    private static void EnsureBottomLayout(RectTransform root)
    {
        var bottom = root.Find("BottomSection") as RectTransform;
        if (bottom != null)
        {
            bottom.anchoredPosition = new Vector2(0f, 20f);
            bottom.sizeDelta = new Vector2(1920f * 0.9f, bottom.sizeDelta.y);
        }

        var boardRoot = FindBoardRoot(root);
        if (boardRoot != null)
        {
            var layout = boardRoot.GetComponent<HorizontalLayoutGroup>() ?? boardRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(18, 18, 8, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            boardRoot.offsetMin = new Vector2(24f, 6f);
            boardRoot.offsetMax = new Vector2(-24f, -6f);
        }

        var boardFrames = root.Find("BottomSection/BoardSlots/BoardFrames");
        if (boardFrames != null)
        {
            foreach (Transform child in boardFrames)
            {
                if (child.TryGetComponent<Image>(out var img))
                {
                    img.color = new Color(DeckUiThemeCache.SecondaryAccentColor.r, DeckUiThemeCache.SecondaryAccentColor.g, DeckUiThemeCache.SecondaryAccentColor.b, 0.16f);
                }
            }
        }

        var handRoot = FindHandRoot(root);
        if (handRoot != null)
        {
            var layout = handRoot.GetComponent<HorizontalLayoutGroup>() ?? handRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(18, 18, 10, 12);
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            handRoot.offsetMin = new Vector2(24f, 10f);
            handRoot.offsetMax = new Vector2(-24f, -10f);
        }
    }

    private static RectTransform FindBoardRoot(RectTransform root)
    {
        return root.Find("BottomSection/BoardSlots/BoardCards") as RectTransform ??
               root.Find("BottomSection/BoardCards") as RectTransform;
    }

    private static RectTransform FindHandRoot(RectTransform root)
    {
        return root.Find("BottomSection/HandRow/HandCards") as RectTransform ??
               root.Find("BottomSection/HandCards") as RectTransform;
    }

    private static void EnsureHandFiligree(RectTransform root)
    {
        var handRoot = FindHandRoot(root);
        var highlight = DeckUiThemeCache.HighlightSprite;
        if (handRoot == null || highlight == null)
        {
            return;
        }

        foreach (Transform child in handRoot)
        {
            if (!child.name.StartsWith("CardFace_"))
            {
                continue;
            }

            var filigree = child.Find($"CardFaceFiligree_{child.GetSiblingIndex()}") as RectTransform;
            if (filigree == null)
            {
                var go = new GameObject($"CardFaceFiligree_{child.GetSiblingIndex()}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                filigree = go.GetComponent<RectTransform>();
                filigree.SetParent(child, false);
                filigree.anchorMin = new Vector2(0.5f, 0.5f);
                filigree.anchorMax = new Vector2(0.5f, 0.5f);
                filigree.pivot = new Vector2(0.5f, 0.5f);
                filigree.sizeDelta = new Vector2(258f, 352f);
                var img = filigree.GetComponent<Image>();
                img.sprite = highlight;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                img.color = CardFiligreeTint;
                filigree.SetAsFirstSibling();
                AttachGlowPulse(filigree, 1.05f, 0.1f, 0.28f);
            }
        }

        handRoot.SetAsLastSibling();
    }

    private static void HideBaseFrame(RectTransform root)
    {
        var names = new[] { "Border", "BorderHighlight" };
        foreach (var name in names)
        {
            var target = root.Find(name);
            if (target != null && target.TryGetComponent<Image>(out var image))
            {
                image.enabled = false;
            }
        }
    }

    private static Image FindImage(RectTransform root, string name)
    {
        var target = root.Find(name);
        return target == null ? null : target.GetComponent<Image>();
    }

    private static void AttachGlowPulse(Transform target, float speed, float minAlpha, float maxAlpha)
    {
        if (target == null)
        {
            return;
        }

        var pulse = target.GetComponent<DeckUiGlowPulse>() ?? target.gameObject.AddComponent<DeckUiGlowPulse>();
        pulse.PulseSpeed = speed;
        pulse.MinAlpha = minAlpha;
        pulse.MaxAlpha = maxAlpha;
    }

    private static void AttachBreathing(Transform target, float amplitude = 0.02f)
    {
        if (target == null)
        {
            return;
        }

        var assembly = typeof(DeckUiGlowPulse).Assembly;
        var breathingType = assembly.GetType("DeckUiBreathingScale");
        if (breathingType == null)
        {
            return;
        }

        var breathing = target.GetComponent(breathingType) ?? target.gameObject.AddComponent(breathingType);
        var amplitudeProperty = breathingType.GetProperty("ScaleAmplitude", BindingFlags.Public | BindingFlags.Instance);
        amplitudeProperty?.SetValue(breathing, amplitude);
    }

    private static void AttachSlowRotate(Transform target, float speed)
    {
        if (target == null)
        {
            return;
        }

        var assembly = typeof(DeckUiGlowPulse).Assembly;
        var rotateType = assembly.GetType("DeckUiSlowRotate");
        if (rotateType == null)
        {
            return;
        }

        var rotate = target.GetComponent(rotateType) ?? target.gameObject.AddComponent(rotateType);
        var speedProperty = rotateType.GetProperty("RotationSpeed", BindingFlags.Public | BindingFlags.Instance);
        speedProperty?.SetValue(rotate, speed);
    }

    private static void AttachSineFloat(Transform target, Vector2 amplitude, Vector2 frequency)
    {
        if (target == null)
        {
            return;
        }

        var sine = target.GetComponent<DeckUiSineFloat>() ?? target.gameObject.AddComponent<DeckUiSineFloat>();
        sine.Amplitude = amplitude;
        sine.Frequency = frequency;
    }

    private static Sprite LoadCardSprite(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
        return null;
#endif
    }
}
