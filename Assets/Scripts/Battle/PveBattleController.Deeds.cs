using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public BattleDeedDefinition[] deedCatalog=Array.Empty<BattleDeedDefinition>();
        public BattleArtifactDefinition[] artifactCatalog=Array.Empty<BattleArtifactDefinition>();
        private List<BattleDeedDefinition> _draftDeeds;
        private int _deedChoicePage, _deedKind;
        private string _openingSourceIds="";
        private ActionCardData[] OriginalDeeds() => deedCatalog.Where(d=>d.source is ActionCardSO || d.source is SpellCardSO).Select(d=>d.Action()).ToArray();
        private SpellCardData[] OriginalSpells() => deedCatalog.Where(d=>d.source is SpellCardSO).Select(d=>d.Spell()).ToArray();
        private PlayerState CreateConfiguredPlayer(BattleUnitDefinition[] units, int playerId = 0, bool useSelectedCards = true)
        {
            if(useSelectedCards && _draftDeeds!=null && (_draftDeeds.Count<1 || _draftDeeds.Count>7))
                throw new InvalidOperationException("自定构筑起点须选择1至7张原卡，后续牌库与轮次沿用同一批实际牌");
            var player=Loadout.CreatePlayer(units, playerId);
            if(useSelectedCards && _draftDeeds!=null)
            {
                player.Deck.SetDeck(Array.Empty<DeedCard>());
                foreach(var binding in _draftDeeds)
                {
                    binding.Validate();
                    player.Deck.Hand.Add(new DeedCard(binding.id,binding.source is SpellCardSO ? CardType.Spell : binding.source is ItemCardSO ? CardType.Artifact : CardType.Action));
                }
            }
            _openingSourceIds=string.Join(",",player.Deck.Hand.Select(c=>c.Id));
            return player;
        }
        private void OpenDeedPicker()
        {
            _draftDeeds ??= loadouts[_draftLoadout].openingHand.Select(c=>deedCatalog.Single(d=>d.id==c.id))
                .Concat((loadouts[_draftLoadout].artifacts??Array.Empty<BattleArtifactDefinition>()).Select(c=>deedCatalog.Single(d=>d.id==c.source.Id))).ToList();
            _campPicker=4;_pickerPage=0;_deedKind=0;
        }
        private BattleDeedDefinition[] DeedPageCatalog() => deedCatalog.Where(d=>_deedKind switch {
            0=>d.source.Set==CardSet.BasicAction,1=>d.source.Set==CardSet.AdvancedAction,2=>d.source is SpellCardSO,_=>d.source is ItemCardSO}).OrderBy(d=>d.id).ToArray();
        private void DrawDeedPicker(Transform shade)
        {
            var panel=W.Panel(shade,"DeedPicker",70,60,1780,960,W.Ink,true);
            var background=panel.GetComponent<UnityEngine.UI.Image>();
            var color=background.color;color.a=1;background.color=color;
            W.Text(panel,"Title","选择原卡构筑起点 · "+_draftDeeds.Count+" / 7 张",32,20,1710,57,34,W.Gold);
            var names=new[]{"基础行动","高级行动","法术","神器"};
            for(int i=0;i<4;i++)
            {
                int kind=i;Button(panel,"Btn_DeedKind_"+i,(_deedKind==i?"● ":"")+names[i],32+i*432,91,402,54,()=>{_deedKind=kind;_pickerPage=0;});
            }
            var cards=DeedPageCatalog();
            for(int slot=0;slot<12 && _pickerPage*12+slot<cards.Length;slot++)
            {
                var card=cards[_pickerPage*12+slot];float x=32+slot%4*432,y=174+slot/4*176;
                int count=_draftDeeds.Count(d=>d.id==card.id);
                var b=Button(panel,"Btn_ChooseDeed_"+card.id,"",x,y,402,157,()=>
                {
                    int index=_draftDeeds.FindIndex(d=>d.id==card.id);
                    if(index>=0)_draftDeeds.RemoveAt(index);
                    else {if(_draftDeeds.Count>=7)throw new InvalidOperationException("请先移除一张起点手牌");_draftDeeds.Add(card);}
                });
                W.Sprite(b.transform,"OriginalArt",card.artwork,10,11,86,130);
                W.Text(b.transform,"CardName",(count>0 ? "已选 "+count+" · " : "")+card.source.NameCn,112,15,277,73,22,W.Cream);
                bool active=card.source is ItemCardSO ? PveArtifactInputs.Supports(card.id)
                    : BattleCardChoice.For(card.id).Length>0;
                W.Text(b.transform,"EntryStatus",active ? "已有原效果操作分支" : "原图已绑定 · 操作待接",112,105,277,40,17,W.Muted);
            }
            Button(panel,"Btn_DeedPrevious","上一页",32,722,290,57,()=>_pickerPage--).interactable=_pickerPage>0;
            W.Text(panel,"DeedPage",$"{_pickerPage+1} / {(cards.Length+11)/12} 页 · {cards.Length} 张原卡",352,731,720,40,22,W.Cream);
            Button(panel,"Btn_DeedNext","下一页",1430,722,290,57,()=>_pickerPage++).interactable=(_pickerPage+1)*12<cards.Length;
            W.Text(panel,"ChosenDeeds",string.Join(" / ",_draftDeeds.Select(d=>d.source.NameCn)),32,806,1688,54,20,W.Gold);
            Button(panel,"Btn_ClearDeeds","清空自定手牌",32,888,350,52,()=>_draftDeeds.Clear());
            Button(panel,"Btn_DoneDeeds","返回构筑",412,888,1308,52,()=>_campPicker=0,true);
        }
    }
}
