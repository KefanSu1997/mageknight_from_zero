using UnityEngine;
using System.Text;
using MK.Logic.Core;
using MK.Logic.Data.Cards;

/// <summary>
/// 法術牌所對應的 ScriptableObject。
/// </summary>
[CreateAssetMenu(menuName = "MageKnight/Card/SpellCard")]
public class SpellCardSO : CardSO
{
    [System.Serializable]
    public class HalfSpell
    {
        public string Name;
        [TextArea]
        public string Effect;
        public ManaColor[] ManaCost;
    }

    [Tooltip("法術主色")]
    public ManaColor ManaColor;

    public HalfSpell Top;
    public HalfSpell Bottom;

    /// <summary>
    /// 從資料記錄生成 ScriptableObject。
    /// </summary>
    public static SpellCardSO FromData(SpellCardData data)
    {
        var so = CreateInstance<SpellCardSO>();
        so.Id = data.Id;
        so.NameCn = data.Name;
        so.Set = data.Set;
        so.ImagePath = data.ImagePath;
        so.EnImagePath = data.EnImagePath;
        so.ManaColor = data.ManaColor;
        so.Top = new HalfSpell { Name = data.Top.Name, Effect = data.Top.Effect, ManaCost = data.Top.ManaCost };
        so.Bottom = new HalfSpell { Name = data.Bottom.Name, Effect = data.Bottom.Effect, ManaCost = data.Bottom.ManaCost };
        return so;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(base.ToString());
        sb.AppendLine($" 〈法术〉主色:{ManaColor}");
        AppendHalf("● 上半", Top, sb);
        AppendHalf("● 下半", Bottom, sb);
        return sb.ToString();
    }

    private static void AppendHalf(string title, HalfSpell half, StringBuilder sb)
    {
        if (half == null) return;
        sb.AppendLine(title);
        sb.AppendLine($"    名称：{half.Name}");
        sb.AppendLine($"    效果：{half.Effect}");
        if (half.ManaCost != null && half.ManaCost.Length > 0)
            sb.AppendLine($"    魔晶：{string.Join(", ", half.ManaCost)}");
    }
}
