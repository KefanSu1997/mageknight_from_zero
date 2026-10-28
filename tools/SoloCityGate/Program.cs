using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;
using MK.Logic.Data.Cards;

CardJsonLoader.SetBaseDirectory(Path.Combine(Directory.GetCurrentDirectory(),"resources/text_json"));
int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
var bindingFixtures=new System.Collections.Generic.List<object>();
void ExportBindings(PveJourneySession journey,string kind,object commands)
{
    bindingFixtures.Add(new {kind,commands,observation=new {
        Combat=journey.ObserveSoloCombat(),Inventory=new {
            Units=journey.Player.Units.Select((u,i)=>new {Slot=i,Id=u.Card.Id,Wounds=u.Wounds,Ready=u.IsReady}).ToArray()
        }},scope="controlled native combat fixture, not full solo-conquest trajectory"});
}
foreach(var type in new[]{PlaceType.Keep,PlaceType.MageTower,PlaceType.City})
{
    var p=new PlayerState();var origin=p.Position;var destination=new AxialCoord(origin.Q+1,origin.R);
    var map=new MapState();var site=new PlaceState(type){Enemy=CardJsonLoader.LoadMonsters().First()};
    map.Places[destination]=site;
    map.Placed[destination]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    var context=new ActionContext{MovementMap=map,DayPart=DayPart.Day,MovementPool=5};
    var movement=new MovementService();
    Check(!movement.TryMoveUsingPool(p,destination,map,context),"Ordinary move cannot bypass hostile "+type);
    Check(p.Position.Equals(origin)&&context.MovementPool==5&&!context.MovedThisTurn&&!context.InBattle,
        "Rejected fortified entry leaves position, movement and combat unchanged");
    site.Enemy=null;site.OwnerId=p.Id;
    Check(movement.TryMoveUsingPool(p,destination,map,context),"Conquered friendly site remains accessible "+type);
    Check(p.Position.Equals(destination)&&context.MovementPool==3,"Friendly entry pays actual terrain cost");
}
foreach(var color in new[]{CityColor.Red,CityColor.Green,CityColor.Blue,CityColor.White})
foreach(var level in new[]{5,8})
{
    var supply=new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(3));
    var before=supply.DrawPile.ToArray();var city=SoloCityGarrison.Reveal(color,level,supply);
    Check(city.CityLevel==level&&city.CityColor==color&&!city.Conquered,"Actual unconquered city metadata");
    Check(city.Defenders.Count==(level==5?3:4),"Guard count");
    Check(city.Defenders.All(e=>supply.IsInUse(e)&&before.Any(b=>ReferenceEquals(b,e))),"Actual physical supply tokens");
    Check(city.Defenders.Select(e=>e.TokenColor).SequenceEqual(SoloCityGarrison.Composition(color,level)),"Composition consumed exactly");
    var map=new MapState();var pos=new AxialCoord(1,0);map.Places.Add(pos,city);map.Placed.Add(pos,new MapTile(TileSet.City,1,new[]{TerrainType.Plains}));
    Check(PublicOriginalMap.ObserveSite(map,city).Guards.All(g=>!g.Revealed&&g.DefinitionId==null),"City guards initially hidden");
    var player=new PlayerState{Position=new AxialCoord(0,0)};SiteVisibility.RevealVisible(player,map,DayPart.Night);
    Check(PublicOriginalMap.ObserveSite(map,city).Guards.All(g=>g.Revealed&&g.DefinitionId!=null),"Adjacent city reveals even at night");
    var poor=new EnemyTokenSupply(before.Where(e=>e.TokenColor!=EnemyTokenColor.White),new Random(3));var unchanged=poor.DrawPile.ToArray();
    bool rejected=false;try{SoloCityGarrison.Reveal(color,level,poor);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&poor.InUse.Count==0&&poor.DrawPile.SequenceEqual(unchanged),"Insufficient supply rejection before any draw");
}
bool unsupported=false;try{SoloCityGarrison.Composition(CityColor.Blue,7);}catch(ArgumentOutOfRangeException){unsupported=true;}
Check(unsupported,"No silent approximation of unsupported variant levels");
Console.WriteLine($"PASS {checks} actual city guard/visibility checks; no claim of city assault or full scenario acceptance.");
var catalog=PrintedTileCatalog.Parse(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}
    .Select(f=>File.ReadAllText(Path.Combine("resources/original_maps",f))).ToArray());
