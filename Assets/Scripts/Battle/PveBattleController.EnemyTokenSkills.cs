using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private SkillCard _enemyTokenSkill;
        private readonly List<Monster> _skillEnemies = new();
        private int _tokenSkillMode, _tokenSkillPage;

        private void OpenEnemyTokenSkill(SkillCard skill)
        {
            _enemyTokenSkill=skill; _skillEnemies.Clear(); _tokenSkillPage=0;
            _tokenSkillMode=0;
        }

        private void DrawEnemyTokenSkill()
        {
            bool fork=_enemyTokenSkill.SourceId=="hero_skill_pool_001:05";
            bool borrowed=IsHeadbandInput(_enemyTokenSkill);
            var player=_journey.Player;
            var enemies=fork?_battle.Enemies.Where((e,i)=>!_battle.Context.DefeatedEnemyIndices.Contains(i)).ToArray()
                :(_tokenSkillMode==0?player.PuppetTokens.DefeatedThisTurn:player.PuppetTokens.KeptTokens).ToArray();
            _tokenSkillPage=Math.Min(_tokenSkillPage,Math.Max(0,(enemies.Length-1)/4));
            var panel=W.Panel(_root,"EnemyTokenSkill",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title",_enemyTokenSkill.Id+" · 选择实际敌人标记",40,25,1800,65,36,W.Gold);
            W.Text(panel,"Instruction",borrowed?HeadbandInputInstruction():fork?"选择1至3个不同目标；每份冰火远程1只可用于对应敌人。":"保存本回合击败的标记，或消费以前保存的标记；每回合只能选择其中一项。",40,105,1810,65,24,W.Cream);
            if(!fork)
                for(int i=0;i<3;i++)
                {
                    int mode=i;
                    Button(panel,"Btn_Mode_"+i,new[]{"保存本回合战果","消费库存作攻击","消费库存作格挡"}[i],40+i*310,184,294,52,
                        ()=>{_tokenSkillMode=mode;_skillEnemies.Clear();_tokenSkillPage=0;},_tokenSkillMode==i);
                }
            for(int slot=0;slot<4 && _tokenSkillPage*4+slot<enemies.Length;slot++)
            {
                int index=_tokenSkillPage*4+slot;var enemy=enemies[index];
                var art=worldEnemyCatalog.Concat(Encounter.enemies).FirstOrDefault(e=>e.originalId==enemy.Id);
                var button=Button(panel,"Btn_Enemy_"+index,"",40+slot%2*480,255+slot/2*265,455,245,()=>
                {
                    int selectedIndex=_skillEnemies.FindIndex(e=>ReferenceEquals(e,enemy));
                    if(selectedIndex>=0){_skillEnemies.RemoveAt(selectedIndex);return;}
                    if(!fork)_skillEnemies.Clear();
                    if(fork && _skillEnemies.Count==3)throw new InvalidOperationException("叉形闪电最多选择三个不同目标");
                    _skillEnemies.Add(enemy);
                },_skillEnemies.Any(e=>ReferenceEquals(e,enemy)));
                if(art?.originalToken!=null)Texture(button.transform,"OriginalToken",art.originalToken,15,22,186,186);
                W.Text(button.transform,"Enemy",(art?.displayName??enemy.Id)+"\n护甲 "+enemy.Armor+"\n"+(fork?"绑定冰火远程1":_tokenSkillMode==0?"实际本回合战果":"实际保存的标记"),218,35,218,180,23,W.Cream);
            }
            Texture(panel,"OriginalSkillReference",SkillReference(_enemyTokenSkill),1050,250,270,365);
            W.Text(panel,"OriginalRule",_enemyTokenSkill.PrintedRule,1360,250,490,460,24,W.Cream);
            if(enemies.Length==0)W.Text(panel,"Empty",fork?"当前没有存活敌人。":_tokenSkillMode==0?"本回合没有可保存的实际击败标记。":"尚未保存敌人标记。",40,300,910,100,27,W.Muted);
            Button(panel,"Btn_Previous","上一页",40,805,450,55,()=>_tokenSkillPage--).interactable=_tokenSkillPage>0;
            Button(panel,"Btn_Next","下一页",520,805,450,55,()=>_tokenSkillPage++).interactable=(_tokenSkillPage+1)*4<enemies.Length;
            W.Text(panel,"Feedback",_feedback??"选择标记与用途后确认；失败不会消耗标记或技能次数。",40,894,1810,70,24,W.Cream);
            if(borrowed)DrawHeadbandFinish(panel);
            Button(panel,"Btn_Back","返回当前行动",40,990,880,60,()=>{_enemyTokenSkill=null;if(borrowed)ClearHeadbandInput();}).interactable=!borrowed || _journey.Context.PendingHeadbandBorrow==null;
            Button(panel,"Btn_Confirm","确认原技能与实际目标",960,990,880,60,()=>
            {
                if(borrowed)
                {
                    if(fork)_journey.UseHeadbandForkedLightning(_headbandPhysical,_headbandSkill,_skillEnemies.ToArray());
                    else _journey.UseHeadbandPuppet(_headbandPhysical,_headbandSkill,_skillEnemies.Single(),_tokenSkillMode);
                    AfterHeadbandInput();return;
                }
                if(fork)_battle.UseForkedLightning(_enemyTokenSkill,_skillEnemies.Select(e=>Enumerable.Range(0,_battle.Enemies.Count).Single(i=>ReferenceEquals(_battle.Enemies[i],e))).ToArray());
                else _battle.UsePuppetToken(_enemyTokenSkill,_skillEnemies.Single(),_tokenSkillMode);
                _enemyTokenSkill=null;_selectedSkill=null;
            },true).interactable=_skillEnemies.Count>0 && (borrowed || !SkillActions.IsUsed(player,_enemyTokenSkill))
                && (fork || _tokenSkillMode==0 || _journey.Context.InBattle && (_tokenSkillMode==1 && _journey.Context.CombatPhase==Phase.Melee || _tokenSkillMode==2 && _journey.Context.CombatPhase==Phase.Block));
        }
    }
}
