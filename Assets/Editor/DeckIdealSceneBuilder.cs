using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageKnight.EditorTools
{
    /// <summary>
    /// Generates the ideal deck static composition and exports acceptance screenshots.
    /// Builds the full composition from independent uGUI elements (no single full-frame composite image).
    /// </summary>
    public static class DeckIdealSceneBuilder
    {
        private const string BuildSignature = "2026-01-05_restore_deck_layout_v2";
        private const string ScenePath = "Assets/Scenes/Part1/Part1_DeckIdeal.unity";
        private const int DiscardScatterSeed = 1018;
        private const int IdealDeckCount = 30;
        private const int IdealDiscardCount = 0;

        private static readonly Vector2 BaseResolution = new(1920f, 1080f);
        private static readonly Vector2 ReferenceResolution = new(1378f, 1204f);
        private static readonly float UniformScale = ReferenceResolution.x / BaseResolution.x;
        private static readonly float VerticalScale = ReferenceResolution.y / BaseResolution.y;

        private static readonly Vector2[] HandCardCenters = ScaleFromBase(new[]
        {
            new Vector2(-594f, -280f),
            new Vector2(-280f, -280f),
            new Vector2(6f, -280f),
            new Vector2(286f, -280f),
            new Vector2(622f, -280f)
        });
        private static readonly Vector2 HandCardSize = ScaleSizeUniform(new Vector2(260f, 366f));
        private static readonly Vector2 PileCardSize = ScaleSizeUniform(new Vector2(220f, 304f));

        private static readonly string[] ScreenshotTargets =
        {
            "multi-agent-workspace/runs/T-20251028-012/review_bundle/artifacts/screenshots/deck_ideal_overview.png"
        };

        private const string ThemeBackgroundPath = "Assets/UI/Images/DeckTheme/Ideal/Candidates/Board/deck_ideal_board_candidate_20260104_f.png";
        private const string ThemeBorderPath = "Assets/UI/Images/DeckTheme/deckui_border.jpg";
        private const string ThemeMagicCirclePath = "Assets/UI/Images/DeckTheme/deckui_magic_circle.jpg";
        private const string ThemeHighlightPath = "Assets/UI/Images/DeckTheme/deckui_card_highlight.jpg";
        private const string ThemeCardBackPath = "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardBack/deck_ideal_card_back_candidate_20260104_f.png";
        private static readonly string[] ThemeCardFacePaths =
        {
            "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_a.png",
            "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_b.png",
            "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_c.png",
            "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_d.png",
            "Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/deck_ideal_card_face_candidate_20260104_e.png"
        };

        private static readonly Color DeckStackTint = new(1f, 1f, 1f, 0.96f);
        private static readonly Color DiscardStackTint = new(1f, 1f, 1f, 0.9f);
        private static readonly Color FrameColor = new(0.9f, 0.94f, 1f, 0.38f);
        private static readonly Color FiligreeColor = new(0.94f, 0.95f, 1f, 0.36f);
        private static readonly Color GemColor = new(0.88f, 0.94f, 1f, 0.52f);
        private static readonly Color BackgroundTint = new(0f, 0f, 0f, 0.3f);
        private static readonly Color MagicCircleColor = new(0.62f, 0.82f, 1f, 0f);
        private static readonly Color SwirlColor = new(1f, 0.82f, 0.46f, 0f);
        private static readonly Color CardGlowColor = new(1f, 0.86f, 0.52f, 0.22f);
        private static readonly Color SlotTint = new(0.08f, 0.1f, 0.14f, 0.82f);

        private static Sprite _whiteSprite;

        private struct ThemeSprites
        {
            public Sprite Background;
            public Sprite Foreground;
            public Sprite Border;
            public Sprite Highlight;
            public Sprite MagicCircle;
            public Sprite CardBack;
            public Sprite[] CardFaces;
            public Sprite Monster;
            public Color BackgroundColor;
            public Color AccentColor;
            public Color SecondaryAccentColor;
        }

        [MenuItem("Tools/Scene Builders/Build + Capture Deck Ideal")]
        public static void BuildAndCaptureMenu()
        {
            Debug.Log($"[DeckIdealSceneBuilder] BuildSignature: {BuildSignature}");
            BuildScene();
            CaptureScreenshot();
        }

        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            var camera = CreateCamera();
            var canvas = CreateCanvas(camera);
            EnsureEventSystem();

            var theme = BuildTheme();
            camera.backgroundColor = theme.BackgroundColor;
            BuildStaticLayout(canvas, theme);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[DeckIdealSceneBuilder] Scene saved to {ScenePath}");
        }

        public static void CaptureScreenshot()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[DeckIdealSceneBuilder] No camera found for screenshot.");
                return;
            }

            var bytes = CaptureCamera(camera);
            WriteScreenshots(bytes, ScreenshotTargets);
        }

        private static Camera CreateCamera()
        {
            var cameraGo = new GameObject("DeckUICamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = ReferenceResolution.y * 0.5f;
            camera.aspect = ReferenceResolution.x / ReferenceResolution.y;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            // Keep capture deterministic: HDR/MSAA can introduce subtle resolve differences when rendering UI to RT.
            camera.allowHDR = false;
            camera.allowMSAA = false;
            return camera;
        }

        private static Canvas CreateCanvas(Camera camera)
        {
            var canvasGo = new GameObject("UIRoot", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ReferenceResolution;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static ThemeSprites BuildTheme()
        {
            EnsureSpriteImport(ThemeBackgroundPath);
            AssetDatabase.ImportAsset(ThemeBackgroundPath, ImportAssetOptions.ForceUpdate);
            var background = LoadSpriteLoose(ThemeBackgroundPath) ?? DeckUiThemeCache.BackgroundSprite;

            EnsureSpriteImport(ThemeBorderPath);
            AssetDatabase.ImportAsset(ThemeBorderPath, ImportAssetOptions.ForceUpdate);
            var border = LoadSpriteLoose(ThemeBorderPath) ?? DeckUiThemeCache.BorderSprite;

            EnsureSpriteImport(ThemeMagicCirclePath);
            AssetDatabase.ImportAsset(ThemeMagicCirclePath, ImportAssetOptions.ForceUpdate);
            var magicCircle = LoadSpriteLoose(ThemeMagicCirclePath) ?? DeckUiThemeCache.MagicCircleSprite;

            EnsureSpriteImport(ThemeCardBackPath);
            AssetDatabase.ImportAsset(ThemeCardBackPath, ImportAssetOptions.ForceUpdate);
            var cardBack = LoadSpriteLoose(ThemeCardBackPath);

            cardBack ??= DeckUiThemeCache.CardBackSprite;
            if (cardBack == null)
            {
                Debug.LogError($"[DeckIdealSceneBuilder] Card back sprite missing at path: {ThemeCardBackPath}");
            }

            EnsureSpriteImport(ThemeHighlightPath);
            AssetDatabase.ImportAsset(ThemeHighlightPath, ImportAssetOptions.ForceUpdate);
            var highlight = LoadSpriteLoose(ThemeHighlightPath) ?? DeckUiThemeCache.HighlightSprite;

            return new ThemeSprites
            {
                Background = background,
                Border = border,
                Highlight = highlight,
                MagicCircle = magicCircle,
                CardBack = cardBack,
                CardFaces = LoadCardFaceSprites(),
                Monster = null,
                BackgroundColor = new Color(0.18f, 0.08f, 0.28f, 1f),
                AccentColor = new Color(0.48f, 0.64f, 0.98f, 1f),
                SecondaryAccentColor = new Color(1f, 0.82f, 0.52f, 1f)
            };
        }

        private static Sprite[] LoadCardFaceSprites()
        {
            if (ThemeCardFacePaths == null || ThemeCardFacePaths.Length == 0)
            {
                return null;
            }

            var faces = new List<Sprite>(ThemeCardFacePaths.Length);
            foreach (var path in ThemeCardFacePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var sprite = LoadSpriteLoose(path);
                if (sprite != null)
                {
                    faces.Add(sprite);
                }
            }

            return faces.Count > 0 ? faces.ToArray() : null;
        }

        private static void BuildStaticLayout(Canvas canvas, ThemeSprites theme)
        {
            var root = canvas.GetComponent<RectTransform>();
            foreach (Transform child in root)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            BuildBackground(root, theme);
            BuildCenterStage(root, theme);
            BuildCornerShelves(root, theme);
            BuildBottomBand(root, theme);

            var highlightRoot = CreateRect("HighlightRoot", root);
            highlightRoot.anchorMin = Vector2.zero;
            highlightRoot.anchorMax = Vector2.one;
            highlightRoot.offsetMin = Vector2.zero;
            highlightRoot.offsetMax = Vector2.zero;
            highlightRoot.pivot = new Vector2(0.5f, 0.5f);
            var highlightGroups = BuildHighlightOverlays(highlightRoot, theme);

            BuildDeckStack(root, theme);
            BuildDiscardPile(root, theme);
            BuildHandZone(root, theme);
            BuildBorder(root, theme);

            var overlayRoot = CreateRect("Overlay", root);
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
            overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            overlayRoot.SetAsLastSibling();
            BuildCountPlaques(overlayRoot, theme);
            BuildInteractionHitboxes(overlayRoot, highlightGroups);
        }

        private static Vector2 PixelToAnchoredPosition(Vector2 pixel)
        {
            // Convert from top-left pixel coordinates into Canvas anchoredPosition coordinates (origin at center).
            return new Vector2(pixel.x - (ReferenceResolution.x * 0.5f), (ReferenceResolution.y * 0.5f) - pixel.y);
        }

        private static void BuildCountPlaques(RectTransform parent, ThemeSprites theme)
        {
            var highlight = theme.Highlight ?? theme.Border;
            var accent = theme.SecondaryAccentColor;

            // Positions are tuned for the 1378x1204 DeckIdeal composition (top-left deck pile, top-right discard pile).
            CreateCountPlaque(parent,
                "DeckCountPlaque",
                highlight,
                "DECK",
                IdealDeckCount,
                accent,
                new Vector2(0.5f, 0.5f),
                PixelToAnchoredPosition(new Vector2(212f, 410f)));

            CreateCountPlaque(parent,
                "DiscardCountPlaque",
                highlight,
                "DISCARD",
                IdealDiscardCount,
                accent,
                new Vector2(0.5f, 0.5f),
                PixelToAnchoredPosition(new Vector2(1166f, 410f)));
        }

        private static void BuildBackground(RectTransform root, ThemeSprites theme)
        {
            var bg = CreateImage("TableBackground", root, theme.Background, Vector2.zero, Vector2.zero);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;
            bg.preserveAspect = false;
            bg.raycastTarget = false;
            bg.color = Color.white;
        }

        private static void BuildInteractionHitboxes(RectTransform root)
        {
            BuildInteractionHitboxes(root, Array.Empty<CanvasGroup>());
        }

        private static CanvasGroup[] BuildHighlightOverlays(RectTransform root, ThemeSprites theme)
        {
            var borderSprite = theme.Border ?? theme.Highlight;
            var glowSprite = theme.Border ?? theme.Highlight;
            if (borderSprite == null && glowSprite == null)
            {
                return Array.Empty<CanvasGroup>();
            }

            var deck = CreateHighlight(root,
                "DeckHighlight",
                borderSprite,
                glowSprite,
                theme.AccentColor,
                0f,
                PixelToAnchoredPosition(new Vector2(214f, 286f)),
                ScaleSizeUniform(new Vector2(420f, 320f)));

            var discard = CreateHighlight(root,
                "DiscardHighlight",
                borderSprite,
                glowSprite,
                theme.SecondaryAccentColor,
                0f,
                PixelToAnchoredPosition(new Vector2(1164f, 286f)),
                ScaleSizeUniform(new Vector2(420f, 320f)));

            var handTint = Color.Lerp(theme.AccentColor, theme.SecondaryAccentColor, 0.45f);
            var hand = CreateHighlight(root,
                "HandHighlight",
                borderSprite,
                glowSprite,
                handTint,
                0f,
                PixelToAnchoredPosition(new Vector2(689f, 960f)),
                ScaleSizeUniform(new Vector2(1220f, 420f)));

            return new[] { deck, discard, hand };
        }

        private static CanvasGroup CreateHighlight(
            RectTransform parent,
            string name,
            Sprite borderSprite,
            Sprite glowSprite,
            Color tint,
            float alpha,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var root = CreateRect(name, parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = size;

            if (glowSprite != null)
            {
                var outerGlow = CreateImage("OuterGlow", root, glowSprite, Vector2.zero, Vector2.zero);
                outerGlow.rectTransform.anchorMin = Vector2.zero;
                outerGlow.rectTransform.anchorMax = Vector2.one;
                outerGlow.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(-28f, -28f));
                outerGlow.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(-28f, -28f));
                outerGlow.type = Image.Type.Sliced;
                outerGlow.fillCenter = false;
                outerGlow.color = new Color(tint.r, tint.g, tint.b, 0.24f);
                outerGlow.raycastTarget = false;
                var outerOutline = outerGlow.gameObject.AddComponent<Outline>();
                outerOutline.effectColor = new Color(tint.r, tint.g, tint.b, 0.28f);
                outerOutline.effectDistance = new Vector2(6f, -6f);

                var innerGlow = CreateImage("InnerGlow", root, glowSprite, Vector2.zero, Vector2.zero);
                innerGlow.rectTransform.anchorMin = Vector2.zero;
                innerGlow.rectTransform.anchorMax = Vector2.one;
                innerGlow.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(-10f, -10f));
                innerGlow.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(-10f, -10f));
                innerGlow.type = Image.Type.Sliced;
                innerGlow.fillCenter = false;
                innerGlow.color = new Color(tint.r, tint.g, tint.b, 0.36f);
                innerGlow.raycastTarget = false;
            }

            if (borderSprite != null)
            {
                var border = CreateImage("Border", root, borderSprite, Vector2.zero, Vector2.zero);
                border.rectTransform.anchorMin = Vector2.zero;
                border.rectTransform.anchorMax = Vector2.one;
                border.rectTransform.offsetMin = Vector2.zero;
                border.rectTransform.offsetMax = Vector2.zero;
                border.type = Image.Type.Sliced;
                border.fillCenter = false;
                var baseAlpha = alpha <= 0f ? 0.85f : Mathf.Clamp01(alpha);
                border.color = new Color(tint.r, tint.g, tint.b, baseAlpha);
                border.raycastTarget = false;
            }

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static void BuildInteractionHitboxes(RectTransform root, CanvasGroup[] highlightGroups)
        {
            CreateHitbox(root, "DeckHitbox", PixelToAnchoredPosition(new Vector2(220f, 250f)), ScaleSizeUniform(new Vector2(360f, 300f)), highlightGroups, 0);
            CreateHitbox(root, "DiscardHitbox", PixelToAnchoredPosition(new Vector2(1160f, 250f)), ScaleSizeUniform(new Vector2(360f, 300f)), highlightGroups, 1);
            CreateHitbox(root, "HandHitbox", PixelToAnchoredPosition(new Vector2(689f, 960f)), ScaleSizeUniform(new Vector2(1220f, 420f)), highlightGroups, 2);
        }

        private static void CreateHitbox(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, CanvasGroup[] highlightGroups, int highlightIndex)
        {
            var hitbox = CreateRect(name, parent);
            hitbox.anchorMin = new Vector2(0.5f, 0.5f);
            hitbox.anchorMax = new Vector2(0.5f, 0.5f);
            hitbox.pivot = new Vector2(0.5f, 0.5f);
            hitbox.anchoredPosition = anchoredPosition;
            hitbox.sizeDelta = size;

            hitbox.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var hitImage = hitbox.gameObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            if (highlightGroups != null && highlightIndex >= 0 && highlightIndex < highlightGroups.Length)
            {
                var highlighter = hitbox.gameObject.AddComponent<DeckIdealHitboxHighlighter>();
                highlighter.Setup(highlightGroups[highlightIndex], highlightGroups);
            }
        }

        private static void BuildBorder(RectTransform root, ThemeSprites theme)
        {
            if (theme.Border == null)
            {
                return;
            }

            var borderPadding = ScaleSizeUniform(new Vector2(18f, 18f));

            var border = CreateImage("Border", root, theme.Border, Vector2.zero, Vector2.zero);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = borderPadding;
            border.rectTransform.offsetMax = -borderPadding;
            border.type = Image.Type.Sliced;
            border.fillCenter = false;
            border.color = FrameColor;
            border.raycastTarget = false;

            if (theme.Border != null)
            {
                var borderHighlight = CreateImage("BorderHighlight", root, theme.Border, Vector2.zero, Vector2.zero);
                borderHighlight.rectTransform.anchorMin = Vector2.zero;
                borderHighlight.rectTransform.anchorMax = Vector2.one;
                borderHighlight.rectTransform.offsetMin = borderPadding + ScaleSizeUniform(new Vector2(8f, 8f));
                borderHighlight.rectTransform.offsetMax = -(borderPadding + ScaleSizeUniform(new Vector2(8f, 8f)));
                borderHighlight.type = Image.Type.Sliced;
                borderHighlight.fillCenter = false;
                borderHighlight.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0.16f);
                borderHighlight.raycastTarget = false;
            }
        }

        private static void BuildCenterStage(RectTransform root, ThemeSprites theme)
        {
            if (theme.MagicCircle == null)
            {
                return;
            }

            var center = CreateRect("CenterStage", root);
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.anchoredPosition = new Vector2(0f, ScaleFromBase(new Vector2(0f, 64f)).y);
            center.sizeDelta = ScaleSizeUniform(new Vector2(920f, 920f));

            var circle = CreateImage("MagicCircleOverlay", center, theme.MagicCircle, Vector2.zero, Vector2.zero);
            circle.rectTransform.anchorMin = Vector2.zero;
            circle.rectTransform.anchorMax = Vector2.one;
            circle.rectTransform.offsetMin = Vector2.zero;
            circle.rectTransform.offsetMax = Vector2.zero;
            circle.preserveAspect = true;
            circle.type = Image.Type.Sliced;
            circle.fillCenter = false;
            circle.color = MagicCircleColor;
            circle.enabled = false;
            circle.raycastTarget = false;

            if (theme.Highlight != null)
            {
                var swirl = CreateImage("MagicSwirl", center, theme.Highlight, Vector2.zero, Vector2.zero);
                swirl.rectTransform.anchorMin = Vector2.zero;
                swirl.rectTransform.anchorMax = Vector2.one;
                swirl.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(60f, 60f));
                swirl.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(60f, 60f));
                swirl.type = Image.Type.Sliced;
                swirl.fillCenter = false;
                swirl.color = SwirlColor;
                swirl.enabled = false;
                swirl.raycastTarget = false;
            }

            if (theme.CardBack != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    var card = CreateImage($"FloatingCard_{i}", center, theme.CardBack, ScaleSizeUniform(new Vector2(180f, 252f)), Vector2.zero);
                    card.preserveAspect = true;
                    card.raycastTarget = false;
                    card.color = new Color(DeckStackTint.r, DeckStackTint.g, DeckStackTint.b, 0.78f);
                    card.rectTransform.anchoredPosition = ScaleFromBase(new Vector2((i - 1) * 150f, 40f + i * 36f));
                    card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 8f);
                }
            }

            AddSparkles(root, theme, new Vector2(0f, 32f));
        }

        private static void AddSparkles(RectTransform root, ThemeSprites theme, Vector2 centerOffset)
        {
            if (theme.Highlight == null)
            {
                return;
            }

            var sparkles = CreateRect("Sparkles", root);
            sparkles.anchorMin = new Vector2(0.5f, 0.5f);
            sparkles.anchorMax = new Vector2(0.5f, 0.5f);
            sparkles.pivot = new Vector2(0.5f, 0.5f);
            sparkles.anchoredPosition = ScaleFromBase(centerOffset);
            sparkles.sizeDelta = ScaleSizeUniform(new Vector2(980f, 980f));

            var rng = new System.Random(DiscardScatterSeed);
            var sparkleCount = 26;
            for (int i = 0; i < sparkleCount; i++)
            {
                var radius = Mathf.Lerp(80f, 460f, (float)rng.NextDouble());
                var angle = Mathf.Lerp(0f, 360f, (float)rng.NextDouble());
                var rad = angle * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

                var size = Mathf.Lerp(14f, 42f, (float)rng.NextDouble());
                var alpha = Mathf.Lerp(0.06f, 0.22f, (float)rng.NextDouble());
                var color = (i % 3 == 0) ? theme.SecondaryAccentColor : theme.AccentColor;
                var tint = new Color(color.r, color.g, color.b, alpha);

                var sparkle = CreateImage($"Sparkle_{i}", sparkles, theme.Highlight, ScaleSizeUniform(new Vector2(size, size)), ScaleFromBase(pos));
                sparkle.type = Image.Type.Sliced;
                sparkle.raycastTarget = false;
                sparkle.color = tint;
            }
        }

        private static void BuildDeckStack(RectTransform parent, ThemeSprites theme)
        {
            if (theme.CardBack == null)
            {
                return;
            }

            var root = CreateRect("DeckStackOverlay", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = PixelToAnchoredPosition(new Vector2(214f, 286f));
            root.sizeDelta = ScaleSizeUniform(new Vector2(420f, 320f));

            var shadowLayer = CreateRect("Shadows", root);
            shadowLayer.anchorMin = Vector2.zero;
            shadowLayer.anchorMax = Vector2.one;
            shadowLayer.offsetMin = Vector2.zero;
            shadowLayer.offsetMax = Vector2.zero;

            var cardLayer = CreateRect("Cards", root);
            cardLayer.anchorMin = Vector2.zero;
            cardLayer.anchorMax = Vector2.one;
            cardLayer.offsetMin = Vector2.zero;
            cardLayer.offsetMax = Vector2.zero;

            var count = Mathf.Clamp(IdealDeckCount / 6, 5, 8);
            for (int i = 0; i < count; i++)
            {
                var t = count <= 1 ? 1f : i / (count - 1f);
                var offset = new Vector2(i * 14f, -i * 10f) + new Vector2(-42f, 34f);
                var rotation = Mathf.Lerp(-8f, 6f, t);
                var alpha = Mathf.Lerp(0.94f, 0.72f, t);
                var scale = Mathf.Lerp(1.02f, 0.96f, t);
                CreateDiscardCardWithShadow(
                    shadowLayer,
                    cardLayer,
                    $"DeckStack_{i}",
                    theme.CardBack,
                    PileCardSize,
                    offset,
                    rotation,
                    scale,
                    alpha,
                    Mathf.Lerp(0.22f, 0.12f, t),
                    new Vector2(10f, -12f));
            }
        }

        private static void BuildDiscardPile(RectTransform parent, ThemeSprites theme)
        {
            if (theme.CardBack == null)
            {
                return;
            }

            var root = CreateRect("DiscardStackOverlay", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = PixelToAnchoredPosition(new Vector2(1164f, 286f));
            root.sizeDelta = ScaleSizeUniform(new Vector2(420f, 320f));

            var shadowLayer = CreateRect("Shadows", root);
            shadowLayer.anchorMin = Vector2.zero;
            shadowLayer.anchorMax = Vector2.one;
            shadowLayer.offsetMin = Vector2.zero;
            shadowLayer.offsetMax = Vector2.zero;

            var cardLayer = CreateRect("Cards", root);
            cardLayer.anchorMin = Vector2.zero;
            cardLayer.anchorMax = Vector2.one;
            cardLayer.offsetMin = Vector2.zero;
            cardLayer.offsetMax = Vector2.zero;

            // Deterministic scatter that reads as "discard": semi-transparent, messy overlap, slight tilt, layered shadows.
            // Avoid runtime randomness so automation screenshots remain pixel-stable.
            var rng = new System.Random(DiscardScatterSeed);
            var cardCount = Mathf.Clamp(IdealDiscardCount, 4, 7);
            var cardSize = PileCardSize;

            for (int i = 0; i < cardCount; i++)
            {
                var t = cardCount <= 1 ? 1f : i / (cardCount - 1f);

                var offset = new Vector2(
                    Mathf.Lerp(-72f, 64f, (float)rng.NextDouble()) + Mathf.Lerp(-10f, 12f, t),
                    Mathf.Lerp(34f, -46f, (float)rng.NextDouble()) + Mathf.Lerp(8f, -14f, t));

                var rotation = Mathf.Lerp(-26f, 20f, (float)rng.NextDouble()) + Mathf.Lerp(-5f, 6f, t);
                var scale = Mathf.Lerp(0.96f, 1.04f, (float)rng.NextDouble());

                var alphaBase = Mathf.Lerp(0.22f, 0.44f, t);
                var alphaJitter = Mathf.Lerp(-0.02f, 0.02f, (float)rng.NextDouble());
                var alpha = Mathf.Clamp01(alphaBase + alphaJitter);

                var shadowAlpha = Mathf.Lerp(0.16f, 0.26f, t);
                var shadowOffset = new Vector2(
                    Mathf.Lerp(8f, 14f, (float)rng.NextDouble()),
                    Mathf.Lerp(-14f, -8f, (float)rng.NextDouble()));

                CreateDiscardCardWithShadow(
                    shadowLayer,
                    cardLayer,
                    $"DiscardScatter_{i}",
                    theme.CardBack,
                    cardSize,
                    offset,
                    rotation,
                    scale,
                    alpha,
                    shadowAlpha,
                    shadowOffset);
            }
        }

        private static void BuildCornerShelves(RectTransform root, ThemeSprites theme)
        {
            if (theme.Highlight == null)
            {
                return;
            }

            BuildShelf(root, theme, "DeckShelf", PixelToAnchoredPosition(new Vector2(256f, 224f)));
            BuildShelf(root, theme, "DiscardShelf", PixelToAnchoredPosition(new Vector2(1122f, 224f)));
        }

        private static void BuildShelf(RectTransform root, ThemeSprites theme, string name, Vector2 anchoredPosition)
        {
            var shelf = CreateRect(name, root);
            shelf.anchorMin = new Vector2(0.5f, 0.5f);
            shelf.anchorMax = new Vector2(0.5f, 0.5f);
            shelf.pivot = new Vector2(0.5f, 0.5f);
            shelf.anchoredPosition = anchoredPosition;
            shelf.sizeDelta = ScaleSizeUniform(new Vector2(520f, 300f));

            var baseSprite = theme.Background ?? theme.Highlight;
            var baseImg = CreateImage("Base", shelf, baseSprite, Vector2.zero, Vector2.zero);
            baseImg.rectTransform.anchorMin = Vector2.zero;
            baseImg.rectTransform.anchorMax = Vector2.one;
            baseImg.rectTransform.offsetMin = Vector2.zero;
            baseImg.rectTransform.offsetMax = Vector2.zero;
            baseImg.preserveAspect = false;
            baseImg.type = Image.Type.Sliced;
            baseImg.fillCenter = false;
            baseImg.color = new Color(0.04f, 0.02f, 0.07f, 0f);
            baseImg.raycastTarget = false;

            var rim = CreateImage("Rim", shelf, theme.Highlight, Vector2.zero, Vector2.zero);
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.offsetMin = Vector2.zero;
            rim.rectTransform.offsetMax = Vector2.zero;
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            rim.color = new Color(theme.AccentColor.r, theme.AccentColor.g, theme.AccentColor.b, 0f);
            rim.raycastTarget = false;
        }

        private static void BuildBottomBand(RectTransform root, ThemeSprites theme)
        {
            if (theme.Highlight == null)
            {
                return;
            }

            var band = CreateRect("BottomBand", root);
            band.anchorMin = new Vector2(0f, 0f);
            band.anchorMax = new Vector2(1f, 0f);
            band.pivot = new Vector2(0.5f, 0f);
            band.anchoredPosition = Vector2.zero;
            band.sizeDelta = ScaleSizeUniform(new Vector2(0f, 520f));

            var baseSprite = theme.Background ?? theme.Highlight;
            var baseImg = CreateImage("Base", band, baseSprite, Vector2.zero, Vector2.zero);
            baseImg.rectTransform.anchorMin = Vector2.zero;
            baseImg.rectTransform.anchorMax = Vector2.one;
            baseImg.rectTransform.offsetMin = Vector2.zero;
            baseImg.rectTransform.offsetMax = Vector2.zero;
            baseImg.preserveAspect = false;
            baseImg.type = Image.Type.Sliced;
            baseImg.fillCenter = false;
            baseImg.color = new Color(0.03f, 0.02f, 0.06f, 0f);
            baseImg.raycastTarget = false;

            var rim = CreateImage("Rim", band, theme.Highlight, Vector2.zero, Vector2.zero);
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.offsetMin = Vector2.zero;
            rim.rectTransform.offsetMax = Vector2.zero;
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            rim.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0f);
            rim.raycastTarget = false;
        }

        private static void CreateDiscardCardWithShadow(
            RectTransform shadowLayer,
            RectTransform cardLayer,
            string baseName,
            Sprite sprite,
            Vector2 size,
            Vector2 offset,
            float rotation,
            float scale,
            float alpha,
            float shadowAlpha,
            Vector2 shadowOffset)
        {
            var shadow = CreateImage($"{baseName}_Shadow", shadowLayer, sprite, size, Vector2.zero);
            shadow.preserveAspect = true;
            shadow.raycastTarget = false;
            shadow.color = new Color(0f, 0f, 0f, shadowAlpha);

            var shadowRect = shadow.rectTransform;
            shadowRect.anchoredPosition = ScaleFromBase(offset + shadowOffset);
            shadowRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            shadowRect.localScale = Vector3.one * (scale * 1.02f);

            var card = CreateImage($"{baseName}_Card", cardLayer, sprite, size, Vector2.zero);
            card.preserveAspect = true;
            card.raycastTarget = false;
            card.color = new Color(DeckStackTint.r, DeckStackTint.g, DeckStackTint.b, alpha);

            var rect = card.rectTransform;
            rect.anchoredPosition = ScaleFromBase(offset);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = Vector3.one * scale;
        }

        private static void BuildDeckZone(RectTransform parent, ThemeSprites theme, string name, Vector2 anchoredPosition, int count, bool isDeck)
        {
            var zone = CreateRect(name, parent);
            zone.anchorMin = new Vector2(0.5f, 0.5f);
            zone.anchorMax = new Vector2(0.5f, 0.5f);
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.anchoredPosition = anchoredPosition;
            zone.sizeDelta = ScaleSizeUniform(new Vector2(420f, 320f));

            var pileRoot = CreateRect(isDeck ? "DeckPileRoot" : "DiscardPileRoot", zone);
            pileRoot.anchorMin = new Vector2(0.5f, 0.62f);
            pileRoot.anchorMax = new Vector2(0.5f, 0.62f);
            pileRoot.pivot = new Vector2(0.5f, 0.5f);
            pileRoot.anchoredPosition = Vector2.zero;
            pileRoot.sizeDelta = ScaleSizeUniform(new Vector2(300f, 300f));

            var pileCards = CreateRect("PileCards", pileRoot);
            pileCards.anchorMin = new Vector2(0.5f, 0.5f);
            pileCards.anchorMax = new Vector2(0.5f, 0.5f);
            pileCards.pivot = new Vector2(0.5f, 0.5f);
            pileCards.anchoredPosition = ScaleFromBase(new Vector2(0f, 2f));
            pileCards.sizeDelta = ScaleSizeUniform(new Vector2(320f, 320f));

            var hitbox = CreateRect(isDeck ? "DeckPileHitbox" : "DiscardPileHitbox", zone);
            hitbox.anchorMin = new Vector2(0.5f, 0.62f);
            hitbox.anchorMax = new Vector2(0.5f, 0.62f);
            hitbox.pivot = new Vector2(0.5f, 0.5f);
            hitbox.anchoredPosition = Vector2.zero;
            hitbox.sizeDelta = ScaleSizeUniform(new Vector2(260f, 300f));
            hitbox.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var hitImage = hitbox.gameObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            // NOTE: The ideal foreground art already contains the deck/discard piles. We only provide
            // a stable hitbox here to avoid double-render artifacts in screenshots.
        }

        private static void BuildHandZone(RectTransform parent, ThemeSprites theme)
        {
            if (theme.CardBack == null)
            {
                return;
            }

            var handZone = CreateRect("HandZone", parent);
            handZone.anchorMin = new Vector2(0.5f, 0.5f);
            handZone.anchorMax = new Vector2(0.5f, 0.5f);
            handZone.pivot = new Vector2(0.5f, 0.5f);
            handZone.anchoredPosition = Vector2.zero;
            handZone.sizeDelta = ReferenceResolution;

            var count = Mathf.Min(5, HandCardCenters.Length);
            for (int i = 0; i < count; i++)
            {
                BuildHandCard(handZone, theme, i, HandCardCenters[i]);
            }
        }

        private static void BuildHandCard(RectTransform parent, ThemeSprites theme, int index, Vector2 center)
        {
            var cardRoot = CreateRect($"HandCard_{index}", parent);
            cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cardRoot.pivot = new Vector2(0.5f, 0.5f);
            cardRoot.anchoredPosition = center;
            cardRoot.sizeDelta = HandCardSize;

            var button = cardRoot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var hitImage = cardRoot.gameObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            var glowSprite = theme.Border ?? theme.Highlight;
            if (glowSprite != null)
            {
                var glow = CreateImage("CardFaceGlow", cardRoot, glowSprite, Vector2.zero, Vector2.zero);
                glow.rectTransform.anchorMin = Vector2.zero;
                glow.rectTransform.anchorMax = Vector2.one;
                glow.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(-28f, -28f));
                glow.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(-28f, -28f));
                glow.type = Image.Type.Sliced;
                glow.fillCenter = false;
                glow.color = new Color(CardGlowColor.r, CardGlowColor.g, CardGlowColor.b, 0.26f);
                glow.raycastTarget = false;
            }

            var shadow = CreateImage("Shadow", cardRoot, theme.CardBack, Vector2.zero, Vector2.zero);
            shadow.rectTransform.anchorMin = Vector2.zero;
            shadow.rectTransform.anchorMax = Vector2.one;
            shadow.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(10f, -12f));
            shadow.rectTransform.offsetMax = ScaleSizeUniform(new Vector2(10f, -12f));
            shadow.preserveAspect = false;
            shadow.color = new Color(0f, 0f, 0f, 0.32f);
            shadow.raycastTarget = false;

            BuildHandCardFace(cardRoot, theme, index);

            var filigreeSprite = theme.Border ?? theme.Highlight;
            if (filigreeSprite != null)
            {
                var filigree = CreateImage("Filigree", cardRoot, filigreeSprite, Vector2.zero, Vector2.zero);
                filigree.rectTransform.anchorMin = Vector2.zero;
                filigree.rectTransform.anchorMax = Vector2.one;
                filigree.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(20f, 24f));
                filigree.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(20f, 24f));
                filigree.type = Image.Type.Sliced;
                filigree.fillCenter = false;
                filigree.color = new Color(FiligreeColor.r, FiligreeColor.g, FiligreeColor.b, 0.48f);
                filigree.raycastTarget = false;
            }
        }

        private static void BuildHandCardFace(RectTransform cardRoot, ThemeSprites theme, int index)
        {
            var faceHost = CreateRect("Face", cardRoot);
            faceHost.anchorMin = Vector2.zero;
            faceHost.anchorMax = Vector2.one;
            faceHost.offsetMin = Vector2.zero;
            faceHost.offsetMax = Vector2.zero;

            var baseSprite = theme.Highlight ?? theme.CardBack;
            var baseImg = CreateImage("Base", faceHost, baseSprite, Vector2.zero, Vector2.zero);
            baseImg.rectTransform.anchorMin = Vector2.zero;
            baseImg.rectTransform.anchorMax = Vector2.one;
            baseImg.rectTransform.offsetMin = Vector2.zero;
            baseImg.rectTransform.offsetMax = Vector2.zero;
            baseImg.preserveAspect = false;
            baseImg.raycastTarget = false;
            var faceSprite = (theme.CardFaces != null && theme.CardFaces.Length > 0)
                ? theme.CardFaces[index % theme.CardFaces.Length]
                : null;

            if (faceSprite != null)
            {
                baseImg.color = new Color(0.05f, 0.03f, 0.08f, 0.55f);
                if (baseImg.type == Image.Type.Simple && baseSprite == theme.Highlight)
                {
                    baseImg.type = Image.Type.Sliced;
                }

                var art = CreateImage("Art", faceHost, faceSprite, Vector2.zero, Vector2.zero);
                art.rectTransform.anchorMin = Vector2.zero;
                art.rectTransform.anchorMax = Vector2.one;
                art.rectTransform.offsetMin = Vector2.zero;
                art.rectTransform.offsetMax = Vector2.zero;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else
            {
                var hue = (index * 0.18f) % 1f;
                var tint = Color.HSVToRGB(hue, 0.45f, 0.85f);
                baseImg.color = new Color(tint.r, tint.g, tint.b, 0.92f);
                if (baseImg.type == Image.Type.Simple && baseSprite == theme.Highlight)
                {
                    baseImg.type = Image.Type.Sliced;
                }
            }

            if (theme.MagicCircle != null)
            {
                var runeOuter = CreateImage("RuneCircleOuter", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeOuter.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeOuter.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeOuter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeOuter.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, 10f));
                runeOuter.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(236f, 236f));
                runeOuter.preserveAspect = true;
                runeOuter.color = new Color(theme.AccentColor.r, theme.AccentColor.g, theme.AccentColor.b, 0.62f);
                runeOuter.raycastTarget = false;
                runeOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f + index * 6f);

                var runeMid = CreateImage("RuneCircleMid", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeMid.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeMid.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeMid.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeMid.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -2f));
                runeMid.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(190f, 190f));
                runeMid.preserveAspect = true;
                runeMid.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0.54f);
                runeMid.raycastTarget = false;
                runeMid.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f + index * 5f);

                var runeInner = CreateImage("RuneCircleInner", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeInner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeInner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeInner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeInner.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -8f));
                runeInner.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(138f, 138f));
                runeInner.preserveAspect = true;
                runeInner.color = new Color(theme.AccentColor.r, theme.AccentColor.g, theme.AccentColor.b, 0.5f);
                runeInner.raycastTarget = false;
                runeInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f - index * 4f);

                var runeCore = CreateImage("RuneCircleCore", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeCore.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeCore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeCore.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -14f));
                runeCore.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(96f, 96f));
                runeCore.preserveAspect = true;
                runeCore.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0.42f);
                runeCore.raycastTarget = false;
                runeCore.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -26f + index * 7f);

                var runeArcTop = CreateImage("RuneArcTop", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeArcTop.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeArcTop.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeArcTop.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeArcTop.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, 118f));
                runeArcTop.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(86f, 86f));
                runeArcTop.preserveAspect = true;
                runeArcTop.color = new Color(theme.AccentColor.r, theme.AccentColor.g, theme.AccentColor.b, 0.46f);
                runeArcTop.raycastTarget = false;
                runeArcTop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 32f);

                var runeArcBottom = CreateImage("RuneArcBottom", faceHost, theme.MagicCircle, Vector2.zero, Vector2.zero);
                runeArcBottom.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                runeArcBottom.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                runeArcBottom.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                runeArcBottom.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -126f));
                runeArcBottom.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(92f, 92f));
                runeArcBottom.preserveAspect = true;
                runeArcBottom.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0.38f);
                runeArcBottom.raycastTarget = false;
                runeArcBottom.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            }

            var frameSprite = theme.Border ?? theme.Highlight;
            if (frameSprite != null)
            {
                var frameShadow = CreateImage("FrameShadow", faceHost, frameSprite, Vector2.zero, Vector2.zero);
                frameShadow.rectTransform.anchorMin = Vector2.zero;
                frameShadow.rectTransform.anchorMax = Vector2.one;
                frameShadow.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(2f, 2f));
                frameShadow.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(2f, 2f));
                frameShadow.type = Image.Type.Sliced;
                frameShadow.fillCenter = false;
                frameShadow.color = new Color(0.03f, 0.02f, 0.08f, 0.9f);
                frameShadow.raycastTarget = false;

                var frame = CreateImage("Frame", faceHost, frameSprite, Vector2.zero, Vector2.zero);
                frame.rectTransform.anchorMin = Vector2.zero;
                frame.rectTransform.anchorMax = Vector2.one;
                frame.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(6f, 6f));
                frame.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(6f, 6f));
                frame.type = Image.Type.Sliced;
                frame.fillCenter = false;
                frame.color = new Color(0.98f, 0.88f, 0.62f, 0.98f);
                frame.raycastTarget = false;
                var frameOutline = frame.gameObject.AddComponent<Outline>();
                frameOutline.effectColor = new Color(1f, 0.92f, 0.68f, 0.52f);
                frameOutline.effectDistance = new Vector2(3f, -3f);

                var frameAccent = CreateImage("FrameAccent", faceHost, frameSprite, Vector2.zero, Vector2.zero);
                frameAccent.rectTransform.anchorMin = Vector2.zero;
                frameAccent.rectTransform.anchorMax = Vector2.one;
                frameAccent.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(12f, 12f));
                frameAccent.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(12f, 12f));
                frameAccent.type = Image.Type.Sliced;
                frameAccent.fillCenter = false;
                frameAccent.color = new Color(0.88f, 0.86f, 0.68f, 0.55f);
                frameAccent.raycastTarget = false;

                var innerFrame = CreateImage("FrameInner", faceHost, frameSprite, Vector2.zero, Vector2.zero);
                innerFrame.rectTransform.anchorMin = Vector2.zero;
                innerFrame.rectTransform.anchorMax = Vector2.one;
                innerFrame.rectTransform.offsetMin = ScaleSizeUniform(new Vector2(20f, 20f));
                innerFrame.rectTransform.offsetMax = -ScaleSizeUniform(new Vector2(20f, 20f));
                innerFrame.type = Image.Type.Sliced;
                innerFrame.fillCenter = false;
                innerFrame.color = new Color(0.64f, 0.9f, 1f, 0.78f);
                innerFrame.raycastTarget = false;
            }

            if (theme.Highlight != null)
            {
                var sigil = CreateImage("SigilGlow", faceHost, theme.Highlight, Vector2.zero, Vector2.zero);
                sigil.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                sigil.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                sigil.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                sigil.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -10f));
                sigil.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(170f, 170f));
                sigil.type = Image.Type.Sliced;
                sigil.fillCenter = false;
                sigil.color = new Color(1f, 0.9f, 0.72f, 0.42f);
                sigil.raycastTarget = false;

                var sigilCore = CreateImage("SigilCore", faceHost, GetWhiteSprite(), Vector2.zero, Vector2.zero);
                sigilCore.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                sigilCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                sigilCore.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                sigilCore.rectTransform.anchoredPosition = ScaleFromBase(new Vector2(0f, -10f));
                sigilCore.rectTransform.sizeDelta = ScaleSizeUniform(new Vector2(32f, 32f));
                sigilCore.color = new Color(1f, 0.96f, 0.8f, 0.86f);
                sigilCore.raycastTarget = false;
                var sigilGlow = sigilCore.gameObject.AddComponent<Shadow>();
                sigilGlow.effectColor = new Color(1f, 0.9f, 0.7f, 0.55f);
                sigilGlow.effectDistance = new Vector2(1f, -1f);

                var sparklePositions = new[]
                {
                    new Vector2(-92f, 128f),
                    new Vector2(78f, 108f),
                    new Vector2(-112f, 54f),
                    new Vector2(104f, 34f),
                    new Vector2(-120f, -12f),
                    new Vector2(98f, -62f),
                    new Vector2(-74f, -120f),
                    new Vector2(72f, -132f),
                    new Vector2(-10f, 148f),
                    new Vector2(16f, -150f),
                    new Vector2(118f, -12f),
                    new Vector2(-126f, 102f)
                };
                var sparkleSizes = new[] { 10f, 14f, 8f, 12f, 6f, 10f, 12f, 9f, 7f, 8f, 6f, 9f };
                var sparkleSprite = GetWhiteSprite();
                for (int i = 0; i < sparklePositions.Length; i++)
                {
                    var size = ScaleSizeUniform(new Vector2(sparkleSizes[i], sparkleSizes[i]));
                    var pos = ScaleFromBase(sparklePositions[i]);
                    var sparkle = CreateImage($"Sparkle_{i}", faceHost, sparkleSprite, size, pos);
                    sparkle.type = Image.Type.Simple;
                    sparkle.color = new Color(1f, 0.95f, 0.78f, 0.82f);
                    sparkle.raycastTarget = false;
                    var sparkleGlow = sparkle.gameObject.AddComponent<Shadow>();
                    sparkleGlow.effectColor = new Color(1f, 0.9f, 0.72f, 0.4f);
                    sparkleGlow.effectDistance = new Vector2(1f, -1f);
                }
            }
        }

        private static Vector2 ScaleFromBase(Vector2 value)
        {
            return new Vector2(value.x * UniformScale, value.y * VerticalScale);
        }

        private static Vector2[] ScaleFromBase(Vector2[] values)
        {
            var scaled = new Vector2[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                scaled[i] = ScaleFromBase(values[i]);
            }

            return scaled;
        }

        private static Vector2 ScaleSizeUniform(Vector2 size)
        {
            return size * UniformScale;
        }

        private static int GetVisualDeckStackCount(int deckCount)
        {
            if (deckCount <= 0)
            {
                return 0;
            }

            // Keep the visual stack bounded for stable screenshots.
            var t = Mathf.InverseLerp(5f, 40f, deckCount);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(6f, 10f, t)), 6, 10);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            return image;
        }

        private static Image CreateOverlay(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            var overlay = CreateImage(name, parent, sprite, size, Vector2.zero);
            overlay.type = Image.Type.Sliced;
            overlay.color = color;
            overlay.raycastTarget = false;
            return overlay;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 1f, 1f, 0.92f);
            text.raycastTarget = false;

            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                              Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = builtinFont;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static void CreateCornerLabel(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
        {
            var badgeRoot = CreateRect(name, parent);
            badgeRoot.anchorMin = anchor;
            badgeRoot.anchorMax = anchor;
            badgeRoot.pivot = pivot;
            badgeRoot.anchoredPosition = anchoredPos;
            badgeRoot.sizeDelta = new Vector2(156f, 44f);

            var background = CreateImage("Background", badgeRoot, null, Vector2.zero, Vector2.zero);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = false;

            var text = CreateText("Label", badgeRoot, label, 18, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-8f, -4f);
            text.color = new Color(1f, 1f, 1f, 0.94f);
        }

        private static void CreateCountPlaque(Transform parent, string name, Sprite sprite, string label, int count, Color accent, Vector2 anchor, Vector2 anchoredPosition)
        {
            var plaque = CreateRect(name, parent);
            plaque.anchorMin = anchor;
            plaque.anchorMax = anchor;
            plaque.pivot = new Vector2(0.5f, 0.5f);
            plaque.anchoredPosition = anchoredPosition;
            plaque.sizeDelta = ScaleSizeUniform(new Vector2(216f, 78f));

            var plaqueSprite = sprite ?? GetWhiteSprite();
            var bgColor = new Color(0.42f, 0.32f, 0.14f, 0.88f);
            var bg = CreateImage("Background", plaque, plaqueSprite, Vector2.zero, Vector2.zero);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;
            bg.type = Image.Type.Simple;
            bg.color = bgColor;
            bg.raycastTarget = false;
            var dropShadow = bg.gameObject.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            dropShadow.effectDistance = new Vector2(2f, -2f);
            var bgOutline = bg.gameObject.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0.98f, 0.86f, 0.52f, 0.58f);
            bgOutline.effectDistance = new Vector2(1f, -1f);

            var glow = CreateImage("Glow", plaque, plaqueSprite, Vector2.zero, Vector2.zero);
            glow.rectTransform.anchorMin = Vector2.zero;
            glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-10f, -10f);
            glow.rectTransform.offsetMax = new Vector2(10f, 10f);
            glow.type = Image.Type.Simple;
            glow.color = new Color(1f, 0.84f, 0.42f, 0.08f);
            glow.raycastTarget = false;
            glow.rectTransform.SetAsFirstSibling();

            var title = CreateText("Title", plaque, label, 12, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(10f, 0f);
            title.rectTransform.offsetMax = new Vector2(-10f, -4f);
            title.color = new Color(0.98f, 0.9f, 0.7f, 0.92f);

            var number = CreateText("Count", plaque, count.ToString(), 22, TextAnchor.LowerCenter);
            number.rectTransform.anchorMin = new Vector2(0f, 0f);
            number.rectTransform.anchorMax = new Vector2(1f, 0.56f);
            number.rectTransform.offsetMin = new Vector2(10f, 4f);
            number.rectTransform.offsetMax = new Vector2(-10f, 0f);
            number.color = new Color(1f, 0.96f, 0.82f, 0.98f);
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSprite;
        }

        private static void CreateHandCard(Transform parent, string name, Sprite plateSprite, Sprite faceSprite, Vector2 size, Color accent)
        {
            var cardRoot = CreateRect(name, parent);
            var element = cardRoot.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            element.minWidth = size.x;
            element.minHeight = size.y;

            if (plateSprite != null)
            {
                var shadow = CreateImage("Shadow", cardRoot, plateSprite, Vector2.zero, new Vector2(10f, -12f));
                shadow.rectTransform.anchorMin = Vector2.zero;
                shadow.rectTransform.anchorMax = Vector2.one;
                shadow.rectTransform.offsetMin = new Vector2(-18f, -18f);
                shadow.rectTransform.offsetMax = new Vector2(18f, 18f);
                shadow.type = Image.Type.Sliced;
                shadow.color = new Color(0f, 0f, 0f, 0.32f);
                shadow.raycastTarget = false;

                var glow = CreateImage("Glow", cardRoot, plateSprite, Vector2.zero, Vector2.zero);
                glow.rectTransform.anchorMin = Vector2.zero;
                glow.rectTransform.anchorMax = Vector2.one;
                glow.rectTransform.offsetMin = new Vector2(-26f, -26f);
                glow.rectTransform.offsetMax = new Vector2(26f, 26f);
                glow.type = Image.Type.Sliced;
                glow.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                glow.raycastTarget = false;
            }

            var face = CreateImage("Face", cardRoot, faceSprite, Vector2.zero, Vector2.zero);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = Vector2.zero;
            face.rectTransform.offsetMax = Vector2.zero;
            face.preserveAspect = true;
            face.raycastTarget = false;
        }

        private static void CreateHandCardAbsolute(RectTransform parent, string name, Sprite plateSprite, Sprite faceSprite, Vector2 size, Color accent, Vector2 anchoredPosition)
        {
            var cardRoot = CreateRect(name, parent);
            cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cardRoot.pivot = new Vector2(0.5f, 0.5f);
            cardRoot.anchoredPosition = anchoredPosition;
            cardRoot.sizeDelta = size;

            var face = CreateImage("Face", cardRoot, faceSprite, Vector2.zero, Vector2.zero);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = Vector2.zero;
            face.rectTransform.offsetMax = Vector2.zero;
            face.preserveAspect = true;
            face.raycastTarget = false;

            var shadow = face.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(8f, -10f);
        }

        private static void SpawnDeckStack(Transform parent, Sprite sprite, Color tint, int count)
        {
            if (sprite == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var offset = new Vector2(i * 7f, -i * 5f);
                var card = CreateImage($"DeckCard_{i}", parent, sprite, PileCardSize, offset);
                card.preserveAspect = true;
                card.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01((0.96f - i * 0.04f) * tint.a));
                card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4.5f + i * 0.25f);
                card.raycastTarget = false;

                var shadow = card.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
                shadow.effectDistance = new Vector2(8f, -10f);
            }
        }

        private static void SpawnDiscardPile(Transform parent, Sprite sprite, Color tint)
        {
            if (sprite == null)
            {
                return;
            }

            var layers = new[]
            {
                (pos: new Vector2(-46f, -18f), rot: -22f, alpha: 0.46f, scale: 0.98f),
                (pos: new Vector2(-18f, 10f), rot: -9f, alpha: 0.42f, scale: 1.02f),
                (pos: new Vector2(8f, 18f), rot: 8f, alpha: 0.38f, scale: 1.05f),
                (pos: new Vector2(34f, 6f), rot: 20f, alpha: 0.34f, scale: 1.0f),
                (pos: new Vector2(52f, -16f), rot: 32f, alpha: 0.3f, scale: 0.96f),
                (pos: new Vector2(12f, -22f), rot: 14f, alpha: 0.28f, scale: 0.94f)
            };

            for (int i = 0; i < layers.Length; i++)
            {
                var cardPos = layers[i].pos;
                var layer = CreateImage($"DiscardCard_{i}", parent, sprite, PileCardSize, cardPos);
                layer.preserveAspect = true;
                layer.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(layers[i].alpha * tint.a));
                layer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, layers[i].rot);
                layer.rectTransform.localScale = Vector3.one * layers[i].scale;
                layer.raycastTarget = false;

                var shadow = layer.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.2f);
                shadow.effectDistance = new Vector2(8f, -10f);
            }
        }

        private static void CreateCountBadge(Transform parent, string name, Sprite highlightSprite, string label, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Color accent)
        {
            var badgeRoot = CreateRect(name, parent);
            badgeRoot.anchorMin = anchor;
            badgeRoot.anchorMax = anchor;
            badgeRoot.pivot = pivot;
            badgeRoot.anchoredPosition = anchoredPos;
            badgeRoot.sizeDelta = new Vector2(204f, 66f);

            var bgColor = new Color(0.06f, 0.04f, 0.1f, 0.72f);
            var background = CreateImage("Background", badgeRoot, highlightSprite, Vector2.zero, Vector2.zero);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;
            background.type = Image.Type.Sliced;
            background.color = bgColor;
            background.raycastTarget = false;

            if (highlightSprite != null)
            {
                var rim = CreateOverlay("Rim", badgeRoot, highlightSprite, new Color(accent.r, accent.g, accent.b, 0.18f), Vector2.zero);
                rim.rectTransform.anchorMin = Vector2.zero;
                rim.rectTransform.anchorMax = Vector2.one;
                rim.rectTransform.offsetMin = new Vector2(6f, 6f);
                rim.rectTransform.offsetMax = new Vector2(-6f, -6f);
                rim.type = Image.Type.Sliced;
                rim.raycastTarget = false;
            }

            var text = CreateText("Label", badgeRoot, label, 18, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10f, 6f);
            text.rectTransform.offsetMax = new Vector2(-10f, -6f);
            text.color = new Color(accent.r, accent.g, accent.b, 0.92f);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite LoadSpriteLoose(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                return sprite;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                return null;
            }

            var rect = new Rect(0f, 0f, texture.width, texture.height);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var dirty = false;

            var normalizedPath = assetPath.Replace('\\', '/');
            var desiredBorder = Vector4.zero;
            if (normalizedPath.EndsWith("/deckui_border.jpg", StringComparison.OrdinalIgnoreCase))
            {
                desiredBorder = new Vector4(140f, 140f, 140f, 140f);
            }

            if (importer.spriteBorder != desiredBorder)
            {
                importer.spriteBorder = desiredBorder;
                dirty = true;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static void SpawnDiscardFan(Transform parent, Sprite sprite, Color tint)
        {
            var alphaBase = 0.48f;
            var layers = new[]
            {
                (pos: new Vector2(-34f, -12f), rot: -18f, alpha: alphaBase * 0.95f, scale: 0.98f),
                (pos: new Vector2(-14f, 6f), rot: -6f, alpha: alphaBase * 0.9f, scale: 1.0f),
                (pos: new Vector2(6f, 10f), rot: 6f, alpha: alphaBase * 0.85f, scale: 1.02f),
                (pos: new Vector2(28f, 2f), rot: 18f, alpha: alphaBase * 0.8f, scale: 1.0f),
                (pos: new Vector2(44f, -18f), rot: 28f, alpha: alphaBase * 0.7f, scale: 0.96f)
            };

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = CreateImage($"StackCard_{i}", parent, sprite, new Vector2(210f, 290f), layers[i].pos);
                layer.preserveAspect = true;
                layer.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(layers[i].alpha));
                layer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, layers[i].rot);
                layer.rectTransform.localScale = Vector3.one * layers[i].scale;
                layer.raycastTarget = false;
            }
        }

        private static void SpawnStack(Transform parent, Sprite sprite, Color tint, int count, float xStep, float rotationStep, float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(i * xStep, -i * 4f);
                var card = CreateImage($"StackCard_{i}", parent, sprite, new Vector2(210f, 290f), pos);
                card.preserveAspect = true;
                card.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha * (0.92f - i * 0.06f)));
                card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * rotationStep);
                card.raycastTarget = false;
            }
        }

        private static byte[] CaptureCamera(Camera camera)
        {
            var renderTexture = new RenderTexture((int)ReferenceResolution.x, (int)ReferenceResolution.y, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            Canvas.ForceUpdateCanvases();

            camera.targetTexture = renderTexture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            var png = texture.EncodeToPNG();

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            UnityEngine.Object.DestroyImmediate(texture);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);

            return png;
        }

        private static void WriteScreenshots(byte[] png, string[] targets)
        {
            foreach (var target in targets)
            {
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(target, png);
                Debug.Log($"[DeckIdealSceneBuilder] Saved screenshot: {target}");
            }

            AssetDatabase.Refresh();
        }

    }
}
