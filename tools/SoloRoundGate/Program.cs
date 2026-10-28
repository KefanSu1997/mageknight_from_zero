using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

int checks = 0;
var lifecycleBindings=new System.Collections.Generic.List<System.Text.Json.JsonElement>();
void CaptureLifecycle(SoloConquestEpisode e,string kind,object commands)
{
    if(args.Contains("--binding-fixtures"))
        lifecycleBindings.Add(System.Text.Json.JsonSerializer.SerializeToElement(new {kind,observation=e.Observe(),commands}));
}
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
void Reject(Action operation, string message)
{
    bool rejected = false;
    try { operation(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected,message);
}
var p = new PlayerState(0,"native-round-gate");
p.Deck.SetDeck(Enumerable.Range(0,16).Select(i=>new DeedCard("basic_fixture_"+i,CardType.Action)));
var game = new GameEngine(new[]{p},3,manaRandom:new Random(1),roundShuffleRandom:new Random(2));
Reject(game.AnnounceSoloDummyRoundEnd,"No announcement before a round exists");
for (int round=1;round<=6;round++)
{
    game.StartNewRound(new[]{OriginalTactics.Get(round%2==1?"day_1":"night_1").Card});
    Check(game.Clock.RoundIndex==round && !game.CheckEndOfScenario(),"Correct live round");
    var context=new ActionContext();
    game.BeginNextTurn(context);
    Reject(game.AnnounceSoloDummyRoundEnd,"Dummy cannot interrupt the player's active turn");
    game.EndCurrentTurn(context,new[]{p.Deck.Hand[0]});
    game.AnnounceSoloDummyRoundEnd();
    Check(game.ClockTracker.CountdownStarted && !game.ClockTracker.RoundEnded,"Exactly one final turn remains");
    Reject(game.AnnounceSoloDummyRoundEnd,"No duplicate announcement");
    Reject(game.EndRound,"Cannot skip player's final turn");
    var final=new ActionContext();
    game.BeginNextTurn(final);
    game.EndCurrentTurn(final,new[]{p.Deck.Hand[0]});
    Check(game.ClockTracker.RoundEnded,"Actual native end-turn consumes countdown");
    Reject(()=>game.BeginNextTurn(new ActionContext()),"No extra turn after countdown");
    game.EndRound();
}
Check(game.CheckEndOfScenario(),"Six rounds exhaust three days");
Reject(game.PrepareNewRound,"No seventh-round refresh");
Console.WriteLine($"PASS {checks} native round-boundary checks. Dummy deck scheduling and used-tactic removal are not covered by this fixture.");
var q = new PlayerState(0,"initiative-gate");
q.Deck.SetDeck(Enumerable.Range(0,16).Select(i=>new DeedCard("basic_fixture_"+i,CardType.Action)));
var engine = new GameEngine(new[]{q},3,manaRandom:new Random(3),roundShuffleRandom:new Random(4));
int dummyCalls=0;
var solo = new SoloTacticRound(engine,()=>++dummyCalls==3,new Random(5));
solo.Start(OriginalTactics.Get("day_6").Card);
Check(solo.DummyDue,"Lower dummy tactic acts first");
Reject(()=>solo.BeginPlayerTurn(new ActionContext()),"Cannot jump ahead of dummy");
solo.AdvanceDummy();
var extraContext = new ActionContext();
solo.BeginPlayerTurn(extraContext);
engine.UseRightMoment();
solo.EndPlayerTurn(extraContext,new[]{q.Deck.Hand[0]});
Check(!solo.DummyDue && dummyCalls==1,"Earned immediate turn does not advance dummy");
var extra = new ActionContext();
solo.BeginPlayerTurn(extra);
solo.EndPlayerTurn(extra,new[]{q.Deck.Hand[0]});
Check(solo.DummyDue,"Dummy follows completion of immediate turn");
solo.AdvanceDummy();
var ordinary = new ActionContext();
solo.BeginPlayerTurn(ordinary);
solo.EndPlayerTurn(ordinary,new[]{q.Deck.Hand[0]});
solo.AdvanceDummy();
Check(engine.ClockTracker.CountdownStarted && dummyCalls==3,"Scheduled empty-deck callback announces once");
Reject(solo.AdvanceDummy,"No extra dummy draw after announcement");
var last = new ActionContext();
solo.BeginPlayerTurn(last);
solo.EndPlayerTurn(last,new[]{q.Deck.Hand[0]});
var playerTactic=solo.PlayerTactic.Id;
var dummyTactic=solo.DummyTactic.Id;
solo.EndRound();
Check(solo.RemovedTactics.Contains(playerTactic) && solo.RemovedTactics.Contains(dummyTactic),"Both used tactics removed");
Check(solo.AvailableTactics().All(t=>t.Part==DayPart.Night),"Night uses its own remaining tactic pool");
Console.WriteLine($"PASS {checks} total native checks including dummy initiative, extra turn scheduling and used tactic removal; callback fixture is not a full dummy deck/scenario.");
var redClock = new SoloDummyDeck(Enumerable.Range(0,16).Select(i=>new SoloDummyCard("r"+i,"red_fixture",ManaColor.Red)),new[]{3,0,0,0},71);
Check(!redClock.TakeTurn() && redClock.Remaining==10,"First three plus three matching crystals");
var restored=SoloDummyDeck.Restore(redClock.Capture());
for(int i=0;i<3;i++)
{
    Check(redClock.TakeTurn()==restored.TakeTurn(),"Restored announcement matches");
    Check(redClock.Capture().Draw.SequenceEqual(restored.Capture().Draw),"Restored draw order matches");
    Check(redClock.Capture().Discard.SequenceEqual(restored.Capture().Discard),"Restored discard matches");
}
Check(redClock.Announced && redClock.Remaining==0,"Exhaustion announced on next turn");
redClock.CloseRound(); restored.CloseRound();
var gained=new SoloDummyCard("advanced-physical","advanced_card_000",ManaColor.Red);
redClock.BeginNextRound(gained,ManaColor.Red);restored.BeginNextRound(gained,ManaColor.Red);
Check(redClock.Capture().Draw.SequenceEqual(restored.Capture().Draw),"Saved RNG preserves next round shuffle");
Check(redClock.Observe().Crystals[0]==4 && redClock.Remaining==17,"Dummy crystals exceed normal player cap; deck grows");
var publicCopy=redClock.Observe();publicCopy.Crystals[0]=99;
Check(redClock.Observe().Crystals[0]==4,"Public snapshots cannot mutate clock");

CardJsonLoader.SetBaseDirectory(System.IO.Path.GetFullPath("resources/text_json"));
var originalCards=CardJsonLoader.LoadBasicActions().ToDictionary(c=>c.Id);
var goldyx=OriginalHeroes.Get("Goldyx");
var actualDummy=new SoloDummyDeck(goldyx.StartingCards.Select((id,i)=>
    new SoloDummyCard("Goldyx/"+i,id,originalCards[id].RequiredCrystals.Single())),new[]{0,1,2,0},117);
var actualPlayer=OriginalHeroes.Get("Tovak").CreatePlayer(originalCards.Values,new Random(118));
var actualEngine=new GameEngine(new[]{actualPlayer},3,manaRandom:new Random(119));
var actualOrder=new SoloTacticRound(actualEngine,actualDummy.TakeTurn,new Random(120));
actualOrder.Start(OriginalTactics.Get("day_1").Card);
int turns=0;
while(!actualEngine.ClockTracker.RoundEnded)
{
    if(actualOrder.DummyDue){actualOrder.AdvanceDummy();continue;}
    var c=new ActionContext();actualOrder.BeginPlayerTurn(c);
    actualOrder.EndPlayerTurn(c,new[]{actualPlayer.Deck.Hand[0]});
    if(++turns>16)throw new Exception("Dummy failed to bound actual native turns");
}
Check(actualDummy.Announced && actualDummy.Remaining==0,"Real hero dummy deck actually ended the native round");
Reject(()=>actualOrder.EndRound(actualDummy,_=>ManaColor.Red),"Missing offers rejected before changing round or dummy");
Check(actualEngine.Clock.RoundIndex==1 && !actualDummy.Closed,"Rejected transfer preserves clock");
var advancedDefinitions=CardJsonLoader.LoadAdvancedActions().Take(6).ToArray();
var spellDefinitions=CardJsonLoader.LoadSpells().Take(6).ToArray();
actualEngine.Globals.AdvActions.SetDeck(advancedDefinitions.Select(c=>new AdvActionCard(c.Id)));
actualEngine.Globals.Spells.SetDeck(spellDefinitions.Select(c=>new SpellCard(c.Id)));
actualEngine.Globals.AdvActions.FillVacancies();actualEngine.Globals.Spells.FillVacancies();
var oldAdvanced=actualEngine.Globals.AdvActions.Offer.ToArray();
var oldSpells=actualEngine.Globals.Spells.Offer.ToArray();
ManaColor PrintedColor(string id) => id.StartsWith("advanced_card_")
    ? advancedDefinitions.Single(c=>c.Id==id).RequiredCrystals.Single()
    : spellDefinitions.Single(c=>c.Id==id).ManaColor;
var oldCrystals=actualDummy.Observe().Crystals;
actualOrder.EndRound(actualDummy,PrintedColor);
Check(actualEngine.Clock.RoundIndex==2,"Actual hero cards advanced to night through native lifecycle");
Check(actualDummy.Round==2 && actualDummy.Remaining==17,"Actual bottom advanced card grows next dummy deck");
Check(actualDummy.Capture().Cards.Count(c=>c.DefinitionId==oldAdvanced[0].Id)==1,"Bottom advanced transferred once");
Check(ReferenceEquals(actualEngine.Globals.AdvActions.Offer[0],oldAdvanced[1]),"No double advanced offer rotation");
Check(actualEngine.Globals.AdvActions.DiscardPile.Count==0,"Dummy advanced not also returned to supply");
Check(ReferenceEquals(actualEngine.Globals.Spells.Offer[0],oldSpells[1])
    && actualEngine.Globals.Spells.DiscardPile.Count==1
    && ReferenceEquals(actualEngine.Globals.Spells.DiscardPile[0],oldSpells[0]),"Exactly one normal spell rotation");
int colorIndex=(int)PrintedColor(oldSpells[0].Id);
Check(actualDummy.Observe().Crystals[colorIndex]==oldCrystals[colorIndex]+1,"Actual old bottom spell determines crystal");
Reject(()=>actualOrder.EndRound(actualDummy,PrintedColor),"No repeated supply transfer");
Console.WriteLine($"PASS {checks} checks including original hero one-round native integration and actual supply transfer. Full world/scenario restore still pending.");
// Continue the SAME native game: no reset, no replacement player or dummy deck.
for(int round=2;round<=6;round++)
{
    var available=actualOrder.AvailableTactics();
    Check(available.Count==6-2*((round-1)/2),"Exactly two tactics of this day part removed per previous occurrence");
    var pick=available.OrderBy(t=>t.Id is "day_2" or "day_3" or "night_5" or "night_6" ? 1:0).ThenBy(t=>t.Number).First();
    actualOrder.Start(pick);
    if(actualEngine.HasPendingTacticSelection)
    {
        if(pick.Id=="day_2")actualEngine.ResolveTacticSelection(actualPlayer,Array.Empty<DeedCard>());
        else if(pick.Id=="day_3")actualEngine.ResolveTacticSelection(actualPlayer,die:actualEngine.Globals.Mana.Dice.First());
        else if(pick.Id=="night_5")actualEngine.ResolveTacticSelection(actualPlayer,new[]{actualPlayer.Deck.DrawPile.First()});
        else throw new Exception("Unimplemented tactic selection in gate");
    }
    int roundTurns=0;
    while(!actualEngine.ClockTracker.RoundEnded)
    {
        if(actualOrder.DummyDue){actualOrder.AdvanceDummy();continue;}
        if(pick.Id=="night_6" && !actualPlayer.Tactic.Spent)actualEngine.ResolveSparingPower(true);
        var context=new ActionContext();actualOrder.BeginPlayerTurn(context);
        actualOrder.EndPlayerTurn(context,new[]{actualPlayer.Deck.Hand[0]});
        if(++roundTurns>30)throw new Exception("Continuous native dummy clock failed to terminate round");
    }
    Check(actualDummy.Announced && actualDummy.Remaining==0,"Continuous round ends by actual dummy exhaustion");
    var before=actualDummy.Capture();
    actualOrder.EndRound(actualDummy,PrintedColor);
    Check(actualOrder.RemovedTactics.Count==round*2,"No reused tactic across continuous rounds");
    if(round<6)
        Check(actualDummy.Round==round+1 && actualDummy.Remaining==16+round,"Exactly one advanced card added at actual round boundary");
    else
        Check(actualDummy.Closed && actualDummy.Round==6 && actualDummy.Capture().Cards.SequenceEqual(before.Cards),"Final round closes without inventing seventh-round dummy upgrade");
}
Check(actualEngine.CheckEndOfScenario() && actualEngine.Clock.RoundIndex==7,"Native six-round time limit reached without resetting state");
Reject(()=>actualOrder.Start(OriginalTactics.Get("day_1").Card),"Cannot restart solo game after time limit");
Console.WriteLine($"PASS {checks} checks; original Tovak/Goldyx continuous six-round clock, real offers and all twelve removed tactics. No world exploration/combat or policy capability claim.");
var emptyPlayer=new PlayerState(0,"empty-final-turn-gate");
emptyPlayer.Deck.SetDeck(Enumerable.Range(0,16).Select(i=>new DeedCard("empty_fixture_"+i,CardType.Action)));
var emptyGame=new GameEngine(new[]{emptyPlayer},3,manaRandom:new Random(131));
int emptyDummyCalls=0;
var emptyOrder=new SoloTacticRound(emptyGame,()=>++emptyDummyCalls==4,new Random(132));
emptyOrder.Start(OriginalTactics.Get("day_1").Card);
Reject(emptyOrder.SkipEmptyFinalTurn,"Cannot skip ordinary populated turn");
for(int i=0;i<4;i++)
{
    var context=new ActionContext();emptyOrder.BeginPlayerTurn(context);
    Reject(emptyOrder.SkipEmptyFinalTurn,"Cannot skip inside active player turn");
    emptyOrder.EndPlayerTurn(context,emptyPlayer.Deck.Hand.ToArray());
    Reject(emptyOrder.SkipEmptyFinalTurn,"Cannot skip before scheduled dummy turn");
    emptyOrder.AdvanceDummy();
}
Check(emptyPlayer.Deck.Hand.Count==0 && emptyPlayer.Deck.DrawPileCount==0,"Actual end-turn discards exhausted all sixteen cards");
Check(emptyOrder.CanSkipEmptyFinalTurn,"Scheduled exhausted final player turn is explicitly available");
emptyOrder.SkipEmptyFinalTurn();
Check(emptyGame.ClockTracker.RoundEnded && !emptyOrder.DummyDue,"Empty final turn consumes countdown without another dummy draw");
Reject(emptyOrder.SkipEmptyFinalTurn,"Cannot skip final turn twice");
emptyOrder.EndRound();
Check(emptyGame.Clock.RoundIndex==2,"Empty final turn still permits normal round cleanup");
Console.WriteLine($"PASS {checks} checks including native empty-final-turn scheduling. Full world/scenario acceptance remains pending.");
foreach(bool dummyEmptyAtFinalStart in new[]{false,true})
{
    var declarer=new PlayerState(0,"player-declaration-gate");
    declarer.Deck.SetDeck(Enumerable.Range(0,16).Select(i=>new DeedCard("declaration_fixture_"+i,CardType.Action)));
    var declarationGame=new GameEngine(new[]{declarer},3,manaRandom:new Random(141));
    int calls=0;
    var declarationOrder=new SoloTacticRound(declarationGame,()=>++calls==4 && dummyEmptyAtFinalStart,new Random(142));
    declarationOrder.Start(OriginalTactics.Get("day_1").Card);
    Reject(declarationOrder.AnnounceRoundEnd,"Cannot declare with populated draw pile");
    for(int i=0;i<3;i++)
    {
        var c=new ActionContext();declarationOrder.BeginPlayerTurn(c);
        Reject(declarationOrder.AnnounceRoundEnd,"Cannot declare after starting own turn");
        declarationOrder.EndPlayerTurn(c,declarer.Deck.Hand.ToArray());
        Reject(declarationOrder.AnnounceRoundEnd,"Cannot declare ahead of dummy");
        declarationOrder.AdvanceDummy();
    }
    Check(declarer.Deck.DrawPileCount==0 && declarer.Deck.Hand.Count==1,"Actual discard/draw exhausted draw pile while preserving last hand card");
    Check(declarationOrder.CanAnnounceRoundEnd,"Player may choose declaration rather than play remaining hand");
    declarationOrder.AnnounceRoundEnd();
    Check(!declarationGame.ClockTracker.RoundEnded && declarationOrder.DummyDue,"Dummy gets one final turn after player declaration");
    Reject(()=>declarationOrder.BeginPlayerTurn(new ActionContext()),"Declarer cannot take another turn");
    Reject(declarationOrder.EndRound,"Cannot finish before dummy final turn");
    declarationOrder.AdvanceDummy();
    Check(calls==4 && declarationGame.ClockTracker.RoundEnded,"Exactly one dummy final turn, even if empty at its start");
    Reject(declarationOrder.AdvanceDummy,"No duplicate final dummy turn");
    Reject(declarationOrder.AnnounceRoundEnd,"No second declaration");
    declarationOrder.EndRound();
    Check(declarationGame.Clock.RoundIndex==2,"Player declaration completes native round");
}
Console.WriteLine($"PASS {checks} checks including player-declared round end; not full exploration/combat acceptance.");
foreach(int endingCase in new[]{0,1,2}) // ordinary, exhausted hand/deck, already-final round turn
{
    var winner=new PlayerState(0,"city-boundary-fixture");
    winner.Deck.SetDeck(Enumerable.Range(0,endingCase==1?1:16).Select(i=>new DeedCard("city_fixture_"+i,CardType.Action)));
    var cityGame=new GameEngine(new[]{winner},3,manaRandom:new Random(151));
    int cityDummyCalls=0;
    var cityOrder=new SoloTacticRound(cityGame,()=>++cityDummyCalls==1,new Random(152));
    var city1=new PlaceState(PlaceType.City);
    var city2=new PlaceState(PlaceType.City);
    cityGame.Map.Places.Add(new MK.Logic.Runtime.Map.AxialCoord(0,0),city1);
    cityGame.Map.Places.Add(new MK.Logic.Runtime.Map.AxialCoord(1,0),city2);
    Check(!cityGame.SoloCitiesConquered,"Empty site initialization cannot forge conquest");
    cityOrder.Start(OriginalTactics.Get(endingCase==2?"day_6":"day_1").Card);
    if(endingCase==2)cityOrder.AdvanceDummy();
    var c=new ActionContext();cityOrder.BeginPlayerTurn(c);
    // Boundary fixture injects the post-combat ownership ledger; this is not a combat test.
    city1.OwnerId=winner.Id;city1.CityShields.Add(winner.Id);
    Check(!cityGame.SoloCitiesConquered,"One conquered city is not victory");
    city2.OwnerId=winner.Id;city2.CityShields.Add(winner.Id);
    Check(cityGame.SoloCitiesConquered,"Both actual ownership/shield ledgers required");
    cityOrder.EndPlayerTurn(c,winner.Deck.Hand.ToArray());
    Reject(cityOrder.AdvanceDummy,"No dummy turn after city victory");
    if(endingCase==0)
    {
        Check(cityOrder.CityFinalTurnPending && !cityOrder.ScenarioEnded,"Exactly one further human turn after conquest");
        var finalContext=new ActionContext();cityOrder.BeginPlayerTurn(finalContext);
        cityOrder.EndPlayerTurn(finalContext,winner.Deck.Hand.ToArray());
    }
    else if(endingCase==1)
    {
        Check(cityOrder.CanSkipEmptyFinalTurn,"No impossible empty final city turn");
        cityOrder.SkipEmptyFinalTurn();
    }
    Check(cityOrder.ScenarioEnded,"City-ending case terminates at actual turn boundary");
    Check(cityGame.Clock.RoundIndex==1,"City victory preserves scoring round; no extra supply refresh");
    Reject(()=>cityOrder.BeginPlayerTurn(new ActionContext()),"No post-terminal fame farming");
    Reject(cityOrder.EndRound,"No post-terminal round refresh");
    Reject(()=>cityOrder.Start(OriginalTactics.Get("night_1").Card),"No next round after city victory");
}
Console.WriteLine($"PASS {checks} checks including city-ending boundary fixtures; actual city combat and whole-scenario restore remain pending.");
// Exercise the actual journey API, not a second clock calling GameEngine alongside it.
var journeyPlayer=OriginalHeroes.Get("Tovak").CreatePlayer(originalCards.Values,new Random(161));
var journey=new MK.Logic.Runtime.Battles.PveJourneySession(journeyPlayer,
    originalCards.Values.Concat(advancedDefinitions),spells:spellDefinitions,
    manaRandom:new Random(162),supplyRandom:new Random(163),roundShuffleRandom:new Random(164));
var journeyDummy=new SoloDummyDeck(goldyx.StartingCards.Select((id,i)=>
    new SoloDummyCard("Goldyx/"+i,id,originalCards[id].RequiredCrystals.Single())),new[]{0,1,2,0},165);
journey.EnableSoloClock(journeyDummy,PrintedColor,new Random(166));
Reject(()=>journey.EnableSoloClock(journeyDummy,PrintedColor,new Random(167)),"No replacement clock resets dummy timing");
for(int round=1;round<=6;round++)
{
    var tactic=journey.SoloClock.AvailableTactics()
        .OrderBy(t=>t.Id is "day_2" or "day_3" or "night_5" or "night_6"?1:0).ThenBy(t=>t.Number).First();
    journey.SelectTactic(tactic.Id);
    if(journey.Game.HasPendingTacticSelection)
    {
        if(tactic.Id=="day_2")journey.Game.ResolveTacticSelection(journeyPlayer,Array.Empty<DeedCard>());
        else if(tactic.Id=="day_3")journey.Game.ResolveTacticSelection(journeyPlayer,die:journey.Game.Globals.Mana.Dice.First());
        else if(tactic.Id=="night_5")journey.Game.ResolveTacticSelection(journeyPlayer,new[]{journeyPlayer.Deck.DrawPile.First()});
        else throw new Exception("Unhandled actual journey tactic");
    }
    int safety=0;
    while(!journey.Game.ClockTracker.RoundEnded)
    {
        if(++safety>60)throw new Exception("Journey clock did not terminate");
        if(journey.SoloClock.DummyDue)
        {
            Reject(journey.BeginTurn,"Journey API cannot bypass scheduled dummy");
            var snapshot=MK.Logic.Runtime.Battles.DemoStateCheckpoint.Capture(journey);
            journey.SoloClock.AdvanceDummy();
            var expected=journeyDummy.Capture();
            bool expectedDue=journey.SoloClock.DummyDue;
            snapshot.Restore();
            Check(journey.SoloClock.DummyDue,"In-memory restore includes scheduler");
            journey.SoloClock.AdvanceDummy();
            var replay=journeyDummy.Capture();
            Check(replay.Draw.SequenceEqual(expected.Draw) && replay.Discard.SequenceEqual(expected.Discard)
                && replay.RandomState==expected.RandomState && replay.Announced==expected.Announced
                && journey.SoloClock.DummyDue==expectedDue,"Journey dummy replay preserves exact clock and physical cards");
            continue;
        }
        if(journey.SoloClock.CanSkipEmptyFinalTurn){journey.SoloClock.SkipEmptyFinalTurn();continue;}
        if(tactic.Id=="night_6" && !journeyPlayer.Tactic.Spent)journey.Game.ResolveSparingPower(true);
        journey.BeginTurn();
        journey.EndTurn(new[]{journeyPlayer.Deck.Hand[0]});
    }
    journey.EndRound();
    Check(journey.Game.Clock.RoundIndex==round+1,"Journey native round transfer advances once");
}
Check(journey.SoloClock.ScenarioEnded && journey.CompletedTurns>0,"Actual journey API reaches six-round terminal");
Reject(journey.BeginTurn,"Journey API rejects post-terminal player turn");
Reject(journey.EndRound,"Journey API rejects post-terminal refresh");
Console.WriteLine($"PASS {checks} checks including journey-integrated six rounds and in-memory dummy replay; durable whole-world checkpoint and full combat scenario remain pending.");
var skillPlayer=OriginalHeroes.Get("Tovak").CreatePlayer(originalCards.Values,new Random(171));
var skillJourney=new MK.Logic.Runtime.Battles.PveJourneySession(skillPlayer,
    originalCards.Values.Concat(advancedDefinitions),spells:spellDefinitions,
    originalSkills:CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Tovak").SkillPoolId),
    manaRandom:new Random(172),supplyRandom:new Random(173),progressionRandom:new Random(174));
var skillDummy=new SoloDummyDeck(goldyx.StartingCards.Select((id,i)=>
    new SoloDummyCard("Goldyx/"+i,id,originalCards[id].RequiredCrystals.Single())),new[]{0,1,2,0},175);
skillJourney.EnableSoloClock(skillDummy,PrintedColor,new Random(176));
var dummySkills=CardJsonLoader.LoadHeroSkills(goldyx.SkillPoolId);
skillJourney.EnableSoloDummySkills("Goldyx",dummySkills,new Random(177));
Reject(()=>skillJourney.EnableSoloDummySkills("Goldyx",dummySkills,new Random(178)),"Cannot reshuffle dummy skills after setup");
skillJourney.SelectTactic("day_1");
skillJourney.BeginTurn();
// Level fixture injects fame only; does not claim a combat-earned victory.
skillPlayer.Fame=15;
skillJourney.EndTurn(new[]{skillPlayer.Deck.Hand[0]});
Check(skillJourney.Progression.Pending && skillJourney.Progression.CommonSkills.Count==0,"No dummy skill available before first acquisition");
var beforeSkill=MK.Logic.Runtime.Battles.DemoStateCheckpoint.Capture(skillJourney);
var ownSkill=skillJourney.Progression.Choices.First();
var ownAction=skillJourney.Progression.Actions.Last();
skillJourney.ChooseLevelReward(ownSkill,ownAction);
var revealed=skillJourney.Progression.CommonSkills.Single(s=>s.SourceId.StartsWith(goldyx.SkillPoolId+":"));
Check(revealed.IsInteractive==false && skillJourney.Progression.Pending,"Dummy noninteractive skill is offered for subsequent pending level");
beforeSkill.Restore();
Check(skillJourney.Progression.CommonSkills.Count==0,"Restore retracts revealed dummy skill");
skillJourney.ChooseLevelReward(ownSkill,ownAction);
Check(skillJourney.Progression.CommonSkills.Any(s=>ReferenceEquals(s,revealed)),"Restore repeats identical physical dummy skill");
Reject(()=>skillJourney.ChooseLevelReward(revealed,skillJourney.Progression.Actions.Last()),"Common skill requires bottom advanced action");
skillJourney.ChooseLevelReward(revealed,skillJourney.Progression.Actions[0]);
Check(skillPlayer.Skills.Any(s=>ReferenceEquals(s,revealed)) && skillPlayer.Level==4,"Actual common skill acquired at following level");
var nextDummy=skillJourney.Progression.CommonSkills.Where(s=>s.SourceId.StartsWith(goldyx.SkillPoolId+":")).ToArray();
Check(nextDummy.Length==1 && !ReferenceEquals(nextDummy[0],revealed) && nextDummy[0].IsInteractive==false,"One new dummy skill per acquisition, no repeat or interactive token");
Check(skillJourney.CompletedTurns==1 && !skillJourney.Game.IsEndingTurn,"Split level reward completes and schedules journey turn exactly once");
Console.WriteLine($"PASS {checks} checks including native dummy skill offer timing and replay; injected fame fixture, not full-scenario policy acceptance.");
var scoreIds=advancedDefinitions.Select(c=>c.Id).ToHashSet();
var timeEndedScore=journey.CaptureSoloScore(scoreIds);
Check(timeEndedScore.Terminal && timeEndedScore.EarlyRounds==0 && timeEndedScore.DummyCards==0,
    "Sixth-round cleanup cannot invent an unused seventh round or redraw dummy cards for score");
Check(timeEndedScore.NoAnnouncement==5,"Dummy declaration is not a player declaration");
var ledger=new SoloScoreRewardLedger(skillJourney.CaptureSoloScore(scoreIds));
int nativeFame=skillPlayer.Fame;
var scoringWound=new DeedCard("wound",CardType.Wound);
skillPlayer.Deck.GainToDiscard(scoringWound);
double damageReward=ledger.Advance(skillJourney.CaptureSoloScore(scoreIds));
skillPlayer.Deck.TryRemoveFromDiscardExact(scoringWound);
double healingReward=ledger.Advance(skillJourney.CaptureSoloScore(scoreIds));
Check(Math.Abs(damageReward+0.02)<1e-12 && Math.Abs(damageReward+healingReward)<1e-12,"Real wound/heal inventory cycle yields zero net reward");
Check(ledger.Advance(skillJourney.CaptureSoloScore(scoreIds))==0 && skillPlayer.Fame==nativeFame,
    "Repeated observation gives no reward and scoring never changes native fame");
// Conquest ledger fixture, separated explicitly from actual city-combat acceptance.
var rewardStart=skillJourney.CaptureSoloScore(scoreIds);
var terminalLedger=new SoloScoreRewardLedger(rewardStart);
for(int i=0;i<2;i++)
{
    var city=new PlaceState(PlaceType.City){OwnerId=skillPlayer.Id};city.CityShields.Add(skillPlayer.Id);
    skillJourney.Game.Map.Places.Add(new MK.Logic.Runtime.Map.AxialCoord(i,0),city);
}
double rewardSum=terminalLedger.Advance(skillJourney.CaptureSoloScore(scoreIds));
Check(Math.Abs(rewardSum-0.35)<1e-12,"Two distinct conquered cities score 20+15, not per shield");
while(!skillJourney.SoloClock.ScenarioEnded)
{
    if(skillJourney.SoloClock.DummyDue){skillJourney.SoloClock.AdvanceDummy();continue;}
    skillJourney.BeginTurn();skillJourney.EndTurn(new[]{skillPlayer.Deck.Hand[0]});
}
var terminalScore=skillJourney.CaptureSoloScore(scoreIds);
rewardSum+=terminalLedger.Advance(terminalScore);
Check(terminalScore.EarlyRounds==150 && terminalScore.NoAnnouncement==5 && terminalScore.Terminal,
    "First-round conquest awards only five unused rounds at actual final turn completion");
Check(Math.Abs(rewardSum-0.01*(terminalScore.Total-rewardStart.Total))<1e-12,
    "Accumulated real-state rewards reconcile exactly with final minus initial score");
Reject(()=>terminalLedger.Advance(terminalScore),"Final bonus cannot be paid twice");
Console.WriteLine($"PASS {checks} including journey-derived terminal score and native wound-cycle reward reconciliation; city ownership fixture, not a full played conquest.");
foreach(var hero in OriginalHeroes.All)
{
    var heroPlayer=hero.CreatePlayer(originalCards.Values,new Random(181));
    var soloSetup=new MK.Logic.Runtime.Battles.PveJourneySession(heroPlayer,
        originalCards.Values.Concat(CardJsonLoader.LoadAdvancedActions()),spells:CardJsonLoader.LoadSpells(),
        originalSkills:CardJsonLoader.LoadHeroSkills(hero.SkillPoolId),manaRandom:new Random(182),
        supplyRandom:new Random(183),progressionRandom:new Random(184),soloConquest:true);
    var restoreSupply=MK.Logic.Runtime.Battles.DemoStateCheckpoint.Capture(soloSetup);
    var spellIds=new System.Collections.Generic.List<string>();
    while(soloSetup.Game.Globals.Spells.Offer.Count>0)spellIds.Add(soloSetup.Game.Globals.Spells.Claim(0).Id);
    Check(spellIds.Count==20 && spellIds.Distinct().Count()==20
        && !spellIds.Intersect(new[]{"magic_000","magic_006","magic_012","magic_018"}).Any(),
        "soloSetup physical spell supply removes exactly original109--112");
    restoreSupply.Restore();
    soloSetup.SelectTactic("day_1");soloSetup.BeginTurn();
    heroPlayer.Fame=99; // progression coverage fixture only, not earned game score
    soloSetup.EndTurn(new[]{heroPlayer.Deck.Hand[0]});
    int levels=0;
    while(soloSetup.Progression.Pending)
    {
        Check(soloSetup.Progression.Choices.All(s=>s.IsInteractive==false),"Own interactive skill never enters soloSetup choice");
        Check(soloSetup.Progression.Choices.Count==(levels==4?1:2),"Nine retained own skills are consumed without inventing a tenth token");
        soloSetup.ChooseLevelReward(soloSetup.Progression.Choices[0],soloSetup.Progression.Actions[0]);
        if(++levels>5)throw new Exception("Repeated level rewards");
    }
    Check(heroPlayer.Level==10 && heroPlayer.Skills.Count==5 && soloSetup.CompletedTurns==1,
        "All seven heroes complete five skill rewards with exact physical skill inventory");
}
Console.WriteLine($"PASS {checks} checks including seven-hero soloSetup supply exclusions and level10 progression fixtures; full scenario remains pending.");
var episodeCatalog=PrintedTileCatalog.Load(new[]{"tutorial_opening.json","countryside_3_11_transcription.json","core_1_8_transcription.json"}
    .Select(f=>System.IO.Path.Combine("resources/original_maps",f)).ToArray());
ulong fixtureSeed=1;
// Only this lifecycle fixture avoids an unverified ruin inventory; never a training-world filter.
while(fixtureSeed<1000)
{
    var probe=SoloConquestTileSupply.Create(episodeCatalog,fixtureSeed);
    if(Enumerable.Range(0,2).Select(_=>probe.Draw()).All(t=>t.Cells.All(c=>c.Site!="AncientRuin")))break;
    fixtureSeed++;
}
Check(fixtureSeed<1000,"Explicit non-ruin lifecycle fixture selected");
var episodeTiles=SoloConquestTileSupply.Create(episodeCatalog,fixtureSeed);
var episodePlayer=OriginalHeroes.Get("Tovak").CreatePlayer(originalCards.Values,new Random(191));
var episodeAdvanced=CardJsonLoader.LoadAdvancedActions().ToArray();
var episodeJourney=new PveJourneySession(episodePlayer,
    originalCards.Values.Concat(episodeAdvanced).Concat(CardJsonLoader.LoadSpells()
        .Select(s=>new MK.Logic.Data.Cards.ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost))),
    artifacts:CardJsonLoader.LoadItems(),spells:CardJsonLoader.LoadSpells(),
    originalSkills:CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Tovak").SkillPoolId),
    enemySupply:new EnemyTokenSupply(CardJsonLoader.LoadMonsters(),new Random(192)),
    manaRandom:new Random(193),supplyRandom:new Random(194),roundShuffleRandom:new Random(195),
    progressionRandom:new Random(196),soloConquest:true,
    unitDeck:UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit));
