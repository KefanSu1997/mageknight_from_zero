using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloVisibleCard(int Slot,string Id,string Type);
    public sealed record SoloVisibleUnitAbility(int Index,string Kind,UnitAbility Definition);
    public sealed record SoloVisibleUnit(int Slot,string Id,bool Ready,bool CanActivate,int Wounds,
        bool Destroyed,string Banner,string BannerEffect,string Attunement,bool NeedsUpkeep)
    {
        public bool CourageSpent { get; init; }
        public bool FortitudeSpent { get; init; }
        public bool ReturningBanner { get; init; }
        public SoloVisibleUnitAbility[] Actions { get; init; } = Array.Empty<SoloVisibleUnitAbility>();
    }
    public sealed record SoloVisibleSkill(int Slot,string Id,bool UsedThisTurn,bool UsedThisRound,bool Flipped);
    public sealed record SoloVisibleMana(string Color,int Tokens,int Crystals);
    public sealed record SoloVisibleSourceDie(int Slot,string Face,bool OwnedByPlayer,bool PendingReturnChoice);
    public sealed record SoloVisibleSource(SoloVisibleSourceDie[] Available,SoloVisibleSourceDie[] TurnHeld,
        SoloVisibleSourceDie[] RoundHeld,SoloVisibleSourceDie[] TacticHeld,bool UsedThisTurn,int ExtraDice,int ExtraBlackDice);
    public sealed record SoloPuppetAttack(int Power,string Element);
    public sealed record SoloVisiblePuppet(int Slot,string Id,int Armor,string[] Abilities,SoloPuppetAttack[] Attacks);
    public sealed record SoloPublicInventory(string Hero,int Fame,int Level,int Reputation,int Armor,
        int CommandSlots,int DrawCount,int TacticAsideCount,SoloVisibleCard[] Hand,SoloVisibleCard[] Discard,
        SoloVisibleCard[] Played,SoloVisibleCard[] RoundAside,SoloVisibleUnit[] Units,SoloVisibleSkill[] Skills,
        SoloVisibleMana[] Mana,int Movement,int Influence,int Healing,int GrailHealing)
    {
        public bool ForgoSourceDice { get; init; }
        public SoloVisibleSource Source { get; init; }
        public SoloVisiblePuppet[] PuppetCandidates { get; init; } = Array.Empty<SoloVisiblePuppet>();
        public SoloVisiblePuppet[] PuppetStored { get; init; } = Array.Empty<SoloVisiblePuppet>();
    }

    public sealed partial class PveJourneySession
    {
        /// <summary>Variable-length visible inventory branch, not the complete policy observation.
        /// Draw order and face-down tactic cards are deliberately not serialized.</summary>
        public SoloPublicInventory ObserveSoloInventory()
        {
            if(!IsSoloConquest)throw new InvalidOperationException("Solo conquest observation required.");
            var p=Player;var c=Context;
            var source=c?.ManaSource??Game.Globals.Mana;
            SoloVisibleSourceDie[] Held(System.Collections.Generic.IReadOnlyDictionary<ManaDie,PlayerState> held)
                =>held==null?Array.Empty<SoloVisibleSourceDie>():held.Select((pair,i)=>new SoloVisibleSourceDie(i,
                    pair.Key.Face.ToString(),ReferenceEquals(pair.Value,p),
                    source.PendingSourceOpeningReturns(p).Any(d=>ReferenceEquals(d,pair.Key)))).ToArray();
            SoloVisibleCard[] Cards(System.Collections.Generic.IEnumerable<DeedCard> cards)
                =>cards.Select((card,i)=>new SoloVisibleCard(i,card.Id,card.Type.ToString())).ToArray();
            SoloVisiblePuppet[] Puppets(System.Collections.Generic.IEnumerable<Monster> tokens)
                =>tokens.Select((token,i)=>new SoloVisiblePuppet(i,token.Id,token.Armor,
                    token.Abilities.Select(a=>a.ToString()).ToArray(),Enumerable.Range(0,token.AttackCount)
                        .Select(a=>new SoloPuppetAttack(token.GetAttack(a).Value,token.GetAttack(a).Element.ToString())).ToArray())).ToArray();
            return new SoloPublicInventory(p.HeroId,p.Fame,p.Level,p.Reputation,p.Armor,p.CommandSlots,
                p.Deck.DrawPileCount,p.Deck.TacticAsideCards.Count,Cards(p.Deck.Hand),Cards(p.Deck.DiscardPile),
                Cards(p.Deck.PlayedCards),Cards(p.Deck.RoundAsideCards),
                p.Units.Select((u,i)=>new SoloVisibleUnit(i,u.Card.Id,u.IsReady,u.CanActivate,u.Wounds,
                    u.IsDestroyed,u.Banner?.Id,u.BannerEffectId,u.AttunementColor?.ToString(),u.NeedsAttunementUpkeep)
                    {CourageSpent=u.CourageBannerSpent,FortitudeSpent=u.FortitudeBannerSpent,
                        ReturningBanner=p.BannersReturningAtRoundEnd.Contains(u),
                        Actions=(u.Card.Actions??Array.Empty<UnitAbility>()).Select((a,k)=>new SoloVisibleUnitAbility(k,a.Kind.ToString(),
                            a with {Cost=a.Cost?.ToArray(),MovementReductionTerrains=a.MovementReductionTerrains?.ToArray()})).ToArray()}).ToArray(),
                p.Skills.Select((s,i)=>new SoloVisibleSkill(i,s.SourceId,p.SkillsUsedThisTurn.Contains(s.SourceId),
                    p.SkillsUsedThisRound.Contains(s.SourceId),p.FlippedSkills.Contains(s.SourceId))).ToArray(),
                Enum.GetValues(typeof(ManaColor)).Cast<ManaColor>().Select(color=>new SoloVisibleMana(color.ToString(),
                    p.Mana.Tokens.TryGetValue(color,out var t)?t:0,p.Mana.Crystals.TryGetValue(color,out var k)?k:0)).ToArray(),
                c?.MovementPool??0,c?.InfluencePool??0,c?.Healing.Ordinary??0,c?.Healing.Grail??0)
                {ForgoSourceDice=p.ForgoSourceDiceThisTurn,
                    Source=source==null?null:new(source.Dice.Select((d,i)=>new SoloVisibleSourceDie(i,d.Face.ToString(),false,false)).ToArray(),
                        Held(source.TurnClaims),Held(source.RoundClaims),Held(source.TacticClaims),source.WasUsedThisTurn(p),
                        c?.ExtraManaDice??0,c?.ExtraBlackManaDice??0),
                    PuppetCandidates=Puppets(p.PuppetTokens.DefeatedThisTurn),
                    PuppetStored=Puppets(p.PuppetTokens.KeptTokens)};
        }
    }
}
