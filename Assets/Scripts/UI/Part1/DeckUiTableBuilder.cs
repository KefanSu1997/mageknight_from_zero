using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据 Part1 场景需求构建统一的牌桌布局骨架。
/// 返回的 LayoutRoots 仅包含容器与占位背景，业务脚本可在其上填充具体 UI。
/// </summary>
public class DeckUiTableBuilder
{
    public struct LayoutRoots
    {
        public RectTransform Root;
        public RectTransform TableBackground;
        public RectTransform TableFrame;
        public RectTransform TableSurface;
        public RectTransform TableContent;
        public RectTransform ActionColumnShell;
        public RectTransform ActionColumn;
        public RectTransform CenterBoard;
        public RectTransform MagicCircle;
        public RectTransform DeckZoneShell;
        public RectTransform DeckZoneRoot;
        public RectTransform DeckStackAnchor;
        public RectTransform DeckStackDisplay;
        public RectTransform DiscardZoneShell;
        public RectTransform DiscardStackAnchor;
        public RectTransform DiscardFanDisplay;
        public RectTransform PromptBanner;
        public RectTransform InfoPanelFrame;
        public RectTransform InfoPanel;
        public RectTransform ManaReservoir;
        public RectTransform HandStripShell;
        public RectTransform HandStrip;
        public RectTransform HandGlow;
        public RectTransform OverlayRoot;
    }

    private const float ActionColumnWidth = 440f;
    private const float TableSurfaceWidth = 1880f;
    private const float TableSurfaceHeight = 1040f;
    private const float TableTiltAngle = 18f;
    private const float DeckZoneTiltAngle = -10f;
    private const float HandStripTiltAngle = 0f;
    private const float DeckZoneHeight = 320f;
    private const float HandStripHeight = 260f;
    private const float TableInset = 140f;
    private const float InfoDockWidth = 520f;
    private const float SectionSpacing = 24f;
    private const float ShellDropShadowAlpha = 0.32f;

