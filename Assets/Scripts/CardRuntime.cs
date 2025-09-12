using UnityEngine;
using UnityEngine.UI;
using TMPro;                     // ← 记得加
using System;
using System.Linq;
using UnityEngine.ResourceManagement.AsyncOperations;

public class CardRuntime : MonoBehaviour
{
    public CardSO Data { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;

    private AsyncOperationHandle<Sprite> _handle;

    #nullable enable
public event Action<CardSO> OnCardInfoChanged = delegate {};

    public void Init(CardSO data)
    {
        Data = data;

        // 重置旧的贴图，避免显示缓存残影
        if (artwork != null)
        {
            artwork.sprite = null;
            artwork.material = null;   // 清掉不正确的材质，如 Default-Skybox
        }

        RefreshFace();
        OnCardInfoChanged?.Invoke(Data);

        // 限制显示一行，溢出显示省略号
        nameLabel.maxVisibleLines = 1;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;
    }

    [Header("Selection Settings")]
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);

    private bool isSelected = false;

    private void RefreshFace()
    {
        // 文本更新
        nameLabel.text = Data.NameCn;
        if (Data is ActionCardSO action)
            costLabel.text = string.Join(", ", action.RequiredCrystals.Select(c => c.ToString()));
        else if (Data is SpellCardSO spell)
            costLabel.text = string.Join(", ", spell.Top.ManaCost.Select(c => c.ToString()));
        else
            costLabel.text = "—";

        // 如有旧句柄，先释放资源，避免内存累积
        if (_handle.IsValid())
        {
            Data.ReleaseSprite();
        }

        // 使用 ImagePath 异步加载卡图
        _handle = Data.LoadSpriteAsync();
        _handle.Completed += h =>
        {
            if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null)
            {
                artwork.sprite = h.Result;
                // 确保使用 Unity 默认的 UI 材质，而不是 Skybox 等 3D 材质
                artwork.material = null;
                // 若需要原始尺寸，可启用以下一行
                // artwork.SetNativeSize();
            }
            else
            {
                Debug.LogError($"加载卡图失败：{Data.ImagePath}");
            }
        };

        // 初始化选择高亮
        if (selectionHighlight != null)
        {
            selectionHighlight.color = normalColor;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionHighlight != null)
        {
            selectionHighlight.color = selected ? selectedColor : normalColor;
        }
    }

    public bool IsSelected => isSelected;

    private void OnDestroy()
    {
        // 释放资源，避免内存泄漏
        if (Data != null)
        {
            Data.ReleaseSprite();
        }
    }
}
