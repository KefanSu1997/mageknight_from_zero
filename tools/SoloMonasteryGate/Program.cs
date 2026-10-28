using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;
int checks=0;
var rewardBindings=new List<JsonElement>();
void CaptureRewards(SoloConquestEpisode episode,SoloPolicyCommand[] commands)
{
    if(args.Contains("--binding-fixtures"))rewardBindings.Add(JsonSerializer.SerializeToElement(
        new {kind="EndTurn",observation=episode.Observe(),commands=commands.Select(c=>c.EndTurn).ToArray()}));
}
void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
void Reject(Action action,string text){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,text);}
CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources","text_json"));
var catalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}
    .Select(f=>Path.Combine("resources","original_maps",f)).ToArray());
var setup=new SoloEpisodeSetup(1,"Norowas","Goldyx",new[]{0,1,2,0},SoloDummyDualColorRule.MaximumMatchingCrystals,
    new SoloEpisodeSeeds(1,191,192,193,194,195,196,197,198,199,200));
var e=SoloEpisodeFactory.Create(setup,catalog);var j=e.Journey;var p=j.Player;
for(int i=0;!j.Game.HasActiveTurn&&i<10;i++)
{
    var s=SoloDecisionSupport.Capture(e);
    s.ExecuteRecorded(e,Enumerable.Range(0,s.CommandsJson.Count).First(k=>JsonSerializer.Deserialize<SoloPolicyCommand>(s.CommandsJson[k]).Clock!=null));
}
Check(j.Game.HasActiveTurn,"Original hero turn opened");
// Explicit site and unit fixture, not a policy success demonstration.
e.Apply(_=>{j.Game.Map.Places[p.Position]=new PlaceState(PlaceType.Monastery);p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_007")));});
var root=DemoStateCheckpoint.Capture(e);var site=j.Game.Map.Places[p.Position];
int reputation=p.Reputation;var day=j.Context.DayPart;
string before=SoloStateDigest.Compute(e);
var choices=SoloAdventureInputs.Legal(e);
Check(choices.Length==1&&choices[0].Kind==SoloAdventureKind.BurnMonastery&&before==SoloStateDigest.Compute(e),"Burn proposal preserves hidden enemy supply");
var command=new SoloPolicyCommand(1,e.Steps,Adventure:choices.Single());
command=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(command));command.Execute(e);
Check(p.Reputation==reputation-3&&j.Context.DayPart==day&&j.Context.InBattle&&!j.Context.UnitsAllowed,
    "Burning costs three reputation, retains actual day/night, forbids units");
Check(j.Battle.Enemies.Single().TokenColor==EnemyTokenColor.Purple&&!site.MonasteryBurned&&!site.Conquered,
    "Actual violet defender does not imply monastery already destroyed");
Check(!SoloUnitInputs.Legal(e).Any(),"No unit activation enters legal support");
var combat=DemoStateCheckpoint.Capture(e);string combatHash=SoloStateDigest.Compute(e);
root.Restore();command.Execute(e);Check(combatHash==SoloStateDigest.Compute(e),"Burn entry replays exact hidden draw and penalty");
// Failure: normal phase progression and actual hero damage, no artificial win flag.
while(j.Context.InBattle)e.Apply(_=>j.Battle.Advance());
Check(j.Context.Dungeon.Completed&&!j.Context.Dungeon.Won&&!site.MonasteryBurned&&site.OwnerId==null
    &&j.Context.Dungeon.Reward==DungeonRewardKind.None,"Failed burn leaves monastery intact without reward");
Check(p.Reputation==reputation-3,"Failure cannot refund declaration penalty");
combat.Restore();
// Supply a decisive attack only for native reward lifecycle coverage.
e.Apply(_=>{j.Context.CombatPower.AddAttack(100,Element.ColdFire,AttackType.Siege);j.Battle.Attack(0);});
while(j.Context.InBattle)e.Apply(_=>j.Battle.Advance());
Check(site.MonasteryBurned&&site.Conquered&&site.OwnerId==p.Id&&j.Context.Dungeon.Won,
    "Formal victory destroys and owns actual monastery");
Check(!j.CanInteractAtCurrentSite&&!SoloAdventureInputs.Legal(e).Any()
    &&PublicOriginalMap.ObserveSite(j.Game.Map,site).MonasteryBurned,"Destroyed monastery has no further interaction or burn branch");
Check(j.Context.Dungeon.Reward==DungeonRewardKind.Artifact&&j.Context.Dungeon.ArtifactChoices.Count==0,
    "Victory records reward but does not reveal hidden artifacts");
int artifacts=p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact);
e.Apply(_=>{p.Mana.AddToken(ManaColor.Red,2);j.Context.Healing.Add(3);});
var beforeCommit=DemoStateCheckpoint.Capture(e);
string preCommit=SoloStateDigest.Compute(e);
var commitment=SoloEndTurnInputs.Legal(e).First();
Check(commitment.CommitRewards&&preCommit==SoloStateDigest.Compute(e),"Commit proposals do not leak or consume hidden reward cards");
SoloEndTurnInputs.Apply(e,commitment);
Check(j.CommittedRewardEnd!=null&&j.Context.Dungeon.ArtifactChoices.Count==2&&e.Observe().Offers.CommittedRewardEnd!=null,
    "Only committed end turn reveals reward and exposes locked choices");
