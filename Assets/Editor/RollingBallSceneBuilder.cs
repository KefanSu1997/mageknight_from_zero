using System.IO;
using MageKnight.Scripts.Demo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.EditorTools
{
    public static class RollingBallSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/RollingBallDemo.unity";
        private const string ScreenshotDir = "multi-agent-workspace/review_bundle/artifacts/screenshots";

        [MenuItem("Tools/Scene Builders/Build Rolling Ball Demo")]
        public static void BuildSceneMenu() => BuildScene();

        [MenuItem("Tools/Scene Builders/Build + Capture Rolling Ball")]
        public static void BuildAndCaptureMenu()
        {
            BuildScene();
            CaptureScreenshots();
        }

        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            SetupLighting();
            CreateEnvironmentAndBall(out _);
            CreateUiHint();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[RollingBallSceneBuilder] Scene saved to {ScenePath}");
        }

        private static void CaptureScreenshots()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SetActiveScene(scene);

            var camera = Object.FindFirstObjectByType<Camera>();
            var ball = GameObject.Find("PlayerBall");

            if (camera == null || ball == null)
            {
                Debug.LogError("[RollingBallSceneBuilder] Missing camera or ball for screenshots.");
                return;
            }

            Directory.CreateDirectory(ScreenshotDir);

            PositionForSlope(camera.transform, ball.transform);
            Capture(camera, Path.Combine(ScreenshotDir, "rolling_slope_001.png"));

            PositionForBlock(camera.transform, ball.transform);
            Capture(camera, Path.Combine(ScreenshotDir, "rolling_block_001.png"));

            AssetDatabase.Refresh();
            Debug.Log($"[RollingBallSceneBuilder] Screenshots captured in {ScreenshotDir}");
        }

        private static void PositionForSlope(Transform cameraTransform, Transform ball)
        {
            ResetBall(ball, new Vector3(0f, 2.2f, 6.5f));
            cameraTransform.position = new Vector3(-6.5f, 7f, -5f);
            cameraTransform.LookAt(ball.position + new Vector3(0f, 0.4f, 1f));
        }

        private static void PositionForBlock(Transform cameraTransform, Transform ball)
        {
            ResetBall(ball, new Vector3(1.4f, 1f, 10.8f));
            cameraTransform.position = new Vector3(5.5f, 7f, 16f);
            cameraTransform.LookAt(ball.position + new Vector3(0f, 0.4f, 0f));
        }

        private static void ResetBall(Transform ball, Vector3 targetPosition)
        {
            ball.position = targetPosition;
            ball.rotation = Quaternion.identity;
            if (ball.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private static void CreateEnvironmentAndBall(out Camera camera)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(18f, 1f, 24f);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.position = new Vector3(0f, 1.25f, 6f);
            ramp.transform.rotation = Quaternion.Euler(-25f, 0f, 0f);
            ramp.transform.localScale = new Vector3(8f, 0.5f, 10f);

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "ObstacleBlock";
            block.transform.position = new Vector3(2f, 0.75f, 12f);
            block.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            block.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PlayerBall";
            ball.transform.position = new Vector3(0f, 1f, -8f);
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var controller = ball.AddComponent<RollingBallController>();

            var cameraGo = new GameObject("Main Camera");
            camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 6f, -12f);
            camera.transform.LookAt(ball.transform.position + new Vector3(0f, 1.2f, 4f));

            var follow = cameraGo.AddComponent<RollingBallCameraFollow>();
            follow.SetTarget(ball.transform);
            controller.SetFollowCamera(camera);
        }

        private static void SetupLighting()
        {
            var light = new GameObject("Directional Light", typeof(Light));
            var lightComp = light.GetComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateUiHint()
        {
            var canvas = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasComp = canvas.GetComponent<Canvas>();
            canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComp.sortingOrder = 10;

            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var hintText = new GameObject("Txt_Hint", typeof(TextMeshProUGUI));
            hintText.transform.SetParent(canvas.transform, false);
            var tmp = hintText.GetComponent<TextMeshProUGUI>();
            tmp.text = "WASD 滚动小球，撞击方块";
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 1f, 1f, 0.9f);

            var rect = tmp.rectTransform;
            rect.anchorMin = new Vector2(0.03f, 0.03f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Capture(Camera camera, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var renderTexture = new RenderTexture(1920, 1080, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log($"[RollingBallSceneBuilder] Saved screenshot: {path}");

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            Object.DestroyImmediate(texture);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
        }
    }
}
