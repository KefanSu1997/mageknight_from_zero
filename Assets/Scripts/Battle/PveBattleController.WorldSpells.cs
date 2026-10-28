using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public OriginalEnemyView[] worldEnemyCatalog=Array.Empty<OriginalEnemyView>();
        private BattleEncounter _siteEncounter;
        private bool _siteRewardOpen;
        private void ClearSiteEncounter(){if(_siteEncounter!=null)Destroy(_siteEncounter);_siteEncounter=null;_siteRewardOpen=false;}
        private EnemyTokenSupply CreateWorldSupply()
        {
            var original=MonsterJsonLoader.ParseMonsters(encounters[0].originalMonsterJson.text);
            return new EnemyTokenSupply(worldEnemyCatalog.Select(v=>original.Single(m=>m.Id==v.originalId) with{}),
                Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null);
        }
        private void AdoptWorldBattle()
        {
            if(_journey?.Battle==null || ReferenceEquals(_journey.Battle,_battle))return;
            if(!_journey.Context.TunnelAssaultActive && _journey.Context.Dungeon==null && _journey.Context.RampagingBattle==null)return;
            _battle=_journey.Battle;ClearSiteEncounter();
            _siteEncounter=ScriptableObject.CreateInstance<BattleEncounter>();
            _siteEncounter.background=encounters[_encounter].background;
            _siteEncounter.originalMonsterJson=encounters[0].originalMonsterJson;
            _siteEncounter.enemies=_battle.Enemies.Select(e=>worldEnemyCatalog.Single(v=>v.originalId==e.Id)).ToArray();
            _siteEncounter.displayName=_journey.Context.RampagingBattle!=null?"地图肆虐敌人 · 实际标记":_journey.Context.Dungeon!=null?"地下城 · 原夜战与部队限制":(_journey.Context.TunnelTargetSite.Type==PlaceType.MageTower?"法师塔":"要塞")+" · 地道突袭";
            _siteEncounter.dayPart=_journey.Context.DayPart;
            _journeyHand=false;_journeyTools=false;_journeyMap=false;_targets.Clear();
        }
        private void FinishWorldSite()
        {
            var ctx=_journey.Context;bool withdraw=ctx.TunnelDefenders.Any(m=>!ctx.IsTunnelDefenderDefeated(m));
            _journey.FinishTunnelAssault(withdraw);_siteRewardOpen=ctx.PendingSpellReward;
            _feedback=withdraw?"守军仍在，已按原规则撤回地道起点。":"已征服实际地点；守军弃置，保留当前玩家与全部资源。";
        }
        private void DrawSiteReward()
        {
            if(_journey.Context.Dungeon?.Reward==DungeonRewardKind.Artifact){DrawDungeonArtifactReward();return;}
            var panel=W.Panel(_root,"SiteReward",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","地点 · 选择原法术奖励",40,30,1800,65,36,W.Gold);
            W.Text(panel,"Rule","选定后在正式回合结束时取得到牌库顶，再按手牌上限补牌；供应立即补位。",40,115,1800,90,26,W.Cream);
            var offer=_journey.Game.Globals.Spells.Offer;
            for(int i=0;i<offer.Count;i++)
            {
                var spell=offer[i];var binding=Binding(new DeedCard(spell.Id,CardType.Spell));
                var b=Button(panel,"Btn_Reward_"+i,"",170+i*550,255,480,640,()=>{_journey.SelectSiteSpellReward(spell);_siteRewardOpen=false;_feedback="已选 "+binding.source.NameCn+"，结束本回合后领取。";});
                W.Sprite(b.transform,"OriginalReward",binding.artwork,84,25,312,450);
                W.Text(b.transform,"Name",binding.source.NameCn,35,520,410,65,29,W.Cream);
            }
        }
        private void DrawWorldSpellInputs(Transform panel,string id,bool strong)
        {
            var ctx=_journey?.Context??_battle.Context;
            if(id=="magic_023"&&strong)
            {
                W.Text(panel,"TargetCost","已选 "+_inputEnemyIndices.Count+" 个敌人；额外移动费用 "+(_inputEnemyIndices.Count*(_inputEnemyIndices.Count-1)/2)+"；当前移动 "+ctx.MovementPool,40,430,1800,60,25,W.Gold);
                for(int i=0;i<ctx.Enemies.Count;i++)
                {
                    int index=i;var enemy=ctx.Enemies[i];var view=EnemyView(enemy.Id);
                    var b=Button(panel,"Btn_WingsEnemy_"+i,"",40+i*440,520,420,280,()=>{if(!_inputEnemyIndices.Add(index))_inputEnemyIndices.Remove(index);});
                    b.interactable=!ctx.DefeatedEnemyIndices.Contains(i);
                    Texture(b.transform,"OriginalTarget",view.originalToken,115,20,190,190);
                    W.Text(b.transform,"TargetName",(_inputEnemyIndices.Contains(i)?"● ":"")+view.displayName,30,222,360,46,24,W.Cream);
                }
                return;
            }
            if(_journey==null || travelRoute==null){W.Text(panel,"NoMap","请在实际旅程地图中选择路径。",40,440,1800,60,25,W.Gold);return;}
            var from=_inputPath.Count==0?_journey.Player.Position:_inputPath.Last();
            W.Text(panel,"PathTitle","逐格点选已揭示路径 · 下一格出发点 ("+from.Q+","+from.R+")",40,434,1070,40,24,W.Gold);
            foreach(var site in travelRoute.sites)
            {
                var coord=new AxialCoord(site.q,site.r);bool visible=_journey.Game.Map.Placed.ContainsKey(coord);
                int dq=coord.Q-from.Q,dr=coord.R-from.R;bool adjacent=(Math.Abs(dq)+Math.Abs(dr)+Math.Abs(dq+dr))/2==1;
                string name=site.fortifiedSite?(site.placeType==PlaceType.MageTower?"法师塔":"要塞"):TerrainName(site.terrain);
                var b=Button(panel,"Btn_Path_"+site.id,name+"\n("+coord.Q+","+coord.R+")",450+(site.q+site.r*.5f)*180,600+site.r*120,169,104,()=>_inputPath.Add(coord));
                b.interactable=visible&&adjacent&&_inputPath.Count<(id=="magic_004"?3:5);
            }
            W.Text(panel,"PathSummary","实际路径：\n"+string.Join(" → ",_inputPath.Select(c=>"("+c.Q+","+c.R+")"))+"\n\n"+
                (id=="magic_004"?"至多三格，不花移动力；不能经过湖泊或沼泽。":"1–5格，每格另付1移动力。")+"\n"+(strong?"终点须为尚未征服的实际城防地点。":"终点须安全。"),1140,400,730,270,24,W.Cream);
            Button(panel,"Btn_PathBack","撤回最后一格",1140,710,720,60,()=>_inputPath.RemoveAt(_inputPath.Count-1)).interactable=_inputPath.Count>0;
            Button(panel,"Btn_PathClear","清空路径",1140,783,720,55,()=>_inputPath.Clear());
        }
    }
}
