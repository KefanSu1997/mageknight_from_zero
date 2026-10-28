using System;
using System.Collections.Generic;
using System.Linq;
using MageKnight.Adventure.Content;
using MK.Logic.Core;
using MK.Logic.Runtime.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public AdventureDefinition travelRoute;
        private bool _journeyMap;
        private AxialCoord? _journeyMapTarget;
        private MK.Logic.Data.SkillCard _flightSkill;
        private bool _showMapGuards;

        private MapState CreateJourneyMap(MK.Logic.Runtime.EnemyTokenSupply supply)
        {
            var map=new MapState();
            if(_draftOriginalMap || _originalStartHero!=null)
            {
                var source=Resources.Load<TextAsset>("Maps/tutorial_opening");
                if(source==null)throw new InvalidOperationException("缺少已校对的原起始地图定义");
                OriginalMapCatalog.Parse(source.text).Initialize(map,supply);
                return map;
            }
            if(travelRoute==null)return map;
            foreach(var cell in travelRoute.sites.Where(s=>s.revealed))
            {
                map.Placed.Add(new AxialCoord(cell.q,cell.r),new MapTile(TileSet.Countryside,0,Enumerable.Repeat(cell.terrain,6).ToArray()));
                if(cell.fortifiedSite)map.Places.Add(new AxialCoord(cell.q,cell.r),new MK.Logic.Runtime.PlaceState(cell.placeType){Enemy=supply.Draw(MK.Logic.Runtime.EnemyTokenSupply.GuardColor(cell.placeType))});
                else if(cell.dungeon)map.Places.Add(new AxialCoord(cell.q,cell.r),new MK.Logic.Runtime.PlaceState(PlaceType.Dungeon));
                else if(cell.recruitAt==RecruitLocation.Village)map.Places.Add(new AxialCoord(cell.q,cell.r),new MK.Logic.Runtime.PlaceState(PlaceType.Village));
            }
            return map;
        }
        private static string TerrainName(TerrainType terrain)=>terrain switch {
            TerrainType.Plains=>"平原",TerrainType.Forest=>"森林",TerrainType.Hills=>"丘陵",TerrainType.Desert=>"沙漠",
            TerrainType.Swamp=>"沼泽",TerrainType.Wasteland=>"荒原",TerrainType.Mountain=>"高山",TerrainType.Lake=>"湖泊",_=>terrain.ToString()};
        private string TravelTargetLabel()=>_journeyMapTarget.HasValue && _journey.Game.Map.Placed.TryGetValue(_journeyMapTarget.Value,out var tile)
            ? TerrainName(tile.Edges[0])+" ("+_journeyMapTarget.Value.Q+","+_journeyMapTarget.Value.R+")" : "未选择";
        private void OpenJourneyMap(){_journeyMap=true;_journeyHand=false;_journeyTools=false;}

        private void DrawJourneyMap()
        {
            if(_dungeonIntro){DrawDungeonIntro();return;}
            if(_showMapGuards){DrawMapGuards();return;}
            if(_harvestPicker){DrawHarvestPicker();return;}
            if(_journey.Game.Map.OriginalCells.Count>0){DrawOriginalJourneyMap();return;}
            var panel=W.Panel(_root,"JourneyMap",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            var position=_journey.Player.Position;var ctx=_journey.Context;
            W.Text(panel,"Title","地形路线 · 连续旅程",40,25,1800,62,36,W.Gold);
            W.Text(panel,"MapStatus",$"移动力 {ctx.MovementPool} · 当前 ({position.Q},{position.R}) · {TerrainName(ctx.CurrentTerrain)} · "+(_journey.Game.Clock.DayPart==DayPart.Day?"白昼":"黑夜"),40,104,1800,54,26,W.Cream);
            W.Text(panel,"MapScope","自定地形布局 · 原版地形费用；点选目标后确认移动。",40,171,1800,50,21,W.Muted);
            foreach(var site in travelRoute.sites)
            {
                var coord=new AxialCoord(site.q,site.r);bool visible=_journey.Game.Map.Placed.ContainsKey(coord);
                bool here=position.Equals(coord);
                var button=Button(panel,"Btn_Hex_"+site.id,"",530+(site.q+site.r*.5f)*233,468+site.r*183,220,162,()=>_journeyMapTarget=coord,here);
                if(site.location?.background!=null)
                {
                    var art=W.Sprite(button.transform,"LocationArt",site.location.background,7,7,206,102);
                    art.color=new Color(1,1,1,visible?.65f:.15f);
                }
                string place=site.dungeon?"地下城":site.fortifiedSite?(site.placeType==PlaceType.MageTower?"法师塔":"要塞"):site.recruitAt==RecruitLocation.Village?"村庄":TerrainName(site.terrain);
                if(site.fortifiedSite && _journey.Game.Map.Places.TryGetValue(coord,out var actual))place+=actual.Conquered?" · 已征服":" · 敌对";
                W.Text(button.transform,"Terrain",here?"你在这里 · "+place:visible?place:"未探索",8,110,204,43,21,W.Cream,TextAlignmentOptions.Center);
            }
            W.Text(panel,"Selected","目的地："+TravelTargetLabel(),1320,290,550,58,28,W.Gold);
            int required=_journeyMapTarget.HasValue?_journey.MovementCost(_journeyMapTarget.Value):int.MaxValue;
            bool lakeFee=ctx.RequireBlueForLake && _journeyMapTarget.HasValue
                && _journey.Game.Map.Placed.TryGetValue(_journeyMapTarget.Value,out var selectedTile)
                && selectedTile.Edges[0]==TerrainType.Lake;
            string costText=required==int.MaxValue?"当前未揭示或不可通行。":$"进入费用：移动力 {required}";
            if(lakeFee)costText+=$"\n另付 1 蓝色魔力\n蓝标记 {_journey.Player.Mana.Tokens.GetValueOrDefault(ManaColor.Blue)} / 蓝晶体 {_journey.Player.Mana.Crystals.GetValueOrDefault(ManaColor.Blue)}";
            else costText+=$"\n剩余移动力：{ctx.MovementPool}";
            if(ctx.TemporalPortalPending)costText=$"时光传送：至多 {ctx.TeleportRange} 格\n已揭示安全格 · 不消耗移动\n本回合行动已使用";
            else if(ctx.SpaceBendingActive)costText+="\n空间扭曲：可跨两格，仍付目的地费用";
            if(_flightSkill!=null)
            {
                int distance=_journeyMapTarget.HasValue?Math.Max(Math.Abs(position.Q-_journeyMapTarget.Value.Q),Math.Max(Math.Abs(position.R-_journeyMapTarget.Value.R),Math.Abs(position.Q+position.R-_journeyMapTarget.Value.Q-_journeyMapTarget.Value.R))):0;
                costText=(IsHeadbandInput(_flightSkill)?"头环借飞行 · 仅一次":"飞行 · 每轮一次")+$"\n安全落点距离 {distance} · "+(distance==1?"移动力 0":distance==2?"移动力 2":"请选择一格或两格内落点")+$"\n剩余移动力：{ctx.MovementPool}";
            }
            W.Text(panel,"Cost",costText,1320,370,550,132,24,W.Cream);
            Button(panel,"Btn_Move",_flightSkill!=null?"确认飞行":ctx.TemporalPortalPending?"确认安全传送":"确认移动",1320,520,550,70,()=>
            {
                if(!_journeyMapTarget.HasValue)throw new InvalidOperationException("请先选择地图目标");
                if(_flightSkill!=null)
                {
                    if(IsHeadbandInput(_flightSkill)){_journey.UseHeadbandFlight(_headbandPhysical,_headbandSkill,_journeyMapTarget.Value);AfterHeadbandInput();}
                    else {_journey.Fly(_flightSkill,_journeyMapTarget.Value);_flightSkill=null;}
                }
                else if(ctx.TemporalPortalPending)_journey.TemporalPortal(_journeyMapTarget.Value);
                else _journey.Move(_journeyMapTarget.Value,_harvestMaterial,_harvestMaterial?.Type==MK.Logic.Core.CardType.Artifact?_harvestColor:null);
                _harvestMaterial=null;
            },true);
            if(_flightSkill!=null)
                Button(panel,"Btn_CancelFlight","取消选择飞行",1320,700,550,55,()=>{if(IsHeadbandInput(_flightSkill))ClearHeadbandInput();else _flightSkill=null;});
            else if(ctx.TemporalPortalPending)
                Button(panel,"Btn_SkipPortal","不传送，保留下次抽牌奖励",1320,700,550,55,()=>_journey.TemporalPortal(null));
            else if(ctx.HarvestTriggers!=0)
                Button(panel,"Btn_Harvest","随本次移动收获："+(_harvestMaterial==null?"不弃牌":CardName(_harvestMaterial)),1320,700,550,55,()=>{_harvestPicker=true;_harvestPage=0;});
            Button(panel,"Btn_TravelHand","打出原移动卡",1320,617,550,70,()=>{_journeyMap=false;if(_journeyCard==null)OpenJourneyHand();else _journeyHand=true;});
            int row=0;
            foreach(var played in ctx.SteadyTempoPlayed)
            {
                var card=played.card;bool selected=ctx.SteadyTempoReturns.Contains(card);
                Button(panel,"Btn_SteadyReturn_"+row,(selected?"已选择 · ":"")+"回合末回收这张稳步前进",1320,778+row*58,550,51,()=>_journey.SelectSteadyReturn(card,!selected));row++;
            }
            if(_journeyMapTarget.HasValue && _journey.Game.Map.Places.TryGetValue(_journeyMapTarget.Value,out var guardPlace) && guardPlace.Defenders.Count>0)
            {
                bool revealed=_journey.Game.Map.RevealedSites.Contains(guardPlace);
                Button(panel,"Btn_MapGuards",revealed?"查看已公开守军":"守军尚未公开",1320,853,550,55,()=>{if(!revealed)throw new InvalidOperationException("此地守军尚未公开");_showMapGuards=true;});
            }
            if(_journey.Game.Map.Places.TryGetValue(position,out var currentSite) && currentSite.Type==PlaceType.Dungeon)
                Button(panel,"Btn_Dungeon","当前地下城 · 查看原规则与进入",40,842,1180,64,()=>_dungeonIntro=true,true);
            W.Text(panel,"Feedback",_feedback??"移动后仍可继续行动；开始战斗或交涉后，不能再进行普通移动。",40,925,1810,58,23,W.Cream);
            Button(panel,"Btn_CloseMap",_journeyCard==null?"返回当前回合":"返回已选原手牌",40,1000,1830,56,()=>{_journeyMap=false;if(IsHeadbandInput(_flightSkill))ClearHeadbandInput();else _flightSkill=null;if(_journeyCard!=null)_journeyHand=true;},true);
        }
        private void DrawMapGuards()
        {
            var panel=W.Panel(_root,"MapGuards",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            if(!_journeyMapTarget.HasValue || !_journey.Game.Map.Places.TryGetValue(_journeyMapTarget.Value,out var place) || !_journey.Game.Map.RevealedSites.Contains(place))
                throw new InvalidOperationException("此地守军尚未公开");
            W.Text(panel,"Title","已公开守军 · "+TravelTargetLabel(),50,35,1800,70,36,W.Gold);
            int i=0;
            foreach(var enemy in place.Defenders)
            {
                var art=worldEnemyCatalog.Single(v=>v.originalId==enemy.Id);
                Texture(panel,"OriginalGuard_"+i,art.originalToken,100+i*420,170,320,320);
                W.Text(panel,"Guard_"+i,art.displayName+$"\n护甲 {enemy.Armor} · 攻击 {enemy.Attack}",85+i*420,520,350,100,26,W.Cream);i++;
            }
            W.Text(panel,"Scope","来自该地点的实际守军；查看不会抽换敌人、移动英雄或开始战斗。",50,850,1800,70,25,W.Muted);
            Button(panel,"Btn_CloseGuards","返回地图",50,990,1820,60,()=>_showMapGuards=false,true);
        }
    }
}
