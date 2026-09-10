using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ElementCardFlipPresenter : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color BackTint = Color.white;
    private const float EarthBackCropInsetPixels = 24f;
    private const float DefaultBackCropInsetPixels = 0f;
    private static Sprite builtInUiSprite;
    private static Texture2D builtInUiTexture;

    [SerializeField] private string elementLabel = "Element";
    [SerializeField] private string frontSpritePath;
    [SerializeField] private string backSpritePath;
    [SerializeField] private string frameSpritePath;
    [SerializeField] private bool showFrontOnStart = true;

    private Image backImage;
    private Image frameImage;
    private Image rootHitImage;
    private Button rootButton;
    private Sprite backSprite;
    private Sprite frontSprite;
    private Sprite frameSprite;
    private bool isFront;
    private GameObject backRoot;
    private GameObject frontRoot;
    private Image frontArtImage;
    private AspectRatioFitter frontArtAspect;
    private Image frontTintImage;
    private Image frontFrameImage;
    private TMP_Text labelText;
    private Image sigilImage;
    private CanvasGroup backGroup;
    private CanvasGroup frontGroup;
    private RectMask2D backMask;
    private bool initialized;

    public bool IsFrontShown => isFront;

    public void Configure(string label, string frontPath, string backPath, string framePath, bool showFront)
    {
        elementLabel = label;
        frontSpritePath = frontPath;
        backSpritePath = backPath;
        frameSpritePath = framePath;
        showFrontOnStart = showFront;
        EnsureSetup(forceRefresh: true);
    }

    private void Awake()
    {
        EnsureSetup(forceRefresh: false);
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        EnforceCurrentStateVisuals();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        Toggle();
    }

    private void EnsureSetup(bool forceRefresh)
    {
        if (initialized && !forceRefresh)
        {
            return;
        }

        backImage ??= FindChildImage("Img_Back");
        frameImage ??= FindChildImage("Img_Frame");

        if (backImage == null)
        {
            Debug.LogWarning($"ElementCardFlipPresenter: Img_Back missing on {name}");
            return;
        }

        EnsureRootInteraction();
        SyncRootRectToBack();

        if (forceRefresh || frontSprite == null)
        {
            var frontFallback = frontArtImage != null ? frontArtImage.sprite : null;
            frontSprite = LoadSprite(frontSpritePath) ?? frontSprite ?? frontFallback;
        }

        if (forceRefresh || backSprite == null)
        {
            backSprite = LoadSprite(backSpritePath) ?? backSprite ?? backImage.sprite;
        }

        if (forceRefresh || frameSprite == null)
        {
            var fallbackFrameSprite = frameImage != null ? frameImage.sprite : null;
            frameSprite = LoadSprite(frameSpritePath) ?? frameSprite ?? fallbackFrameSprite;
        }

        EnsureRoots();
        EnsureCanvasGroups();
        ConfigureChildRaycastTargets();

        UpdateOverlayLabel();
        UpdateOverlayDecoration();
        UpdateFrontArtAspect();

#if UNITY_EDITOR && ELEMENT_CARDS_VERBOSE_LOGS
        LogSetupState();
#endif

        ApplyState(showFrontOnStart);
        initialized = true;
    }

    private void Toggle()
    {
        ApplyState(!isFront);
    }

    public void ToggleFromButton()
    {
        Toggle();
    }

    public void SetFront(bool showFront)
    {
        EnsureSetup(forceRefresh: false);
        ApplyState(showFront);
    }

    private void ApplyState(bool showFront)
    {
        isFront = showFront;

        UpdateOverlayLabel();
        UpdateOverlayDecoration();
        UpdateFrontArtAspect();

        EnforceCurrentStateVisuals();

#if UNITY_EDITOR && ELEMENT_CARDS_VERBOSE_LOGS
        Debug.Log(
            $"[ElementCardFlip] {name} showFront={showFront} backActive={(backRoot != null && backRoot.activeSelf)} frontActive={(frontRoot != null && frontRoot.activeSelf)}");
#endif
    }

    private void EnforceCurrentStateVisuals()
    {
        ApplyExclusiveRootState(isFront);
        ApplyBackVisualState(isFront);
        ApplyFrontVisualState(isFront);
        DisableLegacySiblingVisuals(isFront);
    }

    private void ApplyExclusiveRootState(bool showFront)
    {
        var showBack = !showFront;
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            var childGo = child.gameObject;
            var isBackRoot = childGo == backRoot;
            var isFrontRoot = childGo == frontRoot;
            // Treat unknown direct children as front-only legacy visuals.
            var shouldShow = isBackRoot ? showBack : showFront;
            var group = ResolveVisibilityGroup(childGo, isBackRoot, isFrontRoot);
            ApplyRootVisibility(childGo, group, shouldShow);
        }

        if (showFront && frontRoot != null)
        {
            frontRoot.transform.SetAsLastSibling();
        }
        else if (showBack && backRoot != null)
        {
            backRoot.transform.SetAsLastSibling();
        }
    }

    private CanvasGroup ResolveVisibilityGroup(GameObject root, bool isBackRoot, bool isFrontRoot)
    {
        if (root == null)
        {
            return null;
        }

        if (isBackRoot)
        {
            backGroup = EnsureCanvasGroup(root, backGroup);
            return backGroup;
        }

        if (isFrontRoot)
        {
            frontGroup = EnsureCanvasGroup(root, frontGroup);
            return frontGroup;
        }

        return root.GetComponent<CanvasGroup>();
    }

    private Image FindChildImage(string childName)
    {
        var child = transform.Find(childName);
        if (child != null)
        {
            return child.GetComponent<Image>();
        }

        var images = GetComponentsInChildren<Image>(includeInactive: true);
        if (images == null)
        {
            return null;
        }

        foreach (var image in images)
        {
            if (image != null && image.name == childName)
            {
                return image;
            }
        }

        return null;
    }

    private void EnsureRoots()
    {
        EnsureBackRoot();

        if (frontRoot == null)
        {
            var existingFront = transform.Find("FrontRoot");
            if (existingFront != null)
            {
                frontRoot = existingFront.gameObject;
            }
        }

        if (frontRoot == null)
        {
            frontRoot = BuildFrontRoot();
        }
        else
        {
            CacheFrontReferences();
            ConfigureFrontRootRect(frontRoot.GetComponent<RectTransform>());
        }
    }

    private void EnsureCanvasGroups()
    {
        if (backRoot != null)
        {
            backGroup = EnsureCanvasGroup(backRoot, backGroup);
        }

        if (frontRoot != null)
        {
            frontGroup = EnsureCanvasGroup(frontRoot, frontGroup);
        }
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject root, CanvasGroup existing)
    {
        if (root == null)
        {
            return existing;
        }

        var group = existing != null ? existing : root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static void ApplyRootVisibility(GameObject root, CanvasGroup group, bool show)
    {
        if (group != null)
        {
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
        }

        if (root != null)
        {
            root.SetActive(show);
        }
    }

    private void EnsureBackRoot()
    {
        if (backRoot == null)
        {
            var existingBack = transform.Find("BackRoot");
            backRoot = existingBack != null ? existingBack.gameObject : new GameObject("BackRoot", typeof(RectTransform));
        }

        var rect = backRoot.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        backMask ??= backRoot.GetComponent<RectMask2D>();
        if (backMask == null)
        {
            backMask = backRoot.AddComponent<RectMask2D>();
        }

        backMask.padding = Vector4.zero;
        backMask.softness = Vector2Int.zero;

        if (backImage != null)
        {
            backImage.transform.SetParent(backRoot.transform, false);
            backImage.transform.SetSiblingIndex(0);
            ApplyBackCropRect(backImage.rectTransform);
            backImage.type = Image.Type.Simple;
            backImage.preserveAspect = false;
            backImage.color = BackTint;
        }

        if (frameImage != null)
        {
            frameImage.transform.SetParent(backRoot.transform, false);
            frameImage.transform.SetAsLastSibling();
            ResetToFillRect(frameImage.rectTransform, Vector2.zero, Vector2.zero);
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
        }
    }

    private void CacheFrontReferences()
    {
        if (frontRoot == null)
        {
            return;
        }

        var frontTransform = frontRoot.transform;
        if (frontArtImage == null)
        {
            var art = frontTransform.Find("Img_FrontArt");
            if (art != null)
            {
                frontArtImage = art.GetComponent<Image>();
                frontArtAspect = art.GetComponent<AspectRatioFitter>();
                ResetToFillRect(art.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            }
        }

        if (frontTintImage == null)
        {
            var tint = frontTransform.Find("Img_FrontTint");
            if (tint != null)
            {
                frontTintImage = tint.GetComponent<Image>();
                ResetToFillRect(tint.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            }
        }

        if (frontFrameImage == null)
        {
            var frame = frontTransform.Find("Img_FrontFrame");
            if (frame != null)
            {
                frontFrameImage = frame.GetComponent<Image>();
                ResetToFillRect(frame.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            }
        }

        if (sigilImage == null)
        {
            var sigil = frontTransform.Find("Img_FrontSigil");
            if (sigil != null)
            {
                sigilImage = sigil.GetComponent<Image>();
            }
        }

        if (labelText == null)
        {
            var label = frontTransform.Find("Txt_Element");
            if (label != null)
            {
                labelText = label.GetComponent<TMP_Text>();
            }
        }
    }

    private void ConfigureFrontRootRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetParent(transform, false);
        ResetToFillRect(rect, Vector2.zero, Vector2.zero);
        rect.SetAsLastSibling();
    }

    private GameObject BuildFrontRoot()
    {
        var root = new GameObject("FrontRoot", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        ConfigureFrontRootRect(rect);

        var artGo = new GameObject(
            "Img_FrontArt",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AspectRatioFitter));
        var artRect = artGo.GetComponent<RectTransform>();
        artRect.SetParent(rect, false);
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        frontArtImage = artGo.GetComponent<Image>();
        frontArtImage.raycastTarget = false;
        frontArtImage.maskable = true;
        frontArtImage.sprite = frontSprite;
        frontArtImage.color = Color.white;
        frontArtImage.preserveAspect = false;

        frontArtAspect = artGo.GetComponent<AspectRatioFitter>();
        frontArtAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        var tintGo = new GameObject(
            "Img_FrontTint",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var tintRect = tintGo.GetComponent<RectTransform>();
        tintRect.SetParent(rect, false);
        tintRect.anchorMin = Vector2.zero;
        tintRect.anchorMax = Vector2.one;
        tintRect.offsetMin = Vector2.zero;
        tintRect.offsetMax = Vector2.zero;

        frontTintImage = tintGo.GetComponent<Image>();
        frontTintImage.raycastTarget = false;
        frontTintImage.maskable = true;
        frontTintImage.sprite = GetBuiltInUiSprite();
        frontTintImage.color = new Color(0.08f, 0.10f, 0.14f, 0.65f);

        var frontFrameGo = new GameObject(
            "Img_FrontFrame",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var frontFrameRect = frontFrameGo.GetComponent<RectTransform>();
        frontFrameRect.SetParent(rect, false);
        frontFrameRect.anchorMin = Vector2.zero;
        frontFrameRect.anchorMax = Vector2.one;
        frontFrameRect.offsetMin = Vector2.zero;
        frontFrameRect.offsetMax = Vector2.zero;

        frontFrameImage = frontFrameGo.GetComponent<Image>();
        frontFrameImage.raycastTarget = false;
        frontFrameImage.maskable = false;
        frontFrameImage.sprite = frameSprite;
        frontFrameImage.color = Color.white;
        frontFrameImage.preserveAspect = true;

        var sigilGo = new GameObject("Img_FrontSigil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var sigilRect = sigilGo.GetComponent<RectTransform>();
        sigilRect.SetParent(rect, false);
        sigilRect.anchorMin = new Vector2(0.5f, 0.5f);
        sigilRect.anchorMax = new Vector2(0.5f, 0.5f);
        sigilRect.sizeDelta = new Vector2(140f, 140f);
        sigilRect.anchoredPosition = new Vector2(0f, 30f);
        sigilImage = sigilGo.GetComponent<Image>();
        sigilImage.raycastTarget = false;

        var labelGo = new GameObject("Txt_Element", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(260f, 120f);
        labelRect.anchoredPosition = new Vector2(0f, -70f);

        labelText = labelGo.GetComponent<TextMeshProUGUI>();
        labelText.raycastTarget = false;
        labelText.font = ResolveTmpFont();
        labelText.fontSize = 44f;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 34f;
        labelText.fontSizeMax = 52f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(0.99f, 0.96f, 0.84f, 0.98f);
        labelText.outlineWidth = 0.18f;
        labelText.outlineColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        return root;
    }

    private void UpdateOverlayLabel()
    {
        if (labelText != null)
        {
            labelText.text = elementLabel;
        }
    }

    private void UpdateOverlayDecoration()
    {
        if (frontArtImage != null)
        {
            frontArtImage.sprite = frontSprite;
            frontArtImage.color = Color.white;
            frontArtImage.enabled = frontArtImage.sprite != null;
        }

        if (frontTintImage != null)
        {
            frontTintImage.enabled = false;
        }

        if (frontFrameImage != null)
        {
            frontFrameImage.enabled = false;
        }

        if (sigilImage != null)
        {
            sigilImage.enabled = false;
        }
    }

    private void ApplyBackVisualState(bool showFront)
    {
        var showBack = !showFront;
        if (showBack && backSprite == null)
        {
            backSprite = LoadSprite(backSpritePath) ?? backSprite;
        }
        if (showBack && frameSprite == null)
        {
            frameSprite = LoadSprite(frameSpritePath) ?? frameSprite;
        }

        if (backImage != null)
        {
            ApplyBackCropRect(backImage.rectTransform);
            backImage.sprite = backSprite;
            backImage.color = BackTint;
            backImage.type = Image.Type.Simple;
            backImage.preserveAspect = false;
            backImage.raycastTarget = false;
            backImage.transform.SetSiblingIndex(0);
        }

        if (frameImage != null)
        {
            ResetToFillRect(frameImage.rectTransform, Vector2.zero, Vector2.zero);
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;
            frameImage.transform.SetAsLastSibling();
        }

        if (backRoot == null)
        {
            if (backImage != null)
            {
                backImage.enabled = showBack && backImage.sprite != null;
            }
            if (frameImage != null)
            {
                frameImage.enabled = showBack && frameImage.sprite != null;
            }

            return;
        }

        var graphics = backRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
        foreach (var graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            var isBackGraphic = graphic == backImage || string.Equals(graphic.name, "Img_Back", System.StringComparison.Ordinal);
            if (isBackGraphic)
            {
                if (graphic is Image backGraphicImage)
                {
                    backGraphicImage.type = Image.Type.Simple;
                    backGraphicImage.preserveAspect = false;
                    backGraphicImage.color = BackTint;
                    SetGraphicVisibility(backGraphicImage, showBack && backGraphicImage.sprite != null);
                }
                else
                {
                    SetGraphicVisibility(graphic, false);
                }

                continue;
            }

            var isFrameGraphic = graphic == frameImage || string.Equals(graphic.name, "Img_Frame", System.StringComparison.Ordinal);
            if (isFrameGraphic)
            {
                if (graphic is Image frameGraphicImage)
                {
                    frameGraphicImage.type = Image.Type.Simple;
                    frameGraphicImage.preserveAspect = false;
                    frameGraphicImage.color = Color.white;
                    SetGraphicVisibility(frameGraphicImage, showBack && frameGraphicImage.sprite != null);
                }
                else
                {
                    SetGraphicVisibility(graphic, false);
                }

                continue;
            }

            if (graphic is Image hiddenImage)
            {
                hiddenImage.sprite = null;
                hiddenImage.overrideSprite = null;
                hiddenImage.color = Color.clear;
            }

            SetGraphicVisibility(graphic, false);
        }
    }

    private void ApplyBackCropRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        var inset = GetBackCropInsetPixels();
        ResetToFillRect(rect, new Vector2(-inset, -inset), new Vector2(inset, inset));
    }

    private float GetBackCropInsetPixels()
    {
        if (string.IsNullOrWhiteSpace(elementLabel))
        {
            return DefaultBackCropInsetPixels;
        }

        var normalized = elementLabel.Trim().ToLowerInvariant();
        return normalized == "earth" ? EarthBackCropInsetPixels : DefaultBackCropInsetPixels;
    }

    private void ApplyFrontVisualState(bool showFront)
    {
        if (frontArtImage != null)
        {
            frontArtImage.sprite = frontSprite;
            frontArtImage.color = Color.white;
            frontArtImage.raycastTarget = false;
        }

        if (frontRoot != null)
        {
            var graphics = frontRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var graphic in graphics)
            {
                if (graphic == null)
                {
                    continue;
                }

                var isFrontArt = graphic == frontArtImage || string.Equals(graphic.name, "Img_FrontArt", System.StringComparison.Ordinal);
                if (isFrontArt)
                {
                    if (graphic is Image frontArtGraphic)
                    {
                        SetGraphicVisibility(frontArtGraphic, showFront && frontArtGraphic.sprite != null);
                    }
                    else
                    {
                        SetGraphicVisibility(graphic, false);
                    }

                    continue;
                }

                SetGraphicVisibility(graphic, false);
            }
        }

        if (frontTintImage != null)
        {
            frontTintImage.enabled = false;
            if (frontTintImage.gameObject.activeSelf)
            {
                frontTintImage.gameObject.SetActive(false);
            }
        }

        if (frontFrameImage != null)
        {
            frontFrameImage.enabled = false;
            if (frontFrameImage.gameObject.activeSelf)
            {
                frontFrameImage.gameObject.SetActive(false);
            }
        }

        if (sigilImage != null)
        {
            sigilImage.enabled = false;
            if (sigilImage.gameObject.activeSelf)
            {
                sigilImage.gameObject.SetActive(false);
            }
        }

        if (labelText != null)
        {
            labelText.enabled = false;
            if (labelText.gameObject.activeSelf)
            {
                labelText.gameObject.SetActive(false);
            }
        }
    }

    private void DisableLegacySiblingVisuals(bool showFront)
    {
        var keptRoot = showFront ? frontRoot : backRoot;
        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (keptRoot != null && child.gameObject == keptRoot)
            {
                continue;
            }

            var graphics = child.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var graphic in graphics)
            {
                if (graphic == null)
                {
                    continue;
                }

                SetGraphicVisibility(graphic, false);
            }

            if (child.gameObject.activeSelf && child.gameObject != keptRoot)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static void SetGraphicVisibility(Graphic graphic, bool visible)
    {
        if (graphic == null)
        {
            return;
        }

        graphic.enabled = visible;
        graphic.raycastTarget = false;
        if (graphic.gameObject.activeSelf != visible)
        {
            graphic.gameObject.SetActive(visible);
        }
    }

    private void UpdateFrontArtAspect()
    {
        if (frontArtAspect == null)
        {
            return;
        }

        if (frontSprite != null && frontSprite.rect.height > 0.01f)
        {
            frontArtAspect.aspectRatio = frontSprite.rect.width / frontSprite.rect.height;
        }
        else
        {
            // Default to a 2:3 card ratio if no sprite is available yet.
            frontArtAspect.aspectRatio = 2f / 3f;
        }
    }

    private void EnsureRootInteraction()
    {
        rootHitImage ??= gameObject.GetComponent<Image>();
        if (rootHitImage == null)
        {
            rootHitImage = gameObject.AddComponent<Image>();
        }

        rootHitImage.sprite = GetBuiltInUiSprite();
        rootHitImage.type = Image.Type.Simple;
        rootHitImage.color = new Color(0f, 0f, 0f, 0f);
        rootHitImage.raycastTarget = true;
        rootHitImage.maskable = false;

        rootButton ??= gameObject.GetComponent<Button>();
        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
            rootButton.enabled = false;
        }

#if UNITY_EDITOR
        var buttonState = rootButton != null
            ? $"Button.onClick runtime persistent={rootButton.onClick.GetPersistentEventCount()} enabled={rootButton.enabled}"
            : "Button.missing";
        Debug.LogWarning($"[ElementCardFlip] {name} wiring={buttonState}");
#endif
    }

    private void ConfigureChildRaycastTargets()
    {
        if (backImage != null)
        {
            backImage.raycastTarget = false;
        }

        if (frameImage != null)
        {
            frameImage.raycastTarget = false;
        }

        if (frontArtImage != null)
        {
            frontArtImage.raycastTarget = false;
        }

        if (frontTintImage != null)
        {
            frontTintImage.raycastTarget = false;
        }

        if (frontFrameImage != null)
        {
            frontFrameImage.raycastTarget = false;
        }

        if (sigilImage != null)
        {
            sigilImage.raycastTarget = false;
        }

        if (labelText != null)
        {
            labelText.raycastTarget = false;
        }
    }

    private TMP_FontAsset ResolveTmpFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

#if UNITY_EDITOR
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (fonts != null && fonts.Length > 0)
        {
            return fonts[0];
        }
#endif

        return null;
    }

    private static Sprite GetBuiltInUiSprite()
    {
        if (builtInUiSprite != null)
        {
            return builtInUiSprite;
        }

        builtInUiTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
        builtInUiTexture.SetPixel(0, 0, Color.white);
        builtInUiTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        builtInUiTexture.name = "ElementCardFlipPresenter_RuntimeUiTexture";
        builtInUiTexture.hideFlags = HideFlags.HideAndDontSave;

        builtInUiSprite = Sprite.Create(
            builtInUiTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 1f);
        builtInUiSprite.name = "ElementCardFlipPresenter_RuntimeUiSprite";
        builtInUiSprite.hideFlags = HideFlags.HideAndDontSave;

        return builtInUiSprite;
    }

    private void SyncRootRectToBack()
    {
        var rootRect = transform as RectTransform;
        var backRect = backImage != null ? backImage.rectTransform : null;
        if (rootRect == null || backRect == null)
        {
            return;
        }

        if (rootRect.rect.width <= 0.01f || rootRect.rect.height <= 0.01f)
        {
            rootRect.sizeDelta = backRect.sizeDelta;
        }
    }

#if UNITY_EDITOR
    private void LogSetupState()
    {
        var frontSpriteName = frontSprite != null ? frontSprite.name : "null";
        var backSpriteName = backSprite != null ? backSprite.name : "null";
        var backImageSpriteName = backImage != null && backImage.sprite != null ? backImage.sprite.name : "null";
        var frontArtSpriteName = frontArtImage != null && frontArtImage.sprite != null
            ? frontArtImage.sprite.name
            : "null";
        var frontPath = string.IsNullOrWhiteSpace(frontSpritePath) ? "null" : frontSpritePath;
        var backPath = string.IsNullOrWhiteSpace(backSpritePath) ? "null" : backSpritePath;
        var frontRootIndex = frontRoot != null ? frontRoot.transform.GetSiblingIndex().ToString() : "null";
        var backRootIndex = backRoot != null ? backRoot.transform.GetSiblingIndex().ToString() : "null";
        var frontArtIndex = frontArtImage != null ? frontArtImage.transform.GetSiblingIndex().ToString() : "null";
        var frameIndex = frameImage != null ? frameImage.transform.GetSiblingIndex().ToString() : "null";
        var frontArtParent = frontArtImage != null && frontArtImage.transform.parent != null
            ? frontArtImage.transform.parent.name
            : "null";
        var frameParent = frameImage != null && frameImage.transform.parent != null
            ? frameImage.transform.parent.name
            : "null";

        Debug.Log(
            $"[ElementCardFront] {name} frontSprite={frontSpriteName} frontArtSprite={frontArtSpriteName} backSprite={backSpriteName} backImageSprite={backImageSpriteName} frontPath={frontPath} backPath={backPath} frontRootIndex={frontRootIndex} backRootIndex={backRootIndex} frontArtIndex={frontArtIndex} frameIndex={frameIndex} frontArtParent={frontArtParent} frameParent={frameParent}");
    }
#endif

    private static Color GetElementTintColor(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return new Color(0.08f, 0.10f, 0.14f, 0.60f);
        }

        switch (label.Trim().ToLowerInvariant())
        {
            case "earth":
                return new Color(0.20f, 0.42f, 0.18f, 0.60f);
            case "water":
                return new Color(0.10f, 0.28f, 0.54f, 0.62f);
            case "air":
            case "wind":
                return new Color(0.38f, 0.40f, 0.52f, 0.58f);
            case "fire":
                return new Color(0.62f, 0.20f, 0.10f, 0.62f);
            default:
                return new Color(0.08f, 0.10f, 0.14f, 0.60f);
        }
    }

    private static Color GetFrontArtTint(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        switch (label.Trim().ToLowerInvariant())
        {
            case "earth":
                return new Color(0.72f, 0.92f, 0.72f, 1f);
            case "water":
                return new Color(0.70f, 0.85f, 1.00f, 1f);
            case "air":
            case "wind":
                return new Color(0.92f, 0.92f, 1.00f, 1f);
            case "fire":
                return new Color(1.00f, 0.76f, 0.62f, 1f);
            default:
                return new Color(0.85f, 0.85f, 0.85f, 1f);
        }
    }

    private static void ResetToFillRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private Sprite LoadSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

#if UNITY_EDITOR
        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture != null)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
#endif

        var diskTexture = TryLoadTextureFromDisk(assetPath);
        if (diskTexture == null)
        {
            return null;
        }

        return Sprite.Create(
            diskTexture,
            new Rect(0f, 0f, diskTexture.width, diskTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static Texture2D TryLoadTextureFromDisk(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal))
        {
            return null;
        }

        var relativePath = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (System.UnauthorizedAccessException)
        {
            return null;
        }

        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        if (!texture.LoadImage(bytes, markNonReadable: false))
        {
            Object.Destroy(texture);
            return null;
        }

        return texture;
    }
}
