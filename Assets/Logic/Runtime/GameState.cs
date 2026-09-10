// ─── Runtime/GameState.cs ───────────────────────
namespace MK.Logic.Runtime
{
    using MK.Logic.Core;
    using MK.Logic.Runtime.Map;
    using MK.Logic.Data;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>每位玩家在逻辑层的快照</summary>
    public sealed class PlayerState
    {
        public PlayerState(int id, string name)
        {
            Id = id;
            Name = name;
            Reputation = 0;
            Fame = 0;
            Position = new AxialCoord(0, 0);
            Armor = 2;
            Wounds = 0;
            DiscardWounds = 0;
        }

        public PlayerState()
        {
            Name = string.Empty;
            Armor = 2;
            Position = new AxialCoord(0, 0);
        }

        public int Id { get; init; }
        public int Fame { get; set; }
        public int Reputation { get; set; }
        public string Name { get; set; }
        public int Influence => Reputation; // For test compatibility
        public int Level
        {
            get
            {
                int level = 1;
                foreach (int threshold in LevelThresholds)
                    if (Fame >= threshold) level++;
                return level;
            }
        }
        private static readonly int[] LevelThresholds = { 3, 8, 15, 24, 35, 48, 63, 80, 99 };

        /// <summary>
        /// 當前戰術牌提供的額外手牌上限加成。
        /// 在每輪選擇戰術後由流程服務設置，回合開始時計算手牌數時會用到。
        /// </summary>
        public int TacticHandBonus { get; set; } = 0;

        /// <summary>玩家的行動牌牌庫與手牌。</summary>
        public PlayerDeck Deck { get; } = new();

        /// <summary>玩家的魔力資源池。</summary>
        public ManaPool Mana { get; } = new();

        /// <summary>
        /// 本回合玩家持有的公共法力骰。當前只能持有一顆，
        /// 由 <see cref="ManaSource.Take"/> 在取得時賦值，
        /// <see cref="PlayerTurnEngine.EndTurn"/> 會自動返還並清空。
        /// </summary>
        public ManaDie? HeldManaDie { get; set; }

        /// <summary>玩家在地圖上的當前座標。</summary>
        public AxialCoord Position { get; set; } = new AxialCoord(0, 0);

        /// <summary>
        /// 若玩家位於未被控制的要塞或城堡上，手牌上限將額外 -1。
        /// 由移動或場景系統在進入該地形時設置。
        /// </summary>
        public bool KeepOrCastlePenalty { get; set; } = false;

        /// <summary>
        /// 來自技能或已佔領城市的額外手牌上限加成。
        /// 相關流程在獲得技能或控制城市時更新此值。
        /// </summary>
        public int ExtraHandBonus { get; set; } = 0;

        /// <summary>玩家擁有的技能列表。</summary>
        public List<SkillCard> Skills { get; } = new();

        /// <summary>
        /// 玩家已招募的全部部队。进入新回合时仍然保留，以供战斗和其他逻辑使用。
        /// </summary>
        public List<UnitState> Units { get; } = new();

        /// <summary>
        /// 指挥槽数量：初始1个，升到奇数等级时增加；声望不增加指挥槽。
        /// 额外的临时加成通过 TempCommandSlots 控制，
        /// 部分技能可能永久增加指挥槽，存放於 ExtraCommandSlots。
        /// </summary>
        public int CommandSlots
        {
            get
            {
                int slots = 1 + (Level - 1) / 2;
                return slots + TempCommandSlots + ExtraCommandSlots;
            }
        }

        /// <summary>本回合临时获得的额外指挥槽数量。</summary>
        public int TempCommandSlots { get; set; } = 0;

        /// <summary>永久增加的指挥槽数量，來源多為英雄技能。</summary>
        public int ExtraCommandSlots { get; set; } = 0;


        /// <summary>剩余空闲指挥槽数量。</summary>
        public int FreeSlots => CommandSlots - Units.Count;

        /// <summary>
        /// 解雇指定部队，释放指挥槽。
        /// </summary>
        public void Disband(UnitState unit) => Units.Remove(unit);