Check(j.Game.IsEndingTurn&&p.Mana.Tokens.Count==0&&j.Context.Healing.Total==0,
    "Native cleanup expires mana and healing before hidden artifact disclosure");
Reject(()=>j.Game.CompleteEndCurrentTurn(j.Context),"Native completion cannot skip deferred reward grant");
string committed=SoloStateDigest.Compute(e);
var resolution=SoloPolicyCommand.ImplementedChoices(e).ToArray();
CaptureRewards(e,resolution);
Check(resolution.Length==2&&resolution.All(c=>c.EndTurn!=null&&!c.EndTurn.CommitRewards),"Only actual reward resolution is permitted after commitment");
Reject(()=>new SoloPolicyCommand(1,e.Steps,Basic:new(SoloBasicKind.SourceDie,0,ManaColor.Blue)).Execute(e),
    "Cannot return to normal play after reward reveal");
Reject(()=>SoloEndTurnInputs.Apply(e,resolution[0].EndTurn with {DiscardHandSlots=Array.Empty<int>()}),
    "Cannot revise previously committed discard after seeing artifacts");
Check(committed==SoloStateDigest.Compute(e),"Rejected post-reveal changes preserve full state");
beforeCommit.Restore();SoloEndTurnInputs.Apply(e,commitment);
Check(committed==SoloStateDigest.Compute(e),"Commitment and hidden draw replay exactly");
var rewardBoundary=DemoStateCheckpoint.Capture(e);
var end=SoloEndTurnInputs.Legal(e).First();
SoloEndTurnInputs.Apply(e,end);
Check(j.Context.Dungeon.RewardGranted&&p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact)==artifacts+1,
    "Native end turn grants one actual artifact");
