using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UiDocumentBinder : MonoBehaviour
{
    public UIDocument uiDoc;
    public string resourcesImageRoot = "UI/Images";
    public List<string> deckCardImageNames = new(); // 例如 ["Fireball", "IceBolt", ...]

    void Awake()
    {
        if (uiDoc == null) uiDoc = GetComponent<UIDocument>();
        var root = uiDoc?.rootVisualElement;
        if (root == null) return;

        // 1) 加载 USS（可选：如果 UXML 未内联 USS）
        var styleSheet = Resources.Load<StyleSheet>("UI/Views/Generated/DeckViewer"); 
        if (styleSheet != null) root.styleSheets.Add(styleSheet);

        // 2) 绑定按钮事件
        var btn = root.Q<Button>("Btn_Close");
        if (btn != null) btn.clicked += () => gameObject.SetActive(false);

        // 3) 动态填充卡图网格
        var grid = root.Q<VisualElement>("VE_Grid");
        if (grid != null)
        {
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;

            foreach (var name in deckCardImageNames)
            {
                var img = new Image { name = $"Img_{name}" };
                var tex = Resources.Load<Texture2D>($"{resourcesImageRoot}/{name}");
                if (tex != null) img.image = tex;
                img.style.width  = 128;
                img.style.height = 192;
                img.style.marginBottom = 8;
                img.style.marginRight  = 8;
                grid.Add(img);
            }

            // 下一帧请求重绘，确保首帧就能看到
            root.schedule.Execute(() => root.MarkDirtyRepaint()).StartingIn(0);
        }
    }
}
