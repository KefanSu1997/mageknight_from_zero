using System;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;

var p = new PlayerState(0,"gate");
var advanced = new HashSet<string> { "advanced_card_000" };
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
int Score() => SoloInventoryScore.Capture(p, advanced).Total;
var wound1 = new DeedCard("wound",CardType.Wound);
var wound2 = new DeedCard("wound",CardType.Wound);
p.Deck.Hand.Add(wound1);
p.Deck.PutOnTop(wound2);
Check(Score() == -4, "Distinct identical wounds must both count");
p.Deck.Hand.Remove(wound1);
p.Deck.GainToDiscard(wound1);
Check(Score() == -4, "Discarding a wound is not healing");
p.Deck.TryRemoveFromDiscardExact(wound1);
Check(Score() == -2, "Removing an actual wound changes inventory score");
p.Deck.Hand.Add(new DeedCard("advanced_card_000",CardType.Action));
p.Deck.Hand.Add(new DeedCard("magic_000",CardType.Spell));
p.Deck.Hand.Add(new DeedCard("items_000",CardType.Artifact));
Check(Score() == 3, "Printed card categories");
p.Mana.Crystals[ManaColor.Blue] = 3;
p.Mana.Tokens[ManaColor.Blue] = 20;
Check(Score() == 4, "Half crystals rounded down; temporary tokens not scored");
p.Fame = 8;
Check(Score() == 12 && p.Level == 3, "Score reads Fame without changing level");
var unit = new UnitState(new UnitCard("gate_unit", "gate", "gate", 3, 3, 5,
    (RecruitLocation)0, Array.Empty<AttackProfile>(), Array.Empty<Ability>()));
p.Units.Add(unit);
var banner = new DeedCard("items_001",CardType.Artifact);
p.Deck.Hand.Add(banner);
int beforeBanner = Score();
BannerActions.AssignFear(p, unit, banner);
Check(Score() == beforeBanner && SoloInventoryScore.Capture(p,advanced).Artifacts == 2,
    "Native banner assignment preserves artifact ownership and score");
unit.AddWounds(1);
Check(Score() == beforeBanner - 2, "Wounded level-three unit scores one, not three");
Check(SoloInventoryScore.Capture(p,advanced).DeckWounds == 1, "Unit wounds are not deck wound cards");
unit.Heal(1);
Check(Score() == beforeBanner, "Native unit healing restores only lost inventory points");
var artifact = p.Deck.Hand.Find(c => c.Id == "items_000");
p.Deck.TryPutInPlay(artifact);
Check(Score() == beforeBanner, "Playing an artifact does not lose it");
var consumed = new DeedCard("items_004",CardType.Artifact);
p.Deck.Hand.Add(consumed);
int beforeConsume = Score();
Check(p.Deck.TryRemovePermanentlyExact(consumed), "Actual hand artifact removal must succeed");
Check(Score() == beforeConsume - 2, "Permanently consumed artifact leaves score");
p.Deck.Hand.Add(wound2);
bool rejected = false;
try { Score(); } catch (InvalidOperationException) { rejected = true; }
Check(rejected, "Duplicated physical card must fail, not silently double-score");
Console.WriteLine($"PASS {checks} native inventory checks; site and full-scenario scoring not yet integrated.");
