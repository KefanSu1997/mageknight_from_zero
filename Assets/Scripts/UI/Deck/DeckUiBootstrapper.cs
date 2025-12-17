using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class DeckUiBootstrapper : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private HandManager handManager;
    [SerializeField] private HandManagerAdvanced handManagerAdvanced;

    [Header("Theme Sprites (可选)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite magicCircleSprite;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private Sprite cardHighlightSprite;

    [Header("配色")]
    [SerializeField] private Color backgroundColor = new Color32(14, 26, 40, 255);
    [SerializeField] private Color borderColor = new Color32(200, 214, 228, 255);
    [SerializeField] private Color accentColor = new Color32(126, 211, 255, 255);
    [SerializeField] private Color secondaryAccentColor = new Color32(244, 212, 154, 255);
    [SerializeField] private Color textColor = Color.white;

    [Header("布局参数")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private float borderPadding = 48f;
    [SerializeField] private float bottomSectionHeight = 440f;
    [SerializeField] private float bottomCardSpacing = 28f;
    [SerializeField] private float bottomCardWidth = 240f;
    [SerializeField] private float bottomCardHeight = 360f;

    [Header("生成节点 (调试)")]
    [SerializeField] private RectTransform generatedRoot;
    [SerializeField] private RectTransform cardsAnchor;
    [SerializeField] private RectTransform deckStackAnchor;
    [SerializeField] private RectTransform discardGhostAnchor;
    [SerializeField] private RectTransform floatingCardsAnchor;

    private readonly List<Image> generatedCardFrames = new();
    private Text deckCountText;
    private Text discardCountText;

    private static Sprite fallbackSprite;
    public Sprite BackgroundSprite => backgroundSprite;
    public Sprite BorderSprite => borderSprite;
    public Sprite MagicCircleSprite => magicCircleSprite;
    public Sprite CardBackSprite => cardBackSprite;
    public Sprite CardHighlightSprite => cardHighlightSprite;
    public Color BackgroundColor => backgroundColor;
    public Color BorderColor => borderColor;
    public Color AccentColor => accentColor;
    public Color SecondaryAccentColor => secondaryAccentColor;

    private void Reset()
    {
        BuildLayout();
    }

    private void Awake()
    {
        BuildLayout();
        DeckUiThemeCache.NotifyThemeChanged();
    }

    private void BuildLayout()
    {
        if (Application.isPlaying && generatedRoot != null)
        {
            return;
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogError("DeckUiBootstrapper: 未找到 Canvas，无法生成测试场景 UI。");
            return;
        }

        var scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (handManager == null)
        {
            handManager = GetComponentInParent<HandManager>();
            if (handManager == null)
            {
                handManager = Object.FindFirstObjectByType<HandManager>(FindObjectsInactive.Include);
            }
        }

        if (handManagerAdvanced == null)
        {
            handManagerAdvanced = GetComponentInParent<HandManagerAdvanced>();
            if (handManagerAdvanced == null)
            {
                handManagerAdvanced = Object.FindFirstObjectByType<HandManagerAdvanced>(FindObjectsInactive.Include);
            }
        }

        var existing = targetCanvas.transform.Find("DeckUIRoot");
        if (existing != null)
        {
            generatedRoot = existing as RectTransform;
            CacheExistingReferences();
        }
        else
        {
            generatedRoot = CreateRoot(targetCanvas.transform);
            CreateBackground(generatedRoot);
            CreateBorder(generatedRoot);
            CreateMagicCircle(generatedRoot);
            CreateDeckStack(generatedRoot);
            CreateDiscardGhost(generatedRoot);
            CreateBottomSection(generatedRoot);
        }

        AssignHandRoots();
        AssignDebugText();
        DeckIdealRuntimeTuner.ApplyIfInIdealScene(generatedRoot);
    }

    private void CacheExistingReferences()
    {
        cardsAnchor = generatedRoot.Find("BottomSection/HandRow/HandCards") as RectTransform;
        if (cardsAnchor == null)
        {
            cardsAnchor = generatedRoot.Find("BottomSection/HandCards") as RectTransform;
        }
        deckStackAnchor = generatedRoot.Find("DeckStack") as RectTransform;
        discardGhostAnchor = generatedRoot.Find("DiscardGhost") as RectTransform;
        floatingCardsAnchor = generatedRoot.Find("MagicCircle/FloatingCards") as RectTransform;

        if (generatedRoot != null)
        {
            var deckLabel = generatedRoot.GetComponentInChildren<Text>(true);
            if (deckLabel != null)
            {
                deckCountText = deckLabel;
            }
        }
    }

    private RectTransform CreateRoot(Transform canvasTransform)
    {
        var go = new GameObject("DeckUIRoot", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvasTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void CreateBackground(RectTransform parent)
    {
        var background = CreateImage("Background", parent, backgroundSprite, backgroundColor);
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        background.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        background.raycastTarget = false;
    }

    private void CreateBorder(RectTransform parent)
    {
        var border = CreateImage("Border", parent, borderSprite, borderColor);
        border.rectTransform.anchorMin = Vector2.zero;
        border.rectTransform.anchorMax = Vector2.one;
        border.rectTransform.offsetMin = new Vector2(borderPadding, borderPadding);
        border.rectTransform.offsetMax = new Vector2(-borderPadding, -borderPadding);
        border.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        border.type = Image.Type.Sliced;
        border.raycastTarget = false;

        var highlight = CreateImage("BorderHighlight", parent, cardHighlightSprite, accentColor * 0.5f, true);
        highlight.rectTransform.anchorMin = Vector2.zero;
        highlight.rectTransform.anchorMax = Vector2.one;
        highlight.rectTransform.offsetMin = new Vector2(borderPadding + 10f, borderPadding + 10f);
        highlight.rectTransform.offsetMax = new Vector2(-(borderPadding + 10f), -(borderPadding + 10f));
        highlight.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        highlight.type = Image.Type.Sliced;
        highlight.canvasRenderer.SetAlpha(0.6f);
        highlight.raycastTarget = false;
    }

    private void CreateMagicCircle(RectTransform parent)
    {
        var magicCircleRoot = CreateRect("MagicCircle", parent);
        magicCircleRoot.anchorMin = new Vector2(0.5f, 0.5f);
        magicCircleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        magicCircleRoot.anchoredPosition = Vector2.zero;
        magicCircleRoot.sizeDelta = new Vector2(referenceResolution.x * 0.45f, referenceResolution.x * 0.45f);

        var circle = CreateImage("Circle", magicCircleRoot, magicCircleSprite, secondaryAccentColor * 0.8f);
        circle.rectTransform.anchorMin = Vector2.zero;
        circle.rectTransform.anchorMax = Vector2.one;
        circle.rectTransform.offsetMin = Vector2.zero;
        circle.rectTransform.offsetMax = Vector2.zero;
        circle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        circle.canvasRenderer.SetAlpha(0.75f);
        circle.raycastTarget = false;
        circle.gameObject.AddComponent<DeckUiRotateGraphic>();

        floatingCardsAnchor = CreateRect("FloatingCards", magicCircleRoot);
        floatingCardsAnchor.anchorMin = Vector2.zero;
        floatingCardsAnchor.anchorMax = Vector2.one;
        floatingCardsAnchor.offsetMin = new Vector2(referenceResolution.x * 0.02f, referenceResolution.x * 0.02f);
        floatingCardsAnchor.offsetMax = new Vector2(-referenceResolution.x * 0.02f, -referenceResolution.x * 0.02f);
        floatingCardsAnchor.pivot = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < 3; i++)
        {
            var card = CreateImage($"FloatingCard_{i}", floatingCardsAnchor, cardBackSprite, accentColor);
            var rect = card.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(bottomCardWidth * 0.7f, bottomCardHeight * 0.7f);
            rect.anchoredPosition = new Vector2((i - 1) * 160f, 60f + i * 25f);
            rect.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 8f);
            var motion = card.gameObject.AddComponent<DeckUiSineFloat>();
            motion.Amplitude = new Vector2(12f + i * 4f, 18f + i * 6f);
            motion.Frequency = new Vector2(0.15f + i * 0.03f, 0.18f + i * 0.02f);
            motion.SetPhaseOffset(i * 0.8f);
        }
    }

    private void CreateDeckStack(RectTransform parent)
    {
        deckStackAnchor = CreateRect("DeckStack", parent);
        deckStackAnchor.anchorMin = new Vector2(0f, 1f);
        deckStackAnchor.anchorMax = new Vector2(0f, 1f);
        deckStackAnchor.pivot = new Vector2(0f, 1f);
        deckStackAnchor.anchoredPosition = new Vector2(borderPadding + 120f, -borderPadding - 40f);
        deckStackAnchor.sizeDelta = new Vector2(400f, 320f);

        for (int i = 0; i < 4; i++)
        {
            var stackCard = CreateImage($"DeckCard_{i}", deckStackAnchor, cardBackSprite, accentColor);
            var rect = stackCard.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(bottomCardWidth * 0.65f, bottomCardHeight * 0.65f);
            rect.anchoredPosition = new Vector2(i * 24f, -i * 18f);
            stackCard.canvasRenderer.SetAlpha(Mathf.Clamp01(1f - i * 0.18f));
            stackCard.raycastTarget = false;
        }

        if (cardHighlightSprite != null)
        {
            var glow = CreateImage("DeckGlow", deckStackAnchor, cardHighlightSprite, accentColor, true);
            var glowRect = glow.rectTransform;
            glowRect.anchorMin = new Vector2(0f, 1f);
            glowRect.anchorMax = new Vector2(0f, 1f);
            glowRect.pivot = new Vector2(0f, 1f);
            glowRect.sizeDelta = new Vector2(bottomCardWidth * 0.72f, bottomCardHeight * 0.72f);
            glowRect.anchoredPosition = new Vector2(16f, -12f);
            glow.type = Image.Type.Sliced;
            glow.transform.SetAsLastSibling();
            var pulse = glow.gameObject.AddComponent<DeckUiGlowPulse>();
            pulse.MinAlpha = 0.18f;
            pulse.MaxAlpha = 0.55f;
        }

        deckCountText = CreateText("DeckCountLabel", deckStackAnchor, "牌组: 0", TextAnchor.UpperLeft);
        var deckCountRect = deckCountText.rectTransform;
        deckCountRect.anchorMin = new Vector2(0f, 0f);
        deckCountRect.anchorMax = new Vector2(0f, 0f);
        deckCountRect.pivot = new Vector2(0f, 0f);
        deckCountRect.anchoredPosition = new Vector2(8f, -deckStackAnchor.sizeDelta.y + 16f);
    }

    private void CreateDiscardGhost(RectTransform parent)
    {
        discardGhostAnchor = CreateRect("DiscardGhost", parent);
        discardGhostAnchor.anchorMin = new Vector2(1f, 1f);
        discardGhostAnchor.anchorMax = new Vector2(1f, 1f);
        discardGhostAnchor.pivot = new Vector2(1f, 1f);
        discardGhostAnchor.anchoredPosition = new Vector2(-borderPadding - 120f, -borderPadding - 60f);
        discardGhostAnchor.sizeDelta = new Vector2(420f, 320f);

        for (int i = 0; i < 3; i++)
        {
            var ghostCard = CreateImage($"GhostCard_{i}", discardGhostAnchor, cardBackSprite, secondaryAccentColor);
            var rect = ghostCard.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(bottomCardWidth * 0.6f, bottomCardHeight * 0.6f);
            rect.anchoredPosition = new Vector2(-i * 28f, -i * 18f);
            ghostCard.canvasRenderer.SetAlpha(Mathf.Clamp01(0.6f - i * 0.18f));
            ghostCard.raycastTarget = false;
        }

        if (cardHighlightSprite != null)
        {
            var glow = CreateImage("DiscardGlow", discardGhostAnchor, cardHighlightSprite, secondaryAccentColor, true);
            var glowRect = glow.rectTransform;
            glowRect.anchorMin = new Vector2(1f, 1f);
            glowRect.anchorMax = new Vector2(1f, 1f);
            glowRect.pivot = new Vector2(1f, 1f);
            glowRect.sizeDelta = new Vector2(bottomCardWidth * 0.68f, bottomCardHeight * 0.68f);
            glowRect.anchoredPosition = new Vector2(-20f, -14f);
            glow.type = Image.Type.Sliced;
            glow.transform.SetAsLastSibling();
            var pulse = glow.gameObject.AddComponent<DeckUiGlowPulse>();
            pulse.MinAlpha = 0.14f;
            pulse.MaxAlpha = 0.48f;
        }

        discardCountText = CreateText("DiscardCountLabel", discardGhostAnchor, "弃牌: 0", TextAnchor.UpperRight);
        var discardRect = discardCountText.rectTransform;
        discardRect.anchorMin = new Vector2(1f, 0f);
        discardRect.anchorMax = new Vector2(1f, 0f);
        discardRect.pivot = new Vector2(1f, 0f);
        discardRect.anchoredPosition = new Vector2(-8f, -discardGhostAnchor.sizeDelta.y + 16f);
    }

    private void CreateBottomSection(RectTransform parent)
    {
        var bottomRoot = CreateRect("BottomSection", parent);
        bottomRoot.anchorMin = new Vector2(0.5f, 0f);
        bottomRoot.anchorMax = new Vector2(0.5f, 0f);
        bottomRoot.pivot = new Vector2(0.5f, 0f);
        bottomRoot.anchoredPosition = new Vector2(0f, borderPadding + 36f);
        bottomRoot.sizeDelta = new Vector2(referenceResolution.x * 0.92f, bottomSectionHeight);

        var rows = CreateRect("Rows", bottomRoot);
        rows.anchorMin = Vector2.zero;
        rows.anchorMax = Vector2.one;
        rows.offsetMin = Vector2.zero;
        rows.offsetMax = Vector2.zero;
        rows.pivot = new Vector2(0.5f, 0.5f);

        var vertical = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 12f;
        vertical.padding = new RectOffset(16, 16, 12, 12);
        vertical.childAlignment = TextAnchor.LowerCenter;
        vertical.childControlHeight = true;
        vertical.childControlWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = true;

        var boardRow = CreateRect("BoardSlots", rows);
        var boardElement = boardRow.gameObject.AddComponent<LayoutElement>();
        boardElement.preferredHeight = bottomCardHeight * 0.85f + 28f;

        var boardFrames = CreateRect("BoardFrames", boardRow);
        boardFrames.anchorMin = Vector2.zero;
        boardFrames.anchorMax = Vector2.one;
        boardFrames.offsetMin = Vector2.zero;
        boardFrames.offsetMax = Vector2.zero;
        boardFrames.pivot = new Vector2(0.5f, 0.5f);

        var boardLayout = boardFrames.gameObject.AddComponent<HorizontalLayoutGroup>();
        boardLayout.spacing = bottomCardSpacing;
        boardLayout.padding = new RectOffset(12, 12, 8, 8);
        boardLayout.childAlignment = TextAnchor.MiddleCenter;
        boardLayout.childControlHeight = true;
        boardLayout.childControlWidth = true;
        boardLayout.childForceExpandHeight = false;
        boardLayout.childForceExpandWidth = false;

        for (int i = 0; i < 5; i++)
        {
            var slot = CreateImage($"BoardSlot_{i}", boardFrames, cardBackSprite, secondaryAccentColor * 0.22f);
            var slotRect = slot.rectTransform;
            slotRect.sizeDelta = new Vector2(bottomCardWidth * 0.95f, bottomCardHeight * 0.8f);
            slot.type = Image.Type.Sliced;
            slot.color = secondaryAccentColor * 0.9f;
            slot.raycastTarget = false;
            var element = slot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = slotRect.sizeDelta.x;
            element.preferredHeight = slotRect.sizeDelta.y;
        }

        var boardCards = CreateRect("BoardCards", boardRow);
        boardCards.anchorMin = Vector2.zero;
        boardCards.anchorMax = Vector2.one;
        boardCards.offsetMin = new Vector2(26f, 10f);
        boardCards.offsetMax = new Vector2(-26f, -6f);
        boardCards.pivot = new Vector2(0.5f, 0.5f);
        boardCards.SetAsLastSibling();

        var handRow = CreateRect("HandRow", rows);
        var handElement = handRow.gameObject.AddComponent<LayoutElement>();
        handElement.preferredHeight = bottomCardHeight + 72f;

        var frameLayer = CreateRect("HandFrames", handRow);
        frameLayer.anchorMin = Vector2.zero;
        frameLayer.anchorMax = Vector2.one;
        frameLayer.offsetMin = Vector2.zero;
        frameLayer.offsetMax = Vector2.zero;
        frameLayer.pivot = new Vector2(0.5f, 0.5f);

        var layout = frameLayer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = bottomCardSpacing;
        layout.padding = new RectOffset(16, 16, 20, 20);
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        for (int i = 0; i < 5; i++)
        {
            var frame = CreateImage($"CardFrame_{i}", frameLayer, cardHighlightSprite, accentColor, true);
            var frameRect = frame.rectTransform;
            frameRect.sizeDelta = new Vector2(bottomCardWidth, bottomCardHeight);
            frame.type = Image.Type.Sliced;
            frame.canvasRenderer.SetAlpha(0.9f);
            var element = frame.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = bottomCardWidth;
            element.preferredHeight = bottomCardHeight;
            generatedCardFrames.Add(frame);
        }

        cardsAnchor = CreateRect("HandCards", handRow);
        cardsAnchor.anchorMin = Vector2.zero;
        cardsAnchor.anchorMax = Vector2.one;
        cardsAnchor.offsetMin = new Vector2(32f, 18f);
        cardsAnchor.offsetMax = new Vector2(-32f, -12f);
        cardsAnchor.pivot = new Vector2(0.5f, 0.5f);
        cardsAnchor.SetAsLastSibling();
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite, Color fallbackColor, bool tintWhenSpriteAssigned = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = tintWhenSpriteAssigned ? fallbackColor : Color.white;
        }
        else
        {
            image.sprite = GetFallbackSprite();
            image.color = fallbackColor;
        }
        return image;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private Text CreateText(string name, Transform parent, string defaultText, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = defaultText;
        text.alignment = anchor;
        text.color = textColor;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtinFont == null)
        {
            builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        text.font = builtinFont;
        return text;
    }

    private void AssignHandRoots()
    {
        if (cardsAnchor == null)
        {
            Debug.LogWarning("DeckUiBootstrapper: 未能找到 HandCards 容器，默认回退到 Canvas 根。");
            cardsAnchor = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
        }

        if (handManager != null)
        {
            handManager.handRoot = cardsAnchor;
        }

        if (handManagerAdvanced != null)
        {
            handManagerAdvanced.handRoot = cardsAnchor;
        }
    }

    private void AssignDebugText()
    {
        if (handManagerAdvanced != null)
        {
            if (handManagerAdvanced.deckCountText == null && deckCountText != null)
            {
                handManagerAdvanced.deckCountText = deckCountText;
            }

            if (handManagerAdvanced.discardCountText == null && discardCountText != null)
            {
                handManagerAdvanced.discardCountText = discardCountText;
            }
        }
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        var tex = new Texture2D(2, 2)
        {
            name = "DeckUIFallbackTexture",
            hideFlags = HideFlags.HideAndDontSave
        };

        var color = Color.white;
        tex.SetPixel(0, 0, color);
        tex.SetPixel(1, 0, color);
        tex.SetPixel(0, 1, color);
        tex.SetPixel(1, 1, color);
        tex.Apply();

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        fallbackSprite.name = "DeckUIFallbackSprite";
        return fallbackSprite;
    }
}
