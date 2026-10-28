using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _sharedSkillsOpen;
        private SkillCard _sharedOwned;
        private SharedSkillToken _sharedToken;
        private readonly List<DeedCard> _sharedCards=new();
        private ManaColor _sharedColor=ManaColor.Red;
        private ManaDie _sharedDie;
        private Monster _sharedEnemy, _sharedArmorEnemy;
        private int _sharedAttack, _sharedPage;
        private SidewaysType _sharedSideways=SidewaysType.Move;
        private SharedSkillArea SharedArea=>_journey.Game.SharedSkills;
        private void OpenSharedSkills(SkillCard skill=null)
        {
            _sharedSkillsOpen=true;_sharedOwned=skill;_sharedToken=null;_sharedCards.Clear();
            _sharedEnemy=_sharedArmorEnemy=null;_sharedDie=null;_sharedPage=_sharedAttack=0;
            _sharedSideways=_journey.Context.InBattle?(_battle.Phase==Phase.Block?SidewaysType.Block:SidewaysType.Attack):SidewaysType.Move;
        }
        private bool SharedChoicePending => _journey?.Game.HasActiveTurn==true &&
            (SharedArea.PendingHowls(_journey.Context).Count>0 || SharedArea.PendingOverloadChoices(_journey.Context).Count>0);
        private void DrawSharedSkillsIfNeeded()
        {
            if(_journey?.Game.HasActiveTurn!=true || _journey.Game.IsEndingTurn)return;
            if(!_sharedSkillsOpen && !SharedChoicePending)return;
            var player=_journey.Player;var ctx=_journey.Context;var area=SharedArea;
            var howls=area.PendingHowls(ctx);var outputs=area.PendingOverloadChoices(ctx);
            var skill=howls.Count>0?howls[0].Skill:_sharedToken?.Skill??_sharedOwned;
            var panel=W.Panel(_root,"SharedSkills",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",player.Name+" · 互动技能与中央区",40,22,1810,58,34,W.Gold);
            W.Text(panel,"State","移动 "+ctx.MovementPool+" · 影响 "+ctx.InfluencePool+" · 手牌创伤 "+player.Wounds+" · 中央标记 "+area.Tokens.Count,530,318,1340,48,24,W.Cream);
            if(skill!=null)
            {
                Texture(panel,"OriginalSkill",SkillReference(skill),40,95,205,287);
                var text=W.Text(panel,"OriginalRule",skill.PrintedRule,280,98,1590,206,24,W.Cream);
                text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=24;
            }
            else W.Text(panel,"Instruction","选择本人技能发动，或选择中央的实际标记领取。\n共享技能的主人、颜色和归还时机保留在同一局中。",40,104,1810,170,27,W.Cream);
            W.Text(panel,"OwnLabel","本人互动技能",40,402,450,38,24,W.Gold);
            var own=player.Skills.Where(s=>s.IsInteractive==true).ToArray();
            for(int i=0;i<own.Length;i++)
            {
                var item=own[i];Button(panel,"Btn_Own_"+i,item.Id+(SkillActions.IsUsed(player,item)?" · 已用":""),40,447+i*60,450,54,()=>OpenSharedSkills(item),ReferenceEquals(item,_sharedOwned));
            }
            W.Text(panel,"CentralLabel","中央 · 可由实际玩家领取",40,527,450,40,24,W.Gold);
            var tokens=area.Tokens.ToArray();
            for(int i=0;i<tokens.Length;i++)
            {
                var token=tokens[i];Button(panel,"Btn_Token_"+i,token.Owner.Name+" · "+token.Skill.Id+(token.Color.HasValue?" · "+ColorName(token.Color.Value):""),40,578+i*76,450,70,()=>
                { _sharedToken=token;_sharedOwned=null;_sharedCards.Clear();_sharedDie=null;_sharedPage=0; },ReferenceEquals(token,_sharedToken));
            }
            bool mandatory=howls.Count>0 || outputs.Count>0;
            if(outputs.Count>0)
            {
                W.Text(panel,"Pending","本牌有多种实际贡献，请选择其中一项加4。",530,402,1340,60,25,W.Gold);
                for(int i=0;i<outputs.Count;i++){int index=i;Button(panel,"Btn_Output_"+i,outputs[i].Label,530,485+i*70,1340,60,()=>area.ResolveOverload(player,ctx,index),true);}
            }
            else if(howls.Count>0)
            {
                DrawSharedEnemyChoices(panel,true);
                Button(panel,"Btn_Confirm","确认本次群狼减甲与减攻",530,920,1340,63,()=>
                {area.ResolveHowl(player,ctx,_sharedArmorEnemy,_sharedEnemy,_sharedAttack);_sharedEnemy=_sharedArmorEnemy=null;},true);
            }
            else
            {
                var returns=area.PendingSourceOpeningReturns(player,ctx);
                if(returns.Count>0)
                {
                    W.Text(panel,"ReturnRule","这些原骰须选择回合末是否重掷；选择后仍留在本人持有区直到正式回合末。",530,395,1340,76,24,W.Gold);
                    for(int i=0;i<returns.Count;i++)
                    {
                        var die=returns[i];W.Text(panel,"ReturnDie_"+i,ColorName(die.Face)+"色 · 技能额外原骰",530,490+i*88,600,60,23,W.Cream);
                        Button(panel,"Btn_ReturnRoll_"+i,"回合末重掷",1160,490+i*88,335,61,()=>area.ChooseSourceOpeningReturn(player,ctx,die,true));
                        Button(panel,"Btn_ReturnKeep_"+i,"保持骰面归还",1515,490+i*88,355,61,()=>area.ChooseSourceOpeningReturn(player,ctx,die,false));
                    }
                }
                else DrawSharedSelected(panel,skill);
            }
            W.Text(panel,"Feedback",_feedback??"实际费用、原牌和标记在确认后才发生变化。",40,992,1830,40,20,W.Muted);
            Button(panel,"Btn_Back","返回当前玩家操作",40,925,450,58,()=>_sharedSkillsOpen=false).interactable=!mandatory;
        }
        private void DrawSharedSelected(Transform panel,SkillCard skill)
        {
            var p=_journey.Player;var c=_journey.Context;var a=SharedArea;
            string id=skill?.SourceId;
            if(skill==null)
            {
                if(c.InBattle && _battle.Phase==Phase.Block && _battle.Enemies.Any(e=>NatureRevengeActions.HasCumbersome(c,e)))
                {
                    DrawSharedEnemyChoices(panel,false);
                    Button(panel,"Btn_Cumbersome","支付实际移动1：所选笨重攻击 -1",530,920,1340,63,()=>NatureRevengeActions.SpendMove(c,_sharedEnemy,_sharedAttack,1),true);
                }
                return;
            }
            if(id==SharedSkillArea.PainId || id==SharedSkillArea.HowlId)
            {
                bool pickWounds=id==SharedSkillArea.PainId;
                var cards=p.Deck.Hand.Where(card=>!pickWounds || card.Type==CardType.Wound).ToArray();
                _sharedPage=Math.Min(_sharedPage,Math.Max(0,(cards.Length-1)/6));
                if(_sharedToken!=null || id==SharedSkillArea.HowlId)
                    for(int i=0;i<4;i++)
                    {
                        var type=(SidewaysType)i;
                        Button(panel,"Btn_Sideways_"+type,new[]{"移动","影响力","格挡","攻击"}[(int)type],530+i*335,400,320,53,()=>_sharedSideways=type,_sharedSideways==type);
                    }
                else W.Text(panel,"PickRule","选择零至两张实际伤牌移除；其余伤牌保持原实体。",530,400,1340,60,24,W.Gold);
                for(int slot=0;slot<6 && _sharedPage*6+slot<cards.Length;slot++)
                {
                    int index=_sharedPage*6+slot;var card=cards[index];bool selected=_sharedCards.Any(x=>ReferenceEquals(x,card));
                    var button=Button(panel,"Btn_Card_"+index,(selected?"● ":"")+CardName(card),530+slot%3*447,493+slot/3*167,425,148,()=>
                    {int old=_sharedCards.FindIndex(x=>ReferenceEquals(x,card));if(old>=0)_sharedCards.RemoveAt(old);else{if(_sharedToken!=null || id==SharedSkillArea.HowlId)_sharedCards.Clear();_sharedCards.Add(card);}},selected);
                    var art=CardArtwork(card);
                    if(art!=null)W.Sprite(button.transform,"OriginalCard",art,12,10,80,116);
                    var label=button.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=24;
                }
                Button(panel,"Btn_PreviousCards","上一页",530,839,420,52,()=>_sharedPage--).interactable=_sharedPage>0;
                Button(panel,"Btn_NextCards","下一页",1450,839,420,52,()=>_sharedPage++).interactable=(_sharedPage+1)*6<cards.Length;
            }
            else if(id==SharedSkillArea.NatureId)DrawSharedEnemyChoices(panel,false);
            else if(id==SharedSkillArea.SourceOpeningId)
            {
                W.Text(panel,"DiceRule",_sharedToken==null?"可选源中一骰重掷；不选即不重掷。":"选择一颗基本色实际源骰，作为本回合额外骰使用。",530,400,1340,70,24,W.Gold);
                var dice=c.ManaSource.Dice.ToArray();
                for(int i=0;i<dice.Length;i++){var die=dice[i];Button(panel,"Btn_Die_"+i,ColorName(die.Face)+"色原骰",530+i%3*447,510+i/3*80,425,65,()=>_sharedDie=ReferenceEquals(_sharedDie,die)?null:die,ReferenceEquals(_sharedDie,die));}
            }
            else if(_sharedToken==null && (id==SharedSkillArea.ManaEnhanceId || id==SharedSkillArea.ManaOverloadId))
            {
                var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Black};
                for(int i=0;i<colors.Length;i++)
                {
                    var color=colors[i];Button(panel,"Btn_Color_"+color,ColorName(color),530+i*267,440,250,61,()=>_sharedColor=color,_sharedColor==color).interactable=id!=SharedSkillArea.ManaEnhanceId || color!=ManaColor.Black;
                }
                W.Text(panel,"ManaRule",id==SharedSkillArea.ManaEnhanceId?"确认仅预选本次付款颜色；之后出原牌或支付正式费用，成功支付才返晶并放中央。":"自己获得选色魔力标记；中央标记仅标识颜色。首次用该色实际供能的合格原牌得到 +4。",530,540,1340,146,24,W.Cream);
                if(id==SharedSkillArea.ManaEnhanceId)Button(panel,"Btn_CancelEnhancement","取消付款预选",530,760,1340,55,()=>a.CancelEnhancement(p,c));
            }
            else if(id==SharedSkillArea.ManaOverloadId || id==SharedSkillArea.HowlId)
                W.Text(panel,"Automatic","此标记在下一次符合原规则的实际操作中自动领取。",530,430,1340,110,26,W.Gold);
            Button(panel,"Btn_Confirm",_sharedToken==null?"确认发动 / 付款预选":"确认领取实际标记",530,920,1340,63,ConfirmSharedSkill,true)
                .interactable=_sharedToken==null || id!=SharedSkillArea.ManaOverloadId && id!=SharedSkillArea.HowlId;
        }
        private void DrawSharedEnemyChoices(Transform panel,bool howl)
        {
            var c=_journey.Context;
            W.Text(panel,"EnemyRule",howl?"分别选择减甲敌人与减攻目标；可以是同一名敌人。":"选择实际敌人与攻击序号。笨重支付只在格挡阶段。",530,398,1340,65,24,W.Gold);
            if(!c.InBattle || _battle==null)return;
            for(int i=0;i<_battle.Enemies.Count;i++)
            {
                int index=i;var enemy=_battle.Enemies[i];if(c.DefeatedEnemyIndices.Contains(i))continue;
                string name=worldEnemyCatalog.Concat(Encounter.enemies).FirstOrDefault(e=>e.originalId==enemy.Id)?.displayName??enemy.Id;
                Button(panel,"Btn_Enemy_"+i,name+" · 减攻目标",530,493+i*77,howl?650:1340,66,()=>{_sharedEnemy=enemy;_sharedAttack=0;},ReferenceEquals(_sharedEnemy,enemy));
                if(howl)Button(panel,"Btn_ArmorEnemy_"+i,name+" · 减甲目标",1220,493+i*77,650,66,()=>_sharedArmorEnemy=enemy,ReferenceEquals(_sharedArmorEnemy,enemy));
            }
            if(_sharedEnemy!=null)Button(panel,"Btn_AttackSlot","所选敌人攻击 "+(_sharedAttack+1)+" / "+_sharedEnemy.AttackCount,530,837,1340,55,()=>_sharedAttack=(_sharedAttack+1)%_sharedEnemy.AttackCount);
        }
        private void ConfirmSharedSkill()
        {
            var p=_journey.Player;var c=_journey.Context;var a=SharedArea;var skill=_sharedToken?.Skill??_sharedOwned;
            if(skill==null)throw new InvalidOperationException("先选择自有技能或中央标记");
            bool claim=_sharedToken!=null;
            switch(skill.SourceId)
            {
                case SharedSkillArea.PainId:
                    if(claim)a.ClaimPain(p,c,_sharedToken,_sharedCards.SingleOrDefault(),_sharedSideways);else a.UsePain(p,c,skill,_sharedCards.ToArray());break;
                case SharedSkillArea.CalmId:
                    if(claim)a.ClaimCalm(p,c,_sharedToken);else a.UseCalm(p,c,skill);break;
                case SharedSkillArea.NatureId:
                    if(claim)a.ClaimNature(p,c,_sharedToken,_sharedEnemy,_sharedAttack);else a.UseNature(p,c,skill,_sharedEnemy,_sharedAttack);break;
                case SharedSkillArea.ManaEnhanceId:
                    if(claim)a.ClaimEnhancement(p,c,_sharedToken);else a.ArmEnhancement(p,c,skill,_sharedColor);break;
                case SharedSkillArea.SourceOpeningId:
                    if(claim)a.ClaimSourceOpening(p,c,_sharedToken,_sharedDie);else a.UseSourceOpening(p,c,skill,_sharedDie);break;
                case SharedSkillArea.ManaOverloadId:a.UseOverload(p,c,skill,_sharedColor);break;
                case SharedSkillArea.HowlId:a.UseHowl(p,c,skill,_sharedCards.SingleOrDefault(),_sharedSideways);break;
                default:throw new InvalidOperationException("该共享技能尚未接入");
            }
            _feedback=skill.SourceId==SharedSkillArea.ManaEnhanceId && !claim?"已预选魔力强化；下一次实际付款成功时生效。":"共享技能操作已完成。";
            _sharedOwned=null;_sharedToken=null;_sharedCards.Clear();
            if(skill.SourceId==SharedSkillArea.ManaEnhanceId && !claim)_sharedSkillsOpen=false;
        }
    }
}
