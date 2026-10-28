using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.SceneAutomation
{
    public sealed partial class StructuredUiPilot
    {
        [Serializable] public class EvaluationEvidence
        {
            public string coordinateSystem = "Game View pixels, top-left origin; x right, y down";
            public string scope = "Current generated UI geometry and vertex colors; not pixel contrast, subject masks, occlusion proof or human readability. No mesh/layout regeneration is requested.";
            public string visibility = "Active enabled canvas graphics, nonzero component/inherited alpha, renderer not culled and rect intersecting screen. Glyph bounds are generated quads before partial mask clipping; colors precede material/shader/texture compositing. Whitespace is excluded; hasVisibleGlyphs=false means height range unavailable.";
            public int screenWidth, screenHeight, frameCount;
            public List<EvaluationText> texts = new();
            public List<EvaluationImage> rawImages = new();
            public List<EvaluationCanvas> canvases = new();
            public List<EvaluationControl> controls = new();
            public List<EvaluationControl> presentationSurfaces = new();
        }
        [Serializable] public class EvaluationControl
        {
            public string path, name;
            public Rect screenRect;
            public float effectiveRendererAlpha;
        }
        [Serializable] public class EvaluationGlyph
        {
            public int sourceIndex, stringLength;
            public string character;
            public Rect screenRect;
            public Color bottomLeftColor, topLeftColor, topRightColor, bottomRightColor;
        }
        [Serializable] public class EvaluationText
        {
            public string path, name, fullText, font, overflowMode;
            public float fontSize, effectiveRendererAlpha;
            public bool isTextOverflowing;
            public int firstOverflowCharacterIndex, generatedCharacterCount;
            public Rect screenRect;
            public Color componentColor;
            public bool hasVisibleGlyphs;
            public float minGlyphHeightPixels, maxGlyphHeightPixels;
            public List<EvaluationGlyph> glyphs = new();
        }
        [Serializable] public class EvaluationImage
        {
            public string path, name, texture;
            public string material, shader;
            public bool shaderSupported;
            public float atmosphereSaturation;
            public Color atmosphereTop, atmosphereGround;
            public Vector4 atmosphereBodyUV;
            public float projectionOpacity, projectionShear;
            public int sourceWidth, sourceHeight;
            public Rect screenRect, uvRect;
            public Color componentColor;
            public float effectiveRendererAlpha;
        }
        [Serializable] public class EvaluationCanvas
        {
            public string path, renderMode, camera;
            public float scaleFactor;
            public Rect pixelRect, screenRect;
        }

        private static Camera EvaluationCamera(Canvas canvas)
        {
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static Rect EvaluationBounds(Transform transform, Camera camera, params Vector3[] localPoints)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var local in localPoints)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(local));
                screen.y = Screen.height - screen.y;
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect EvaluationRect(RectTransform rect, Camera camera)
        {
            var r = rect.rect;
            return EvaluationBounds(rect, camera, new Vector3(r.xMin, r.yMin), new Vector3(r.xMin, r.yMax),
                new Vector3(r.xMax, r.yMax), new Vector3(r.xMax, r.yMin));
        }

        private static bool EvaluationVisible(Graphic graphic)
        {
            if (!graphic.isActiveAndEnabled || graphic.canvas == null || !graphic.canvas.isActiveAndEnabled ||
                graphic.canvasRenderer.cull || graphic.canvasRenderer.GetInheritedAlpha() <= 0 || graphic.color.a <= 0) return false;
            return EvaluationRect(graphic.rectTransform, EvaluationCamera(graphic.canvas))
                .Overlaps(new Rect(0, 0, Screen.width, Screen.height));
        }

        private EvaluationEvidence CaptureEvaluationEvidence()
        {
            var result = new EvaluationEvidence { screenWidth = Screen.width, screenHeight = Screen.height, frameCount = Time.frameCount };
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).OrderBy(t => ObjectPath(t.transform)))
            {
                // UI TMP only: world-space mesh renderers require a separate visibility contract.
                Graphic graphic = text;
                if (!EvaluationVisible(graphic)) continue;
                var camera = EvaluationCamera(graphic.canvas);
                var row = new EvaluationText { path = ObjectPath(text.transform), name = text.name,
                    fullText = text.text, font = text.font != null ? text.font.name : null, fontSize = text.fontSize,
                    overflowMode = text.overflowMode.ToString(), isTextOverflowing = text.isTextOverflowing,
                    firstOverflowCharacterIndex = text.firstOverflowCharacterIndex,
                    screenRect = EvaluationRect(text.rectTransform, camera), componentColor = text.color,
                    effectiveRendererAlpha = graphic.canvasRenderer.GetInheritedAlpha() };
                var info = text.textInfo;
                row.generatedCharacterCount = info == null ? 0 : info.characterCount;
                for (int i = 0; info != null && i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible || char.IsWhiteSpace(ch.character)) continue;
                    var bounds = EvaluationBounds(text.transform, camera, ch.bottomLeft, ch.topLeft, ch.topRight, ch.bottomRight);
                    row.glyphs.Add(new EvaluationGlyph { sourceIndex = ch.index, stringLength = ch.stringLength,
                        character = ch.character.ToString(), screenRect = bounds,
                        bottomLeftColor = ch.vertex_BL.color, topLeftColor = ch.vertex_TL.color,
                        topRightColor = ch.vertex_TR.color, bottomRightColor = ch.vertex_BR.color });
                    row.minGlyphHeightPixels = row.hasVisibleGlyphs ? Mathf.Min(row.minGlyphHeightPixels, bounds.height) : bounds.height;
                    row.maxGlyphHeightPixels = Mathf.Max(row.maxGlyphHeightPixels, bounds.height);
                    row.hasVisibleGlyphs = true;
                }
                result.texts.Add(row);
            }
            foreach (var image in FindObjectsByType<RawImage>(FindObjectsSortMode.None).Where(EvaluationVisible).OrderBy(t => ObjectPath(t.transform)))
                result.rawImages.Add(new EvaluationImage { path = ObjectPath(image.transform), name = image.name,
                    texture = image.texture != null ? image.texture.name : null,
                    sourceWidth = image.texture != null ? image.texture.width : 0,
                    sourceHeight = image.texture != null ? image.texture.height : 0,
                    screenRect = EvaluationRect(image.rectTransform, EvaluationCamera(image.canvas)),
                    uvRect = image.uvRect, componentColor = image.color,
                    material = image.material != null ? image.material.name : "",
                    shader = image.material != null && image.material.shader != null ? image.material.shader.name : "",
                    shaderSupported = image.material != null && image.material.shader != null && image.material.shader.isSupported,
                    atmosphereSaturation = image.material != null && image.material.HasProperty("_Saturation") ? image.material.GetFloat("_Saturation") : 1,
                    atmosphereTop = image.material != null && image.material.HasProperty("_TopLight") ? image.material.GetColor("_TopLight") : Color.white,
                    atmosphereGround = image.material != null && image.material.HasProperty("_GroundLight") ? image.material.GetColor("_GroundLight") : Color.white,
                    atmosphereBodyUV = image.material != null && image.material.HasProperty("_BodyUV") ? image.material.GetVector("_BodyUV") : Vector4.zero,
                    projectionOpacity = image.material != null && image.material.HasProperty("_Opacity") ? image.material.GetFloat("_Opacity") : 0,
                    projectionShear = image.GetComponent<MageKnight.Battle.BattleGroundShear>() != null ? image.GetComponent<MageKnight.Battle.BattleGroundShear>().spread : 0,
                    effectiveRendererAlpha = image.canvasRenderer.GetInheritedAlpha() });
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.isActiveAndEnabled).OrderBy(b => ObjectPath(b.transform)))
            {
                var canvas = button.GetComponentInParent<Canvas>();
                var transform = button.transform as RectTransform;
                if (canvas == null || !canvas.isActiveAndEnabled || transform == null) continue;
                if (button.targetGraphic != null && !EvaluationVisible(button.targetGraphic)) continue;
                var bounds = EvaluationRect(transform, EvaluationCamera(canvas));
                if (!bounds.Overlaps(new Rect(0, 0, Screen.width, Screen.height))) continue;
                result.controls.Add(new EvaluationControl { path = ObjectPath(button.transform), name = button.name, screenRect = bounds });
            }
            foreach (var graphic in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                if ((graphic is MageKnight.Battle.BattleFieldMark || graphic is MageKnight.Battle.BattleRuneMount || graphic is MageKnight.Battle.BattleLabelBacking
                    || graphic is MageKnight.Battle.BattleDock66Shape || graphic.name.EndsWith("Folio66", StringComparison.Ordinal) || graphic.name == "Dock66SharedFolio"
                    || graphic.name == "CompactHeaderShade" || graphic.name == "ManaInventoryShade" || graphic.name == "ManaReliquary" || graphic.name == "PhaseBackdrop"
                    || graphic.name == "HeroCaptionShade" || graphic.name == "CaptionShade" || graphic.name == "ResourceReadability"
                    || graphic.name == "UtilityIconShade" || graphic.name == "ActionReadability" || graphic.name == "ThreatBadge"
                    || graphic.name == "BattleHoverInfo" || graphic.name == "ActorHoverFocus" || graphic.name == "HelpBadge"
                    || graphic.name == "BattleToastSoftBacking" || graphic.name == "ManaTokenSigil" || graphic.name == "ManaCrystalGem" || graphic.name == "SourceDieFace"
                    || graphic.name == "ReadinessSymbol" || graphic.name == "WoundSymbol" || graphic.name == "HeroLeadershipCrest"
                    || (graphic.name == "Glyph" && graphic.transform.parent.name.StartsWith("Unit", StringComparison.Ordinal)
                        && graphic.transform.parent.name.EndsWith("Fact", StringComparison.Ordinal))) && EvaluationVisible(graphic))
                    result.presentationSurfaces.Add(new EvaluationControl { path=ObjectPath(graphic.transform), name=graphic.name,
                        screenRect=EvaluationRect(graphic.rectTransform,EvaluationCamera(graphic.canvas)),
                        effectiveRendererAlpha=graphic.canvasRenderer.GetInheritedAlpha() });
            foreach (var mask in FindObjectsByType<RectMask2D>(FindObjectsSortMode.None))
                if ((mask.name == "EntityHoverViewport" || mask.name == "ChoiceViewport") && mask.isActiveAndEnabled)
                    result.presentationSurfaces.Add(new EvaluationControl { path=ObjectPath(mask.transform), name=mask.name,
                        screenRect=EvaluationRect((RectTransform)mask.transform,EvaluationCamera(mask.GetComponentInParent<Canvas>())) });
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled).OrderBy(c => ObjectPath(c.transform)))
            {
                var pixels = canvas.pixelRect;
                pixels.y = Screen.height - pixels.yMax;
                result.canvases.Add(new EvaluationCanvas { path = ObjectPath(canvas.transform), renderMode = canvas.renderMode.ToString(),
                    camera = canvas.worldCamera != null ? ObjectPath(canvas.worldCamera.transform) : null,
                    scaleFactor = canvas.scaleFactor, pixelRect = pixels,
                    screenRect = EvaluationRect((RectTransform)canvas.transform, EvaluationCamera(canvas)) });
            }
            return result;
        }
    }
}