var episodeDummy=new SoloDummyDeck(goldyx.StartingCards.Select((id,i)=>
    new SoloDummyCard("Goldyx/"+i,id,originalCards[id].RequiredCrystals.Single())),new[]{0,1,2,0},197,
    SoloDummyDualColorRule.MaximumMatchingCrystals); // explicit gate setting, not a production-rule selection
var episodeColors=episodeAdvanced.Where(c=>!string.IsNullOrWhiteSpace(c.BaseEffect)).ToDictionary(c=>c.Id,c=>c.RequiredCrystals.ToArray());
foreach(var spell in CardJsonLoader.LoadSpells())episodeColors.Add(spell.Id,new[]{spell.ManaColor});
episodeJourney.EnableSoloClock(episodeDummy,SoloEpisodeFactory.BindPrintedColors(episodeColors),new Random(198));
episodeJourney.EnableSoloDummySkills("Goldyx",dummySkills,new Random(199));
episodeJourney.InitializeSoloOriginalMap(episodeCatalog,episodeTiles);
var episodeIds=CardJsonLoader.LoadAdvancedActions().Select(c=>c.Id).ToHashSet();
var episode=new SoloConquestEpisode(episodeJourney,episodeTiles,episodeIds);
var initialPolicyObservation=episode.Observe();
var observationJson=System.Text.Json.JsonSerializer.Serialize(initialPolicyObservation);
var observationRoundtrip=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyObservation>(observationJson);
Check(observationRoundtrip.Version==1&&observationRoundtrip.Step==0&&!observationRoundtrip.Terminal
    &&observationRoundtrip.World.Cells.Length==21&&observationRoundtrip.Inventory.Hero=="Tovak"
    &&!observationRoundtrip.Combat.Active,
    "Typed combined observation serializes the actual opening world and hero without a synthetic battle");
Check(episode.Steps==0&&observationJson==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
    "Repeated combined observation is stable and does not advance the scenario");
var envelopeCheckpoint=DemoStateCheckpoint.Capture(episode);
var frozenSupport=SoloDecisionSupport.Capture(episode);
Check(frozenSupport.CommandsJson.Count==6&&frozenSupport.Step==0&&frozenSupport.Sha256.Length==64,
    "Immutable opening support archives all six actual tactic commands and observation digest");
Check(frozenSupport.Sha256==SoloDecisionSupport.Capture(episode).Sha256,
    "Repeated support capture preserves exact serialized commands and boundary digest");
bool immutableSupport=false;
try{((System.Collections.Generic.IList<string>)frozenSupport.CommandsJson)[0]="corrupt";}
catch(NotSupportedException){immutableSupport=true;}
Check(immutableSupport,"Archived command strings cannot be changed through list mutation");
var firstRecorded=frozenSupport.ExecuteRecorded(episode,0);
Check(firstRecorded.BeforeStep==0&&firstRecorded.Transition.Index==1&&firstRecorded.CommandsJson.Count==6
    &&firstRecorded.BehaviorLogProbability==null&&firstRecorded.ValuePrediction==null
    &&System.Text.Json.JsonSerializer.Deserialize<SoloPolicyObservation>(firstRecorded.ObservationAfter).Step==episode.Steps,
    "Recorded real transition binds full support and before/after observation without fabricated policy statistics");
Check(firstRecorded.SupportSeconds>=0&&firstRecorded.ExecuteAndObserveSeconds>=0,
    "Decision timing separates actual support enumeration and execution/observation");
