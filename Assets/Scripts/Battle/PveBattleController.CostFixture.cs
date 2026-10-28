using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using UnityEngine;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private readonly List<BattleEncounter> _costOwnedEncounters = new();
        private CostPrefixAdapter _costPrefixAdapter;
        private readonly List<MK.Logic.Data.Monster> _costSeenDefeats=new();
        private int _costUiKills;
        private int _costDefendMask=-1;
        private bool _costUiPaymentAudit;
        private int _costObservedCompletedTurns,_costObservedRoundIndex;
        private ActionContext _costObservedContext;
        private void ResetCostTurnObservation()
        {
            _costObservedCompletedTurns=_journey.CompletedTurns;
            _costObservedRoundIndex=_journey.Game.Clock.RoundIndex;
            _costObservedContext=_journey.Context;
        }
        // Mirror only transitions already committed by real UI callbacks.
        // No archived expected state, action or post-state is installed here.
        private void ObserveCostTurnChanges()
        {
            if(!_costUiPaymentAudit||_costPrefixAdapter==null)return;
            var a=_costPrefixAdapter;
            if(_journey.CompletedTurns!=_costObservedCompletedTurns)
            {
                if(_journey.CompletedTurns!=_costObservedCompletedTurns+1)
                    throw new InvalidOperationException("Unobserved UI turn boundary");
                _costObservedCompletedTurns=_journey.CompletedTurns;
                var battle=_journey.Battle;
                var survivors=battle==null?a.PendingEnemies:battle.Enemies.Where((e,i)=>!battle.Context.DefeatedEnemyIndices.Contains(i)).ToArray();
                if(survivors.Length==0)
                {
                    a.TaskEncounterCursor++;
                    if(a.TaskEncounterCursor>=a.DeclaredTask.EnemyGroups.Length)
                    {a.Done=true;a.StopReason="task_sequence_complete";}
                }
                if(!a.Done&&a.Turns>=16){a.Done=true;a.StopReason="turn_limit";}
                if(!a.Done)
                {
                    _encounter=Math.Min(a.TaskEncounterCursor,encounters.Length-1);
                    a.PendingEnemies=survivors.Length==0?_journeyEnemies[_encounter]:survivors;
                    _journeyEnemies[_encounter]=a.PendingEnemies;
                }
            }
            if(_journey.Game.Clock.RoundIndex!=_costObservedRoundIndex)
            {
                if(_journey.Game.Clock.RoundIndex!=_costObservedRoundIndex+1)
                    throw new InvalidOperationException("Unobserved UI round boundary");
                _costObservedRoundIndex=_journey.Game.Clock.RoundIndex;
                if(a.Rounds>=2)a.Done=true;else a.Rounds++;
            }
            if(!ReferenceEquals(_journey.Context,_costObservedContext))
            {
                _costObservedContext=_journey.Context;a.Turns++;
                // Same declared independent effect stream as formal setup.
                // This seeds the actual newly started context, not a post-state.
                _journey.Context.EffectRandom=new System.Random(a.DeclaredTask.EffectSeed^a.Turns);
            }
            a.RoundWindow=!a.Done&&_journey.Game.ClockTracker.RoundEnded&&_journey.RoundBannerUnits(_journey.Player).Length>0;
        }
        private IEnumerable<DeedCard> CostOwnedCards()
        {
            var player=_journey.Player;
            return player.Deck.Hand.Concat(player.Deck.DrawPile).Concat(player.Deck.PlayedCards)
                .Concat(player.Deck.DiscardPile).Concat(player.Deck.RemovedCards)
                .Concat(player.Deck.RoundAsideCards).Concat(player.Deck.TacticAsideCards)
                .Concat(player.Units.Where(unit=>unit.Banner!=null).Select(unit=>unit.Banner));
        }
        private void RecordCostUiAcquisition(DeedCard actualCard)
        {
            if(_costPrefixAdapter!=null)_costPrefixAdapter.Acquired.Add(actualCard.Id);
        }
        private HashSet<DeedCard> CostAcquisitionBefore()
            =>_costPrefixAdapter==null?null:new HashSet<DeedCard>(CostOwnedCards(),CostReferenceComparer.Instance);
        private void RecordCostUiNewAcquisitions(HashSet<DeedCard> before,bool nonWoundsOnly=false)
        {
            if(before==null)return;
            foreach(var actual in CostOwnedCards())
                if(!nonWoundsOnly || actual.Type!=CardType.Wound)
                if(before.Add(actual))RecordCostUiAcquisition(actual);
        }
        private void ResetCostUiPose()
        {
            _targets.Clear();_attackSlot=0;_selected=null;_choice=null;
            _selectedUnit=null;_selectedSkill=null;ClearAttackSelection();
            _unitMode=_unitBoost=_unitConversion=0;_unitManaFirst=ManaColor.Red;_unitManaSecond=ManaColor.Blue;
            _supportTarget=-1;_unitUtilityPanel=false;_showUnits=_showSkills=false;
            _skillPage=0;_skillOption=0;_skillWound=null;_skillWoundPicker=false;_skillWoundPage=0;
            _skillChoiceScroll=_unitChoiceScroll=1;
            _sourceColor=ManaColor.Red;
            _battleSourceOpen=false;_battleSourcePage=0;
            _sharedColor=ManaColor.Red;_sharedEnemy=_sharedArmorEnemy=null;_sharedDie=null;_sharedPage=_sharedAttack=0;
            ClearJourneyChoices();
        }
        private void ResetCostUiAudit()
        {
            _costSeenDefeats.Clear();_costUiKills=0;
            if(_journey?.Battle!=null)foreach(int index in _journey.Battle.Context.DefeatedEnemyIndices)
                _costSeenDefeats.Add(_journey.Battle.Enemies[index]);
        }
        public string ReadCostNativeState()
        {
            if(_costPrefixAdapter==null)return "";
            ObserveCostTurnChanges();
            if(_journey.Battle!=null)foreach(int index in _journey.Battle.Context.DefeatedEnemyIndices)
            {
                var actual=_journey.Battle.Enemies[index];
                if(_costSeenDefeats.Any(old=>ReferenceEquals(old,actual)))continue;
                _costSeenDefeats.Add(actual);_costUiKills++;
            }
            var state=_costPrefixAdapter.State();state["kills"]=_costPrefixAdapter.Kills+_costUiKills;
            if(_costUiPaymentAudit)
            {
                var s=_paymentSelection;
                int slot=s?.Material==null?-1:_journey.Player.Deck.Hand.FindIndex(c=>ReferenceEquals(c,s.Material));
                if(s?.Material!=null&&slot<0)throw new InvalidOperationException("Selected UI payment material left the actual hand");
                state["paymentSelection"]=s==null?JValue.CreateNull():JToken.FromObject(new {
                    skill=s.Skill.SourceId,required=s.RequiredColor.ToString(),
                    material=slot<0?-1:(int)state["handOrder"][slot],materialSlot=slot,
                    source=s.SourceColor?.ToString(),die=s.SourceDie==null?-1:_journey.Context.ManaSource.Dice.ToList().IndexOf(s.SourceDie),extra=s.ExtraDie});
            }
            return state.ToString(Newtonsoft.Json.Formatting.None);
        }
        public void PrepareCostPrefix(string json,string paymentConsumerJson=null)
        {
#if UNITY_EDITOR
            if(_costPrefixAdapter==null||string.IsNullOrEmpty(UnityEditor.SessionState.GetString(
                MageKnight.SceneAutomation.StructuredUiPilot.SessionKey,"")))
                throw new InvalidOperationException("Declared cost reset must precede prefix preparation");
            var moves=JsonConvert.DeserializeObject<CostReplayMove[]>(json)
                ??throw new InvalidOperationException("Missing native setup prefix");
            if(moves.Length>512)throw new InvalidOperationException("Setup prefix exceeds archived task limit");
            _costUiPaymentAudit=false;
            foreach(var move in moves)
            {
                _costPrefixAdapter.Prepare(move);
                if(move.Kind=="defend")_costDefendMask=move.Targets;
                if(_journey.Context.EnemyDefense.Selected.Count==0)_costDefendMask=-1;
                // Captures initialize physical audit identities as in the real
                // protocol; they never feed the player/pointer action selector.
                _costPrefixAdapter.State();
            }
            _battle=_journey.Battle;
            _encounter=Math.Min(_costPrefixAdapter.TaskEncounterCursor,encounters.Length-1);
            if(_battle==null)_journeyEnemies[_encounter]=_costPrefixAdapter.PendingEnemies;
            _phaseStarts.Clear();_phaseSourceLabels.Clear();_checkpointBattle=null;
            ResetCostUiAudit();ResetCostUiPose();
            ClearManaPayment();
            if(_costPrefixAdapter.PendingPayment!=null)
            {
                var consumer=string.IsNullOrEmpty(paymentConsumerJson)?null:JsonConvert.DeserializeObject<CostReplayMove>(paymentConsumerJson);
                var selected=_costPrefixAdapter.PendingPayment;
                _paymentBound=consumer?.Kind=="play"?(object)_journey.Player.Deck.Hand.First(c=>c.Id==consumer.Card):
                    consumer?.Kind=="unit"?(object)_journey.Player.Units.First(u=>u.Card.Id==consumer.Card):
                    _journey.Player.Deck.Hand.FirstOrDefault(c=>c.Type!=CardType.Artifact&&c.Type!=CardType.Wound&&!ReferenceEquals(c,selected.Material));
                if(_paymentBound==null)throw new InvalidOperationException("Pending payment has no actual eligible UI consumer");
                if(ReferenceEquals(_paymentBound,selected.Material))throw new InvalidOperationException("Payment material cannot be its UI consumer");
                _paymentSelection=selected;
            }
            _costUiPaymentAudit=true;
            ResetCostTurnObservation();
            // Native preselection can only be produced from the actual UI's
            // open defense panel. Restore that pending UI context as setup.
            if(_battle!=null&&_journey.Context.EnemyDefense.Selected.Count>0&&_costDefendMask>=0)
            {
                _enemyDefendTargets=Enumerable.Range(0,_battle.Enemies.Count).Where(i=>(_costDefendMask&(1<<i))!=0).ToArray();
                _targets.UnionWith(_enemyDefendTargets);_enemyDefendSource=-1;_enemyDefendOpen=true;
                _enemyDefendPlanLabels.Clear();
            }
            Draw();
#else
            throw new InvalidOperationException("Cost prefix is editor-only");
#endif
        }
        public string ReadCostFixtureAudit()
        {
            if(_costOwnedEncounters.Count==0||_journey==null)return "";
            ObserveCostTurnChanges();
            var p=_journey.Player;var c=_journey.Context;
            int[] blockIndices=Array.Empty<int>();string blockError=null;
            try
            {
                if(_battle?.Phase==Phase.Block&&_targets.Count==1&&_targets.Single()<_battle.Enemies.Count&&
                    _attackSlot<_battle.Enemies[_targets.Single()].AttackCount&&!_battle.IsBlocked(_targets.Single(),_attackSlot))
                    blockIndices=c.CombatPower.AvailableBlocks(_targets.Single(),_attackSlot,_battle.Enemies[_targets.Single()])
                        .Select(part=>c.CombatPower.Blocks.Select((original,index)=>new{original,index}).Single(x=>ReferenceEquals(x.original,part)).index).ToArray();
            }
            catch(InvalidOperationException ex){blockError=ex.Message;}
            return JsonConvert.SerializeObject(new {
                turnFlow=new {active=_journey.Game.HasActiveTurn,ending=_journey.Game.IsEndingTurn,
                    prepared=_journey.Game.IsRoundPrepared,roundEnded=_journey.Game.ClockTracker.RoundEnded,
                    canAnnounce=_journey.Game.CanAnnounceRoundEnd,done=_costPrefixAdapter.Done,
                    progressionSeed=_costPrefixAdapter.DeclaredTask.ProgressionSeed,
                    discardSlots=_endTurnDiscards.Select(card=>p.Deck.Hand.FindIndex(c=>ReferenceEquals(c,card))).ToArray(),
                    rewardChoices=_journey.Progression.Choices.Select(s=>s.SourceId).ToArray(),
                    rewardUiSkills=_journey.Progression.Choices.Concat(_journey.Progression.CommonSkills.Where(s=>!s.SourceId.StartsWith(OriginalHeroes.Get(p.HeroId).SkillPoolId+":"))).Select(s=>s.SourceId).ToArray(),
                    rewardActions=_journey.Progression.Actions.Select(card=>card.Id).ToArray()},
                draw=p.Deck.DrawPile.Select(card=>card.Id).ToArray(),
                discard=p.Deck.DiscardPile.Select(card=>card.Id).ToArray(),
                played=p.Deck.PlayedCards.Select(card=>card.Id).ToArray(),
                removed=p.Deck.RemovedCards.Select(card=>card.Id).ToArray(),
                skills=p.Skills.Select(skill=>skill.SourceId).ToArray(),
                skillBindings=p.Skills.Select((skill,index)=>new {
                    index,id=skill.SourceId,interactive=skill.IsInteractive==true,wound=NeedsSkillWound(skill),sideways=NeedsSidewaysSkill(skill),
                    needsEnemyZero=BattleSkillChoice.NeedsEnemy(skill.SourceId)&&skill.SourceId!="hero_skill_pool_001:01",
                    needsEnemyOther=BattleSkillChoice.NeedsEnemy(skill.SourceId),
                    attackZero=BattleSkillChoice.NeedsAttack(skill.SourceId,0),attackOne=BattleSkillChoice.NeedsAttack(skill.SourceId,1),
                    attackOther=BattleSkillChoice.NeedsAttack(skill.SourceId,2),attack200Plus=BattleSkillChoice.NeedsAttack(skill.SourceId,200)
                }).ToArray(),
                uiSkill=_selectedSkill?.SourceId,uiOutsideSkill=_outsideSkill?.SourceId,
                uiSharedColor=_sharedColor.ToString(),uiSharedSideways=_sharedSideways.ToString(),
                units=p.Units.Select(unit=>unit.Card.Id).ToArray(),
                unitAbilities=p.Units.Select((unit,index)=>new {
                    index,id=unit.Card.Id,masters=DelphanaMastersActions.IsMasters(unit),
                    abilities=unit.Card.Actions.Select((ability,slot)=>new {
                        slot,kind=ability.Kind.ToString(),value=ability.Value,
                        targeted=ShockTroopsActions.IsReaction(ability)||DelphanaMastersActions.IsMasters(unit)||
                            ability.Kind==UnitAbilityKind.FreezeArmor||ability.Kind==UnitAbilityKind.StopUnfortifiedEnemy||
                            ability.RemoveTargetFortifications||ability.RemoveTargetResistances
                    }).ToArray()
                }).ToArray(),
                uiUnitMode=_unitMode,uiUnitBoost=_unitBoost,uiUnitConversion=_unitConversion,
                uiUnit=_selectedUnit?.Card.Id,uiOutsideUnit=_outsideUnit?.Card.Id,
                uiUnitManaFirst=_unitManaFirst.ToString(),uiUnitManaSecond=_unitManaSecond.ToString(),
                uiSupportTarget=_supportTarget,
                uiTargets=_targets.OrderBy(i=>i).ToArray(),
                uiCard=_selected?.Id,uiAttackSlot=_attackSlot,
                uiPaymentConsumer=_paymentBound is DeedCard paymentCard?new {Kind="play",Card=paymentCard.Id}:
                    _paymentBound is UnitState paymentUnit?new {Kind="unit",Card=paymentUnit.Card.Id}:null,
                uiReservedAttacks=_battle==null?Array.Empty<int>():_battle.Context.CombatPower.AvailableAttacks(_battle.Phase,c.AllAttacksSiege)
                    .Select((part,index)=>new{part,index}).Where(x=>_reservedAttacks.Any(r=>ReferenceEquals(r,x.part))).Select(x=>x.index).ToArray(),
                uiReservedBlocks=c.CombatPower.Blocks.Select((part,index)=>new{part,index})
                    .Where(x=>_reservedBlocks.Any(r=>ReferenceEquals(r,x.part))).Select(x=>x.index).ToArray(),
                uiBlockIndices=blockIndices,uiBlockError=blockError,
                cardChoices=p.Deck.Hand.Select((card,index)=>new {
                    index,id=card.Id,type=card.Type.ToString(),
                    choices=BattleCardChoice.For(card.Id).Select(choice=>new {
                        id=choice.Id,enhanced=choice.Enhanced,target=choice.Target.ToString(),option=choice.Option
                    }).ToArray()
                }).ToArray(),
                sourceOrder=c.ManaSource.Dice.Select(die=>die.Face.ToString()).ToArray(),
                blackManaWild=c.BlackManaWild,uiSourceColor=_sourceColor.ToString(),
                unitOffer=_journey.Game.Globals.Units.Offer.Select(unit=>unit.Id).ToArray(),
                movement=c.MovementPool,influence=c.InfluencePool,
                enemyRewardMode=_journey.EnemyRewardMode.ToString(),
                phase=_journey.Battle?.Phase.ToString()??"Outside"
            });
        }
        // Test-only entry. Setup cost is separate from actual pointer measurements.
        // Uses the same formal constructors and explicit independent streams as
        // declared BattleRL tasks; never installs a guessed post-action state.
        [Serializable] public sealed class CostTask
        {
            public string HeroId;
            public bool OriginalOpening, OriginalWorld;
            public int RandomSeed, InventorySeed, ShuffleSeed, ManaSeed, SupplySeed,
                ProgressionSeed, EffectSeed, SummonSeed;
            public string[][] EnemyGroups;
            public string WorldSourceSha256;
            public CostBuild Build;
        }
        [Serializable] public sealed class CostBuild
        {
            public string Family;
            public int Fame, InitialWounds;
            public string[] AdditionalCards, UnitIds, SkillIds;
            public Dictionary<string,int> Crystals;
        }

        public void ResetCostFixture(string json)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(UnityEditor.SessionState.GetString(
                MageKnight.SceneAutomation.StructuredUiPilot.SessionKey,"")))
                throw new InvalidOperationException("Cost fixture requires the explicit UI pilot session");
            var document=JObject.Parse(json);
            var allowed=typeof(CostTask).GetFields().Select(f=>f.Name).ToArray();
            var optional=new[]{"Build","OriginalWorld","WorldSourceSha256"};
            if(document.Properties().Any(p=>!allowed.Contains(p.Name))
                ||allowed.Except(optional).Any(name=>document[name]==null))
                throw new InvalidOperationException("Task must specify all independent factors and supported fields");
            var t=JsonConvert.DeserializeObject<CostTask>(json)
                ??throw new InvalidOperationException("Missing declared task");
            var hero=OriginalHeroes.Get(t.HeroId);
            if (new[]{t.RandomSeed,t.InventorySeed,t.ShuffleSeed,t.ManaSeed,t.SupplySeed,
                t.ProgressionSeed,t.EffectSeed,t.SummonSeed}.Any(s=>s<0||s>int.MaxValue-256))
                throw new InvalidOperationException("Invalid independent random streams");
            var monsters=CardJsonLoader.LoadMonsters().ToArray();
            if(t.EnemyGroups==null || t.EnemyGroups.Length<1 || t.EnemyGroups.Length>16
                ||t.EnemyGroups.Any(g=>g==null||g.Length<1||g.Length>3||g.Distinct().Count()!=g.Length
                    ||g.Any(id=>!monsters.Any(m=>m.Id==id))))
                throw new InvalidOperationException("Invalid declared enemy groups");
            if(!t.OriginalOpening&&t.Build==null || t.OriginalOpening&&t.Build!=null)
                throw new InvalidOperationException("Declared fixture needs original opening or explicit midgame build");
            var basics=CardJsonLoader.LoadBasicActions().ToArray();
            var advanced=CardJsonLoader.LoadAdvancedActions().ToArray();
            var spells=CardJsonLoader.LoadSpells().ToArray();
            var items=CardJsonLoader.LoadItems().Where(c=>PveArtifactInputs.Supports(c.Id)).ToArray();
            var units=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).ToArray();
            var skills=CardJsonLoader.LoadHeroSkills(hero.SkillPoolId).ToArray();
            var player=hero.CreatePlayer(basics,new System.Random(t.ShuffleSeed));
            if(t.Build!=null)
            {
                var b=t.Build;
                bool Valid(string[] ids,IEnumerable<string> allowed)=>ids!=null&&ids.Distinct().Count()==ids.Length&&ids.All(allowed.Contains);
                if(string.IsNullOrWhiteSpace(b.Family)||b.Family.Length>64||b.Fame<0||b.Fame>99||b.InitialWounds<0||b.InitialWounds>32
                    ||!Valid(b.AdditionalCards,advanced.Select(c=>c.Id).Concat(spells.Select(c=>c.Id)).Concat(items.Select(c=>c.Id)))
                    ||!Valid(b.UnitIds,units.Select(c=>c.Id))
                    ||!Valid(b.SkillIds,skills.Select(c=>c.SourceId))||b.Crystals==null)
                    throw new InvalidOperationException("Invalid original midgame inventory");
                player.Fame=b.Fame;player.Armor=2+(player.Level+1)/4;
                if(b.SkillIds.Length>player.Level/2)throw new InvalidOperationException("Skills exceed actual level allowance");
                var deck=player.Deck.DrawPile.ToList();
                foreach(var id in b.AdditionalCards)
                    deck.Add(new DeedCard(id,spells.Any(s=>s.Id==id)?CardType.Spell:items.Any(c=>c.Id==id)?CardType.Artifact:CardType.Action));
                player.Deck.SetDeck(deck);player.AddWoundCards(b.InitialWounds,toDiscard:true);
                foreach(var id in b.UnitIds)player.Units.Add(new UnitState(units.Single(u=>u.Id==id)));
                // The real level/loyalty/banner command-slot rules are the limit.
                // Do not impose a separate three-unit UI fixture ceiling.
                if(player.OrdinaryFreeSlots<0)throw new InvalidOperationException("Units exceed actual ordinary command slots");
                foreach(var kv in b.Crystals)
                {
                    if(!Enum.TryParse<ManaColor>(kv.Key,out var color)||Enum.GetName(typeof(ManaColor),color)!=kv.Key
                        ||!FamiliarActions.IsBasicColor(color)||kv.Value<0||kv.Value>3)
                        throw new InvalidOperationException("Invalid original crystal inventory");
                    if(kv.Value>0)player.Mana.AddCrystal(color,kv.Value);
                }
            }
            var supply=new EnemyTokenSupply(monsters.Where(e=>e.TokenColor==EnemyTokenColor.Brown
                ||t.OriginalWorld&&e.TokenColor==EnemyTokenColor.Green).Select(e=>e with{}),new System.Random(t.SummonSeed));
            MapState map=null;
            if(t.OriginalWorld)
            {
                var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../resources/original_maps/tutorial_opening.json"));
                using var hash=SHA256.Create();
                var signature=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
                if(signature!=t.WorldSourceSha256)throw new InvalidOperationException("Original map source binding differs");
                map=new MapState();OriginalMapCatalog.Load(path).Initialize(map,supply);
            }
            var deeds=basics.Concat(advanced).Concat(spells.Select(s=>new MK.Logic.Data.Cards.ActionCardData(
                s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost))).ToArray();
            var journey=new PveJourneySession(player,deeds,artifacts:items,map:map,spells:spells,
                manaRandom:new System.Random(t.ManaSeed),tacticRandom:new System.Random(t.RandomSeed),
                supplyRandom:new System.Random(t.SupplySeed),originalSkills:skills,
                progressionRandom:new System.Random(t.ProgressionSeed),roundShuffleRandom:new System.Random(t.ShuffleSeed),
                unitDeck:units.Where(u=>!player.Units.Any(owned=>ReferenceEquals(owned.Card,u))),
                enemySupply:supply,enemyRewardMode:EnemyRewardMode.PrintedFameOnly);
            foreach(var id in t.Build?.SkillIds??Array.Empty<string>())
                HeroSkillAcquisition.Acquire(player,skills.Single(s=>s.SourceId==id),new System.Random(t.ProgressionSeed),journey.Game.Globals.Units);
            journey.PrepareRound();
            foreach(var unit in player.Units.Where(u=>u.NeedsAttunementUpkeep).ToArray())
            {
                ManaColor? color=player.Mana.Crystals.Where(x=>FamiliarActions.IsBasicColor(x.Key)&&x.Value>0)
                    .OrderBy(x=>x.Key).Select(x=>(ManaColor?)x.Key).FirstOrDefault();
                journey.Game.ResolveFamiliarUpkeep(player,unit,color);
            }
            journey.SelectTactic("day_1");journey.BeginTurn();journey.Context.EffectRandom=new System.Random(t.EffectSeed);
            var views=worldEnemyCatalog.Concat(encounters.SelectMany(e=>e.enemies)).ToArray();
            var generated=t.EnemyGroups.Select((g,i)=>
            {
                var e=ScriptableObject.CreateInstance<BattleEncounter>();e.displayName="成本复现 · 原敌群 "+(i+1);
                e.originalMonsterJson=encounters[0].originalMonsterJson;e.background=encounters[0].background;
                e.enemies=g.Select(id=>views.First(v=>v.originalId==id)).ToArray();return e;
            }).ToArray();
            // Install only after the entire native setup has validated successfully.
            foreach(var old in _costOwnedEncounters)Destroy(old);
            _costOwnedEncounters.Clear();_costOwnedEncounters.AddRange(generated);
            _journey=journey;_battle=null;encounters=generated;_encounter=_draftEncounter=0;
            _costPrefixAdapter=new CostPrefixAdapter(journey,t);
            _costUiPaymentAudit=false;ClearManaPayment();
            _costDefendMask=-1;
            ResetCostUiAudit();
            _costPrefixAdapter.State();
            _journeyEnemies.Clear();
            for(int i=0;i<t.EnemyGroups.Length;i++)_journeyEnemies[i]=t.EnemyGroups[i].Select(id=>monsters.Single(m=>m.Id==id) with{}).ToArray();
            _configuredStartHero=hero;_originalStartHero=null;_draftOriginalMap=t.OriginalWorld;
            _activeUnits=Array.Empty<BattleUnitDefinition>();_camp=false;_journeyTools=false;_meditation=false;
            _phaseStarts.Clear();_phaseSourceLabels.Clear();_checkpointBattle=null;_rejections=0;
            ResetCostUiPose();Draw();
#else
            throw new InvalidOperationException("Cost fixture is editor-only");
#endif
        }
    }
}
