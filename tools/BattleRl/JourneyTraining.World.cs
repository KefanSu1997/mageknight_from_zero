using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;

static partial class JourneyTraining
{
    static OriginalMapCatalog ReadOriginalWorld()=>OriginalWorldOpening??throw new InvalidOperationException("Original world disabled");
    static MapState CreateOriginalWorld(TaskDefinition task,EnemyTokenSupply supply)
    {
        if(task?.OriginalWorld!=true)return null;
        if(!NativeWorld)throw new InvalidOperationException("Declared original world requires its explicit protocol flag");
        string digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes("resources/original_maps/tutorial_opening.json"))).ToLowerInvariant();
        if(task.WorldSourceSha256!=digest||digest!=OriginalWorldSourceSha)throw new InvalidOperationException("Declared original map source differs; never silently rebind");
        var map=new MapState();ReadOriginalWorld().Initialize(map,supply);return map;
    }
    static IEnumerable<Move> WorldMoves()
    {
        yield return new("worldInteract");yield return new("worldInteractEnd");
        yield return new("worldHealingBuy");
        yield return new("worldLoyaltySelect",LoyaltyActions.SourceId,Option:1);
        yield return new("worldLoyaltySelect",LoyaltyActions.SourceId,Option:0);
        foreach(var unit in Units)
        {
            yield return new("worldRecruit",unit.Id);
            yield return new("worldDismiss",unit.Id);
        }
        foreach(var cell in ReadOriginalWorld().Tiles.SelectMany(t=>t.Cells).Where(c=>!c.IsSea))
        {
            yield return new("worldMove",Targets:cell.Position.Q,Parts:cell.Position.R);
            yield return new("worldFly","hero_skill_pool_005:06",Targets:cell.Position.Q,Parts:cell.Position.R);
        }
        for(int slot=0;slot<8;slot++)yield return new("worldInfluence",Parts:slot);
    }
    sealed partial class Env
    {
        object WorldState()=>new
        {
            enabled=DeclaredTask?.OriginalWorld==true,sourceSha256=DeclaredTask?.WorldSourceSha256,q=Journey.Player.Position.Q,r=Journey.Player.Position.R,
            inNegotiation=Context.InNegotiation,interactionClosed=Context.InteractionClosed,
            loyaltySelected=Context.UseLoyaltySlot,loyaltyAvailable=LoyaltyActions.SlotAvailable(Journey.Player),
            loyaltyBound=Journey.Player.LoyaltyUnit?.Card.Id,
            cells=Journey.Game.Map.OriginalCells.Values.OrderBy(c=>c.Position.Q).ThenBy(c=>c.Position.R)
                .Select(c=>new{q=c.Position.Q,r=c.Position.R,terrain=c.TerrainName,site=c.Site,
                    safe=!c.IsSea&&(!Journey.Game.Map.Places.TryGetValue(c.Position,out var site)||site.Enemy==null)}).ToArray()
        };
        void AppendWorldFeatures(List<double> f)
        {
            f.Add(DeclaredTask?.OriginalWorld==true?1:0);f.Add(Journey.Player.Position.Q/8.0);f.Add(Journey.Player.Position.R/8.0);
            f.Add(Context.InNegotiation?1:0);f.Add(Context.InteractionClosed?1:0);
            f.Add(Context.UseLoyaltySlot?1:0);f.Add(LoyaltyActions.SlotAvailable(Journey.Player)?1:0);
            foreach(var unit in Units)f.Add(Journey.Player.LoyaltyUnit?.Card.Id==unit.Id?1:0);
            foreach(var cell in ReadOriginalWorld().Tiles.SelectMany(t=>t.Cells).OrderBy(c=>c.Position.Q).ThenBy(c=>c.Position.R))
            {
                bool exists=Journey.Game.Map.OriginalCells.ContainsKey(cell.Position);
                f.Add(exists?1:0);f.Add(exists&&cell.IsSea?1:0);
                foreach(var terrain in Enum.GetValues<TerrainType>())f.Add(exists&&cell.Terrain==terrain?1:0);
                f.Add(exists&&Journey.Game.Map.Places.TryGetValue(cell.Position,out var site)&&site.Type==PlaceType.Village?1:0);
            }
        }
        bool ApplyWorld(Move action)
        {
            if(!action.Kind.StartsWith("world",StringComparison.Ordinal))return false;
            if(!NativeWorld||DeclaredTask?.OriginalWorld!=true)throw new InvalidOperationException("Actual declared original world required");
            if(B!=null)throw new InvalidOperationException("Original world operation requires actual outside battle turn");
            var player=Journey.Player;var target=new AxialCoord(action.Targets,action.Parts);
            switch(action.Kind)
            {
                case "worldMove":
                    Journey.Move(target);
                    if(B!=null)throw new InvalidOperationException("Rampaging movement battles need separate declared-goal integration; gap retained");
                    break;
                case "worldFly":Journey.Fly(player.Skills.Single(s=>s.SourceId==action.Card),target);break;
                case "worldInteract":Journey.BeginSiteInteraction();break;
                case "worldInteractEnd":Journey.EndSiteInteraction();break;
                case "worldHealingBuy":Journey.BuyVillageHealing();break;
                case "worldLoyaltySelect":Journey.SelectLoyaltyRecruit(action.Option==1);break;
                case "worldRecruit":
                    if(player.Units.Count>=3)throw new InvalidOperationException("Current protocol only observes three unit slots; extended unit observer remains a gap");
                    Journey.RecruitSiteUnit(Journey.Game.Globals.Units.Offer.Single(u=>u.Id==action.Card));break;
                case "worldDismiss":Journey.DismissSiteUnit(player.Units.Single(u=>u.Card.Id==action.Card));break;
                case "worldInfluence":
                    if(action.Parts>=player.Deck.Hand.Count)throw new InvalidOperationException("Actual influence deed slot required");
                    Journey.SidewaysInfluence(player.Deck.Hand[action.Parts]);break;
                default:throw new InvalidOperationException("Unknown original world input");
            }
            return true;
        }
        IEnumerable<Move> WorldCandidates()=>DeclaredTask?.OriginalWorld==true&&B==null?WorldMoves():Enumerable.Empty<Move>();
    }
}