var recordedRoundtrip=System.Text.Json.JsonSerializer.Deserialize<SoloTrajectoryDecision>(System.Text.Json.JsonSerializer.Serialize(firstRecorded));
Check(firstRecorded.Version==3&&firstRecorded.ScoreBefore.Total==firstRecorded.InitialScore
    &&Math.Abs(firstRecorded.Transition.Reward-firstRecorded.RewardScale*
        (firstRecorded.Transition.Score.Total-firstRecorded.ScoreBefore.Total))<1e-12,
    "Recorded accounting uses actual pre/post native scores and reward scale");
var nonzeroScore=new MK.Logic.Runtime.SoloInventoryScore(15,2,3,1,5,4,2);
var scoreRoundtrip=System.Text.Json.JsonSerializer.Deserialize<MK.Logic.Runtime.SoloInventoryScore>(
    System.Text.Json.JsonSerializer.Serialize(nonzeroScore));
Check(scoreRoundtrip.Total==nonzeroScore.Total&&scoreRoundtrip.DeckWounds==2&&scoreRoundtrip.Fame==15,
    "Nonzero score accounting survives trajectory JSON reload");
Check(recordedRoundtrip.SupportSha256==firstRecorded.SupportSha256
    &&recordedRoundtrip.CommandsJson[recordedRoundtrip.SelectedIndex]==frozenSupport.CommandsJson[0],
    "Trajectory JSON roundtrip retains the exact selected complete command");
int trajectoryArgument=Array.IndexOf(args,"--trajectory");
if(trajectoryArgument>=0)System.IO.File.WriteAllText(args[trajectoryArgument+1],System.Text.Json.JsonSerializer.Serialize(firstRecorded));
Reject(()=>frozenSupport.Execute(episode,0),"Consumed support rejects repeated decision execution");
envelopeCheckpoint.Restore();
SoloTrajectoryReplay.Apply(episode,new[]{recordedRoundtrip});
Check(episode.Steps==1,"Serialized journal replays the actual opening decision");
var factorySetup=new SoloEpisodeSetup(1,"Tovak","Goldyx",new[]{0,1,2,0},
    SoloDummyDualColorRule.MaximumMatchingCrystals,
    new SoloEpisodeSeeds(fixtureSeed,191,192,193,194,195,196,197,198,199,200));
var setupReload=System.Text.Json.JsonSerializer.Deserialize<SoloEpisodeSetup>(
    System.Text.Json.JsonSerializer.Serialize(factorySetup));
var rebuiltEpisode=SoloEpisodeFactory.Create(setupReload,episodeCatalog);
var rebuiltDigest=SoloStateDigest.Compute(rebuiltEpisode);
var independentlyRebuilt=SoloEpisodeFactory.Create(setupReload,episodeCatalog);
if(rebuiltDigest!=SoloStateDigest.Compute(independentlyRebuilt))
{
    using(var trace=System.IO.File.CreateText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"solo_digest_first.txt")))SoloStateDigest.Compute(rebuiltEpisode,trace);
    using(var trace=System.IO.File.CreateText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"solo_digest_second.txt")))SoloStateDigest.Compute(independentlyRebuilt,trace);
}
Check(rebuiltDigest==SoloStateDigest.Compute(independentlyRebuilt),
    "Private full graph digest matches independent factory reconstruction including RNG and aliases");
var differentHiddenRandom=SoloEpisodeFactory.Create(setupReload with {
    Seeds=setupReload.Seeds with {Effects=setupReload.Seeds.Effects+1}},episodeCatalog);
Check(System.Text.Json.JsonSerializer.Serialize(rebuiltEpisode.Observe())==System.Text.Json.JsonSerializer.Serialize(differentHiddenRandom.Observe())
    &&rebuiltDigest!=SoloStateDigest.Compute(differentHiddenRandom),
    "Private graph digest detects different hidden effect RNG despite identical public observations");
var privateBoundarySupport=SoloDecisionSupport.Capture(rebuiltEpisode);
Check(rebuiltDigest==SoloStateDigest.Compute(rebuiltEpisode),
    "Complete legal support probing preserves private graph digest");
var privateBoundary=DemoStateCheckpoint.Capture(rebuiltEpisode);
var privateObservation=System.Text.Json.JsonSerializer.Serialize(rebuiltEpisode.Observe());
var hiddenEffectRandom=(Random)typeof(SoloConquestEpisode).GetField("_effectRandom",
    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(rebuiltEpisode);
hiddenEffectRandom.Next();
Check(privateObservation==System.Text.Json.JsonSerializer.Serialize(rebuiltEpisode.Observe()),
    "Advancing hidden RNG alone leaves public observation unchanged");
Reject(()=>privateBoundarySupport.Execute(rebuiltEpisode,0),
    "Stale support rejects hidden RNG advancement even with unchanged public observation");
privateBoundary.Restore();
Check(rebuiltDigest==SoloStateDigest.Compute(rebuiltEpisode),"Restoring boundary restores hidden RNG digest");
// The manually constructed fixture has different closure/RNG identities. A private
// graph replay must use an independent reconstruction of the same factory contract.
var factoryRecorded=SoloDecisionSupport.Capture(independentlyRebuilt).ExecuteRecorded(independentlyRebuilt,0);
SoloTrajectoryReplay.Apply(rebuiltEpisode,new[]{factoryRecorded});
Check(rebuiltDigest!=SoloStateDigest.Compute(rebuiltEpisode),"Executed decision changes private graph digest");
Check(System.Text.Json.JsonSerializer.Serialize(rebuiltEpisode.Observe())==
    System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
    "Independent seed-based factory and serialized journal reproduce the actual opening decision");
Reject(()=>SoloEpisodeFactory.Create(factorySetup with {DummyHero="Tovak"},episodeCatalog),
    "Task setup rejects the player hero as dummy");
if(args.Contains("--journal-lifecycle"))
{
    var lifecycle=SoloEpisodeFactory.Create(setupReload,episodeCatalog);
    var journal=new System.Collections.Generic.List<SoloTrajectoryDecision>();
    while(!lifecycle.Terminal&&journal.Count<400)
    {
        var support=SoloDecisionSupport.Capture(lifecycle);
        var choices=support.CommandsJson.Select((json,index)=>(command:
            System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(json),index)).ToArray();
        // Deterministic clock/discard fixture, not a playing policy or success demonstration.
        var chosen=choices.OrderBy(x=>x.command.Clock!=null?0:x.command.TacticInput!=null?1:
            x.command.EndTurn!=null?2:x.command.Rest!=null?3:100)
            .ThenByDescending(x=>x.command.EndTurn?.DiscardHandSlots.Length??0).First();
        if(chosen.command.Clock==null&&chosen.command.TacticInput==null&&chosen.command.EndTurn==null&&chosen.command.Rest==null)
            throw new Exception("Lifecycle fixture cannot advance with complete clock/turn choices.");
        journal.Add(support.ExecuteRecorded(lifecycle,chosen.index));
        Console.WriteLine($"journal step={journal.Count} round={lifecycle.Journey.Game.Clock.RoundIndex} terminal={lifecycle.Terminal}");
    }
    Check(lifecycle.Terminal,"Unmodified factory lifecycle reaches native scenario termination");
    var serializedJournal=System.Text.Json.JsonSerializer.Serialize(journal);
    var restoredJournal=System.Text.Json.JsonSerializer.Deserialize<SoloTrajectoryDecision[]>(serializedJournal);
    var replayed=SoloEpisodeFactory.Create(setupReload,episodeCatalog);
    SoloTrajectoryReplay.Apply(replayed,restoredJournal);
    Check(replayed.Terminal&&replayed.Steps==lifecycle.Steps
        &&System.Text.Json.JsonSerializer.Serialize(replayed.Observe())==System.Text.Json.JsonSerializer.Serialize(lifecycle.Observe()),
        "Separate factory replays all six rounds, dummy changes and terminal accounting");
    Check(Math.Abs(journal.Sum(x=>x.Transition.Reward)-lifecycle.RewardScale*
        (journal.Last().Transition.Score.Total-lifecycle.InitialScore))<1e-10,
        "Complete native lifecycle reward telescopes after durable journal reload");
    int outputArg=Array.IndexOf(args,"--journal-output");
    if(outputArg>=0)System.IO.File.WriteAllText(args[outputArg+1],serializedJournal);
    Console.WriteLine($"PASS lifecycle journal {journal.Count} decisions; no exploration/combat capability claim.");
    return;
}
envelopeCheckpoint.Restore();
Reject(()=>SoloTrajectoryReplay.Apply(episode,new[]{recordedRoundtrip with
    {Transition=recordedRoundtrip.Transition with {Reward=1}}}),"Replay rejects fabricated reward");
Check(episode.Steps==0&&System.Text.Json.JsonSerializer.Serialize(episode.Observe())==observationJson,
    "Rejected replay restores the whole initial episode boundary");
var envelopeChoices=SoloPolicyCommand.ImplementedChoices(episode).ToArray();
Check(envelopeChoices.Length==6&&envelopeChoices.All(c=>c.Clock?.Kind==SoloClockKind.SelectTactic),
    "Unified opening support contains exactly six available tactics, no premature world actions");
var envelope=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(envelopeChoices[0]));
envelope.Execute(episode);
int envelopeStep=episode.Steps;var envelopeWorld=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles));
Reject(()=>envelope.Execute(episode),"Already consumed command rejects stale physical boundary");
Reject(()=>new SoloPolicyCommand(1,episode.Steps,Clock:new(SoloClockKind.BeginTurn),Rest:new(0,Array.Empty<int>())).Execute(episode),
    "Ambiguous multi-payload command rejected");
Reject(()=>new SoloPolicyCommand(2,episode.Steps,Clock:new(SoloClockKind.BeginTurn)).Execute(episode),
    "Unknown command schema rejected");
Check(episode.Steps==envelopeStep&&envelopeWorld==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles)),
    "Invalid envelopes leave actual world and reward boundary unchanged");
envelopeCheckpoint.Restore();
var publicWorld=episodeJourney.ObserveSoloWorld(episodeTiles);
var privateTiles=episodeTiles.Capture();
var changedHidden=(string[])privateTiles.Main.Clone();
(changedHidden[0],changedHidden[1])=(changedHidden[1],changedHidden[0]);
var alternateTiles=SoloConquestTileSupply.Restore(episodeCatalog,privateTiles with {Main=changedHidden});
Check(System.Text.Json.JsonSerializer.Serialize(publicWorld)==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(alternateTiles)),
    "Full public world branch is invariant to hidden next tile identity");
Check(publicWorld.Cells.Length==21 && publicWorld.Dummy.Remaining==16 && publicWorld.Dummy.Discard.Length==0,
    "Opening observation contains revealed map and public dummy count only");
