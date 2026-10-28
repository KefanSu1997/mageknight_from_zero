using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

int checks=0;
var skillBindingFixtures=new System.Collections.Generic.List<object>();
void SkillFixture(SoloConquestEpisode e,SoloSkillInput[] commands)
    =>skillBindingFixtures.Add(new {kind="Skill",observation=e.Observe(),commands});
void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
void Reject(Action action,string message)
{
    bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}
    Check(rejected,message);
}
string root=Directory.GetCurrentDirectory();
CardJsonLoader.SetBaseDirectory(Path.Combine(root,"resources","text_json"));
var catalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}
    .Select(f=>Path.Combine(root,"resources","original_maps",f)).ToArray());
var setup=new SoloEpisodeSetup(1,"Norowas","Goldyx",new[]{0,1,2,0},SoloDummyDualColorRule.MaximumMatchingCrystals,
    new SoloEpisodeSeeds(1,191,192,193,194,195,196,197,198,199,200));
var episode=SoloEpisodeFactory.Create(setup,catalog);
// Deterministic interface fixture, not a training task or a success demonstration.
for(int n=0;!episode.Journey.Game.HasActiveTurn&&n<10;n++)
{
    var support=SoloDecisionSupport.Capture(episode);
    int index=Enumerable.Range(0,support.CommandsJson.Count).First(i=>
        JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[i]).Clock!=null);
    support.ExecuteRecorded(episode,index);
}
var j=episode.Journey;var p=j.Player;
Check(j.Game.HasActiveTurn,"Actual original hero turn opened");
var leadership=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Norowas").SkillPoolId).Single(s=>s.SourceId=="hero_skill_pool_002:06");
episode.Apply(_=>
{
    p.Skills.Add(leadership);
    p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_007")));
    p.Units.Add(new UnitState(CardJsonLoader.LoadUnit("army_007")));
    j.Game.Map.Places[p.Position]=new PlaceState(PlaceType.SpawningGround);
});
Reject(()=>SoloUnitInputs.Apply(episode,new(0,0,LeadershipSlot:p.Skills.IndexOf(leadership))),
    "Leadership cannot be used as an outside resource action");
SoloAdventureInputs.Apply(episode,SoloAdventureInputs.Legal(episode).Single());
Check(j.Context.InBattle&&j.Context.UnitsAllowed,"Native spawning ground permits owned units");
var boundary=DemoStateCheckpoint.Capture(episode);
string before=SoloStateDigest.Compute(episode);
var choices=SoloUnitInputs.Legal(episode).ToArray();
Check(before==SoloStateDigest.Compute(episode),"Unit support probing preserves private graph and RNG");
var unitObservation=episode.Observe();
Check(before==SoloStateDigest.Compute(episode),"Public printed abilities do not mutate native graph");
Check(unitObservation.Inventory.Units.All(u=>u.Actions.Length==p.Units[u.Slot].Card.Actions.Length
    &&u.Actions.All(a=>a.Index>=0&&a.Kind==p.Units[u.Slot].Card.Actions[a.Index].Kind.ToString()
        &&JsonSerializer.Serialize(a.Definition)==JsonSerializer.Serialize(p.Units[u.Slot].Card.Actions[a.Index]))),
    "Public unit ability index, printed text, costs and numeric fields match native definitions");
int fixtureArg=Array.IndexOf(args,"--binding-fixtures");
if(fixtureArg>=0)File.WriteAllText(args[fixtureArg+1],JsonSerializer.Serialize(new {observation=unitObservation,commands=choices}));
var ordinary=choices.Single(x=>x.UnitSlot==0&&x.Ability==0&&x.LeadershipSlot==-1);
var enhanced=choices.Single(x=>x.UnitSlot==0&&x.Ability==0&&x.LeadershipSlot==p.Skills.IndexOf(leadership));
SoloUnitInputs.Apply(episode,ordinary);
int ordinaryPower=j.Context.CombatPower.Attacks.Single().Value;
Check(!SkillActions.IsUsed(p,leadership),"Ordinary unit activation retains unused leadership");
boundary.Restore();
var command=new SoloPolicyCommand(1,episode.Steps,Unit:enhanced);
command=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(command));
command.Execute(episode);
var attack=j.Context.CombatPower.Attacks.Single();
Check(attack.Value==ordinaryPower+1&&attack.Type==AttackType.Ranged&&attack.Element==Element.Fire
    &&ReferenceEquals(attack.SourceUnit,p.Units[0]),"Leadership adds native ranged bonus to selected actual unit");
