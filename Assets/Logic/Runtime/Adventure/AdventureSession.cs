using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Adventure
{
    /// <summary>独立运行状态与命令入口。UI不负责扣牌、扣资源、判断目标或推进规则阶段。</summary>
    public sealed class AdventureSession
    {
        private readonly List<CardInstance> _deck = new();
        private readonly HashSet<string> _hired = new();
        private readonly MapState _map = new();
        private int _nextSerial;
        public AdventureSpec Definition { get; }
        public PlayerState Player { get; }
        public List<CardInstance> Hand { get; } = new();
        public List<CardInstance> Played { get; } = new();
        public List<CardInstance> Discard { get; } = new();
        public AdventurePhase Phase { get; private set; }
        public EncounterRound Battle { get; private set; }
        public int Turn { get; private set; } = 1;
        public int Movement { get; private set; }
        public int Influence { get; private set; }
        public int SelectedCard { get; private set; } = -1;
        public string SelectedUnit { get; private set; } = "";
        public string Target { get; private set; } = "";
        public bool Enhanced { get; private set; }
        public bool Sideways { get; private set; }
        public bool Accepted { get; private set; } = true;
        public int Revision { get; private set; }
        public string Message { get; private set; } = "选择手牌，再选择目标。";
        public string Rule { get; private set; } = "卡牌和部队提供行动；确认后才消耗资源。";
        public List<string> Journal { get; } = new();
        public CardInstance Selected => Hand.FirstOrDefault(c => c.Serial == SelectedCard);
        public SiteSpec CurrentSite => Definition.Sites.Single(s => s.Position.Equals(Player.Position));
        public SiteSpec TargetSite => Definition.Sites.FirstOrDefault(s => "site:" + s.Id == Target);
        public ActorSpec TargetOffer => Definition.Offers.FirstOrDefault(a => "offer:" + a.Id == Target);
        public int EnemyIndex => Target.StartsWith("enemy:") && int.TryParse(Target.Substring(6), out int i) ? i : -1;
        public bool Completed => Phase == AdventurePhase.Result && Battle != null && Battle.Defeated.All(x => x);
        public int DeckCount => _deck.Count;
        public int CardTotal => Hand.Count + Played.Count + Discard.Count + DeckCount;

        public AdventureSession(AdventureSpec definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Validate(definition);
            Player = new PlayerState { Armor = definition.Hero.Armor, Name = definition.Hero.Name,
                Fame = definition.StartingFame, Reputation = definition.Reputation };
            Player.Mana.AddCrystal(ManaColor.Red, definition.RedCrystals);
            Player.Mana.AddCrystal(ManaColor.Blue, definition.BlueCrystals);
            Player.Mana.AddCrystal(ManaColor.Green, definition.GreenCrystals);
            Player.Mana.AddCrystal(ManaColor.White, definition.WhiteCrystals);
            Player.Position = definition.Sites.Single(s => s.Id == definition.StartSite).Position;
            Movement = definition.StartingMovement;
            Influence = definition.StartingInfluence + (definition.Mode == AdventureMode.Recruitment ? -ReputationTable.CalcRecruitModifier(definition.Reputation) : 0);
            foreach (var site in definition.Sites.Where(s => s.Revealed)) _map.Placed[site.Position] = Tile(site.Terrain);
            for (int i = 0; i < definition.Deck.Length; i++) _deck.Add(new CardInstance(i, definition.Deck[i]));
            _nextSerial = definition.Deck.Length;
            DrawToFive();
            Phase = definition.Mode == AdventureMode.Recruitment ? AdventurePhase.Interaction : AdventurePhase.Travel;
            if (definition.Mode == AdventureMode.Combat) BeginBattle();
        }

        private static void Validate(AdventureSpec definition)
        {
            if (definition.Hero == null || definition.Hero.Armor < 1 || definition.Deck.Length < 5)
                throw new ArgumentException("需要有效英雄和至少五张牌。");
            if (definition.Sites.Select(s => s.Id).Distinct().Count() != definition.Sites.Length ||
                definition.Sites.Select(s => s.Position).Distinct().Count() != definition.Sites.Length)
                throw new ArgumentException("地点ID或坐标重复。");
            if (!definition.Sites.Any(s => s.Id == definition.StartSite && s.Revealed))
                throw new ArgumentException("起始地点必须存在且已揭示。");
            if (definition.Deck.Any(c => !OfficialActionAdapter.Supports(c)))
                throw new ArgumentException("教学牌组包含尚未接入交互的原卡，不能以自制数值替代。");
        }

        private static MapTile Tile(TerrainType terrain) => new(TileSet.Countryside, 0, Enumerable.Repeat(terrain, 6).ToArray());
        public bool IsRevealed(SiteSpec site) => _map.Placed.ContainsKey(site.Position);
        public int Cost(SiteSpec site) => IsRevealed(site) ? TerrainCost.GetCost(site.Terrain, Definition.Time) : 2;
        public bool HasHired(string id) => _hired.Contains(id);
        public bool CardAvailable(CardSpec card) => card.Source != null && Phase != AdventurePhase.Result;
        public int Preview(CardSpec card, bool enhanced = false, bool sideways = false)
            => OfficialActionAdapter.Preview(card, Phase, enhanced, sideways);

        public bool Execute(string command)
        {
            Revision++;
            Accepted = Handle(command);
            Journal.Add((Accepted ? "• " : "× ") + Message);
            if (Journal.Count > 8) Journal.RemoveAt(0);
            return Accepted;
        }

        private bool Handle(string command)
        {
            if (command == "cancel") { SelectedCard = -1; SelectedUnit = ""; Target = ""; Enhanced = Sideways = false; return Say(true, "已取消选择，资源保持不变。"); }
            if (command.StartsWith("unit:"))
            {
                var unit = Player.Units.FirstOrDefault(u => "unit:" + u.Card.Id == command);
                if (unit == null || !unit.CanActivate || Phase != AdventurePhase.Block)
                    return Say(false, "这名部队当前不能使用。");
                SelectedUnit = unit.Card.Id; SelectedCard = -1;
                return Say(true, "已选择部队，点选要格挡的敌人。", "选择不消耗单位；确认后单位变为已用。");
            }
            if (command.StartsWith("card:"))
            {
                if (!int.TryParse(command.Substring(5), out int serial)) return Say(false, "卡牌不存在。");
                var card = Hand.FirstOrDefault(c => c.Serial == serial);
                if (card == null || !CardAvailable(card.Definition)) return Say(false, "这张牌不适用于当前阶段。");
                SelectedCard = serial; SelectedUnit = ""; Enhanced = false;
                Sideways = Preview(card.Definition) == 0;
                return Say(true, "已选择「" + card.Definition.Name + "」，选择目标后确认。", "选择本身不消耗卡牌或魔力。");
            }
            if (command.StartsWith("target:"))
            {
                string target = command.Substring(7);
                if (!ValidTarget(target)) return Say(false, "当前阶段不能选择这个目标。");
                Target = target;
                return Say(true, "目标已选择。", "目标与行动条件会在确认时重新校验。");
            }
            if (command == "enhance")
            {
                if (Selected == null || Preview(Selected.Definition, true) == 0)
                    return Say(false, "这张牌的强化效果不适用于当前阶段，资源未消耗。");
                Enhanced = !Enhanced; Sideways = false;
                return Say(true, Enhanced ? "已选择强化效果；确认时支付魔力。" : "已选择基础效果。");
            }
            if (command == "sideways")
            {
                if (Selected == null) return Say(false, "先选择一张非伤牌。");
                if (Sideways && Preview(Selected.Definition) == 0)
                    return Say(false, "牌面效果不适用于本阶段，可以横置使用。");
                Sideways = !Sideways; Enhanced = false;
                return Say(true, Sideways ? "横置使用：当前行动 +1，不消耗魔力。" : "已改回牌面基础效果。");
            }
            if (command == "confirm") return Confirm();
            return Say(false, "未知行动。");
        }

        private bool ValidTarget(string target)
        {
            if (Phase == AdventurePhase.Travel) return Definition.Sites.Any(s => target == "site:" + s.Id);
            if (Phase == AdventurePhase.Interaction)
                return Definition.Offers.Any(o => target == "offer:" + o.Id) || target == "site:" + CurrentSite.Id;
            if (Phase == AdventurePhase.Block || Phase == AdventurePhase.Attack)
                return Enumerable.Range(0, Battle.Definition.Enemies.Length).Any(i => target == "enemy:" + i);
            return false;
        }

        private bool Confirm()
        {
            if (Selected != null) return PlaySelected();
            if (SelectedUnit.Length > 0) return UseUnit();
            if (Phase == AdventurePhase.Result) return Say(false, "这次遭遇已结束。", "奖励只结算一次。");
            if (Phase == AdventurePhase.Travel) return Visit();
            if (Phase == AdventurePhase.Interaction)
            {
                if (TargetOffer != null) return Recruit(TargetOffer);
                if (Definition.Mode == AdventureMode.Journey && Player.Units.Count == 0)
                    return Say(false, "先招募一名盟友，再出发迎战。", "本次任务的目标是招募后协同作战。");
                EndTurn();
                if (Definition.Mode == AdventureMode.Journey) BeginBattle();
                return Say(true, $"回合结束：弃牌{Discard.Count}，补至手牌{Hand.Count}，牌库{DeckCount}。",
                    "已打出牌进入弃牌堆；补牌到5，临时移动力/影响力/魔力清零，水晶与部队保留。");
            }
            Target = "";
            if (Phase == AdventurePhase.Block)
            {
                // 核心结算器写入伤口；把新增伤牌同步到本场冒险的实际手牌/弃牌实例。
                bool paralyzed = Battle.Definition.Enemies.Where((e, i) => Battle.EffectiveBlock(i) < Battle.RequiredBlock(i))
                    .Any(e => e.Attack > 0 && e.Abilities.Contains(Ability.Paralyze));
                int beforeWounds = Player.Wounds, beforeDiscard = Player.DiscardWounds;
                int wounds = Battle.ResolveDefense(Player);
                if (paralyzed)
                {
                    var lost = Hand.Where(c => c.Definition.Source != null).ToArray();
                    foreach (var c in lost) { Hand.Remove(c); Discard.Add(c); }
                }
                AddWounds(Hand, Player.Wounds - beforeWounds);
                AddWounds(Discard, Player.DiscardWounds - beforeDiscard);
                Phase = AdventurePhase.Attack;
                return Say(true, $"敌人攻击已结算：伤口 +{wounds}。现在出攻击牌。",
                    "只有达到格挡需求才免伤；不足时承受完整攻击，伤害÷英雄护甲向上取整。");
            }
            int fame = Battle.ResolveAttack(Player); Phase = AdventurePhase.Result;
            return Say(true, Completed ? $"胜利，名望 +{fame}。" : "交战结束，仍有敌人存活。",
                "攻击资源按目标和元素合并，应用抗性后与护甲比较；每个敌人的名望只领取一次。");
        }

        private bool PlaySelected()
        {
            var instance = Selected; var card = instance.Definition;
            if (!CardAvailable(card)) return Say(false, "卡牌与当前阶段不符。");
            var action = OfficialActionAdapter.Action(Phase);
            bool targetValid = action == CardAction.Movement ? TargetSite != null :
                action == CardAction.Influence ? TargetOffer != null || Target == "site:" + CurrentSite.Id :
                EnemyIndex >= 0 && EnemyIndex < Battle.Definition.Enemies.Length;
            if (!targetValid) return Say(false, "请先选择行动目标。", "先选卡牌，再选地点、部队或敌人；未确认不消耗资源。");
            int value = Preview(card, Enhanced, Sideways);
            if (value <= 0) return Say(false, "牌面效果不适用于当前阶段，未打出卡牌。");
            if (Enhanced)
            {
                // 先完整校验费用，防止旧 ActionSystem 在魔力不足时降级打出基础效果。
                if (card.Colors.GroupBy(c => c).Any(g =>
                    Player.Mana.Crystals.GetValueOrDefault(g.Key) + Player.Mana.Tokens.GetValueOrDefault(g.Key) < g.Count()))
                    return Say(false, "强化所需魔力不足，手牌与资源保持不变。");
            }
            string mode = Sideways ? "横置" : Enhanced ? "强化" : "基础";
            string text = Sideways ? "任意非伤牌可以横置提供移动、影响、格挡或普通攻击1。" : Enhanced ? card.EnhancedText : card.BaseText;
            value = OfficialActionAdapter.Apply(card, Player, Phase, Enhanced, Sideways);
            if (action == CardAction.Movement) Movement += value;
            else if (action == CardAction.Influence) Influence += value;
            else Battle.Allocate(EnemyIndex, action, value, Element.Physical);
            Hand.Remove(instance); Played.Add(instance); SelectedCard = -1; Enhanced = Sideways = false;
            return Say(true, $"「{card.Name}」{mode}：{ActionName(action)} +{value}，手牌 {Hand.Count + 1} → {Hand.Count}。", text);
        }

        private bool Visit()
        {
            var site = TargetSite;
            if (site == null) return Say(false, "选择地图上的目的地。");
            int dq = site.Position.Q - Player.Position.Q, dr = site.Position.R - Player.Position.R;
            if ((Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2 != 1)
                return Say(false, "只能移动到或探索相邻位置，资源未扣除。");
            int cost = Cost(site);
            if (cost == int.MaxValue) return Say(false, "这种地形不可通行，资源未扣除。");
            if (Movement < cost) return Say(false, $"需要{cost}移动力，当前只有{Movement}。", "先选择移动牌并确认打出，再支付目标费用。");
            int before = Movement;
            if (!IsRevealed(site))
            {
                // 已配置的隐藏位置保持确定性；探索只揭示，不移动玩家。
                _map.Placed[site.Position] = Tile(site.Terrain); Movement -= cost;
                return Say(true, $"揭示{site.Name}：移动力 {before} → {Movement}。", "探索费用2；进入揭示的地形还需另外支付地形费用。");
            }
            if (!new MovementService().TryMove(Player, site.Position, _map, Movement, Definition.Time))
                return Say(false, "不能进入所选地点，资源未扣除。");
            Movement -= cost; Target = "";
            if (Definition.Mode == AdventureMode.Journey && site.RecruitAt != RecruitLocation.None)
            { Phase = AdventurePhase.Interaction; Influence -= ReputationTable.CalcRecruitModifier(Definition.Reputation); }
            return Say(true, $"进入{site.Name}：移动力 {before} → {Movement}。", "按昼夜与底层地形支付费用；进入交涉阶段后不再移动。");
        }

        private bool Recruit(ActorSpec offer)
        {
            if (_hired.Contains(offer.Id)) return Say(false, "这名单位已经加入队伍。");
            int before = Influence;
            if (!new RecruitmentService().TryRecruit(Player, offer.Unit(), CurrentSite.RecruitAt, Influence, applyReputationModifier: false))
                return Say(false, "招募条件未满足：检查地点、影响力和空闲指挥槽。", "招募失败不扣费；声望在交涉开始时只应用一次。");
            Influence -= offer.Cost; _hired.Add(offer.Id); Target = "";
            return Say(true, $"{offer.Name}加入：影响力 {before} → {Influence}。", "支付牌面费用，占用一个指挥槽；单位就绪加入。");
        }

        private bool UseUnit()
        {
            var unit = Player.Units.FirstOrDefault(u => u.Card.Id == SelectedUnit);
            if (unit == null || !unit.CanActivate) return Say(false, "这名部队已用过，不能再次发动。");
            if (Phase != AdventurePhase.Block || unit.Card.BlockValue <= 0) return Say(false, "此单位当前只能用于格挡阶段。");
            if (EnemyIndex < 0 || EnemyIndex >= Battle.Definition.Enemies.Length) return Say(false, "先点选要格挡的敌人。");
            unit.Exhaust(); Battle.Allocate(EnemyIndex, CardAction.Block, unit.Card.BlockValue, Element.Physical); SelectedUnit = "";
            return Say(true, $"{unit.Card.NameCn}提供格挡{unit.Card.BlockValue}，状态变为已用。", "部队能力来自单位定义；已用状态保留到整轮结束。");
        }

        private void EndTurn()
        {
            Discard.AddRange(Played); Played.Clear(); DrawToFive(); Turn++;
            Movement = Influence = 0; Player.Mana.ResetTokens(); SelectedCard = -1; Target = "";
        }
        private void AddWounds(List<CardInstance> destination, int count)
        {
            for (int i = 0; i < count; i++) destination.Add(new CardInstance(_nextSerial++, new CardSpec(null)));
        }
        private void DrawToFive() { while (Hand.Count < 5 && _deck.Count > 0) { Hand.Add(_deck[0]); _deck.RemoveAt(0); } }
        private void BeginBattle() { Battle = new EncounterRound(Definition.Encounter); Phase = AdventurePhase.Block; Target = ""; }
        private bool Say(bool accepted, string message, string rule = null)
        { Message = message; if (rule != null) Rule = rule; return accepted; }
        public static string ActionName(CardAction action) => action switch
        { CardAction.Movement => "移动", CardAction.Influence => "影响", CardAction.Block => "格挡", CardAction.Wound => "伤牌 · 无法打出", _ => "攻击" };

        public Dictionary<string, string> ReadState()
        {
            var s = new Dictionary<string, string>
            {
                ["scenario"] = Definition.Id, ["phase"] = Phase.ToString(), ["target"] = Target,
                ["selectedCard"] = Selected?.Definition.Id ?? "", ["selectedUnit"] = SelectedUnit, ["enhanced"] = Enhanced ? "1" : "0",
                ["sideways"] = Sideways ? "1" : "0",
                ["selectedValue"] = Selected == null ? "0" : Preview(Selected.Definition, Enhanced, Sideways).ToString(),
                ["accepted"] = Accepted ? "1" : "0", ["revision"] = Revision.ToString(),
                ["hand"] = Hand.Count.ToString(), ["deck"] = DeckCount.ToString(), ["played"] = Played.Count.ToString(),
                ["discard"] = Discard.Count.ToString(), ["cardTotal"] = CardTotal.ToString(),
                ["actionCardTotal"] = Hand.Concat(Played).Concat(Discard).Concat(_deck).Count(c => c.Definition.Source != null).ToString(),
                ["woundCards"] = Hand.Count(c => c.Definition.Source == null).ToString(),
                ["movement"] = Movement.ToString(), ["influence"] = Influence.ToString(),
                ["wounds"] = Player.Wounds.ToString(), ["fame"] = Player.Fame.ToString(), ["turn"] = Turn.ToString(),
                ["units"] = Player.Units.Count.ToString(), ["ready"] = Player.Units.Count(u => u.IsReady).ToString(),
                ["freeSlots"] = (Player.CommandSlots - Player.Units.Count).ToString(),
                ["redCrystal"] = Player.Mana.Crystals.GetValueOrDefault(ManaColor.Red).ToString(),
                ["blueCrystal"] = Player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue).ToString(),
                ["greenCrystal"] = Player.Mana.Crystals.GetValueOrDefault(ManaColor.Green).ToString(),
                ["whiteCrystal"] = Player.Mana.Crystals.GetValueOrDefault(ManaColor.White).ToString(),
                ["position"] = CurrentSite.Id, ["revealed"] = Definition.Sites.Count(IsRevealed).ToString(),
                ["completed"] = Completed ? "1" : "0", ["encounter"] = Battle?.Definition.Id ?? ""
            };
            for (int i = 0; i < Hand.Count; i++) s["cardSlot" + i] = Hand[i].Definition.Id;
            for (int i = 0; i < (Battle?.Definition.Enemies.Length ?? 0); i++)
            {
                s["enemy" + i] = Battle.Definition.Enemies[i].Id;
                s["block" + i] = Battle.EffectiveBlock(i).ToString();
                s["requiredBlock" + i] = Battle.RequiredBlock(i).ToString();
                s["attack" + i] = Battle.EffectiveAttack(i).ToString();
                s["defeated" + i] = Battle.Defeated[i] ? "1" : "0";
            }
            return s;
        }
    }
}
