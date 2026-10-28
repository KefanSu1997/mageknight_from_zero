using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private readonly ManaColor[] _artifactColors={ManaColor.Red,ManaColor.Blue,ManaColor.White};
        private void ResetArtifactInputs()
        {
            _artifactBurn=false;
            _artifactColors[0]=ManaColor.Red;_artifactColors[1]=ManaColor.Blue;_artifactColors[2]=ManaColor.White;
        }
        private ManaColor[] ArtifactColors(DeedCard card,bool burn)
            => _artifactColors.Take(PveArtifactInputs.ColorCount(card.Id,burn)).ToArray();
        private void DrawArtifactColors(Transform parent,DeedCard card,bool burn,float x,float y,float width,float rowHeight,string effectiveId=null)
        {
            string id=effectiveId??card.Id;
            for(int i=0;i<PveArtifactInputs.ColorCount(id,burn);i++)
            {
                int index=i;
                string label=(id=="items_011" ? "第"+(i+1)+"次掷到金色时：" : "第"+(i+1)+"颗魔力：")+ColorName(_artifactColors[i])+" · 点击切换";
                var text=Button(parent,"Btn_ArtifactColor_"+i,label,x,y+i*rowHeight,width,rowHeight-4,
                    ()=>_artifactColors[index]=(ManaColor)(((int)_artifactColors[index]+1)%(id=="items_011"?4:6))).GetComponentInChildren<TextMeshProUGUI>();
                text.enableAutoSizing=true;text.fontSizeMin=15;text.fontSizeMax=20;
            }
        }
        private string ManaDisplay(ManaColor color) => _battle.Context.InfiniteMana.Contains(color) ? "∞" : Token(color).ToString();
    }
}
