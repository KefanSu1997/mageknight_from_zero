using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 地脈震顫 / 地動山搖：降低敵人護甲。
    /// option >=0 指定目標索引，-1 表示全部敵人。
    /// 需要於 <see cref="ActionContext.Enemies"/> 提供敵人列表以判斷城防。
    /// </summary>
    public sealed class EarthTremorEffect : ICardEffect
    {
        private readonly bool _enh;
        public EarthTremorEffect(bool enhanced) => _enh = enhanced;

        public void Execute(PlayerState player, ActionContext ctx, int option = -1)
        {
            if (ctx.Enemies == null) return;
            if (option >= 0)
            {
                int val = 3;
                if (_enh && ctx.Enemies.Count > option && ctx.Enemies[option].Abilities.Contains(Ability.Fortified))
                    val = 6;
                ctx.ArmorReduction[option] = val;
            }
            else
            {
                for (int i = 0; i < ctx.Enemies.Count; i++)
                {
                    int val = 2;
                    if (_enh && ctx.Enemies[i].Abilities.Contains(Ability.Fortified))
                        val = 4;
                    ctx.ArmorReduction[i] = val;
                }
            }
        }
    }
}
