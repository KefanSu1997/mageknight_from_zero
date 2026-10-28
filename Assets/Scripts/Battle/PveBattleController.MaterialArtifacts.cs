using System;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using MK.Logic.Runtime.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private DeedCard _materialArtifact,_artifactMaterial;
        private AdvActionCard _artifactAdvanced;
        private SpellCard _artifactSpell;
        private string _tomeChoice;
        private int _materialPage,_staffFirst=1,_staffSecond=3,_materialEnemy=-1;
        private ManaColor _staffCrystal=ManaColor.Red;
        private UnitState _materialUnit;
        private readonly List<AxialCoord> _staffPath=new();
        private void OpenMaterialArtifact(DeedCard card)
        {
            _materialArtifact=card;_artifactMaterial=null;_artifactAdvanced=null;_artifactSpell=null;
            _tomeChoice=null;_materialPage=0;_staffFirst=1;_staffSecond=3;_staffCrystal=ManaColor.Red;
            _materialUnit=null;_materialEnemy=-1;_staffPath.Clear();
        }
        private void DrawMaterialArtifact()
        {
            if(_materialArtifact==null || _journey==null)return;
            var panel=W.Panel(_root,"MaterialArtifact",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            var card=_materialArtifact;string effectiveId=_journey.ArtifactEffectId(card);
            var binding=deedCatalog.Single(b=>b.id==effectiveId);var source=(ItemCardSO)binding.source;
            bool staff=effectiveId=="items_021",wisdom=effectiveId=="items_013";
            W.Text(panel,"Title",source.NameCn+" · 选择原牌与目标",40,25,1800,50,34,W.Gold);
            W.Sprite(panel,"OriginalArtifact",binding.artwork,40,92,218,315);
            var rule=W.Text(panel,"ArtifactRule",_artifactBurn?source.OncePerRound:source.AssignEffect,280,92,820,195,23,W.Cream);
            rule.enableAutoSizing=true;rule.fontSizeMin=17;rule.fontSizeMax=23;
            void Mode(bool burn){_artifactBurn=burn;_artifactAdvanced=null;_artifactSpell=null;_tomeChoice=null;}
            Button(panel,"Btn_Normal",(!_artifactBurn?"● ":"")+"普通",280,305,390,50,()=>Mode(false));
            Button(panel,"Btn_Burn",(_artifactBurn?"● ":"")+"焚毁",700,305,400,50,()=>Mode(true));
            W.Text(panel,"MaterialRule",wisdom?"选择实际行动手牌，永久去除；取得同色供给。":staff&&_artifactBurn?"焚毁无需材料：选择两项不同的原效果。":"选择实际有色手牌作为弃牌材料。",40,423,1060,53,22,W.Cream);
            var materials=_journey.Player.Deck.Hand.Where(c=>!ReferenceEquals(c,card) && (c.Type==CardType.Action || !wisdom&&c.Type==CardType.Spell)).ToArray();
            _materialPage=Math.Min(_materialPage,Math.Max(0,(materials.Length-1)/4));
            if(!(staff&&_artifactBurn))
            {
                for(int i=0;i<4 && _materialPage*4+i<materials.Length;i++)
                {
                    int n=_materialPage*4+i;var m=materials[n];
                    var b=Button(panel,"Btn_Material_"+n,"",40+i*269,490,252,286,()=>_artifactMaterial=m);
                    var art=Binding(m)?.artwork;if(art!=null)W.Sprite(b.transform,"OriginalCard_"+m.Id,art,48,7,156,225);
                    W.Text(b.transform,"MaterialName",(ReferenceEquals(m,_artifactMaterial)?"● ":"")+CardName(m),8,241,236,40,21,W.Cream,TextAlignmentOptions.Center);
                }
                Button(panel,"Btn_MaterialPrevious","上一页材料",40,791,252,43,()=>_materialPage--).interactable=_materialPage>0;
                Button(panel,"Btn_MaterialNext","下一页材料",848,791,252,43,()=>_materialPage++).interactable=(_materialPage+1)*4<materials.Length;
            }
            if(staff)DrawStaffInputs(panel);
            else
            {
                string[] ids=wisdom&&!_artifactBurn?_journey.Game.Globals.AdvActions.Offer.Select(c=>c.Id).ToArray():_journey.Game.Globals.Spells.Offer.Select(c=>c.Id).ToArray();
                W.Text(panel,"OfferRule",wisdom?"实际公共供应 · 取得后立即补位":"实际法术供应 · 借用不取得供给牌",1140,92,740,48,23,W.Gold);
                for(int i=0;i<ids.Length;i++)
                {
                    int n=i;string id=ids[i];var d=deedCatalog.Single(c=>c.id==id);
                    bool selected=_artifactAdvanced?.Id==id || _artifactSpell?.Id==id;
                    var b=Button(panel,"Btn_ArtifactOffer_"+n,"",1140+i*247,157,232,286,()=>
                    {
                        if(wisdom&&!_artifactBurn)_artifactAdvanced=_journey.Game.Globals.AdvActions.Offer[n];
                        else _artifactSpell=_journey.Game.Globals.Spells.Offer[n];
                        _tomeChoice=null;
                    });
                    W.Sprite(b.transform,"OriginalOffer",d.artwork,38,7,156,225);
                    W.Text(b.transform,"OfferName",(selected?"● ":"")+d.source.NameCn,7,241,218,40,21,W.Cream,TextAlignmentOptions.Center);
                }
                if(!wisdom)
                {
                    var choices=_journey.TomeChoices(_artifactSpell,_artifactBurn);
                    for(int i=0;i<choices.Length;i++)
                    {
                        var choice=choices[i];Button(panel,"Btn_TomeChoice_"+choice.Id,(_tomeChoice==choice.Id?"● ":"")+choice.Label,1140,462+i*44,726,40,()=>_tomeChoice=choice.Id);
                    }
                    if(_artifactSpell!=null && choices.Length==0)W.Text(panel,"NoSpellChoice","该法术在当前阶段尚无可用操作分支。",1140,470,726,55,21,W.Muted);
                    var selected=choices.FirstOrDefault(c=>c.Id==_tomeChoice);
                    if(selected?.Target==BattleCardTarget.EnemyIndex)
                        for(int i=0;i<_battle.Enemies.Count;i++)
                        {
                            int n=i;Button(panel,"Btn_MaterialEnemy_"+i,(_materialEnemy==i?"● ":"")+"敌人 "+(i+1)+" · "+_battle.Enemies[i].Id,1140,661+i*45,726,41,()=>_materialEnemy=n).interactable=!_journey.Context.DefeatedEnemyIndices.Contains(i);
                        }
                    else DrawMaterialUnits(panel,661);
                }
            }
            W.Text(panel,"MaterialFeedback",_feedback??"确认前不会消耗材料；颜色、费用与阶段按原规则校验。",40,884,1830,83,22,W.Gold);
            Button(panel,"Btn_ConfirmMaterial","按原规则确认",985,998,885,59,()=>
            {
                if(effectiveId=="items_023" && _artifactSpell!=null && PveDeedInputs.Supports(_artifactSpell.Id))
                {
                    var offered=_artifactSpell;var payment=_artifactMaterial;bool burn=_artifactBurn;
                    var chosen=_journey.TomeChoices(offered,burn).SingleOrDefault(c=>c.Id==_tomeChoice);
                    if(chosen==null)throw new InvalidOperationException("请选择原法术效果");
                    if(payment==null)throw new InvalidOperationException("请选择实际同色弃牌");
                    OpenDeedInputs(new DeedCard(offered.Id,CardType.Spell),chosen.Id,_journey.Context.InBattle,true);
                    _inputArtifact=card;_inputPaymentCard=payment;_inputForcedEnhanced=burn;
                    _inputEnemy=_materialEnemy;_inputUnit=_materialUnit;
                    _inputSubmit=(choice,input)=>
                    {
                        _journey.PlayMaterialArtifact(card,burn,payment,spell:offered,spellChoice:choice,target:input.TargetIndex,unit:input.Unit,inputs:input);
                        _materialArtifact=null;
                    };
                    return;
                }
                var acquiredBefore=wisdom?CostAcquisitionBefore():null;
                _journey.PlayMaterialArtifact(card,_artifactBurn,_artifactMaterial,_artifactAdvanced,_artifactSpell,_tomeChoice,_materialEnemy,_materialUnit,_staffFirst,_staffSecond,_staffCrystal,_staffPath);
                if(wisdom)RecordCostUiNewAcquisitions(acquiredBefore);
                _materialArtifact=null;_journeyCard=null;_selected=null;_feedback="原神器已结算；请继续消费所得资源或原牌。";
            },true);
            Button(panel,"Btn_CloseMaterial","返回 · 保留原牌",40,998,905,59,()=>_materialArtifact=null);
        }
        private void DrawMaterialUnits(Transform panel,int y)
        {
            for(int i=0;i<_journey.Player.Units.Count;i++)
            {
                var u=_journey.Player.Units[i];Button(panel,"Btn_MaterialUnit_"+i,(ReferenceEquals(u,_materialUnit)?"● ":"")+u.Card.NameCn+" · 等级"+u.Card.Level,1140,y+i*45,726,41,()=>_materialUnit=ReferenceEquals(u,_materialUnit)?null:u);
            }
        }
        private void DrawStaffInputs(Transform panel)
        {
            string[] names={"白：最多移动两格","蓝：同色魔晶2","红：重整最高3级部队","绿：治疗3"};
            W.Text(panel,"StaffRule","第一项"+(_artifactBurn?" / 第二项（须不同）":""),1140,92,730,48,25,W.Gold);
            for(int i=0;i<4;i++)
            {
                int n=i;Button(panel,"Btn_StaffFirst_"+i,(_staffFirst==i?"● ":"")+names[i],1140,153+i*49,355,45,()=>_staffFirst=n);
                if(_artifactBurn)Button(panel,"Btn_StaffSecond_"+i,(_staffSecond==i?"● ":"")+names[i],1510,153+i*49,356,45,()=>_staffSecond=n);
            }
            if(_staffFirst==1 || _artifactBurn&&_staffSecond==1)
                for(int i=0;i<4;i++)
                {
                    var c=(ManaColor)i;Button(panel,"Btn_StaffCrystal_"+c,(_staffCrystal==c?"● ":"")+ColorName(c),1140+i%2*370,367+i/2*48,356,44,()=>_staffCrystal=c);
                }
            if(_staffFirst==2 || _artifactBurn&&_staffSecond==2)DrawMaterialUnits(panel,480);
            if(_staffFirst==0 || _artifactBurn&&_staffSecond==0)
            {
                W.Text(panel,"StaffPath","路径："+string.Join(" → ",_staffPath.Select(c=>$"({c.Q},{c.R})")),1140,480,726,53,21,W.Cream);
                int i=0;
                foreach(var hex in _journey.Game.Map.Placed.Keys)
                {
                    var cell=hex;int n=i++;Button(panel,"Btn_StaffHex_"+cell.Q+"_"+cell.R,$"({cell.Q},{cell.R})",1140+n%3*247,547+n/3*46,230,42,()=>
                    {if(_staffPath.Count>=2)throw new InvalidOperationException("原法杖至多两格；请先清除路径");_staffPath.Add(cell);});
                }
                Button(panel,"Btn_ClearStaffPath","清除路径 · 可以选择不移动",1140,766,726,46,()=>_staffPath.Clear());
            }
        }
    }
}
