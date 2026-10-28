using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _battleSourceOpen;
        private int _battleSourcePage;
        private void OpenEmbeddedSource()
        {
            _battleMenuOpen=false;_journeyTools=false;_fluentSourceOpen=false;
            _battleSourceOpen=!_battleSourceOpen;_battleSourcePage=0;
            _quickSourceReturn=_battleSourceOpen;_tacticDice.Clear();
        }
        private void DrawEmbeddedSource()
        {
            if(_battle.Phase==Phase.End){_battleSourceOpen=false;return;}
            var dice=_journey.Game.Globals.Mana.Dice;int pages=Math.Max(1,(dice.Count+5)/6);
            _battleSourcePage=Math.Min(_battleSourcePage,pages-1);
            bool twoRows=Math.Min(6,dice.Count-_battleSourcePage*6)>3;
            float actionY=twoRows?400:278;
            var p=InformationSurface(_root,"BattleSource",1510,165,385,twoRows?520:398,new Color32(24,46,39,250),true);
            W.Text(p,"SourceHeading","公共魔力",18,8,285,44,28,W.Gold);
            var close=Button(p,"Btn_CloseTools","×",332,8,40,44,()=>{_battleSourceOpen=false;_quickSourceReturn=false;_tacticDice.Clear();});
            SourceTextCommand(close);
            W.Text(p,"SourceQuota","普通 "+(_journey.Player.HeldManaDie==null?"可用":"已用")+" · 额外 "+_journey.Context.ExtraManaDice,18,52,349,40,22,W.Cream);
            if(pages>1)SourceTextCommand(Button(p,"Btn_SourcePage",(_battleSourcePage+1)+"/"+pages,282,52,85,40,()=>_battleSourcePage=(_battleSourcePage+1)%pages));
            var tints=new Color[]{new Color32(222,94,76,255),new Color32(91,166,226,255),new Color32(112,182,115,255),new Color32(224,230,216,255),W.Gold,new Color32(66,61,73,255)};
            for(int slot=0;slot<6 && _battleSourcePage*6+slot<dice.Count;slot++)
            {
                int index=_battleSourcePage*6+slot;var die=dice[index];bool selected=_tacticDice.Contains(die);
                var button=Button(p,"Btn_Source_"+index,"",18+(slot%3)*109,96+(slot/3)*122,102,120,()=>
                {if(_tacticDice.Contains(die))_tacticDice.Remove(die);else {_tacticDice.Clear();_tacticDice.Add(die);}});
                QuietGraphics(button.transform);var hit=button.GetComponent<Graphic>();hit.enabled=true;hit.color=Color.clear;button.targetGraphic=hit;
                var face=W.Rect(button.transform,"SourceDieFace",15,2,72,72).gameObject.AddComponent<BattleSourceDieGraphic>();
                face.selected=selected;face.raycastTarget=false;face.color=tints[(int)die.Face];
                var symbol=W.Rect(button.transform,"SourceDieSigil",30,27,32,32).gameObject.AddComponent<BattleResourceGlyph>();
                symbol.element=(int)die.Face;symbol.color=tints[(int)die.Face];symbol.raycastTarget=false;
                W.Text(button.transform,"SourceDieIdentity",(selected?"已选":"")+ManaShort(die.Face)+"骰",2,76,98,44,22,selected?W.Gold:W.Cream,TextAlignmentOptions.Center);
                HoverInfo(button.gameObject,"第 "+(index+1)+" 颗 · "+ColorName(die.Face),
                    "选择这颗实际源骰，再确认普通或额外取用。选择不会消费。\n金骰保留金色，在付款时决定基本色；黑骰的选色许可由原规则判定。");
                var help=button.GetComponent<BattleHoverInfo>();help.compactGuidance=true;help.bodyFontSize=22;
            }
            bool wild=_tacticDice.Count==1 && _tacticDice[0].Face==ManaColor.Black && (_journey.Context.BlackManaWild || _journey.Context.ExtraBlackManaDice>0);
            if(wild)
            {
                var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black};
                for(int i=0;i<colors.Length;i++)
                {var c=colors[i];SourceTextCommand(Button(p,"Btn_SourceColor_"+c,ManaShort(c),18+i*57,actionY-54,52,44,()=>_sourceColor=c,_sourceColor==c));}
            }
            var ordinary=Button(p,"Btn_UseSource","普通取用",18,actionY,164,50,()=>UseJourneySource(false));
            ordinary.interactable=_journey.Player.HeldManaDie==null;SourceTextCommand(ordinary);
            var extra=Button(p,"Btn_UseExtraSource","额外取用",203,actionY,164,50,()=>UseJourneySource(true));
            extra.interactable=_journey.Context.ExtraManaDice>0;SourceTextCommand(extra);
            var details=Button(p,"Btn_SourceTacticDetails","战术与完整规则",18,actionY+61,349,46,()=>{_battleSourceOpen=false;_journeyTools=true;});SourceTextCommand(details);
        }
        private void SourceTextCommand(Button button)
        {
            QuietGraphics(button.transform);var hit=button.GetComponent<Graphic>();hit.enabled=true;hit.color=Color.clear;button.targetGraphic=hit;
            var text=button.GetComponentInChildren<TMP_Text>();text.fontSize=22;text.color=button.interactable?W.Cream:W.Muted;text.fontStyle=FontStyles.Bold;
            var line=W.Panel(button.transform,"SourceCommandLine",8,button.GetComponent<RectTransform>().rect.height-4,button.GetComponent<RectTransform>().rect.width-16,2,button.interactable?W.Gold:W.Muted);
            line.GetComponent<Graphic>().raycastTarget=false;
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleSourceDieGraphic:MaskableGraphic
    {
        public bool selected;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(r.xMin+r.width*x,r.yMin+r.height*y);
            void Face(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {int n=mesh.currentVertCount;foreach(var v in new[]{a,b,c,d})mesh.AddVert(v,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
            Face(P(.05f,.8f),P(.2f,.95f),P(.95f,.95f),P(.8f,.8f),Color.Lerp(color,Color.white,.55f));
            Face(P(.8f,.05f),P(.95f,.2f),P(.95f,.95f),P(.8f,.8f),Color.Lerp(new Color32(13,26,25,255),color,.35f));
            Face(P(.05f,.05f),P(.05f,.8f),P(.8f,.8f),P(.8f,.05f),selected?new Color32(229,195,124,255):new Color32(147,146,110,255));
            Face(P(.085f,.085f),P(.085f,.765f),P(.765f,.765f),P(.765f,.085f),Color.Lerp(new Color32(20,38,32,255),color,.26f));
        }
    }
}
