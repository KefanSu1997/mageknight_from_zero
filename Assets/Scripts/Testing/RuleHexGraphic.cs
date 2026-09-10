using UnityEngine;
using UnityEngine.UI;

/// <summary>地图六边格采用真正的UI几何体，文字和点击区域独立于地图插画。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RuleHexGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        var r = rectTransform.rect;
        helper.AddVert(r.center, color, Vector2.one * .5f);
        for (int i = 0; i < 6; i++)
        {
            float angle = (90 + i * 60) * Mathf.Deg2Rad;
            helper.AddVert(r.center + new Vector2(Mathf.Cos(angle) * r.width / 2, Mathf.Sin(angle) * r.height / 2), color, Vector2.zero);
        }
        for (int i = 0; i < 6; i++) helper.AddTriangle(0, i + 1, (i + 1) % 6 + 1);
    }

    public override bool Raycast(Vector2 point, Camera eventCamera)
    {
        if (!base.Raycast(point, eventCamera)) return false;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, eventCamera, out var local);
        var r = rectTransform.rect;
        float x = Mathf.Abs((local.x - r.center.x) / (r.width * .5f));
        float y = Mathf.Abs((local.y - r.center.y) / (r.height * .5f));
        return x <= .8661f && y <= 1 - x / 1.73205f;
    }
}