Check(!p.Units[0].IsReady&&p.Units[1].IsReady&&SkillActions.IsUsed(p,leadership),
    "Combined operation exhausts only selected unit and marks skill used");
Check(!SoloUnitInputs.Legal(episode).Any(x=>x.LeadershipSlot>=0),"Spent leadership cannot be reused through another unit");
string after=SoloStateDigest.Compute(episode);
Reject(()=>SoloUnitInputs.Apply(episode,enhanced with {UnitSlot=1}),"Repeated leadership on ready second unit rejected");
Check(after==SoloStateDigest.Compute(episode),"Rejected reuse preserves full private state");
boundary.Restore();command.Execute(episode);
Check(after==SoloStateDigest.Compute(episode),"Serialized leadership decision replays exact private state");
var wolf=SoloEpisodeFactory.Create(setup with {Hero="Wolfhawk"},catalog);
for(int n=0;!wolf.Journey.Game.HasActiveTurn&&n<10;n++)
{
    var support=SoloDecisionSupport.Capture(wolf);
    int index=Enumerable.Range(0,support.CommandsJson.Count).First(i=>
        JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[i]).Clock!=null);
    support.ExecuteRecorded(wolf,index);
}
var wj=wolf.Journey;var wp=wj.Player;
var bullseye=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Wolfhawk").SkillPoolId).Single(s=>s.SourceId=="hero_skill_pool_004:04");
wolf.Apply(_=>
{
    wp.Skills.Add(bullseye);
    wp.Deck.SetDeck(new[]{new DeedCard("advanced_card_000",CardType.Action),new DeedCard("advanced_card_001",CardType.Action)});
    wp.Deck.DrawExact(2);
    wp.Mana.AddCrystal(ManaColor.Red);wp.Mana.AddCrystal(ManaColor.Blue);
    wj.Game.Map.Places[wp.Position]=new PlaceState(PlaceType.SpawningGround);
});
SoloAdventureInputs.Apply(wolf,SoloAdventureInputs.Legal(wolf).Single());
foreach(string cardId in new[]{"advanced_card_000","advanced_card_001"})
    wolf.Apply(_=>wj.Battle.Play(wp.Deck.Hand.Single(c=>c.Id==cardId),"strong"));
var bullBoundary=DemoStateCheckpoint.Capture(wolf);
string bullBefore=SoloStateDigest.Compute(wolf);
var bullChoices=SoloSkillInputs.Legal(wolf).Where(x=>x.AttackSourceSlot>=0).ToArray();
SkillFixture(wolf,bullChoices);
var publicAttacks=wolf.Observe().Combat.Attacks;
Check(publicAttacks.All(a=>a.SourceCardId==(wj.Context.CombatPower.Attacks[a.Slot].SourceCard as DeedCard)?.Id
    &&a.PlayedCardSlot>=0&&ReferenceEquals(wp.Deck.PlayedCards[a.PlayedCardSlot],wj.Context.CombatPower.Attacks[a.Slot].SourceCard)),
    "Public attack choices map to actual visible card identities and physical played slots");
Check(bullChoices.Length==2&&bullBefore==SoloStateDigest.Compute(wolf),
    "Bullseye retains both physical card contributions without spending skill during enumeration");