    public LayoutRoots Build(Transform parent)
    {
        LayoutRoots roots = new LayoutRoots();

        var root = CreateRect("Part1TableRoot", parent);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        roots.Root = root;

        var rootBackgroundColor = DeckUiThemeCache.BackgroundColor;
        rootBackgroundColor.a = Mathf.Clamp01(rootBackgroundColor.a * 0.9f);
        var tableBackground = CreateImage("TableBackground", root, rootBackgroundColor, Image.Type.Simple);
        tableBackground.rectTransform.anchorMin = Vector2.zero;
        tableBackground.rectTransform.anchorMax = Vector2.one;
        tableBackground.rectTransform.offsetMin = Vector2.zero;
        tableBackground.rectTransform.offsetMax = Vector2.zero;
        tableBackground.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tableBackground.raycastTarget = false;
        roots.TableBackground = tableBackground.rectTransform;

        var tableFrame = CreateImage("TableFrame", root, new Color(0.18f, 0.12f, 0.22f, 0.94f));
        tableFrame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tableFrame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tableFrame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tableFrame.rectTransform.sizeDelta = new Vector2(TableSurfaceWidth + 220f, TableSurfaceHeight + 260f);
        tableFrame.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        AddShadow(tableFrame, new Vector2(0f, -18f), ShellDropShadowAlpha);
        roots.TableFrame = tableFrame.rectTransform;

        var tableSurface = CreateRect("TableSurface", root);
        tableSurface.anchorMin = new Vector2(0.5f, 0.5f);
        tableSurface.anchorMax = new Vector2(0.5f, 0.5f);
        tableSurface.pivot = new Vector2(0.5f, 0.5f);
        tableSurface.sizeDelta = new Vector2(TableSurfaceWidth, TableSurfaceHeight);
        tableSurface.anchoredPosition = new Vector2(0f, 96f);
        tableSurface.localRotation = Quaternion.Euler(TableTiltAngle, 0f, 0f);
        roots.TableSurface = tableSurface;

        var surfaceColor = DeckUiThemeCache.BackgroundSprite != null
            ? Color.white
            : new Color(0.16f, 0.1f, 0.22f, 0.96f);
        var surfaceBase = CreateImage("SurfaceBase", tableSurface, surfaceColor);
        surfaceBase.rectTransform.anchorMin = Vector2.zero;
        surfaceBase.rectTransform.anchorMax = Vector2.one;
        surfaceBase.rectTransform.offsetMin = Vector2.zero;
        surfaceBase.rectTransform.offsetMax = Vector2.zero;
        surfaceBase.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        if (DeckUiThemeCache.BackgroundSprite != null)
        {
            surfaceBase.sprite = DeckUiThemeCache.BackgroundSprite;
            surfaceBase.type = Image.Type.Sliced;
        }
        AddOutline(surfaceBase, new Color(0.42f, 0.28f, 0.54f, 0.3f), new Vector2(4f, -4f));
        surfaceBase.raycastTarget = false;

        var surfaceInset = CreateRect("SurfaceInset", tableSurface);
        surfaceInset.anchorMin = Vector2.zero;
        surfaceInset.anchorMax = Vector2.one;
        surfaceInset.offsetMin = new Vector2(TableInset, TableInset * 1.05f);
        surfaceInset.offsetMax = new Vector2(-TableInset, -TableInset * 0.4f);
        surfaceInset.pivot = new Vector2(0.5f, 0.5f);
        roots.TableContent = surfaceInset;

        var actionShell = CreateRect("ActionColumnShell", root);
        actionShell.anchorMin = new Vector2(0f, 0f);
        actionShell.anchorMax = new Vector2(0f, 1f);
        actionShell.pivot = new Vector2(0f, 0.5f);
        actionShell.anchoredPosition = new Vector2(64f, 0f);
        actionShell.sizeDelta = new Vector2(ActionColumnWidth + 88f, -120f);
        roots.ActionColumnShell = actionShell;

        var actionShadow = CreateImage("ActionColumnShadow", actionShell, new Color(0f, 0f, 0f, ShellDropShadowAlpha), Image.Type.Simple);
        actionShadow.rectTransform.anchorMin = Vector2.zero;
        actionShadow.rectTransform.anchorMax = Vector2.one;
        actionShadow.rectTransform.offsetMin = new Vector2(24f, 12f);
        actionShadow.rectTransform.offsetMax = new Vector2(-6f, -18f);
        actionShadow.raycastTarget = false;

        var actionPanel = CreateImage("ActionColumnRoot", actionShell, new Color(0.24f, 0.12f, 0.24f, 0.92f));
        actionPanel.rectTransform.anchorMin = Vector2.zero;
        actionPanel.rectTransform.anchorMax = Vector2.one;
        actionPanel.rectTransform.offsetMin = new Vector2(32f, 48f);
        actionPanel.rectTransform.offsetMax = new Vector2(-24f, -48f);
        AddOutline(actionPanel, new Color(0.6f, 0.46f, 0.2f, 0.35f), new Vector2(2f, -2f));
        roots.ActionColumn = actionPanel.rectTransform;

        var infoDock = CreateRect("InfoDockShell", surfaceInset);
        infoDock.anchorMin = new Vector2(1f, 0f);
        infoDock.anchorMax = new Vector2(1f, 1f);
        infoDock.pivot = new Vector2(1f, 0.5f);
        infoDock.sizeDelta = new Vector2(InfoDockWidth, 0f);
        infoDock.anchoredPosition = new Vector2(-12f, 0f);

        var infoFrame = CreateImage("InfoDockFrame", infoDock, new Color(0.14f, 0.1f, 0.18f, 0.94f));
        infoFrame.rectTransform.anchorMin = Vector2.zero;
        infoFrame.rectTransform.anchorMax = Vector2.one;
        infoFrame.rectTransform.offsetMin = new Vector2(12f, 24f);
        infoFrame.rectTransform.offsetMax = new Vector2(-12f, -24f);
        AddOutline(infoFrame, new Color(0.9f, 0.78f, 0.46f, 0.35f), new Vector2(2f, -2f));
        AddShadow(infoFrame, new Vector2(0f, -10f), ShellDropShadowAlpha);
        roots.InfoPanelFrame = infoFrame.rectTransform;

        const float promptHeight = 96f;
        const float manaHeight = 220f;

        var promptBanner = CreateImage("PromptBanner", infoFrame.rectTransform, new Color(0.3f, 0.2f, 0.44f, 0.95f));
        promptBanner.rectTransform.anchorMin = new Vector2(0f, 1f);
        promptBanner.rectTransform.anchorMax = new Vector2(1f, 1f);
        promptBanner.rectTransform.pivot = new Vector2(0.5f, 1f);
        promptBanner.rectTransform.sizeDelta = new Vector2(0f, promptHeight);
        promptBanner.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        roots.PromptBanner = promptBanner.rectTransform;

        var infoPanel = CreateRect("InfoPanelRoot", infoFrame.rectTransform);
        infoPanel.anchorMin = new Vector2(0f, 0f);
        infoPanel.anchorMax = new Vector2(1f, 1f);
        infoPanel.offsetMin = new Vector2(28f, manaHeight + 48f);
        infoPanel.offsetMax = new Vector2(-28f, -(promptHeight + 40f));
        infoPanel.pivot = new Vector2(0.5f, 0.5f);
        var infoImage = infoPanel.gameObject.AddComponent<Image>();
        infoImage.color = new Color(0.18f, 0.14f, 0.26f, 0.95f);
        infoImage.raycastTarget = true;
        roots.InfoPanel = infoPanel;

        var manaReservoir = CreateImage("ManaReservoirRoot", infoFrame.rectTransform, new Color(0.12f, 0.24f, 0.2f, 0.94f));
        manaReservoir.rectTransform.anchorMin = new Vector2(0f, 0f);
        manaReservoir.rectTransform.anchorMax = new Vector2(1f, 0f);
        manaReservoir.rectTransform.pivot = new Vector2(0.5f, 0f);
        manaReservoir.rectTransform.sizeDelta = new Vector2(0f, manaHeight);
        manaReservoir.rectTransform.anchoredPosition = new Vector2(0f, 28f);
        roots.ManaReservoir = manaReservoir.rectTransform;

        var centerBoard = CreateRect("CenterBoardRoot", surfaceInset);
        centerBoard.anchorMin = new Vector2(0f, 0f);
        centerBoard.anchorMax = new Vector2(1f, 1f);
        centerBoard.offsetMin = new Vector2(0f, 0f);
        centerBoard.offsetMax = new Vector2(-(InfoDockWidth + 36f), 0f);
        centerBoard.pivot = new Vector2(0.5f, 0.5f);
        roots.CenterBoard = centerBoard;

        var centerPlate = CreateImage("CenterPlate", centerBoard, new Color(0.12f, 0.09f, 0.16f, 0.94f));
        centerPlate.rectTransform.anchorMin = Vector2.zero;
        centerPlate.rectTransform.anchorMax = Vector2.one;
        centerPlate.rectTransform.offsetMin = new Vector2(36f, 44f);
        centerPlate.rectTransform.offsetMax = new Vector2(-36f, -96f);
        AddOutline(centerPlate, new Color(0.34f, 0.26f, 0.5f, 0.3f), new Vector2(1.5f, -1.5f));

        var magicCircle = CreateImage("MagicCircle", centerPlate.rectTransform, new Color(
            DeckUiThemeCache.SecondaryAccentColor.r,
            DeckUiThemeCache.SecondaryAccentColor.g,
            DeckUiThemeCache.SecondaryAccentColor.b,
            0.72f), Image.Type.Simple);
        magicCircle.rectTransform.anchorMin = new Vector2(0.5f, 0.58f);
        magicCircle.rectTransform.anchorMax = new Vector2(0.5f, 0.58f);
        magicCircle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        magicCircle.rectTransform.sizeDelta = new Vector2(820f, 820f);
        roots.MagicCircle = magicCircle.rectTransform;

        var deckShell = CreateRect("DeckZoneShell", centerPlate.rectTransform);
        deckShell.anchorMin = new Vector2(0f, 1f);
        deckShell.anchorMax = new Vector2(0f, 1f);
        deckShell.pivot = new Vector2(0f, 1f);
        deckShell.anchoredPosition = new Vector2(160f, -16f);
        deckShell.sizeDelta = new Vector2(420f, DeckZoneHeight);
        deckShell.localRotation = Quaternion.Euler(DeckZoneTiltAngle, 0f, 0f);
        roots.DeckZoneShell = deckShell;
        deckShell.SetAsLastSibling();

        var deckPedestal = CreateImage("DeckPedestal", deckShell, new Color(0.18f, 0.14f, 0.24f, 0.94f));
        deckPedestal.rectTransform.anchorMin = Vector2.zero;
        deckPedestal.rectTransform.anchorMax = Vector2.one;
        deckPedestal.rectTransform.offsetMin = new Vector2(-24f, -18f);
        deckPedestal.rectTransform.offsetMax = new Vector2(36f, 24f);
        AddOutline(deckPedestal, new Color(0.86f, 0.74f, 0.38f, 0.35f), new Vector2(2f, -2f));
        deckPedestal.raycastTarget = false;

        var deckContent = CreateRect("DeckZoneRoot", deckShell);
        deckContent.anchorMin = new Vector2(0f, 0f);
        deckContent.anchorMax = new Vector2(1f, 1f);
        deckContent.offsetMin = new Vector2(18f, 32f);
        deckContent.offsetMax = new Vector2(-24f, -16f);
        roots.DeckZoneRoot = deckContent;

        var deckStackAnchor = CreateRect("DeckStackAnchor", deckContent);
        deckStackAnchor.anchorMin = new Vector2(0f, 0f);
        deckStackAnchor.anchorMax = new Vector2(0.48f, 1f);
        deckStackAnchor.offsetMin = Vector2.zero;
        deckStackAnchor.offsetMax = Vector2.zero;
        roots.DeckStackAnchor = deckStackAnchor;

        var deckStackDisplay = CreateRect("DeckStackDisplay", deckShell);
        deckStackDisplay.anchorMin = new Vector2(0f, 1f);
        deckStackDisplay.anchorMax = new Vector2(0f, 1f);
        deckStackDisplay.pivot = new Vector2(0f, 1f);
        deckStackDisplay.anchoredPosition = new Vector2(-12f, 28f);
        deckStackDisplay.sizeDelta = new Vector2(220f, 240f);
        CreateStackDisplay(deckStackDisplay, DeckUiThemeCache.CardBackSprite, DeckUiThemeCache.AccentColor, 5, 18f);
        deckStackDisplay.SetSiblingIndex(0);
        roots.DeckStackDisplay = deckStackDisplay;

        var discardShell = CreateRect("DiscardZoneShell", centerPlate.rectTransform);
        discardShell.anchorMin = new Vector2(1f, 1f);
        discardShell.anchorMax = new Vector2(1f, 1f);
        discardShell.pivot = new Vector2(1f, 1f);
        discardShell.anchoredPosition = new Vector2(-160f, -16f);
        discardShell.sizeDelta = new Vector2(420f, DeckZoneHeight);
        discardShell.localRotation = Quaternion.Euler(DeckZoneTiltAngle, 0f, 0f);
        roots.DiscardZoneShell = discardShell;
        discardShell.SetAsLastSibling();

        var discardPedestal = CreateImage("DiscardPedestal", discardShell, new Color(0.2f, 0.12f, 0.18f, 0.94f));
        discardPedestal.rectTransform.anchorMin = Vector2.zero;
        discardPedestal.rectTransform.anchorMax = Vector2.one;
        discardPedestal.rectTransform.offsetMin = new Vector2(-24f, -20f);
        discardPedestal.rectTransform.offsetMax = new Vector2(24f, 18f);
        AddOutline(discardPedestal, new Color(0.78f, 0.38f, 0.34f, 0.35f), new Vector2(2f, -2f));
        discardPedestal.raycastTarget = false;

        var discardContent = CreateRect("DiscardZoneRoot", discardShell);
        discardContent.anchorMin = new Vector2(0f, 0f);
        discardContent.anchorMax = new Vector2(1f, 1f);
        discardContent.offsetMin = new Vector2(28f, 32f);
        discardContent.offsetMax = new Vector2(-18f, -16f);

        var discardAnchor = CreateRect("DiscardStackAnchor", discardContent);
        discardAnchor.anchorMin = new Vector2(0.52f, 0f);
        discardAnchor.anchorMax = new Vector2(1f, 1f);
        discardAnchor.offsetMin = Vector2.zero;
        discardAnchor.offsetMax = Vector2.zero;
        roots.DiscardStackAnchor = discardAnchor;

        var discardFanDisplay = CreateRect("DiscardFanDisplay", discardShell);
        discardFanDisplay.anchorMin = new Vector2(1f, 1f);
        discardFanDisplay.anchorMax = new Vector2(1f, 1f);
        discardFanDisplay.pivot = new Vector2(1f, 1f);
        discardFanDisplay.anchoredPosition = new Vector2(24f, 32f);
        discardFanDisplay.sizeDelta = new Vector2(240f, 240f);
        CreateFanDisplay(discardFanDisplay, DeckUiThemeCache.CardBackSprite, DeckUiThemeCache.SecondaryAccentColor, 4, 15f);
        discardFanDisplay.SetSiblingIndex(0);
        roots.DiscardFanDisplay = discardFanDisplay;

        var handShell = CreateRect("HandStripShell", root);
        handShell.anchorMin = new Vector2(0.1f, 0f);
        handShell.anchorMax = new Vector2(0.9f, 0f);
        handShell.pivot = new Vector2(0.5f, 0f);
        handShell.anchoredPosition = new Vector2(0f, 32f);
        handShell.sizeDelta = new Vector2(0f, HandStripHeight);
        handShell.localRotation = Quaternion.identity;
        roots.HandStripShell = handShell;

        var handSurface = CreateImage("HandSurface", handShell, new Color(0.1f, 0.12f, 0.24f, 0.94f));
        handSurface.rectTransform.anchorMin = Vector2.zero;
        handSurface.rectTransform.anchorMax = Vector2.one;
        handSurface.rectTransform.offsetMin = new Vector2(-24f, 12f);
        handSurface.rectTransform.offsetMax = new Vector2(24f, 48f);
        AddOutline(handSurface, new Color(0.54f, 0.62f, 0.92f, 0.3f), new Vector2(2f, -2f));
        handSurface.raycastTarget = false;

        var handGlow = CreateImage("HandGlow", handShell, DeckUiThemeCache.SecondaryAccentColor, Image.Type.Sliced);
        handGlow.rectTransform.anchorMin = Vector2.zero;
        handGlow.rectTransform.anchorMax = Vector2.one;
        handGlow.rectTransform.offsetMin = new Vector2(12f, 16f);
        handGlow.rectTransform.offsetMax = new Vector2(-12f, -12f);
        handGlow.canvasRenderer.SetAlpha(0.55f);
        handGlow.raycastTarget = false;
        roots.HandGlow = handGlow.rectTransform;

        var handStrip = CreateRect("HandStripRoot", handShell);
        handStrip.anchorMin = Vector2.zero;
        handStrip.anchorMax = Vector2.one;
        handStrip.offsetMin = new Vector2(24f, 24f);
        handStrip.offsetMax = new Vector2(-24f, -32f);
        roots.HandStrip = handStrip;

        var overlayRoot = CreateRect("OverlayRoot", root);
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        overlayRoot.pivot = new Vector2(0.5f, 0.5f);
        overlayRoot.SetAsLastSibling();
        roots.OverlayRoot = overlayRoot;

        return roots;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Color color, Image.Type type = Image.Type.Sliced)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.type = type;
        return image;
    }

    private static void AddShadow(Graphic graphic, Vector2 distance, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        var shadow = graphic.GetComponent<Shadow>() ?? graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = distance;
    }

    private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
    {
        if (graphic == null)
        {
            return;
        }

        var outline = graphic.GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void CreateStackDisplay(RectTransform parent, Sprite sprite, Color tint, int layerCount, float offset)
    {
        for (int i = 0; i < layerCount; i++)
        {
            var layer = CreateImage($"StackLayer_{i}", parent, tint, Image.Type.Sliced);
            var rect = layer.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(180f, 240f);
            rect.anchoredPosition = new Vector2(i * offset, -i * offset * 0.75f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -3f * i);
            if (sprite != null)
            {
                layer.sprite = sprite;
            }

            layer.canvasRenderer.SetAlpha(Mathf.Clamp01(1f - i * 0.12f));
            layer.raycastTarget = false;
        }
    }

    private static void CreateFanDisplay(RectTransform parent, Sprite sprite, Color tint, int layerCount, float angleStep)
    {
        float radius = 140f;
        for (int i = 0; i < layerCount; i++)
        {
            float angle = -angleStep * (layerCount - 1) * 0.5f + angleStep * i;
            var layer = CreateImage($"FanLayer_{i}", parent, tint, Image.Type.Sliced);
            var rect = layer.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(180f, 240f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            rect.anchoredPosition = new Vector2(-Mathf.Sin(Mathf.Deg2Rad * angle) * radius, -Mathf.Cos(Mathf.Deg2Rad * angle) * 60f);
            if (sprite != null)
            {
                layer.sprite = sprite;
            }

            layer.canvasRenderer.SetAlpha(Mathf.Clamp01(0.85f - i * 0.15f));
            layer.raycastTarget = false;
        }
    }
}
