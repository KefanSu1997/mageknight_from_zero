using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Adventure
{
    public enum AdventureMode { Combat, Exploration, Recruitment, Journey }
    public enum AdventurePhase { Travel, Interaction, Block, Attack, Result }
    public enum CardAction { Movement, Influence, Block, Attack, Wound }

    // 素材在Unity定义资源中；规则层只接收不可变数值和稳定ID。
    public sealed record ActorSpec(string Id, string Name, int Armor, int Attack, int Block,
        int Fame, Element Element, Ability[] Abilities, int Cost = 0,
        RecruitLocation RecruitAt = RecruitLocation.Village)
    {
        public MK.Logic.Data.Monster Monster() => new(Id, Armor, Attack, Element, Fame, Abilities.ToArray());
        public UnitCard Unit() => new(Id, Name, Name, 1, Armor, Cost, RecruitAt,
            new[] { new AttackProfile(AttackType.Melee, Attack, Element) }, Abilities.ToArray(), BlockValue: Block);
    }

    /// <summary>原卡数据快照；不在冒险层重新定义名称、效果数值或耗色。</summary>
    public sealed record CardSpec(ActionCardData Source)
    {
        public string Id => Source?.Id ?? "wound";
        public string Name => Source?.Name ?? "伤口";
        public string BaseText => Source?.BaseEffect ?? "伤牌不能打出。";
        public string EnhancedText => Source?.EnhancedEffect ?? "";
        public ManaColor[] Colors => Source?.RequiredCrystals ?? Array.Empty<ManaColor>();
    }
    public sealed record CardInstance(int Serial, CardSpec Definition);
    public sealed record SiteSpec(string Id, string Name, AxialCoord Position, TerrainType Terrain,
        bool Revealed = true, RecruitLocation RecruitAt = RecruitLocation.None);
    public sealed record EncounterSpec(string Id, ActorSpec[] Enemies);
    public sealed record AdventureSpec(string Id, AdventureMode Mode, ActorSpec Hero,
        CardSpec[] Deck, SiteSpec[] Sites, string StartSite, ActorSpec[] Offers,
        EncounterSpec Encounter, DayPart Time = DayPart.Day, int StartingMovement = 0,
        int StartingInfluence = 0, int StartingFame = 0, int Reputation = 0, int RedCrystals = 1,
        int BlueCrystals = 1, int GreenCrystals = 1, int WhiteCrystals = 1);
}