        public int Armor { get; init; } = 2;
        /// <summary>Hand wounds are the actual cards, including cards drawn or discarded this turn.</summary>
        public int Wounds
        {
            get => Deck.Hand.Count(card => card.Type == CardType.Wound);
            set => Deck.SetWoundCount(value, false);
        }

        /// <summary>Actual wound cards in the discard pile, including poison wounds.</summary>
        public int DiscardWounds
        {
            get => Deck.DiscardPile.Count(card => card.Type == CardType.Wound);
            set => Deck.SetWoundCount(value, true);
        }

        /// <summary>每回合開始自動獲得的法力標記。</summary>
        public Dictionary<ManaColor, int> TokensPerTurn { get; } = new();

        /// <summary>受到魔力詛咒的顏色。</summary>
        public ManaColor? ManaCurseColor { get; set; }

        /// <summary>本回合是否已觸發魔力詛咒。</summary>
        public bool ManaCurseTriggered { get; set; }

        /// <summary>將手中所有非傷牌棄入棄牌堆，用於 Paralyze 能力。</summary>
        public void DiscardNonWoundHand()
        {
            var tmp = Deck.Hand.Where(c => c.Type != CardType.Wound).ToList();
            foreach (var c in tmp)
                Deck.Discard(c);
        }

        /// <summary>增加進入棄牌堆的傷牌數量。</summary>
        /// <summary>
        /// 增加指定數量的創傷牌到棄牌堆，同時統計毒素造成的傷口數。
        /// </summary>
        /// <param name="count">新增的創傷數量</param>
        public void AddWoundsToDiscard(int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
                Deck.GainToDiscard(new DeedCard("w", CardType.Wound));
        }

        /// <summary>
        /// 將創傷牌加入手牌或棄牌堆。
        /// </summary>
        /// <param name="count">創傷數量</param>
        /// <param name="toDiscard">若為 true 則置入棄牌堆</param>
        public void AddWoundCards(int count, bool toDiscard = false)
        {
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var w = new DeedCard("w", CardType.Wound);
                if (toDiscard)
                    Deck.GainToDiscard(w);
                else
                    Deck.Hand.Add(w);
            }
        }

        /// <summary>
        /// 本回合 Block 阶段玩家最后一次打出的“格挡元素”。
        /// 缺省为 Physical，UI 在 Block 阶段调用 TurnMachine.SetBlock 时会写入。
        /// </summary>
        public Element LastBlockElement { get; set; } = Element.Physical;

        /// <summary>傀儡大師保留的敵人標記。</summary>
        public Data.Monster? StoredEnemy { get; set; }

        /// <summary>混亂大師當前指向的顏色。</summary>
        public ManaColor? ChaosColor { get; set; }

        /// <summary>本回合是否忽略聲望變化。</summary>
        public bool IgnoreReputationChange { get; set; }
    }

    /// <summary>整局游戏的状态容器</summary>
    public sealed class GameState
    {
        public Phase CurrentPhase { get; private set; } = Phase.None;
        public int Round { get; private set; } = 1;

        // 不使用 List<PlayerState>? —— 开启 Nullable Reference Types 可显式禁空
        public IList<PlayerState> Players { get; } = new List<PlayerState>();
        public int ActivePlayerIndex { get; private set; } = 0;
        public PlayerState ActivePlayer => Players[ActivePlayerIndex];

        /// <summary>将状态推进到下一个子阶段</summary>
        internal void NextPhase() =>
            CurrentPhase = CurrentPhase switch
            {
                Phase.None          => Phase.Ranged,
                Phase.Ranged        => Phase.Block,
                Phase.Block         => Phase.AssignDamage,
                Phase.AssignDamage  => Phase.Melee,
                Phase.Melee         => Phase.End,
                Phase.End           => Phase.None,
                _                   => Phase.None
            };

        public void NextPlayer()
        {
            ActivePlayerIndex = (ActivePlayerIndex + 1) % Players.Count;
            CurrentPhase = Phase.None;
        }
    }
}
