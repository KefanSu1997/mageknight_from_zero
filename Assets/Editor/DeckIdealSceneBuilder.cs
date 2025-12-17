using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageKnight.EditorTools
{
    /// <summary>
    /// 生成理想卡组静态构图场景并输出验收截图。
    /// 直接使用理想主题背景/卡背/角色卡面，避免半透明蒙版导致泛白。
    /// </summary>
    public static class DeckIdealSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Part1/Part1_DeckIdeal.unity";
        private const int DefaultDeckCount = 30;
        private const int DefaultDiscardCount = 0;

        private static readonly Vector2 DeckAnchor = new(0.14f, 0.82f);
        private static readonly Vector2 DiscardAnchor = new(0.86f, 0.82f);
        private static readonly Vector2 HandAnchor = new(0.5f, 0.12f);

        private static readonly string[] ScreenshotTargets =
        {
            "multi-agent-workspace/runs/T-20251028-006/review_bundle/artifacts/screenshots/deck_ideal_overview.png"
        };

        private static readonly string[] MonsterPreviewTargets =
        {
            "multi-agent-workspace/runs/T-20251028-005/review_bundle/artifacts/screenshots/monster_assets_001.png",
            "multi-agent-workspace/review_bundle/artifacts/screenshots/monster_assets_001.png"
        };

        private const string IdealBackgroundPath = "Assets/UI/Images/DeckTheme/Ideal/deck_ideal_background.png";
        private const string IdealCardBackPath = "Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png";
        private const string IdealForegroundPath = "Assets/UI/Images/DeckTheme/Ideal/deck_ideal_foreground.png";
        private const string IdealMonsterPath = "Assets/GameData/cards/monster_ideal_boss.png";
        private static readonly string[] IdealCardFaces =
        {
            "Assets/GameData/cards/ideal_card_01.png",
            "Assets/GameData/cards/ideal_card_02.png",
            "Assets/GameData/cards/ideal_card_03.png",
            "Assets/GameData/cards/ideal_card_04.png",
            "Assets/GameData/cards/ideal_card_05.png"
        };

        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        private static readonly Color DeckStackTint = new(1f, 1f, 1f, 0.96f);
        private static readonly Color DiscardStackTint = new(1f, 1f, 1f, 0.9f);
        private static readonly Color FrameColor = new(0.9f, 0.94f, 1f, 0.85f);
        private static readonly Color FiligreeColor = new(0.94f, 0.95f, 1f, 0.4f);
        private static readonly Color GemColor = new(0.88f, 0.94f, 1f, 0.52f);
        private static readonly Color BackgroundTint = new(0f, 0f, 0f, 0.6f);
        private static readonly Color MagicCircleColor = new(0.62f, 0.82f, 1f, 0.32f);
        private static readonly Color SwirlColor = new(1f, 0.82f, 0.46f, 0.24f);
        private static readonly Color CardGlowColor = new(1f, 0.88f, 0.58f, 0.26f);
        private static readonly Color SlotTint = new(0.08f, 0.1f, 0.14f, 0.82f);

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
            WriteMonsterAssetPreview();
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
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            return camera;
        }

        private static Canvas CreateCanvas(Camera camera)
        {
            var canvasGo = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

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
            var faces = new Sprite[IdealCardFaces.Length];
            for (int i = 0; i < IdealCardFaces.Length; i++)
            {
                faces[i] = LoadSprite(IdealCardFaces[i]);
            }

            var background = LoadSpriteLoose(IdealBackgroundPath);
            if (background == null)
            {
                EnsureSpriteImport(IdealBackgroundPath);
                AssetDatabase.ImportAsset(IdealBackgroundPath, ImportAssetOptions.ForceUpdate);
                background = LoadSpriteLoose(IdealBackgroundPath);
            }

            background ??= DeckUiThemeCache.BackgroundSprite;

            var foreground = LoadSpriteLoose(IdealForegroundPath);
            if (foreground == null)
            {
                EnsureSpriteImport(IdealForegroundPath);
                AssetDatabase.ImportAsset(IdealForegroundPath, ImportAssetOptions.ForceUpdate);
                foreground = LoadSpriteLoose(IdealForegroundPath);
            }

            foreground ??= background;

            var cardBack = LoadSpriteLoose(IdealCardBackPath);
            if (cardBack == null)
            {
                EnsureSpriteImport(IdealCardBackPath);
                AssetDatabase.ImportAsset(IdealCardBackPath, ImportAssetOptions.ForceUpdate);
                cardBack = LoadSpriteLoose(IdealCardBackPath);
            }

            cardBack ??= DeckUiThemeCache.CardBackSprite;
            if (cardBack == null)
            {
                Debug.LogError($"[DeckIdealSceneBuilder] Card back sprite missing at path: {IdealCardBackPath}");
            }

            return new ThemeSprites
            {
                Background = background,
                Foreground = foreground,
                Border = DeckUiThemeCache.BorderSprite ?? DeckUiThemeCache.HighlightSprite,
                Highlight = DeckUiThemeCache.HighlightSprite,
                MagicCircle = DeckUiThemeCache.MagicCircleSprite,
                CardBack = cardBack,
                CardFaces = faces,
                Monster = LoadSprite(IdealMonsterPath),
                BackgroundColor = new Color(0.18f, 0.08f, 0.28f, 1f),
                AccentColor = new Color(0.48f, 0.64f, 0.98f, 1f),
                SecondaryAccentColor = new Color(1f, 0.82f, 0.52f, 1f)
            };
        }

        private static void BuildStaticLayout(Canvas canvas, ThemeSprites theme)
        {
            var root = canvas.GetComponent<RectTransform>();
            foreach (Transform child in root)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var bg = CreateImage("IdealBackground", root, theme.Background, Vector2.zero, Vector2.zero);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;
            bg.preserveAspect = false;
            bg.raycastTarget = false;

            var fg = CreateImage("IdealForeground", root, theme.Foreground, Vector2.zero, Vector2.zero);
            fg.rectTransform.anchorMin = Vector2.zero;
            fg.rectTransform.anchorMax = Vector2.one;
            fg.rectTransform.offsetMin = Vector2.zero;
            fg.rectTransform.offsetMax = Vector2.zero;
            fg.preserveAspect = false;
            fg.raycastTarget = false;

            var overlayRoot = CreateRect("DynamicOverlay", root);
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
            overlayRoot.pivot = new Vector2(0.5f, 0.5f);

            BuildDeckZone(overlayRoot, theme, "DeckZone", DeckAnchor, DefaultDeckCount, isDeck: true);
            BuildDeckZone(overlayRoot, theme, "DiscardZone", DiscardAnchor, DefaultDiscardCount, isDeck: false);
            BuildHandZone(overlayRoot, theme);
        }

        private static void BuildDeckZone(RectTransform parent, ThemeSprites theme, string name, Vector2 anchor, int count, bool isDeck)
        {
            var zone = CreateRect(name, parent);
            zone.anchorMin = anchor;
            zone.anchorMax = anchor;
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.anchoredPosition = Vector2.zero;
            zone.sizeDelta = new Vector2(560f, 380f);

            var shadowSprite = theme.Highlight ?? theme.Border;
            if (shadowSprite != null)
            {
                var baseShadow = CreateImage("ZoneShadow", zone, shadowSprite, Vector2.zero, new Vector2(6f, -10f));
                baseShadow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                baseShadow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                baseShadow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                baseShadow.rectTransform.sizeDelta = new Vector2(520f, 360f);
                baseShadow.type = Image.Type.Sliced;
                baseShadow.color = new Color(0f, 0f, 0f, 0.22f);
                baseShadow.raycastTarget = false;
            }

            var stackRoot = CreateRect("StackRoot", zone);
            stackRoot.anchorMin = new Vector2(0.5f, 0.58f);
            stackRoot.anchorMax = new Vector2(0.5f, 0.58f);
            stackRoot.pivot = new Vector2(0.5f, 0.5f);
            stackRoot.anchoredPosition = Vector2.zero;
            stackRoot.sizeDelta = new Vector2(460f, 320f);

            if (theme.CardBack != null)
            {
                if (isDeck)
                {
                    SpawnDeckStack(stackRoot, theme.CardBack, DeckStackTint, 8);
                }
                else
                {
                    SpawnDiscardPile(stackRoot, theme.CardBack, DiscardStackTint);
                }
            }

            var plaqueSprite = theme.Highlight ?? theme.Border;
            CreateCountPlaque(zone, $"{(isDeck ? "Deck" : "Discard")}Plaque", plaqueSprite, isDeck ? "DECK" : "DISCARD", count, theme.SecondaryAccentColor);
        }

        private static void BuildHandZone(RectTransform parent, ThemeSprites theme)
        {
            var handZone = CreateRect("HandZone", parent);
            handZone.anchorMin = HandAnchor;
            handZone.anchorMax = HandAnchor;
            handZone.pivot = new Vector2(0.5f, 0f);
            handZone.anchoredPosition = new Vector2(0f, 22f);
            handZone.sizeDelta = new Vector2(1560f, 430f);

            var plateSprite = theme.Highlight ?? theme.Border;
            if (plateSprite != null)
            {
                var plate = CreateImage("HandPlate", handZone, plateSprite, Vector2.zero, new Vector2(0f, 18f));
                plate.rectTransform.anchorMin = Vector2.zero;
                plate.rectTransform.anchorMax = Vector2.one;
                plate.rectTransform.offsetMin = new Vector2(40f, 28f);
                plate.rectTransform.offsetMax = new Vector2(-40f, -60f);
                plate.type = Image.Type.Sliced;
                plate.color = new Color(0.05f, 0.03f, 0.09f, 0.38f);
                plate.raycastTarget = false;

                var rim = CreateImage("HandRim", handZone, plateSprite, Vector2.zero, new Vector2(0f, 18f));
                rim.rectTransform.anchorMin = Vector2.zero;
                rim.rectTransform.anchorMax = Vector2.one;
                rim.rectTransform.offsetMin = new Vector2(48f, 34f);
                rim.rectTransform.offsetMax = new Vector2(-48f, -66f);
                rim.type = Image.Type.Sliced;
                rim.color = new Color(theme.SecondaryAccentColor.r, theme.SecondaryAccentColor.g, theme.SecondaryAccentColor.b, 0.18f);
                rim.raycastTarget = false;
            }

            var row = CreateRect("HandCards", handZone);
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 8f);
            row.sizeDelta = new Vector2(1460f, 410f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 42f;
            layout.padding = new RectOffset(24, 24, 10, 12);

            var cardSize = new Vector2(250f, 360f);
            for (int i = 0; i < 5; i++)
            {
                var face = theme.CardFaces != null && i < theme.CardFaces.Length ? theme.CardFaces[i] : null;
                CreateHandCard(row, $"HandCard_{i}", plateSprite, face, cardSize, theme.SecondaryAccentColor);
            }
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

        private static void CreateCountPlaque(Transform parent, string name, Sprite sprite, string label, int count, Color accent)
        {
            var plaque = CreateRect(name, parent);
            plaque.anchorMin = new Vector2(0.5f, 0.12f);
            plaque.anchorMax = new Vector2(0.5f, 0.12f);
            plaque.pivot = new Vector2(0.5f, 0.5f);
            plaque.anchoredPosition = new Vector2(0f, -150f);
            plaque.sizeDelta = new Vector2(220f, 78f);

            if (sprite != null)
            {
                var bg = CreateImage("Background", plaque, sprite, Vector2.zero, Vector2.zero);
                bg.rectTransform.anchorMin = Vector2.zero;
                bg.rectTransform.anchorMax = Vector2.one;
                bg.rectTransform.offsetMin = Vector2.zero;
                bg.rectTransform.offsetMax = Vector2.zero;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.05f, 0.03f, 0.09f, 0.78f);
                bg.raycastTarget = false;

                var rim = CreateImage("Rim", plaque, sprite, Vector2.zero, Vector2.zero);
                rim.rectTransform.anchorMin = Vector2.zero;
                rim.rectTransform.anchorMax = Vector2.one;
                rim.rectTransform.offsetMin = new Vector2(6f, 6f);
                rim.rectTransform.offsetMax = new Vector2(-6f, -6f);
                rim.type = Image.Type.Sliced;
                rim.color = new Color(accent.r, accent.g, accent.b, 0.22f);
                rim.raycastTarget = false;
            }

            var title = CreateText("Title", plaque, label, 14, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 0.48f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(12f, 0f);
            title.rectTransform.offsetMax = new Vector2(-12f, -6f);
            title.color = new Color(accent.r, accent.g, accent.b, 0.88f);

            var number = CreateText("Count", plaque, count.ToString(), 24, TextAnchor.LowerCenter);
            number.rectTransform.anchorMin = new Vector2(0f, 0f);
            number.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            number.rectTransform.offsetMin = new Vector2(12f, 4f);
            number.rectTransform.offsetMax = new Vector2(-12f, 0f);
            number.color = new Color(accent.r, accent.g, accent.b, 0.94f);
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
                var shadow = CreateImage("Shadow", cardRoot, plateSprite, Vector2.zero, new Vector2(8f, -10f));
                shadow.rectTransform.anchorMin = Vector2.zero;
                shadow.rectTransform.anchorMax = Vector2.one;
                shadow.rectTransform.offsetMin = new Vector2(-10f, -10f);
                shadow.rectTransform.offsetMax = new Vector2(10f, 10f);
                shadow.type = Image.Type.Sliced;
                shadow.color = new Color(0f, 0f, 0f, 0.28f);
                shadow.raycastTarget = false;

                var basePlate = CreateImage("Plate", cardRoot, plateSprite, Vector2.zero, Vector2.zero);
                basePlate.rectTransform.anchorMin = Vector2.zero;
                basePlate.rectTransform.anchorMax = Vector2.one;
                basePlate.rectTransform.offsetMin = Vector2.zero;
                basePlate.rectTransform.offsetMax = Vector2.zero;
                basePlate.type = Image.Type.Sliced;
                basePlate.color = new Color(0.06f, 0.04f, 0.1f, 0.44f);
                basePlate.raycastTarget = false;
            }

            var face = CreateImage("Face", cardRoot, faceSprite, Vector2.zero, Vector2.zero);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = new Vector2(10f, 12f);
            face.rectTransform.offsetMax = new Vector2(-10f, -10f);
            face.preserveAspect = false;
            face.raycastTarget = false;

            if (plateSprite != null)
            {
                var glow = CreateImage("Glow", cardRoot, plateSprite, Vector2.zero, Vector2.zero);
                glow.rectTransform.anchorMin = Vector2.zero;
                glow.rectTransform.anchorMax = Vector2.one;
                glow.rectTransform.offsetMin = new Vector2(-12f, -12f);
                glow.rectTransform.offsetMax = new Vector2(12f, 12f);
                glow.type = Image.Type.Sliced;
                glow.color = new Color(accent.r, accent.g, accent.b, 0.12f);
                glow.raycastTarget = false;
                glow.transform.SetAsFirstSibling();
            }
        }

        private static void SpawnDeckStack(Transform parent, Sprite sprite, Color tint, int count)
        {
            if (sprite == null)
            {
                return;
            }

            var shadowSprite = DeckUiThemeCache.HighlightSprite;
            for (int i = 0; i < count; i++)
            {
                var offset = new Vector2(i * 16f, -i * 10f);
                if (shadowSprite != null)
                {
                    var shadow = CreateImage($"DeckShadow_{i}", parent, shadowSprite, new Vector2(240f, 330f), offset + new Vector2(8f, -10f));
                    shadow.preserveAspect = false;
                    shadow.type = Image.Type.Sliced;
                    shadow.color = new Color(0f, 0f, 0f, 0.18f * (0.9f - i * 0.08f));
                    shadow.raycastTarget = false;
                }

                var card = CreateImage($"DeckCard_{i}", parent, sprite, new Vector2(230f, 320f), offset);
                card.preserveAspect = true;
                card.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(0.98f - i * 0.06f));
                card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f + i * 0.7f);
                card.raycastTarget = false;
            }
        }

        private static void SpawnDiscardPile(Transform parent, Sprite sprite, Color tint)
        {
            if (sprite == null)
            {
                return;
            }

            var shadowSprite = DeckUiThemeCache.HighlightSprite;
            var layers = new[]
            {
                (pos: new Vector2(-46f, -24f), rot: -22f, alpha: 0.42f, scale: 0.98f),
                (pos: new Vector2(-18f, 8f), rot: -8f, alpha: 0.4f, scale: 1.02f),
                (pos: new Vector2(8f, 14f), rot: 8f, alpha: 0.36f, scale: 1.04f),
                (pos: new Vector2(34f, 4f), rot: 20f, alpha: 0.32f, scale: 1.0f),
                (pos: new Vector2(52f, -20f), rot: 32f, alpha: 0.28f, scale: 0.96f)
            };

            for (int i = 0; i < layers.Length; i++)
            {
                var cardPos = layers[i].pos;
                if (shadowSprite != null)
                {
                    var shadow = CreateImage($"DiscardShadow_{i}", parent, shadowSprite, new Vector2(240f, 330f), cardPos + new Vector2(10f, -12f));
                    shadow.preserveAspect = false;
                    shadow.type = Image.Type.Sliced;
                    shadow.color = new Color(0f, 0f, 0f, 0.16f);
                    shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, layers[i].rot);
                    shadow.rectTransform.localScale = Vector3.one * (layers[i].scale * 1.02f);
                    shadow.raycastTarget = false;
                }

                var layer = CreateImage($"DiscardCard_{i}", parent, sprite, new Vector2(230f, 320f), cardPos);
                layer.preserveAspect = true;
                layer.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(layers[i].alpha));
                layer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, layers[i].rot);
                layer.rectTransform.localScale = Vector3.one * layers[i].scale;
                layer.raycastTarget = false;
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

            if (importer.textureType == TextureImporterType.Sprite)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
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

        private static void WriteMonsterAssetPreview()
        {
            try
            {
                var sourcePath = Path.GetFullPath(IdealMonsterPath);
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[DeckIdealSceneBuilder] Monster asset missing: {sourcePath}");
                    return;
                }

                var bytes = File.ReadAllBytes(sourcePath);
                foreach (var target in MonsterPreviewTargets)
                {
                    var directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(target, bytes);
                    Debug.Log($"[DeckIdealSceneBuilder] Saved monster preview: {target}");
                }

                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeckIdealSceneBuilder] Failed to export monster preview: {ex.Message}");
            }
        }
    }
}
