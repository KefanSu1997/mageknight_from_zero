using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _headbandFlipInput;

        private void DrawHeadbandFlipInput()
        {
            if (!_headbandFlipInput || _headbandSkill == null) return;
            bool ritual = _headbandSkill.SourceId == "hero_skill_pool_003:03";
            bool disguise = _headbandSkill.SourceId == HeroSocialSkillRules.Disguise;
            var panel = W.Panel(_root, "HeadbandFlip", 0, 0, 1920, 1080, new Color(.025f, .065f, .06f, 1));
            panel.GetComponent<Image>().raycastTarget = true;
            W.Text(panel, "Title", "启迪头环 · " + _headbandSkill.Id, 40, 25, 1800, 65, 36, W.Gold);
            W.Text(panel, "Instruction", "选择本次借用的原效果：不翻面的每回合效果使用两次；翻面效果使用一次。公共技能仍在供应，个人技能状态不变。", 40, 110, 1810, 90, 25, W.Cream);
            Texture(panel, "OriginalSkillReference", SkillReference(_headbandSkill), 40, 230, 300, 420);
            W.Text(panel, "OriginalRule", _headbandSkill.PrintedRule, 400, 230, 1430, 240, 26, W.Cream);
            if (ritual)
            {
                string[] colors = { "红", "蓝", "绿", "白", "金", "黑" };
                for (int i = 0; i < colors.Length; i++)
                {
                    int option = i;
                    Button(panel, "Btn_Option_" + i, colors[i] + "色临时魔力标记 1", 400 + i % 3 * 475,
                        505 + i / 3 * 95, 450, 76, () => _publicSkillOption = option, _publicSkillOption == i);
                }
            }
            else
            {
                Button(panel, "Btn_Option_0", disguise ? "不翻面效果两次 · 影响力 2 + 2" : "不翻面效果两次 · 攻击 2 + 2",
                    400, 505, 1430, 76, () => _publicSkillOption = 0, _publicSkillOption == 0);
                Button(panel, "Btn_Option_1", disguise ? "翻面效果一次 · 本回合忽略声誉轨作用" : "翻面效果一次 · 攻击 4",
                    400, 610, 1430, 76, () => _publicSkillOption = 1, _publicSkillOption == 1);
            }
            W.Text(panel, "Scope", ritual ? "获得临时标记；按原昼夜限制支付原卡或部队费用。" : disguise
                ? "忽略轨道位置的效果；实际声誉增减继续生效，回合结束清除许可。"
                : "攻击在本次近战中使用；不会翻动玩家自己的技能标记。", 400, 740, 1430, 100, 26, W.Gold);
            W.Text(panel, "Feedback", _feedback ?? "确认前不消耗头环；翻回自有标记不是本次借用效果。", 40, 889, 1810, 70, 23, W.Cream);
            Button(panel, "Btn_Back", "返回原头环", 40, 990, 880, 60, ClearHeadbandInput);
            Button(panel, "Btn_Confirm", "确认本次原效果", 960, 990, 880, 60, () =>
            {
                _journey.UseHeadbandFlip(_headbandPhysical, _headbandSkill, _publicSkillOption);
                AfterHeadbandInput();
                _feedback = "原头环借用完成；个人技能标记与公共供应归属保留。";
            }, true);
        }
    }
}
