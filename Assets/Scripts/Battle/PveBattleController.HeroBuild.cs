using System;
using System.Collections.Generic;
using System.Linq;
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
        private PlayerState _cooperativeFirst;
        private SkillCard[] _cooperativeFirstSupplySkills;
        private int _buildFame=15;
        private bool _buildOriginalDeck=true;
        private void OpenHeroBuild(bool originalDeck)
        {
            // A new setup must not inherit a previous rehearsal's deck mode or companion.
            _buildOriginalDeck=originalDeck;
            _buildSkills.Clear();_cooperativeFirst=null;_cooperativeFirstSupplySkills=null;
            _campPicker=6;
        }
        private readonly HashSet<int> _buildSkills=new();
        private SkillCard[] BuildSkillPool()=>CardJsonLoader.ParseHeroSkills(
            loadouts.Select(l=>l.hero?.originalSkillsJson).First(j=>j!=null).text,
            OriginalHeroes.Get(_draftHeroId).SkillPoolId).ToArray();
        private void BeginHeroBuild() => CompleteHeroBuild(false);
        private void CompleteHeroBuild(bool addCompanion)
        {
            var hero=OriginalHeroes.Get(_draftHeroId);var pool=BuildSkillPool();
            if (_cooperativeFirst?.HeroId == hero.Id) throw new InvalidOperationException("同一局请选择不同原英雄");
            var level=new PlayerState{Fame=_buildFame}.Level;
            if(_buildSkills.Count>level/2 || _buildSkills.Any(i=>i<0 || i>=pool.Length))
                throw new InvalidOperationException("已学技能必须来自所选英雄且数量符合原等级");
            _loadout=_draftLoadout;
            var player=CreateConfiguredPlayer(Array.Empty<BattleUnitDefinition>(), _cooperativeFirst == null ? 0 : 1, useSelectedCards:!_buildOriginalDeck);
            if(_buildOriginalDeck)
            {
                player.Deck.SetDeck(CreateOriginalHeroPlayer(hero,Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null).Deck.DrawPile);
            }
            player.Name=hero.Name;player.HeroId=hero.Id;player.Fame=_buildFame;player.Skills.Clear();
            player.Armor=2+(level+1)/4;
            var skillRandom=Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null;
            foreach(int index in _buildSkills.OrderBy(i=>i).Where(i=>pool[i].SourceId!=LoyaltyActions.SourceId))HeroSkillAcquisition.Acquire(player,pool[index],skillRandom);
            _activeUnits=_draftUnits.ToArray();
            foreach(var unit in _activeUnits)player.Units.Add(new UnitState(unit.Snapshot()));
            if(player.FreeSlots<0)throw new InvalidOperationException("实际部队数超过英雄等级指挥槽");
            var acquired=_buildSkills.Select(i=>pool[i]).Where(s=>s.SourceId==LoyaltyActions.SourceId).ToArray();
            if (addCompanion)
            {
                if (_cooperativeFirst != null) throw new InvalidOperationException("当前界面支持两位英雄合作");
                _cooperativeFirst=player;_cooperativeFirstSupplySkills=acquired;
                _draftHeroId=OriginalHeroes.All.First(h=>h.Id!=hero.Id).Id;_buildSkills.Clear();
                return;
            }
            _originalStartHero=null;_configuredStartHero=hero;
            if (_cooperativeFirst == null) StartJourney(player,acquired);
            else
            {
                var first=_cooperativeFirst;
                var unique=deedCatalog.Where(d=>d.source.Set!=MK.Logic.Core.CardSet.BasicAction).Select(d=>d.id).ToHashSet();
                var cards=new[]{first,player}.SelectMany(p=>p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile)).ToArray();
                if (cards.Where(c=>unique.Contains(c.Id)).GroupBy(c=>c.Id).Any(g=>g.Count()>1))
                    throw new InvalidOperationException("两位英雄不能重复持有同一张唯一高级行动、法术或神器；请调整第二位构筑");
                StartJourney(first,_cooperativeFirstSupplySkills,player,acquired);
                _cooperativeFirst=null;_cooperativeFirstSupplySkills=null;
            }
        }
        private void DrawHeroBuild(Transform shade)
        {
            var panel=W.Panel(shade,"HeroBuild",40,35,1840,1010,new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget=true;
            var hero=OriginalHeroes.Get(_draftHeroId);var player=new PlayerState{Fame=_buildFame};
            W.Text(panel,"Title","原英雄构筑 · "+hero.Name+(_cooperativeFirst==null?"":" · 第一位："+_cooperativeFirst.Name),30,20,1770,55,34,W.Gold);
            W.Text(panel,"Scope",_buildOriginalDeck?"本英雄完整16张原牌洗混；等级、资源、部队和技能为自定起点。开始后使用正式回合和升级。":"高级小牌库演练：仅使用已选原卡，可能无牌可抽；资源、部队和技能为自定起点。",30,85,1770,55,23,W.Cream);
            for(int i=0;i<OriginalHeroes.All.Count;i++)
            {
                var h=OriginalHeroes.All[i];
                Button(panel,"Btn_Hero_"+h.Id,h.Name,30+i*253,153,243,55,()=>{_draftHeroId=h.Id;_buildSkills.Clear();},h.Id==hero.Id);
            }
            int[] fame={3,15,35,63,99};
            for(int i=0;i<fame.Length;i++)
            {
                int f=fame[i];int level=new PlayerState{Fame=f}.Level;
                Button(panel,"Btn_Level_"+level,"等级"+level+" · 名望"+f,30+i*353,229,340,55,()=>{_buildFame=f;_buildSkills.Clear();},f==_buildFame);
            }
            W.Text(panel,"Limit","已学 "+_buildSkills.Count+" / "+player.Level/2+" · 指挥槽 "+player.CommandSlots+" · 技能按原英雄归属选择",30,305,1770,45,25,W.Gold);
            var pool=BuildSkillPool();
            for(int i=0;i<pool.Length;i++)
            {
                int index=i;var skill=pool[i];bool selected=_buildSkills.Contains(i);
                var b=Button(panel,"Btn_Skill_"+i,(selected?"● ":"")+skill.Id+"\n"+skill.PrintedRule,30+i%2*893,370+i/2*100,873,91,()=>
                {if(!_buildSkills.Remove(index))_buildSkills.Add(index);},selected);
                b.interactable=selected || _buildSkills.Count<player.Level/2;
                var t=b.GetComponentInChildren<TextMeshProUGUI>();t.enableAutoSizing=true;t.fontSizeMin=14;t.fontSizeMax=21;
            }
            Button(panel,"Btn_DeckSource",_buildOriginalDeck?"牌库：本英雄完整16张（已选）":"使用本英雄完整16张原牌",30,877,850,40,()=>_buildOriginalDeck=true);
            Button(panel,"Btn_AddCompanion",_cooperativeFirst==null?"保存这位英雄，再配置第二位":"取消合作，返回单英雄",910,877,870,40,()=>{if(_cooperativeFirst==null)CompleteHeroBuild(true);else{_cooperativeFirst=null;_cooperativeFirstSupplySkills=null;}});
            Button(panel,"Btn_Back","返回配置",30,935,300,57,()=>_campPicker=0);
            Button(panel,"Btn_BuildUnits","配置原部队",350,935,330,57,()=>{_unitPickerFromHeroBuild=true;_campPicker=3;_pickerPage=0;});
            Button(panel,"Btn_Begin",_cooperativeFirst==null?"按此原英雄构筑开始连续旅程":"开始双英雄连续旅程",700,935,1080,57,BeginHeroBuild,true);
        }
    }
}