string done=SoloStateDigest.Compute(e);rewardBoundary.Restore();SoloEndTurnInputs.Apply(e,end);
Check(done==SoloStateDigest.Compute(e),"Reward and next hand replay exactly");
Reject(()=>SoloEndTurnInputs.Apply(e,end),"Completed turn cannot grant artifact again");
Check(done==SoloStateDigest.Compute(e),"Duplicate reward rejection is transactional");
foreach(var siteType in new[]{PlaceType.Dungeon,PlaceType.Tomb,PlaceType.SpawningGround})
{
    var a=SoloEpisodeFactory.Create(setup,catalog);var aj=a.Journey;
    for(int i=0;!aj.Game.HasActiveTurn&&i<10;i++)
    {
        var support=SoloDecisionSupport.Capture(a);
        support.ExecuteRecorded(a,Enumerable.Range(0,support.CommandsJson.Count).First(k=>JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[k]).Clock!=null));
    }
    a.Apply(_=>aj.Game.Map.Places[aj.Player.Position]=new PlaceState(siteType));
    SoloAdventureInputs.Apply(a,SoloAdventureInputs.Legal(a).Single());
    a.Apply(_=>
    {
        for(int k=0;k<aj.Battle.Enemies.Count;k++)
        {aj.Context.CombatPower.AddAttack(100,Element.ColdFire,AttackType.Siege);aj.Battle.Attack(k);}
    });
    while(aj.Context.InBattle)a.Apply(_=>aj.Battle.Advance());
    Check(SoloAdventureRewards.ArtifactCount(aj)==0,"No pre-commit artifact disclosure: "+siteType);
    if(!SoloAdventureRewards.NeedsArtifactReveal(aj))
    {
        Check(siteType==PlaceType.Dungeon&&aj.Context.Dungeon.Reward==DungeonRewardKind.Spell,
            "Only random spell-only dungeon reward bypasses hidden artifact commitment");
        continue;
    }
    var ending=SoloEndTurnInputs.Legal(a).First();
    Check(ending.CommitRewards,"Actual adventure reward requires commit: "+siteType);
    SoloEndTurnInputs.Apply(a,ending);
    Check(SoloAdventureRewards.ArtifactCount(aj)==2&&aj.CommittedRewardEnd!=null,"Committed adventure reveals pair: "+siteType);
    var onlyRewards=SoloPolicyCommand.ImplementedChoices(a).ToArray();
    Check(onlyRewards.Length>0&&onlyRewards.All(c=>c.EndTurn!=null&&!c.EndTurn.CommitRewards),
        "Adventure has only forward reward resolution: "+siteType);
    var saved=DemoStateCheckpoint.Capture(a);
    onlyRewards[0].Execute(a);string completed=SoloStateDigest.Compute(a);
    saved.Restore();onlyRewards[0].Execute(a);
    Check(completed==SoloStateDigest.Compute(a),"Adventure committed reward replay exact: "+siteType);
}
// Synthetic reward combinations exercise the interface only; these are NOT claimed
// to be the unverified printed ruin inventory and must not enter formal task pools.
foreach(var extraReward in new[]{RuinReward.Spell,RuinReward.AdvancedAction,RuinReward.Unit,RuinReward.Artifact,RuinReward.FourBasicCrystals})
{
    var r=SoloEpisodeFactory.Create(setup,catalog);var rj=r.Journey;var rp=rj.Player;
    for(int i=0;!rj.Game.HasActiveTurn&&i<10;i++)
    {
        var support=SoloDecisionSupport.Capture(r);
        support.ExecuteRecorded(r,Enumerable.Range(0,support.CommandsJson.Count).First(k=>JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[k]).Clock!=null));
    }
    r.Apply(_=>
    {
        var token=new RuinToken("synthetic-interface-"+extraReward,Array.Empty<ManaColor>(),0,
            new[]{EnemyTokenColor.Green},new[]{RuinReward.Artifact,extraReward});
        rj.Game.Globals.Ruins=new RuinTokenSupply(new[]{token},new Random(712));
        var ruin=new PlaceState(PlaceType.AncientRuin){OriginalRuinToken=rj.Game.Globals.Ruins.Draw(),RuinToken=RuinTokenType.Enemy};
        rj.Game.Map.Places[rp.Position]=ruin;rj.Game.Map.RevealedSites.Add(ruin);
        if(extraReward==RuinReward.Unit)
        {
            var regular=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).First(c=>!c.IsElite&&!c.InteractionRecruitOnly);
            rj.Game.Globals.Units.SetDeck(new[]{regular});rj.Game.Globals.Units.AppendRegularToOffer(1);
        }
    });
    SoloAdventureInputs.Apply(r,SoloAdventureInputs.Legal(r).Single());
    r.Apply(_=>{rj.Context.CombatPower.AddAttack(100,Element.ColdFire,AttackType.Siege);rj.Battle.Attack(0);});
    while(rj.Context.InBattle)r.Apply(_=>rj.Battle.Advance());
    Check(rj.Context.Ruin.Won&&rj.Context.Ruin.ArtifactChoices.Count==0,"Ruin reward hidden before commit: "+extraReward);
    string beforeRuinProbe=SoloStateDigest.Compute(r);
    var commit=SoloEndTurnInputs.Legal(r).First();
    Check(commit.CommitRewards&&beforeRuinProbe==SoloStateDigest.Compute(r),"Ruin commit probe preserves private graph: "+extraReward);
    SoloEndTurnInputs.Apply(r,commit);
    int wanted=extraReward==RuinReward.Artifact?2:1;
    Check(rj.Context.Ruin.ArtifactChoices.Count==wanted+1&&rj.Game.IsEndingTurn,"Ruin reveals correct joint reward after cleanup: "+extraReward);
    var pending=DemoStateCheckpoint.Capture(r);string pendingDigest=SoloStateDigest.Compute(r);
    var rewards=SoloPolicyCommand.ImplementedChoices(r).ToArray();
    Check(rewards.Length>0&&rewards.All(c=>c.EndTurn?.RuinReward?.ArtifactSlots.Length==wanted)
        &&pendingDigest==SoloStateDigest.Compute(r),"All actual ruin resolution candidates preserve supply: "+extraReward);
    int priorUnits=rp.Units.Count;
    int priorArtifacts=rp.Deck.Hand.Concat(rp.Deck.DrawPile).Concat(rp.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact);
    rewards[0].Execute(r);
    Check(rj.Context.Ruin.RewardGranted&&rp.Deck.Hand.Concat(rp.Deck.DrawPile).Concat(rp.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact)==priorArtifacts+wanted,
        "Ruin grants exact artifact count: "+extraReward);
    if(extraReward==RuinReward.Unit)Check(rp.Units.Count==priorUnits+1&&rj.Game.Globals.Units.Offer.Count==0,"Ruin free recruit consumes actual offer once");
    if(extraReward==RuinReward.FourBasicCrystals)Check(new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.All(c=>rp.Mana.Crystals.GetValueOrDefault(c)==1),"Ruin grants all four crystals once");
    string finished=SoloStateDigest.Compute(r);pending.Restore();rewards[0].Execute(r);
    Check(finished==SoloStateDigest.Compute(r),"Joint ruin reward and next hand replay exactly: "+extraReward);
    Reject(()=>rewards[0].Execute(r),"Stale ruin reward cannot be repeated");
    Check(finished==SoloStateDigest.Compute(r),"Repeated ruin reward preserves private state: "+extraReward);
}
Console.WriteLine($"PASS {checks} monastery/adventure native checks; injected site/attack fixtures, not complete scenario or policy acceptance.");
int bindingOutput=Array.IndexOf(args,"--binding-fixtures");
if(bindingOutput>=0)File.WriteAllText(args[bindingOutput+1],JsonSerializer.Serialize(rewardBindings));