foreach(var choice in bullChoices)
{
    var original=wj.Context.CombatPower.Attacks[choice.AttackSourceSlot];
    var other=wj.Context.CombatPower.Attacks[1-choice.AttackSourceSlot];
    var bullCommand=new SoloPolicyCommand(1,wolf.Steps,Skill:choice);
    bullCommand=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(bullCommand));
    bullCommand.Execute(wolf);
    var boosted=wj.Context.CombatPower.Attacks[choice.AttackSourceSlot];
    Check(boosted.Value==original.Value+1&&boosted.Element==original.Element
        &&ReferenceEquals(boosted.SourceCard,original.SourceCard)&&ReferenceEquals(other,wj.Context.CombatPower.Attacks[1-choice.AttackSourceSlot]),
        "Bullseye modifies only selected actual card and retains element/source/other attack");
    Check(SkillActions.IsUsed(wp,bullseye)&&!SoloSkillInputs.Legal(wolf).Any(x=>x.AttackSourceSlot>=0),
        "Bullseye is consumed once, not a reusable preselection");
    string bullAfter=SoloStateDigest.Compute(wolf);
    Reject(()=>SoloSkillInputs.Apply(wolf,choice),"Repeated bullseye rejected");
    Check(bullAfter==SoloStateDigest.Compute(wolf),"Repeated bullseye cannot farm a contribution");
    bullBoundary.Restore();bullCommand.Execute(wolf);
    Check(bullAfter==SoloStateDigest.Compute(wolf),"Bullseye serialized command exact private-state replay");
    bullBoundary.Restore();
}
var krang=SoloEpisodeFactory.Create(setup with {Hero="Krang"},catalog);
for(int n=0;!krang.Journey.Game.HasActiveTurn&&n<10;n++)
{
    var support=SoloDecisionSupport.Capture(krang);
    int index=Enumerable.Range(0,support.CommandsJson.Count).First(i=>
        JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[i]).Clock!=null);
    support.ExecuteRecorded(krang,index);
}
var kj=krang.Journey;var kp=kj.Player;
var puppet=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Krang").SkillPoolId).Single(s=>s.SourceId==PuppetMasterActions.SkillId);
krang.Apply(_=>{kp.Skills.Add(puppet);kj.Game.Map.Places[kp.Position]=new PlaceState(PlaceType.SpawningGround);});
SoloAdventureInputs.Apply(krang,SoloAdventureInputs.Legal(krang).Single());
// Supply a decisive contribution only to exercise the native kill/reservation interface.
// This fixture is not a policy trajectory or evidence of a reachable training build.
krang.Apply(_=>{kj.Context.CombatPower.AddAttack(100,Element.ColdFire,AttackType.Siege);kj.Battle.Attack(0);});
var physicalToken=kj.Battle.Enemies[0];
Check(kj.Context.DefeatedEnemyIndices.Contains(0)&&kp.PuppetTokens.DefeatedThisTurn.Any(t=>ReferenceEquals(t,physicalToken)),
    "Formal enemy defeat creates actual physical puppet candidate");
var keepBoundary=DemoStateCheckpoint.Capture(krang);
string keepBefore=SoloStateDigest.Compute(krang);
var keepChoice=SoloSkillInputs.Legal(krang).Single(x=>x.PuppetSlot>=0&&x.Option==0);
SkillFixture(krang,new[]{keepChoice});
Check(keepBefore==SoloStateDigest.Compute(krang)&&krang.Observe().Inventory.PuppetCandidates[keepChoice.PuppetSlot].Id==physicalToken.Id,
    "Puppet support and public candidate slot refer to the defeated physical token");
var keepCommand=new SoloPolicyCommand(1,krang.Steps,Skill:keepChoice);
keepCommand=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(keepCommand));
keepCommand.Execute(krang);
Check(kp.PuppetTokens.DefeatedThisTurn.Count==0&&ReferenceEquals(kp.PuppetTokens.KeptTokens.Single(),physicalToken)
    &&krang.Observe().Inventory.PuppetStored.Single().Id==physicalToken.Id,
    "Keep transfers actual candidate into persistent public stored list");
Check(SkillActions.IsUsed(kp,puppet)&&!SoloSkillInputs.Legal(krang).Any(x=>x.PuppetSlot>=0),
    "Keeping spends this turn's skill and prevents immediate reuse");
