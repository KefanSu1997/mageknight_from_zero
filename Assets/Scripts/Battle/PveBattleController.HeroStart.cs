using System;
using System.Linq;
using MK.Logic.Data;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private string _draftHeroId="Tovak";
        private OriginalHero _originalStartHero;
        private OriginalHero _configuredStartHero;
        private void BeginConfiguredHero()
        {
            var hero=OriginalHeroes.Get(_draftHeroId);
            _loadout=_draftLoadout;
            var player=CreateConfiguredPlayer(Array.Empty<BattleUnitDefinition>());
            player.HeroId=hero.Id;player.Name=hero.Name;player.Fame=0;player.Skills.Clear();
            _originalStartHero=null;_configuredStartHero=hero;_activeUnits=Array.Empty<BattleUnitDefinition>();
            StartJourney(player);
        }
        private void BeginBattleHero()
        {
            BeginFullDeckHero(false);
        }
        private void BeginOriginalHero()
        {
            BeginFullDeckHero(true);
        }
        private void BeginFullDeckHero(bool originalMap)
        {
            var hero=OriginalHeroes.Get(_draftHeroId);
            var player=CreateOriginalHeroPlayer(hero);
            _originalStartHero=originalMap?hero:null;_configuredStartHero=originalMap?null:hero;
            _draftOriginalMap=originalMap;
            _activeUnits=Array.Empty<BattleUnitDefinition>();
            StartJourney(player);
        }
        private MK.Logic.Runtime.PlayerState CreateOriginalHeroPlayer(OriginalHero hero, System.Random shuffle=null)
        {
            foreach(string id in hero.StartingCards.Distinct())
                (deedCatalog.SingleOrDefault(d=>d.id==id)??throw new InvalidOperationException("缺少起始原卡素材："+id)).Validate();
            return hero.CreatePlayer(OriginalDeeds(),shuffle);
        }
        private void DrawHeroStart(Transform shade)
        {
            var panel=W.Panel(shade,"HeroStart",40,35,1840,1010,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","选择原版英雄 · 标准起始牌库",30,20,1770,60,34,W.Gold);
            W.Text(panel,"Note","16张原牌洗混后抽5张；等级1、护甲2、指挥槽1，魔晶/部队/已学技能均为0。",30,93,1770,56,23,W.Cream);
            for(int i=0;i<OriginalHeroes.All.Count;i++)
            {
                var hero=OriginalHeroes.All[i];
                var b=Button(panel,"Btn_Hero_"+hero.Id,"",30+i%2*443,175+i/2*171,425,154,()=>_draftHeroId=hero.Id,hero.Id==_draftHeroId);
                var first=deedCatalog.Single(d=>d.id==hero.UniqueCards[0]);
                W.Sprite(b.transform,"OriginalHeroCard",first.artwork,8,8,94,135);
                W.Text(b.transform,"Name",hero.Name,115,18,295,45,24,W.Cream);
                W.Text(b.transform,"Unique",string.Join(" / ",hero.UniqueCards.Select(id=>deedCatalog.Single(d=>d.id==id).source.NameCn)),115,75,295,65,20,W.Cream);
            }
            var selected=OriginalHeroes.Get(_draftHeroId);
            W.Text(panel,"SelectedHero",selected.Name+" · 原专属牌",970,175,810,45,29,W.Gold);
            for(int i=0;i<selected.UniqueCards.Count;i++)
                W.Sprite(panel,"UniqueCard_"+i,deedCatalog.Single(d=>d.id==selected.UniqueCards[i]).artwork,986+i*385,240,244,350);
            var rows=selected.StartingCards.GroupBy(id=>id).Select(g=>deedCatalog.Single(d=>d.id==g.Key).source.NameCn+" ×"+g.Count()).ToArray();
            W.Text(panel,"DeckComposition",string.Join("\n",Enumerable.Range(0,(rows.Length+1)/2).Select(i=>string.Join("    ",rows.Skip(i*2).Take(2)))),970,613,820,245,21,W.Cream);
            W.Text(panel,"Scope","下方两种玩法均使用完整16张原牌。地图标准开局后续探索尚未接入。",30,872,1160,44,21,W.Muted);
            Button(panel,"Btn_ConfiguredHero","高级演练 · 已选小牌库",1210,872,570,44,BeginConfiguredHero);
            Button(panel,"Btn_Back","返回演练配置",30,936,390,54,()=>_campPicker=0);
            Button(panel,"Btn_BattleHero","完整16张牌 · 战斗试玩",450,936,650,54,BeginBattleHero);
            Button(panel,"Btn_StartHero","原版16张牌标准开局",1130,936,650,54,BeginOriginalHero,true);
        }
    }
}
