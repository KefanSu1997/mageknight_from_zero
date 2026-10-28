using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _harvestPicker;
        private int _harvestPage;
        private DeedCard _harvestMaterial;
        private ManaColor _harvestColor=ManaColor.Red;

        private void DrawHarvestPicker()
        {
            var panel=W.Panel(_root,"HarvestInputs",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","野蛮收获 · 随下一次成功移动弃牌换晶",40,25,1820,65,34,W.Gold);
            W.Text(panel,"Rule","可不弃牌。普通卡得到印刷同色魔晶；神器可选择基本色。移动失败不会弃牌，也不会消耗次数。",40,114,1820,92,24,W.Cream);
            var cards=_journey.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray();
            if(_harvestMaterial!=null&&!cards.Any(c=>ReferenceEquals(c,_harvestMaterial)))_harvestMaterial=null;
            _harvestPage=Math.Min(_harvestPage,Math.Max(0,(cards.Length-1)/4));
            for(int i=0;i<4&&_harvestPage*4+i<cards.Length;i++)
            {
                int index=_harvestPage*4+i;var c=cards[index];
                var b=Button(panel,"Btn_Material_"+index,(ReferenceEquals(c,_harvestMaterial)?"● ":"")+CardName(c),40+i*464,585,444,55,()=>_harvestMaterial=c);
                var art=Binding(c)?.artwork;if(art!=null)W.Sprite(panel,"OriginalMaterial_"+index,art,137+i*464,225,240,349);
            }
            Button(panel,"Btn_None",_harvestMaterial==null?"● 不弃牌":"不弃牌",40,688,444,55,()=>_harvestMaterial=null);
            if(_harvestMaterial?.Type==CardType.Artifact)
                for(int i=0;i<4;i++)
                {var color=(ManaColor)i;Button(panel,"Btn_Color_"+color,(_harvestColor==color?"● ":"")+ColorName(color),510+i*343,688,325,55,()=>_harvestColor=color);}
            Button(panel,"Btn_Previous","上一页",40,802,444,55,()=>_harvestPage--).interactable=_harvestPage>0;
            Button(panel,"Btn_Next","下一页",1432,802,444,55,()=>_harvestPage++).interactable=(_harvestPage+1)*4<cards.Length;
            W.Text(panel,"Feedback",_feedback??"此处只选择材料，确认地图移动时按正式规则一起结算。",40,915,1820,59,23,W.Cream);
            Button(panel,"Btn_Done","返回地图确认移动",40,1000,1830,55,()=>_harvestPicker=false,true);
        }
    }
}
