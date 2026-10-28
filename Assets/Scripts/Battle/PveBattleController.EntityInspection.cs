using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private BattleInspectionData HeroInspection()
        {
            var data = new BattleInspectionData { role = "英雄 · 等级" + _battle.Player.Level, identityGlyph = 3,
                hint = "点击角色查看英雄技能。" };
            data.facts.Add(new BattleInspectionFact("HeroArmor", "护甲", _battle.Player.Armor.ToString(), 5));
            data.facts.Add(new BattleInspectionFact("HeroFame", "名望", _battle.Player.Fame.ToString(), 8));
            data.facts.Add(new BattleInspectionFact("HeroWounds", "创伤", _battle.Player.Wounds.ToString(), 2));
            var colors = new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green, ManaColor.White, ManaColor.Gold, ManaColor.Black };
            for (int i = 0; i < colors.Length; i++)
                data.resources.Add(new BattleInspectionResource { id = colors[i].ToString(), label = ManaShort(colors[i]),
                    element = i, mana = ManaDisplay(colors[i]), crystal = i < 4 ? Crystal(colors[i]).ToString() : null });
            return data;
        }

        private BattleInspectionData UnitInspection(UnitState unit, string originalActions)
        {
            var data = new BattleInspectionData { role = "部队", identityGlyph = 4,
                hint = "点击角色选择原能力；实际费用以动作区为准。" };
            int armor = unit.Card.Armor + _battle.Context.UnitArmorBonus
                + BannerActions.HonorBonus(_battle.Context, unit) + BannerActions.ProtectionArmor(unit);
            data.facts.Add(new BattleInspectionFact("UnitLevel", "等级", unit.Card.Level.ToString(), 4));
            data.facts.Add(new BattleInspectionFact("UnitArmor", "护甲", armor.ToString(), 5));
            data.facts.Add(new BattleInspectionFact("UnitWounds", "创伤", unit.Wounds.ToString(), 2));
            string[] lines = UnitText(unit).Split('\n');
            data.sections.Add(new BattleInspectionSection("UnitState", "当前状态", lines.Length > 1 ? lines[1] : "", unit.IsReady ? 0 : 1));
            if (!UnitParticipation.CanUse(_battle.Player, _battle.Context, unit))
                data.sections.Add(new BattleInspectionSection("UnitSite", "地点限制", "地下城禁止使用此部队。\n只有忠诚契约实际指挥的部队例外。", 6));
            // Keep every canonical non-numeric rule line, including actual fees,
            // banners and resistances; only the already displayed stat row moves.
            string remaining = string.Join("\n", lines.Skip(2));
            if (!string.IsNullOrWhiteSpace(remaining))
                data.sections.Add(new BattleInspectionSection("UnitConditions", "费用、抗性与规则", remaining, 10));
            data.sections.Add(new BattleInspectionSection("UnitAbilities", "原能力与印刷费用", originalActions, 10));
            return data;
        }

        private BattleInspectionData EnemyInspection(string canonicalReadout)
        {
            var data = new BattleInspectionData { role = "敌人", identityGlyph = 9,
                hint = "点击实体选择正式目标。" };
            string[] lines = canonicalReadout.Split('\n');
            if (lines.Length > 0) data.sections.Add(new BattleInspectionSection("EnemyThreat", "威胁", lines[0], 9));
            if (lines.Length > 1) data.sections.Add(new BattleInspectionSection("EnemyDefense", "防御与名望", lines[1], 5));
            if (lines.Length > 2) data.sections.Add(new BattleInspectionSection("EnemyTraits", "特性", string.Join("\n", lines.Skip(2)), 10));
            data.sections.Add(new BattleInspectionSection("PhaseRules", "当前阶段规则", RulesText(), 10));
            return data;
        }
    }

    public sealed class BattleInspectionData
    {
        public string role, hint;
        public int identityGlyph;
        public readonly List<BattleInspectionFact> facts = new();
        public readonly List<BattleInspectionResource> resources = new();
        public readonly List<BattleInspectionSection> sections = new();
    }
    public sealed class BattleInspectionFact
    {
        public string id, label, value;
        public int glyph;
        public BattleInspectionFact(string id, string label, string value, int glyph)
        { this.id = id; this.label = label; this.value = value; this.glyph = glyph; }
    }
    public sealed class BattleInspectionResource
    {
        public string id, label, mana, crystal;
        public int element;
    }
    public sealed class BattleInspectionSection
    {
        public string id, title, text;
        public int glyph;
        public BattleInspectionSection(string id, string title, string text, int glyph)
        { this.id = id; this.title = title; this.text = text; this.glyph = glyph; }
    }
}
