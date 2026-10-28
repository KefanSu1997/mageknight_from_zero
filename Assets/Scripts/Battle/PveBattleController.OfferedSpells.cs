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
        private bool _offeredSpellsOpen;
        private void DrawOfferedSpells()
        {
            var ctx=_journey.Context;
            var panel=W.Panel(_root,"OfferedSpells",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","魔法天赋 · 使用供应原法术",40,25,1820,60,34,W.Gold);
            W.Text(panel,"Rule","本回合许可："+string.Join("、",ctx.SpellColors.Select(ColorName))+"\n正常支付原法术费用；仅消耗一次同色许可，原牌保留在供应。",40,110,1820,116,26,W.Cream);
            for(int i=0;i<ctx.SpellSupply.Offer.Count;i++)
            {
                var spell=ctx.SpellSupply.Offer[i];var d=deedCatalog.Single(c=>c.id==spell.Id);var choices=_journey.OfferedSpellChoices(spell);
                var b=Button(panel,"Btn_Spell_"+i,"",40+i*614,270,583,508,()=>
                {
                    OpenDeedInputs(new DeedCard(spell.Id,CardType.Spell),choices[0].Id,ctx.InBattle,true);_offeredSpellsOpen=false;
                    _inputSubmit=(choice,input)=>_journey.PlayOfferedSpell(spell,choice,input.TargetIndex,input,input.AttackIndex);
                });
                b.interactable=choices.Length>0&&ctx.SpellColors.Contains(((SpellCardSO)d.source).ManaColor);
                W.Sprite(b.transform,"OriginalOfferedSpell",d.artwork,158,13,266,385);
                W.Text(b.transform,"OfferedSpellName",d.source.NameCn,20,424,542,51,27,W.Gold,TextAlignmentOptions.Center);
            }
            W.Text(panel,"Feedback",_feedback??"选择一张当前阶段合法的供应原法术，再选择原模式与必要目标。",40,868,1810,93,25,W.Cream);
            Button(panel,"Btn_Close","返回当前行动",40,1000,1830,55,()=>_offeredSpellsOpen=false);
        }
    }
}
