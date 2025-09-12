using System.Text;
using UnityEngine;
using MK.Logic.Core;
using MK.Logic.Data.Cards;

/// <summary>
/// 普通或高級行動牌的 ScriptableObject 版本。
/// </summary>
[CreateAssetMenu(menuName = "MageKnight/Card/ActionCard")]
public class ActionCardSO : CardSO
{
    [TextArea]
    [Tooltip("基礎效果文字")]
    public string BaseEffect;

    [TextArea]
    [Tooltip("強化效果文字")]
    public string EnhancedEffect;

    [Tooltip("啟動強化所需魔晶顏色")]
    public ManaColor[] RequiredCrystals;

    /// <summary>
    /// 由資料記錄快速轉成 ScriptableObject 以利產生資源檔。
    /// </summary>
    public static ActionCardSO FromData(ActionCardData data)
    {
        var so = CreateInstance<ActionCardSO>();
        so.Id = data.Id;
        so.NameCn = data.Name;
        so.Set = data.Set;
        so.ImagePath = data.ImagePath;
        so.EnImagePath = data.EnImagePath;
        so.BaseEffect = data.BaseEffect;
        so.EnhancedEffect = data.EnhancedEffect;
        so.RequiredCrystals = data.RequiredCrystals;
        return so;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(base.ToString());              // 牌组 & 名称
        sb.AppendLine(" 〈行动牌〉");
        sb.AppendLine($"  - 基础：{BaseEffect}");
        sb.AppendLine($"  - 强化：{EnhancedEffect}");
        if (RequiredCrystals != null && RequiredCrystals.Length > 0)
            sb.AppendLine($"  - 强化消耗：{string.Join(", ", RequiredCrystals)}");
        return sb.ToString();
    }
}
