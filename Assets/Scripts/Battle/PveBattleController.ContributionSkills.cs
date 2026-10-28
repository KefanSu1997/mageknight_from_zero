using System;
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
        private SkillCard _contributionSkill, _leadershipSkill;
        private AttackProfile _skillAttack;
        private BlockAllocation _skillBlock;
        private int _contributionPage;
        private static string ContributionElement(Element e)=>e switch{Element.Fire=>"火焰",Element.Ice=>"寒冰",Element.ColdFire=>"冰火",_=>"物理"};
        private void OpenContributionSkill(SkillCard skill)
        { _contributionSkill=skill;_skillAttack=null;_skillBlock=null;_contributionPage=0; }
        private void UseSelectedUnit(UnitState unit,int abilityIndex,int option=0,int target=-1,ManaColor[] manaColors=null,int boost=0,int attackIndex=0)
        {
            if(IsHeadbandInput(_leadershipSkill))
            {
                WithManaPayment(unit,()=>_journey.UseHeadbandLeadership(_headbandPhysical,_headbandSkill,
                    unit,abilityIndex,option,target,manaColors,boost,attackIndex));
                AfterHeadbandInput();return;
            }
            WithManaPayment(unit,()=>_battle.UseUnit(unit,abilityIndex,option,target,manaColors,boost,attackIndex,_leadershipSkill),
                (physical,payment)=>
                {
                    if(_leadershipSkill!=null)throw new InvalidOperationException("请先取消领导，再选择头环付款");
                    _battle.UseUnitWithBorrowedPayment(physical,payment,unit,abilityIndex,option,target,manaColors,boost,attackIndex);
                });
            _leadershipSkill=null;
        }
        private void DrawContributionSkill()
        {
            bool soul=_contributionSkill.SourceId=="hero_skill_pool_003:00";
            bool borrowed=IsHeadbandInput(_contributionSkill);
            var attacks=_battle.Context.CombatPower.AvailableAttacks(_battle.Phase,_battle.Context.AllAttacksSiege)
                .Where(a=>a.SourceCard!=null && a.SourceUnit==null).ToArray();
            var blocks=_battle.Context.CombatPower.Blocks.ToArray();
            int count=soul?blocks.Length:attacks.Length;
            _contributionPage=Math.Min(_contributionPage,Math.Max(0,(count-1)/4));
            var panel=W.Panel(_root,"ContributionSkill",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",_contributionSkill.Id+" · 选择实际"+(soul?"格挡":"原牌攻击"),40,25,1800,65,36,W.Gold);
            W.Text(panel,"Instruction",borrowed?HeadbandInputInstruction():"加值保留该笔的原元素、攻击方式和附效；结算时由同一实际贡献消费。",40,104,1800,65,24,W.Cream);
            for(int slot=0;slot<4 && _contributionPage*4+slot<count;slot++)
            {
                int index=_contributionPage*4+slot;
                var attack=soul?null:attacks[index];var block=soul?blocks[index]:null;
                string id=attack?.SourceCard switch {DeedCard c=>c.Id,SpellCard c=>c.Id,_=>null};
                var binding=id==null?null:deedCatalog.SingleOrDefault(d=>d.id==id);
                string name=soul?"实际格挡 #"+(index+1):binding?.source.NameCn??id;
                string label=name+"\n"+(soul?ContributionElement(block.Element):(attack.Type==AttackType.Melee?"攻击":attack.Type==AttackType.Ranged?"远程":"攻城")+" · "+ContributionElement(attack.Element))+" "+(soul?block.Value:attack.Value)
                    +" → "+((soul?block.Value:attack.Value)+(soul?1:_battle.Phase==Phase.Ranged?1:2));
                var button=Button(panel,"Btn_Part_"+index,"",40+slot%2*475,200+slot/2*275,450,251,
                    ()=>{_skillAttack=attack;_skillBlock=block;},soul?ReferenceEquals(block,_skillBlock):ReferenceEquals(attack,_skillAttack));
                if(binding?.artwork!=null)W.Sprite(button.transform,"OriginalCard",binding.artwork,12,15,142,211);
                W.Text(button.transform,"Contribution",label,binding==null?25:170,40,binding==null?400:265,178,24,W.Cream);
            }
            Texture(panel,"OriginalSkillReference",SkillReference(_contributionSkill),1050,200,260,365);
            W.Text(panel,"OriginalRule",_contributionSkill.PrintedRule,1350,200,500,365,25,W.Cream);
            if(count==0)W.Text(panel,"Empty","请先打出原牌或激活部队，产生当前阶段的实际贡献。",40,210,920,120,27,W.Muted);
            Button(panel,"Btn_Previous","上一页",40,790,450,56,()=>_contributionPage--).interactable=_contributionPage>0;
            Button(panel,"Btn_Next","下一页",515,790,450,56,()=>_contributionPage++).interactable=(_contributionPage+1)*4<count;
            W.Text(panel,"Feedback",_feedback??"确认后加值只属于这一次所选贡献。",40,882,1820,65,24,W.Cream);
            if(borrowed)DrawHeadbandFinish(panel);
            Button(panel,"Btn_Back","返回战斗",40,990,880,60,()=>{_contributionSkill=null;if(borrowed)ClearHeadbandInput();})
                .interactable=!borrowed || _journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","确认所选加值",960,990,880,60,()=>
            {
                if(borrowed){_journey.UseHeadbandBullseye(_headbandPhysical,_headbandSkill,_skillAttack);AfterHeadbandInput();return;}
                if(soul)_battle.ApplySoulGuideBlock(_skillBlock);
                else _battle.BoostCardAttack(_contributionSkill,_skillAttack);
                _contributionSkill=null;
            },true).interactable=soul?_skillBlock!=null && _battle.Context.SoulGuideBlockPending:_skillAttack!=null && (borrowed || !SkillActions.IsUsed(_battle.Player,_contributionSkill));
        }
    }
}
