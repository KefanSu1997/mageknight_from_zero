using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public static class ElementCardsShowcaseBootstrapper
{
    private const string SceneName = "Part1_ElementCardsShowcase";
    private const string ScenePathSuffix = "Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity";
    private sealed class ElementCardSpec
    {
        public ElementCardSpec(
            string cardName,
            string elementLabel,
            string titleHint,
            string frontArtPath,
            string backPath,
            string framePath)
        {
            CardName = cardName;
            ElementLabel = elementLabel;
            TitleHint = titleHint;
            FrontArtPath = frontArtPath;
            BackPath = backPath;
            FramePath = framePath;
        }

        public string CardName { get; }
        public string ElementLabel { get; }
        public string TitleHint { get; }
        public string FrontArtPath { get; }
        public string BackPath { get; }
        public string FramePath { get; }
    }

    private sealed class AutomationStepSpec
    {
        public AutomationStepSpec(ElementCardSpec cardSpec, bool showFront, string label)
        {
            CardSpec = cardSpec;
            ShowFront = showFront;
            Label = label;
        }

        public ElementCardSpec CardSpec { get; }
        public bool ShowFront { get; }
        public string Label { get; }
    }

    private static readonly ElementCardSpec[] CardSpecs =
    {
        new ElementCardSpec(
            "Card_Earth",
            "Earth",
            "Earth Tremor / Earth Quake",
            "Assets/GameData/cards/magic_003.png",
            "Assets/UI/Images/ElementCards/element_card_back_earth_imdream.png",
            "Assets/UI/Images/ElementCards/element_card_frame_earth_imdream.png"),
        new ElementCardSpec(
            "Card_Water",
            "Water",
            "Freeze / Deadly Freeze",
            "Assets/GameData/cards/magic_013.png",
            "Assets/UI/Images/ElementCards/element_card_back_water_imdream.png",
            "Assets/UI/Images/ElementCards/element_card_frame_water_imdream.png"),
        new ElementCardSpec(
            "Card_Wind",
            "Wind",
            "Wind Wings / Night Wings",
            "Assets/GameData/cards/magic_023.png",
            "Assets/UI/Images/ElementCards/element_card_back_air_imdream.png",
            "Assets/UI/Images/ElementCards/element_card_frame_air_imdream.png"),
        new ElementCardSpec(
            "Card_Fire",
            "Fire",
            "Fireball / Firestorm",
            "Assets/GameData/cards/magic_009.png",
            "Assets/UI/Images/ElementCards/element_card_back_fire_imdream.png",
            "Assets/UI/Images/ElementCards/element_card_frame_fire_imdream.png")
    };
    private static readonly AutomationStepSpec[] AutomationSteps = BuildAutomationSteps();

    private static readonly Dictionary<string, Vector2> BaseAnchoredPositions = new Dictionary<string, Vector2>();
    private static readonly Dictionary<string, Vector3> BaseScales = new Dictionary<string, Vector3>();
    private const string ManualCaptureTriggerRelativePath = "multi-agent-workspace/runs/T-20251028-020/manual_capture_trigger.flag";
    private static bool _frontShown;
    private static int _automationStepIndex = -1;
    private static bool _manualCaptureTriggeredThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
        {
            return;
        }

        EnsureSetup();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void OnEnterPlayMode(UnityEditor.EnterPlayModeOptions _)
    {
        _automationStepIndex = -1;
        _frontShown = true;
        _manualCaptureTriggeredThisSession = false;
        UnityEditor.EditorApplication.delayCall += BootstrapDuringPlayMode;
    }

    private static void BootstrapDuringPlayMode()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureSetup();
    }