string keptDigest=SoloStateDigest.Compute(krang);
keepBoundary.Restore();keepCommand.Execute(krang);
Check(keptDigest==SoloStateDigest.Compute(krang),"Physical token reservation and keep replay exactly");
// Separate next-use branch fixture: no claim of traversing the intervening full turn.
krang.Apply(_=>
{
    typeof(SkillActions).GetMethod("StartTurn",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
        .Invoke(null,new object[]{kp});
    kj.Battle.Advance();
});
Check(kj.Battle.Phase==Phase.Block,"Native phase advance reaches block consumer");
var consumeBoundary=DemoStateCheckpoint.Capture(krang);
var consumeChoice=SoloSkillInputs.Legal(krang).Single(x=>x.PuppetSlot==0&&x.Option==2);
SkillFixture(krang,new[]{consumeChoice});
var consumeCommand=new SoloPolicyCommand(1,krang.Steps,Skill:consumeChoice);
consumeCommand=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(consumeCommand));
consumeCommand.Execute(krang);
Check(kp.PuppetTokens.KeptTokens.Count==0&&krang.Observe().Inventory.PuppetStored.Length==0
    &&kj.Context.CombatPower.Blocks.Last().Value==(physicalToken.Armor+1)/2,
    "Block consumer removes physical stored token and produces native half-armor power");
string consumedDigest=SoloStateDigest.Compute(krang);
Reject(()=>SoloSkillInputs.Apply(krang,consumeChoice),"Consumed puppet cannot be reused");
Check(consumedDigest==SoloStateDigest.Compute(krang),"Invalid puppet reuse preserves state and supply");
consumeBoundary.Restore();consumeCommand.Execute(krang);
Check(consumedDigest==SoloStateDigest.Compute(krang),"Puppet consumption and supply release replay exactly");
var brae=SoloEpisodeFactory.Create(setup with {Hero="Braevalar"},catalog);
for(int n=0;!brae.Journey.Game.HasActiveTurn&&n<10;n++)
{
    var support=SoloDecisionSupport.Capture(brae);
    int index=Enumerable.Range(0,support.CommandsJson.Count).First(i=>
        JsonSerializer.Deserialize<SoloPolicyCommand>(support.CommandsJson[i]).Clock!=null);
    support.ExecuteRecorded(brae,index);
}
var bj=brae.Journey;var bp=bj.Player;
var fork=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Braevalar").SkillPoolId).Single(s=>s.SourceId==ForkedLightningActions.SourceId);
brae.Apply(_=>{bp.Skills.Add(fork);bj.Game.Map.Places[bp.Position]=new PlaceState(PlaceType.SpawningGround);});
SoloAdventureInputs.Apply(brae,SoloAdventureInputs.Legal(brae).Single());
var forkBoundary=DemoStateCheckpoint.Capture(brae);
string forkBefore=SoloStateDigest.Compute(brae);
var forks=SoloSkillInputs.Legal(brae).Where(x=>x.EnemySlots!=null).ToArray();
SkillFixture(brae,forks);
Check(forks.Length==3&&forks.Select(x=>string.Join(",",x.EnemySlots)).ToHashSet().SetEquals(new[]{"0","1","0,1"}),
    "Two actual enemies expose every nonempty lightning subset exactly once");
