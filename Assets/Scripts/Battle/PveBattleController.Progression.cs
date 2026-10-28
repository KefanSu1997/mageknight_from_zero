using System;
using System.Linq;
using MK.Logic.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public static readonly int[] SkillReferenceIndices={0,1,3,4,7,8,10,11,14,15,17,18,21,22};
        public Texture2D[] skillReferencePages=Array.Empty<Texture2D>();
        private SkillCard _upgradeSkill;
        private AdvActionCard _upgradeAction;
        private Texture2D SkillReference(SkillCard skill)
        {
            // The current game uses original ability icons in all skill panels,
            // including progression and special consumers; never a card scan.
            if (embeddedBattleUi) return SkillIcon(skill);
            var parts=skill.SourceId.Split(':');int index=int.Parse(parts[1]);
            int first=parts[0] switch {"hero_skill_pool_000"=>0,"hero_skill_pool_001"=>3,"hero_skill_pool_002"=>7,
                "hero_skill_pool_003"=>10,"hero_skill_pool_004"=>14,"hero_skill_pool_005"=>17,"hero_skill_pool_007"=>21,_=>-1};
            if(first<0)return null;
            int sheet=first+(index>=6?1:0),slot=Array.IndexOf(SkillReferenceIndices,sheet);
            return slot>=0 && slot<skillReferencePages.Length?skillReferencePages[slot]:null;
        }
        private void DrawLevelReward()
        {
            var reward=_journey.Progression;
            var panel=W.Panel(_root,"LevelReward",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",$"{_journey.Player.Name} · 升至等级 {reward.PendingLevel}",40,26,1810,58,35,W.Gold);
            W.Text(panel,"Timing","已清理回合，尚未补牌：选一个技能和一张高级行动，新牌置顶后再补牌。",40,100,1810,49,24,W.Cream);
            var skills=reward.Choices.Concat(reward.CommonSkills.Where(s=>!s.SourceId.StartsWith(OriginalHeroes.Get(_journey.Player.HeroId).SkillPoolId+":"))).ToArray();
            for(int i=0;i<skills.Length;i++)
            {
                var skill=skills[i];bool own=reward.Choices.Any(s=>ReferenceEquals(s,skill));
                var b=Button(panel,"Btn_RewardSkill_"+i,(ReferenceEquals(_upgradeSkill,skill)?"● ":"")+(own?"自身 · ":"公共 · ")+skill.Id,40+i%2*455,177+i/2*77,435,65,()=>
                {
                    _upgradeSkill=skill;
                    if(!own)_upgradeAction=reward.Actions[0];
                });
                var label=b.GetComponentInChildren<TextMeshProUGUI>();label.enableAutoSizing=true;label.fontSizeMin=17;label.fontSizeMax=22;
            }
            if(_upgradeSkill!=null)
            {
                Texture(panel,"OriginalSkillReference",SkillReference(_upgradeSkill),45,375,285,399);
                var rule=W.Text(panel,"SkillRule",_upgradeSkill.Id+"\n"+_upgradeSkill.PrintedRule,365,377,560,353,23,W.Cream);
                rule.enableAutoSizing=true;rule.fontSizeMin=17;rule.fontSizeMax=23;
                W.Text(panel,"SkillOrigin",embeddedBattleUi
                    ? "左侧图标用于识别技能；完整规则、费用与使用时机见右侧说明。"
                    : "左图为此技能所在的原参考页；技能操作仍依原时机与已接入选项。",45,794,880,65,20,W.Muted);
            }
            bool common=_upgradeSkill!=null && !reward.Choices.Any(s=>ReferenceEquals(s,_upgradeSkill));
            for(int i=0;i<reward.Actions.Count;i++)
            {
                var action=reward.Actions[i];var binding=deedCatalog.Single(d=>d.id==action.Id);
                var b=Button(panel,"Btn_RewardAction_"+i,"",980+i*301,177,280,563,()=>_upgradeAction=action,ReferenceEquals(action,_upgradeAction));
                W.Sprite(b.transform,"OriginalArt",binding.artwork,14,18,252,365);
                W.Text(b.transform,"CardName",(i==0?"最底 · ":"")+binding.source.NameCn,12,408,256,75,22,W.Cream);
                W.Text(b.transform,"Select",ReferenceEquals(action,_upgradeAction)?"已选择":"选择高级行动",12,495,256,47,21,W.Cream);
                b.interactable=!common || i==0;
            }
            W.Text(panel,"Rule","选自身技能：任意高级行动，另一枚技能进入公共区。\n选他人的公共技能：只能拿最底高级行动，两枚自身技能都进入公共区。",980,774,890,103,22,W.Cream);
            W.Text(panel,"Feedback",_feedback??"完成选择后自动处理后续升级并补牌，不会重新创建玩家。",40,911,1830,60,22,W.Muted);
            Button(panel,"Btn_ConfirmReward","确认本次升级",40,1000,1830,55,()=>
            {
                var before=CostAcquisitionBefore();
                _journey.ChooseLevelReward(_upgradeSkill,_upgradeAction);RecordCostUiNewAcquisitions(before,true);
                _upgradeSkill=null;_upgradeAction=null;
            },true).interactable=_upgradeSkill!=null && _upgradeAction!=null;
        }
    }
}