foreach(var tile in catalog.Tiles.Values)
{
    var globals=new GlobalResources(1,()=>DayPart.Day,new Random(3)){Enemies=new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(3))};
    var cards=CardJsonLoader.LoadAdvancedActions().Select(c=>new AdvActionCard(c.Id)).ToArray();
    globals.AdvActions.SetDeck(cards);
    var map=new MapState();
    if(tile.Cells.Any(c=>c.Site=="AncientRuin"))
    {
        bool rejected=false;try{SoloPrintedSiteFactory.Prepare(tile,map,globals);}catch(NotSupportedException){rejected=true;}
        Check(rejected&&globals.Enemies.InUse.Count==0&&globals.MonasteryOffer.Count==0,"Missing ruin inventory rejects before site effects");
        continue;
    }
    var sites=SoloPrintedSiteFactory.Prepare(tile,map,globals);
    OriginalTileMapPlacement.Place(map,tile,new AxialCoord(0,0),sites);
    Check(globals.MonasteryOffer.Count==tile.Cells.Count(c=>c.Site=="Monastery"),"Monastery gets one actual advanced action");
    Check(globals.AdvActions.Offer.Count==0,"Monastery does not modify advanced action offer");
    Check(globals.MonasteryOffer.All(c=>cards.Any(original=>ReferenceEquals(original,c))),"Monastery physical card identity");
    foreach(var site in sites.Values)
    {
        Check(site.Defenders.All(globals.Enemies.IsInUse),"Every placed guard belongs to actual supply");
        if(site.Type==PlaceType.Dungeon||site.Type==PlaceType.Tomb||site.Type==PlaceType.MonsterDen||site.Type==PlaceType.SpawningGround)
            Check(site.Defenders.Count==0,"Adventure-site guard draw deferred until entry");
        if(site.Type==PlaceType.City)Check(site.CityLevel==5,"First revealed city is level5");
    }
    if(tile.HasCity)
    {
        var second=SoloPrintedSiteFactory.Prepare(tile,map,globals).Values.Single(s=>s.Type==PlaceType.City);
        Check(second.CityLevel==8,"Second revealed city is level8");
    }
}
Console.WriteLine($"PASS {checks} including actual printed-site supply initialization; ruins and full exploration session remain separate gates.");
{
    // Select a non-ruin integration fixture; never use this filter for training world generation.
    ulong seed=1;
    for(;seed<100;seed++)
    {
        var probe=SoloConquestTileSupply.Create(catalog,seed);
        var opening=Enumerable.Range(0,3).Select(_=>probe.Draw()).ToArray();
        if(opening.All(t=>t.Cells.All(c=>c.Site!="AncientRuin"))
            &&opening.Take(2).Any(t=>t.Cells.Any(c=>c.Site=="RampagingOrcs"||c.Site=="Keep"||c.Site=="MageTower")))break;
    }
    Check(seed<100,"Explicit supported integration fixture found");
    var supply=SoloConquestTileSupply.Create(catalog,seed);var map=new MapState();
    var p=new PlayerState{HeroId="Norowas",Position=new AxialCoord(2,-3)};
    p.Deck.SetDeck(Enumerable.Range(0,16).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
    var spells=CardJsonLoader.LoadSpells().ToArray();
    var cards=CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions());
    var j=new PveJourneySession(p,cards,CardJsonLoader.LoadItems(),map:map,spells:spells,
        enemySupply:new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(1)),
        manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1));
    j.Game.Globals.Enemies=new EnemyTokenSupply();
    bool setupFailed=false;try{j.InitializeSoloOriginalMap(catalog,supply);}catch(InvalidOperationException){setupFailed=true;}
    Check(setupFailed&&map.OriginalTiles.Count==0&&map.Places.Count==0&&supply.Capture().Revealed.Length==0,
        "Incomplete opening supply rolls back all drawn tiles and map cells");
    j.Game.Globals.Enemies=j.EnemySupply;j.InitializeSoloOriginalMap(catalog,supply);
    Check(map.OriginalCells.Count==21&&supply.Capture().Main.Length==9&&supply.Capture().Revealed.Length==2,
        "Opening consumes two of seven countryside tiles, not two extra tiles");
    Check(p.Position.Equals(catalog.Tiles["A"].Cells.Single(c=>c.Site=="Portal").Local),"Hero starts on actual printed portal");
    bool setupTwice=false;try{j.InitializeSoloOriginalMap(catalog,supply);}catch(InvalidOperationException){setupTwice=true;}
    Check(setupTwice&&map.OriginalCells.Count==21,"Setup cannot redraw existing world");
    p.Position=new AxialCoord(2,-3); // Explicit exploration-edge fixture, not a policy move.
    bool inactive=false;try{j.ExploreOriginal(supply,new AxialCoord(4,-5));}catch(InvalidOperationException){inactive=true;}
    Check(inactive&&supply.Capture().Revealed.Length==2,"Inactive session cannot explore");
    Check(j.OriginalExplorationPositions(supply).Count==0,"Inactive policy receives no exploration support");
    j.SelectTactic("day_1");j.BeginTurn();j.Context.MovementPool=2;
    var legalPositions=j.OriginalExplorationPositions(supply).ToArray();
    Check(legalPositions.Contains(new AxialCoord(4,-5)),"Actual exploration target exposed before tile draw");
    var beforeSupport=supply.Capture();
    var hiddenOrder=(string[])beforeSupport.Main.Clone();
    (hiddenOrder[0],hiddenOrder[1])=(hiddenOrder[1],hiddenOrder[0]);
    var alternative=SoloConquestTileSupply.Restore(catalog,beforeSupport with {Main=hiddenOrder});
    Check(legalPositions.SequenceEqual(j.OriginalExplorationPositions(alternative)),
        "Changing hidden next tile identity leaves public exploration support unchanged");
    Check(supply.Capture().Main.SequenceEqual(beforeSupport.Main) && supply.Capture().RandomState==beforeSupport.RandomState,
        "Candidate enumeration never draws or changes supply RNG");
    j.Context.MovementPool=1;
    Check(j.OriginalExplorationPositions(supply).Count==0,"Movement cost enforced in candidate support");
    j.Context.MovementPool=2;j.Context.TurnActionCommitted=true;
    Check(j.OriginalExplorationPositions(supply).Count==0,"Committed action prevents later exploration candidates");
    j.Context.TurnActionCommitted=false;
    var beforeProbe=DemoStateCheckpoint.Capture(j,supply);
    foreach(var position in legalPositions)
    {
        var drawn=j.ExploreOriginal(supply,position);
        Check(j.Game.Map.OriginalTiles[drawn.Id].Anchor.Equals(position) && j.Context.MovementPool==0,
            "Every emitted exploration option executes the same paid native transaction");
        beforeProbe.Restore();
    }
    var next=supply.Capture().Main[0];j.ExploreOriginal(supply,new AxialCoord(4,-5));
    Check(map.OriginalTiles.ContainsKey(next)&&map.OriginalCells.Count==28,"Actual journey exploration places expected top tile");
    Check(j.Context.MovementPool==0&&!j.Context.TurnActionCommitted,"Exploration spends movement without consuming action");
    Check(map.Places.Values.SelectMany(s=>s.Defenders).All(j.EnemySupply.IsInUse),"Journey map guards preserve actual supply identity");
}
Console.WriteLine($"PASS {checks} including formal journey exploration; configured start, not seven-hero full-scenario acceptance.");
{
    var token=new RuinToken("fixture_blue_altar",new[]{ManaColor.Blue,ManaColor.Blue,ManaColor.Blue},7,
        Array.Empty<EnemyTokenColor>(),Array.Empty<RuinReward>());
    var supply=new RuinTokenSupply(new[]{token},new Random(1));
    var checkpoint=DemoStateCheckpoint.Capture(supply);
    Check(ReferenceEquals(supply.Draw(),token)&&supply.IsInUse(token),"Draw preserves physical ruin token");
    supply.Discard(token);
    bool duplicate=false;try{supply.Discard(token);}catch(InvalidOperationException){duplicate=true;}
    Check(duplicate,"Cannot discard same ruin twice");
    checkpoint.Restore();
    Check(supply.Remaining==1&&!supply.IsInUse(token),"Ruin inventory rollback");
    var globals=new GlobalResources(1,()=>DayPart.Night,new Random(1)){Ruins=supply,
        Enemies=new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(1))};
    globals.AdvActions.SetDeck(CardJsonLoader.LoadAdvancedActions().Select(c=>new AdvActionCard(c.Id)));
    var tile=catalog.Tiles.Values.First(t=>t.Cells.Count(c=>c.Site=="AncientRuin")==1);
    var map=new MapState();var sites=SoloPrintedSiteFactory.Prepare(tile,map,globals);
    var site=sites.Values.Single(s=>s.Type==PlaceType.AncientRuin);
    Check(ReferenceEquals(site.OriginalRuinToken,token)&&supply.IsInUse(token),"Actual token bound to revealed map site");
    OriginalTileMapPlacement.Place(map,tile,new AxialCoord(0,0),sites);
    Check(PublicOriginalMap.ObserveSite(map,site).RuinToken==null
        &&PublicOriginalMap.ObserveSite(map,site).RuinDetails==null,"Night unrevealed ruin content hidden");
    var p=new PlayerState();bool bypass=false;
    try{site.EnterRuin(p,true,true);}catch(InvalidOperationException){bypass=true;}
    Check(bypass&&p.Fame==0&&!site.RuinConquered,"Boolean shortcut cannot grant original altar fame");
}
Console.WriteLine($"PASS {checks} including physical ruin inventory and legacy shortcut rejection; full printed inventory and actual ruin actions pending.");
{
    var token=new RuinToken("fixture_blue_altar",new[]{ManaColor.Blue,ManaColor.Blue,ManaColor.Blue},7,
        Array.Empty<EnemyTokenColor>(),Array.Empty<RuinReward>());
    var supply=new RuinTokenSupply(new[]{token},new Random(1));
    var p=new PlayerState{HeroId="Norowas"};
    p.Deck.SetDeck(Enumerable.Range(0,16).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
    var map=new MapState();var site=new PlaceState(PlaceType.AncientRuin){RuinToken=RuinTokenType.Altar,OriginalRuinToken=supply.Draw()};
    map.Places[p.Position]=site;map.Placed[p.Position]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map,
        manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1));
    j.Game.Globals.Ruins=supply;j.SelectTactic("day_1");j.BeginTurn();
    var details=PublicOriginalMap.ObserveSite(map,site).RuinDetails;
    Check(details!=null&&details.Fame==7&&details.ManaCost.SequenceEqual(new[]{"Blue","Blue","Blue"}),"Visible altar exposes exact payment and reward");
    p.Mana.AddCrystal(ManaColor.Blue,2);
    bool poor=false;try{j.ActivateOriginalAltar();}catch(InvalidOperationException){poor=true;}
    Check(poor&&p.Mana.Crystals[ManaColor.Blue]==2&&p.Fame==0&&!site.RuinConquered&&supply.IsInUse(token),"Insufficient altar payment is atomic");
    p.Mana.AddCrystal(ManaColor.Blue,1);j.Context.MovementPool=2;j.Context.InfluencePool=4;
    j.ActivateOriginalAltar();
    Check(p.Mana.Crystals[ManaColor.Blue]==0&&p.Fame==7,"Three actual mana paid for seven fame");
    Check(site.RuinConquered&&site.OwnerId==p.Id&&!supply.IsInUse(token),"Altar conquest binds shield and discards token");
    Check(j.Context.TurnActionCommitted&&j.Context.MovementPool==0&&j.Context.InfluencePool==0,"Altar consumes turn action and movement phase ends");
    bool repeated=false;try{j.ActivateOriginalAltar();}catch(InvalidOperationException){repeated=true;}
    Check(repeated&&p.Fame==7,"Altar reward cannot repeat");
    Check(PublicOriginalMap.ObserveSite(map,site).RuinDetails==null,"Conquered altar has no active token offer");
}
Console.WriteLine($"PASS {checks} including formal altar payment/conquest; printed inventory and enemy-ruin rewards still pending.");
foreach(var type in new[]{PlaceType.Keep,PlaceType.MageTower,PlaceType.City})
{
    var p=new PlayerState{HeroId="Norowas"};
    p.Deck.SetDeck(Enumerable.Range(0,16).Select(_=>new DeedCard("basic_card_000",CardType.Action)));
    var map=new MapState();var origin=p.Position;var target=new AxialCoord(origin.Q+1,origin.R);
    map.Placed[origin]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    map.Placed[target]=new MapTile(TileSet.Countryside,1,new[]{type==PlaceType.City?TerrainType.Desert:TerrainType.Plains});
    var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map,
        manaRandom:new Random(1),tacticRandom:new Random(1),supplyRandom:new Random(1),
        enemySupply:new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(1)));
    var guard=j.EnemySupply.Draw(EnemyTokenColor.Gray);
    var site=new PlaceState(type){Enemy=guard};map.Places[target]=site;
    var rampager=j.EnemySupply.Draw(EnemyTokenColor.Green);
    var rampagePosition=new AxialCoord(origin.Q,origin.R+1);
    map.Placed[rampagePosition]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    map.Places[rampagePosition]=new PlaceState(PlaceType.OrcRampaging){Enemy=rampager};
    j.SelectTactic("day_1");j.BeginTurn();j.Context.MovementPool=1;
    bool rejected=false;try{j.AssaultFortifiedSite(target);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&p.Position.Equals(origin)&&j.Context.MovementPool==1&&j.Battle==null&&p.Reputation==0,
        "Underfunded assault rolls back world and action "+type);
    j.Context.MovementPool=2;j.AssaultFortifiedSite(target);
    Check(p.Position.Equals(target)&&j.Context.InBattle&&j.Context.MovementPool==0&&j.Context.TurnActionCommitted,
        "Paid assault commits movement and enters combat "+type);
    Check(j.Battle.Enemies.Count==2&&ReferenceEquals(j.Battle.Enemies[0],guard)&&ReferenceEquals(j.Battle.Enemies[1],rampager),
        "Actual guard and provoked rampager share combat "+type);
    Check(CombatMath.IsFortified(guard,0,j.Context)&&!j.Context.IgnoreSiteFortifications,
        "Normal assault retains site fortification "+type);
    Check(CombatMath.IsFortified(rampager,1,j.Context)==rampager.Abilities.Contains(Ability.Fortified),
        "Provoked enemy does not inherit site fortification "+type);
    Check(p.Reputation==(type==PlaceType.City?0:-1)&&j.Context.TunnelOrigin.Equals(origin),
        "Assault reputation and exact retreat origin "+type);
    bool premature=false;try{j.FinishFortifiedAssault(true);}catch(InvalidOperationException){premature=true;}
    Check(premature&&p.Position.Equals(target),"Cannot bypass unfinished assault by withdrawing");
    while(j.Battle.Phase!=Phase.End)
    {
        if(j.Battle.Phase!=Phase.AssignDamage)
        {
            var advance=new SoloBasicInput(SoloBasicKind.AdvanceCombat,ExpectedPhase:j.Battle.Phase);
            advance=System.Text.Json.JsonSerializer.Deserialize<SoloBasicInput>(System.Text.Json.JsonSerializer.Serialize(advance));
            SoloBasicOperations.Execute(j,advance);continue;
        }
        bool unsafeAdvance=false;
        try{SoloBasicOperations.Execute(j,new(SoloBasicKind.AdvanceCombat,ExpectedPhase:Phase.AssignDamage));}
        catch(InvalidOperationException){unsafeAdvance=true;}
        Check(unsafeAdvance&&j.Battle.Phase==Phase.AssignDamage,"Generic phase advance cannot bypass complete damage allocation");
        var damageCheckpoint=DemoStateCheckpoint.Capture(j);
        int woundsBeforeProbe=p.Wounds;
        var damageChoices=SoloDefenseInputs.LegalDamage(j).ToArray();
        ExportBindings(j,"Damage",damageChoices);
        Check(damageChoices.Length==1&&p.Wounds==woundsBeforeProbe&&j.Battle.Phase==Phase.AssignDamage,
            "Without owned units complete damage support has exactly one unchanged hero-allocation outcome");
        j.Battle.Advance();int directWounds=p.Wounds;var directPhase=j.Battle.Phase;
        damageCheckpoint.Restore();
        var allocations=Enumerable.Range(0,j.Battle.Enemies.Count)
            .SelectMany(e=>Enumerable.Range(0,j.Battle.Enemies[e].AttackCount)
                .Select(a=>new SoloDamageAllocation(e,a,Array.Empty<int>()))).ToArray();
        var damageInput=new SoloDamageInput(allocations);
        damageInput=System.Text.Json.JsonSerializer.Deserialize<SoloDamageInput>(System.Text.Json.JsonSerializer.Serialize(damageInput));
        SoloDefenseInputs.Damage(j,damageInput);
        Check(p.Wounds==directWounds&&j.Battle.Phase==directPhase,"Complete serialized damage matches native unblocked settlement "+type);
        bool replay=false;try{SoloDefenseInputs.Damage(j,damageInput);}catch(InvalidOperationException){replay=true;}
        Check(replay&&p.Wounds==directWounds,"Damage command cannot repeat after formal settlement");
    }
    Check(p.Wounds>0&&!j.Context.InBattle,"Unblocked real defenders inflict persistent wounds");
    Check(SoloAssaultSettlement.Legal(j).Length==1&&SoloAssaultSettlement.Legal(j)[0].Withdraw
        &&p.Position.Equals(target)&&j.Context.TunnelAssaultActive,"Losing assault support only permits retreat and probing preserves world");
    bool stay=false;try{j.FinishFortifiedAssault(false);}catch(InvalidOperationException){stay=true;}
    Check(stay&&p.Position.Equals(target),"Surviving guards prevent occupying the site");
    j.FinishFortifiedAssault(true);
    Check(p.Position.Equals(origin)&&!j.Context.TunnelAssaultActive&&!j.Context.FortifiedSite,
        "Finished losing assault returns to exact origin");
    Check(type==PlaceType.City?ReferenceEquals(site.Enemy,guard)&&j.EnemySupply.IsInUse(guard)
        :site.Enemy==null&&site.GuardNeedsReplacement&&!j.EnemySupply.IsInUse(guard),
        "City retains survivor; keep/tower discards and requires replacement");
    Check(ReferenceEquals(map.Places[rampagePosition].Enemy,rampager)&&j.EnemySupply.IsInUse(rampager),
        "Surviving rampager remains in its actual world site");
    bool twice=false;try{j.FinishFortifiedAssault(true);}catch(InvalidOperationException){twice=true;}
    Check(twice,"Assault settlement cannot repeat");
}
Console.WriteLine($"PASS {checks} including native assault, unblocked combat and retreat; winning conquest and complete scenario remain separate gates.");
// Controlled one-guard fixtures exercise settlement through real card play and combat.
// They are not original starting decks or level-5/8 city strength demonstrations.
foreach(var type in new[]{PlaceType.Keep,PlaceType.MageTower,PlaceType.City})
{
    var guardDefinition=CardJsonLoader.LoadMonsters().Where(e=>e.TokenColor==EnemyTokenColor.Gray)
        .OrderBy(e=>e.Attack).ThenBy(e=>e.Armor).First();
    Console.WriteLine($"Winning fixture guard: {guardDefinition.Id}, armor {guardDefinition.Armor}, attack {guardDefinition.Attack}, {string.Join(',',guardDefinition.Abilities)}");
    var supply=new EnemyTokenSupply(new[]{guardDefinition},new Random(71));
    var p=new PlayerState{HeroId="Norowas"};
    p.Deck.SetDeck(Enumerable.Range(0,16).Select(_=>new DeedCard("basic_card_016",CardType.Action)));
    var map=new MapState();var origin=p.Position;var target=new AxialCoord(origin.Q+1,origin.R);
    map.Placed[origin]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    map.Placed[target]=new MapTile(TileSet.Countryside,1,new[]{TerrainType.Plains});
    var guard=supply.Draw(EnemyTokenColor.Gray);var site=new PlaceState(type){Enemy=guard};map.Places[target]=site;
    var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions(),map:map,
        spells:CardJsonLoader.LoadSpells(),enemySupply:supply,manaRandom:new Random(71),tacticRandom:new Random(71));
    j.SelectTactic("day_1");j.BeginTurn();j.Context.MovementPool=2;
    j.AssaultFortifiedSite(target);
    j.Battle.Advance();
    var blockCheckpoint=DemoStateCheckpoint.Capture(j);
    new MK.Logic.Runtime.CardEffects.OutmaneuverEffect(false).Execute(p,j.Context);
    int unreduced=CardCombatActions.DefenseProfile(guard,0,j.Context).Attack;
    var followups=SoloCombatFollowups.Legal(j).ToArray();
    Check(followups.Any(x=>x.Kind==SoloCombatFollowupKind.ReduceAttack)&&j.Context.AttackReductionTimes==1
        &&CardCombatActions.DefenseProfile(guard,0,j.Context).Attack==unreduced,
        "Followup enumeration preserves native pending reduction and enemy attack");
    var reduce=followups.First(x=>x.Kind==SoloCombatFollowupKind.ReduceAttack);
    reduce=System.Text.Json.JsonSerializer.Deserialize<SoloCombatFollowup>(System.Text.Json.JsonSerializer.Serialize(reduce));
    SoloCombatFollowups.Execute(j,reduce);
    Check(j.ObserveSoloCombat().AttackReductionTimes==0
        &&j.ObserveSoloCombat().Enemies[0].Attacks[0].Power==Math.Max(0,unreduced-2)
        &&!SoloCombatFollowups.Legal(j).Any(x=>x.Kind==SoloCombatFollowupKind.ReduceAttack),
        "Serialized reduction consumes one native use and changes effective attack exactly once");
    bool repeatedReduction=false;
    try{SoloCombatFollowups.Execute(j,reduce);}catch(InvalidOperationException){repeatedReduction=true;}
    Check(repeatedReduction,"Consumed reduction cannot be repeated as a zero-cost loop");
    blockCheckpoint.Restore();
    j.Battle.Play(p.Deck.Hand.First(),"block");
    var blockSources=j.Context.CombatPower.Blocks.ToArray();
    var combatBefore=j.ObserveSoloCombat();
    Check(combatBefore.Active&&combatBefore.Phase=="Block"&&combatBefore.Enemies.Length==j.Battle.Enemies.Count
        &&combatBefore.Enemies[0].Attacks.Length==guard.AttackCount&&combatBefore.Blocks.Length==blockSources.Length,
        "Combat observer preserves actual enemy attack and block source cardinalities");
    Check(combatBefore.Enemies[0].Attacks[0].Power==CardCombatActions.DefenseProfile(guard,0,j.Context,0).Attack
        &&combatBefore.Enemies[0].EffectiveArmor==CombatMath.EffectiveArmor(guard,0,j.Context,j.Battle.Phase),
        "Combat observer uses native effective attack and armor values");
    var blockOptions=SoloDefenseInputs.LegalBlocks(j).ToArray();
    ExportBindings(j,"Block",blockOptions);
    Check(blockOptions.Length>0&&!j.Battle.IsBlocked(0)&&blockSources.SequenceEqual(j.Context.CombatPower.Blocks),
        "Block support probing preserves unconsumed physical contributions");
    var blockInput=System.Text.Json.JsonSerializer.Deserialize<SoloBlockInput>(System.Text.Json.JsonSerializer.Serialize(blockOptions[0]));
    SoloDefenseInputs.Block(j,blockInput);
    Check(j.Battle.IsBlocked(0)&&!SoloDefenseInputs.LegalBlocks(j).Any(),"Complete block consumes its source and cannot block twice");
    Check(j.ObserveSoloCombat().Enemies[0].Attacks[0].Blocked&&j.ObserveSoloCombat().Blocks.Length==0,
        "Combat observer reflects consumed block and blocked physical attack");
    blockCheckpoint.Restore();
    j.Battle.Advance();j.Battle.Advance();
    foreach(var card in p.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray())
    {
        int slot=p.Deck.Hand.ToList().FindIndex(c=>ReferenceEquals(c,card));
        var deed=new SoloDeedInput(slot,"attack");
        deed=System.Text.Json.JsonSerializer.Deserialize<SoloDeedInput>(System.Text.Json.JsonSerializer.Serialize(deed));
        SoloDeedInputs.Execute(j,deed);
    }
    var beforeAttackParts=j.Context.CombatPower.Attacks.ToArray();
    var attackView=j.ObserveSoloCombat();
    Check(attackView.Attacks.Length==beforeAttackParts.Length&&attackView.Attacks.Select(x=>x.Slot)
        .SequenceEqual(Enumerable.Range(0,beforeAttackParts.Length))&&beforeAttackParts.SequenceEqual(j.Context.CombatPower.Attacks),
        "Combat observer retains physical attack contribution indices without mutating them");
    var attackOptions=SoloAttackInputs.Legal(j).ToArray();
    ExportBindings(j,"Attack",attackOptions);
    Check(attackOptions.Length>0&&p.Fame==0&&beforeAttackParts.SequenceEqual(j.Context.CombatPower.Attacks)
        &&j.Context.EnemyDefense.Selected.Count==0,"Complete attack support probing preserves contributions, fame and defense choices");
    var attackInput=System.Text.Json.JsonSerializer.Deserialize<SoloAttackInput>(System.Text.Json.JsonSerializer.Serialize(attackOptions[0]));
    SoloAttackInputs.Execute(j,attackInput);
    Check(j.Context.DefeatedEnemyIndices.Contains(0)&&p.Fame==guard.Fame,"Serialized complete attack awards printed enemy fame once "+type);
    j.Battle.Advance();
    bool retreat=false;try{j.FinishFortifiedAssault(true);}catch(InvalidOperationException){retreat=true;}
    Check(SoloAssaultSettlement.Legal(j).Length==1&&!SoloAssaultSettlement.Legal(j)[0].Withdraw
        &&supply.IsInUse(guard)&&site.OwnerId!=p.Id,"Winning assault support only permits occupation and probing does not consume guards");
    Check(retreat&&!site.Conquered&&supply.IsInUse(guard),"Won assault cannot choose withdrawal or prematurely discard guard");
    j.FinishFortifiedAssault(false);
    Check(site.Conquered&&site.OwnerId==p.Id&&p.Position.Equals(target)&&!supply.IsInUse(guard),
        "Victory occupies actual site and releases its physical guard "+type);
    Check(site.CityShields.Count==(type==PlaceType.City?1:0),"Only city defender grants city shield");
    if(type==PlaceType.MageTower)
    {
        Check(j.Context.PendingSpellReward,"Tower victory creates pending native spell reward");
        bool missing=false;try{j.EndTurn();}catch(InvalidOperationException){missing=true;}
        Check(missing&&j.Game.HasActiveTurn,"Cannot end tower turn without selecting actual reward");
        var reward=j.Game.Globals.Spells.Offer.First();
        var finishInput=new SoloEndTurnInput(Array.Empty<int>(),SpellRewardSlot:0);
        finishInput=System.Text.Json.JsonSerializer.Deserialize<SoloEndTurnInput>(System.Text.Json.JsonSerializer.Serialize(finishInput));
        SoloEndTurnInputs.Execute(j,finishInput);
        Check(!j.Context.PendingSpellReward&&!j.Game.Globals.Spells.Offer.Any(c=>ReferenceEquals(c,reward)),
            "Formal end turn consumes selected supply spell exactly once");
    }
    else j.EndTurn();
    Check(j.CompletedTurns==1&&j.CompletedBattles==1&&p.Fame==guard.Fame,
        "Victory closes a single actual world turn without repeated fame");
}
Console.WriteLine($"PASS {checks} including real-card winning assault and deferred tower reward; controlled fixtures, not full-scenario policy acceptance.");
// Real original explosive-bolts card, actual two-enemy encounter, no injected followup counter.
{
    var definitions=CardJsonLoader.LoadMonsters().ToArray();
    var weak=definitions.First(m=>m.Armor<=3&&m.Abilities.Count==0);
    var survivor=definitions.First(m=>m.Id!=weak.Id&&m.Armor>1&&m.Abilities.Count==0);
    var p=new PlayerState{HeroId="Tovak"};
    p.Deck.SetDeck(new[]{new DeedCard("advanced_card_041",CardType.Action)});
    var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
        manaRandom:new Random(301),tacticRandom:new Random(302));
    j.SelectTactic("day_1");j.BeginTurn();p.Mana.AddToken(ManaColor.Red,1);
    j.BeginBattle(new[]{weak,survivor});
    SoloDeedInputs.Execute(j,new(0,"strong_red"));
    Check(j.Context.CombatPower.Attacks.Single().ArmorReductionsPerDefeatedEnemy==1,
        "Original enhanced explosive bolts produces the native kill-dependent armor effect");
    SoloAttackInputs.Execute(j,new(new[]{0},new[]{0},Array.Empty<SoloDefendInput>()));
    Check(j.Context.DefeatedEnemyIndices.Contains(0)&&!j.Context.DefeatedEnemyIndices.Contains(1)
        &&j.ObserveSoloCombat().ExplosiveArmorUses==1,
        "Real attack kill earns exactly one pending explosive armor use");
    int armorBefore=CombatMath.EffectiveArmor(survivor,1,j.Context,j.Battle.Phase);
    var followups=SoloCombatFollowups.Legal(j).ToArray();
    Check(followups.Any(x=>x.Kind==SoloCombatFollowupKind.ExplosiveArmor&&x.EnemySlot==1)
        &&followups.Any(x=>x.Kind==SoloCombatFollowupKind.ExplosiveArmor&&x.EnemySlot==-1)
        &&!followups.Any(x=>x.EnemySlot==0)&&j.Context.ExplosiveBoltArmorUses==1
        &&CombatMath.EffectiveArmor(survivor,1,j.Context,j.Battle.Phase)==armorBefore,
        "Armor followups include survivor or decline, exclude defeated enemy, and probing preserves state");
    var pending=DemoStateCheckpoint.Capture(j);
    var choice=followups.Single(x=>x.EnemySlot==1);
    choice=System.Text.Json.JsonSerializer.Deserialize<SoloCombatFollowup>(System.Text.Json.JsonSerializer.Serialize(choice));
    SoloCombatFollowups.Execute(j,choice);
    Check(j.Context.ExplosiveBoltArmorUses==0&&CombatMath.EffectiveArmor(survivor,1,j.Context,j.Battle.Phase)==armorBefore-1,
        "Serialized followup consumes one real kill reward and reduces surviving armor by one");
    bool repeat=false;try{SoloCombatFollowups.Execute(j,choice);}catch(InvalidOperationException){repeat=true;}
    Check(repeat,"Consumed explosive kill reward cannot be farmed repeatedly");
    pending.Restore();SoloCombatFollowups.Execute(j,followups.Single(x=>x.EnemySlot==-1));
    Check(j.Context.ExplosiveBoltArmorUses==0&&CombatMath.EffectiveArmor(survivor,1,j.Context,j.Battle.Phase)==armorBefore,
        "Declining clears the real pending reward without modifying surviving enemy armor");
    j.Battle.Advance();
    Check(j.Battle.Phase==Phase.Block,"Resolved explosive reward permits formal battle phase advance");
}
Console.WriteLine($"PASS {checks} including real explosive-bolts kill and finite followup; full scenario remains pending.");
{
    var p=new PlayerState{HeroId="Tovak"};
    p.Deck.SetDeck(new[]{new DeedCard("advanced_card_006",CardType.Action),new DeedCard("advanced_card_018",CardType.Action)});
    var j=new PveJourneySession(p,CardJsonLoader.LoadBasicActions().Concat(CardJsonLoader.LoadAdvancedActions()),
        manaRandom:new Random(311),tacticRandom:new Random(312));
    j.SelectTactic("day_1");j.BeginTurn();
    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddToken(color,2);
    j.BeginBattle(new[]{CardJsonLoader.LoadMonsters().First(m=>m.Abilities.Count==0)});
    int agilitySlot=p.Deck.Hand.FindIndex(c=>c.Id=="advanced_card_006");
    SoloDeedInputs.Execute(j,new(agilitySlot,"strong"));
    var conversions=SoloCombatFollowups.Legal(j).Where(x=>x.Kind==SoloCombatFollowupKind.MoveToRanged).ToArray();
    Check(conversions.Select(x=>x.Amount).SequenceEqual(new[]{1,2})&&j.Context.MovementPool==4
        &&j.ObserveSoloCombat().MoveCostRanged==2,
        "Native agility exposes all affordable ranged conversion sizes without spending during enumeration");
    var convert=System.Text.Json.JsonSerializer.Deserialize<SoloCombatFollowup>(System.Text.Json.JsonSerializer.Serialize(conversions.Last()));
    SoloCombatFollowups.Execute(j,convert);
    Check(j.Context.MovementPool==0&&j.Context.RangedPool==2
        &&!SoloCombatFollowups.Legal(j).Any(x=>x.Kind==SoloCombatFollowupKind.MoveToRanged),
        "Actual agility conversion pays four movement for two ranged power and cannot repeat for free");
    j.Battle.Advance();
    int diplomacySlot=p.Deck.Hand.FindIndex(c=>c.Id=="advanced_card_018");
    SoloDeedInputs.Execute(j,new(diplomacySlot,"strong_ice"));
    var blockConversions=SoloCombatFollowups.Legal(j).Where(x=>x.Kind==SoloCombatFollowupKind.InfluenceToBlock).ToArray();
    Check(blockConversions.Select(x=>x.Amount).SequenceEqual(new[]{1,2,3,4})&&j.Context.InfluencePool==4,
        "Native diplomacy exposes every affordable influence conversion amount");
    SoloCombatFollowups.Execute(j,blockConversions.Last());
    Check(j.Context.InfluencePool==0&&j.Context.CombatPower.Blocks.Single().Value==4
        &&j.Context.CombatPower.Blocks.Single().Element==Element.Ice,
        "Diplomacy consumes influence and preserves chosen ice block element");
}
Console.WriteLine($"PASS {checks} including original-card resource conversions; full scenario remains pending.");
int bindingArg=Array.IndexOf(args,"--binding-fixtures");
if(bindingArg>=0)File.WriteAllText(args[bindingArg+1],System.Text.Json.JsonSerializer.Serialize(bindingFixtures));
