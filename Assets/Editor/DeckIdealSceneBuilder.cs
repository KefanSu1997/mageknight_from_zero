using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MageKnight.EditorTools
{
    /// <summary>
    /// 一键生成“理想卡组”演示场景并输出验收截图。
    /// 组合 DeckUiBootstrapper 的紫色主题素材，补充角色卡面与顶置立绘，方便自动化验收。
    /// </summary>
    public static class DeckIdealSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Part1/Part1_DeckIdeal.unity";
        private const string ScreenshotPath = "multi-agent-workspace/review_bundle/artifacts/screenshots/part1_deck_ideal_001.png";
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        private struct ThemeSprites
        {
            public Sprite Highlight;
            public Sprite CardBack;
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

            var bootstrapper = canvas.gameObject.AddComponent<DeckUiBootstrapper>();
            InvokeBuildLayout(bootstrapper);

            var root = canvas.transform.Find("DeckUIRoot") as RectTransform;
            if (root == null)
            {
                Debug.LogError("[DeckIdealSceneBuilder] DeckUIRoot not found after bootstrap.");
                return;
            }

            var theme = new ThemeSprites
            {
                Highlight = DeckUiThemeCache.HighlightSprite,
                CardBack = DeckUiThemeCache.CardBackSprite
            };

            AddTopCreatureArt(root, theme);
            AddFloatingCardFaces(root);
            AddHandFaces(root, theme);
            AddForegroundGlow(root, theme);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[DeckIdealSceneBuilder] Scene saved to {ScenePath}");
        }

        public static void CaptureScreenshot()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("[DeckIdealSceneBuilder] No camera found for screenshot.");
                return;
            }

            var directory = Path.GetDirectoryName(ScreenshotPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CaptureCamera(camera, ScreenshotPath);
            AssetDatabase.Refresh();
            Debug.Log($"[DeckIdealSceneBuilder] Saved screenshot: {ScreenshotPath}");
        }

        private static Camera CreateCamera()
        {
            var cameraGo = new GameObject("DeckUICamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.01f, 0.08f, 1f);
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
            rect.localScale = Vector3.one; // 避免缩放被意外设为 0 导致画面全白

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void InvokeBuildLayout(DeckUiBootstrapper bootstrapper)
        {
            var method = typeof(DeckUiBootstrapper).GetMethod("BuildLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(bootstrapper, null);
        }

        private static void AddTopCreatureArt(RectTransform root, ThemeSprites theme)
        {
            var anchor = CreateRect("TopCreature", root);
            anchor.anchorMin = new Vector2(0.5f, 1f);
            anchor.anchorMax = new Vector2(0.5f, 1f);
            anchor.pivot = new Vector2(0.5f, 1f);
            anchor.anchoredPosition = new Vector2(0f, -120f);
            anchor.sizeDelta = new Vector2(620f, 360f);

            var art = anchor.gameObject.AddComponent<Image>();
            art.sprite = LoadCardSprite("Assets/GameData/cards/advanced_card_004.png") ??
                         LoadCardSprite("Assets/GameData/cards/advanced_card_003.png");
            art.color = Color.white;
            art.preserveAspect = true;

            if (theme.Highlight != null)
            {
                var glow = CreateOverlay("TopCreatureGlow", anchor, theme.Highlight, new Color(0.9f, 0.72f, 0.28f, 0.6f), new Vector2(680f, 420f));
                glow.transform.SetAsFirstSibling();
            }
        }

        private static void AddFloatingCardFaces(RectTransform root)
        {
            var floatingRoot = root.Find("MagicCircle/FloatingCards") as RectTransform;
            if (floatingRoot == null)
            {
                return;
            }

            var face = LoadCardSprite("Assets/GameData/cards/advanced_card_002.png");
            if (face == null)
            {
                return;
            }

            foreach (Transform child in floatingRoot)
            {
                if (child.TryGetComponent<Image>(out var image))
                {
                    image.sprite = face;
                    image.color = new Color(1f, 1f, 1f, 0.92f);
                    image.preserveAspect = true;
                }
            }
        }

        private static void AddHandFaces(RectTransform root, ThemeSprites theme)
        {
            var handRoot = root.Find("BottomSection/HandCards") as RectTransform;
            if (handRoot == null)
            {
                return;
            }

            var cards = new[]
            {
                LoadCardSprite("Assets/GameData/cards/advanced_card_000.png"),
                LoadCardSprite("Assets/GameData/cards/advanced_card_001.png"),
                LoadCardSprite("Assets/GameData/cards/advanced_card_002.png"),
                LoadCardSprite("Assets/GameData/cards/advanced_card_003.png"),
                LoadCardSprite("Assets/GameData/cards/advanced_card_004.png")
            };

            float spacing = 240f;
            float startX = -spacing * 2f;
            for (int i = 0; i < 5; i++)
            {
                var sprite = cards[i] != null ? cards[i] : theme.CardBack;
                var card = CreateImage($"CardFace_{i}", handRoot, sprite, new Vector2(240f, 340f), new Vector2(startX + spacing * i, 20f));
                card.color = Color.white;
                card.preserveAspect = true;
                card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i - 2) * 3f);

                if (theme.Highlight != null)
                {
                    var glow = CreateOverlay($"CardFaceGlow_{i}", card.rectTransform, theme.Highlight, new Color(0.96f, 0.82f, 0.38f, 0.78f), new Vector2(268f, 372f));
                    glow.transform.SetAsFirstSibling();
                }
            }
        }

        private static void AddForegroundGlow(RectTransform root, ThemeSprites theme)
        {
            if (theme.Highlight == null)
            {
                return;
            }

            var overlay = CreateImage("ForegroundSparkle", root, theme.Highlight, new Vector2(0f, 0f), Vector2.zero);
            overlay.rectTransform.anchorMin = Vector2.zero;
            overlay.rectTransform.anchorMax = Vector2.one;
            overlay.rectTransform.offsetMin = Vector2.zero;
            overlay.rectTransform.offsetMax = Vector2.zero;
            overlay.color = new Color(0.7f, 0.6f, 1f, 0.18f);
            overlay.type = Image.Type.Sliced;
            overlay.raycastTarget = false;
            overlay.rectTransform.SetAsLastSibling();
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

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Sprite LoadCardSprite(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void CaptureCamera(Camera camera, string path)
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

            File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
    }
}