publicWorld.Dummy.Crystals[0]=999;
Check(episodeJourney.ObserveSoloWorld(episodeTiles).Dummy.Crystals[0]!=999,"Policy observation cannot mutate dummy resources");
double episodeReturn=0;
var testedTacticInputs=new System.Collections.Generic.HashSet<string>();
var testedTacticAbilities=new System.Collections.Generic.HashSet<string>();
for(int round=1;round<=6;round++)
{
    foreach(var tactic in episodeJourney.SoloClock.AvailableTactics()
        .Where(t=>t.Id is "day_6" or "night_2" or "night_3" or "night_4" or "night_6").ToArray())
    {
        if(testedTacticAbilities.Contains(tactic.Id))continue;
        var fixture=DemoStateCheckpoint.Capture(episode);
        SoloClockCommands.Apply(episode,new(SoloClockKind.SelectTactic,tactic.Id));
        if(tactic.Id=="night_2")
        {
            Check(!SoloTacticAbilities.Legal(episode).Any(),"Long night is unavailable while draw pile is nonempty");
            episodePlayer.Deck.DrawExact(episodePlayer.Deck.DrawPileCount);
            foreach(var card in episodePlayer.Deck.Hand.Take(4).ToArray())episodePlayer.Deck.TryDiscardExact(card);
        }
        if(tactic.Id is "day_6" or "night_3")SoloClockCommands.Apply(episode,new(SoloClockKind.BeginTurn));
        var beforeAbilityHand=episodePlayer.Deck.Hand.ToArray();var beforeAbilityDraw=episodePlayer.Deck.DrawPile.ToArray();
        var beforeAbilityDummy=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
        var abilities=SoloTacticAbilities.Legal(episode);
        CaptureLifecycle(episode,"TacticAbility",abilities);
        Check(abilities.Length>0,"Forced original tactic ability has legal choices: "+tactic.Id);
        Check(beforeAbilityHand.SequenceEqual(episodePlayer.Deck.Hand)&&beforeAbilityDraw.SequenceEqual(episodePlayer.Deck.DrawPile)
            &&beforeAbilityDummy==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture()),
            "Ability candidate probing preserves cards and dummy state: "+tactic.Id);
        var selected=abilities.Last();
        if(tactic.Id=="night_6")selected=abilities.Single(a=>a.Kind==SoloTacticAbilityKind.StorePower);
        selected=System.Text.Json.JsonSerializer.Deserialize<SoloTacticAbility>(System.Text.Json.JsonSerializer.Serialize(selected));
        SoloTacticAbilities.Apply(episode,selected);
        Check(!SoloTacticAbilities.Legal(episode).Any(),"Used tactic ability cannot be repeated in the same window: "+tactic.Id);
        if(tactic.Id=="night_6")Check(episodePlayer.Deck.DrawPile.Count()==beforeAbilityDraw.Length-1
            &&episodePlayer.Deck.TacticAsideCards.Count()==1,"Sparing power stores exactly one actual top card");
        if(tactic.Id=="day_6")Check(episodeJourney.Context.ExtraTurn,"Right moment registers native extra turn");
        if(tactic.Id=="night_3")Check(episodePlayer.Tactic.ManaSearchUsedThisTurn,"Mana search uses native once-per-turn flag");
        if(tactic.Id=="night_2")Check(episodePlayer.Deck.DrawPileCount==3&&episodePlayer.Deck.DiscardPile.Count()==1,
            "Long night returns exactly three actual discarded cards to draw pile");
        fixture.Restore();testedTacticAbilities.Add(tactic.Id);
    }
    foreach(var inputTactic in episodeJourney.SoloClock.AvailableTactics()
        .Where(t=>t.Id is "day_2" or "day_3" or "night_5").ToArray())
    {
        if(testedTacticInputs.Contains(inputTactic.Id))continue;
        var inputFixture=DemoStateCheckpoint.Capture(episode);
        SoloClockCommands.Apply(episode,new(SoloClockKind.SelectTactic,inputTactic.Id));
        var inputHand=episodePlayer.Deck.Hand.ToArray();var inputDraw=episodePlayer.Deck.DrawPile.ToArray();
        var inputDummy=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
        int inputStep=episode.Steps;
        var options=SoloTacticInputs.Legal(episode);
        CaptureLifecycle(episode,"TacticInput",options);
        Check(options.Length>0,"Forced original tactic has available complete inputs: "+inputTactic.Id);
        Check(inputHand.SequenceEqual(episodePlayer.Deck.Hand)&&inputDraw.SequenceEqual(episodePlayer.Deck.DrawPile)
            &&inputDummy==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture())&&inputStep==episode.Steps,
            "Forced tactic probing is physically reversible: "+inputTactic.Id);
        if(inputTactic.Id=="day_2")
        {
            int n=inputHand.Length;
            Check(options.Length==1+n+n*(n-1)/2+n*(n-1)*(n-2)/6,"Rethink enumerates all subsets of zero through three actual hand cards");
        }
        if(inputTactic.Id=="night_5")Check(options.Select(c=>c.CardId).SequenceEqual(inputDraw.Select(c=>c.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal)),
            "Deck-search support exposes canonical card identities, never draw positions");
        var selectedInput=options.Last();
        selectedInput=System.Text.Json.JsonSerializer.Deserialize<SoloTacticInput>(System.Text.Json.JsonSerializer.Serialize(selectedInput));
        SoloTacticInputs.Apply(episode,selectedInput);
        Check(!episodeJourney.Game.HasPendingTacticSelection,"Forced tactic input completes real selection: "+inputTactic.Id);
        if(inputTactic.Id=="day_3")
        {
            SoloClockCommands.Apply(episode,new(SoloClockKind.BeginTurn));
            var reserved=SoloTacticAbilities.Legal(episode).Single(a=>a.Kind==SoloTacticAbilityKind.ReservedMana);
            SoloTacticAbilities.Apply(episode,reserved);
            Check(episodePlayer.Tactic.Spent&&episodePlayer.Tactic.ReservedDie==null
                &&!SoloTacticAbilities.Legal(episode).Any(),"Actual reserved source die is consumed once through tactic dispatcher");
        }
        if(inputTactic.Id=="night_5")Check(episodePlayer.Deck.Hand.Count(c=>c.Id==selectedInput.CardId)==inputHand.Count(c=>c.Id==selectedInput.CardId)+1,
            "Selected search card actually moves into hand");
        inputFixture.Restore();testedTacticInputs.Add(inputTactic.Id);
    }
    var pick=episodeJourney.SoloClock.AvailableTactics()
        .OrderBy(t=>t.Id is "day_2" or "day_3" or "night_5" or "night_6"?1:0).ThenBy(t=>t.Number).First();
    var clockStateBefore=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles));
    var dummyBefore=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
    int stepBeforeClock=episode.Steps;
    var clockSupport=SoloClockCommands.Legal(episode);
    Check(clockSupport.Length==episodeJourney.SoloClock.AvailableTactics().Count
        &&clockSupport.All(c=>c.Kind==SoloClockKind.SelectTactic),"Round boundary offers all remaining actual tactics only");
    Check(clockStateBefore==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles))
        &&dummyBefore==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture())&&episode.Steps==stepBeforeClock,
        "Clock support probing restores dummy physical deck, public state and reward index");
    var selectedClock=clockSupport.Single(c=>c.TacticId==pick.Id);
    selectedClock=System.Text.Json.JsonSerializer.Deserialize<SoloClockCommand>(System.Text.Json.JsonSerializer.Serialize(selectedClock));
    episodeReturn+=SoloClockCommands.Apply(episode,selectedClock).Reward;
    if(episodeJourney.Game.HasPendingTacticSelection)
    {
        var pendingSnapshot=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles));
        var pendingDummy=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
        var handBefore=episodePlayer.Deck.Hand.ToArray();var drawBefore=episodePlayer.Deck.DrawPile.ToArray();
        var inputs=SoloTacticInputs.Legal(episode);
        Check(inputs.Length>0,"Actual pending tactic has complete serializable choices");
        Check(pendingSnapshot==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles))
            &&pendingDummy==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture())
            &&handBefore.SequenceEqual(episodePlayer.Deck.Hand)&&drawBefore.SequenceEqual(episodePlayer.Deck.DrawPile),
            "Tactic candidate probing restores physical hand, draw order and dummy clock");
        if(pick.Id=="night_5")Check(inputs.Select(i=>i.CardId).SequenceEqual(drawBefore.Select(c=>c.Id).Distinct().OrderBy(id=>id,StringComparer.Ordinal)),
            "Search choices disclose identities in canonical order, without hidden draw slots");
        var input=System.Text.Json.JsonSerializer.Deserialize<SoloTacticInput>(System.Text.Json.JsonSerializer.Serialize(inputs[0]));
        episodeReturn+=SoloTacticInputs.Apply(episode,input).Reward;
        Check(!episodeJourney.Game.HasPendingTacticSelection,"Chosen complete tactic input clears native pending state");
    }
    while(!episodeJourney.Game.ClockTracker.RoundEnded)
    {
        if(pick.Id=="night_6" && !episodePlayer.Tactic.Spent)
            episodeReturn+=episode.Apply(j=>j.Game.ResolveSparingPower(true)).Reward;
        Check(SoloClockCommands.Legal(episode).Contains(new SoloClockCommand(SoloClockKind.BeginTurn)),
            "Native player turn is present after automatic dummy scheduling");
        episodeReturn+=SoloClockCommands.Apply(episode,new(SoloClockKind.BeginTurn)).Reward;
        if(round==1 && episodeJourney.CompletedTurns==0)
        {
            var spatialStart=DemoStateCheckpoint.Capture(episode);
            episodeJourney.Context.MovementPool=4; // controlled movement-budget fixture
            var beforeSpatial=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles));
            var beforeSupply=System.Text.Json.JsonSerializer.Serialize(episodeTiles.Capture());
            var commands=SoloSpatialCommands.Legal(episodeJourney,episodeTiles);
            Check(commands.Any(c=>c.Kind==SoloSpatialKind.Move),"Actual opening exposes coordinate movement candidates");
            Check(commands.Distinct().Count()==commands.Length,"Spatial support contains no duplicate choices");
            Check(beforeSpatial==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles))
                &&beforeSupply==System.Text.Json.JsonSerializer.Serialize(episodeTiles.Capture())
                &&episodeJourney.Context.MovementPool==4,"Spatial probing leaves public world, tile RNG and movement unchanged");
            Check(commands.SequenceEqual(SoloSpatialCommands.Legal(episodeJourney,alternateTiles)),
                "Spatial support is independent of hidden next tile face");
            var command=commands.First(c=>c.Kind==SoloSpatialKind.Move);
            var serialized=System.Text.Json.JsonSerializer.Serialize(command);
            var reloaded=System.Text.Json.JsonSerializer.Deserialize<SoloSpatialCommand>(serialized);
            var spatialResult=SoloSpatialCommands.Apply(episode,reloaded);
            Check(episodePlayer.Position.Equals(new MK.Logic.Runtime.Map.AxialCoord(command.Q,command.R))
                &&spatialResult.Index==episode.Steps,"Serialized spatial command executes through native episode transaction");
            spatialStart.Restore();
        }
        int beforeStep=episode.Steps,beforeFame=episodePlayer.Fame;
        var beforeDummy=episodeDummy.Capture();
        Reject(()=>episode.Apply(j=>{j.Player.Fame+=999;throw new InvalidOperationException("Injected failure after mutation");}),
            "Failed transaction must propagate");
        Check(episode.Steps==beforeStep && episodePlayer.Fame==beforeFame
            && episodeDummy.Capture().Draw.SequenceEqual(beforeDummy.Draw),"Rejected step leaves native state, clock and reward index intact");
        if(round==1 && episodeJourney.CompletedTurns==0)
        {
            // Controlled site fixtures; restore before the actual six-round trajectory.
            var siteFixture=DemoStateCheckpoint.Capture(episode);
            episode.Apply(j=>j.Player.Fame=3); // controlled level-two reward trigger
            SoloEndTurnInputs.Apply(episode,new(new[]{0}));
            Check(episodeJourney.Game.IsEndingTurn&&episodeJourney.Progression.Pending,
                "Real end turn pauses for native level reward before refill and dummy turn");
            var levelChoices=episodeJourney.Progression.Choices.ToArray();
            var levelActions=episodeJourney.Progression.Actions.ToArray();
            var rewardView=episodeJourney.ObserveSoloOffers();
            Check(rewardView.PendingLevel&&rewardView.PendingLevelNumber==2
                &&rewardView.LevelSkills.Select(x=>x.Id).SequenceEqual(levelChoices.Select(x=>x.SourceId))
                &&rewardView.AdvancedActions.Select(x=>x.Id).SequenceEqual(levelActions.Select(x=>x.Id)),
                "Policy offer observation matches actual pending skill and advanced reward slots");
            var levelDummy=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
            int levelBefore=episodePlayer.Level;int levelStep=episode.Steps;
            var rewards=SoloLevelRewardInputs.Legal(episode);
            CaptureLifecycle(episode,"LevelReward",rewards);
            Check(rewards.Count(r=>r.Offer==SoloSkillOffer.Own)==levelChoices.Length*levelActions.Length,
                "Every own-skill and actual action-offer pair remains available");
            Check(levelBefore==episodePlayer.Level&&levelStep==episode.Steps
                &&levelChoices.SequenceEqual(episodeJourney.Progression.Choices)&&levelActions.SequenceEqual(episodeJourney.Progression.Actions)
                &&levelDummy==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture()),
                "Reward candidate probing restores level, actual offers, reward index and dummy clock");
            var chosenReward=rewards.First(r=>r.Offer==SoloSkillOffer.Own);
            string acquiredSkill=levelChoices[chosenReward.SkillSlot].SourceId;
            string acquiredAction=levelActions[chosenReward.ActionSlot].Id;
            chosenReward=System.Text.Json.JsonSerializer.Deserialize<SoloLevelRewardInput>(System.Text.Json.JsonSerializer.Serialize(chosenReward));
            SoloLevelRewardInputs.Apply(episode,chosenReward);
            Check(episodePlayer.Level==2&&episodePlayer.Skills.Any(s=>s.SourceId==acquiredSkill)
                &&episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile).Any(c=>c.Id==acquiredAction),
                "Serialized reward choice acquires actual skill and advanced action");
            Check(!episodeJourney.Game.IsEndingTurn&&episodeJourney.CompletedTurns==1
                &&!SoloLevelRewardInputs.Legal(episode).Any(),"Reward completes paused turn once and removes reward support");
            Check(!episodeJourney.ObserveSoloOffers().PendingLevel&&episodeJourney.ObserveSoloOffers().LevelSkills.Length==0,
                "Resolved level choice disappears from policy offer observation");
            Reject(()=>SoloLevelRewardInputs.Apply(episode,chosenReward),"Completed level reward cannot be collected twice");
            siteFixture.Restore();
            episode.Apply(j=>j.Player.Fame=99); // controlled multi-level trigger, no policy performance claim
            SoloEndTurnInputs.Apply(episode,new(new[]{0}));
            int levelSelections=0;
            while(episodeJourney.Progression.Pending)
            {
                var options=SoloLevelRewardInputs.Legal(episode);
                CaptureLifecycle(episode,"LevelReward",options);
                Check(options.Length>0&&episodeJourney.CompletedTurns==0,"Each pending skill level stays inside the same unfinished turn");
                Check(options.Where(r=>r.Offer==SoloSkillOffer.Common).All(r=>r.ActionSlot==0),
                    "Common-skill alternatives require the bottom actual action offer");
                SoloLevelRewardInputs.Apply(episode,options.First(r=>r.Offer==SoloSkillOffer.Own));
                if(++levelSelections>5)throw new Exception("Unexpected repeated level reward loop");
            }
            Check(levelSelections==5&&episodePlayer.Level==10&&episodeJourney.CompletedTurns==1&&episodePlayer.Fame==99,
                "Five native skill rewards resolve level two through ten without inventing fame or duplicate turns");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.Village);
                j.Context.InfluencePool=3;j.Player.Wounds=1;
            });
            int siteProbeStep=episode.Steps;
            var siteInputs=SoloSiteInputs.Legal(episode).ToArray();
            CaptureLifecycle(episode,"Site",siteInputs);
            Check(siteInputs.Any(x=>x.Operation==SoloSiteOperation.BuyHealing)
                &&episode.Steps==siteProbeStep&&!episodeJourney.Context.InNegotiation&&episodeJourney.Context.InfluencePool==3,
                "Site support probing restores negotiation, influence and episode index");
            var siteCommand=new SoloPolicyCommand(1,episode.Steps,Site:new(SoloSiteOperation.BuyHealing));
            siteCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(siteCommand));
            var boughtHealing=siteCommand.Execute(episode);
            Check(episodeJourney.Context.InNegotiation&&episodeJourney.Context.InfluencePool==0
                &&episodeJourney.Context.Healing.Total==1&&boughtHealing.Reward==0,
                "Village healing atomically opens negotiation and spends three influence without purchase reward");
            int beforeRejectedPurchase=episode.Steps;
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyHealing)),"Unaffordable second healing purchase rejects");
            Check(episode.Steps==beforeRejectedPurchase&&episodeJourney.Context.Healing.Total==1,
                "Rejected site purchase restores resources and reward index");
            SoloHealingInputs.Apply(episode,new());
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.EndInteraction));
            Check(episodePlayer.Wounds==0&&episodeJourney.Context.InteractionClosed&&!SoloSiteInputs.Legal(episode).Any(),
                "Paid village treatment heals a real wound and closed negotiation cannot reopen");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.City){OwnerId=j.Player.Id,CityColor=CityColor.White};
                j.Context.InfluencePool=100;
                foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddCrystal(c,1);
            });
            var recruitInput=SoloSiteInputs.Legal(episode).First(x=>x.Operation==SoloSiteOperation.Recruit&&!x.Loyalty);
            CaptureLifecycle(episode,"Site",new[]{recruitInput});
            var recruitedCard=episodeJourney.Game.Globals.Units.Offer[recruitInput.Slot];
            var recruitView=episodeJourney.ObserveSoloOffers();
            Check(recruitView.Units[recruitInput.Slot].Id==recruitedCard.Id
                &&recruitView.Units.Select(x=>x.Slot).SequenceEqual(Enumerable.Range(0,recruitView.Units.Length)),
                "Recruitment policy slot resolves to the same visible original unit definition");
            int priorUnits=episodePlayer.Units.Count;
            SoloSiteInputs.Apply(episode,recruitInput);
            Check(episodePlayer.Units.Count==priorUnits+1&&ReferenceEquals(episodePlayer.Units.Last().Card,recruitedCard)
                &&episodeJourney.Context.InfluencePool<100,"Recruit command acquires the actual offered card and consumes influence");
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.Dismiss,episodePlayer.Units.Count-1));
            Check(episodePlayer.Units.Count==priorUnits,"Dismiss command removes the selected actual owned unit");
            episodePlayer.Deck.SetDeck(new[]{new DeedCard("items_017",CardType.Artifact)});
            episodePlayer.Deck.DrawExact(1);
            var commandBanner=episodePlayer.Deck.Hand[0];
            var commandBurnBoundary=DemoStateCheckpoint.Capture(episode);
            string commandBurnBefore=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            var commandBurn=SoloArtifactInputs.Legal(episode).First(x=>x.Burn);
            CaptureLifecycle(episode,"Artifact",new[]{commandBurn});
            Check(commandBurnBefore==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                "Command banner burn support probing does not consume inventory or supply");
            var commandBurnOffer=episodeJourney.Game.Globals.Units.Offer[commandBurn.OfferedUnitSlot];
            int commandBurnFame=episodePlayer.Fame;
            int commandBurnInfluence=episodeJourney.Context.InfluencePool;
            int commandBurnUnitCount=episodePlayer.Units.Count;
            var commandBurnEnvelope=new SoloPolicyCommand(1,episode.Steps,Artifact:commandBurn);
            commandBurnEnvelope=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(commandBurnEnvelope));
            commandBurnEnvelope.Execute(episode);
            Check(episodePlayer.Units.Any(u=>ReferenceEquals(u.Card,commandBurnOffer))
                &&!episodeJourney.Game.Globals.Units.Offer.Contains(commandBurnOffer)
                &&episodePlayer.Units.Count==commandBurnUnitCount+1-(commandBurn.DisbandUnitSlot>=0?1:0),
                "Serialized command banner burn recruits selected physical supply with explicit disband semantics");
            Check(episodePlayer.Fame==commandBurnFame+2&&episodeJourney.Context.InfluencePool==commandBurnInfluence,
                "Command banner burn awards native two fame without spending interaction influence");
            Check(!episodePlayer.Deck.Hand.Contains(commandBanner)&&!episodePlayer.Deck.PlayedCards.Contains(commandBanner)
                &&!episodePlayer.Deck.DiscardPile.Contains(commandBanner)&&!episodePlayer.Deck.DrawPile.Contains(commandBanner),
                "Burned command banner cannot return to the persistent deck");
            string commandBurnAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            commandBurnBoundary.Restore();commandBurnEnvelope.Execute(episode);
            Check(commandBurnAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                "Command banner burn restores and replays identical public state and score");
            commandBurnBoundary.Restore();
            var commandBannerPlay=SoloArtifactInputs.Legal(episode).Single(x=>!x.Burn);
            CaptureLifecycle(episode,"Artifact",new[]{commandBannerPlay});
            var commandBannerEnvelope=new SoloPolicyCommand(1,episode.Steps,Artifact:commandBannerPlay);
            commandBannerEnvelope=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(commandBannerEnvelope));
            commandBannerEnvelope.Execute(episode);
            var bannerRecruitBoundary=DemoStateCheckpoint.Capture(episode);
            string bannerRecruitBefore=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            var bannerRecruit=SoloSiteInputs.Legal(episode).First(x=>x.Operation==SoloSiteOperation.Recruit&&x.CommandBannerPlayedSlot>=0);
            CaptureLifecycle(episode,"Site",new[]{bannerRecruit});
            Check(bannerRecruitBefore==System.Text.Json.JsonSerializer.Serialize(episode.Observe())
                &&episodePlayer.Deck.PlayedCards.Contains(commandBanner),"Recruit support probing preserves played banner and actual offer");
            var bannerRecruitCommand=new SoloPolicyCommand(1,episode.Steps,Site:bannerRecruit);
            bannerRecruitCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(bannerRecruitCommand));
            bannerRecruitCommand.Execute(episode);
            Check(episodePlayer.Units.Count==priorUnits+1&&ReferenceEquals(episodePlayer.Units.Last().Banner,commandBanner)
                &&!episodePlayer.Deck.PlayedCards.Contains(commandBanner)&&episodeJourney.Context.CommandBannerForRecruit==null,
                "Complete recruitment attaches actual banner and consumes pending selection atomically");
            string bannerRecruitAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            bannerRecruitBoundary.Restore();bannerRecruitCommand.Execute(episode);
            Check(bannerRecruitAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Command banner recruitment replays with same offer and unit");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.City){OwnerId=j.Player.Id,CityColor=CityColor.Green};
                j.Context.InfluencePool=12;
            });
            var greenBoundary=DemoStateCheckpoint.Capture(episode);
            string greenBefore=SoloStateDigest.Compute(episode);
            var greenChoices=SoloSiteInputs.Legal(episode).Where(x=>x.Operation is SoloSiteOperation.BuyAdvanced or SoloSiteOperation.BuyBlindAdvanced).ToArray();
            CaptureLifecycle(episode,"Site",greenChoices);
            Check(greenChoices.Count(x=>x.Operation==SoloSiteOperation.BuyAdvanced)==episodeJourney.Game.Globals.AdvActions.Offer.Count
                &&greenChoices.Count(x=>x.Operation==SoloSiteOperation.BuyBlindAdvanced)==1
                &&greenBefore==SoloStateDigest.Compute(episode),"Green city offers all visible advanced cards and one identity-free blind draw without changing hidden supply");
            foreach(var greenChoice in greenChoices)
            {
                var offerBefore=episodeJourney.Game.Globals.AdvActions.Offer.ToArray();
                string expectedId=greenChoice.Operation==SoloSiteOperation.BuyAdvanced?offerBefore[greenChoice.Slot].Id:null;
                int ownedBefore=episodePlayer.Deck.DrawPileCount;
                var greenCommand=new SoloPolicyCommand(1,episode.Steps,Site:greenChoice);
                greenCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(greenCommand));
                greenCommand.Execute(episode);
                Check(episodeJourney.Context.InfluencePool==6&&episodePlayer.Deck.DrawPileCount==ownedBefore+1,
                    "Green city purchase pays six influence and gains exactly one real card");
                if(expectedId!=null)Check(episodePlayer.Deck.DrawPile.Any(c=>c.Id==expectedId)
                    &&offerBefore.Where((c,i)=>i!=greenChoice.Slot).All(c=>episodeJourney.Game.Globals.AdvActions.Offer.Contains(c)),
                    "Visible advanced purchase preserves unchosen offer identities while replenishing");
                else Check(greenChoice.Slot==-1&&offerBefore.SequenceEqual(episodeJourney.Game.Globals.AdvActions.Offer),
                    "Blind advanced purchase does not reveal a selectable card ID or alter the visible offer");
                string greenAfter=SoloStateDigest.Compute(episode);
                greenBoundary.Restore();greenCommand.Execute(episode);
                Check(greenAfter==SoloStateDigest.Compute(episode),"Green city visible/blind purchase replays exact hidden deck and accounting");
                greenBoundary.Restore();
            }
            episodeJourney.Context.InfluencePool=5;
            string poorGreen=SoloStateDigest.Compute(episode);
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyBlindAdvanced)),"Insufficient green-city influence rejected before drawing");
            Check(poorGreen==SoloStateDigest.Compute(episode),"Rejected blind purchase cannot consume or peek a hidden card");
            greenBoundary.Restore();
            episodeJourney.Game.Globals.AdvActions.SetDeck(Array.Empty<AdvActionCard>());
            string emptyGreen=SoloStateDigest.Compute(episode);
            Check(!SoloSiteInputs.Legal(episode).Any(x=>x.Operation==SoloSiteOperation.BuyBlindAdvanced),
                "Empty advanced deck removes blind purchase while retaining actual offer purchases");
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyBlindAdvanced)),"Empty blind draw rejected");
            Check(emptyGreen==SoloStateDigest.Compute(episode),"Empty blind draw preserves influence and complete private state");
            greenBoundary.Restore();
            string malformedGreen=SoloStateDigest.Compute(episode);
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyBlindAdvanced,Slot:0)),
                "Blind purchase cannot encode a hidden card slot");
            Check(malformedGreen==SoloStateDigest.Compute(episode),"Malformed blind command is transactional");
            siteFixture.Restore();
            var whiteElite=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).First(c=>c.IsElite);
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.City){OwnerId=j.Player.Id,CityColor=CityColor.White};
                j.Game.Globals.Units.SetDeck(new[]{whiteElite});
                j.Context.InfluencePool=4;
            });
            var whiteBoundary=DemoStateCheckpoint.Capture(episode);
            string whiteBefore=SoloStateDigest.Compute(episode);
            var whiteChoices=SoloSiteInputs.Legal(episode).Where(x=>x.Operation==SoloSiteOperation.ExpandEliteOffer).ToArray();
            CaptureLifecycle(episode,"Site",whiteChoices);
            Check(whiteChoices.Length==1&&whiteChoices[0].Slot==-1&&whiteBefore==SoloStateDigest.Compute(episode),
                "White city expansion exposes one identity-free choice and probing preserves private supply");
            var whiteCommand=new SoloPolicyCommand(1,episode.Steps,Site:whiteChoices.Single());
            whiteCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(whiteCommand));
            whiteCommand.Execute(episode);
            Check(episodeJourney.Context.InfluencePool==2&&episodeJourney.Game.Globals.Units.Offer.Count==1
                &&ReferenceEquals(episodeJourney.Game.Globals.Units.Offer[0],whiteElite),
                "White city pays two influence to reveal actual elite card without recruiting it");
            string whiteAfter=SoloStateDigest.Compute(episode);
            whiteBoundary.Restore();whiteCommand.Execute(episode);
            Check(whiteAfter==SoloStateDigest.Compute(episode),"White city expansion private state replays exactly");
            Check(!SoloSiteInputs.Legal(episode).Any(x=>x.Operation==SoloSiteOperation.ExpandEliteOffer),
                "Empty elite deck removes expansion choice");
            string emptyWhite=SoloStateDigest.Compute(episode);
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.ExpandEliteOffer)),"Empty elite expansion rejected");
            Check(emptyWhite==SoloStateDigest.Compute(episode),"Empty elite expansion cannot consume influence");
            whiteBoundary.Restore();episodeJourney.Context.InfluencePool=1;
            string poorWhite=SoloStateDigest.Compute(episode);
            Reject(()=>SoloSiteInputs.Apply(episode,new(SoloSiteOperation.ExpandEliteOffer)),"Insufficient influence cannot reveal elite card");
            Check(poorWhite==SoloStateDigest.Compute(episode),"Unaffordable elite expansion preserves hidden supply");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.City){OwnerId=j.Player.Id,CityColor=CityColor.Red};
                j.Context.InfluencePool=24;
            });
            int redSupplyBefore=episodeJourney.Game.Globals.Artifacts.Count;
            int redOwnedBefore=episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile).Concat(episodePlayer.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact);
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyArtifact));
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyArtifact));
            Check(episodeJourney.Context.InfluencePool==0&&episodeJourney.PurchasedCityArtifacts==2
                &&episodeJourney.CityArtifactChoices.Count==0&&episodeJourney.Game.Globals.Artifacts.Count==redSupplyBefore,
                "Red city buys two deferred artifacts without drawing or revealing either");
            Check(episodeJourney.ObserveSoloOffers().CityArtifacts.Length==0&&episodeJourney.ObserveSoloOffers().PurchasedCityArtifacts==2,
                "Policy sees purchased count but no hidden artifact identities before end turn");
            var redPurchaseBoundary=DemoStateCheckpoint.Capture(episode);
            var redEnd=SoloEndTurnInputs.Legal(episode).First();
            SoloEndTurnInputs.Apply(episode,redEnd);
            Check(episodeJourney.Game.IsEndingTurn&&episodeJourney.CityArtifactChoices.Count==3
                &&episodeJourney.ObserveSoloOffers().CityArtifacts.Length==3,
                "Committed end turn reveals N plus one artifacts before drawing next hand");
            var redRewardBoundary=DemoStateCheckpoint.Capture(episode);
            var redCommands=SoloPolicyCommand.ImplementedChoices(episode).ToArray();
            CaptureLifecycle(episode,"CityArtifactReward",redCommands.Select(c=>c.CityArtifactReward).ToArray());
            Check(redCommands.Length==6&&redCommands.All(c=>c.CityArtifactReward?.KeepSlots.Length==2),
                "All ordered two-of-three choices retained and no return to normal play");
            string redPending=SoloStateDigest.Compute(episode);
            Reject(()=>new SoloPolicyCommand(1,episode.Steps,Site:new(SoloSiteOperation.BuyArtifact)).Execute(episode),
                "Cannot purchase again after seeing end-turn artifact choices");
            Reject(()=>new SoloPolicyCommand(1,episode.Steps,CityArtifactReward:new(new[]{0,0})).Execute(episode),
                "Duplicate artifact selection rejected");
            Check(redPending==SoloStateDigest.Compute(episode),"Rejected reward choices preserve all private state");
            foreach(var redReward in redCommands)
            {
                var rewardCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(redReward));
                rewardCommand.Execute(episode);
                Check(episodeJourney.PurchasedCityArtifacts==0&&episodeJourney.CityArtifactChoices.Count==0
                    &&episodeJourney.Game.Globals.Artifacts.Count==redSupplyBefore-2
                    &&episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile).Concat(episodePlayer.Deck.DiscardPile).Count(c=>c.Type==CardType.Artifact)==redOwnedBefore+2,
                    "Red city delivers exactly two artifacts and returns only unchosen card");
                string redDone=SoloStateDigest.Compute(episode);
                redRewardBoundary.Restore();rewardCommand.Execute(episode);
                Check(redDone==SoloStateDigest.Compute(episode),"Red city end-turn reward replays exact physical supply and next hand");
                redRewardBoundary.Restore();
            }
            redPurchaseBoundary.Restore();
            SoloEndTurnInputs.Apply(episode,redEnd);
            Check(redPending==SoloStateDigest.Compute(episode),"End-turn reveal itself replays without extra draws");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.Monastery);
                j.Context.InfluencePool=8;
                j.Game.Globals.MonasteryOffer.Clear();
                j.Game.Globals.MonasteryOffer.Add(j.Game.Globals.AdvActions.Draw());
            });
            var monasteryBoundary=DemoStateCheckpoint.Capture(episode);
            var monasteryCard=episodeJourney.Game.Globals.MonasteryOffer.Single();
            var mainAdvancedBefore=episodeJourney.Game.Globals.AdvActions.Offer.ToArray();
            var monasteryObservation=episodeJourney.ObserveSoloOffers();
            Check(monasteryObservation.MonasteryActions.Single().Id==monasteryCard.Id,
                "Separate public monastery offer uses actual physical supply");
            string monasteryBefore=SoloStateDigest.Compute(episode);
            var monasteryChoices=SoloSiteInputs.Legal(episode).ToArray();
            CaptureLifecycle(episode,"Site",monasteryChoices);
            Check(monasteryChoices.Any(x=>x.Operation==SoloSiteOperation.LearnMonastery)
                &&monasteryChoices.Any(x=>x.Operation==SoloSiteOperation.BuyMonasteryHealing)
                &&monasteryBefore==SoloStateDigest.Compute(episode),"Monastery choices preserve complete hidden state");
            int monasteryHealing=episodeJourney.Context.Healing.Total;
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyMonasteryHealing));
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.LearnMonastery,0));
            Check(episodeJourney.Context.InfluencePool==0&&episodeJourney.Context.Healing.Total==monasteryHealing+1
                &&episodeJourney.Game.Globals.MonasteryOffer.Count==0
                &&mainAdvancedBefore.SequenceEqual(episodeJourney.Game.Globals.AdvActions.Offer)
                &&episodePlayer.Deck.DrawPile.Any(c=>c.Id==monasteryCard.Id),
                "Monastery charges two per healing and six per learned card without replenishing either offer");
            string monasteryAfter=SoloStateDigest.Compute(episode);
            monasteryBoundary.Restore();
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.BuyMonasteryHealing));
            SoloSiteInputs.Apply(episode,new(SoloSiteOperation.LearnMonastery,0));
            Check(monasteryAfter==SoloStateDigest.Compute(episode),"Monastery purchases replay physical cards and accounting");
            monasteryBoundary.Restore();
            episodeJourney.Game.Map.Places[episodePlayer.Position].MonasteryBurned=true;
            Check(!episodeJourney.CanInteractAtCurrentSite&&SoloSiteInputs.Legal(episode).Count()==0,
                "Explicit burned monastery cannot sell healing, teach, or recruit");
            Check(PublicOriginalMap.ObserveSite(episodeJourney.Game.Map,episodeJourney.Game.Map.Places[episodePlayer.Position]).MonasteryBurned,
                "Destroyed monastery status is public to the policy");
            monasteryBoundary.Restore();
            var monasteryGlobals=episodeJourney.Game.Globals;
            monasteryGlobals.AdvActions.SetDeck(new[]{new AdvActionCard("advanced_card_000"),new AdvActionCard("advanced_card_001")});
            monasteryGlobals.RefreshMonasteryOffer(2);
            Check(monasteryGlobals.MonasteryOffer.Select(c=>c.Id).SequenceEqual(new[]{"advanced_card_000","advanced_card_001"})
                &&ReferenceEquals(monasteryGlobals.AdvActions.Draw(),monasteryCard),
                "Round refresh returns leftover monastery cards to bottom and deals one per intact monastery");
            siteFixture.Restore();
            episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.City){OwnerId=j.Player.Id,CityColor=CityColor.Blue};
                j.Context.InfluencePool=7;
                foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddCrystal(c,1);
            });
            var spellInput=SoloSiteInputs.Legal(episode).First(x=>x.Operation==SoloSiteOperation.BuySpell);
            CaptureLifecycle(episode,"Site",new[]{spellInput});
            string purchasedSpell=episodeJourney.Game.Globals.Spells.Offer[spellInput.Slot].Id;
            SoloSiteInputs.Apply(episode,spellInput);
            Check(episodeJourney.Context.InfluencePool==0&&episodePlayer.Deck.DrawPile.Any(c=>c.Id==purchasedSpell),
                "Spell purchase consumes seven influence and places the actual offered spell in the deck");
            siteFixture.Restore();
            episode.Apply(j=>j.Context.PendingSpellReward=true); // separate real conquest is exercised by SoloCityGate
            int pendingRewardStep=episode.Steps;
            var beforeRewardOffers=episodeJourney.Game.Globals.Spells.Offer.ToArray();
            var rewardEnds=SoloEndTurnInputs.Legal(episode).ToArray();
            Check(rewardEnds.Select(x=>x.SpellRewardSlot).Distinct().OrderBy(x=>x)
                .SequenceEqual(Enumerable.Range(0,beforeRewardOffers.Length))
                &&episodeJourney.Context.SelectedSpellReward==null&&episode.Steps==pendingRewardStep,
                "Complete end-turn support includes every pending spell and restores all reward preselection");
            Reject(()=>SoloEndTurnInputs.Apply(episode,new(new[]{0})),"Pending reward rejects missing spell slot");
            Reject(()=>SoloEndTurnInputs.Apply(episode,new(new[]{0},SpellRewardSlot:beforeRewardOffers.Length)),
                "Pending reward rejects out-of-range spell slot");
            Check(episode.Steps==pendingRewardStep&&beforeRewardOffers.SequenceEqual(episodeJourney.Game.Globals.Spells.Offer),
                "Invalid reward parameters leave supply and reward ledger unchanged");
            SoloEndTurnInputs.Apply(episode,rewardEnds.First());
            Check(!episodeJourney.Context.PendingSpellReward&&!episodeJourney.Game.HasActiveTurn,
                "Complete end-turn command consumes pending reward and ends the actual turn");
            siteFixture.Restore();
            var deedBoundary=DemoStateCheckpoint.Capture(episode);
            foreach(string travelSpell in new[]{"magic_004","magic_023"})
            {
                episodePlayer.Deck.SetDeck(new[]{new DeedCard(travelSpell,CardType.Spell)});episodePlayer.Deck.DrawExact(1);
                foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})episodePlayer.Mana.AddToken(c,2);
                episodeJourney.Context.MovementPool=1;
                var pathBoundary=DemoStateCheckpoint.Capture(episode);
                var beforePaths=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                var pathChoices=SoloDeedInputs.SpellChoices(episode).Where(x=>x.Choice=="base"&&x.Inputs.TravelPath.Length>0).ToArray();
                Check(pathChoices.Length>0&&beforePaths==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Travel spell support uses revealed paths without consuming mana or moving: "+travelSpell);
                Check(pathChoices.All(x=>x.Inputs.TravelPath.Length<=(travelSpell=="magic_004"?3:1)),
                    "Path support respects native spell limit and actual movement budget");
                var pathInput=pathChoices.First();var pathEnd=pathInput.Inputs.TravelPath.Last();
                var spellCommand=new SoloPolicyCommand(1,episode.Steps,Deed:pathInput);
                spellCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(spellCommand));
                spellCommand.Execute(episode);
                Check(episodePlayer.Position.Equals(pathEnd)&&!episodePlayer.Deck.Hand.Any(c=>c.Id==travelSpell),
                    "Serialized complete travel spell pays and reaches actual selected destination");
                var pathAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                pathBoundary.Restore();spellCommand.Execute(episode);
                Check(pathAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Travel spell replay preserves public result");
                if(travelSpell=="magic_004")
                {
                    pathBoundary.Restore();
                    episodeJourney.Context.DayPart=DayPart.Night;episodePlayer.Mana.AddToken(ManaColor.Black,1);
                    var guard=episodeJourney.EnemySupply.Draw(EnemyTokenSupply.GuardColor(PlaceType.Keep));
                    var fort=new PlaceState(PlaceType.Keep){Enemy=guard};
                    episodeJourney.Game.Map.Places[pathEnd]=fort;
                    var tunnelBoundary=DemoStateCheckpoint.Capture(episode);
                    var tunnelChoices=SoloDeedInputs.SpellChoices(episode).Where(x=>x.Choice=="strong"
                        &&x.Inputs.TravelPath.Length>0&&x.Inputs.TravelPath.Last().Equals(pathEnd)).ToArray();
                    Check(tunnelChoices.Length>0&&!episodeJourney.Context.InBattle&&ReferenceEquals(fort.Enemy,guard),
                        "Tunnel attack probing preserves actual fortified guard and movement phase");
                    var tunnelCommand=new SoloPolicyCommand(1,episode.Steps,Deed:tunnelChoices[0]);
                    tunnelCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(tunnelCommand));
                    tunnelCommand.Execute(episode);
                    Check(episodeJourney.Context.InBattle&&episodePlayer.Position.Equals(pathEnd)
                        &&ReferenceEquals(episodeJourney.Battle.Enemies.Single(),guard)
                        &&episodeJourney.Context.IgnoreSiteFortifications&&episodeJourney.Context.TunnelAssaultActive,
                        "Complete tunnel attack enters original defender battle with native fortification bypass");
                    var tunnelAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                    tunnelBoundary.Restore();tunnelCommand.Execute(episode);
                    Check(tunnelAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Tunnel attack reproduces battle boundary");
                }
                deedBoundary.Restore();
            }
            foreach(string materialArtifact in new[]{"items_013","items_021"})
            foreach(bool burnMaterialArtifact in new[]{false,true})
            {
                ManaColor materialColor=materialArtifact=="items_021"?ManaColor.Blue:
                    CardColorHelper.Get(burnMaterialArtifact?episodeJourney.Game.Globals.Spells.Offer[0].Id:
                        episodeJourney.Game.Globals.AdvActions.Offer[0].Id).Value;
                string materialId=originalCards.Values.First(c=>CardColorHelper.Get(c.Id)==materialColor).Id;
                episodePlayer.Deck.SetDeck(new[]{new DeedCard(materialArtifact,CardType.Artifact),new DeedCard(materialId,CardType.Action)});
                episodePlayer.Deck.DrawExact(2);
                var materialBoundary=DemoStateCheckpoint.Capture(episode);
                var beforeMaterials=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                var inputs=SoloMaterialArtifactInputs.Legal(episode).Where(x=>x.Burn==burnMaterialArtifact).ToArray();
                CaptureLifecycle(episode,"MaterialArtifact",inputs);
                Check(inputs.Length>0&&beforeMaterials==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Material artifact choices preserve real hand and supply: "+materialArtifact+"/"+burnMaterialArtifact);
                var input=materialArtifact=="items_013"?inputs.First(x=>x.OfferSlot==0):
                    inputs.First(x=>x.First==1&&(!burnMaterialArtifact||x.Second==3)&&x.Crystal==ManaColor.Red);
                var physicalArtifact=episodePlayer.Deck.Hand[0];var physicalMaterial=episodePlayer.Deck.Hand[1];
                episode.Apply(j=>j.PlayMaterialArtifact(physicalArtifact,input.Burn,input.MaterialSlot<0?null:physicalMaterial,
                    advanced:materialArtifact=="items_013"&&!input.Burn?j.Game.Globals.AdvActions.Offer[0]:null,
                    spell:materialArtifact=="items_013"&&input.Burn?j.Game.Globals.Spells.Offer[0]:null,
                    first:input.First,second:input.Second,crystal:input.Crystal,path:input.Path));
                var nativeMaterials=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                materialBoundary.Restore();
                var materialEnvelope=new SoloPolicyCommand(1,episode.Steps,MaterialArtifact:input);
                materialEnvelope=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(materialEnvelope));
                materialEnvelope.Execute(episode);
                Check(nativeMaterials==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Serialized material artifact matches original effect: "+materialArtifact+"/"+burnMaterialArtifact);
                Check(materialArtifact=="items_013"?episodePlayer.Deck.RemovedCards.Contains(physicalMaterial):
                    burnMaterialArtifact?episodePlayer.Deck.Hand.Contains(physicalMaterial):episodePlayer.Deck.DiscardPile.Contains(physicalMaterial),
                    "Actual material is removed, discarded, or retained according to the native mode");
                deedBoundary.Restore();
            }
            foreach(bool tomeBurn in new[]{false,true})
            {
                episodeJourney.Game.Globals.Spells.Offer.Clear();
                var offeredTomeSpell=new SpellCard("magic_002");
                episodeJourney.Game.Globals.Spells.Offer.Add(offeredTomeSpell);
                string greenMaterial=originalCards.Values.First(c=>CardColorHelper.Get(c.Id)==ManaColor.Green).Id;
                episodePlayer.Deck.SetDeck(new[]{new DeedCard("items_023",CardType.Artifact),
                    new DeedCard(greenMaterial,CardType.Action),new DeedCard(greenMaterial,CardType.Action),
                    new DeedCard(greenMaterial,CardType.Action)});
                episodePlayer.Deck.DrawExact(4);
                episodePlayer.Deck.TryDiscardExact(episodePlayer.Deck.Hand[3]);
                episodePlayer.Deck.TryDiscardExact(episodePlayer.Deck.Hand[2]);
                var tomeBoundary=DemoStateCheckpoint.Capture(episode);
                var tomeBefore=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                var tomeOptions=SoloMaterialArtifactInputs.Legal(episode).Where(x=>x.Burn==tomeBurn).ToArray();
                CaptureLifecycle(episode,"MaterialArtifact",tomeOptions);
                Check(tomeOptions.Length>0&&tomeBefore==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Tome candidates preserve actual spell supply and hand for both modes");
                var tomeInput=tomeOptions[0];var tome=episodePlayer.Deck.Hand[0];var payment=episodePlayer.Deck.Hand[1];
                episode.Apply(j=>j.PlayMaterialArtifact(tome,tomeBurn,payment,spell:offeredTomeSpell,
                    spellChoice:tomeInput.SpellChoice,target:tomeInput.EnemySlot,inputs:tomeInput.SpellInputs.Resolve(j)));
                var tomeNative=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                tomeBoundary.Restore();
                var tomeCommand=new SoloPolicyCommand(1,episode.Steps,MaterialArtifact:tomeInput);
                tomeCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(tomeCommand));
                tomeCommand.Execute(episode);
                Check(tomeNative==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Tome serialized action equals native borrowed spell");
                Check(episodeJourney.Game.Globals.Spells.Offer.Contains(offeredTomeSpell)&&episodePlayer.Deck.DiscardPile.Contains(payment)
                    &&episodePlayer.Deck.RemovedCards.Contains(tome)==tomeBurn,"Tome payment, supply ownership and burn are physical");
                deedBoundary.Restore();
            }
            foreach(string resourceArtifact in new[]{"items_004","items_005","items_006","items_007","items_010","items_011","items_014","items_015","items_012"})
            {
                episodePlayer.Deck.SetDeck(new[]{new DeedCard(resourceArtifact,CardType.Artifact)});
                episodePlayer.Deck.DrawExact(1);
                var artifactBoundary=DemoStateCheckpoint.Capture(episode);
                var beforeArtifact=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                var options=SoloArtifactInputs.Legal(episode).ToArray();
                CaptureLifecycle(episode,"Artifact",options);
                Check(options.Length>0&&beforeArtifact==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Original resource artifact support preserves state: "+resourceArtifact);
                foreach(bool burn in new[]{false,true})
                {
                    var option=options.First(x=>x.Burn==burn);
                    episode.Apply(j=>j.PlayOutsideArtifact(j.Player.Deck.Hand[option.HandSlot],burn,null,option.Colors));
                    var nativeExpected=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                    artifactBoundary.Restore();
                    var command=new SoloPolicyCommand(1,episode.Steps,Artifact:option);
                    command=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(command));
                    command.Execute(episode);
                    Check(nativeExpected==System.Text.Json.JsonSerializer.Serialize(episode.Observe())
                        &&episodePlayer.Deck.RemovedCards.Count==(burn?1:0),
                        "Serialized resource artifact matches native effect and permanent consumption: "+resourceArtifact+"/"+burn);
                    artifactBoundary.Restore();
                }
                deedBoundary.Restore();
            }
            foreach(var adventureType in new[]{PlaceType.Dungeon,PlaceType.Tomb,PlaceType.MonsterDen,PlaceType.SpawningGround})
            {
                episodeJourney.Game.Map.Places[episodePlayer.Position]=new PlaceState(adventureType);
                var adventureBoundary=DemoStateCheckpoint.Capture(episode);
                var beforeAdventure=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                var adventureChoices=SoloAdventureInputs.Legal(episode);
                Check(adventureChoices.Length==1&&beforeAdventure==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                    "Adventure entry probe restores actual enemy supply and public state: "+adventureType);
                var adventureCommand=new SoloPolicyCommand(1,episode.Steps,Adventure:adventureChoices[0]);
                adventureCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(adventureCommand));
                adventureCommand.Execute(episode);
                var encountered=episodeJourney.Battle.Enemies.Select(e=>e.Id).ToArray();
                Check(episodeJourney.Context.InBattle&&encountered.Length==(adventureType==PlaceType.SpawningGround?2:1)
                    &&SoloAdventureInputs.Legal(episode).Length==0,
                    "Serialized adventure entry creates actual battle once: "+adventureType);
                if(adventureType==PlaceType.SpawningGround)
                {
                    var wingsBoundary=DemoStateCheckpoint.Capture(episode);
                    episodePlayer.Deck.SetDeck(new[]{new DeedCard("magic_023",CardType.Spell)});episodePlayer.Deck.DrawExact(1);
                    episodeJourney.Context.DayPart=DayPart.Night;episodeJourney.Context.MovementPool=2;
                    foreach(var c in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black})episodePlayer.Mana.AddToken(c,1);
                    var wings=SoloDeedInputs.SpellChoices(episode).Where(x=>x.Choice=="strong").ToArray();
                    int eligible=episodeJourney.Battle.Enemies.Count(e=>!e.Abilities.Contains(Ability.MagicResist));
                    Check(wings.Length==(1<<eligible)-1,"Night wings preserves all nonempty eligible target combinations");
                    if(wings.Length>0)
                    {
                        var selected=wings.OrderByDescending(x=>x.Inputs.EnemyIndices.Length).First();
                        var wingsCommand=new SoloPolicyCommand(1,episode.Steps,Deed:selected);
                        wingsCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(wingsCommand));
                        wingsCommand.Execute(episode);int n=selected.Inputs.EnemyIndices.Length;
                        Check(episodeJourney.Context.MovementPool==2-n*(n-1)/2
                            &&selected.Inputs.EnemyIndices.All(episodeJourney.Context.NightWingsSkippedIndices.Contains),
                            "Night wings consumes triangular movement cost and skips exactly selected enemy attacks");
                    }
                    wingsBoundary.Restore();
                    var fearUnit=new UnitState(UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).OrderBy(u=>u.Level).First());
                    episodePlayer.Units.Add(fearUnit);
                    episodePlayer.Deck.SetDeck(new[]{new DeedCard("items_001",CardType.Artifact)});episodePlayer.Deck.DrawExact(1);
                    var fearCard=episodePlayer.Deck.Hand[0];
                    BannerActions.AssignFear(episodePlayer,fearUnit,fearCard);
                    episode.Apply(j=>j.Battle.Advance());
                    Check(episodeJourney.Battle.Phase==Phase.Block&&episodeJourney.Context.UnitsAllowed,
                        "Actual spawning ground permits assigned unit in block phase");
                    var fearBoundary=DemoStateCheckpoint.Capture(episode);
                    var fearBefore=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                    var fearOptions=SoloBannerInputs.Legal(episode).Where(x=>x.Kind==SoloBannerKind.Fear).ToArray();
                    CaptureLifecycle(episode,"Banner",fearOptions);
                    Check(fearOptions.Length==episodeJourney.Battle.Enemies.Sum(e=>e.AttackCount)
                        &&fearBefore==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                        "Fear support includes each actual attack without consuming its unit");
                    var fearInput=fearOptions[0];int fearFame=episodePlayer.Fame;
                    var fearCommand=new SoloPolicyCommand(1,episode.Steps,Banner:fearInput);
                    fearCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(fearCommand));
                    fearCommand.Execute(episode);
                    Check(!fearUnit.IsReady&&episodePlayer.Fame==fearFame+1
                        &&episodeJourney.ObserveSoloCombat().Enemies[fearInput.EnemySlot].Attacks[fearInput.AttackIndex].Cancelled
                        &&!episodeJourney.Context.SuccessfullyBlockedAttacks.Contains((fearInput.EnemySlot,fearInput.AttackIndex)),
                        "Fear cancels an attack, exhausts its unit and awards native fame without claiming successful block");
                    var fearAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                    Reject(()=>SoloBannerInputs.Apply(episode,fearInput),"Fear cannot farm fame by repeating the cancelled attack");
                    Check(fearAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Rejected fear reuse preserves resources and score");
                    fearBoundary.Restore();fearCommand.Execute(episode);
                    Check(fearAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Fear replays same actual attack and unit state");
                    wingsBoundary.Restore();
                }
                if(adventureType==PlaceType.Dungeon)
                {
                    var battleBoundary=DemoStateCheckpoint.Capture(episode);
                    foreach(string artifactId in new[]{"items_016","items_009","items_001"})
                    {
                        episodePlayer.Deck.SetDeck(new[]{new DeedCard(artifactId,CardType.Artifact),
                            new DeedCard("basic_card_000",CardType.Action),new DeedCard("basic_card_001",CardType.Action)});
                        episodePlayer.Deck.DrawExact(3);
                        var choices=SoloArtifactInputs.Legal(episode).ToArray();
                        CaptureLifecycle(episode,"Artifact",choices);
                        Check(choices.Length>0,"Actual ranged artifact options exposed: "+artifactId);
                        if(artifactId=="items_009")Check(choices.Count(x=>x.Burn)==6,"All six horn burn bonuses retained");
                        var selected=artifactId=="items_016"?choices.First(x=>!x.Burn&&x.MaterialSlots.Length==2)
                            :artifactId=="items_009"?choices.First(x=>x.Burn&&x.HornBonus==5):choices.First(x=>x.Burn);
                        var originalHand=episodePlayer.Deck.Hand.ToArray();
                        var selectedBoundary=DemoStateCheckpoint.Capture(episode);
                        episode.Apply(j=>
                        {
                            if(artifactId=="items_016")j.Battle.PlayArtifact(originalHand[0],false,null,originalHand.Skip(1).ToArray());
                            else if(artifactId=="items_009")j.Battle.PlayHorn(originalHand[0],true,5);
                            else j.Battle.BurnFearBanner(originalHand[0],selected.EnemySlots);
                        });
                        var expected=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
                        selectedBoundary.Restore();
                        var command=new SoloPolicyCommand(1,episode.Steps,Artifact:selected);
                        command=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(command));
                        command.Execute(episode);
                        Check(expected==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),
                            "Complete combat artifact parameters match native execution and RNG: "+artifactId);
                        battleBoundary.Restore();
                    }
                }
                adventureBoundary.Restore();adventureCommand.Execute(episode);
                Check(encountered.SequenceEqual(episodeJourney.Battle.Enemies.Select(e=>e.Id)),
                    "Restored adventure entry draws the same native enemies: "+adventureType);
                deedBoundary.Restore();
            }
            episode.Apply(j=>j.Player.Deck.SetDeck(new[]{new DeedCard("basic_card_000",CardType.Action),
                new DeedCard("basic_card_001",CardType.Action),new DeedCard("basic_card_002",CardType.Action)}));
            string hiddenInventoryBefore=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloInventory());
            episode.Apply(j=>j.Player.Deck.SetDeck(new[]{new DeedCard("basic_card_002",CardType.Action),
                new DeedCard("basic_card_001",CardType.Action),new DeedCard("basic_card_000",CardType.Action)}));
            Check(hiddenInventoryBefore==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloInventory()),
                "Inventory observation cannot distinguish permutations of hidden draw order");
            episode.Apply(j=>
            {
                j.Player.Deck.SetDeck(Enumerable.Range(0,12).Select(i=>new DeedCard("basic_card_000",CardType.Action)));
                j.Player.Deck.DrawExact(12);
            });
            var largeInventory=episodeJourney.ObserveSoloInventory();
            Check(largeInventory.Hand.Length==12&&largeInventory.Hand.Select(x=>x.Slot).SequenceEqual(Enumerable.Range(0,12))
                &&largeInventory.DrawCount==0,
                "Inventory observation retains all actual hand slots without legacy eight-slot truncation");
            var inventoryPhysical=episodePlayer.Deck.Hand.ToArray();int inventoryStep=episode.Steps;
            episodeJourney.ObserveSoloInventory();
            Check(inventoryPhysical.SequenceEqual(episodePlayer.Deck.Hand)&&episode.Steps==inventoryStep,
                "Visible inventory projection has no card or decision side effects");
            deedBoundary.Restore();
            int basicStep=episode.Steps;var basicHand=episodePlayer.Deck.Hand.ToArray();
            var utilityOptions=SoloBasicOperations.Legal(episode).ToArray();
            Check(episode.Steps==basicStep&&basicHand.SequenceEqual(episodePlayer.Deck.Hand)&&episodePlayer.HeldManaDie==null,
                "Basic resource support probing preserves physical hand, source reservation and decision index");
            var takeDie=utilityOptions.First(x=>x.Kind==SoloBasicKind.SourceDie&&!x.Extra);
            var actualDie=episodeJourney.Context.ManaSource.Dice[takeDie.Slot];
            string sourceBefore=SoloStateDigest.Compute(episode);
            var publicSource=episode.Observe().Inventory.Source;
            Check(publicSource.Available.Select(d=>d.Face).SequenceEqual(episodeJourney.Context.ManaSource.Dice.Select(d=>d.Face.ToString()))
                &&publicSource.Available.Select(d=>d.Slot).SequenceEqual(Enumerable.Range(0,publicSource.Available.Length)),
                "Public source exposes exact current dice faces and physical slots");
            Check(!publicSource.UsedThisTurn&&publicSource.ExtraDice==episodeJourney.Context.ExtraManaDice
                &&publicSource.ExtraBlackDice==episodeJourney.Context.ExtraBlackManaDice,"Public source reports actual remaining quota state");
            Check(sourceBefore==SoloStateDigest.Compute(episode),"Source observation does not roll dice or change private state");
            int priorMana=episodePlayer.Mana.Tokens.TryGetValue(takeDie.Color.Value,out var previousTokenCount)?previousTokenCount:0;
            takeDie=System.Text.Json.JsonSerializer.Deserialize<SoloBasicInput>(System.Text.Json.JsonSerializer.Serialize(takeDie));
            SoloBasicOperations.Apply(episode,takeDie);
            Check(ReferenceEquals(episodePlayer.HeldManaDie,actualDie)
                &&episodePlayer.Mana.Tokens[takeDie.Color.Value]==priorMana+1,
                "Serialized source action reserves the actual die and produces its legal selected mana");
            Check(!SoloBasicOperations.Legal(episode).Any(x=>x.Kind==SoloBasicKind.SourceDie&&!x.Extra),
                "Ordinary source-die quota cannot be consumed twice");
            var sourceAfter=episode.Observe().Inventory.Source;
            Check(sourceAfter.UsedThisTurn&&sourceAfter.Available.Length==publicSource.Available.Length-1
                &&sourceAfter.TurnHeld.Any(d=>d.OwnedByPlayer&&d.Face==actualDie.Face.ToString()),
                "Taken source die moves from available to publicly owned turn-held zone");
            deedBoundary.Restore();
            var sideways=utilityOptions.First(x=>x.Kind==SoloBasicKind.SidewaysMovement);
            var sidewaysCard=episodePlayer.Deck.Hand[sideways.Slot];int movementBefore=episodeJourney.Context.MovementPool;
            SoloBasicOperations.Apply(episode,sideways);
            Check(episodeJourney.Context.MovementPool==movementBefore+1&&!episodePlayer.Deck.Hand.Any(c=>ReferenceEquals(c,sidewaysCard)),
                "Actual sideways move consumes its card and yields native one movement");
            deedBoundary.Restore();
            foreach(string parameterCard in new[]{"basic_card_009","basic_card_013","basic_card_023"})
            {
                episode.Apply(j=>
                {
                    j.Player.Deck.SetDeck(new[]{new DeedCard(parameterCard,CardType.Action),new DeedCard("basic_card_000",CardType.Action)});
                    j.Player.Deck.DrawExact(2);
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddToken(color,3);
                });
                var beforeBasicHand=episodePlayer.Deck.Hand.ToArray();int beforeBasicStep=episode.Steps;
                var basicOptions=SoloDeedInputs.BasicChoices(episode).Where(x=>episodePlayer.Deck.Hand[x.HandSlot].Id==parameterCard).ToArray();
                Check(basicOptions.Length>0&&beforeBasicHand.SequenceEqual(episodePlayer.Deck.Hand)&&episode.Steps==beforeBasicStep,
                    "Parameterized original basic support exists without consuming physical hand or episode index: "+parameterCard);
                var parameterized=basicOptions.First(x=>parameterCard=="basic_card_023"?x.Inputs.HandSlots.Length==1:x.Choice=="strong");
                int expectedDice=parameterCard=="basic_card_013"?2:1;
                Check(parameterCard=="basic_card_023"?parameterized.Inputs.HandSlots.Single()!=parameterized.HandSlot
                    :parameterized.Inputs.DiceSlots.Distinct().Count()==expectedDice&&parameterized.Inputs.Colors.Length==expectedDice,
                    "Complete basic parameters bind distinct actual dice or a different material card: "+parameterCard);
                parameterized=System.Text.Json.JsonSerializer.Deserialize<SoloDeedInput>(System.Text.Json.JsonSerializer.Serialize(parameterized));
                SoloDeedInputs.Apply(episode,parameterized);
                Check(!episodePlayer.Deck.Hand.Any(c=>ReferenceEquals(c,beforeBasicHand[parameterized.HandSlot])),
                    "Parameterized policy deed consumes its actual played card: "+parameterCard);
                deedBoundary.Restore();
            }
            bool deedCompared=false;
            var repelSkill=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Tovak").SkillPoolId)
                .Single(s=>s.SourceId=="hero_skill_pool_007:07");
            episode.Apply(j=>j.Player.Skills.Add(repelSkill));
            int repelSlot=episodePlayer.Skills.IndexOf(repelSkill);
            int repelMovement=episodeJourney.Context.MovementPool;var repelHand=episodePlayer.Deck.Hand.ToArray();
            var sidewaysSkills=SoloSkillInputs.SidewaysLegal(episode).Where(x=>x.SkillSlot==repelSlot).ToArray();
            Check(sidewaysSkills.Any(x=>x.Sideways==SidewaysType.Move&&x.ForgoSourceDice)
                &&repelHand.SequenceEqual(episodePlayer.Deck.Hand)&&episodeJourney.Context.MovementPool==repelMovement,
                "Complete sideways skill probing preserves hand and includes source-die-forgoing alternative");
            var repel=sidewaysSkills.First(x=>x.Sideways==SidewaysType.Move&&x.ForgoSourceDice);
            var repelCard=episodePlayer.Deck.Hand[repel.HandSlot];
            var repelCommand=new SoloPolicyCommand(1,episode.Steps,Skill:repel);
            repelCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(repelCommand));
            repelCommand.Execute(episode);
            Check(episodeJourney.Context.MovementPool==repelMovement+3&&!episodePlayer.Deck.Hand.Any(c=>ReferenceEquals(c,repelCard))
                &&episodeJourney.ObserveSoloInventory().Skills[repelSlot].UsedThisTurn,
                "Skill and actual sideways card are consumed together for native three movement");
            Check(!SoloBasicOperations.Legal(episode).Any(x=>x.Kind==SoloBasicKind.SourceDie)
                &&episodeJourney.ObserveSoloInventory().ForgoSourceDice
                &&!SoloSkillInputs.SidewaysLegal(episode).Any(x=>x.SkillSlot==repelSlot),
                "Forgoing source dice and once-per-turn sideways use remain enforced after command");
            deedBoundary.Restore();
            // A legitimately learnable common skill on this fixture hero; not a claim of initial ownership.
            var flightSkill=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Goldyx").SkillPoolId)
                .Single(s=>s.SourceId=="hero_skill_pool_005:06");
            episode.Apply(j=>{j.Player.Skills.Add(flightSkill);j.Context.MovementPool=2;});
            int flightSlot=episodePlayer.Skills.IndexOf(flightSkill);var flightStart=episodePlayer.Position;
            int flightStep=episode.Steps;
            var flights=SoloSpatialCommands.Legal(episodeJourney,episodeTiles).Where(x=>x.Kind==SoloSpatialKind.Fly).ToArray();
            Check(flights.Length>0&&episodePlayer.Position.Equals(flightStart)&&episodeJourney.Context.MovementPool==2
                &&episode.Steps==flightStep&&!episodeJourney.ObserveSoloInventory().Skills[flightSlot].UsedThisRound,
                "Flight support probing preserves map position, actual movement and once-per-round use");
            var flight=flights.First();var flightBoundary=DemoStateCheckpoint.Capture(episode);
            episodeJourney.Fly(flightSkill,new AxialCoord(flight.Q,flight.R));
            var flightExpected=episodePlayer.Position;int flightCostResult=episodeJourney.Context.MovementPool;
            flightBoundary.Restore();
            var flightCommand=new SoloPolicyCommand(1,episode.Steps,Spatial:flight);
            flightCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(flightCommand));
            flightCommand.Execute(episode);
            Check(episodePlayer.Position.Equals(flightExpected)&&episodeJourney.Context.MovementPool==flightCostResult
                &&episodeJourney.ObserveSoloInventory().Skills[flightSlot].UsedThisRound,
                "Serialized flight matches native actual landing/cost and consumes the skill");
            Check(!SoloSpatialCommands.Legal(episodeJourney,episodeTiles).Any(x=>x.Kind==SoloSpatialKind.Fly),
                "Used flight is absent from legal support for the rest of the round");
            deedBoundary.Restore();
            var ownMovementSkill=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get("Tovak").SkillPoolId)
                .Single(s=>s.SourceId=="hero_skill_pool_007:00");
            episode.Apply(j=>j.Player.Skills.Add(ownMovementSkill));
            int ownSkillSlot=episodePlayer.Skills.IndexOf(ownMovementSkill);
            var skillBoundary=DemoStateCheckpoint.Capture(episode);
            episodeJourney.UseOutsideSkill(ownMovementSkill,0);
            int directSkillMovement=episodeJourney.Context.MovementPool;
            skillBoundary.Restore();
            int skillProbeStep=episode.Steps,skillProbeMovement=episodeJourney.Context.MovementPool;
            var ownSkillChoices=SoloSkillInputs.Legal(episode).Where(x=>x.SkillSlot==ownSkillSlot).ToArray();
            Check(ownSkillChoices.Length==1&&episode.Steps==skillProbeStep&&episodeJourney.Context.MovementPool==skillProbeMovement
                &&!episodeJourney.ObserveSoloInventory().Skills[ownSkillSlot].UsedThisTurn,
                "Actual owned hero skill support probing preserves resources and frequency state");
            var skillCommand=new SoloPolicyCommand(1,episode.Steps,Skill:ownSkillChoices.Single());
            skillCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(skillCommand));
            skillCommand.Execute(episode);
            Check(episodeJourney.Context.MovementPool==directSkillMovement&&directSkillMovement>skillProbeMovement
                &&episodeJourney.ObserveSoloInventory().Skills[ownSkillSlot].UsedThisTurn,
                "Serialized own hero skill matches native movement and reports consumed frequency");
            Check(!SoloSkillInputs.Legal(episode).Any(x=>x.SkillSlot==ownSkillSlot),
                "Consumed once-per-turn hero skill cannot produce repeated free movement");
            deedBoundary.Restore();
            var scoutCard=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit)
                .First(u=>u.Actions.Any(a=>a.Kind==UnitAbilityKind.ScoutToken));
            var scoutUnit=new UnitState(scoutCard);
            episode.Apply(j=>j.Player.Units.Add(scoutUnit));
            var scoutDraw=episodeJourney.Context.EnemySupply.DrawPile.ToArray();
            var scoutInUse=episodeJourney.Context.EnemySupply.InUse.ToArray();
            var scoutPrior=episodeJourney.Context.LastScoutObservation;
            var scoutWorldBefore=System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles));
            int scoutStep=episode.Steps;
            var scoutOptions=SoloUnitInputs.Legal(episode).Where(x=>x.Scout!=null).ToArray();
            Check(scoutOptions.Any(x=>x.Scout.SupplyColor.HasValue)&&scoutUnit.IsReady&&episode.Steps==scoutStep
                &&ReferenceEquals(scoutPrior,episodeJourney.Context.LastScoutObservation)
                &&scoutDraw.SequenceEqual(episodeJourney.Context.EnemySupply.DrawPile),
                "Scouting support does not disclose or consume a hidden supply face during probing");
            Check(scoutWorldBefore==System.Text.Json.JsonSerializer.Serialize(episodeJourney.ObserveSoloWorld(episodeTiles)),
                "Policy world observation is unchanged by scouting candidate enumeration");
            var scoutInput=scoutOptions.First(x=>x.Scout.SupplyColor.HasValue);
            var scoutExpected=scoutDraw.First(m=>m.TokenColor==scoutInput.Scout.SupplyColor.Value);
            var scoutCommand=new SoloPolicyCommand(1,episode.Steps,Unit:scoutInput);
            scoutCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(scoutCommand));
            scoutCommand.Execute(episode);
            Check(!scoutUnit.IsReady&&ReferenceEquals(episodeJourney.Context.LastScoutObservation.Enemy,scoutExpected)
                &&scoutDraw.SequenceEqual(episodeJourney.Context.EnemySupply.DrawPile)
                &&scoutInUse.SequenceEqual(episodeJourney.Context.EnemySupply.InUse),
                "Actual serialized scouting reveals the physical top token privately without drawing it");
            var learnedScout=episodeJourney.ObserveSoloWorld(episodeTiles).LastScouting;
            Check(learnedScout!=null&&learnedScout.EnemyId==scoutExpected.Id&&learnedScout.Q==null&&learnedScout.R==null
                &&learnedScout.EnemyColor==scoutExpected.TokenColor.ToString()&&learnedScout.GuardSlot==null,
                "Policy receives only the legitimately scouted supply token, without invented map location");
            Check(!SoloUnitInputs.Legal(episode).Any(x=>x.Scout!=null),
                "Exhausted scout cannot repeatedly reveal hidden tokens");
            deedBoundary.Restore();
            var movementUnitCard=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit)
                .First(u=>u.Actions.Any(a=>a.Kind==UnitAbilityKind.Movement&&a.Cost.Length==0));
            var movementUnit=new UnitState(movementUnitCard);
            episode.Apply(j=>j.Player.Units.Add(movementUnit));
            int movementUnitSlot=episodePlayer.Units.IndexOf(movementUnit);
            int movementAbility=Array.FindIndex(movementUnitCard.Actions,a=>a.Kind==UnitAbilityKind.Movement&&a.Cost.Length==0);
            int unitProbeStep=episode.Steps,unitMoveBefore=episodeJourney.Context.MovementPool;
            var unitChoices=SoloUnitInputs.Legal(episode).ToArray();
            Check(movementUnit.IsReady&&episode.Steps==unitProbeStep&&episodeJourney.Context.MovementPool==unitMoveBefore,
                "Unit support probing preserves actual readiness, movement and episode step");
            var unitMove=unitChoices.Single(x=>x.UnitSlot==movementUnitSlot&&x.Ability==movementAbility);
            var unitCommand=new SoloPolicyCommand(1,episode.Steps,Unit:unitMove);
            unitCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(unitCommand));
            unitCommand.Execute(episode);
            Check(!movementUnit.IsReady&&episodeJourney.Context.MovementPool==unitMoveBefore+movementUnitCard.Actions[movementAbility].Value,
                "Serialized actual unit movement consumes readiness and supplies printed movement");
            Check(!SoloUnitInputs.Legal(episode).Any(x=>x.UnitSlot==movementUnitSlot),
                "Exhausted unit cannot repeatedly produce resources");
            int unitUsedStep=episode.Steps,unitUsedMovement=episodeJourney.Context.MovementPool;
            Reject(()=>SoloUnitInputs.Apply(episode,unitMove),"Repeated exhausted unit activation rejected");
            Check(episode.Steps==unitUsedStep&&episodeJourney.Context.MovementPool==unitUsedMovement&&!movementUnit.IsReady,
                "Rejected unit reuse rolls back without reward or resource farming");
            episodePlayer.Deck.SetDeck(new[]{new DeedCard("items_003",CardType.Artifact)});
            episodePlayer.Deck.DrawExact(1);
            var courageCard=episodePlayer.Deck.Hand[0];
            episode.Apply(j=>BannerActions.AssignCourage(j.Player,movementUnit,courageCard));
            var courageBoundary=DemoStateCheckpoint.Capture(episode);
            var courageBefore=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            var courageChoice=SoloBannerInputs.Legal(episode).Single(x=>x.Kind==SoloBannerKind.Courage&&x.UnitSlot==movementUnitSlot);
            CaptureLifecycle(episode,"Banner",new[]{courageChoice});
            Check(courageBefore==System.Text.Json.JsonSerializer.Serialize(episode.Observe())&&!movementUnit.IsReady
                &&!movementUnit.CourageBannerSpent,"Banner support probes do not spend readiness or per-round use");
            var courageCommand=new SoloPolicyCommand(1,episode.Steps,Banner:courageChoice);
            courageCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(courageCommand));
            courageCommand.Execute(episode);
            Check(movementUnit.IsReady&&movementUnit.CourageBannerSpent&&ReferenceEquals(movementUnit.Banner,courageCard),
                "Serialized courage activation readies its actual unit and retains assigned physical banner");
            Check(episodeJourney.ObserveSoloInventory().Units[movementUnitSlot].CourageSpent,
                "Policy observation exposes the spent flag of the actual assigned banner");
            var courageAfter=System.Text.Json.JsonSerializer.Serialize(episode.Observe());
            Check(!SoloBannerInputs.Legal(episode).Any(x=>x.Kind==SoloBannerKind.Courage&&x.UnitSlot==movementUnitSlot),
                "Spent courage banner cannot repeatedly ready units in the same round");
            Reject(()=>SoloBannerInputs.Apply(episode,courageChoice),"Repeated courage activation rejects");
            Check(courageAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Rejected banner reuse leaves state intact");
            courageBoundary.Restore();courageCommand.Execute(episode);
            Check(courageAfter==System.Text.Json.JsonSerializer.Serialize(episode.Observe()),"Courage command replays exactly from saved boundary");
            deedBoundary.Restore();
            episode.Apply(j=>
            {
                j.Player.Deck.SetDeck(new[]{new DeedCard("magic_002",CardType.Spell),new DeedCard("basic_card_000",CardType.Action),
                    new DeedCard("basic_card_001",CardType.Action),new DeedCard("basic_card_002",CardType.Action)});
                j.Player.Deck.DrawExact(4);
                foreach(var c in j.Player.Deck.Hand.Where(c=>c.Type!=CardType.Spell).ToArray())j.Player.Deck.TryDiscardExact(c);
                j.Context.DayPart=DayPart.Night;
                foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black})j.Player.Mana.AddToken(color,3);
            });
            var discardSpellBefore=episodePlayer.Deck.DiscardPile.ToArray();int spellProbeStep=episode.Steps;
            var meditation=SoloDeedInputs.SpellChoices(episode).Where(x=>x.Choice=="strong_top").ToArray();
            if(meditation.Length!=6)
            {
                Console.WriteLine($"Meditation diagnostic: options={meditation.Length}, discard={episodePlayer.Deck.DiscardPile.Count}, day={episodeJourney.Context.DayPart}");
                var diagnostic=DemoStateCheckpoint.Capture(episode);
                try {SoloDeedInputs.Apply(episode,new(0,"strong_top",Inputs:new(DiscardSlots:new[]{0,1})));}
                catch(Exception ex){Console.WriteLine(ex.ToString());}
                finally {diagnostic.Restore();}
            }
            Check(meditation.Length==6&&episode.Steps==spellProbeStep&&discardSpellBefore.SequenceEqual(episodePlayer.Deck.DiscardPile),
                "Meditation support retains all six ordered pairs of three physical discard cards without mutation");
            var meditationInput=System.Text.Json.JsonSerializer.Deserialize<SoloDeedInput>(System.Text.Json.JsonSerializer.Serialize(meditation[0]));
            var selectedDiscard=meditationInput.Inputs.DiscardSlots.Select(i=>discardSpellBefore[i]).ToArray();
            SoloDeedInputs.Apply(episode,meditationInput);
            Check(episodePlayer.Deck.DrawPile.Take(2).SequenceEqual(selectedDiscard)&&episodePlayer.Deck.DiscardPile.Count==1,
                "Serialized strong meditation returns the exact physical cards in the selected deck order");
            deedBoundary.Restore();
            episode.Apply(j=>
            {
                j.Player.Deck.SetDeck(new[]{new DeedCard("magic_005",CardType.Spell)});j.Player.Deck.DrawExact(1);
                j.Context.DayPart=DayPart.Night;
                var unit=new UnitState(UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).OrderBy(u=>u.Level).First());
                unit.Exhaust();j.Player.Units.Add(unit);
                foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black})j.Player.Mana.AddToken(color,3);
            });
            var unitSpell=SoloDeedInputs.SpellChoices(episode).First(x=>x.Choice=="strong"&&x.Inputs.UnitSlots?.Length==1);
            Check(!episodePlayer.Units[0].IsReady,"Unit spell candidate probing does not ready its target");
            SoloDeedInputs.Apply(episode,unitSpell);
            Check(episodePlayer.Units[0].IsReady,"Spell unit-subset input actually readies the selected owned unit");
            deedBoundary.Restore();
            foreach(bool enhancedLearning in new[]{false,true})
            {
                episode.Apply(j=>
                {
                    j.Player.Deck.SetDeck(new[]{new DeedCard("advanced_card_026",CardType.Action)});
                    j.Player.Deck.DrawExact(1);j.Player.Mana.AddToken(ManaColor.White,1);
                    j.Context.InfluencePool=0;
                });
                Check(!SoloLearningInputs.Legal(episode).Any(),"No learning purchase before native ability granted");
                var learningCard=SoloDeedInputs.AdvancedChoices(episode).First(x=>x.Choice==(enhancedLearning?"strong":"base"));
                SoloDeedInputs.Apply(episode,learningCard);
                Check(episode.Observe().Offers.LearningModes==(enhancedLearning?2:1),"Printed learning mode is public");
                Check(!SoloLearningInputs.Legal(episode).Any(),"Learning cannot buy without sufficient influence");
                episode.Apply(j=>j.Context.InfluencePool=9);
                string learningBefore=SoloStateDigest.Compute(episode);
                var learningInputs=SoloLearningInputs.Legal(episode).ToArray();
                CaptureLifecycle(episode,"Learning",learningInputs);
                Check(learningInputs.Length==episodeJourney.Game.Globals.AdvActions.Offer.Count
                    &&learningInputs.All(x=>x.Enhanced==enhancedLearning),"Every actual offer and only granted mode selectable");
                Check(SoloStateDigest.Compute(episode)==learningBefore,"Learning probe preserves private state");
                var purchase=learningInputs[0];
                string learnedId=episodeJourney.Game.Globals.AdvActions.Offer[purchase.OfferSlot].Id;
                var learningBoundary=DemoStateCheckpoint.Capture(episode);
                new SoloPolicyCommand(1,episode.Steps,Learning:purchase).Execute(episode);
                Check(episodeJourney.Context.InfluencePool==(enhancedLearning?0:3),"Exact native learning influence fee paid");
                Check((enhancedLearning?episodePlayer.Deck.Hand:episodePlayer.Deck.DiscardPile).Any(c=>c.Id==learnedId),
                    "Learned card reaches printed destination");
                Check(episode.Observe().Offers.LearningUsed&&!SoloLearningInputs.Legal(episode).Any(),"Once per turn learning marked and exhausted");
                string learningAfter=SoloStateDigest.Compute(episode);
                Reject(()=>SoloLearningInputs.Apply(episode,purchase),"Repeated learning purchase rejected");
                learningBoundary.Restore();
                SoloLearningInputs.Apply(episode,System.Text.Json.JsonSerializer.Deserialize<SoloLearningInput>(System.Text.Json.JsonSerializer.Serialize(purchase)));
                Check(SoloStateDigest.Compute(episode)==learningAfter,"Learning purchase serialized replay exact");
                deedBoundary.Restore();
            }
            foreach(bool enhancedCopy in new[]{false,true})
            {
                episode.Apply(j=>
                {
                    j.Player.Deck.SetDeck(new[]{new DeedCard("advanced_card_024",CardType.Action),new DeedCard("basic_card_001",CardType.Action)});
                    j.Player.Deck.DrawExact(2);
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddToken(color,3);
                });
                string overloadBefore=SoloStateDigest.Compute(episode);
                var overload=SoloDeedInputs.AdvancedChoices(episode).First(x=>x.Choice==(enhancedCopy?"strong":"base"));
                Check(SoloStateDigest.Compute(episode)==overloadBefore,"Overload probing preserves private state");
                SoloDeedInputs.Apply(episode,overload);
                Check(episodeJourney.Context.PendingRepeatedEffect?.Remaining==(enhancedCopy?1:2),"Overload retains native repeat count");
                Check(!episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile).Concat(episodePlayer.Deck.DiscardPile)
                    .Any(c=>c.Id=="basic_card_001"),"Overload permanently removes physical donor");
                var locked=SoloPolicyCommand.ImplementedChoices(episode).ToArray();
                Check(locked.Length>0&&locked.All(c=>c.Repeated!=null),"Only committed repeat resolutions offered");
                Reject(()=>new SoloPolicyCommand(1,episode.Steps,Deed:overload).Execute(episode),"Cannot play another card during repeat");
                while(episodeJourney.Context.PendingRepeatedEffect!=null)
                {
                    var observed=episode.Observe().Offers.RepeatedEffect;
                    Check(observed!=null&&observed.CardId=="basic_card_001","Pending donor and count publicly observable");
                    string repeatedBefore=SoloStateDigest.Compute(episode);
                    var repeat=SoloRepeatedInputs.Legal(episode).First();
                    CaptureLifecycle(episode,"Repeated",new[]{repeat});
                    Check(SoloStateDigest.Compute(episode)==repeatedBefore,"Continuation probe preserves exact private state");
                    var repeatBoundary=DemoStateCheckpoint.Capture(episode);
                    SoloRepeatedInputs.Apply(episode,repeat);
                    string repeatedAfter=SoloStateDigest.Compute(episode);
                    repeatBoundary.Restore();
                    SoloRepeatedInputs.Apply(episode,System.Text.Json.JsonSerializer.Deserialize<SoloRepeatedInput>(System.Text.Json.JsonSerializer.Serialize(repeat)));
                    Check(SoloStateDigest.Compute(episode)==repeatedAfter,"Continuation serialized replay exact");
                }
                Check(episode.Observe().Offers.RepeatedEffect==null,"Obligation clears only after final repeat");
                deedBoundary.Restore();
            }
            foreach(bool woundToDiscard in new[]{false,true})
            {
                episode.Apply(j=>
                {
                    j.Player.Deck.SetDeck(new[]{new DeedCard("advanced_card_030",CardType.Action)});
                    j.Player.Deck.DrawExact(1);
                    j.Game.Globals.AdvActions.Offer.Clear();
                    j.Game.Globals.AdvActions.Offer.Add(new AdvActionCard("advanced_card_006"));
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddToken(color,3);
                });
                string copyBefore=SoloStateDigest.Compute(episode);
                var copies=SoloDeedInputs.AdvancedChoices(episode).Where(x=>x.Choice=="strong").ToArray();
                Check(SoloStateDigest.Compute(episode)==copyBefore,"Bloodline copy support preserves private state and RNG");
                Check(copies.Any(x=>x.Inputs.WoundToDiscard)&&copies.Any(x=>!x.Inputs.WoundToDiscard),
                    "Bloodline exposes both native wound destinations");
                var copied=copies.First(x=>x.Inputs.WoundToDiscard==woundToDiscard);
                var copyBoundary=DemoStateCheckpoint.Capture(episode);
                int beforeMovement=episodeJourney.Context.MovementPool;
                SoloDeedInputs.Apply(episode,copied);
                Check(episodeJourney.Context.MovementPool==beforeMovement+4,"Bloodline executes borrowed printed movement");
                Check(episodeJourney.Game.Globals.AdvActions.Offer.Single().Id=="advanced_card_006",
                    "Borrowed action remains in actual offer");
                Check((woundToDiscard?episodePlayer.Deck.DiscardPile:episodePlayer.Deck.Hand).Count(c=>c.Type==CardType.Wound)==1,
                    "Bloodline wound enters chosen physical zone");
                string copyAfter=SoloStateDigest.Compute(episode);
                copyBoundary.Restore();
                SoloDeedInputs.Apply(episode,System.Text.Json.JsonSerializer.Deserialize<SoloDeedInput>(System.Text.Json.JsonSerializer.Serialize(copied)));
                Check(SoloStateDigest.Compute(episode)==copyAfter,"Serialized bloodline effect has exact private replay");
                deedBoundary.Restore();
            }
            foreach(string advancedId in new[]{"advanced_card_020","advanced_card_030","advanced_card_033"})
            {
                episode.Apply(j=>
                {
                    j.Player.Deck.SetDeck(new[]{new DeedCard(advancedId,CardType.Action),new DeedCard("basic_card_000",CardType.Action)});
                    j.Player.Deck.DrawExact(2);
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})j.Player.Mana.AddToken(color,3);
                });
                var actualAdvanced=episodePlayer.Deck.Hand.First(c=>c.Id==advancedId);
                int advancedProbeStep=episode.Steps;
                var advancedOfferBefore=episodeJourney.Game.Globals.AdvActions.Offer.ToArray();
                var spellOfferBefore=episodeJourney.Game.Globals.Spells.Offer.ToArray();
                var advancedOptions=SoloDeedInputs.AdvancedChoices(episode).ToArray();
                Check(advancedOptions.Length>0&&episode.Steps==advancedProbeStep
                    &&advancedOfferBefore.SequenceEqual(episodeJourney.Game.Globals.AdvActions.Offer)
                    &&spellOfferBefore.SequenceEqual(episodeJourney.Game.Globals.Spells.Offer)
                    &&episodePlayer.Deck.Hand.Any(c=>ReferenceEquals(c,actualAdvanced)),
                    "Advanced support probe preserves actual offers, card and episode boundary: "+advancedId);
                var selectedAdvanced=System.Text.Json.JsonSerializer.Deserialize<SoloDeedInput>(System.Text.Json.JsonSerializer.Serialize(advancedOptions[0]));
                var removedMaterial=advancedId=="advanced_card_020"?episodePlayer.Deck.Hand[selectedAdvanced.Inputs.HandSlots[0]]:null;
                var gainedAdvancedId=advancedId=="advanced_card_030"?advancedOfferBefore[selectedAdvanced.Inputs.AdvancedSlot].Id:null;
                int crystalsBefore=episodePlayer.Mana.Crystals.Values.Sum();
                SoloDeedInputs.Apply(episode,selectedAdvanced);
                Check(!episodePlayer.Deck.Hand.Any(c=>ReferenceEquals(c,actualAdvanced)),
                    "Serialized advanced choice consumes actual advanced card: "+advancedId);
                if(advancedId=="advanced_card_030")
                    Check(episodePlayer.Wounds==1&&episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile)
                        .Concat(episodePlayer.Deck.DiscardPile).Any(c=>c.Id==gainedAdvancedId),
                        "Bloodline acquisition gains actual offered action and retains its real wound cost");
                if(advancedId=="advanced_card_020")
                    Check(!episodePlayer.Deck.Hand.Concat(episodePlayer.Deck.DrawPile).Concat(episodePlayer.Deck.DiscardPile)
                        .Concat(episodePlayer.Deck.PlayedCards).Any(c=>ReferenceEquals(c,removedMaterial)),
                        "Crystallize removes its selected actual material from all owned deck zones");
                if(advancedId=="advanced_card_033")
                    Check(episodePlayer.Mana.Crystals.Values.Sum()>crystalsBefore,
                        "Spell forge selected actual offer produces native crystal reward");
                deedBoundary.Restore();
            }
            episode.Apply(j=>
            {
                j.Player.Deck.SetDeck(new[]{new DeedCard("basic_card_018",CardType.Action),new DeedCard("basic_card_000",CardType.Action)});
                j.Player.Deck.DrawExact(2);
                j.Player.Mana.AddToken(ManaColor.Green,3);
            });
            var pathOptions=SoloDeedInputs.BasicChoices(episode).Where(x=>episodePlayer.Deck.Hand[x.HandSlot].Id=="basic_card_018").ToArray();
            Check(pathOptions.Where(x=>x.Choice=="base").Select(x=>x.TargetHex.Value).ToHashSet()
                .SetEquals(episodeJourney.Game.Map.Placed.Keys),"Druidic paths offers every actual revealed hex instead of dropping its base effect");
            var pathOption=pathOptions.First(x=>x.Choice=="base");
            SoloDeedInputs.Apply(episode,pathOption);
            Check(episodeJourney.Context.HexMoveReduction.ContainsKey(pathOption.TargetHex.Value)
                &&episodeJourney.Context.MovementPool>0,"Chosen original terrain card applies actual movement and selected hex reduction");
            deedBoundary.Restore();
            episode.Apply(j=>j.Player.Deck.TryDiscardExact(j.Player.Deck.Hand[0]));
            var discardReference=episodePlayer.Deck.DiscardPile[0];
            var discardInput=new SoloDeedSelection(DiscardSlots:new[]{0});
            discardInput=System.Text.Json.JsonSerializer.Deserialize<SoloDeedSelection>(System.Text.Json.JsonSerializer.Serialize(discardInput));
            Check(ReferenceEquals(discardInput.Resolve(episodeJourney).Cards[0],discardReference),
                "Serialized discard selection resolves to the exact discard object, not the same-index hand card");
            Reject(()=>new SoloDeedSelection(HandSlots:new[]{0},DiscardSlots:new[]{0}).Resolve(episodeJourney),
                "Ambiguous mixed hand/discard material selection rejects");
            deedBoundary.Restore();
            episode.Apply(j=>
            {
                j.Player.Deck.SetDeck(new[]{new DeedCard("basic_card_005",CardType.Action)});
                j.Player.Deck.DrawExact(1);
                var unit=new UnitState(UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).OrderBy(u=>u.Level).First());
                unit.Exhaust();j.Player.Units.Add(unit);
                j.Player.Mana.AddToken(ManaColor.Green,3);
            });
            var readyOptions=SoloDeedInputs.BasicChoices(episode).Where(x=>x.Inputs.UnitSlot>=0).ToArray();
            Check(readyOptions.Length>0&&!episodePlayer.Units[0].IsReady,"Original ready-unit deed retains an actual exhausted unit target in legal support");
            SoloDeedInputs.Apply(episode,readyOptions[0]);
            Check(episodePlayer.Units[0].IsReady,"Serialized ready-unit deed reaches the native unit consumer");
            deedBoundary.Restore();
            string DeedOutcome()=>System.Text.Json.JsonSerializer.Serialize(new {
                hand=episodePlayer.Deck.Hand.Select(c=>c.Id).ToArray(),
                draw=episodePlayer.Deck.DrawPile.Select(c=>c.Id).ToArray(),
                discard=episodePlayer.Deck.DiscardPile.Select(c=>c.Id).ToArray(),
                played=episodePlayer.Deck.PlayedCards.Select(c=>c.Id).ToArray(),
                episodePlayer.Fame,episodePlayer.Wounds,episodeJourney.Context.MovementPool,
                episodeJourney.Context.InfluencePool,healing=episodeJourney.Context.Healing.Total,
                world=episodeJourney.ObserveSoloWorld(episodeTiles),episode.Steps});
            for(int handSlot=0;handSlot<episodePlayer.Deck.Hand.Count&&!deedCompared;handSlot++)
            foreach(var choice in episodeJourney.OutsideChoices(episodePlayer.Deck.Hand[handSlot]))
            {
                string directOutcome;double directReward;
                try
                {
                    var direct=episode.Apply(j=>j.PlayOutsideWithInputs(j.Player.Deck.Hand[handSlot],choice.Id,null));
                    directReward=direct.Reward;directOutcome=DeedOutcome();
                }
                catch(InvalidOperationException){deedBoundary.Restore();continue;}
                deedBoundary.Restore();
                var deedCommand=new SoloPolicyCommand(1,episode.Steps,Deed:new(handSlot,choice.Id));
                deedCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(deedCommand));
                var viaCommand=deedCommand.Execute(episode);
                Check(viaCommand.Reward==directReward&&DeedOutcome()==directOutcome,
                    "Serialized actual starting-hand deed matches native play, physical zones, resources and world clock");
                deedCompared=true;deedBoundary.Restore();break;
            }
            Check(deedCompared,"At least one actual original starting-hand deed exercised through the policy envelope");
            var resolvedMaterials=new SoloDeedSelection(HandSlots:new[]{0}).Resolve(episodeJourney);
            Check(ReferenceEquals(resolvedMaterials.Cards[0],episodePlayer.Deck.Hand[0]),
                "Serialized material slot resolves to the exact physical hand object");
            string beforeRejectedDeed=DeedOutcome();
            Reject(()=>SoloDeedInputs.Apply(episode,new(episodePlayer.Deck.Hand.Count,"base")),"Deed rejects stale out-of-range physical slot");
            Check(DeedOutcome()==beforeRejectedDeed,"Rejected deed preserves physical zones, resources and episode boundary");
            deedBoundary.Restore();
            episode.Apply(j=>j.Player.Wounds=2);
            var beforeHealing=DemoStateCheckpoint.Capture(episode);
            episode.Apply(j=>{j.Context.Healing.Add(1);j.Context.Healing.Add(1,true);});
            var healingChoices=SoloHealingInputs.Legal(episode).ToArray();
            CaptureLifecycle(episode,"Healing",healingChoices);
            Check(healingChoices.Length==2 && episodePlayer.Wounds==2 && episodeJourney.Context.Healing.Total==2,
                "Healing choices preserve actual wounds and distinguish Grail source priority without mutation");
            int healingFame=episodePlayer.Fame;
            var healCommand=new SoloPolicyCommand(1,episode.Steps,Healing:new(-1,false));
            healCommand=System.Text.Json.JsonSerializer.Deserialize<SoloPolicyCommand>(System.Text.Json.JsonSerializer.Serialize(healCommand));
            var healed=healCommand.Execute(episode);
            Check(episodePlayer.Wounds==1 && episodePlayer.Fame==healingFame && Math.Abs(healed.Reward-.02)<1e-12,
                "Ordinary healing consumes a real hand wound and only recovers its official inventory penalty");
            Reject(()=>healCommand.Execute(episode),"A healing receipt cannot be replayed at a later decision boundary");
            healed=SoloHealingInputs.Apply(episode,new(-1,true));
            Check(episodePlayer.Wounds==0 && episodePlayer.Fame==healingFame+1 && Math.Abs(healed.Reward-.03)<1e-12,
                "Grail healing adds only native spent-point fame and the removed wound score");
            Check(!SoloHealingInputs.Legal(episode).Any(),"Spent healing cannot produce repeated healing rewards");
            beforeHealing.Restore();
            int ordinaryRest=episodePlayer.Deck.Hand.Count(c=>c.Type!=CardType.Wound);
            var rests=SoloRestInputs.Legal(episode).ToArray();
            CaptureLifecycle(episode,"Rest",rests);
            Check(rests.Length==ordinaryRest*4 && episodePlayer.Wounds==2,
                "Rest support includes every ordinary card and every subset of the two actual wounds without mutation");
            var rest=rests.First(r=>r.WoundHandSlots.Length==2);
            rest=System.Text.Json.JsonSerializer.Deserialize<SoloRestInput>(System.Text.Json.JsonSerializer.Serialize(rest));
            var restResult=SoloRestInputs.Apply(episode,rest);
            Check(restResult.Reward==0 && restResult.Score.Inventory.DeckWounds==2
                &&episodePlayer.Wounds==0&&episodePlayer.DiscardWounds==2,
                "Actual rest discards wounds but does not heal them or grant reward");
            Check(!SoloRestInputs.Legal(episode).Any()&&!SoloSpatialCommands.Legal(episodeJourney,episodeTiles).Any(),
                "Rest cannot be repeated or followed by movement/exploration/assault");
            var afterRest=SoloEndTurnInputs.Apply(episode,new(Array.Empty<int>()));
            Check(!episodeJourney.Game.HasActiveTurn && afterRest.Score.Inventory.DeckWounds==2,
                "Completed rest can end without another discard and wounds remain in the full deck");
            siteFixture.Restore();
            var woundSetup=episode.Apply(j=>
            {
                j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.MagicalGlade);
                j.Player.Wounds=1;j.Player.DiscardWounds=1;
            });
            var woundInput=new SoloEndTurnInput(new[]{0},SoloWoundZone.Discard,0);
            var woundResult=SoloEndTurnInputs.Apply(episode,woundInput);
            Check(woundResult.Score.Inventory.DeckWounds==1 && Math.Abs(woundResult.Reward-.02)<1e-12,
                "Glade removes one actual discard wound and refunds exactly its native score cost");
            Check(Math.Abs(woundSetup.Reward+woundResult.Reward+.02)<1e-12,
                "Creating two wounds and removing one has negative net reward, no healing farming bonus");
            int afterSiteStep=episode.Steps;var afterSiteScore=woundResult.Score.Total;
            Reject(()=>SoloEndTurnInputs.Apply(episode,woundInput),"Completed glade turn cannot be submitted twice");
            Check(episode.Steps==afterSiteStep && episodeJourney.CaptureSoloScore(episodeIds).Total==afterSiteScore,
                "Rejected duplicate site receipt does not change reward state");
            siteFixture.Restore();
            episode.Apply(j=>j.Game.Map.Places[j.Player.Position]=new PlaceState(PlaceType.DeepMine)
                {MineColors=new[]{ManaColor.Red,ManaColor.Blue}});
            int mineStep=episode.Steps;
            Reject(()=>SoloEndTurnInputs.Apply(episode,new(new[]{0})),"Multicolor mine requires an explicit actual color");
            Reject(()=>SoloEndTurnInputs.Apply(episode,new(new[]{0},MineColor:ManaColor.White)),"Mine rejects unavailable color");
            Check(episode.Steps==mineStep&&episodeJourney.Game.HasActiveTurn,"Rejected mine choices preserve active turn");
            episode.Apply(j=>j.Player.Mana.AddCrystal(ManaColor.Blue,1));
            var mineResult=SoloEndTurnInputs.Apply(episode,new(new[]{0},MineColor:ManaColor.Blue));
            Check(episodePlayer.Mana.Crystals[ManaColor.Blue]==2 && Math.Abs(mineResult.Reward-.01)<1e-12,
                "Actual selected mine crystal crosses the two-crystal official score threshold once");
            siteFixture.Restore();
            var beforeEndDummy=System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture());
            var beforeEndHand=episodePlayer.Deck.Hand.ToArray();int beforeEndIndex=episode.Steps;
            var endings=SoloEndTurnInputs.Legal(episode).ToArray();
            int ordinaryCount=beforeEndHand.Count(c=>c.Type!=CardType.Wound);
            Check(endings.Length==(1<<ordinaryCount)-1 && endings.All(e=>e.DiscardHandSlots.Length>0),
                "Inactive opening turn offers every nonempty discard subset and rejects a free empty turn");
            Check(beforeEndHand.SequenceEqual(episodePlayer.Deck.Hand)&&beforeEndIndex==episode.Steps
                &&beforeEndDummy==System.Text.Json.JsonSerializer.Serialize(episodeDummy.Capture()),
                "End-turn support probing restores hand, reward index and dummy clock");
            var serializedEnd=System.Text.Json.JsonSerializer.Serialize(endings.Single(e=>e.DiscardHandSlots.SequenceEqual(new[]{0})));
            var selectedEnd=System.Text.Json.JsonSerializer.Deserialize<SoloEndTurnInput>(serializedEnd);
            episodeReturn+=SoloEndTurnInputs.Apply(episode,selectedEnd).Reward;
        }
        else episodeReturn+=SoloEndTurnInputs.Apply(episode,new(new[]{0})).Reward;
    }
    Check(SoloClockCommands.Legal(episode).SequenceEqual(new[]{new SoloClockCommand(SoloClockKind.EndRound)}),
        "Expired round exposes exactly one native end-round transition");
    var boundary=SoloClockCommands.Apply(episode,new(SoloClockKind.EndRound));episodeReturn+=boundary.Reward;
    Check(boundary.Terminal==(round==6),"Only actual scenario boundary terminates episode");
}
Check(episode.Terminal && Math.Abs(episodeReturn-0.01*(episodeJourney.CaptureSoloScore(episodeIds).Total-episode.InitialScore))<1e-12,
    "Whole six-round transaction reward equals native final minus initial score");
