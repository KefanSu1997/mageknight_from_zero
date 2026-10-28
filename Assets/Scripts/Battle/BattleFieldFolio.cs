using UnityEngine;
using UnityEngine.UI;

namespace MageKnight.Battle
{
    // A field-manual leaf for temporary inspection. The cut corners, folded
    // tip, binding and restrained fibers belong to the same bronze/linen family
    // as battle controls; no decorative geometry receives pointer events.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleFieldFolio : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            Color ink = new Color32(111, 83, 44, 255);
            Color paperTop = new Color32(233, 221, 189, 255);
            Color paperBottom = new Color32(214, 199, 160, 255);
            Vector2[] points = {
                new Vector2(r.xMin + 8, r.yMin), new Vector2(r.xMax - 8, r.yMin),
                new Vector2(r.xMax, r.yMin + 8), new Vector2(r.xMax, r.yMax - 21),
                new Vector2(r.xMax - 21, r.yMax), new Vector2(r.xMin + 8, r.yMax),
                new Vector2(r.xMin, r.yMax - 8), new Vector2(r.xMin, r.yMin + 8)
            };
            void Vertex(Vector2 p, Color c) => mesh.AddVert(p, c, Vector2.zero);
            void Fill(Vector2 offset, bool shadow)
            {
                int start = mesh.currentVertCount;
                Vertex(r.center + offset, shadow ? new Color(0, .02f, .015f, .42f) : Color.Lerp(paperBottom, paperTop, .5f));
                foreach (var p in points)
                    Vertex(p + offset, shadow ? new Color(0, .02f, .015f, .42f)
                        : Color.Lerp(paperBottom, paperTop, Mathf.InverseLerp(r.yMin, r.yMax, p.y)));
                for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % points.Length + 1);
            }
            void Line(Vector2 a, Vector2 b, float width, Color c)
            {
                Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2;
                int k = mesh.currentVertCount;
                Vertex(a + n, c); Vertex(a - n, c); Vertex(b + n, c); Vertex(b - n, c);
                mesh.AddTriangle(k, k + 1, k + 2); mesh.AddTriangle(k + 1, k + 3, k + 2);
            }
            Fill(new Vector2(3, -5), true);
            Fill(Vector2.zero, false);
            for (int i = 0; i < points.Length; i++) Line(points[i], points[(i + 1) % points.Length], 1.5f, ink);
            Line(new Vector2(r.xMin + 8, r.yMin + 12), new Vector2(r.xMin + 8, r.yMax - 12), 2, new Color(.48f, .33f, .15f, .35f));
            // Folded corner and deterministic fine fibers, independent of game RNG.
            int tip = mesh.currentVertCount;
            Vertex(new Vector2(r.xMax - 21, r.yMax), new Color32(184, 162, 115, 255));
            Vertex(new Vector2(r.xMax - 21, r.yMax - 21), paperTop);
            Vertex(new Vector2(r.xMax, r.yMax - 21), new Color32(184, 162, 115, 255));
            mesh.AddTriangle(tip, tip + 1, tip + 2);
            for (int i = 0; i < 24; i++)
            {
                float y = r.yMin + 16 + (r.height - 32) * (i + .4f) / 24;
                float x = r.xMin + 14 + ((i * 73) % 211) / 211f * (r.width - 60);
                Line(new Vector2(x, y), new Vector2(Mathf.Min(x + 15 + i % 9, r.xMax - 12), y + .5f), .5f, new Color(.45f, .32f, .14f, .045f));
            }
        }
    }
}
