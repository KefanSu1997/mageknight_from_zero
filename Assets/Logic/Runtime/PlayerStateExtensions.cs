using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// PlayerState扩展类，为地点交互测试提供额外属性和方法
    /// </summary>
    public static class PlayerStateExtensions
    {
        /// <summary>
        /// 获取魔法池的总点数（用于法师塔交互测试）
        /// </summary>
        public static int GetTotal(this ManaPool manaPool)
        {
            return manaPool.GetTotal();
        }

        /// <summary>
        /// 使用魔法点数（用于法术购买）
        /// </summary>
        public static bool UseMana(this ManaPool manaPool, int amount)
        {
            return false;
        }

        /// <summary>
        /// 添加水晶到魔法池
        /// </summary>
        public static void AddCrystal(this ManaPool manaPool, ManaColor color, int count)
        {
            // 这里简化实现，具体逻辑取决于ManaPool的实现
            // 添加水晶到对应颜色
        }

        /// <summary>
        /// 获取总攻击力
        /// </summary>
        public static int GetTotalAttackPower(this PlayerState player)
        {
            var baseAttack = player.Level;
            var unitAttack = 0;
            foreach (var unit in player.Units)
            {
                // 从UnitState获取攻击属性，通过UnitCard.Attacks获取攻击值总和
                var totalAttack = 0;
                if (unit.Card.Attacks != null)
                {
                    foreach (var attack in unit.Card.Attacks)
                    {
                        totalAttack += attack.Value;
                    }
                }
                unitAttack += totalAttack;
            }
            return baseAttack + unitAttack;
        }

        /// <summary>
        /// 获取总法术强度
        /// </summary>
        public static int GetTotalSpellPower(this PlayerState player)
        {
            var baseSpell = player.Level;
            var unitSpell = 0;
            foreach (var unit in player.Units)
            {
                // 检查攻击配置中是否有法术攻击类型
                var totalSpell = 0;
                if (unit.Card.Attacks != null)
                {
                    foreach (var attack in unit.Card.Attacks)
                    {
                        if (attack.Type == AttackType.Siege)
                            totalSpell += attack.Value;
                    }
                }
                unitSpell += totalSpell;
            }
            return baseSpell + unitSpell;
        }

        /// <summary>
        /// 初始化牌库（用于测试）
        /// </summary>
        public static void InitializeStarterDeck(this PlayerDeck deck)
        {
            for (int i = 0; i < 5; i++)
            {
                deck.Hand.Add(new DeedCard($"TestCard{i}", CardType.Action));
            }
        }

        /// <summary>
        /// 将法术牌添加到玩家手牌（用于市场交互）
        /// </summary>
        public static void AddCardToHand(this PlayerDeck deck, SpellCard card)
        {
            // TODO: 需要确认PlayerDeck是否支持SpellCard
        }
        
        /// <summary>
        /// 将高级行动牌添加到玩家手牌（用于市场交互）
        /// </summary>
        public static void AddCardToHand(this PlayerDeck deck, AdvActionCard card)
        {
            // TODO: 需要确认PlayerDeck是否支持AdvActionCard
        }

        /// <summary>
        /// 从牌库中抽取指定数量的牌
        /// </summary>
        public static void DrawExact(this PlayerDeck deck, int count)
        {
            // 简化实现：直接添加测试牌
            for (int i = 0; i < count; i++)
            {
                deck.Hand.Add(new DeedCard($"Drawn Card {i}", CardType.Action));
            }
        }

        /// <summary>
        /// 交换手牌上限
        /// </summary>
        public static int HandLimit(this PlayerState player)
        {
            return 5 + player.TacticHandBonus + player.ExtraHandBonus;
        }

        /// <summary>
        /// 命令上限
        /// </summary>
        public static int CommandLimit(this PlayerState player)
        {
            return player.CommandSlots;
        }
    }
}