using System;
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
PveJourneySession Start(bool conquered)
{
    var player=new PlayerState{HeroId="Norowas",Fame=3};
    player.Deck.SetDeck(Enumerable.Range(0,12).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
    player.Deck.Hand.Add(new DeedCard("magic_009",CardType.Spell));
    player.Deck.Hand.Add(new DeedCard("basic_card_000",CardType.Action));player.Mana.AddCrystal(ManaColor.Red,2);
    var map=new MapState();map.Placed[player.Position]=new MapTile(TileSet.Core,1,new[]{TerrainType.Desert});
    map.Places[player.Position]=new PlaceState(PlaceType.Tomb){DungeonConquered=conquered,OwnerId=conquered?99:(int?)null};
    // Fixed original ice dragon: the supplied fire spell is sufficient without bypassing armor.
    var enemy=CardJsonLoader.LoadMonsters().Single(x=>x.Id=="monster_010");
    Check(enemy.TokenColor==EnemyTokenColor.Red,"Printed red token");
    var enemies=new EnemyTokenSupply(new[]{enemy},new Random(1));
    var spells=CardJsonLoader.LoadSpells().ToArray();
    var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()).Concat(spells.Select(x=>new ActionCardData(x.Id,x.Name,x.Set,x.ImagePath,x.EnImagePath,x.Top.Effect,x.Bottom.Effect,x.Top.ManaCost)));
    var journey=new PveJourneySession(player,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,enemySupply:enemies,manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1));
    journey.SelectTactic("day_1");journey.BeginTurn();return journey;
}
foreach(bool conquered in new[]{false,true})
{
    var j=Start(conquered);var enemy=j.EnemySupply.DrawPile.Single();
    bool wrongRejected=false;try{j.EnterDungeon();}catch(InvalidOperationException){wrongRejected=true;}
    Check(wrongRejected && ReferenceEquals(j.EnemySupply.DrawPile.Single(),enemy),"Wrong site entry does not draw");
    j.EnterTomb();Check(ReferenceEquals(j.Battle.Enemies.Single(),enemy),"Actual red token drawn");
    Check(j.Context.DayPart==DayPart.Night && !j.Context.UnitsAllowed,"Night and no units");
    var die=j.Context.ManaSource.Dice.First();die.TurnTo(ManaColor.Black);
    CardManaActions.UseSourceDie(j.Player,j.Context,die,ManaColor.Black);
    j.Battle.Play(j.Player.Deck.Hand.First(x=>x.Id=="magic_009"),"strong");
    Check(j.Battle.Attack(0).Kills==1,"Actual original spell defeats red enemy");
    while(j.Battle.Phase!=Phase.End)j.Battle.Advance();
    var encounter=j.Context.Dungeon;
    Check(encounter.Won && j.EnemySupply.InUse.Count==0 && ReferenceEquals(j.EnemySupply.DiscardPile.Single(),enemy),"Enemy discarded after battle");
    Check(j.Context.DayPart==DayPart.Day && j.Context.UnitsAllowed,"Outside state restored");
    if(!conquered)
    {
        Check(encounter.Reward==DungeonRewardKind.SpellAndArtifact && encounter.RewardDie==null,"Tomb guarantees both without a die");
        Check(encounter.Site.Conquered && encounter.Site.OwnerId==j.Player.Id,"First conquest shield");
        var artifact=j.RevealDungeonArtifactReward()[0];j.SelectDungeonArtifactReward(artifact);
        bool missingSpell=false;try{j.EndTurn();}catch(InvalidOperationException){missingSpell=true;}
        Check(missingSpell && !j.Game.IsEndingTurn,"Both choices required before formal end");
        var spell=j.Game.Globals.Spells.Offer[0];j.SelectSiteSpellReward(spell);j.EndTurn();
        var owned=j.Player.Deck.Hand.Concat(j.Player.Deck.DrawPile).ToArray();
        Check(owned.Count(c=>c.Id==artifact.Id && c.Type==CardType.Artifact)==1,"Artifact once");
        Check(owned.Count(c=>c.Id==spell.Id && c.Type==CardType.Spell)==1,"Spell once");
    }
    else
    {
        Check(encounter.Reward==DungeonRewardKind.None && encounter.Site.OwnerId==99,"Revisit fame only; previous shield retained");
        j.EndTurn();
    }
}
{
    var j=Start(false);j.EnterTomb();var enemy=j.Battle.Enemies.Single();
    while(j.Battle.Phase!=Phase.End)j.Battle.Advance();
    Check(!j.Context.Dungeon.Won && !j.Context.Dungeon.Site.Conquered,"Failure does not grant conquest");
    Check(j.Context.Dungeon.Reward==DungeonRewardKind.None && !j.Context.PendingSpellReward,"Failure grants no reward");
    Check(ReferenceEquals(j.EnemySupply.DiscardPile.Single(),enemy),"Failure also discards actual red token");
    j.EndTurn(j.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).Take(1).ToArray());j.BeginTurn();j.EnterTomb();
    Check(ReferenceEquals(j.Battle.Enemies.Single(),enemy) && j.EnemySupply.DiscardPile.Count==0,"Later entry draws from recyclable red supply");
}
Console.WriteLine($"PASS {checks} real tomb entry/combat/reward checks; complete scenario training not yet integrated.");
