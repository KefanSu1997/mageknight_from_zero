using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>
    /// 魅惑 / 附身：影響力或奪取敵人攻擊力。
    /// option 低8位為顏色(基礎選擇水晶時)，高8位 1 表減招募費。
    /// </summary>
    public sealed class CharmEffect : ICardEffect, ICardEffectValidator
    {
        private readonly bool _enh;
        public CharmEffect(bool enhanced) => _enh = enhanced;

        public void Validate(PlayerState player, ActionContext ctx, int option)
        {
            if (_enh && (ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count))
                throw new System.InvalidOperationException("附身必须选择当前战斗中的敌人");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.InfluencePool += 4;
                if (ctx.InNegotiation)
                {
                    bool reduce = (option >> 8) != 0;
                    ManaColor color = (ManaColor)(option & 0xFF);
                    if (reduce)
                        ctx.RecruitDiscount += 3;
                    else
                        player.Mana.AddCrystal(color,1);
                }
            }
            else
            {
                ctx.SkipAttackIndices.Add(option);
                ctx.PossessedEnemyIndices.Add(option);
                var enemy = ctx.Enemies[option];
                // 只复制牌面攻击与元素，不复制迅捷、残暴、毒素等能力。
                ctx.CombatPower.AddAttack(enemy.Attack, enemy.AttackElement, excludedTargetIndex: option);
            }
        }
    }
}
