using System;
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
        private DeedCard _specialCard;
        private UnitCard _specialOffer;
        private int _specialOfferPage;
        private UnitState _specialUnit;
        private SkillCard _publicSkill;
        private int _publicSkillOption,_publicSkillPage,_publicEnemy=-1;
        private void OpenSpecialArtifact(DeedCard card)
        {
            _specialCard=card;_specialOffer=null;_specialOfferPage=0;_specialUnit=null;_publicSkill=null;_publicSkillOption=0;_publicSkillPage=0;_publicEnemy=-1;ResetArtifactInputs();
        }
        private void DrawSpecialArtifact()
        {
            if(_specialCard==null || _journey==null)return;
            var p=_journey.Player;var ctx=_journey.Context;var card=_specialCard;
            bool box=card.Id=="items_024";var pending=ctx.MysticBox;
            bool revealed=box&&pending!=null&&ReferenceEquals(pending.Box,card);
            string id=_journey.ArtifactEffectId(card);var binding=deedCatalog.Single(c=>c.id==id);var original=(ItemCardSO)binding.source;
            var panel=W.Panel(_root,"SpecialArtifact",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",(box?"神秘宝盒 · ":"")+original.NameCn,40,25,1800,58,34,W.Gold);
            W.Sprite(panel,"OriginalSpecialArtifact",binding.artwork,40,110,220,319);
            var text=W.Text(panel,"OriginalSpecialRule",_artifactBurn?original.OncePerRound:original.AssignEffect,295,110,795,190,23,W.Cream);
            text.enableAutoSizing=true;text.fontSizeMin=17;text.fontSizeMax=23;
            if(box)W.Text(panel,"BoxIdentity",revealed?"供应原神器暂时揭示。使用/焚毁移动宝盒本体；回合末原神器归供应底。":"先揭示实际神器牌堆顶；可以保留至回合末不使用。",295,311,795,109,22,W.Gold);
            Button(panel,"Btn_SpecialNormal",(!_artifactBurn?"● ":"")+"普通",40,456,510,53,()=>_artifactBurn=false).interactable=!box||revealed;
            Button(panel,"Btn_SpecialBurn",(_artifactBurn?"● ":"")+"焚毁",580,456,510,53,()=>_artifactBurn=true).interactable=!box||revealed;
            if(id=="items_017" && _artifactBurn)
            {
                var offer=_journey.Game.Globals.Units.Offer;
                _specialOfferPage=Math.Min(_specialOfferPage,Math.Max(0,(offer.Count-1)/3));
                for(int slot=0;slot<3 && _specialOfferPage*3+slot<offer.Count;slot++)
                {
                    int i=_specialOfferPage*3+slot;
                    var unit=offer[i];var art=unitCatalog.Single(u=>u.originalId==unit.Id).artwork;
                    var b=Button(panel,"Btn_FreeOffer_"+i,"",1140+slot*246,110,232,338,()=>_specialOffer=unit);
                    W.Sprite(b.transform,"OriginalFreeUnit",art,18,8,196,250);
                    W.Text(b.transform,"FreeUnitName",(ReferenceEquals(unit,_specialOffer)?"● ":"")+unit.NameCn,6,271,220,62,22,W.Cream,TextAlignmentOptions.Center);
                }
                Button(panel,"Btn_FreeOfferPrevious","上一页",1140,457,230,43,()=>_specialOfferPage--).interactable=_specialOfferPage>0;
                W.Text(panel,"FreeOfferPage",(_specialOfferPage+1)+" / "+Math.Max(1,(offer.Count+2)/3),1390,457,200,43,23,W.Cream,TextAlignmentOptions.Center);
                Button(panel,"Btn_FreeOfferNext","下一页",1610,457,256,43,()=>_specialOfferPage++).interactable=(_specialOfferPage+1)*3<offer.Count;
                W.Text(panel,"DisbandRule","满编时选择下方部队解散；确认前不消耗。",1140,508,730,45,21,W.Cream);
            }
            if(id=="items_022")
            {
                var skills=_journey.Game.Globals.Skills.Offer;
                _publicSkillPage=Math.Min(_publicSkillPage,Math.Max(0,(skills.Count-1)/6));
                for(int i=0;i<6&&_publicSkillPage*6+i<skills.Count;i++)
                {
                    int n=_publicSkillPage*6+i;var s=skills[n];var b=Button(panel,"Btn_PublicSkill_"+n,(ReferenceEquals(s,_publicSkill)?"● ":"")+s.Id,1140,110+i*57,726,51,()=>{_publicSkill=s;_publicSkillOption=0;});
                    var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=16;t.fontSizeMax=22;
                }
                Button(panel,"Btn_PublicPrevious","上一页",1140,470,347,43,()=>_publicSkillPage--).interactable=_publicSkillPage>0;
                Button(panel,"Btn_PublicNext","下一页",1510,470,356,43,()=>_publicSkillPage++).interactable=(_publicSkillPage+1)*6<skills.Count;
                if(skills.Count==0)W.Text(panel,"EmptyPublicSkills","公共技能区为空。正式升级时未选择的技能会进入这里。",1140,135,726,115,24,W.Muted);
                if(_publicSkill!=null)
                {
                    Texture(panel,"OriginalPublicSkill",SkillReference(_publicSkill),40,554,210,294);
                    var rule=W.Text(panel,"PublicSkillRule",_publicSkill.PrintedRule,280,554,810,260,23,W.Cream);rule.enableAutoSizing=true;rule.fontSizeMin=16;rule.fontSizeMax=23;
                    if(!_artifactBurn)
                    {
                        var choices=_journey.HeadbandChoices(_publicSkill);
                        for(int i=0;i<choices.Length;i++)
                        {
                            var c=choices[i];Button(panel,"Btn_PublicOption_"+c.Option,(_publicSkillOption==c.Option?"● ":"")+c.Label,1140,544+i*52,726,47,()=>_publicSkillOption=c.Option);
                        }
                        if(_publicSkill.SourceId=="hero_skill_pool_007:04" && ctx.InBattle)
                            for(int i=0;i<_battle.Enemies.Count;i++)
                            {
                                int n=i;Button(panel,"Btn_PublicEnemy_"+i,(_publicEnemy==i?"● ":"")+"敌人 "+(i+1),1140,665+i*46,726,42,()=>_publicEnemy=n).interactable=!ctx.DefeatedEnemyIndices.Contains(i);
                            }
                    }
                }
            }
            else
            {
                if(PveArtifactInputs.IsResource(id))DrawArtifactColors(panel,card,_artifactBurn,1140,110,726,61,id);
                for(int i=0;i<p.Units.Count;i++)
                {
                    var unit=p.Units[i];Button(panel,"Btn_SpecialUnit_"+i,(ReferenceEquals(unit,_specialUnit)?"● ":"")+unit.Card.NameCn+" · 创伤"+unit.Wounds,1140,557+i*49,726,44,()=>_specialUnit=ReferenceEquals(unit,_specialUnit)?null:unit);
                }
            }
            W.Text(panel,"SpecialFeedback",_feedback??(box&&pending?.Used==true?"本回合宝盒已经使用；原神器将于回合末归底。":"按实际供应与原效果选择；确认前不会消耗。"),40,888,1830,83,22,W.Gold);
            Button(panel,"Btn_ConfirmSpecial",box&&!revealed?"揭示牌堆顶原神器":PveArtifactInputs.IsMaterial(id)?"继续选择实际材料":"按原规则确认",990,998,880,58,()=>
            {
                if(box&&!revealed){_journey.RevealBox(card);return;}
                if(PveArtifactInputs.IsMaterial(id)){_specialCard=null;OpenMaterialArtifact(card);return;}
                if(box && ctx.InBattle && (id is "items_008" or "items_009" or "items_016" or "items_019" or "items_020" || id=="items_001" && _artifactBurn))
                {
                    _specialCard=null;_journeyHand=false;_selected=card;_selectedUnit=null;_selectedSkill=null;_showUnits=_showSkills=false;
                    _targets.Clear();_materials.Clear();_selectMaterials=false;_hornBonus=0;_shieldSplit=false;
                    _feedback="按揭示的原神器选择材料或目标；实际使用的是宝盒。";return;
                }
                if(id=="items_022" && !_artifactBurn && HasDynamicHeadbandInput(_publicSkill))
                {OpenHeadbandInput(card,_publicSkill);return;}
                _journey.PlaySpecialArtifact(card,_artifactBurn,_specialOffer,id=="items_017"&&_artifactBurn?_specialUnit:null,_publicSkill,_publicSkillOption,_publicEnemy,_specialUnit,
                    _artifactColors.Take(PveArtifactInputs.ColorCount(id,_artifactBurn)).ToArray());
                _specialCard=null;_journeyCard=null;_selected=null;_feedback="原效果已结算；实际卡与供应去向已保留。";
            },true).interactable=!box||!revealed||!pending.Used;
            Button(panel,"Btn_CloseSpecial","返回当前行动",40,998,910,58,()=>_specialCard=null);
        }
    }
}

