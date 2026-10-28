using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private DeedCard _inputCard;
        private DeedCard _inputArtifact,_inputPaymentCard;
        private string _inputChoice;
        private bool _inputInBattle;
        private int _inputPage;
        private readonly List<ManaDie> _inputDice=new();
        private readonly List<DeedCard> _inputMaterials=new();
        private readonly ManaColor[] _inputColors={ManaColor.Red,ManaColor.Blue,ManaColor.Green};
        private ManaColor? _inputCrystal;
        private AdvActionCard _inputAdvanced;
        private readonly List<SpellCard> _inputSpells=new();
        private UnitState _inputUnit;
        private readonly List<UnitState> _inputUnits=new();
        private int _restorePage,_honorPage;
        private readonly List<int> _inputBonuses=new();
        private UnitCard _inputOfferedUnit;
        private UnitState _inputDisband;
        private Action<string,PveDeedSelection> _inputSubmit;
        private bool? _inputForcedEnhanced;
        private int _inputEnemy=-1,_inputAttack;
        private readonly List<MK.Logic.Runtime.Map.AxialCoord> _inputPath=new();
        private readonly HashSet<int> _inputEnemyIndices=new();
        private void OpenDeedInputs(DeedCard card,string choice,bool inBattle,bool borrowed=false)
        {
            if(!borrowed && (card.Id=="advanced_card_024" || card.Id=="advanced_card_030"&&(choice=="strong"||(_journey?.Context??_battle.Context).NextCardEnhanced)))
            {OpenCopyPicker(card,choice,inBattle);return;}
            ResetMindInputs();
            _inputSubmit=null;_inputForcedEnhanced=null;_inputArtifact=null;_inputPaymentCard=null;_inputEnemy=-1;_inputAttack=0;
            _inputPath.Clear();_inputEnemyIndices.Clear();
            _inputCard=card;_inputChoice=choice;_inputInBattle=inBattle;_inputPage=0;
            _inputDice.Clear();_inputMaterials.Clear();_inputCrystal=null;
            _inputAdvanced=null;_inputSpells.Clear();
            _inputUnit=null;_inputUnits.Clear();_restorePage=_honorPage=0;_inputBonuses.Clear();_inputOfferedUnit=null;_inputDisband=null;
            _inputColors[0]=ManaColor.Red;_inputColors[1]=ManaColor.Blue;_inputColors[2]=ManaColor.Green;
        }
        private void DrawDeedInputs()
        {
            if(_mindResultOpen){DrawMindResult();return;}
            if(_mindResponder>=0){DrawMindResponse();return;}
            var p=_journey?.Player??_battle.Player;var ctx=_journey?.Context??_battle.Context;var card=_inputCard;
            var choices=_inputInBattle?_battle.Choices(card):_journey.OutsideChoices(card);
            if(_inputForcedEnhanced.HasValue)choices=choices.Where(c=>c.Enhanced==_inputForcedEnhanced.Value).ToArray();
            var choice=choices.FirstOrDefault(c=>c.Id==_inputChoice)??choices.First();_inputChoice=choice.Id;
            bool strong=_inputForcedEnhanced??(choice.Enhanced || card.Type!=CardType.Spell && ctx.NextCardEnhanced);
            var binding=Binding(card);
            var panel=W.Panel(_root,"DeedInputs",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",(_inputArtifact!=null?"法术宝典 · ":"")+binding.source.NameCn+" · "+(strong?"强化效果":"基础效果"),40,25,1820,60,34,W.Gold);
            W.Sprite(panel,"OriginalInputCard",binding.artwork,40,110,220,319);
            string rule;
            if(binding.source is SpellCardSO spell){var half=choice.Enhanced?spell.Bottom:spell.Top;rule=half.Name+"："+half.Effect+"\n印刷费用："+string.Join("、",half.ManaCost.Select(ColorName));}
            else {var action=(ActionCardSO)binding.source;rule=strong?action.EnhancedEffect:action.BaseEffect;}
            if(_inputArtifact!=null)rule+="\n宝典已选同色弃牌；免印刷费用，额外费用仍按原效果支付。";
            var printed=W.Text(panel,"OriginalInputRule",rule,295,110,795,279,23,W.Cream);printed.enableAutoSizing=true;printed.fontSizeMin=17;printed.fontSizeMax=23;
            choices=DrawCardMode(panel,choices,_inputChoice,id=>{ResetMindInputs();_inputChoice=id;_inputDice.Clear();_inputMaterials.Clear();_inputSpells.Clear();_inputAdvanced=null;_inputUnit=null;_inputUnits.Clear();_inputBonuses.Clear();_inputPage=0;},1140,110,721,"Btn_InputChoice_");
            for(int i=0;i<choices.Length;i++)
            {
                var c=choices[i];if(c.Id=="base" || c.Id=="strong")continue;string mode=c.Enhanced?"强化":"基础";
                var b=Button(panel,"Btn_InputChoice_"+c.Id,(choice.Id==c.Id?"● ":"")+(c.Label.StartsWith(mode)?"":mode+" · ")+c.Label,
                    1140+i%2*370,170+i/2*68,351,61,()=>{ResetMindInputs();_inputChoice=c.Id;_inputDice.Clear();_inputMaterials.Clear();_inputSpells.Clear();_inputAdvanced=null;_inputUnit=null;_inputUnits.Clear();_restorePage=_honorPage=0;_inputBonuses.Clear();_inputPage=0;});
                var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=15;t.fontSizeMax=21;
            }
            int count=PveDeedInputs.ColorCount(card.Id,strong);
            var colors=(card.Id=="advanced_card_012"?new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black}:
                card.Id is "advanced_card_021" or "advanced_card_040" or "magic_006" or "magic_012" or "magic_018"?new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}:
                new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black});
            for(int row=0;row<count;row++)
            {
                int n=row;W.Text(panel,"ColorLabel_"+n,"第"+(n+1)+"枚颜色",40,474+row*65,190,46,22,W.Gold);
                for(int i=0;i<colors.Length;i++)
                {var color=colors[i];Button(panel,"Btn_InputColor_"+n+"_"+color,(_inputColors[n]==color?"● ":"")+ColorName(color),242+i*142,466+row*65,135,49,()=>{if(_inputColors[n]!=color)ResetMindInputs();_inputColors[n]=color;});}
            }
            if(card.Id=="advanced_card_012" && strong)
            {
                W.Text(panel,"CrystalLabel","可选：另付1魔力转同色魔晶",40,680,1050,39,22,W.Gold);
                Button(panel,"Btn_InputCrystal_None",_inputCrystal==null?"● 不转晶":"不转晶",40,731,190,50,()=>_inputCrystal=null);
                for(int i=0;i<4;i++){var color=(ManaColor)i;Button(panel,"Btn_InputCrystal_"+color,(_inputCrystal==color?"● ":"")+ColorName(color),251+i*214,731,200,50,()=>_inputCrystal=color);}
            }
            bool transform=(card.Id=="basic_card_009"||card.Id=="basic_card_013")&&strong;
            if(transform || card.Id=="advanced_card_022" && !strong)
            {
                W.Text(panel,"SourceSelection","按选择次序与左侧颜色对应：实际可用源骰",1140,408,726,69,22,W.Gold);
                var dice=ctx.ManaSource?.Dice;
                for(int i=0;dice!=null&&i<dice.Count;i++)
                {
                    var die=dice[i];int order=_inputDice.IndexOf(die);
                    Button(panel,"Btn_InputDie_"+i,(order>=0?"已选"+(order+1)+" · ":"")+ColorName(die.Face),1140+i%2*370,494+i/2*70,351,60,()=>
                    {if(!_inputDice.Remove(die))_inputDice.Add(die);});
                }
            }
            bool remove=PveDeedInputs.RemovesMaterial(card.Id);
            if(card.Id is "magic_004" or "magic_023")DrawWorldSpellInputs(panel,card.Id,strong);
            if(card.Id is "magic_006" or "magic_012")DrawManaSpellInputs(panel,card.Id);
            if(card.Id=="magic_018")DrawMindInputs(panel,strong);
            bool perseverance=card.Id=="advanced_card_037";
            if(card.Id=="advanced_card_038"&&!strong)DrawNatureUnitInputs(panel);
            if(card.Id=="advanced_card_040"&&strong)DrawManaPowerBonuses(panel,choice);
            bool talent=card.Id=="advanced_card_025";
            bool supply=card.Id=="advanced_card_027"||card.Id=="advanced_card_033"||talent&&strong||card.Id=="advanced_card_030";
            if(supply)DrawInputSupply(panel,card.Id=="advanced_card_033"||talent,408);
            bool discard=card.Id=="magic_002"&&strong,material=card.Id=="basic_card_023"||remove||talent&&!strong;
            bool sacrifice=card.Id=="magic_011"&&!strong;
            if(card.Id=="magic_011")DrawSacrificeInputs(panel,strong);
            if(card.Id=="magic_020"&&strong)DrawHonorSummonsInputs(panel);
            if(card.Id=="magic_005")DrawRestoreInputs(panel,strong);
            if(discard||material||perseverance||sacrifice)
            {
                var cards=(discard?p.Deck.DiscardPile:p.Deck.Hand).Where(c=>discard || !ReferenceEquals(c,card)&&!ReferenceEquals(c,_inputArtifact)&&!ReferenceEquals(c,_inputPaymentCard)&&(perseverance?strong||c.Type==CardType.Wound:remove?c.Type==CardType.Action:c.Type!=CardType.Wound)).ToArray();
                if(talent)cards=cards.Where(c=>(c.Type==CardType.Action||c.Type==CardType.Spell)&&CardColorHelper.Get(c.Id).HasValue).ToArray();
                _inputPage=Math.Min(_inputPage,Math.Max(0,(cards.Length-1)/4));
                int left=supply?40:1140;
                W.Text(panel,"MaterialsTitle",sacrifice?"可不弃牌；至多三张其他实际非伤手牌":perseverance?(strong?"可不弃牌；可选多张，但至多一张伤牌":"可不弃牌；至多选一张实际伤牌"):discard?"选择实际弃牌（选牌次序即置顶/底次序）":remove?"永久去除另一张实际原行动手牌":"选择另一张实际非创伤手牌",left,435,726,49,22,W.Gold);
                for(int i=0;i<4&&_inputPage*4+i<cards.Length;i++)
                {
                    int index=_inputPage*4+i;var c=cards[index];int order=_inputMaterials.FindIndex(selected=>ReferenceEquals(selected,c));
                    var b=Button(panel,"Btn_InputMaterial_"+index,"",left+i%2*370,490+i/2*160,351,149,()=>
                    {int selected=_inputMaterials.FindIndex(item=>ReferenceEquals(item,c));if(selected>=0)_inputMaterials.RemoveAt(selected);else{if(material||perseverance&&!strong)_inputMaterials.Clear();_inputMaterials.Add(c);}});
                    var art=Binding(c)?.artwork;if(art!=null)W.Sprite(b.transform,"OriginalMaterial",art,9,7,87,127);
                    W.Text(b.transform,"MaterialName",(order>=0?"已选"+(order+1)+" · ":"")+CardName(c),109,25,231,99,22,W.Cream);
                }
                Button(panel,"Btn_InputPrevious","上一页",left,817,351,44,()=>_inputPage--).interactable=_inputPage>0;
                Button(panel,"Btn_InputNext","下一页",left+370,817,351,44,()=>_inputPage++).interactable=(_inputPage+1)*4<cards.Length;
            }
            if(_inputSubmit!=null && choice.Target!=BattleCardTarget.None)DrawBorrowedEnemyInputs(panel);
            W.Text(panel,"Resources",$"移动 {ctx.MovementPool} · 影响力 {ctx.InfluencePool} · 手牌 {p.Deck.Hand.Count} · 创伤 {p.Wounds}\n"+
                string.Join("  ",new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White}.Select(c=>ColorName(c)+" 晶"+p.Mana.Crystals.GetValueOrDefault(c)+"/标"+p.Mana.Tokens.GetValueOrDefault(c))),40,864,1200,73,22,W.Cream);
            W.Text(panel,"Feedback",_feedback??"先完成实际选择，确认时统一校验原费用、来源与目标。",40,947,1820,41,21,W.Gold);
            if(_inputArtifact==null)DrawPaymentButton(panel,card,1280,883,575,48);
            Button(panel,"Btn_CloseInputs","返回当前行动",40,1000,890,55,()=>_inputCard=null);
            Button(panel,"Btn_ConfirmInputs","确认 · "+(strong?"强化效果":"基础效果"),970,1000,900,55,()=>
            {
                var input=new PveDeedSelection{Dice=_inputDice.ToArray(),Colors=_inputColors.Take(count).ToArray(),Cards=_inputMaterials.ToArray(),Crystal=_inputCrystal,Advanced=_inputAdvanced,Spells=_inputSpells.ToArray(),Unit=_inputUnit,Units=card.Id=="magic_005"&&strong?_inputUnits.ToArray():null,BonusChoices=_inputBonuses.ToArray(),OfferedUnit=_inputOfferedUnit,DisbandUnit=_inputDisband,TargetIndex=_inputEnemy,AttackIndex=_inputAttack,TravelPath=_inputPath.ToArray(),EnemyIndices=_inputEnemyIndices.OrderBy(i=>i).ToArray(),MindResponses=card.Id=="magic_018"?_mindResponses.ToArray():null,MindStealTarget=_mindStealCard};
                var acquiredBefore=_inputSubmit==null?CostAcquisitionBefore():null;
                if(_inputArtifact!=null)_inputSubmit(choice.Id,input);
                else WithManaPayment(card,()=>
                {
                    if(_inputSubmit!=null)_inputSubmit(choice.Id,input);
                    else if(_inputInBattle)_battle.Play(card,choice.Id,inputs:input);else _journey.PlayOutsideWithInputs(card,choice.Id,input);
                },(physical,payment)=>
                {
                    if(_inputSubmit!=null)throw new InvalidOperationException("复制或供应出牌付款尚未接入头环，请选择实际自有原卡");
                    if(_inputInBattle)_battle.PlayWithBorrowedPayment(physical,payment,card,choice.Id,inputs:input);
                    else _journey.PlayOutsideWithBorrowedPayment(physical,payment,card,choice.Id,input);
                });
                RecordCostUiNewAcquisitions(acquiredBefore,nonWoundsOnly:true);
                _inputSubmit=null;_inputForcedEnhanced=null;_inputArtifact=null;_inputPaymentCard=null;
                _mindResultOpen=card.Id=="magic_018";_mindPage=0;
                _inputCard=_mindResultOpen?card:null;_selected=null;_choice=null;_journeyCard=null;_journeyChoice=null;
                AdoptWorldBattle();
                _feedback="原效果已结算，实际卡牌、魔力与材料已保留。";
            },true).interactable=card.Id!="magic_018"||MindReady(strong);
        }
    }
}
