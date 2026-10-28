using System;
using MageKnight.Adventure.Content;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using UnityEngine;

namespace MageKnight.Battle
{
    [CreateAssetMenu(menuName = "MageKnight/Battle/Loadout")]
    public sealed class BattleLoadout : ScriptableObject
    {
        public string displayName;
        [Tooltip("确定性出战手牌快照，不是英雄标准初始牌库。使用已有原卡绑定。")]
        public ActionCardDefinition[] openingHand;
        [Tooltip("可重放的真实原卡牌库顶序列；不会混洗或复制到手牌。")]
        public ActionCardDefinition[] drawPile = Array.Empty<ActionCardDefinition>();
        public int redCrystals = 1, blueCrystals = 1, whiteCrystals, greenCrystals;
        public int blackTokens;
        public Sprite heroArtwork;
        [Tooltip("已招募的真实部队快照，须符合当前指挥槽。")]
        public BattleUnitDefinition[] units = Array.Empty<BattleUnitDefinition>();
        public BattleArtifactDefinition[] artifacts = Array.Empty<BattleArtifactDefinition>();
        public BattleHeroDefinition hero;
        public int startingFame;
        [Tooltip("已升级取得的技能索引；演练快照不免费授予整池技能。")]
        public int[] learnedSkills = Array.Empty<int>();
        [Tooltip("仅为可重放演练固定效果、供应与技能洗混种子；关闭时使用正常随机。")]
        public bool replayEffectRandom;
        public int effectRandomSeed;

        public PlayerState CreatePlayer(BattleUnitDefinition[] selectedUnits = null, int playerId = 0)
        {
            if (openingHand == null || openingHand.Length + (artifacts?.Length ?? 0) == 0 || openingHand.Length + (artifacts?.Length ?? 0) > 7)
                throw new InvalidOperationException("战斗手牌配置须包含1至7张原卡");
            var player = new PlayerState(playerId, hero == null ? "魔法骑士" : hero.displayName) { Fame = startingFame, HeroId = hero?.heroId ?? "" };
            if (startingFame < 0 || learnedSkills.Length > player.Level / 2 || learnedSkills.Length > 0 && hero == null)
                throw new InvalidOperationException("英雄已学技能数必须符合当前等级");
            var indices = new System.Collections.Generic.HashSet<int>();
            foreach (int index in learnedSkills)
            {
                if (!indices.Add(index)) throw new InvalidOperationException("不能重复配置同一技能");
                HeroSkillAcquisition.Acquire(player,hero.LearnedSkill(index),replayEffectRandom?new System.Random(effectRandomSeed):null);
            }
            foreach (var unit in selectedUnits ?? units ?? Array.Empty<BattleUnitDefinition>())
                player.Units.Add(new UnitState(unit.Snapshot()));
            if (player.FreeSlots < 0) throw new InvalidOperationException("出战部队超过英雄的实际指挥槽");
            foreach (var binding in openingHand)
            {
                binding.Snapshot(); // Validate original source and artwork before constructing the encounter.
                player.Deck.Hand.Add(new DeedCard(binding.id, CardType.Action));
            }
            // PutOnTop is a stack operation; preserve the configured first-card-on-top order.
            for (int i = drawPile.Length - 1; i >= 0; i--)
            {
                drawPile[i].Snapshot();
                player.Deck.PutOnTop(new DeedCard(drawPile[i].id, CardType.Action));
            }
            foreach (var artifact in artifacts ?? Array.Empty<BattleArtifactDefinition>())
                player.Deck.Hand.Add(new DeedCard(artifact.Snapshot().Id, CardType.Artifact));
            Add(player, ManaColor.Red, redCrystals); Add(player, ManaColor.Blue, blueCrystals); Add(player, ManaColor.White, whiteCrystals);
            Add(player, ManaColor.Green, greenCrystals);
            if (blackTokens < 0) throw new InvalidOperationException("初始魔力标记数量不可为负");
            player.Mana.AddToken(ManaColor.Black, blackTokens);
            return player;
        }
        private static void Add(PlayerState p, ManaColor color, int count)
        {
            if (count < 0 || count > 3) throw new InvalidOperationException("每色初始晶体必须在0至3之间");
            if (count > 0) p.Mana.AddCrystal(color, count);
        }
    }
}
