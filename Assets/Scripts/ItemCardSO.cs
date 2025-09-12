using System.Text;
using UnityEngine;
using MK.Logic.Data.Cards;
using MK.Logic.Core;

/// <summary>
/// 神器或物品牌資料的 ScriptableObject。
/// </summary>
[CreateAssetMenu(menuName = "MageKnight/Card/ItemCard")]
public class ItemCardSO : CardSO
{
    [Tooltip("物品類型描述")]
    public string ItemType;
    [TextArea]
    [Tooltip("分配效果文本")]
    public string AssignEffect;
    [TextArea]
    [Tooltip("每回合一次效果文本")]
    public string OncePerRound;
    

    /// <summary>
    /// 從資料記錄轉成 ScriptableObject。
    /// </summary>
    public static ItemCardSO FromData(ItemCardData data)
    {
        var so = CreateInstance<ItemCardSO>();
        so.Id = data.Id;
        so.NameCn = data.Name;
        so.Set = data.Set;
        so.ImagePath = data.ImagePath;
        so.EnImagePath = data.EnImagePath;
        so.ItemType = data.ItemType;
        so.AssignEffect = data.AssignEffect;
        so.OncePerRound = data.OncePerRound;
        return so;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(base.ToString());
        sb.AppendLine($" 〈{ItemType}〉");
        if (!string.IsNullOrWhiteSpace(AssignEffect))
            sb.AppendLine($"  - 分配：{AssignEffect}");
        if (!string.IsNullOrWhiteSpace(OncePerRound))
            sb.AppendLine($"  - 每回合一次：{OncePerRound}");
        return sb.ToString();
    }
}