#endif

    public static bool IsElementCardsScenePath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return false;
        }

        var normalized = scenePath.Replace('\\', '/');
        return normalized.EndsWith(ScenePathSuffix, System.StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureSetup()
    {
        foreach (var spec in CardSpecs)
        {
            SetupCard(spec);
        }

        _frontShown = true;
        _automationStepIndex = -1;
        SetAllFront(showFront: true);
        EnsureAutomationButton();
        HideAutomationStatusLabels();
        SetAutomationStatus("Scene Start");
        TryStartManualCaptureFromFileTrigger();
    }

    public static void SetAllFront(bool showFront)
    {
        foreach (var spec in CardSpecs)
        {
            ToggleCard(spec.CardName, showFront, isFocused: false);
        }

        _frontShown = showFront;
    }

    public static void SetExclusiveFront(string cardName)
    {
        foreach (var spec in CardSpecs)
        {
            var isFocused = string.Equals(spec.CardName, cardName, System.StringComparison.Ordinal);
            ToggleCard(spec.CardName, isFocused, isFocused);
        }

        _frontShown = true;
    }

    private static void SetupCard(ElementCardSpec spec)
    {
        var card = GameObject.Find(spec.CardName);
        if (card == null)
        {
            Debug.LogWarning($"ElementCardsShowcase: Card root not found: {spec.CardName}");
            return;
        }

        ApplyBackSprite(card, spec.BackPath);
        ApplyFrameSprite(card, spec.FramePath);

        var presenter = card.GetComponent<ElementCardFlipPresenter>();
        if (presenter == null)
        {
            presenter = card.AddComponent<ElementCardFlipPresenter>();
        }

        // Back uses the card back art; front uses the element art.
        presenter.Configure(spec.ElementLabel, spec.FrontArtPath, spec.BackPath, spec.FramePath, showFront: true);
        NormalizeBackLayout(card);
        LogCardBinding("setup", spec, card);
    }

    private static void ApplyFrameSprite(GameObject card, string framePath)
    {
#if UNITY_EDITOR
        var frameImage = FindChildImage(card.transform, "Img_Frame");
        if (frameImage == null)
        {
            return;
        }

        var frameSprite = LoadSpriteAtPath(framePath);
        if (frameSprite != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.color = Color.white;
        }
#endif
    }

    private static void ApplyBackSprite(GameObject card, string backPath)
    {
#if UNITY_EDITOR
        var backImage = FindChildImage(card.transform, "Img_Back");
        if (backImage == null)
        {
            return;
        }

        var backSprite = LoadSpriteAtPath(backPath);
        if (backSprite == null)
        {
            return;
        }

        backImage.sprite = backSprite;
        backImage.type = Image.Type.Simple;
        backImage.preserveAspect = false;
        backImage.color = Color.white;
#endif
    }

    private static Sprite LoadSpriteAtPath(string assetPath)
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

    private static void EnsureAutomationButton()
    {
        var buttonGo = GameObject.Find("AutomationButton");
        if (buttonGo == null)
        {
            return;
        }

        var image = buttonGo.GetComponent<Image>();
        if (image == null)
        {
            image = buttonGo.AddComponent<Image>();
        }

        image.sprite = null;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        image.maskable = false;

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
        {
            button = buttonGo.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(AdvanceAutomationStepFromAutomation);
    }

    public static void ToggleAllCards()
    {
        _frontShown = !_frontShown;
        _automationStepIndex = -1;
        SetAutomationStatus(_frontShown ? "All Front" : "All Back");
        Debug.Log($"[ElementCardsShowcase] ToggleAllCards frontShown={_frontShown}");

        foreach (var spec in CardSpecs)
        {
            ToggleCard(spec.CardName, _frontShown, isFocused: false);
        }
    }

    public static void AdvanceAutomationStepFromAutomation()
    {
        AdvanceAutomationStep();
    }

    private static void AdvanceAutomationStep()
    {
        if (AutomationSteps.Length == 0)
        {
            ToggleAllCards();
            return;
        }

        _automationStepIndex = (_automationStepIndex + 1) % AutomationSteps.Length;
        var step = AutomationSteps[_automationStepIndex];
        var targetCardName = step.CardSpec.CardName;
        var showFront = step.ShowFront;
        var label = step.Label;

        foreach (var spec in CardSpecs)
        {
            var isFocused = string.Equals(spec.CardName, targetCardName, System.StringComparison.Ordinal);
            var cardShowFront = isFocused && showFront;
            ToggleCard(spec.CardName, cardShowFront, isFocused);
        }

        _frontShown = showFront;
        SetAutomationStatus(label);
        LogCardBinding("step", step.CardSpec, GameObject.Find(step.CardSpec.CardName));
        Debug.Log(
            $"[ElementCardsShowcase] AutomationStep index={_automationStepIndex} label={label} targetCard={step.CardSpec.CardName} targetElement={step.CardSpec.ElementLabel} titleHint={step.CardSpec.TitleHint}");
    }

    private static AutomationStepSpec[] BuildAutomationSteps()
    {
        var steps = new List<AutomationStepSpec>(CardSpecs.Length * 2);
        foreach (var spec in CardSpecs)
        {
            steps.Add(new AutomationStepSpec(spec, showFront: false, label: $"{spec.ElementLabel} Back"));
            steps.Add(new AutomationStepSpec(spec, showFront: true, label: $"{spec.ElementLabel} Front"));
        }

        return steps.ToArray();
    }

    private static void LogCardBinding(string phase, ElementCardSpec spec, GameObject card)
    {
        if (spec == null)
        {
            return;
        }

        var presenter = card != null ? card.GetComponent<ElementCardFlipPresenter>() : null;
        var presenterState = presenter != null ? "present" : "missing";
#if UNITY_EDITOR
        var frontSprite = LoadSpriteAtPath(spec.FrontArtPath);
        var frontSpriteName = frontSprite != null ? frontSprite.name : "null";
#else
        var frontSpriteName = "runtime-unresolved";
#endif
        Debug.Log(
            $"[ElementCardsMapping] phase={phase} card={spec.CardName} element={spec.ElementLabel} frontPath={spec.FrontArtPath} frontSprite={frontSpriteName} titleHint={spec.TitleHint} backPath={spec.BackPath} framePath={spec.FramePath} presenter={presenterState}");
    }

    private static void ToggleCard(string cardName, bool showFront, bool isFocused)
    {
        var card = GameObject.Find(cardName);
        if (card == null)
        {
            return;
        }

        ApplyCardVisualTransform(cardName, card, showFront, isFocused);

        var presenter = card.GetComponent<ElementCardFlipPresenter>();
        if (presenter == null)
        {
            presenter = card.AddComponent<ElementCardFlipPresenter>();
        }

        presenter.SetFront(showFront);
        EnforceCardSideState(card, showFront);
    }

    private static void EnforceCardSideState(GameObject card, bool showFront)
    {
        if (card == null)
        {
            return;
        }

        var frontRoot = card.transform.Find("FrontRoot");
        var backRoot = card.transform.Find("BackRoot");
        SetRootVisible(frontRoot != null ? frontRoot.gameObject : null, showFront);
        SetRootVisible(backRoot != null ? backRoot.gameObject : null, !showFront);

        if (showFront && frontRoot != null)
        {
            frontRoot.SetAsLastSibling();
        }
        else if (!showFront && backRoot != null)
        {
            backRoot.SetAsLastSibling();
        }

        var frameImage = FindChildImage(card.transform, "Img_Frame");
        if (frameImage != null)
        {
            if (!showFront && !frameImage.gameObject.activeSelf)
            {
                frameImage.gameObject.SetActive(true);
            }

            frameImage.enabled = !showFront && frameImage.sprite != null;
            frameImage.raycastTarget = false;
            if (!showFront)
            {
                frameImage.transform.SetAsLastSibling();
            }
        }

        var backImage = FindChildImage(card.transform, "Img_Back");
        if (backImage != null)
        {
            if (!showFront && !backImage.gameObject.activeSelf)
            {
                backImage.gameObject.SetActive(true);
            }

            backImage.enabled = !showFront && backImage.sprite != null;
            backImage.raycastTarget = false;
            if (!showFront)
            {
                backImage.transform.SetAsLastSibling();
            }
        }

        if (!showFront && backRoot != null)
        {
            var backGraphics = backRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
            foreach (var graphic in backGraphics)
            {
                if (graphic == null)
                {
                    continue;
                }

                var isBackGraphic = backImage != null
                                    && (graphic == backImage
                                        || string.Equals(graphic.name, "Img_Back", System.StringComparison.Ordinal));
                if (isBackGraphic)
                {
                    graphic.enabled = backImage != null && backImage.sprite != null;
                    graphic.raycastTarget = false;
                    if (!graphic.gameObject.activeSelf)
                    {
                        graphic.gameObject.SetActive(true);
                    }

                    continue;
                }

                var isFrameGraphic = frameImage != null
                                     && (graphic == frameImage
                                         || string.Equals(graphic.name, "Img_Frame", System.StringComparison.Ordinal));
                if (isFrameGraphic)
                {
                    graphic.enabled = frameImage != null && frameImage.sprite != null;
                    graphic.raycastTarget = false;
                    if (!graphic.gameObject.activeSelf)
                    {
                        graphic.gameObject.SetActive(true);
                    }

                    continue;
                }

                if (graphic is Image hiddenImage)
                {
                    hiddenImage.sprite = null;
                    hiddenImage.overrideSprite = null;
                    hiddenImage.color = Color.clear;
                }

                graphic.enabled = false;
                graphic.raycastTarget = false;
                if (graphic.gameObject.activeSelf)
                {
                    graphic.gameObject.SetActive(false);
                }
            }
        }

        ForceHideRootGraphic(card);
        DisableNonTargetGraphics(card.transform, frontRoot, backRoot, showFront);
    }

    private static void ForceHideRootGraphic(GameObject card)
    {
        if (card == null)
        {
            return;
        }

        var rootImage = card.GetComponent<Image>();
        if (rootImage == null)
        {
            return;
        }

        rootImage.enabled = true;
        rootImage.sprite = null;
        rootImage.type = Image.Type.Simple;
        rootImage.preserveAspect = false;
        rootImage.color = new Color(0f, 0f, 0f, 0f);
        rootImage.raycastTarget = true;
    }

    private static void DisableNonTargetGraphics(Transform cardRoot, Transform frontRoot, Transform backRoot, bool showFront)
    {
        if (cardRoot == null)
        {
            return;
        }

        var keepRoot = showFront ? frontRoot : backRoot;
        var graphics = cardRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
        if (graphics == null)
        {
            return;
        }

        foreach (var graphic in graphics)
        {
            if (graphic == null || graphic.gameObject == cardRoot.gameObject)
            {
                continue;
            }

            var withinKeepRoot = keepRoot != null
                                 && (graphic.transform == keepRoot || graphic.transform.IsChildOf(keepRoot));
            if (withinKeepRoot)
            {
                var keepGraphic = showFront
                    ? string.Equals(graphic.name, "Img_FrontArt", System.StringComparison.Ordinal)
                    : string.Equals(graphic.name, "Img_Back", System.StringComparison.Ordinal)
                      || string.Equals(graphic.name, "Img_Frame", System.StringComparison.Ordinal);
                graphic.enabled = keepGraphic;
                graphic.raycastTarget = false;
                if (graphic.gameObject.activeSelf != keepGraphic)
                {
                    graphic.gameObject.SetActive(keepGraphic);
                }

                continue;
            }

            graphic.enabled = false;
            graphic.raycastTarget = false;
            if (graphic.gameObject.activeSelf)
            {
                graphic.gameObject.SetActive(false);
            }
        }
    }

    private static void SetRootVisible(GameObject root, bool visible)
    {
        if (root == null)
        {
            return;
        }

        var group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        if (root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    private static void ApplyCardVisualTransform(string cardName, GameObject card, bool showFront, bool isFocused)
    {
        var rect = card.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        if (!BaseAnchoredPositions.ContainsKey(cardName))
        {
            BaseAnchoredPositions[cardName] = rect.anchoredPosition;
        }

        if (!BaseScales.ContainsKey(cardName))
        {
            BaseScales[cardName] = rect.localScale;
        }

        var basePos = BaseAnchoredPositions[cardName];
        var baseScale = BaseScales[cardName];

        if (showFront)
        {
            rect.anchoredPosition = basePos + new Vector2(0f, 26f);
            rect.localScale = baseScale * 1.08f;
        }
        else if (isFocused)
        {
            rect.anchoredPosition = basePos + new Vector2(0f, 10f);
            rect.localScale = baseScale * 1.04f;
        }
        else
        {
            rect.anchoredPosition = basePos;
            rect.localScale = baseScale;
        }
    }

    private static void NormalizeBackLayout(GameObject card)
    {
        var backRoot = card.transform.Find("BackRoot") as RectTransform;
        if (backRoot != null)
        {
            backRoot.anchorMin = Vector2.zero;
            backRoot.anchorMax = Vector2.one;
            backRoot.offsetMin = Vector2.zero;
            backRoot.offsetMax = Vector2.zero;
            backRoot.localScale = Vector3.one;
            backRoot.localRotation = Quaternion.identity;
        }

        NormalizeBackImage(FindChildImage(card.transform, "Img_Back"));
        NormalizeBackImage(FindChildImage(card.transform, "Img_Frame"));
    }

    private static void NormalizeBackImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;

        var rect = image.rectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static Image FindChildImage(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        var directChild = root.Find(childName);
        if (directChild != null)
        {
            return directChild.GetComponent<Image>();
        }

        var images = root.GetComponentsInChildren<Image>(includeInactive: true);
        if (images == null)
        {
            return null;
        }

        foreach (var image in images)
        {
            if (image != null && string.Equals(image.name, childName, System.StringComparison.Ordinal))
            {
                return image;
            }
        }

        return null;
    }

    private static void SetAutomationStatus(string status)
    {
        var safeStatus = string.IsNullOrWhiteSpace(status) ? "Unknown" : status.Trim();
        HideAutomationStatusLabels();
        Debug.Log($"[ElementCardsShowcase] State={safeStatus}");
    }

    private static void HideAutomationStatusLabels()
    {
        var labels = Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
        if (labels == null || labels.Length == 0)
        {
            return;
        }

        foreach (var label in labels)
        {
            if (label == null || label.gameObject == null)
            {
                continue;
            }

            var objectName = label.gameObject.name;
            var textValue = label.text;
            var nameMatch = objectName.StartsWith("AutomationStatusLabel", System.StringComparison.Ordinal);
            var textMatch = !string.IsNullOrWhiteSpace(textValue)
                && textValue.StartsWith("State:", System.StringComparison.OrdinalIgnoreCase);
            if (!nameMatch && !textMatch)
            {
                continue;
            }

            label.enabled = false;
            if (label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(false);
            }
        }
    }

    private static void TryStartManualCaptureFromFileTrigger()
    {
#if UNITY_EDITOR
        if (_manualCaptureTriggeredThisSession)
        {
            return;
        }

        var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        var triggerPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, ManualCaptureTriggerRelativePath));
        if (!System.IO.File.Exists(triggerPath))
        {
            return;
        }

        if (MageKnight.SceneAutomation.SceneAutomationRuntimeState.PendingRequest != null)
        {
            MageKnight.SceneAutomation.SceneAutomationRuntimeState.Clear();
            Debug.LogWarning("[ElementCardsShowcase] Cleared stale SceneAutomation pending request before manual capture trigger.");
        }

        _manualCaptureTriggeredThisSession = true;
        System.IO.File.Delete(triggerPath);

        var runner = Object.FindFirstObjectByType<MageKnight.SceneAutomation.ElementCardsManualCaptureRunner>();
        if (runner == null)
        {
            var host = new GameObject("ElementCardsManualCaptureRunner");
            runner = host.AddComponent<MageKnight.SceneAutomation.ElementCardsManualCaptureRunner>();
        }

        runner.Begin();
        Debug.Log("[ElementCardsShowcase] Manual capture trigger consumed.");
#endif
    }
}