Check(forkBefore==SoloStateDigest.Compute(brae),"Multi-target probing preserves private state");
foreach(var choice in forks)
{
    var forkCommand=new SoloPolicyCommand(1,brae.Steps,Skill:choice);
    forkCommand=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(forkCommand));
    forkCommand.Execute(brae);
    var contributions=bj.Context.CombatPower.Attacks;
    Check(contributions.Count==choice.EnemySlots.Length&&contributions.Select((a,i)=>
        a.Value==1&&a.Element==Element.ColdFire&&a.Type==AttackType.Ranged
        &&ReferenceEquals(a.RequiredEnemy,bj.Battle.Enemies[choice.EnemySlots[i]])&&ReferenceEquals(a.SourceSkill,fork)).All(x=>x),
        "Each lightning contribution is restricted to its selected physical enemy");
    Check(SkillActions.IsUsed(bp,fork)&&!SoloSkillInputs.Legal(brae).Any(x=>x.EnemySlots!=null),
        "Lightning is one consumed operation without repeatable target preselection");
    string forkAfter=SoloStateDigest.Compute(brae);
    Reject(()=>SoloSkillInputs.Apply(brae,choice),"Repeated lightning rejected");
    Check(forkAfter==SoloStateDigest.Compute(brae),"Rejected lightning reuse preserves state");
    forkBoundary.Restore();forkCommand.Execute(brae);
    Check(forkAfter==SoloStateDigest.Compute(brae),"Lightning JSON replay preserves private state");
    forkBoundary.Restore();
}
foreach(var invalid in new[]{Array.Empty<int>(),new[]{0,0},new[]{-1},new[]{2},new[]{0,1,0,1}})
{
    Reject(()=>SoloSkillInputs.Apply(brae,new(bp.Skills.IndexOf(fork),0,EnemySlots:invalid)),"Malformed lightning target set rejected");
    Check(forkBefore==SoloStateDigest.Compute(brae),"Malformed lightning leaves all resources intact");
}
var shape=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Braevalar").SkillPoolId).Single(s=>s.SourceId=="hero_skill_pool_001:06");
brae.Apply(_=>
{
    bp.Skills.Add(shape);
    bp.Deck.SetDeck(new[]{new DeedCard("basic_card_023",CardType.Action),new DeedCard("basic_card_008",CardType.Action)});
    bp.Deck.DrawExact(2);bp.Mana.AddCrystal(ManaColor.Red);
    bj.Battle.Advance();
});
Check(bj.Battle.Phase==Phase.Block,"Shapeshift fixture reaches native block phase");
var shapeBoundary=DemoStateCheckpoint.Capture(brae);
string shapeBefore=SoloStateDigest.Compute(brae);
var shapes=SoloShapeshiftInputs.Legal(brae).Where(x=>bp.Deck.Hand[x.HandSlot].Id=="basic_card_023"&&x.Option==0).ToArray();
var shapeFixture=JsonSerializer.SerializeToElement(new {kind="Shapeshift",observation=brae.Observe(),commands=shapes});
Check(shapes.Length==2&&shapes.Any(x=>x.Enhanced)&&shapes.Any(x=>!x.Enhanced)
    &&shapeBefore==SoloStateDigest.Compute(brae),"Both original paid and base shapeshift modes survive exact probing");
foreach(var choice in shapes)
{
    var source=bp.Deck.Hand[choice.HandSlot];var material=bp.Deck.Hand[choice.MaterialSlot];
    int manaBefore=bp.Mana.GetTotal();
    var shapeCommand=new SoloPolicyCommand(1,brae.Steps,Shapeshift:choice);
    shapeCommand=JsonSerializer.Deserialize<SoloPolicyCommand>(JsonSerializer.Serialize(shapeCommand));
    shapeCommand.Execute(brae);
    Check(bj.Context.BlockPool==(choice.Enhanced?5:3)&&bp.Mana.GetTotal()==manaBefore-(choice.Enhanced?1:0),
        "Shapeshift retains printed output and original enhanced mana payment");
    Check(bp.Deck.PlayedCards.Any(c=>ReferenceEquals(c,source))&&bp.Deck.DiscardPile.Any(c=>ReferenceEquals(c,material))
        &&SkillActions.IsUsed(bp,shape),"Actual source played and distinct material discarded with skill consumed");
    Check(!SoloShapeshiftInputs.Legal(brae).Any(),"Used shapeshift cannot repeat");
    string shapeAfter=SoloStateDigest.Compute(brae);
    shapeBoundary.Restore();shapeCommand.Execute(brae);
    Check(shapeAfter==SoloStateDigest.Compute(brae),"Serialized shapeshift preserves exact state and payment on replay");
    shapeBoundary.Restore();
}
var sampleShape=shapes[0];
foreach(var invalid in new[]{sampleShape with {MaterialSlot=-1},sampleShape with {MaterialSlot=sampleShape.HandSlot},
    sampleShape with {Target=FixedOutputKind.Movement},sampleShape with {Hex=new AxialCoord(0,0)}})
{
    Reject(()=>SoloShapeshiftInputs.Apply(brae,invalid),"Invalid shapeshift physical payment or phase rejected");
    Check(shapeBefore==SoloStateDigest.Compute(brae),"Rejected shapeshift preserves resources, skill and RNG");
}
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="owned special skill branch fixtures",formal_training_started=false}));
int skillFixtureArg=Array.IndexOf(args,"--skill-binding-fixtures");
if(skillFixtureArg>=0)File.WriteAllText(args[skillFixtureArg+1],JsonSerializer.Serialize(skillBindingFixtures));
int shapeFixtureArg=Array.IndexOf(args,"--equipment-binding-fixtures");
if(shapeFixtureArg>=0)File.WriteAllText(args[shapeFixtureArg+1],JsonSerializer.Serialize(new[]{shapeFixture}));
