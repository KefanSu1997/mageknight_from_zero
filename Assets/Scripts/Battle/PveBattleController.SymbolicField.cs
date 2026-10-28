using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private RectTransform FieldFact(Transform parent, string id, string mark, string value,
            string title, string explanation, float x, float y, float width = 90)
        {
            var cell = W.Rect(parent, id, x, y, width, 44);
            var hit = cell.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var glyph = W.Rect(cell, "Sigil", 2, 5, 34, 34).gameObject.AddComponent<BattleFieldMark>();
            glyph.mark = mark; glyph.color = W.Gold; glyph.raycastTarget = false;
            if (!string.IsNullOrEmpty(value))
            {
                var number = W.Text(cell, "Value", value, 37, 0, width - 39, 44, 24, W.Cream, TextAlignmentOptions.MidlineLeft);
                number.fontStyle = FontStyles.Bold; number.raycastTarget = false;
                number.outlineColor = new Color32(10, 20, 17, 255); number.outlineWidth = .24f;
            }
            HoverInfo(cell.gameObject, title, explanation, true);
            var help = cell.GetComponent<BattleHoverInfo>(); help.symbolFact = true; help.compactRule = true; help.bodyFontSize = 22;
            return cell;
        }

        private void DrawEnemySymbolReadout(Transform parent, TMP_Text intent, TMP_Text numbers,
            Monster enemy, int index, string canonical, float width)
        {
            float top = -intent.rectTransform.anchoredPosition.y;
            intent.gameObject.SetActive(false); if (numbers != null) numbers.gameObject.SetActive(false);
            int attacks = enemy.SummonAttack ? 1 : Math.Max(1, enemy.AttackCount);
            float cellWidth = Mathf.Min(108, width / attacks);
            for (int a = 0; a < attacks; a++)
            {
                var profile = enemy.SummonAttack ? null : CardCombatActions.DefenseProfile(enemy, index, _battle.Context, a);
                string mark = enemy.SummonAttack ? "Summon" : profile.AttackElement == Element.Fire ? "Fire"
                    : profile.AttackElement == Element.Ice ? "Ice" : profile.AttackElement == Element.ColdFire ? "ColdFire" : "Attack";
                string value = enemy.SummonAttack ? "×" + enemy.SummonCount : profile.Attack.ToString();
                FieldFact(parent, "FieldFact_Attack_" + a, mark, value, "第" + (a + 1) + "次攻击", canonical,
                    (width - attacks * cellWidth) / 2 + a * cellWidth, top, cellWidth);
            }
            int armor = CombatMath.EffectiveArmor(enemy, index, _battle.Context);
            int bonus = EnemyRewardRules.GetFactionFame(enemy, _battle.Context);
            var armorCell=FieldFact(parent, "FieldFact_Armor", "Armor", armor.ToString(), "当前有效护甲", canonical, (width - 176) / 2, 425, 88);
            var fameCell=FieldFact(parent, "FieldFact_Fame", "Fame", enemy.Fame + (bonus > 0 ? "+" + bonus : ""), "名望奖励", canonical, (width - 176) / 2 + 88, 425, 88);
            foreach(var cell in new[]{armorCell,fameCell})
            {cell.sizeDelta=new Vector2(88,36);Place(cell.Find("Sigil"),2,1,34,34);}
            var abilities = enemy.Abilities.ToArray();
            string[] labels = Abilities(enemy, index).Split(new[] { " · " }, StringSplitOptions.None);
            // Keep one full-size mark per rule; row wrapping never shrinks marks.
            int columns = Math.Max(1, (int)(width / 38));
            for (int a = 0; a < abilities.Length; a++)
            {
                string label = a < labels.Length ? labels[a] : abilities[a].ToString();
                int inRow = Math.Min(columns, abilities.Length - a / columns * columns);
                var fact = FieldFact(parent, "Trait_" + a, abilities[a].ToString(), "", label,
                    label + "\n\n" + canonical + "\n\n" + RulesText(), (width - inRow * 38) / 2 + a % columns * 38, 465 + a / columns * 42, 38);
                fact.sizeDelta=new Vector2(38,38);Place(fact.Find("Sigil"),2,2,34,34);
                if (label.Contains("已移除") || label.Contains("已忽略"))
                {
                    var slash = W.Rect(fact, "InactiveRuleSlash", 1, 7, 30, 30).gameObject.AddComponent<BattleFieldMark>();
                    slash.mark = "Negate"; slash.color = new Color32(235, 130, 105, 255); slash.raycastTarget = false;
                }
            }
        }

        private void FinishSymbolicBattlefield()
        {
            // Remove the prose-plate pattern, including captions and utility HUDs.
            // Actual button seals, full card art, ground shadows and deliberate
            // reading surfaces are different objects and remain available.
            string[] plates = { "CompactHeaderShade", "ManaInventoryShade", "PhaseBackdrop", "ResourceReadability",
                "HeroCaptionShade", "CaptionShade", "UtilityCaptionBacking", "UtilityIconShade", "ActionShelfShade", "ActionReadability", "ThreatBadge", "ThreatSwords" };
            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true))
                if (graphic is BattleLabelBacking || plates.Contains(graphic.name)) graphic.enabled = false;
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>())
                EmbossFieldText(text);
            _status.gameObject.SetActive(false); _pools.gameObject.SetActive(false);
            FieldFact(_root, "FieldFact_HeroArmor", "Armor", _battle.Player.Armor.ToString(), "英雄护甲", StatusText(), 38, 65, 100);
            FieldFact(_root, "FieldFact_HeroWounds", "Wounds", _battle.Player.Wounds.ToString(), "英雄创伤", StatusText(), 148, 65, 100);
            FieldFact(_root, "FieldFact_HeroFame", "Fame", _battle.Player.Fame.ToString(), "英雄名望", StatusText(), 258, 65, 130);
            string pool = PoolText();
            int[] values = { _battle.AttackTotal(AttackType.Ranged), _battle.AttackTotal(AttackType.Siege), _battle.Context.BlockPool, _battle.AttackTotal(AttackType.Melee), _battle.Context.InfluencePool, _battle.Player.Reputation };
            string[] marks = { "Ranged", "Siege", "Armor", "Attack", "Influence", "Reputation" };
            string[] names = { "远程资源", "攻城资源", "格挡资源", "近战资源", "影响力", "声誉" };
            int count = pool.Contains("影响力") ? 6 : 4;
            for (int i = 0; i < count; i++)
            {var cell=FieldFact(_root, "FieldFact_Pool_" + i, marks[i], values[i].ToString(), names[i], pool, 1100 - count * 49 + i * 98, 702, 98);cell.sizeDelta=new Vector2(98,38);Place(cell.Find("Sigil"),2,2,34,34);}
            if (_battle.Context.AllAttacksColdFire || _battle.Context.AllAttacksSiege)
                FieldFact(_root,"FieldFact_PoolModifier","Rule","", "当前攻击修正",pool,1458,698,40);
            string[] prose = { "SkillDecisionRule", "EffectiveTarget", "CurrentCardEffect", "MinimalDecisionCue", "BlockRiskCue", "HandReadingCue", "UnitAvailability" };
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>().ToArray())
            {
                if (text.name == "CardEffectHint") { text.gameObject.SetActive(false); continue; }
                if (!prose.Contains(text.name) || string.IsNullOrWhiteSpace(text.text)) continue;
                var old = text.GetComponent<BattleHoverInfo>();
                string title = text.name == "EffectiveTarget" ? "目标与结算预览" : text.name == "CurrentCardEffect" ? "所选效果与费用" : "规则与状态";
                string body = old != null ? old.body : text.text;
                var rect = text.rectTransform;
                if(text.name=="EffectiveTarget")
                {DrawDecisionFacts(rect.parent,rect.anchoredPosition.x,-rect.anchoredPosition.y,body);text.gameObject.SetActive(false);continue;}
                FieldFact(rect.parent, "FieldFact_" + text.name, text.name == "EffectiveTarget" ? "Armor" : "Rule",
                    text.name == "EffectiveTarget" ? _targets.Count.ToString() : "", title, body,
                    rect.anchoredPosition.x, -rect.anchoredPosition.y, text.name == "EffectiveTarget" ? 90 : 44);
                text.gameObject.SetActive(false);
            }
            var manaCue=_root.Find("ManaHoverCue");if(manaCue!=null)manaCue.gameObject.SetActive(false);
            // The inventory's own six information surfaces retain the complete
            // distinction; do not add a seventh framed book icon above them.
            CompactUnitCommands();
            InstallReadonlyFolios66();
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>()) EmbossFieldText(text);
            InstallRuneMounts();
            InstallCardChoiceMounts();
            RestyleActionDock66();
        }

        private void InstallCardChoiceMounts()
        {
            foreach(var button in _root.GetComponentsInChildren<Button>())
            {
                if(_selected==null)continue;
                bool mode=button.name=="Btn_Choice_base"||button.name=="Btn_Choice_strong";
                bool resource=button.transform.Find("ActionObjectGlyph")!=null && button.name.StartsWith("Btn_Choice_",StringComparison.Ordinal);
                bool sideways=button.name=="Btn_Sideways"&&!button.interactable;
                if(!mode&&!resource&&!sideways)continue;
                var r=(RectTransform)button.transform;var caption=button.GetComponentInChildren<TMP_Text>();if(caption==null)continue;
                float width=sideways?180:r.rect.width;
                RuneMount(r,"ChoiceRuneMount",0,0,width,r.rect.height,0);
                if(mode||sideways)
                {
                    var glyph=W.Rect(r,"ChoiceModeGlyph",7,Mathf.Max(1,(r.rect.height-28)/2),28,28).gameObject.AddComponent<BattleFieldMark>();
                    glyph.mark=sideways?"Sideways":button.name=="Btn_Choice_strong"?"Strong":"Base";glyph.color=RuneInk;glyph.raycastTarget=false;
                    Place(caption.transform,42,0,width-47,r.rect.height);
                }
                if(sideways)
                {
                    var help=button.GetComponent<BattleHoverInfo>();if(help!=null)help.body=caption.text+"\n\n"+help.body;
                    caption.text="横置不可用";
                }
                RuneText(caption);
            }
        }

        private void DrawDecisionFacts(Transform parent,float x,float y,string full)
        {
            void Fact(int n,string mark,string value,string title)
            {
                var cell=FieldFact(parent,"FieldFact_Decision_"+n,mark,value,title,full,x+n%3*110,y+n/3*78,106);
                cell.sizeDelta=new Vector2(106,72);
                var label=W.Text(cell,"Meaning",title,4,43,98,29,21,W.Cream,TextAlignmentOptions.Center);label.raycastTarget=false;
            }
            if(_battle.Phase==Phase.AssignDamage)
            {
                int pending=Enumerable.Range(0,_battle.Enemies.Count).Sum(i=>Enumerable.Range(0,_battle.Enemies[i].AttackCount).Count(a=>!_battle.IsBlocked(i,a)&&!_battle.Context.IsAttackCancelled(i,a)&&!_battle.Context.DefeatedEnemyIndices.Contains(i)&&CardCombatActions.DefenseProfile(_battle.Enemies[i],i,_battle.Context,a).Attack>0));
                Fact(0,"Incoming",pending.ToString(),"攻击笔数");Fact(1,"Plan",_battle.DamagePlan.Count.ToString(),"已排顺序");Fact(2,"Target",_targets.Count.ToString(),"所选目标");
                return;
            }
            if(_targets.Count==0)return;
            if(_battle.Phase==Phase.Block)
            {
                var parts=SelectedBlockParts();var value=parts.Length==0?(Effective:0,Required:_battle.BlockPreview(_targets.Single(),_attackSlot).Required):_battle.BlockSelectedPreview(_targets.Single(),parts,_attackSlot);
                Fact(0,"Target",(_attackSlot+1).ToString(),"攻击序号");Fact(1,"Armor",value.Required.ToString(),"需求");Fact(2,"Committed",value.Effective.ToString(),"有效格挡");return;
            }
            var targets=_targets.OrderBy(i=>i).ToArray();var selected=SelectedAttackParts(targets);
            int effective=CombatMath.EffectiveAttack(_battle.Enemies,targets,selected,_battle.Phase,_battle.Context);
            Fact(0,"Target",targets.Length.ToString(),"目标");Fact(1,"Committed",effective.ToString(),"有效攻击");
            if(_battle.DefendChoices(targets).Count>0){Fact(2,"Alert","!","待分防护");return;}
            int need=_battle.AttackArmorPreview(targets);Fact(2,"Armor",need.ToString(),"需求");
            Fact(3,"Missing",Math.Max(0,need-effective).ToString(),"差额");
            Fact(4,"Reserve",(_battle.Context.CombatPower.AvailableAttacks(_battle.Phase,_battle.Context.AllAttacksSiege).Length-selected.Length).ToString(),"保留贡献");
            if(!CombatMath.CanAttack(_battle.Enemies,targets,selected,_battle.Phase,_battle.Context))Fact(5,"Alert","!","城防限制");
        }

        private void CompactUnitCommands()
        {
            if(_selectedUnit==null)return;
            foreach(var evidence in _root.GetComponentsInChildren<BattleUnitCommandEvidence>())
            {
                var button=evidence.GetComponent<Button>();
                if(!int.TryParse(button.name.Substring("Btn_UnitAbility_".Length),out int index))continue;
                var a=_selectedUnit.Card.Actions[index];var caption=button.GetComponentInChildren<TMP_Text>();
                caption.text=a.Value.ToString();caption.fontSize=28;Place(caption.transform,60,0,68,44);
                RuneMount(button.transform,"UnitAmountRuneMount",60,0,68,44,caption.transform.GetSiblingIndex());
                if(a.Cost.Length==0)FieldFact(button.transform,"FieldFact_Fee","Free","0","印刷魔力费用",evidence.fullCaption,60,44,85);
                else for(int i=0;i<a.Cost.Length;i++)
                {
                    var fee=FieldFact(button.transform,"FieldFact_Fee_"+i,"Rule","", "印刷费用 · "+ColorName(a.Cost[i]),evidence.fullCaption,60+i*42,44,38);
                    fee.Find("Sigil").gameObject.SetActive(false);
                    var gem=W.Rect(fee,"FeeGem",4,7,30,30).gameObject.AddComponent<BattleResourceGlyph>();gem.element=(int)a.Cost[i];gem.color=a.Cost[i] switch {ManaColor.Red=>new Color32(180,48,35,255),ManaColor.Blue=>new Color32(30,93,178,255),ManaColor.Green=>new Color32(38,120,53,255),ManaColor.White=>new Color32(146,158,166,255),ManaColor.Gold=>new Color32(169,113,24,255),_=>new Color32(46,36,57,255)};gem.raycastTarget=false;
                }
                int influence=CardUnitActions.ActivationInfluenceCost(_battle.Context,_selectedUnit);
                if(influence>0)FieldFact(button.transform,"FieldFact_InfluenceFee","Influence",influence.ToString(),"额外影响力费用",evidence.fullCaption,150,44,86);
                if(a.DoubleAgainstSwift)FieldFact(button.transform,"FieldFact_SwiftBonus","Swift","×2","对迅捷加倍",evidence.fullCaption,240,44,90);
            }
        }

        private static readonly Color RuneInk = ((Color)new Color32(35, 43, 34, 255)).linear;
        private static void RuneText(TMP_Text text)
        {
            text.color=RuneInk; text.outlineWidth=0;
            if(text.fontMaterial!=null)text.fontMaterial.DisableKeyword("UNDERLAY_ON");
            text.fontStyle|=FontStyles.Bold;
        }
        private static void RuneMount(Transform parent,string name,float x,float y,float width,float height,int sibling)
        {
            if(parent.Find(name)!=null)return;
            var mount=W.Rect(parent,name,x,y,width,height).gameObject.AddComponent<BattleRuneMount>();
            mount.raycastTarget=false;mount.transform.SetSiblingIndex(sibling);
        }
        private void InstallRuneMounts()
        {
            foreach(var mark in _root.GetComponentsInChildren<BattleFieldMark>().ToArray())
            {
                var parent=mark.transform.parent as RectTransform;
                if(parent==null)continue;
                bool clothed=parent.GetComponent<BattleReadonlyFolio66>()!=null;
                if(mark.name=="InactiveRuleSlash"){mark.color=clothed?new Color32(245,126,103,255):((Color)new Color32(150,46,31,255)).linear;continue;}
                if(clothed)
                {
                    mark.color=mark.mark=="Fire"||mark.mark=="FireResist"?new Color32(244,168,125,255)
                        :mark.mark=="Ice"||mark.mark=="IceResist"?new Color32(157,218,244,255):W.Cream;
                    foreach(var t in parent.GetComponentsInChildren<TMP_Text>()){t.color=W.Cream;EmbossFieldText(t);}
                    continue;
                }
                RuneMount(parent,"RuneMount",0,0,parent.rect.width,parent.rect.height,0);
                mark.color=RuneInk;
                foreach(var text in parent.GetComponentsInChildren<TMP_Text>())RuneText(text);
            }
            foreach(var evidence in _root.GetComponentsInChildren<BattleUnitCommandEvidence>())
            {var caption=evidence.GetComponentInChildren<TMP_Text>();if(caption!=null)RuneText(caption);}
            foreach(var slot in _root.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ManaInventory_",StringComparison.Ordinal)).ToArray())
            {
                foreach(var text in slot.GetComponentsInChildren<TMP_Text>())
                {text.color=W.Cream;EmbossFieldText(text);}
            }
            foreach(var gem in _root.GetComponentsInChildren<BattleResourceGlyph>().Where(g=>g.name=="FeeGem"))gem.runeMounted=true;
            foreach(var glyph in _root.GetComponentsInChildren<Graphic>().Where(g=>g is BattlePartyStateGlyph||g is BattleUtilityGlyph).ToArray())
            {
                var r=glyph.rectTransform;var parent=r.parent;
                if(parent.GetComponent<BattleReadonlyFolio66>()!=null)
                {
                    foreach(var t in parent.GetComponentsInChildren<TMP_Text>()){t.color=W.Cream;EmbossFieldText(t);}
                    continue;
                }
                bool fact=parent.name.StartsWith("Unit",StringComparison.Ordinal)&&parent.name.EndsWith("Fact",StringComparison.Ordinal);
                if(fact)
                {
                    var p=(RectTransform)parent;RuneMount(parent,"StateRuneMount",0,0,p.rect.width,80,0);
                    glyph.color=RuneInk;
                    foreach(var text in parent.GetComponentsInChildren<TMP_Text>())RuneText(text);
                }
                else
                {
                    float width=glyph.name=="WoundSymbol"?70:r.rect.width+4;
                    RuneMount(parent,glyph.name+"RuneMount",r.anchoredPosition.x-2,-r.anchoredPosition.y-2,width,r.rect.height+4,glyph.transform.GetSiblingIndex());
                    glyph.color=RuneInk;
                    if(glyph.name=="WoundSymbol")
                    {var count=parent.Find("WoundCount")?.GetComponent<TMP_Text>();if(count!=null)RuneText(count);}
                }
            }
        }

        private static void EmbossFieldText(TMP_Text text)
        {
            text.extraPadding = true;
            if (text.text.Length <= 14) text.fontStyle |= FontStyles.Bold;
            text.outlineWidth = .3f; text.outlineColor = new Color32(9, 20, 16, 255);
            var material = text.fontMaterial;
            if (material != null && material.HasProperty("_UnderlayColor"))
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", new Color(.015f, .025f, .018f, 1));
                material.SetFloat("_UnderlayOffsetX", .5f); material.SetFloat("_UnderlayOffsetY", -.5f);
                material.SetFloat("_UnderlayDilate", .45f); material.SetFloat("_UnderlaySoftness", .08f);
            }
        }
    }

    // A single engraved line-symbol family. A dark keyline is part of the mark,
    // never a rectangular text backing. Exact rule names remain on hover.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleFieldMark : MaskableGraphic
    {
        public string mark;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 P(float x,float y) => new Vector2(r.xMin+r.width*x,r.yMin+r.height*y);
            void L(float x,float y,float u,float v)
            {
                var a=P(x,y); var b=P(u,v); var n=new Vector2(-(b-a).y,(b-a).x).normalized;
                void Q(float w,Color tint){var d=n*w;int k=mesh.currentVertCount;mesh.AddVert(a+d,tint,Vector2.zero);mesh.AddVert(a-d,tint,Vector2.zero);mesh.AddVert(b+d,tint,Vector2.zero);mesh.AddVert(b-d,tint,Vector2.zero);mesh.AddTriangle(k,k+1,k+2);mesh.AddTriangle(k+1,k+3,k+2);}
                // The opaque enamel face already isolates the mark. A second
                // dark 5px keyline closes the negative space in elemental marks.
                Q(1.3f,color);
            }
            void Ring(float cx,float cy,float radius){for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;L(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius,cx+Mathf.Cos(b)*radius,cy+Mathf.Sin(b)*radius);}}
            void Shield(){L(.18f,.83f,.5f,.96f);L(.5f,.96f,.82f,.83f);L(.82f,.83f,.77f,.38f);L(.77f,.38f,.5f,.09f);L(.5f,.09f,.23f,.38f);L(.23f,.38f,.18f,.83f);}
            void Flame(){L(.47f,.95f,.27f,.52f);L(.27f,.52f,.17f,.61f);L(.17f,.61f,.2f,.27f);L(.2f,.27f,.5f,.08f);L(.5f,.08f,.83f,.3f);L(.83f,.3f,.7f,.74f);L(.7f,.74f,.6f,.49f);L(.6f,.49f,.47f,.95f);}
            void Ice(){L(.5f,.13f,.5f,.87f);L(.18f,.3f,.82f,.7f);L(.18f,.7f,.82f,.3f);L(.34f,.79f,.5f,.63f);L(.66f,.79f,.5f,.63f);L(.34f,.21f,.5f,.37f);L(.66f,.21f,.5f,.37f);}
            switch(mark)
            {
                case "Armor": case "Defend": case "Guard": Shield(); if(mark!="Armor")L(.3f,.52f,.7f,.52f);break;
                case "Fame": for(int i=0;i<5;i++){float a=(90+i*144)*Mathf.Deg2Rad,b=(90+(i+1)*144)*Mathf.Deg2Rad;L(.5f+Mathf.Cos(a)*.4f,.5f+Mathf.Sin(a)*.4f,.5f+Mathf.Cos(b)*.4f,.5f+Mathf.Sin(b)*.4f);}break;
                case "Wounds": case "Vampirism": L(.5f,.96f,.18f,.35f);L(.18f,.35f,.26f,.13f);L(.26f,.13f,.7f,.13f);L(.7f,.13f,.82f,.35f);L(.82f,.35f,.5f,.96f);if(mark=="Vampirism"){L(.3f,.7f,.4f,.46f);L(.7f,.7f,.6f,.46f);}break;
                case "Ranged": case "Elusive": L(.25f,.1f,.6f,.5f);L(.6f,.5f,.25f,.9f);L(.25f,.1f,.25f,.9f);L(.1f,.5f,.9f,.5f);L(.9f,.5f,.73f,.66f);L(.9f,.5f,.73f,.34f);break;
                case "Siege": case "Fortified": case "IgnoreSiteFortification": L(.15f,.15f,.85f,.15f);L(.2f,.15f,.2f,.85f);L(.8f,.15f,.8f,.85f);L(.2f,.85f,.37f,.85f);L(.37f,.85f,.37f,.7f);L(.37f,.7f,.63f,.7f);L(.63f,.7f,.63f,.85f);L(.63f,.85f,.8f,.85f);if(mark=="IgnoreSiteFortification")L(.1f,.1f,.9f,.95f);if(mark=="Siege")L(.22f,.25f,.8f,.8f);break;
                case "Swift": L(.5f,.25f,.1f,.72f);L(.1f,.72f,.4f,.65f);L(.4f,.65f,.5f,.85f);L(.5f,.85f,.6f,.65f);L(.6f,.65f,.9f,.72f);L(.9f,.72f,.5f,.25f);break;
                case "Poison": Ring(.5f,.6f,.28f);Ring(.4f,.64f,.04f);Ring(.6f,.64f,.04f);L(.3f,.2f,.7f,.2f);L(.3f,.1f,.7f,.3f);L(.3f,.3f,.7f,.1f);break;
                case "Fire": Flame();break;
                case "Ice": Ice();break;
                case "ColdFire":
                    L(.29f,.9f,.12f,.46f);L(.12f,.46f,.28f,.19f);L(.28f,.19f,.44f,.46f);L(.44f,.46f,.36f,.65f);
                    L(.73f,.19f,.73f,.87f);L(.57f,.36f,.89f,.7f);L(.57f,.7f,.89f,.36f);break;
                case "FireResist": Shield();L(.49f,.75f,.35f,.46f);L(.35f,.46f,.5f,.3f);L(.5f,.3f,.65f,.46f);L(.65f,.46f,.58f,.64f);break;
                case "IceResist": Shield();L(.5f,.3f,.5f,.76f);L(.35f,.4f,.65f,.66f);L(.35f,.66f,.65f,.4f);break;
                case "ColdFireResist": Shield();L(.36f,.74f,.27f,.49f);L(.27f,.49f,.4f,.35f);L(.4f,.35f,.45f,.5f);L(.63f,.34f,.63f,.75f);L(.53f,.45f,.72f,.65f);L(.53f,.65f,.72f,.45f);break;
                case "MagicResist": Shield();L(.5f,.75f,.34f,.52f);L(.34f,.52f,.5f,.3f);L(.5f,.3f,.66f,.52f);L(.66f,.52f,.5f,.75f);break;
                case "PhysicalResist": Shield();L(.5f,.3f,.5f,.76f);L(.36f,.48f,.64f,.48f);break;
                case "Brutal": L(.5f,.1f,.5f,.9f);L(.15f,.65f,.85f,.65f);L(.15f,.65f,.2f,.9f);L(.85f,.65f,.8f,.9f);L(.2f,.9f,.8f,.9f);break;
                case "Assassination": L(.25f,.12f,.78f,.88f);L(.78f,.88f,.78f,.55f);L(.78f,.88f,.48f,.76f);L(.2f,.4f,.5f,.18f);break;
                case "Paralyze": Ring(.5f,.76f,.15f);L(.5f,.6f,.5f,.1f);L(.2f,.48f,.8f,.48f);L(.7f,.86f,.35f,.13f);break;
                case "Regenerate": case "Heal": L(.5f,.12f,.5f,.9f);L(.17f,.52f,.83f,.52f);Ring(.5f,.5f,.36f);break;
                case "Cumbersome": L(.15f,.18f,.85f,.18f);L(.15f,.18f,.3f,.72f);L(.3f,.72f,.7f,.72f);L(.7f,.72f,.85f,.18f);Ring(.5f,.82f,.09f);break;
                case "Influence": Ring(.5f,.5f,.32f);L(.26f,.5f,.74f,.5f);L(.5f,.26f,.5f,.74f);break;
                case "Reputation": L(.2f,.1f,.2f,.9f);L(.2f,.9f,.85f,.75f);L(.85f,.75f,.2f,.56f);break;
                case "Negate": L(.08f,.12f,.92f,.9f);break;
                case "Summon": Ring(.5f,.5f,.36f);L(.25f,.5f,.75f,.5f);L(.5f,.25f,.5f,.75f);break;
                case "Target": Ring(.5f,.5f,.27f);L(.5f,.06f,.5f,.3f);L(.5f,.7f,.5f,.94f);L(.06f,.5f,.3f,.5f);L(.7f,.5f,.94f,.5f);break;
                case "Committed": L(.2f,.14f,.82f,.88f);L(.15f,.4f,.4f,.15f);L(.78f,.12f,.78f,.5f);L(.62f,.3f,.78f,.12f);break;
                case "Missing": L(.2f,.14f,.44f,.43f);L(.57f,.58f,.82f,.88f);L(.15f,.4f,.4f,.15f);L(.28f,.76f,.46f,.76f);break;
                case "Reserve": L(.25f,.1f,.75f,.1f);L(.25f,.1f,.25f,.65f);L(.75f,.1f,.75f,.65f);L(.4f,.3f,.4f,.9f);L(.6f,.3f,.6f,.9f);break;
                case "Incoming": L(.15f,.85f,.85f,.85f);L(.5f,.85f,.5f,.1f);L(.5f,.1f,.25f,.36f);L(.5f,.1f,.75f,.36f);break;
                case "Plan": L(.2f,.8f,.8f,.8f);L(.2f,.5f,.8f,.5f);L(.2f,.2f,.8f,.2f);L(.1f,.8f,.12f,.8f);L(.1f,.5f,.12f,.5f);break;
                case "Free": Ring(.5f,.5f,.32f);L(.15f,.12f,.85f,.9f);break;
                case "Base": L(.2f,.15f,.8f,.15f);L(.8f,.15f,.8f,.85f);L(.8f,.85f,.2f,.85f);L(.2f,.85f,.2f,.15f);L(.35f,.5f,.65f,.5f);break;
                case "Strong": L(.2f,.2f,.8f,.2f);L(.8f,.2f,.8f,.9f);L(.8f,.9f,.2f,.9f);L(.2f,.9f,.2f,.2f);L(.35f,.5f,.5f,.7f);L(.5f,.7f,.65f,.5f);L(.5f,.32f,.5f,.7f);break;
                case "Sideways": L(.1f,.32f,.9f,.32f);L(.9f,.32f,.9f,.74f);L(.9f,.74f,.1f,.74f);L(.1f,.74f,.1f,.32f);L(.25f,.1f,.75f,.9f);break;
                case "Success": L(.15f,.5f,.4f,.2f);L(.4f,.2f,.88f,.84f);break;
                case "Alert": L(.5f,.94f,.08f,.12f);L(.08f,.12f,.92f,.12f);L(.92f,.12f,.5f,.94f);L(.5f,.7f,.5f,.4f);Ring(.5f,.25f,.02f);break;
                case "Attack": L(.2f,.14f,.82f,.88f);L(.2f,.88f,.82f,.14f);L(.12f,.4f,.4f,.12f);L(.6f,.12f,.88f,.4f);break;
                default: L(.1f,.2f,.5f,.1f);L(.5f,.1f,.9f,.2f);L(.1f,.2f,.1f,.85f);L(.9f,.2f,.9f,.85f);L(.1f,.85f,.5f,.74f);L(.5f,.74f,.9f,.85f);L(.5f,.1f,.5f,.74f);break;
            }
        }
    }

    // Small bronze-and-bone enamel mounts belong to the symbols/counts, not
    // explanatory prose. Opaque faces isolate the mark from every background.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleRuneMount : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            void Face(float inset,Color tint)
            {
                float l=r.xMin+inset,rr=r.xMax-inset,b=r.yMin+inset,t=r.yMax-inset,c=Mathf.Min(8,(t-b)*.22f);
                var p=new[]{new Vector2(l+c,b),new Vector2(rr-c,b),new Vector2(rr,b+c),new Vector2(rr,t-c),new Vector2(rr-c,t),new Vector2(l+c,t),new Vector2(l,t-c),new Vector2(l,b+c)};
                int k=mesh.currentVertCount;mesh.AddVert(r.center,tint,Vector2.zero);
                foreach(var point in p){var shade=Color.Lerp(tint*.88f,tint,Mathf.InverseLerp(b,t,point.y));shade.a=1;mesh.AddVert(point,shade,Vector2.zero);}
                for(int i=0;i<8;i++)mesh.AddTriangle(k,k+i+1,k+(i+1)%8+1);
            }
            Face(0,((Color)new Color32(53,42,27,255)).linear);
            Face(1.3f,((Color)new Color32(190,145,66,255)).linear);
            Face(3.2f,((Color)new Color32(234,219,178,255)).linear);
        }
    }
}
