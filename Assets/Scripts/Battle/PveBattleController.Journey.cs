using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using AxialCoord = MK.Logic.Runtime.Map.AxialCoord;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public Texture2D[] tacticArtwork = Array.Empty<Texture2D>();
        private PveJourneySession _journey;
        private readonly Dictionary<int,Monster[]> _journeyEnemies = new();
        private readonly List<DeedCard> _tacticCards = new();
        private readonly List<ManaDie> _tacticDice = new();
        private bool _journeyTools, _meditation;
        private int _tacticCardPage;
        private readonly List<string> _cooperativeTactics = new();
        private ManaColor _sourceColor = ManaColor.Red;

        private void BeginJourney()
        {
            _originalStartHero=null;_configuredStartHero=null;
            _loadout = _draftLoadout;
            _activeUnits = _draftUnits?.ToArray() ?? Loadout.units;
            StartJourney(CreateConfiguredPlayer(_activeUnits));
        }
        private void StartJourney(PlayerState player, SkillCard[] initialSupplySkills=null, PlayerState companion=null, SkillCard[] companionSupplySkills=null)
        {
            _fluentSourceOpen = _fluentDiscardMode = false;
            _loadout = _draftLoadout; _encounter = _draftEncounter;
            if(_draftOriginalMap || _originalStartHero!=null)
            {
                player.Position=new MK.Logic.Runtime.Map.AxialCoord(0,0);
                if(companion!=null)companion.Position=player.Position;
            }
            else if(travelRoute!=null)
            {
                var start=travelRoute.sites.Single(s=>s.id==travelRoute.startSite && s.revealed);
                player.Position=new MK.Logic.Runtime.Map.AxialCoord(start.q,start.r);
                if(companion!=null)companion.Position=player.Position;
            }
            var skillJson=loadouts.Select(l=>l.hero?.originalSkillsJson).FirstOrDefault(json=>json!=null);
            var skills=string.IsNullOrEmpty(player.HeroId)?null:CardJsonLoader.ParseHeroSkills(skillJson.text,OriginalHeroes.Get(player.HeroId).SkillPoolId);
            ClearSiteEncounter();var worldSupply=CreateWorldSupply();
            _journey = new PveJourneySession(player,
                OriginalDeeds(),artifactCatalog.Select(a=>a.Snapshot()),map:CreateJourneyMap(worldSupply),enemySupply:worldSupply,spells: OriginalSpells(),originalSkills:skills,unitDeck:CreateJourneyUnitDeck(player),
                supplyRandom:Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null,
                progressionRandom:Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null,
                manaRandom:Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null,
                tacticRandom:Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null,
                roundShuffleRandom:Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null,
                companions:companion==null?null:new[]{companion},
                companionSkills:p=>CardJsonLoader.ParseHeroSkills(skillJson.text,OriginalHeroes.Get(p.HeroId).SkillPoolId),
                enemyRewardMode:EnemyRewardMode.TezlaJustFame);
            _upgradeSkill=null;_upgradeAction=null;
            _journeyMap=false;_journeyMapTarget=null;_harvestMaterial=null;_harvestPicker=false;
            _journeyEnemies.Clear();
            foreach (int index in Enumerable.Range(0,encounters.Length)) _journeyEnemies[index] = encounters[index].LoadEnemies();
            _battle = null; _camp = false; _journeyTools = false; _meditation = false;
            ClearJourneyChoices(); _cooperativeTactics.Clear(); _journey.PrepareRound();
            foreach(var skill in initialSupplySkills??Array.Empty<SkillCard>())
                HeroSkillAcquisition.Acquire(player,skill,units:_journey.Game.Globals.Units);
            foreach(var skill in companionSupplySkills??Array.Empty<SkillCard>())
                HeroSkillAcquisition.Acquire(companion,skill,units:_journey.Game.Globals.Units);
        }
        private void ClearJourneyChoices(bool preserveEndTurnDiscards = false)
        {
            ClearAttackSelection(); _handPage = 0;
            _enemyDefendOpen = false;
            _enemyDefendTargets = Array.Empty<int>();
            _enemyDefendSource = -1;
            _siteEndOpen=false;_siteEndWound=null;_siteEndMine=null;_siteEndPage=0;
            ClearHeadbandInput();
            _sharedSkillsOpen=false;_sharedOwned=null;_sharedToken=null;_sharedCards.Clear();
            ClearManaPayment();_enemyTokenSkill=null;_skillEnemies.Clear();_flightSkill=null;_showMapGuards=false;_shapeSkill=null;_shapeCard=null;_shapeMaterial=null;_contributionSkill=null;_leadershipSkill=null;_skillAttack=null;_skillBlock=null;
            _ownedUnitsOpen=false;_outsideUnit=null;
            _offeredUnitsOpen=false;_borrowedUnit=null;
            _copyHost=null;_inputSubmit=null;_offeredSpellsOpen=false;
            _inputCard=null;
            _learningOpen=false;
            _conversionsOpen=false;_peacefulUnit=null;
            _materialArtifact=null;
            _specialCard=null;
            _journeyInteraction=false;_offerUnit=null;_dismissUnit=null;_siteSpellShop=false;_shopSpell=null;
            _journeySupport=false;_outsideSkill=null;_restOrdinary=null;_restWounds.Clear();
            if (!preserveEndTurnDiscards) _endTurnDiscards.Clear();
            _journeyHand=false;_journeyCard=null;_journeyUnitTargets.Clear();
            _journeyMap=false;
            _targets.Clear(); _selected = null; _selectedUnit = null; _selectedSkill = null;
            _choice = null; _feedback = null; _tacticCards.Clear(); _tacticDice.Clear(); _tacticCardPage = 0;
            _showUnits = _showSkills = _bowPanel = _recovery = _selectMaterials = false; _materials.Clear();
        }
        private void StartJourneyBattle(int index)
        {
            if(_journey.Game.Map.OriginalCells.Count>0)
                throw new InvalidOperationException("请从原地图选择实际敌人并按位置挑战");
            ClearSiteEncounter();
            var enemies = _journeyEnemies[index].Where(e=>!_journey.IsDefeated(e)).ToArray();
            var battle = _journey.BeginBattle(enemies);
            _battle = battle; _encounter = index; _journeyTools = false;
            _fluentSourceOpen=optimizedInteractionUi;
            // Entering combat continues this turn; retain the chosen actual hand instances.
            ClearJourneyChoices(preserveEndTurnDiscards: true);
        }
        private void EndJourneyTurn()
        {
            if(SharedChoicePending || SharedArea.PendingSourceOpeningReturns(_journey.Player,_journey.Context).Count>0){OpenSharedSkills();return;}
            var place=SiteTurnRules.At(_journey.Game.Map,_journey.Player);
            if(place?.Type==PlaceType.MagicalGlade && SiteEndWounds().Length>0
                || (place?.Type==PlaceType.ManaMine || place?.Type==PlaceType.DeepMine) && place.MineColors?.Length>1)
            {_siteEndOpen=true;_siteEndWound=null;_siteEndMine=null;_siteEndPage=0;return;}
            CompleteJourneySiteTurn(null);
        }
        private void CompleteJourneySiteTurn(SiteTurnSelection choice)
        {
            _journey.EndTurn(_endTurnDiscards.Where(c=>_journey.Player.Deck.Hand.Any(h=>ReferenceEquals(c,h))).ToArray(),choice);
            _fluentDiscardMode=false;
            _journeyTools = false; ClearJourneyChoices();
        }
        private string JourneyStatus()
        {
            var p = _journey.Player;
            return $"{p.Name}  ·  等级 {p.Level}  ·  名望 {p.Fame}  ·  创伤 {p.Wounds}\n" +
                $"手牌 {p.Deck.Hand.Count}  /  牌库 {p.Deck.DrawPileCount}  /  已打出 {p.Deck.PlayedCards.Count}  /  弃牌 {p.Deck.DiscardPile.Count}\n" +
                $"第 {_journey.Game.Clock.RoundIndex} 轮 · {(_journey.Game.Clock.DayPart == DayPart.Day ? "白昼" : "黑夜")}  ·  已结束 {_journey.CompletedTurns} 回合 / {_journey.CompletedBattles} 场战斗";
        }
        private void DrawJourneyMenu()
        {
            _journey.PreparePlayerView();
            Texture(_root,"Environment",Encounter.background,0,0,1920,1080);
            W.Panel(_root,"Shade",0,0,1920,1080,new Color(.025f,.07f,.065f,.94f));
            W.Text(_root,"Title","MAGE KNIGHT  /  连续旅程",42,27,1200,64,38,W.Gold);
            bool inlineSourceInMenu=optimizedInteractionUi && _fluentSourceOpen && _journey.Game.HasActiveTurn
                && !_journey.Game.IsRoundPrepared && !_journey.Game.HasPendingTacticSelection && _journey.Player.Tactic!=null;
            W.Text(_root,"JourneyStatus",JourneyStatus(),42,108,inlineSourceInMenu?900:1510,108,23,W.Cream);
            W.Text(_root,"JourneySetup",_journey.Game.Map.OriginalCells.Count>0?"原起始地图 A / 1 / 2 · 实际地点与敌人 · 后续探索尚未接入":_journey.Players.Count>1?"合作构筑："+string.Join(" / ",_journey.Players.Select(p=>p.Name))+" · 独立手牌与资源 · 地图为自定布局":_configuredStartHero!=null?"原英雄："+_configuredStartHero.Name+" · 牌区与资源以上方实际状态为准 · 地图为自定布局":_originalStartHero==null?"构筑起点："+Loadout.displayName+" · 自定牌库与已招募部队":
                "标准英雄开局："+_originalStartHero.Name+" · 16张原牌 · 地图与遭遇仍为自定布局",42,225,inlineSourceInMenu?900:1800,37,20,W.Muted);
            W.Text(_root,"JourneyFeedback",_feedback ?? "沿用同一玩家继续行动。官方 Just Fame：阵营敌额外名望+1，不使用阵营奖励标记。",42,986,1810,58,21,W.Cream);
            var game = _journey.Game; var player = _journey.Player;
            if (game.IsRoundPrepared)
            {
                var familiar = player.Units.FirstOrDefault(u=>u.NeedsAttunementUpkeep && !u.IsDestroyed);
                if (familiar != null) { DrawFamiliarUpkeep(familiar); return; }
                DrawTacticPicker(); return;
            }
            if (player.Tactic == null)
            {
                Button(_root,"Btn_PrepareRound","准备新轮：补牌与重整",42,310,650,64,()=>{_journey.PrepareRound();_cooperativeTactics.Clear();ClearJourneyChoices();},true); return;
            }
            if (game.HasPendingTacticSelection)
            {
                DrawTacticDetails(_root,42,290);
                if (player.Tactic.Card.Id == "day_3") DrawSource(_root,600,310,true);
                else DrawTacticCards(false);
                return;
            }
            if(player.ChaosUnusedRotationPending){DrawTacticDetails(_root,42,290);DrawChaosUnusedRotation();return;}
            if (game.ClockTracker.RoundEnded)
            {
                DrawBannerRoundChoices();
                Button(_root,"Btn_EndRound","结束本轮，进入"+(game.Clock.DayPart == DayPart.Day ? "黑夜" : "白昼"),42,310,650,64,()=>{_journey.EndRound();ClearJourneyChoices();},true); return;
            }
            if (!game.HasActiveTurn)
            {
                DrawTacticDetails(_root,42,290);
                if (_meditation) { DrawTacticCards(true); return; }
                if (player.Tactic.Card.Id == "night_6" && !player.Tactic.Spent)
                {
                    Button(_root,"Btn_Store","盖放牌库顶牌",600,320,510,63,()=>game.ResolveSparingPower(false));
                    Button(_root,"Btn_Collect","翻面取回全部蓄牌",1150,320,650,63,()=>game.ResolveSparingPower(true));
                }
                if (player.Tactic.Card.Id == "night_4" && !player.Tactic.Spent)
                    Button(_root,"Btn_Meditation","午夜冥想：选择手牌",600,415,1200,63,()=>{_meditation=true;_tacticCards.Clear();});
                if (player.Tactic.Card.Id == "night_2" && !player.Tactic.Spent)
                    Button(_root,"Btn_LongNight","漫漫长夜：回收随机弃牌",600,505,1200,63,()=>game.UseLongNight(player));
                if(game.CanSkipEmptyTurn)
                    Button(_root,"Btn_SkipEmptyTurn","手牌与牌库耗尽：跳过本轮最后回合",600,625,1200,70,()=>{game.SkipEmptyTurn();ClearJourneyChoices();},true);
                else
                    Button(_root,"Btn_BeginTurn",player.Deck.Hand.Count+player.Deck.DrawPileCount==0?"无牌可用，请声明轮次结束":"开始我的回合",600,625,1200,70,()=>{_journey.BeginTurn();_battle=null;ClearJourneyChoices();},true).interactable=player.Deck.Hand.Count+player.Deck.DrawPileCount>0;
                Button(_root,"Btn_Announce","声明轮次结束（代替本回合）",600,724,1200,63,()=>_journey.AnnounceRoundEnd()).interactable=game.CanAnnounceRoundEnd;
                return;
            }
            if(_journey.Game.Map.OriginalCells.Count>0)DrawOriginalJourneyOverview();
            else
            {
                W.Text(_root,"EncounterPrompt","选择本回合敌群 · 幸存者保留，已击败标记不重生",42,291,1730,45,26,W.Gold);
                for (int i=0;i<encounters.Length;i++)
                {
                    int index=i; int remaining=_journeyEnemies[i].Count(e=>!_journey.IsDefeated(e));
                    Button(_root,"Btn_JourneyEncounter_"+i,encounters[i].displayName+" · 剩余 "+remaining,
                        42+i%3*614,355+i/3*87,582,69,()=>StartJourneyBattle(index)).interactable=remaining>0;
                }
            }
            Button(_root,"Btn_JourneyTools","战术 / 公共魔力源",42,872,415,64,OpenFluentManaTools);
            if(optimizedInteractionUi)
            {
                Button(_root,"Btn_JourneyHand","手牌与恢复",42,792,410,64,OpenJourneyHand);
                Button(_root,"Btn_QuickSupport","休息 / 已学技能",482,792,410,64,OpenFluentSupport);
            }
            else Button(_root,"Btn_JourneyHand","手牌与恢复",42,792,850,64,OpenJourneyHand);
            Button(_root,"Btn_JourneyInteraction",_journey.AtMagicalGlade?"林地招募 / 部队供应":RecruitmentSiteName(_journey.CurrentSiteRecruitLocation)+" / 部队供应",477,872,415,64,()=>_journeyInteraction=true);
            if(travelRoute!=null || _journey.Game.Map.OriginalCells.Count>0)Button(_root,"Btn_JourneyMap","查看地形 / 移动",953,792,850,64,OpenJourneyMap);
            if(optimizedInteractionUi)
            {
                Button(_root,"Btn_EndTurn","按当前弃牌选择结束",953,872,410,64,EndJourneyTurn);
                Button(_root,"Btn_EndTurnDiscardOrdinary","弃非创伤牌并结束",1393,872,410,64,EndJourneyTurnDiscardingOrdinary).interactable=CanFluentEndTurn;
            }
            else Button(_root,"Btn_EndTurn","结束回合 / 补牌",953,872,850,64,EndJourneyTurn);
            if(_journey.Context.OfferedUnitUses>0)
                Button(_root,"Btn_OfferedUnits","战斗号召 · 选择供应能力",42,942,850,43,OpenOfferedUnits)
                    .GetComponentInChildren<TextMeshProUGUI>().fontSize=18;
            if(_journey.Context.SpellColors.Count>0)
                Button(_root,"Btn_OfferedSpells","魔法天赋 · 供应施法",953,942,850,43,()=>_offeredSpellsOpen=true)
                    .GetComponentInChildren<TextMeshProUGUI>().fontSize=18;
            DrawFluentSource(false);
            if (_journeyTools) DrawJourneyTools();
            if (_journeyHand) DrawJourneyHand();
            if (_materialArtifact!=null) DrawMaterialArtifact();
            if (_journeyMap) DrawJourneyMap();
            if (_journeyInteraction) DrawJourneyInteraction();
            if(_specialCard!=null)DrawSpecialArtifact();
            if(_copyHost!=null)DrawCopyPicker();
            if(_offeredSpellsOpen)DrawOfferedSpells();
            if(_inputCard!=null)DrawDeedInputs();
            DrawPendingCopy();
            if(_learningOpen)DrawLearningSupply();
            if(_conversionsOpen)DrawConversions();
            if(_offeredUnitsOpen)DrawOfferedUnits();
            if(_ownedUnitsOpen)DrawOwnedUnits();
            if(_sidewaysSkill!=null)DrawSidewaysSkill();
            DrawHeadbandTargetInput();
            DrawHeadbandSocialInput();
            DrawHeadbandFlipInput();
            if(_enemyTokenSkill!=null)DrawEnemyTokenSkill();
            if(_shapeSkill!=null)DrawShapeshift();
            if(_manaPaymentOpen)DrawManaPayment();
            DrawHeadbandPaymentStatus();
            DrawSharedSkillsIfNeeded();
        }
        private void DrawTacticPicker()
        {
            var picker=_journey.Players[_cooperativeTactics.Count];
            W.Text(_root,"TacticOwner","为 "+picker.Name+" 选择战术 · 已选 "+_cooperativeTactics.Count+" / "+_journey.Players.Count,42,270,1730,36,24,W.Gold);
            var choices=OriginalTactics.All.Where(t=>t.Card.Part==_journey.Game.Clock.DayPart).ToArray();
            for(int i=0;i<choices.Length;i++)
            {
                var t=choices[i]; float x=42+i*307;
                var panel=W.Panel(_root,"Tactic_"+t.Card.Id,x,310,288,598,W.Ink,true);
                int art=Array.IndexOf(OriginalTactics.All,t);
                if(art<tacticArtwork.Length) Texture(panel,"OriginalTacticArt",tacticArtwork[art],14,18,260,385);
                W.Text(panel,"TacticTitle",t.Card.Number+" · "+t.Name,14,420,260,44,25,W.Gold);
                Button(panel,"Btn_Tactic_"+t.Card.Id,"选择此战术",14,507,260,63,()=>{_cooperativeTactics.Add(t.Card.Id);if(_cooperativeTactics.Count==_journey.Players.Count)_journey.SelectTactics(_cooperativeTactics.ToArray());ClearJourneyChoices();},true).interactable=!_cooperativeTactics.Contains(t.Card.Id);
            }
        }
        private void DrawTacticDetails(Transform parent,float x,float y)
        {
            var t=OriginalTactics.Get(_journey.Player.Tactic.Card.Id); int index=Array.IndexOf(OriginalTactics.All,t);
            var panel=W.Panel(parent,"TacticDetails",x,y,480,640,W.Ink,true);
            if(index<tacticArtwork.Length) Texture(panel,"OriginalTacticArt",tacticArtwork[index],107,16,266,380);
            W.Text(panel,"TacticTitle",t.Name+(_journey.Player.Tactic.Spent ? " · 已翻面" : ""),20,415,440,46,28,W.Gold);
            W.Text(panel,"TacticRule",t.PrintedRule,20,474,440,143,23,W.Cream);
        }
        private void DrawTacticCards(bool meditation)
        {
            bool search=_journey.Player.Tactic.Card.Id=="night_5";
            var deck=_journey.Player.Deck;
            var cards=(search ? deck.DrawPile : deck.Hand).ToArray();
            int maximum=search?1:meditation?5:3;
            int replacements=Math.Min(_tacticCards.Count,deck.DrawPileCount+(meditation?_tacticCards.Count:0));
            string prompt=search ? "选择牌库中的一张原牌" : meditation ? "选择至多5张手牌，先洗回再抽（可含创伤）" : "选择至多3张手牌：先弃 → 从牌库补抽 → 弃牌洗回";
            if(!search) prompt+=$"\n牌库 {deck.DrawPileCount} · 已选 {_tacticCards.Count}/{maximum} · 实际补抽 {replacements} · 结算后手牌 {deck.Hand.Count-_tacticCards.Count+replacements}";
            W.Text(_root,"ChoicePrompt",prompt,600,284,1200,78,23,W.Gold);
            for(int slot=0;slot<8 && _tacticCardPage*8+slot<cards.Length;slot++)
            {
                int index=_tacticCardPage*8+slot; var card=cards[index];
                bool isSelected=_tacticCards.Any(c=>ReferenceEquals(c,card));
                var b=Button(_root,"Btn_TacticCard_"+index,(isSelected?"已选 · ":"")+CardName(card),600+slot%4*300,377+slot/4*215,278,193,()=>
                {
                    int selectedIndex=_tacticCards.FindIndex(c=>ReferenceEquals(c,card));
                    if(selectedIndex>=0) _tacticCards.RemoveAt(selectedIndex);
                    else { if(search)_tacticCards.Clear(); if(_tacticCards.Count<maximum)_tacticCards.Add(card); }
                });
                var sprite=CardArtwork(card);
                if(sprite!=null) W.Sprite(b.transform,"OriginalArt",sprite,82,7,108,130);
                b.GetComponentInChildren<TextMeshProUGUI>().text = "";
                W.Text(b.transform,"CardName",(isSelected?"已选 · ":"")+CardName(card),10,140,258,47,20,W.Cream,TextAlignmentOptions.Center);
            }
            if(!search && !meditation && deck.DrawPileCount<3)
                W.Text(_root,"TacticDeckWarning",deck.DrawPileCount==0
                    ? "当前构筑牌库为空：换下的牌暂不能补抽。体验完整牌库请用「原英雄 · 标准开局」。"
                    : "牌库不足时只能补抽剩余牌；本次弃下的牌在补抽之后才洗回。",600,872,1200,42,18,W.Gold);
            Button(_root,"Btn_CardPrevious","上一页",600,822,275,47,()=>_tacticCardPage--).interactable=_tacticCardPage>0;
            Button(_root,"Btn_CardNext","下一页",1517,822,275,47,()=>_tacticCardPage++).interactable=(_tacticCardPage+1)*8<cards.Length;
            Button(_root,"Btn_ConfirmTactic","确认选择（"+_tacticCards.Count+"张）",600,920,1192,54,()=>
            {
                int selectedCount=_tacticCards.Count, before=deck.Hand.Count;
                if(meditation)_journey.Game.UseMidnightMeditation(_tacticCards.ToArray());
                else _journey.Game.ResolveTacticSelection(_journey.Player,_tacticCards.ToArray());
                _meditation=false;ClearJourneyChoices();
                _feedback=search?"已取得所选原牌。":$"已换下 {selectedCount} 张，实际补抽 {deck.Hand.Count-before+selectedCount} 张；手牌 {deck.Hand.Count}，牌库 {deck.DrawPileCount}。";
            },true).interactable=!search || _tacticCards.Count==1;
        }
        private void DrawFamiliarUpkeep(UnitState unit)
        {
            W.Text(_root,"FamiliarRule",unit.Card.NameCn+"：选择一枚库存魔晶维持，或移除魔宠。临时魔力不能代付，维持不治疗创伤。",42,306,1730,105,28,W.Gold);
            var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White};
            for(int i=0;i<4;i++)
            {
                var color=colors[i];
                Button(_root,"Btn_Upkeep_"+color,ColorName(color)+" · 库存 "+_journey.Player.Mana.Crystals.GetValueOrDefault(color),42+i*455,470,420,70,()=>_journey.Game.ResolveFamiliarUpkeep(_journey.Player,unit,color));
            }
            Button(_root,"Btn_ReleaseFamiliar","放弃维持，移除这支魔宠",42,610,1750,70,()=>_journey.Game.ResolveFamiliarUpkeep(_journey.Player,unit,null));
        }
        private void DrawSource(Transform parent,float x,float y,bool reserve=false)
        {
            var source=_journey.Game.Globals.Mana;
            W.Text(parent,"SourceTitle",reserve?"魔力窃取：选择一颗基本色原骰":"公共魔力源：点选实际骰子",x,y,1120,45,27,W.Gold);
            for(int i=0;i<source.Dice.Count;i++)
            {
                var die=source.Dice[i];
                Button(parent,"Btn_Source_"+i,(_tacticDice.Contains(die)?"已选 · ":"")+ColorName(die.Face),x+i%3*372,y+70+i/3*80,348,65,()=>
                { if(_tacticDice.Contains(die))_tacticDice.Remove(die);else _tacticDice.Add(die); });
            }
            if(reserve)
                Button(parent,"Btn_ReserveDie","确认保留所选骰",x,y+265,1100,66,()=>
                {
                    if(_tacticDice.Count!=1)throw new InvalidOperationException("请选择一颗实际骰子");
                    _journey.Game.ResolveTacticSelection(_journey.Player,die:_tacticDice[0]);ClearJourneyChoices();
                },true);
            else
            {
                var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black};
                W.Text(parent,"GoldChoice","金骰取出保留金色，支付时决定基本色。其他通用许可选择：",x,y+173,1100,35,20,W.Muted);
                bool chooseBlackColor=_tacticDice.Count==1 && _tacticDice[0].Face==ManaColor.Black && (_journey.Context.BlackManaWild || _journey.Context.ExtraBlackManaDice>0);
                for(int i=0;chooseBlackColor && i<colors.Length;i++)
                {
                    var color=colors[i];Button(parent,"Btn_SourceColor_"+color,(_sourceColor==color?"● ":"")+ColorName(color),x+i*185,y+216,173,47,()=>_sourceColor=color);
                }
                Button(parent,"Btn_UseSource","普通额度取骰",x,y+297,535,62,()=>UseJourneySource(false),true);
                Button(parent,"Btn_UseExtraSource","额外额度取骰 · "+_journey.Context.ExtraManaDice,x+562,y+297,538,62,()=>UseJourneySource(true))
                    .interactable=_journey.Context.ExtraManaDice>0;
            }
        }
        private void DrawJourneyTools()
        {
            var overlay=W.Panel(_root,"JourneyTools",0,0,1920,1080,new Color(.025f,.065f,.06f,1f));
            overlay.GetComponent<Image>().raycastTarget=true;
            W.Text(overlay,"Title",JourneyStatus(),42,30,1290,120,27,W.Gold);
            Button(overlay,"Btn_SharedSkills","互动技能 / 中央标记",1390,35,480,60,()=>OpenSharedSkills());
            DrawTacticDetails(overlay,42,185); DrawSource(overlay,600,200);
            var t=_journey.Player.Tactic; var game=_journey.Game;
            if(!t.Spent && t.Card.Id=="day_3") Button(overlay,"Btn_UseReserved","使用战术保留魔力",600,617,1100,64,()=>game.UseReservedTacticMana());
            if(!t.Spent && t.Card.Id=="day_6") Button(overlay,"Btn_RightMoment","最佳时机：本回合之后再行动一回合",600,617,1100,64,()=>game.UseRightMoment());
            if(!t.Spent && t.Card.Id=="night_2") Button(overlay,"Btn_LongNight","漫漫长夜：回收随机弃牌",600,617,1100,64,()=>game.UseLongNight(_journey.Player));
            if(t.Card.Id=="night_3") Button(overlay,"Btn_ManaSearch","魔力搜寻：重掷所选至多两骰（金骰优先）",600,617,1100,64,()=>{game.UseManaSearch(_tacticDice.ToArray());_tacticDice.Clear();});
            var mana=_journey.Player.Mana;
            if(mana.Tokens.GetValueOrDefault(ManaColor.Gold)>0)
                W.Text(overlay,"GoldPayment","金色魔力保留通用：出牌或发动能力时按实际费用支付。",600,698,1100,58,23,W.Gold);
            W.Text(overlay,"Feedback",_feedback ?? OriginalTactics.Get(t.Card.Id).Name + "\n" +
                "临时魔力："+string.Join("  ",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black}.Select(c=>ColorName(c)+" "+mana.Tokens.GetValueOrDefault(c)))+"\n"+
                (_journey.Player.HeldManaDie==null ? "本回合普通源骰尚未取用。" : "本回合普通源骰已取用，结束回合时归还。"),600,770,1110,113,23,W.Cream);
            W.Text(overlay,"RoundClaims","宣夺至轮末："+string.Join("、",game.Globals.Mana.RoundClaims.Where(x=>ReferenceEquals(x.Value,_journey.Player)).Select(x=>ColorName(x.Key.Face)))+
                "  ·  后续每回合："+string.Join("、",_journey.Player.TokensPerTurn.OrderBy(x=>(int)x.Key).Select(x=>ColorName(x.Key)+"×"+x.Value)),600,883,1110,36,20,W.Gold);
            Button(overlay,"Btn_CloseTools","返回当前行动",42,931,1760,70,CloseFluentManaTools,true);
        }
        private void UseJourneySource(bool extra)
        {
            if(_tacticDice.Count!=1)throw new InvalidOperationException("请选择一颗实际骰子");
            var ctx=_journey.Context;var die=_tacticDice[0];
            bool wild=die.Face==ManaColor.Gold || die.Face==ManaColor.Black && (ctx.BlackManaWild || extra&&ctx.ExtraBlackManaDice>0);
            CardManaActions.UseSourceDie(_journey.Player,ctx,die,die.Face==ManaColor.Gold?ManaColor.Gold:wild?_sourceColor:die.Face,extra);_tacticDice.Clear();
            // Only a successful command from the quick battle entry returns
            // to the current card. Rejections keep their reason and screen.
            if(_quickSourceReturn){_quickSourceReturn=false;_journeyTools=false;_battleSourceOpen=false;}
        }
        private Dictionary<string,string> CaptureJourneyState(TextMeshProUGUI[] texts)
        {
            var p=_journey.Player;
            return new Dictionary<string,string> {
                  ["headband.remaining"]=(_journey.Context?.PendingHeadbandBorrow?.Remaining??0).ToString(),
                  ["headband.skill"]=_journey.Context?.PendingHeadbandBorrow?.Skill.SourceId??"",
                  ["shared.count"]=_journey.Game.SharedSkills.Tokens.Count.ToString(),
                  ["shared.ids"]=string.Join(",",_journey.Game.SharedSkills.Tokens.Select(t=>t.Skill.SourceId)),
                  ["mind.responses"]=_mindResponses.Count.ToString(),
                  ["mind.responder"]=_mindResponder>=0&&_journey.Context!=null?_journey.Context.OtherPlayers[_mindResponder].HeroId:"",
                  ["mind.stolen"]=_mindStealCard?.Id??"",["mind.resultOpen"]=_mindResultOpen.ToString(),
                  ["mind.reveals"]=string.Join(";",(_journey.Context?.MindReadRevealedHands??new Dictionary<PlayerState,DeedCard[]>()).Select(pair=>pair.Key.HeroId+":"+string.Join(",",pair.Value.Select(c=>c.Id)))),
                  ["party.hands"]=string.Join(";",_journey.Players.Select(player=>player.HeroId+":"+string.Join(",",player.Deck.Hand.Select(c=>c.Id).OrderBy(id=>id)))),
                  ["party.discards"]=string.Join(";",_journey.Players.Select(player=>player.HeroId+":"+string.Join(",",player.Deck.DiscardPile.Select(c=>c.Id).OrderBy(id=>id)))),
                  ["journey.players"]=_journey.Players.Count.ToString(),["journey.playerId"]=p.Id.ToString(),
                  ["journey.active"]="True",["journey.turnActive"]=_journey.Game.HasActiveTurn.ToString(),
                  ["source.faces"]=string.Join(",",_journey.Game.Globals.Mana.Dice.Select(d=>d.Face)),
                  ["source.claims"]=_journey.Game.Globals.Mana.TurnClaims.Count.ToString(),["source.extra"]=(_journey.Context?.ExtraManaDice??0).ToString(),
                  ["draw.nextBonus"]=(_journey.Context?.NextDrawBonus??0).ToString(),["crystals.return"]=(_journey.Context?.ReturnSpentCrystals??false).ToString(),
                  ["journey.cardActions"]=p.Deck.CardsPlayedOrDiscardedThisTurn.ToString(),["journey.canSkip"]=_journey.Game.CanSkipEmptyTurn.ToString(),
                  ["journey.siteShieldInfluence"]=(_journey.Context?.InteractionCityInfluence??0).ToString(),
                  ["journey.siteRecruitLocation"]=_journey.CurrentSiteRecruitLocation.ToString(),["journey.siteCanInteract"]=_journey.CanInteractAtCurrentSite.ToString(),
                  ["journey.atVillage"]=_journey.AtVillage.ToString(),["journey.interacting"]=(_journey.Context?.InNegotiation??false).ToString(),
                  ["journey.offer"]=string.Join(",",_journey.Game.Globals.Units.Offer.Select(u=>u.Id)),["journey.offerCount"]=_journey.Game.Globals.Units.Offer.Count.ToString(),
                  ["supply.advanced"]=string.Join(",",_journey.Game.Globals.AdvActions.Offer.Select(c=>c.Id)),
                  ["learning.modes"]=(_journey.Context?.LearningModes??0).ToString(),["learning.used"]=(_journey.Context?.LearnUsed??false).ToString(),
                  ["supply.spells"]=string.Join(",",_journey.Game.Globals.Spells.Offer.Select(c=>c.Id)),
                  ["supply.artifacts"]=_journey.Game.Globals.Artifacts.Count.ToString(),
                  ["supply.skills"]=string.Join(",",_journey.Game.Globals.Skills.Offer.Select(s=>s.SourceId)),
                  ["box.revealed"]=_journey.Context?.MysticBox?.RevealedArtifact.Id??"",["box.used"]=(_journey.Context?.MysticBox?.Used??false).ToString(),
                  ["command.selected"]=_journey.Context?.CommandBannerForRecruit?.Id??"",["command.banners"]=p.BannerCommandCount.ToString(),["command.free"]=p.FreeSlots.ToString(),
                  ["units.bannerIds"]=string.Join(",",p.Units.Select(u=>u.Banner?.Id??"")),
                  ["banners.returnSelections"]=string.Join(",",_journey.Players.SelectMany((owner,index)=>_journey.RoundBannerUnits(owner).Where(u=>_journey.IsBannerReturning(owner,u)).Select(u=>index+":"+owner.Units.IndexOf(u)+":"+u.Banner.Id))),
                  ["banners.window"]=_journey.Game.CanChooseRoundBannerReturns.ToString(),
                  ["material.selected"]=_artifactMaterial?.Id??"",["material.offer"]=_artifactAdvanced?.Id??_artifactSpell?.Id??"",
                  ["removed.ids"]=string.Join(",",p.Deck.RemovedCards.Select(c=>c.Id)),
                  ["journey.freeSlots"]=p.FreeSlots.ToString(),["reputation"]=p.Reputation.ToString(),["influence"]=(_journey.Context?.InfluencePool??0).ToString(),
                  ["journey.rested"]=(_journey.Context?.RestedThisTurn??false).ToString(),["journey.moved"]=(_journey.Context?.MovedThisTurn??false).ToString(),
                  ["journey.restSelected"]=_restWounds.Count.ToString(),["journey.discardCount"]=p.Deck.DiscardPile.Count.ToString(),
                  ["journey.removedCount"]=p.Deck.RemovedCards.Count.ToString(),
                  ["journey.outsideSkill"]=_outsideSkill?.SourceId??"",["journey.outsideSkillUsed"]=(_outsideSkill!=null && SkillActions.IsUsed(p,_outsideSkill)).ToString(),
                  ["skills.flipped"]=string.Join(",",p.FlippedSkills.OrderBy(s=>s)),
                  ["source.forgone"]=_journey.Player.ForgoSourceDiceThisTurn.ToString(),
                  ["source.usedThisTurn"]=(_journey.Context?.ManaSource?.WasUsedThisTurn(_journey.Player)??false).ToString(),
                  ["soul.pending"]=(_journey.Context?.SoulGuideBlockPending??false).ToString(),
                ["journey.endingTurn"]=_journey.Game.IsEndingTurn.ToString(),["upgrade.level"]=(_journey.Progression?.PendingLevel??0).ToString(),
                ["upgrade.choices"]=_journey.Progression?.Choices.Count.ToString()??"0",["upgrade.common"]=_journey.Game.Globals.Skills.Offer.Count.ToString(),
                  ["upgrade.action"]=_journey.Progression?.LastAcquiredAction??"",["upgrade.inHand"]=(_journey.Progression!=null && p.Deck.Hand.Any(c=>c.Id==_journey.Progression.LastAcquiredAction)).ToString(),
                  ["upgrade.played"]=(_journey.Progression!=null && p.Deck.PlayedCards.Any(c=>c.Id==_journey.Progression.LastAcquiredAction)).ToString(),
                ["upgrade.skillSources"]=string.Join(",",p.Skills.Select(s=>s.SourceId)),["upgrade.offerCount"]=_journey.Game.Globals.AdvActions.Offer.Count.ToString(),
                ["peaceful.available"]=(_journey.Context?.PeacefulMomentAvailable??false).ToString(),["peaceful.action"]=(_journey.Context?.PeacefulMomentAction??false).ToString(),
                ["conversion.attackCost"]=(_journey.Context?.MoveCostAttack??0).ToString(),["conversion.rangedCost"]=(_journey.Context?.MoveCostRanged??0).ToString(),
                ["conversion.block"]=(_journey.Context?.InfluenceAsBlock??false).ToString(),["conversion.blockElement"]=_journey.Context?.InfluenceBlockElement.ToString()??"",
                ["journey.position"]=p.Position.Q+","+p.Position.R,["movement"]=(_journey.Context?.MovementPool??0).ToString(),
                ["map.originalTiles"]=_journey.Game.Map.OriginalTiles.Count.ToString(),
                ["travel.hexReductions"]=string.Join(",",(_journey.Context?.HexMoveReduction??new Dictionary<AxialCoord,int>()).OrderBy(x=>x.Key.Q).ThenBy(x=>x.Key.R).Select(x=>x.Key.Q+":"+x.Key.R+"="+x.Value)),
                ["travel.terrainReductions"]=string.Join(",",(_journey.Context?.TerrainMoveReduction??new Dictionary<TerrainType,int>()).OrderBy(x=>(int)x.Key).Select(x=>x.Key+"="+x.Value)),
                ["map.originalCells"]=_journey.Game.Map.OriginalCells.Count.ToString(),
                ["map.landCells"]=_journey.Game.Map.Placed.Count.ToString(),
                ["ui.offMapBattles"]=_root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Btn_JourneyEncounter_")).ToString(),
                ["site.current"]=SiteTurnRules.At(_journey.Game.Map,p)?.Type.ToString()??"",
                ["site.endChoiceOpen"]=_siteEndOpen.ToString(),
                ["site.discardWounds"]=p.Deck.DiscardPile.Count(c=>c.Type==CardType.Wound).ToString(),
                ["map.rampaging"]=string.Join(",",_journey.Game.Map.Places.Where(x=>x.Value.Type==PlaceType.OrcRampaging && x.Value.Enemy!=null).Select(x=>x.Key.Q+":"+x.Key.R+"="+x.Value.Enemy.Id)),
                ["charm.discount"]=(_journey.Context?.CharmRecruitDiscount??0).ToString(),
                ["spell.permissions"]=string.Join(",",_journey.Context?.SpellColors??new List<ManaColor>()),
                ["copy.remaining"]=(_journey.Context?.PendingRepeatedEffect?.Remaining??0).ToString(),
                ["copy.source"]=_journey.Context?.PendingRepeatedEffect?.OriginalCardId??"",
                ["world.guards"]=string.Join(",",_journey.Game.Map.Places.Where(x=>x.Value.Type is PlaceType.Keep or PlaceType.MageTower).Select(x=>x.Key.Q+":"+x.Key.R+"="+string.Join("+",x.Value.Defenders.Select(e=>e.Id)))),
                ["world.conquered"]=string.Join(",",_journey.Game.Map.Places.Where(x=>x.Value.OwnerId==p.Id).Select(x=>x.Key.Q+":"+x.Key.R)),
                ["world.tunnel"]=(_journey.Context?.TunnelAssaultActive??false).ToString(),
                ["world.ignoreSite"]=(_journey.Context?.IgnoreSiteFortifications??false).ToString(),
                ["world.rewardPending"]=(_journey.Context?.PendingSpellReward??false).ToString(),
                ["world.reward"]=_journey.Context?.SelectedSpellReward?.Id??"",
                ["puppet.candidates"]=string.Join(",",p.PuppetTokens.DefeatedThisTurn.Select(e=>e.Id)),
                ["puppet.stored"]=string.Join(",",p.PuppetTokens.KeptTokens.Select(e=>e.Id)),
                ["loyalty.bound"]=LoyaltyActions.IsBound(p,p.LoyaltyUnit)?p.LoyaltyUnit.Card.Id:"",
                ["loyalty.available"]=LoyaltyActions.SlotAvailable(p).ToString(),
                ["loyalty.selected"]=(_journey.Context?.UseLoyaltySlot??false).ToString(),
                ["command.ordinaryFree"]=p.OrdinaryFreeSlots.ToString(),
                ["dungeon.active"]=(_journey.Context?.Dungeon!=null).ToString(),
                ["dungeon.reward"]=_journey.Context?.Dungeon?.Reward.ToString()??"",
                ["dungeon.rewardDie"]=_journey.Context?.Dungeon?.RewardDie?.ToString()??"",
                ["dungeon.artifact"]=_journey.Context?.Dungeon?.SelectedArtifact?.Id??"",
                ["dungeon.completed"]=(_journey.Context?.Dungeon?.Completed??false).ToString(),
                ["combat.dayPart"]=_journey.Context?.DayPart.ToString()??"",
                ["combat.unitsAllowed"]=(_journey.Context?.UnitsAllowed??true).ToString(),
                ["units.participating"]=string.Join(",",p.Units.Select(u=>UnitParticipation.CanUse(p,_journey.Context,u))),
                ["world.discard"]=string.Join(",",_journey.EnemySupply.DiscardPile.Select(e=>e.Id)),
                ["scout.enemy"]=_journey.Context?.LastScoutObservation?.Enemy?.Id??"",
                ["world.explorationCost"]=MK.Logic.Runtime.Map.ExplorationService.Cost(p,_journey.Context).ToString(),
                ["world.mapTargetRevealed"]=(_journeyMapTarget.HasValue && _journey.Game.Map.Places.TryGetValue(_journeyMapTarget.Value,out var selectedSite) && _journey.Game.Map.RevealedSites.Contains(selectedSite)).ToString(),
                ["world.mapGuardIds"]=_journeyMapTarget.HasValue && _journey.Game.Map.Places.TryGetValue(_journeyMapTarget.Value,out var guardSite)?string.Join(",",guardSite.Defenders.Select(e=>e.Id)):"",
                ["world.revealedSites"]=_journey.Game.Map.RevealedSites.Count.ToString(),
                ["ui.outsideUnitArtCount"]=_root.GetComponentsInChildren<Image>().Count(i=>i.name=="OriginalOutsideUnit" && i.sprite!=null).ToString(),
                ["source.available"]=string.Join(",",_journey.Game.Globals.Mana.Dice.Select(d=>d.Face)),
                ["source.roundClaims"]=string.Join(",",_journey.Game.Globals.Mana.RoundClaims.Where(x=>ReferenceEquals(x.Value,p)).Select(x=>x.Key.Face)),
                ["source.ordinaryUsed"]=(p.HeldManaDie!=null).ToString(),
                ["source.perTurn"]=string.Join(",",p.TokensPerTurn.OrderBy(x=>(int)x.Key).Select(x=>x.Key+":"+x.Value)),
                ["offer.uses"]=(_journey.Context?.OfferedUnitUses??0).ToString(),["offer.ids"]=string.Join(",",_journey.Game.Globals.Units.Offer.Select(u=>u.Id)),
                ["offer.selected"]=_borrowedUnit?.Id??"",["offer.page"]=_borrowedPage.ToString(),["offer.attackSlot"]=_borrowedAttackSlot.ToString(),
                ["travel.harvest"]=(_journey.Context?.HarvestTriggers??0).ToString(),["travel.portal"]=(_journey.Context?.TemporalPortalPending??false).ToString(),
                ["travel.range"]=(_journey.Context?.TeleportRange??0).ToString(),["travel.drawBonus"]=(_journey.Context?.NextDrawBonus??0).ToString(),
                ["travel.space"]=(_journey.Context?.SpaceBendingActive??false).ToString(),["travel.extraTurn"]=(_journey.Context?.ExtraTurn??false).ToString(),
                ["travel.roundAside"]=p.Deck.RoundAsideCards.Count.ToString(),
                ["journey.terrain"]=_journey.Context?.CurrentTerrain.ToString()??"",["journey.mapTarget"]=TravelTargetLabel(),
                ["journey.moveCost"]=_journeyMapTarget.HasValue?_journey.MovementCost(_journeyMapTarget.Value).ToString():"",
                ["restore.selected"]=string.Join(",",_inputUnits.Select(u=>u.Card.Id)),["restore.levels"]=_inputUnits.Sum(u=>u.Card.Level).ToString(),["honor.page"]=_honorPage.ToString(),["honor.selected"]=_inputOfferedUnit?.Id??"",
                ["journey.handOpen"]=_journeyHand.ToString(),["healing"]=(_journey.Context?.Healing.Total??0).ToString(),
                ["wounds"]=p.Wounds.ToString(),["green"]=p.Mana.Crystals.GetValueOrDefault(ManaColor.Green).ToString(),
                ["rejections"]=_rejections.ToString(),["units.canActivate"]=string.Join(",",p.Units.Select(u=>u.CanActivate)),
                ["journey.completedTurns"]=_journey.CompletedTurns.ToString(),["journey.completedBattles"]=_journey.CompletedBattles.ToString(),
                ["journey.tactic"]=p.Tactic?.Card.Id ?? "",["journey.tacticResolved"]=(p.Tactic?.SelectionResolved??false).ToString(),
                ["journey.tacticSpent"]=(p.Tactic?.Spent??false).ToString(),
                ["journey.roundPrepared"]=_journey.Game.IsRoundPrepared.ToString(),["journey.canAnnounce"]=_journey.Game.CanAnnounceRoundEnd.ToString(),
                ["journey.dayPart"]=_journey.Game.Clock.DayPart.ToString(),["journey.round"]=_journey.Game.Clock.RoundIndex.ToString(),
                ["journey.tacticAside"]=p.Deck.TacticAsideCards.Count.ToString(),["journey.source.count"]=_journey.Game.Globals.Mana.Dice.Count.ToString(),
                ["journey.samePlayer"]=(_battle==null || ReferenceEquals(p,_battle.Player)).ToString(),
                ["hand"]=p.Deck.Hand.Count.ToString(),["deck.count"]=p.Deck.DrawPileCount.ToString(),["played"]=p.Deck.PlayedCards.Count.ToString(),
                ["hero.id"]=p.HeroId,["hero.level"]=p.Level.ToString(),["hero.armor"]=p.Armor.ToString(),["hero.skills"]=p.Skills.Count.ToString(),["hero.commandSlots"]=p.CommandSlots.ToString(),
                ["hero.standardStart"]=(_originalStartHero!=null).ToString(),["hero.deckTotal"]=(p.Deck.Hand.Count+p.Deck.DrawPileCount+p.Deck.PlayedCards.Count+p.Deck.DiscardPile.Count+p.Deck.TacticAsideCards.Count).ToString(),
                ["journey.discardSelected"]=_endTurnDiscards.Count.ToString(),["units.count"]=p.Units.Count.ToString(),
                ["hand.ids"]=string.Join(",",p.Deck.Hand.Select(c=>c.Id)),["discard.ids"]=string.Join(",",p.Deck.DiscardPile.Select(c=>c.Id)),
                ["skills.used"]=string.Join(",",p.Skills.Select(s=>SkillActions.IsUsed(p,s))),
                ["skills.chaosColor"]=p.ChaosColor?.ToString()??"",["skills.chaosPending"]=p.ChaosUnusedRotationPending.ToString(),
                ["skills.ignoreReputationTrack"]=(_journey.Context?.IgnoreReputationTrackEffects??false).ToString(),
                ["skills.forbidInfluenceRecruit"]=(_journey.Context?.ForbidInfluenceRecruitment??false).ToString(),
                ["skills.influenceRecruited"]=(_journey.Context?.InfluenceRecruitmentOccurred??false).ToString(),
                ["mana.infinite"]=string.Join(",",(_journey.Context?.InfiniteMana??new HashSet<ManaColor>()).OrderBy(c=>(int)c)),
                ["mana.allowBlackAtDay"]=(_journey.Context?.AllowBlackAtDay??false).ToString(),
                ["mana.allowGoldAtNight"]=(_journey.Context?.AllowGoldAtNight??false).ToString(),
                ["tokens.red"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.Red).ToString(),["tokens.blue"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.Blue).ToString(),
                ["tokens.green"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.Green).ToString(),["tokens.white"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.White).ToString(),
                ["tokens.gold"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.Gold).ToString(),["tokens.black"]=p.Mana.Tokens.GetValueOrDefault(ManaColor.Black).ToString(),
                ["white"]=p.Mana.Crystals.GetValueOrDefault(ManaColor.White).ToString(),["dice.faces"]=string.Join(",",_journey.Context?.LastManaRolls??new List<ManaColor>()),
                ["fame"]=p.Fame.ToString(),["red"]=p.Mana.Crystals.GetValueOrDefault(ManaColor.Red).ToString(),["blue"]=p.Mana.Crystals.GetValueOrDefault(ManaColor.Blue).ToString(),
                ["units.ready"]=string.Join(",",p.Units.Select(u=>u.IsReady)),["units.wounds"]=string.Join(",",p.Units.Select(u=>u.Wounds)),
                ["units.attunements"]=string.Join(",",p.Units.Select(u=>u.Card.Id+":"+u.AttunementColor+":"+u.NeedsAttunementUpkeep)),
                ["units.instances"]=string.Join(",",p.Units.Select(u=>u.Card.Id+":"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(u))),
                ["ui.tacticArtCount"]=_root.GetComponentsInChildren<RawImage>().Count(i=>i.name=="OriginalTacticArt" && i.texture!=null).ToString(),
                ["ui.journeyMapCost"]=texts.FirstOrDefault(t=>t.name=="Cost")?.text??"",
                ["ui.cardMode"]=CurrentCardMode(), ["ui.truncated"]=string.Join(",",texts.Where(t=>t.isTextOverflowing).Select(t=>t.name)),["journey.feedback"]=_feedback??"",
                ["tactic.selected"]=_tacticCards.Count.ToString(),
                ["tactic.replacementPreview"]=Math.Min(_tacticCards.Count,p.Deck.DrawPileCount+(_meditation?_tacticCards.Count:0)).ToString(),
                ["tactic.warningVisible"]=texts.Any(t=>t.name=="TacticDeckWarning").ToString()
            };
        }
    }
}
