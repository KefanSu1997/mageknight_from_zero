using MK.Logic.Core;
using MK.Logic.Data;
using System.Linq;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 靈魂收割者：攻擊並依擊殺敵人獲得魔晶。
    /// </summary>
    public sealed class SoulHarvesterEffect : ICardEffect
    {
        private readonly bool _once;
        public SoulHarvesterEffect(bool once) => _once = once;

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            if (option != 0 && option != 1)
                throw new System.InvalidOperationException("灵魂收割者必须选择对应抗性色或白色魔晶");
            ctx.CombatPower.AddAttack(_once ? 8 : 3, crystalsPerDefeatedEnemy: _once ? 0 : 1);
            if (_once) ctx.CrystalsPerKill += 1;
            // 白色始终合法；选项0保留对应单一抗性色，选项1明确选择白色。
            ctx.CrystalOnKill = option == 1 ? _ => ManaColor.White : SelectColor;
        }

        public static ManaColor[] AvailableColors(Monster enemy)
        {
            var colors = new System.Collections.Generic.List<ManaColor>();
            if (enemy.Abilities.Contains(Ability.FireResist)) colors.Add(ManaColor.Red);
            if (enemy.Abilities.Contains(Ability.IceResist)) colors.Add(ManaColor.Blue);
            if (enemy.Abilities.Contains(Ability.PhysicalResist)) colors.Add(ManaColor.Green);
            colors.Add(ManaColor.White);
            return colors.ToArray();
        }
        private static ManaColor SelectColor(Monster enemy) => AvailableColors(enemy)[0];
    }
}
