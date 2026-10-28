using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private readonly Dictionary<Texture2D, Material> _projectedShadowMaterials = new();

        private void AddCharacterProjection(RawImage figure, Texture2D art, CharacterLayoutFrame frame, float foot)
        {
            var shader = Resources.Load<Shader>("UI/BattleCharacterProjection");
            if (shader == null || !shader.isSupported) return;
            if (!_projectedShadowMaterials.TryGetValue(art, out var material))
            {
                material = new Material(shader) { name = "GroundProjection_" + art.name, hideFlags = HideFlags.DontSave };
                material.SetFloat("_Opacity", .28f);
                material.SetVector("_BodyUV", frame == null ? new Vector4(0, 1, 0, 0)
                    : new Vector4(1-(frame.y+frame.height)/art.height, 1-frame.y/art.height, 0, 0));
                _projectedShadowMaterials.Add(art, material);
            }
            var rect = figure.rectTransform;
            float height = Mathf.Clamp(rect.rect.height * .15f, 20, 46);
            var projected = W.Rect(rect.parent, "CharacterCastShadow", rect.anchoredPosition.x + rect.rect.width * .06f,
                foot - height + 3, rect.rect.width * .88f, height).gameObject.AddComponent<RawImage>();
            projected.texture = art; projected.material = material; projected.color = new Color(1,1,1,figure.color.a);
            projected.uvRect = figure.uvRect;
            projected.raycastTarget = false;
            var shear = projected.gameObject.AddComponent<BattleGroundShear>(); shear.spread = rect.rect.width * .14f;
            projected.transform.SetSiblingIndex(figure.transform.GetSiblingIndex());
        }

        private void PlaceEnemyThreat(Transform target, Transform button, TMP_Text intent, TMP_Text name, TMP_Text state, float width)
        {
            var image = button.Find("EnemyIllustration")?.GetComponent<RawImage>();
            if (image == null || image.texture == null) return;
            var r = image.rectTransform;
            float headTop = -r.anchoredPosition.y;
            var frame = CharacterFrameFor(image.texture);
            if (frame != null && frame.textureWidth == image.texture.width && frame.textureHeight == image.texture.height)
                headTop += frame.y * r.rect.height / image.texture.height;
            float nameTop = headTop - 48;
            Place(name.transform, 0, nameTop, width, 40);
            float previousTop = nameTop;
            if (state != null && !string.IsNullOrWhiteSpace(state.text))
            {
                state.fontSize = Mathf.Max(state.fontSize,20);
                previousTop -= 34; Place(state.transform,0,previousTop,width,30);
            }
            // Size the HUD to its actual contents, with a gutter between actors.
            // Do not shrink type or drop any attack descriptors to fit the lane.
            Place(intent.transform,0,previousTop-50,width,44);
        }
    }

    // Project the existing alpha silhouette onto the ground. This changes only
    // a separate decorative mesh; actor geometry/full UV and hit targets stay put.
    public sealed class BattleGroundShear : BaseMeshEffect
    {
        public float spread;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var v = new UIVertex();
            for(int i=0;i<mesh.currentVertCount;i++)
            { mesh.PopulateUIVertex(ref v,i); v.position.x+=v.uv0.y*spread;mesh.SetUIVertex(v,i); }
        }
    }

    // Shared ink symbols: readiness lightning, exhaustion hourglass, wounds,
    // and the hero's crown. State is conveyed by shape as well as color.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattlePartyStateGlyph : MaskableGraphic
    {
        public int kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(r.xMin+r.width*x,r.yMin+r.height*y);
            void Line(float ax,float ay,float bx,float by,float thickness=2.5f)
            {
                var a=P(ax,ay);var b=P(bx,by);var d=b-a;var n=new Vector2(-d.y,d.x).normalized*thickness/2;
                // A narrow dark keyline keeps pale ink readable on forest ground.
                void Quad(Vector2 q,Color c){int j=mesh.currentVertCount;mesh.AddVert(a+q,c,Vector2.zero);mesh.AddVert(a-q,c,Vector2.zero);mesh.AddVert(b+q,c,Vector2.zero);mesh.AddVert(b-q,c,Vector2.zero);mesh.AddTriangle(j,j+1,j+2);mesh.AddTriangle(j+1,j+3,j+2);}
                Quad(n*2,new Color(.035f,.07f,.055f,color.a));Quad(n,color);
            }
            if(kind==0)
            {Line(.63f,.94f,.23f,.48f);Line(.23f,.48f,.57f,.48f);Line(.57f,.48f,.36f,.06f);Line(.36f,.06f,.81f,.59f);Line(.81f,.59f,.48f,.59f);Line(.48f,.59f,.63f,.94f);}
            else if(kind==1)
            {Line(.2f,.9f,.8f,.9f);Line(.2f,.1f,.8f,.1f);Line(.26f,.86f,.28f,.68f);Line(.28f,.68f,.7f,.3f);Line(.7f,.3f,.74f,.14f);Line(.74f,.86f,.72f,.68f);Line(.72f,.68f,.3f,.3f);Line(.3f,.3f,.26f,.14f);Line(.36f,.2f,.64f,.2f);}
            else if(kind==2)
            {Line(.5f,.94f,.2f,.44f);Line(.2f,.44f,.2f,.25f);Line(.2f,.25f,.34f,.1f);Line(.34f,.1f,.66f,.1f);Line(.66f,.1f,.8f,.25f);Line(.8f,.25f,.8f,.44f);Line(.8f,.44f,.5f,.94f);Line(.35f,.3f,.37f,.23f,2);}
            else if(kind==3)
            {Line(.16f,.27f,.84f,.27f);Line(.2f,.37f,.12f,.8f);Line(.12f,.8f,.36f,.59f);Line(.36f,.59f,.5f,.96f);Line(.5f,.96f,.64f,.59f);Line(.64f,.59f,.88f,.8f);Line(.88f,.8f,.8f,.37f);Line(.2f,.37f,.8f,.37f);}
            else if(kind==4)
            {Line(.15f,.72f,.5f,.92f);Line(.5f,.92f,.85f,.72f);Line(.15f,.43f,.5f,.63f);Line(.5f,.63f,.85f,.43f);Line(.15f,.14f,.5f,.34f);Line(.5f,.34f,.85f,.14f);}
            else if(kind==5)
            {Line(.15f,.84f,.5f,.96f);Line(.5f,.96f,.85f,.84f);Line(.85f,.84f,.8f,.37f);Line(.8f,.37f,.5f,.08f);Line(.5f,.08f,.2f,.37f);Line(.2f,.37f,.15f,.84f);Line(.5f,.8f,.5f,.28f,2);}
            else if(kind==8)
            {Line(.5f,.92f,.61f,.66f);Line(.61f,.66f,.9f,.64f);Line(.9f,.64f,.68f,.45f);Line(.68f,.45f,.75f,.17f);Line(.75f,.17f,.5f,.32f);Line(.5f,.32f,.25f,.17f);Line(.25f,.17f,.32f,.45f);Line(.32f,.45f,.1f,.64f);Line(.1f,.64f,.39f,.66f);Line(.39f,.66f,.5f,.92f);}
            else
            {Line(.22f,.5f,.78f,.5f);Line(.78f,.5f,.78f,.13f);Line(.78f,.13f,.22f,.13f);Line(.22f,.13f,.22f,.5f);Line(.34f,.5f,.34f,.78f);Line(.34f,.78f,.48f,.9f);Line(.48f,.9f,.66f,.78f);if(kind==6)Line(.66f,.78f,.66f,.5f);else Line(.66f,.78f,.8f,.87f);Line(.5f,.36f,.5f,.24f,3);}
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleThreatSwords : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            Vector2 P(float x,float y)=>new Vector2(r.xMin+r.width*x,r.yMin+r.height*y);
            void Line(float ax,float ay,float bx,float by,float thickness=2)
            {
                var a=P(ax,ay);var b=P(bx,by);var delta=b-a;var n=new Vector2(-delta.y,delta.x).normalized*thickness/2;int k=mesh.currentVertCount;
                mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
                mesh.AddTriangle(k,k+1,k+2);mesh.AddTriangle(k+1,k+3,k+2);
            }
            Line(.15f,.13f,.85f,.87f,3);Line(.15f,.87f,.85f,.13f,3);
            Line(.1f,.35f,.36f,.1f);Line(.64f,.1f,.9f,.35f);
            Line(.7f,.84f,.85f,.87f);Line(.85f,.87f,.82f,.71f);
            Line(.15f,.87f,.3f,.84f);Line(.15f,.87f,.18f,.71f);
        }
    }
}
