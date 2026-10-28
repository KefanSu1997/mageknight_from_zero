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
            if (!_enh && option != 256 && (option < (int)ManaColor.Red || option > (int)ManaColor.White))
                throw new System.InvalidOperationException("魅惑必须选择红蓝绿白魔晶或一次招募减费");
            if (_enh && (!ctx.InBattle || ctx.Enemies == null || option < 0 || option >= ctx.Enemies.Count))
                throw new System.InvalidOperationException("附身必须选择当前战斗中的敌人");
            if (_enh && System.Linq.Enumerable.Contains(ctx.Enemies[option].Abilities, Ability.MagicResist))
                throw new System.InvalidOperationException("奥术免疫敌人不能成为附身目标");
        }

        public void Execute(PlayerState player, ActionContext ctx, int option = 0)
        {
            Validate(player, ctx, option);
            if (!_enh)
            {
                ctx.InfluencePool += 4;
                if (ctx.InNegotiation)
                {
                    bool reduce = option == 256;
                    ManaColor color = (ManaColor)(option & 0xFF);
                    if (reduce)
                        ctx.CharmRecruitDiscount += 3;
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
                ctx.CombatPower.AddAttack(enemy.Attack, enemy.AttackElement, excludedTargetIndex: option, excludedEnemy: enemy);
            }
        }
    }
}
