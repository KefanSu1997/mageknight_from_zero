using MK.Logic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private Sprite _manaReliquarySprite;
        private void DrawEmbeddedManaInventory()
        {
            // A single illustrated inventory object, never a button per value.
            // The UI sprite has a declared authored region; all character/card
            // illustrations remain separate and retain their complete UVs.
            var art=Resources.Load<Texture2D>("UI/CombatChrome/illustrated_v1/mana_reliquary");
            if(art!=null)
            {
                if(_manaReliquarySprite==null || _manaReliquarySprite.texture!=art)
                {
                    float sx=art.width/2172f,sy=art.height/724f;
                    _manaReliquarySprite=Sprite.Create(art,new Rect(39*sx,188*sy,2095*sx,410*sy),new Vector2(.5f,.5f),100,0,
                        SpriteMeshType.FullRect,new Vector4(260*sx,110*sy,260*sx,140*sy));
                    _manaReliquarySprite.name="ManaReliquaryIllustratedRegion";
                    _manaReliquarySprite.hideFlags=HideFlags.DontSave;
                }
                var image=W.Rect(_root,"ManaReliquary",520,0,720,155).gameObject.AddComponent<Image>();
                image.sprite=_manaReliquarySprite;image.type=Image.Type.Sliced;
                image.pixelsPerUnitMultiplier=4*art.height/724f;image.raycastTarget=false;
            }
            W.Text(_root,"ManaTokenLegend","魔力",578,50,66,44,22,W.Cream);
            W.Text(_root,"ManaCrystalLegend","晶体",578,84,66,44,22,W.Cream);
            var colors=new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black};
            var tints=new Color[]{new Color32(222,94,76,255),new Color32(91,166,226,255),new Color32(112,182,115,255),
                new Color32(224,230,216,255),W.Gold,new Color32(66,61,73,255)};
            for(int i=0;i<colors.Length;i++)
            {
                var mana=colors[i];bool basic=i<4;
                // A transparent information hit surface; neither row pays or
                // reserves resources. Formal payment remains in native actions.
                var slot=W.Rect(_root,"ManaInventory_"+mana,650+92*i,0,92,140);
                slot.gameObject.AddComponent<Image>().color=Color.clear;
                W.Text(slot,"ManaColorIdentity",ManaShort(mana),0,25,92,40,22,W.Cream,TextAlignmentOptions.Center);
                var token=W.Rect(slot,"ManaTokenSigil",0,60,30,30).gameObject.AddComponent<BattleResourceGlyph>();
                token.element=i;token.color=tints[i];token.raycastTarget=false;
                if(basic || Crystal(mana)>0)
                {
                    var crystal=W.Rect(slot,"ManaCrystalGem",0,94,30,30).gameObject.AddComponent<BattleResourceGlyph>();
                    crystal.element=i;crystal.crystal=true;crystal.color=tints[i];crystal.raycastTarget=false;
                }
                W.Text(slot,"ManaTokenAmount",ManaDisplay(mana),34,50,58,44,26,W.Cream,TextAlignmentOptions.Left).fontStyle=FontStyles.Bold;
                bool cannotStore=!basic&&Crystal(mana)==0;
                string stored=cannotStore?"×":Crystal(mana).ToString();
                W.Text(slot,"ManaCrystalAmount",stored,34,84,58,44,26,W.Cream,TextAlignmentOptions.Left).fontStyle=FontStyles.Bold;
                string stock=basic?"库存晶体 "+Crystal(mana)+"。同色最多储存3颗，超出转为魔力。":"此色不能储存为晶体。";
                string temporal=_battle.Context.InfiniteMana.Contains(mana)?"本回合该色魔力无尽；回合结束失效。":"当前魔力 "+ManaDisplay(mana)+"；回合结束清空。";
                HoverInfo(slot.gameObject,ColorName(mana)+" · 魔力与晶体",
                    "上行符印："+temporal+"\n下行棱晶："+stock+"\n叉号表示该色不能存为晶体。"+
                    "\n金色与黑色的替代/时段限制、实际付款来源由原规则判定；查看数量不会消费资源。");
                var help=slot.GetComponent<BattleHoverInfo>();help.compactGuidance=true;help.bodyFontSize=22;
            }
        }
    }

    // One metal-edged, faceted visual family. Temporary mana has elemental
    // silhouettes; stored crystals share a cut-gem silhouette below it.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleResourceGlyph : MaskableGraphic
    {
        public int element;
        public bool crystal,unavailable,runeMounted;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void Tri(Vector2 a,Vector2 b,Vector2 c,Color tint)
            {
                int n=mesh.currentVertCount;mesh.AddVert(a,tint,Vector2.zero);mesh.AddVert(b,tint,Vector2.zero);mesh.AddVert(c,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);
            }
            void Line(Vector2 a,Vector2 b,Color tint,float width=1.1f)
            {
                var d=(b-a).normalized;var v=new Vector2(-d.y,d.x)*width;
                Tri(a+v,a-v,b+v,tint);Tri(a-v,b-v,b+v,tint);
            }
            Vector2[] outline;
            if(crystal)outline=new[]{P(.5f,.94f),P(.86f,.65f),P(.78f,.25f),P(.5f,.06f),P(.22f,.25f),P(.14f,.65f)};
            else if(element==0)outline=new[]{P(.5f,.97f),P(.62f,.6f),P(.8f,.78f),P(.89f,.39f),P(.7f,.1f),P(.33f,.08f),P(.13f,.37f),P(.26f,.7f),P(.37f,.51f)};
            else if(element==1)outline=new[]{P(.51f,.97f),P(.79f,.65f),P(.89f,.37f),P(.73f,.12f),P(.44f,.07f),P(.17f,.22f),P(.13f,.46f),P(.37f,.76f)};
            else if(element==2)outline=new[]{P(.85f,.95f),P(.83f,.47f),P(.62f,.16f),P(.21f,.1f),P(.13f,.4f),P(.35f,.74f)};
            else if(element==5)outline=new[]{P(.72f,.94f),P(.38f,.86f),P(.14f,.62f),P(.12f,.33f),P(.36f,.1f),P(.75f,.13f),P(.48f,.34f),P(.42f,.58f),P(.55f,.81f)};
            else
            {
                int points=element==4?16:8;outline=new Vector2[points];
                for(int i=0;i<points;i++){float a=Mathf.PI/2+i*Mathf.PI*2/points;float d=i%2==0?.46f:.21f;outline[i]=P(.5f+Mathf.Cos(a)*d,.5f+Mathf.Sin(a)*d);}
            }
            var center=P(crystal?.5f:element==5?.28f:.5f,.48f);
            Color edge=runeMounted?((Color)new Color32(38,46,34,255)).linear:element==3?new Color32(239,241,228,255):element==5?new Color32(191,190,196,255):new Color32(222,201,153,255);var dark=new Color32(12,28,28,255);
            for(int i=0;i<outline.Length;i++)
            {
                var next=outline[(i+1)%outline.Length];
                Color face=Color.Lerp(dark,color,i%2==0?.72f:.38f);
                if(unavailable)face=Color.Lerp(dark,Color.gray,.16f);
                Tri(center,outline[i],next,face);Line(outline[i],next,unavailable?new Color32(106,112,104,255):edge);
            }
            if(crystal&&!unavailable)
            {
                Line(P(.5f,.94f),P(.37f,.6f),color);Line(P(.37f,.6f),P(.5f,.06f),color);Line(P(.37f,.6f),P(.86f,.65f),color);
            }
            else if(unavailable)Line(P(.17f,.14f),P(.83f,.86f),new Color32(137,143,131,255));
            else if(element==2)Line(P(.21f,.1f),P(.7f,.76f),edge);
            else if(element==1){Line(P(.25f,.34f),P(.48f,.23f),edge);Line(P(.48f,.23f),P(.71f,.32f),edge);}
        }
    }
}