var endingPublic=episodeJourney.ObserveSoloWorld(episodeTiles);
Check(testedTacticInputs.SetEquals(new[]{"day_2","day_3","night_5"}),"All three acquisition-input tactics exercised, not merely compiled");
Check(testedTacticAbilities.SetEquals(new[]{"day_6","night_2","night_3","night_4","night_6"}),"Five tactic ability families plus actual reserved-mana consumption explicitly exercised");
Check(endingPublic.ScenarioEnded && endingPublic.Round==6 && endingPublic.DayPart=="Night"
    && endingPublic.ExplorationPositions.Length==0,"Terminal public state retains actual final night and no exploration support");
Reject(()=>episode.Apply(j=>j.BeginTurn()),"Closed episode rejects any further command");
Console.WriteLine($"PASS {checks} with original-map six-round episode transactions; no combat/exploration policy or durable-save acceptance claim.");
foreach(var rule in new[]{SoloDummyDualColorRule.MaximumMatchingCrystals,SoloDummyDualColorRule.SumMatchingCrystals})
{
    var cards=goldyx.StartingCards.Select((id,i)=>new SoloDummyCard("dual_fixture/"+i,id,originalCards[id].RequiredCrystals.Single())).ToArray();
    var deck=new SoloDummyDeck(cards,new[]{0,2,1,0},201,rule);
    deck.CloseRound();
    var dualDefinition=CardJsonLoader.LoadAdvancedActions().Single(c=>c.Id=="advanced_card_040");
    var colors=dualDefinition.RequiredCrystals.ToArray();
    var dual=new SoloDummyCard("dual_fixture/16",dualDefinition.Id,colors[0],colors[1]);
    deck.BeginNextRound(dual,ManaColor.Blue);
    var saved=deck.Capture();
    var order=cards.Take(2).Concat(new[]{dual}).Concat(cards.Skip(2)).ToArray();
    deck=SoloDummyDeck.Restore(saved with {Draw=order}); // controlled third-card fixture
    var json=System.Text.Json.JsonSerializer.Serialize(deck.Capture());
    var reloaded=SoloDummyDeck.Restore(System.Text.Json.JsonSerializer.Deserialize<SoloDummySnapshot>(json));
    deck.TakeTurn();reloaded.TakeTurn();
    int expected=rule==SoloDummyDualColorRule.MaximumMatchingCrystals?6:7;
    Check(deck.Observe().Discard.Length==expected,"Dual third card uses explicitly selected crystal rule without recursive flips");
    Check(deck.Capture().Draw.SequenceEqual(reloaded.Capture().Draw) && reloaded.DualColorRule==rule,
        "JSON checkpoint preserves both colors, exact draw order and selected rule");
    Check(deck.Capture().Cards.Single(c=>c.PhysicalId==dual.PhysicalId).SecondColor==colors[1],"Actual acquired dual card retains second printed color");
}
var unspecified=new SoloDummyDeck(goldyx.StartingCards.Select((id,i)=>new SoloDummyCard("unspecified/"+i,id,originalCards[id].RequiredCrystals.Single())),new[]{0,1,2,0},202);
var legacy=unspecified.Capture() with {Version=1};
Check(SoloDummyDeck.Restore(legacy).Capture().Draw.SequenceEqual(legacy.Draw),"Legacy single-color snapshot migrates without changing shuffle");
unspecified.CloseRound();
Reject(()=>unspecified.BeginNextRound(new SoloDummyCard("unspecified/16","advanced_card_040",ManaColor.Green,ManaColor.Blue),ManaColor.Blue),
    "Unspecified dual semantics cannot silently pick one color");
Check(unspecified.Round==1 && unspecified.Closed && unspecified.Capture().Cards.Length==16,"Rejected dual transfer preserves deck and round");
Console.WriteLine($"PASS {checks} with explicit dual-color rules and JSON dummy checkpoint migration; production rule choice remains separate.");
int bindingOutput=Array.IndexOf(args,"--binding-fixtures");
if(bindingOutput>=0)System.IO.File.WriteAllText(args[bindingOutput+1],System.Text.Json.JsonSerializer.Serialize(lifecycleBindings));

