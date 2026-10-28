using System.Linq;
using MK.Logic.Data;
using MK.Logic.Data.Cards;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Printed source text is independent of the currently selected card/mode.
        // Hovering another physical hand instance never borrows _choice/_artifactBurn.
        private string CardReadingText(DeedCard card)
        {
            var artifact = ArtifactBinding(card);
            if (artifact != null)
                return "原卡印刷规则\n\n普通效果\n" + artifact.source.AssignEffect +
                    "\n\n焚毁效果\n" + artifact.source.OncePerRound +
                    "\n\n实际模式、附着或移除条件请在正式出牌处核对。";
            var binding = Binding(card);
            if (binding?.source is SpellCardSO spell)
                return "原卡印刷规则\n\n" + spell.Top.Name + "\n" + spell.Top.Effect +
                    "\n印刷费用：" + string.Join("、", spell.Top.ManaCost.Select(ColorName)) +
                    "\n\n" + spell.Bottom.Name + "\n" + spell.Bottom.Effect +
                    "\n印刷费用：" + string.Join("、", spell.Bottom.ManaCost.Select(ColorName)) +
                    "\n\n强力法术通常须黑夜。当前可用模式与实际费用以正式出牌处为准。";
            if (binding != null)
            {
                var source = binding.Action();
                return "原卡印刷规则\n\n基础效果\n" + source.BaseEffect +
                    "\n\n强化效果\n" + source.EnhancedEffect +
                    "\n印刷费用：" + string.Join("、", source.RequiredCrystals.Select(ColorName)) +
                    "\n\n额外费用、免费强化与当前合法模式请在正式出牌处核对。";
            }
            return "创伤通常不能正常打出或横置，会占据手牌空间。\n\n" +
                (_battle.Context.SidewaysWoundValue > 0 ? "当前效果允许将创伤横置为 " + _battle.Context.SidewaysWoundValue + " 点。" : "当前没有允许横置创伤的效果。");
        }
    }
}
