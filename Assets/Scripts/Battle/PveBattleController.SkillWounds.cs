using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private DeedCard _skillWound;
        private bool _skillWoundPicker;
        private int _skillWoundPage;
        private static bool NeedsSkillWound(SkillCard skill)=>skill?.SourceId is "hero_skill_pool_001:08" or "hero_skill_pool_003:04";
        private void DrawSkillWounds(Transform parent,PlayerState player,string instruction=null)
        {
            var wounds=player.Deck.Hand.Where(c=>c.Type==CardType.Wound).ToArray();
            if(!wounds.Any(c=>ReferenceEquals(c,_skillWound)))_skillWound=null;
            _skillWoundPage=Math.Min(_skillWoundPage,Math.Max(0,(wounds.Length-1)/4));
            W.Text(parent,"WoundTargetRule",instruction??"选择一张实际手牌创伤，再选择支付颜色。\n再生是去除伤牌，战斗中也可使用；原条件满足时从真实牌库抽牌。",40,157,900,100,23,W.Cream);
            for(int slot=0;slot<4 && _skillWoundPage*4+slot<wounds.Length;slot++)
            {
                int index=_skillWoundPage*4+slot;var card=wounds[index];int handIndex=player.Deck.Hand.FindIndex(c=>ReferenceEquals(c,card));
                var b=Button(parent,"Btn_Wound_"+index,"",40+slot%2*456,285+slot/2*244,435,229,()=>{_skillWound=card;_skillWoundPicker=false;},ReferenceEquals(card,_skillWound));
                var art=CardArtwork(card);
                if(art!=null)W.Sprite(b.transform,"OriginalWound",art,12,9,143,208);
                W.Text(b.transform,"WoundIdentity","创伤\n手牌第"+(handIndex+1)+"张",173,45,248,120,25,W.Cream,TextAlignmentOptions.Center);
            }
            if(wounds.Length==0)W.Text(parent,"NoWounds","当前手牌没有创伤。",40,295,880,80,28,W.Muted);
            Button(parent,"Btn_WoundsPrevious","上一页创伤",40,810,435,55,()=>_skillWoundPage--).interactable=_skillWoundPage>0;
            Button(parent,"Btn_WoundsNext","下一页创伤",496,810,435,55,()=>_skillWoundPage++).interactable=(_skillWoundPage+1)*4<wounds.Length;
        }
        private void DrawSkillWoundPicker()
        {
            var panel=W.Panel(_root,"SkillWounds",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","再生 · 选择真实手牌创伤",40,25,1800,70,36,W.Gold);
            DrawSkillWounds(panel,_battle.Player);
            Texture(panel,"OriginalSkillReference",SkillReference(_selectedSkill),1030,215,300,420);
            W.Text(panel,"Rule",_selectedSkill.PrintedRule,1390,215,450,420,25,W.Cream);
            Button(panel,"Btn_Back","返回技能选色",40,1000,1800,57,()=>_skillWoundPicker=false);
        }
    }
}
