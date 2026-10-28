using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;

CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
PveJourneySession Start(PlaceType type, RuinToken ruin = null)
{
    var p=new PlayerState{HeroId="Norowas",Fame=3};
    p.Deck.SetDeck(Enumerable.Range(0,16).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
    for(int i=0;i<2;i++)p.Deck.Hand.Add(new DeedCard("magic_009",CardType.Spell));
    for(int i=0;i<2;i++)p.Deck.Hand.Add(new DeedCard("basic_card_016",CardType.Action));
    p.Mana.AddCrystal(ManaColor.Red,3);p.Mana.AddCrystal(ManaColor.Blue,2);
    var map=new MapState();map.Placed[p.Position]=new MapTile(TileSet.Countryside,6,new[]{TerrainType.Hills});
    map.Places[p.Position]=new PlaceState(type){OriginalRuinToken=ruin,RuinToken=ruin?.Kind};
    var tokens=Enumerable.Range(0,3).Select(_=>CardJsonLoader.LoadMonsters().Single(e=>e.Id=="monster_038")).ToArray();
    var spells=CardJsonLoader.LoadSpells().ToArray();
    var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(x=>new ActionCardData(x.Id,x.Name,x.Set,x.ImagePath,x.EnImagePath,x.Top.Effect,x.Bottom.Effect,x.Top.ManaCost)));
    var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,enemySupply:new EnemyTokenSupply(tokens,new Random(1)),manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1));
    if(ruin!=null){j.Game.Globals.Ruins=new RuinTokenSupply(new[]{ruin},new Random(1));j.Game.Globals.Ruins.Draw();}
    j.SelectTactic("day_1");j.BeginTurn();return j;
}
void Kill(PveJourneySession j,int target)
{
    // Explicit original block/attack-card fixture, not original hero-start acceptance.
    if(j.Battle.Phase!=Phase.Melee)
    {
        j.Battle.Advance();
        for(int i=0;i<j.Battle.Enemies.Count;i++)
        {
            j.Battle.Play(j.Player.Deck.Hand.First(c=>c.Id=="basic_card_016"),"strong");
            j.Battle.Block(i,0);
        }
    }
    while(j.Battle.Phase!=Phase.Melee)j.Battle.Advance();
    j.Battle.Play(j.Player.Deck.Hand.First(c=>c.Id=="magic_009"),"base");
    Check(j.Battle.Attack(target).Kills==1,"Original spell kills brown guard");
}
void Finish(PveJourneySession j){while(j.Battle.Phase!=Phase.End)j.Battle.Advance();}
foreach(var type in new[]{PlaceType.MonsterDen,PlaceType.SpawningGround})
foreach(bool win in new[]{false,true})
{
    var j=Start(type);var site=j.Game.Map.Places[j.Player.Position];
    Check(!site.Conquered && site.Defenders.Count==0,"Unentered lair is not conquered and has no pre-drawn enemy");
    j.EnterLair(new FaceRandom(ManaColor.Gold));var guards=j.Battle.Enemies.ToArray();
    Check(j.Context.DayPart==DayPart.Day && j.Context.UnitsAllowed,"Lair preserves normal time and unit permissions");
    if(win){Kill(j,0);if(type==PlaceType.SpawningGround)Kill(j,1);}
    else if(type==PlaceType.SpawningGround)Kill(j,0);
    Finish(j);var encounter=j.Context.Lair;
    Check(encounter.Won==win && site.Conquered==win,"Whole lair conquest only");
    if(win)
    {
        Check(site.Defenders.Count==0 && j.EnemySupply.InUse.Count==0,"Victorious guards returned");
        Check(encounter.RewardDice.Count==(type==PlaceType.MonsterDen?2:3),"Printed reward die count");
        bool pending=false;try{j.EndTurn();}catch(InvalidOperationException){pending=true;}
        Check(pending && !j.Game.IsEndingTurn,"Gold choices block end-turn before mutation");
        SoloAdventureRewards.RevealEarned(j);
        var revealed=encounter.ArtifactChoices.ToArray();int remainingArtifacts=j.Context.ArtifactSupply.Count;
        SoloAdventureRewards.RevealEarned(j);
        Check(revealed.SequenceEqual(encounter.ArtifactChoices)&&remainingArtifacts==j.Context.ArtifactSupply.Count,
            "Earned reward reveal is idempotent and cannot draw another pair");
        var goldCombinations=SoloAdventureRewards.GoldChoices(SoloAdventureRewards.GoldSlots(j).Length).ToArray();
        Check(goldCombinations.Length==(type==PlaceType.MonsterDen?16:64),"All basic-color combinations retained for actual gold dice");
        var finalInput=new SoloEndTurnInput(Array.Empty<int>(),ArtifactRewardSlot:type==PlaceType.SpawningGround?0:-1,
            GoldRewardColors:Enumerable.Repeat(ManaColor.Blue,encounter.RewardDice.Count).ToArray());
        finalInput=System.Text.Json.JsonSerializer.Deserialize<SoloEndTurnInput>(System.Text.Json.JsonSerializer.Serialize(finalInput));
        SoloEndTurnInputs.Execute(j,finalInput);Check(encounter.RewardGranted,"Complete serialized choice formally grants lair reward");
        j.BeginTurn();bool repeat=false;try{j.EnterLair();}catch(InvalidOperationException){repeat=true;}
        Check(repeat,"Conquered lair cannot farm rewards");
    }
    else
    {
        Check(encounter.RewardDice.Count==0,"Partial victory grants no site reward");
        if(type==PlaceType.MonsterDen)Check(ReferenceEquals(site.Enemy,guards[0]),"Failed den retains same guard");
        else
        {
            Check(site.Defenders.Contains(guards[1]) && site.Defenders.Count==2,"Spawning survivor retained and count replenished");
            Check(site.HiddenGuards.Count==1 && !site.HiddenGuards.Contains(guards[1]),"Only replacement hidden");
            var publicBefore=System.Text.Json.JsonSerializer.Serialize(PublicOriginalMap.ObserveSite(j.Game.Map,site));
            var actualHidden=site.HiddenGuards.Single();
            var publicSite=PublicOriginalMap.ObserveSite(j.Game.Map,site);
            Check(publicSite.Guards.Count(g=>g.Revealed)==1 && publicSite.Guards.Count(g=>g.DefinitionId==null)==1,"Mixed known/unknown guards in public state");
            var snapshot=DemoStateCheckpoint.Capture(j.Game.Map,j.EnemySupply);
            // Alter only the unseen identity, keeping its public brown back unchanged.
            var alternative=CardJsonLoader.LoadMonsters().Single(e=>e.Id=="monster_040");
            int slot=site.AdditionalGuards.FindIndex(e=>ReferenceEquals(e,actualHidden));
            Check(slot>=0,"Replacement is a separate actual guard slot");
            site.AdditionalGuards[slot]=alternative;site.HiddenGuards.Clear();site.HiddenGuards.Add(alternative);
            Check(System.Text.Json.JsonSerializer.Serialize(PublicOriginalMap.ObserveSite(j.Game.Map,site))==publicBefore,"Hidden identity cannot change public observation");
            snapshot.Restore();
            Check(ReferenceEquals(site.HiddenGuards.Single(),actualHidden)&&site.Defenders.Contains(actualHidden),"Checkpoint restores physical hidden replacement");
            Check(System.Text.Json.JsonSerializer.Serialize(PublicOriginalMap.ObserveSite(j.Game.Map,site))==publicBefore,"Checkpoint restores public projection");
            j.EndTurn(j.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Take(1).ToArray());j.BeginTurn();j.EnterLair();
            Check(site.HiddenGuards.Count==0 && PublicOriginalMap.ObserveSite(j.Game.Map,site).Guards.All(g=>g.Revealed),"Next actual entry reveals replacements");
        }
    }
}
Console.WriteLine($"PASS {checks} actual lair encounters; configured combat fixture, not whole-scenario acceptance.");
for(int kills=0;kills<=2;kills++)
{
    var token=new RuinToken("fixture_two_brown",Array.Empty<ManaColor>(),0,
        new[]{EnemyTokenColor.Brown,EnemyTokenColor.Brown},new[]{RuinReward.Artifact});
    var j=Start(PlaceType.AncientRuin,token);var site=j.Game.Map.Places[j.Player.Position];
    j.EnterOriginalEnemyRuin();var original=j.Battle.Enemies.ToArray();
    Check(original.Length==2&&original.All(j.EnemySupply.IsInUse),"Ruin draws exact original enemy counts");
    for(int i=0;i<kills;i++)Kill(j,i);
    Finish(j);
    Check(site.Defenders.Count==2-kills,"Ruin never replaces defeated enemies");
    Check(site.Defenders.SequenceEqual(original.Skip(kills)),"Surviving physical enemy identities retained");
    Check(site.RuinConquered==(kills==2)&&j.Context.Ruin.Won==(kills==2),"Only last enemy completes ruin");
    Check(j.Game.Globals.Ruins.IsInUse(token)==(kills!=2),"Yellow token discarded only on full conquest");
    if(kills<2)
    {
        Check(j.Context.Ruin.PendingRewards.Count==0,"Partial victory gives no ruin reward");
        j.EndTurn(j.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Take(1).ToArray());j.BeginTurn();
        j.EnterOriginalEnemyRuin();
        Check(j.Battle.Enemies.SequenceEqual(original.Skip(kills)),"Next actual entry fights same survivors without new draw");
    }
    else
    {
        bool blocked=false;try{j.EndTurn();}catch(InvalidOperationException){blocked=true;}
        Check(blocked&&j.Context.Ruin.PendingRewards.Count==1,"Unresolved actual reward cannot silently disappear at end turn");
        var encounter=j.Context.Ruin;int before=j.Context.ArtifactSupply.Count;
        var choices=encounter.RevealArtifacts();var chosen=choices[0];
        Check(choices.Count==2&&j.Context.ArtifactSupply.Count==before-2,"Artifact reward draws two actual cards");
        encounter.SelectArtifacts(chosen);
        j.EndTurn(j.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Take(1).ToArray());
        Check(encounter.RewardGranted&&encounter.PendingRewards.Count==0&&j.Context.ArtifactSupply.Count==before-1,"Formal end turn keeps one and returns one artifact");
        Check(j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).Count(c=>c.Id==chosen.Id)==1,"Chosen artifact actually enters player's deck once");
    }
}
foreach(var rewards in new[]{new[]{RuinReward.Artifact,RuinReward.Artifact},new[]{RuinReward.Spell,RuinReward.FourBasicCrystals},new[]{RuinReward.AdvancedAction}})
{
    var token=new RuinToken("fixture_rewards",Array.Empty<ManaColor>(),0,new[]{EnemyTokenColor.Brown},rewards);
    var j=Start(PlaceType.AncientRuin,token);j.EnterOriginalEnemyRuin();Kill(j,0);Finish(j);
    var encounter=j.Context.Ruin;
    SoloAdventureRewards.RevealEarned(j);
    var policyRewards=SoloRuinRewardInputs.Choices(j).ToArray();
    Check(policyRewards.Length>0,"Actual ruin reward combinations are exposed to policy");
    if(rewards.Count(r=>r==RuinReward.Artifact)==2)
        Check(policyRewards.Length==6,"Two of three artifacts retain all six ordered choices");
    int artifacts=rewards.Count(r=>r==RuinReward.Artifact);var selected=Array.Empty<string>();
    if(artifacts>0){var choices=encounter.RevealArtifacts();Check(choices.Count==artifacts+1,"Multiple artifacts draw N+1 once");var keep=choices.Take(artifacts).ToArray();selected=keep.Select(c=>c.Id).ToArray();encounter.SelectArtifacts(keep);}
    string spell=null,advanced=null;
    if(rewards.Contains(RuinReward.Spell)){spell=j.Context.SpellSupply.Offer[0].Id;encounter.SelectSpell(0);}
    if(rewards.Contains(RuinReward.AdvancedAction)){advanced=j.Context.AdvActionSupply.Offer[0].Id;encounter.SelectAdvancedAction(0);}
    int white=j.Player.Mana.Crystals.GetValueOrDefault(ManaColor.White);
    var rewardInput=policyRewards.First(x=>x.SpellSlot<=0&&x.AdvancedSlot<=0);
    var finishInput=new SoloEndTurnInput(Enumerable.Range(0,j.Player.Deck.Hand.Count)
        .Where(i=>j.Player.Deck.Hand[i].Type!=CardType.Wound).Take(1).ToArray(),RuinReward:rewardInput);
    finishInput=System.Text.Json.JsonSerializer.Deserialize<SoloEndTurnInput>(System.Text.Json.JsonSerializer.Serialize(finishInput));
    SoloEndTurnInputs.Execute(j,finishInput);
    var owned=j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).Concat(j.Player.Deck.DiscardPile).ToArray();
    Check(encounter.RewardGranted,"Mixed original rewards resolved at formal end turn");
    Check(selected.All(id=>owned.Count(c=>c.Id==id)==1),"All chosen artifacts acquired once");
    if(spell!=null)Check(owned.Any(c=>c.Id==spell),"Actual spell reward acquired");
    if(advanced!=null)Check(owned.Any(c=>c.Id==advanced),"Actual advanced action reward acquired");
    if(rewards.Contains(RuinReward.FourBasicCrystals))Check(j.Player.Mana.Crystals.GetValueOrDefault(ManaColor.White)==white+1,"Four-crystal reward grants white deterministically, not dice");
}
foreach(bool full in new[]{false,true})
{
    var token=new RuinToken("fixture_unit",Array.Empty<ManaColor>(),0,new[]{EnemyTokenColor.Brown},new[]{RuinReward.Unit});
    var j=Start(PlaceType.AncientRuin,token);
    var card=CardJsonLoader.LoadUnit("army_2_006");j.Context.UnitOffer.Add(card);
    UnitState dismissed=null;
    if(full)while(j.Player.OrdinaryFreeSlots>0){dismissed=new UnitState(CardJsonLoader.LoadUnit("army_2_006"));j.Player.Units.Add(dismissed);}
    j.EnterOriginalEnemyRuin();Kill(j,0);Finish(j);var encounter=j.Context.Ruin;
    int index=Array.FindIndex(j.Context.UnitOffer.ToArray(),c=>ReferenceEquals(c,card));
    if(full)
    {
        bool rejected=false;try{encounter.SelectUnit(index);}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&j.Context.UnitOffer.Contains(card),"Full command slots require explicit actual dismissal");
    }
    int before=j.Player.Units.Count;
    var unitInput=new SoloRuinRewardInput(Array.Empty<int>(),UnitSlot:index,
        DisbandSlot:dismissed==null?-1:j.Player.Units.IndexOf(dismissed));
    SoloRuinRewardInputs.Select(j,unitInput);
    Check(j.Player.Units.Count==before&&j.Context.UnitOffer.Contains(card),"Selecting reward neither recruits nor dismisses early");
    SoloEndTurnInputs.Execute(j,new SoloEndTurnInput(Enumerable.Range(0,j.Player.Deck.Hand.Count)
        .Where(i=>j.Player.Deck.Hand[i].Type!=CardType.Wound).Take(1).ToArray(),RuinReward:unitInput));
    Check(encounter.RewardGranted&&!j.Context.UnitOffer.Contains(card),"Formal reward removes actual unit offer");
    Check(j.Player.Units.Any(u=>ReferenceEquals(u.Card,card))&&j.Player.Units.Count==before+(full?0:1),"Actual free unit acquired within command limit");
    if(full)Check(!j.Player.Units.Contains(dismissed),"Selected physical old unit dismissed at reward time");
    bool twice=false;try{encounter.SelectUnit(0);}catch(InvalidOperationException){twice=true;}
    Check(twice,"No repeated unit reward");
}
Console.WriteLine($"PASS {checks} including actual persistent ruin fights and all reward families; full printed inventory remains pending.");
sealed class FaceRandom:Random
{
    private readonly int _face;public FaceRandom(ManaColor face){_face=(int)face;}
    public override int Next(int maxValue)=>_face%maxValue;
}
