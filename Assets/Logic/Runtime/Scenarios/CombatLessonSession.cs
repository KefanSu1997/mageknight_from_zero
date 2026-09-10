using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Scenarios
{
    public sealed class CombatLessonSession : RuleScenarioSession
    {
        public string Encounter { get; private set; }
        public Monster Enemy { get; private set; }
        public int Block { get; private set; }
        public int Attack { get; private set; }
        public Element BlockElement { get; private set; }
        public Element AttackElement { get; private set; }
        public bool Resolved { get; private set; }
        public bool EnemyAlive { get; private set; }
        public int RequiredBlock => AbilityRules.RequiredBlock(Enemy);
        public int EffectiveBlock => (int)Math.Floor(Block * Constants.BlockEfficiency(Enemy.AttackElement, BlockElement));
        public int EffectiveAttack => (int)Math.Floor(Attack * Constants.Efficiency(AttackElement,
            Encounter == "fire" && AttackElement == Element.Fire ? Ability.FireResist : (Ability?)null));

        public CombatLessonSession() => Reset("normal");

        private void Reset(string encounter)
        {
            Encounter = encounter;
            Player = new PlayerState { Armor = 2 };
            Enemy = new Monster("lesson_" + encounter, 4, 4,
                encounter == "fire" ? Element.Fire : Element.Physical, 3,
                encounter == "swift" ? new[] { Ability.Swift } :
                encounter == "brutal" ? new[] { Ability.Brutal } :
                encounter == "fire" ? new[] { Ability.FireResist } : Array.Empty<Ability>());
            Block = 0;
            Attack = 4;
            BlockElement = AttackElement = Element.Physical;
            Resolved = false;
            EnemyAlive = true;
            Journal.Clear();
            Message = "分配格挡与攻击，再结算本次交战。";
            Rule = "格挡必须达到需求才免伤；不足时承受完整攻击。攻击达到敌方护甲即击败。";
        }

        protected override bool Execute(string action)
        {
            if (action.StartsWith("case:")) { Reset(action.Substring(5)); return true; }
            if (action == "reset") { Reset(Encounter); return true; }
            if (Resolved) return Outcome(false, "本次交战已结束，请重新布阵。", "同一敌人不能重复结算或重复领取名望。");
            switch (action)
            {
                case "block+": Block = Math.Min(8, Block + 1); break;
                case "block-": Block = Math.Max(0, Block - 1); break;
                case "attack+": Attack = Math.Min(8, Attack + 1); break;
                case "attack-": Attack = Math.Max(0, Attack - 1); break;
                case "block:physical": BlockElement = Element.Physical; break;
                case "block:ice": BlockElement = Element.Ice; break;
                case "attack:physical": AttackElement = Element.Physical; break;
                case "attack:fire": AttackElement = Element.Fire; break;
                case "resolve":
                    int woundsBefore = Player.Wounds;
                    var result = BattleResolver.Resolve(Player, new[] { Enemy },
                        new[] { new BlockAllocation(0, Block, BlockElement) },
                        new[] { new AttackAllocation(new[] { 0 }, Attack, AttackElement) });
                    EnemyAlive = !result.AllKilled;
                    Resolved = true;
                    int damage = EffectiveBlock >= RequiredBlock ? 0 : Enemy.Attack * (Encounter == "brutal" ? 2 : 1);
                    return Outcome(true,
                        $"有效格挡 {EffectiveBlock}/{RequiredBlock}；伤口 {woundsBefore} → {Player.Wounds}；名望 +{result.FameGain}。",
                        $"格挡 {EffectiveBlock} {(damage == 0 ? "≥" : "<")} {RequiredBlock}，承受伤害 {damage}；{damage}÷护甲2向上取整={Player.Wounds - woundsBefore}伤口。有效攻击 {EffectiveAttack} {(EnemyAlive ? "<" : "≥")} 护甲4，名望+{result.FameGain}。" );
                default: return Outcome(false, "未知行动。", "只接受本场景提供的行动。");
            }
            return Outcome(true, $"格挡 {Block}（有效 {EffectiveBlock}）；攻击 {Attack}（有效 {EffectiveAttack}）。",
                Encounter == "fire" ? "物理格挡火焰仅半效，冰格挡全效；火焰攻击遇火抗减半。" :
                Encounter == "swift" ? "迅捷使格挡需求翻倍：4 × 2 = 8。" :
                Encounter == "brutal" ? "残暴仅使未格挡伤害翻倍：4 × 2 = 8；格挡需求仍为4。" :
                "训练可分配格挡0～8、攻击0～8。它们是预置练习资源，并非正式卡牌能力。");
        }

        public override Dictionary<string, string> ReadState()
        {
            var s = base.ReadState();
            s["case"] = Encounter; s["block"] = Value(Block); s["attack"] = Value(Attack);
            s["effectiveBlock"] = Value(EffectiveBlock); s["requiredBlock"] = Value(RequiredBlock);
            s["effectiveAttack"] = Value(EffectiveAttack); s["enemyArmor"] = "4"; s["enemyAttack"] = "4";
            s["heroArmor"] = "2"; s["enemyAlive"] = EnemyAlive ? "1" : "0";
            s["resolved"] = Resolved ? "1" : "0";
            s["blockElement"] = BlockElement.ToString(); s["attackElement"] = AttackElement.ToString();
            return s;
        }
    }
}
