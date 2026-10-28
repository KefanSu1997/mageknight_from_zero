using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _draftOriginalMap;
        private Texture2D _originalMapSource;
        private void DrawOriginalJourneyOverview()
        {
            var map=_journey.Game.Map;var position=_journey.Player.Position;
            map.OriginalCells.TryGetValue(position,out var current);
            string location=current==null?"当前位置":TerrainName(current.Terrain.Value)+" · "+OriginalSiteName(current.Site);
            W.Text(_root,"OriginalPosition","当前位置："+location+$" ({position.Q},{position.R})",42,290,1800,50,30,W.Gold);
            W.Text(_root,"OriginalActions","点选当前位置或相邻格查看费用与地点行动；打出原卡取得移动力后确认移动。",42,354,1800,70,24,W.Cream);
            var cells=map.OriginalCells.Values.Where(c=>!c.IsSea && (c.Position.Equals(position) || MK.Logic.Runtime.Map.AxialCoord.NeighborDirs.Any(d=>(position+d).Equals(c.Position)))).ToArray();
            for(int i=0;i<cells.Length;i++)
            {
                var cell=cells[i];bool here=cell.Position.Equals(position);
                bool enemy=map.Places.TryGetValue(cell.Position,out var site) && site.Enemy!=null;
                string label=(here?"当前 · ":"")+TerrainName(cell.Terrain.Value)+" "+OriginalSiteName(cell.Site)+$" ({cell.Position.Q},{cell.Position.R})"+(enemy?" · 有敌人":"");
                Button(_root,"Btn_Nearby_"+cell.Position.Q+"_"+cell.Position.R,label,42+i%3*614,450+i/3*88,582,70,()=>{OpenJourneyMap();_journeyMapTarget=cell.Position;},here);
            }
            W.Text(_root,"OriginalProgress","原地图上的敌人、资源与位置持续保留。地点规则在选格后展开。",42,727,1800,40,23,W.Muted);
        }
        private static string OriginalSiteName(string site)=>site switch {
            "Portal"=>"传送门","Village"=>"村庄","MagicalGlade"=>"魔法林地",
            "CrystalMine"=>"绿晶矿","RampagingOrcs"=>"兽人劫掠队",_=>""};

        private void DrawOriginalJourneyMap()
        {
            var map=_journey.Game.Map;var ctx=_journey.Context;var player=_journey.Player;
            var panel=W.Panel(_root,"JourneyMap",0,0,1920,1080,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","原起始地图 · A / 1 / 2",40,25,1800,62,36,W.Gold);
            W.Text(panel,"MapStatus",$"移动力 {ctx.MovementPool} · 当前 ({player.Position.Q},{player.Position.R}) · "+(ctx.DayPart==DayPart.Day?"白昼":"黑夜"),40,102,1800,50,26,W.Cream);
            W.Text(panel,"Scope","原入门规则起始三板块 · 全部开局揭示 · 后续探索板块尚未接入",40,160,1800,43,22,W.Muted);
            if(_originalMapSource==null)_originalMapSource=Resources.Load<Texture2D>("Maps/TutorialOpeningSource");
            if(_originalMapSource==null)throw new InvalidOperationException("缺少原规则地图图示");
            var art=new GameObject("OriginalMapFigure",typeof(RectTransform),typeof(RawImage));
            art.transform.SetParent(panel,false);var rect=art.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(40,-205);rect.sizeDelta=new Vector2(902,783);
            var image=art.GetComponent<RawImage>();image.texture=_originalMapSource;
            image.uvRect=new Rect(0,0,902f/2506f,783f/3261f);image.raycastTarget=false;
            foreach(var pair in map.OriginalCells)
            {
                var at=pair.Key;var cell=pair.Value;
                float x=40+365+(at.Q+at.R*.5f)*112,y=205+461+at.R*94;
                bool here=at.Equals(player.Position),selected=_journeyMapTarget.HasValue && at.Equals(_journeyMapTarget.Value);
                var b=Button(panel,"Btn_Cell_"+at.Q+"_"+at.R,here?"你":selected?"●":"",x-35,y-35,70,70,()=>_journeyMapTarget=at,here);
                b.image.color=here?new Color(.05f,.3f,.8f,.85f):selected?new Color(.7f,.5f,.1f,.5f):new Color(0,0,0,.03f);
                if(map.Places.TryGetValue(at,out var place) && place.Enemy!=null)
                {
                    var binding=worldEnemyCatalog.Single(v=>v.originalId==place.Enemy.Id);
                    Texture(b.transform,"ActualEnemy",binding.originalToken,13,13,44,44);
                }
            }
            OriginalMapCell target=null;
            if(_journeyMapTarget.HasValue)map.OriginalCells.TryGetValue(_journeyMapTarget.Value,out target);
            string title=target==null?"点选地图中的实际格":$"板块 {target.TileId} · ({target.Position.Q},{target.Position.R})\n"+(target.IsSea?"海洋 · 不可占据":TerrainName(target.Terrain.Value)+" · "+OriginalSiteName(target.Site));
            W.Text(panel,"Selected",title,1010,230,860,110,29,W.Gold);
            int cost=target==null?int.MaxValue:_journey.MovementCost(target.Position);
            string note=target==null?"选择目的地后确认移动。":target.IsSea?"海洋不可进入；穿湖与飞行也不能在此落地。":cost==int.MaxValue?"当前地形不可通行。":"进入费用：移动力 "+cost;
            if(_flightSkill!=null)note="飞行：相邻安全格免费，两格安全落点支付2移动。";
            else if(ctx.TemporalPortalPending)note="时光传送：选择原范围内已揭示安全格。";
            if(target!=null && map.Places.TryGetValue(target.Position,out var selectedPlace) && selectedPlace.Enemy!=null)
                note+="\n实际敌人尚在：不能走入；可从相邻格挑战。";
            if(target?.Site=="MagicalGlade")note+="\n回合开始：昼金 / 夜黑；回合末：手牌或弃牌去1伤。";
            if(target?.Site=="CrystalMine")note+="\n回合末获得绿晶1；同色已有3颗时不再取得。";
            W.Text(panel,"Cost",note,1010,357,860,120,25,W.Cream);
            Button(panel,"Btn_Move",_flightSkill!=null?"确认飞行":ctx.TemporalPortalPending?"确认传送":"确认移动",1010,498,860,63,()=>
            {
                if(target==null)throw new InvalidOperationException("请先选择实际地图目标");
                if(_flightSkill!=null){if(IsHeadbandInput(_flightSkill)){_journey.UseHeadbandFlight(_headbandPhysical,_headbandSkill,target.Position);AfterHeadbandInput();}else{_journey.Fly(_flightSkill,target.Position);_flightSkill=null;}}
                else if(ctx.TemporalPortalPending)_journey.TemporalPortal(target.Position);
                else _journey.Move(target.Position,_harvestMaterial,_harvestMaterial?.Type==CardType.Artifact?_harvestColor:null);
                _harvestMaterial=null;AdoptWorldBattle();
            },true);
            Button(panel,"Btn_TravelHand","打出原移动卡",1010,578,860,60,()=>{_journeyMap=false;if(_journeyCard==null)OpenJourneyHand();else _journeyHand=true;});
            if(target!=null && target.Position.Equals(player.Position) && _journey.AtVillage)
                Button(panel,"Btn_CurrentVillage","在当前村庄交涉 / 查看部队供应",1010,657,860,56,()=>{_journeyMap=false;_journeyInteraction=true;},true);
            if(target!=null && target.Position.Equals(player.Position) && !_journey.AtVillage && _journey.CanInteractAtCurrentSite)
                Button(panel,"Btn_CurrentSiteInteraction","在当前"+RecruitmentSiteName(_journey.CurrentSiteRecruitLocation)+"交涉 / 部队供应",1010,657,860,56,()=>{_journeyMap=false;_journeyInteraction=true;},true);
            if(target!=null && target.Position.Equals(player.Position) && _journey.AtMagicalGlade)
                Button(panel,"Btn_CurrentGlade","在当前林地招募魔宠 / 查看部队供应",1010,657,860,56,()=>{_journeyMap=false;_journeyInteraction=true;},true);
            if(target!=null && map.Places.TryGetValue(target.Position,out var enemySite) && enemySite.Enemy!=null)
            {
                Button(panel,"Btn_MapGuards","查看实际公开敌人",1010,657,415,56,()=>_showMapGuards=true);
                Button(panel,"Btn_Challenge","从相邻格挑战",1446,657,424,56,()=>{_journey.ChallengeRampaging(target.Position);AdoptWorldBattle();},true);
            }
            if(_flightSkill!=null)Button(panel,"Btn_CancelFlight","取消飞行选择",1010,735,860,52,()=>{if(IsHeadbandInput(_flightSkill))ClearHeadbandInput();else _flightSkill=null;});
            else if(ctx.TemporalPortalPending)Button(panel,"Btn_SkipPortal","不传送，保留下次抽牌奖励",1010,735,860,52,()=>_journey.TemporalPortal(null));
            else if(ctx.HarvestTriggers!=0)Button(panel,"Btn_Harvest","随移动收获："+(_harvestMaterial==null?"不弃牌":CardName(_harvestMaterial)),1010,735,860,52,()=>{_harvestPicker=true;_harvestPage=0;});
            int row=0;
            foreach(var item in ctx.SteadyTempoPlayed)
            {
                var card=item.card;bool chosen=ctx.SteadyTempoReturns.Contains(card);
                Button(panel,"Btn_SteadyReturn_"+row,(chosen?"已选 · ":"")+"回合末回收稳步前进",1010,803+row*50,860,45,()=>_journey.SelectSteadyReturn(card,!chosen));row++;
            }
            W.Text(panel,"Feedback",_feedback??"移动和挑战沿用同一玩家、实际敌标记与原牌资源；战果保留在地图上。",1010,900,860,80,22,W.Cream);
            Button(panel,"Btn_CloseMap",_journeyCard==null?"返回当前回合":"返回已选原卡",40,1000,1830,56,()=>{_journeyMap=false;if(IsHeadbandInput(_flightSkill))ClearHeadbandInput();else _flightSkill=null;if(_journeyCard!=null)_journeyHand=true;},true);
        }
    }
}
