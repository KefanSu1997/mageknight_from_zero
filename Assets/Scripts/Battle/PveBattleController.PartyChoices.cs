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
        private float _skillChoiceScroll=1, _unitChoiceScroll=1;
        private int PartyChoiceColumns => _showUnits && optimizedInteractionUi && _battle.Phase == Phase.AssignDamage
            && _battle.Player.Units.Count > 0 && _battle.Player.Units.Count <= 4 ? 2 : _showUnits ? 4 : 3;
        private RectTransform PartyChoiceBand(string name, int count, float rowHeight=260)
        {
            var row = W.Rect(_root, name, 36, 768, 1320, 275);
            // One reading surface for the whole tray, rather than a card plate
            // for each object. Actor illustrations remain on the battlefield.
            var shade = W.Rect(row, "ChoiceBandShade", -10, -8, 1300, 252).gameObject.AddComponent<BattleLabelBacking>();
            shade.color = new Color(.012f,.022f,.022f,.88f); shade.raycastTarget = false;
            int columns=PartyChoiceColumns;
            if (count <= columns && rowHeight<=260) return row;
            var viewport = W.Rect(row, "ChoiceViewport", 0, 0, columns==2?810:1290, 260);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = W.Rect(viewport, "ChoiceContent", 0, 0, columns==2?800:1280, Mathf.Ceil(count / (float)columns) * rowHeight);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;
            scroll.horizontal=false;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            bool skills=name=="Skills";
            scroll.verticalNormalizedPosition=skills?_skillChoiceScroll:_unitChoiceScroll;
            scroll.onValueChanged.AddListener(position=>
            {if(skills)_skillChoiceScroll=position.y;else _unitChoiceScroll=position.y;});
            // Hover scrolling belongs to the full-rule reader. Give the tray
            // its own explicit navigation so neither reader steals the other.
            void Arrow(string id,string label,float y,float direction)
            {
                var button=Button(row,id,label,-48,y,42,44,()=>
                {
                    float position=Mathf.Clamp01((skills?_skillChoiceScroll:_unitChoiceScroll)+direction*.75f);
                    if(skills)_skillChoiceScroll=position;else _unitChoiceScroll=position;
                    scroll.verticalNormalizedPosition=position;
                });
                QuietGraphics(button.transform);var hit=button.GetComponent<Graphic>();hit.enabled=true;hit.color=Color.clear;button.targetGraphic=hit;
                var disk=W.Rect(button.transform,"NavigationDisk",0,0,42,44).gameObject.AddComponent<BattleRoundGraphic>();
                disk.color=new Color32(32,51,43,255);disk.raycastTarget=false;
                var text=button.GetComponentInChildren<TMP_Text>();text.enabled=true;text.text=label;text.fontSize=30;text.color=W.Cream;
                Place(text.transform,0,0,42,44);text.transform.SetAsLastSibling();
            }
            Arrow("Btn_ChoiceScrollUp","↑",100,1);Arrow("Btn_ChoiceScrollDown","↓",154,-1);
            return content;
        }

        private void StylePartyChoice(Button button, string name, string status, bool selected, BattleUtilityGlyph.Kind kind)
        {
            QuietGraphics(button.transform);
            var hit = button.GetComponent<Graphic>();hit.enabled=true;hit.color=Color.clear;button.targetGraphic=hit;
            var seal = W.Rect(button.transform,"ChoiceMedallion",112,0,68,68).gameObject.AddComponent<BattleRoundGraphic>();
            seal.color=selected ? W.Gold : new Color32(47,68,59,255);seal.raycastTarget=false;
            var glyph = W.Rect(button.transform,"ChoiceGlyph",130,18,32,32).gameObject.AddComponent<BattleUtilityGlyph>();
            glyph.kind=kind;glyph.color=selected?W.Ink:W.Cream;glyph.raycastTarget=false;
            if (selected)
            {
                var ring=W.Rect(button.transform,"ChoiceSelected",106,-6,80,80).gameObject.AddComponent<BattleRoundGraphic>();
                ring.hollow=true;ring.color=W.Gold;ring.raycastTarget=false;
            }
            var identity=W.Text(button.transform,"ChoiceIdentity",name,4,92,284,64,26,W.Cream,TextAlignmentOptions.Center);
            identity.enableAutoSizing=false;identity.textWrappingMode=TextWrappingModes.Normal;
            var usage=W.Text(button.transform,"ChoiceUsage",status,4,160,284,100,26,W.Cream,TextAlignmentOptions.Top);
            usage.enableAutoSizing=false;usage.textWrappingMode=TextWrappingModes.Normal;
        }

        private static string SkillFrequencyLabel(SkillCard skill) => skill.Frequency switch
        {
            SkillFrequency.OncePerTurn => "每回合一次",
            SkillFrequency.OncePerRound => "每轮一次",
            SkillFrequency.ManualFlip => "手动翻面",
            _ => "次数见全文"
        };

        private static Texture2D SkillIcon(SkillCard skill) => skill == null ? null
            : Resources.Load<Texture2D>("UI/SkillIcons/redrawn_v1/" + skill.SourceId.Replace(':', '_'));
        [Serializable] private sealed class SkillSummaryEntry
        {
            public string sourceId, originalRule, summary;
        }
        [Serializable] private sealed class SkillSummaryCatalog
        {
            public SkillSummaryEntry[] entries=Array.Empty<SkillSummaryEntry>();
        }
        private SkillSummaryCatalog _skillDecisionSummaries;
        private string SkillDecisionSummary(SkillCard skill)
        {
            if(_skillDecisionSummaries==null)
            {
                var source=Resources.Load<TextAsset>("UI/SkillDecisionSummaries");
                _skillDecisionSummaries=source!=null?JsonUtility.FromJson<SkillSummaryCatalog>(source.text):new SkillSummaryCatalog();
            }
            var entry=_skillDecisionSummaries.entries.FirstOrDefault(e=>e.sourceId==skill.SourceId && e.originalRule==skill.PrintedRule);
            // A different current face or rule revision must never inherit an
            // outdated summary. Preserve its actual complete rule as fallback.
            return entry!=null && !string.IsNullOrWhiteSpace(entry.summary)?entry.summary:skill.PrintedRule;
        }
        private string SkillCompactUsage(SkillCard skill) => _battle.Player.FlippedSkills.Contains(skill.SourceId)
            ? "已翻面" : SkillActions.IsUsed(_battle.Player,skill)
            ? (skill.Frequency==SkillFrequency.OncePerRound?"本轮已用":"本回合已用") : "尚未使用";

        private void DrawEmbeddedSkillChoices()
        {
            int skillRows=Mathf.CeilToInt(_battle.Player.Skills.Count/3f);
            var header=W.Text(_root,"SkillLabel",$"{_battle.Player.Name} · 等级 {_battle.Player.Level} · 已学技能 {_battle.Player.Skills.Count}"
                +(skillRows>1?$" · 共 {skillRows} 排，左侧箭头切换":"")+"  ·  悬浮技能查看完整规则与费用",39,718,1320,36,22,W.Gold);
            float rowHeight=260;
            foreach(var skill in _battle.Player.Skills)
            {
                header.fontSize=26;
                float heading=Mathf.Max(44,header.GetPreferredValues(skill.Id,282,0).y+8);
                float status=Mathf.Max(44,header.GetPreferredValues(SkillCompactUsage(skill),282,0).y+8);
                header.fontSize=22;
                float effect=header.GetPreferredValues(SkillDecisionSummary(skill),402,0).y+8;
                rowHeight=Mathf.Max(rowHeight,Mathf.Max(148,heading+8+status+12)+effect+16);
            }
            header.fontSize=22;
            var row=PartyChoiceBand("Skills",_battle.Player.Skills.Count,rowHeight);
            for(int i=0;i<_battle.Player.Skills.Count;i++)
            {
                var skill=_battle.Player.Skills[i];
                int skillRow=i/3;
                var button=Button(row,"Btn_Skill_"+i,"",(i%3)*426,(i/3)*rowHeight,418,rowHeight,()=>
                {
                    // Keep the selected row in view after redraw. Intermediate
                    // scroll positions must not leave a newly selected skill
                    // half outside the tray.
                    _skillChoiceScroll=skillRows>1?1-skillRow/(float)(skillRows-1):1;
                    if(skill.IsInteractive==true && _journey!=null){OpenSharedSkills(skill);return;}
                    _recovery=false;_selectedSkill=skill;_skillOption=0;_selected=null;_selectedUnit=null;
                });
                QuietGraphics(button.transform);
                var hit=button.GetComponent<Graphic>();hit.enabled=true;hit.color=Color.clear;button.targetGraphic=hit;
                var art=SkillIcon(skill);
                if(art!=null)
                {
                    // Original, individually drawn ability art. Full UV keeps
                    // every edge; printed rules remain in the existing reader.
                    float fit=Mathf.Min(108f/art.width,96f/art.height);
                    float iw=art.width*fit,ih=art.height*fit;
                    var icon=W.Rect(button.transform,"RedrawnSkillIcon",4+(108-iw)/2,4+(96-ih)/2,iw,ih).gameObject.AddComponent<RawImage>();
                    icon.texture=art;icon.uvRect=new Rect(0,0,1,1);icon.raycastTarget=false;
                }
                else
                {
                    var seal=W.Rect(button.transform,"SkillFallbackSeal",16,8,88,88).gameObject.AddComponent<BattleRoundGraphic>();
                    seal.color=new Color32(47,68,59,255);seal.raycastTarget=false;
                    var glyph=W.Rect(button.transform,"SkillFallbackGlyph",34,26,52,52).gameObject.AddComponent<BattleUtilityGlyph>();
                    glyph.kind=BattleUtilityGlyph.Kind.Skill;glyph.color=W.Cream;glyph.raycastTarget=false;
                }
                W.Text(button.transform,"ChoiceFrequency",SkillFrequencyLabel(skill),4,108,116,34,22,W.Gold,TextAlignmentOptions.Top);
                var identity=W.Text(button.transform,"ChoiceIdentity",skill.Id,128,0,282,64,26,skill==_selectedSkill?W.Gold:W.Cream);
                float heading=Mathf.Max(44,identity.GetPreferredValues(skill.Id,282,0).y+8);
                Place(identity.transform,128,0,282,heading);
                var usage=W.Text(button.transform,"ChoiceUsage",SkillCompactUsage(skill),128,heading+8,282,70,26,W.Cream);
                float status=Mathf.Max(44,usage.GetPreferredValues(usage.text,282,0).y+8);
                Place(usage.transform,128,heading+8,282,status);
                var summary=W.Text(button.transform,"ChoiceEffectSummary",SkillDecisionSummary(skill),4,Mathf.Max(148,heading+8+status+12),402,150,22,W.Cream);
                summary.enableAutoSizing=false;
                summary.rectTransform.sizeDelta=new Vector2(402,summary.GetPreferredValues(summary.text,402,0).y+8);
                if(skill==_selectedSkill)
                {
                    var underline=W.Panel(button.transform,"SkillSelectedUnderline",4,rowHeight-5,402,3,W.Gold);
                    underline.GetComponent<Graphic>().raycastTarget=false;
                }
                HoverInfo(button.gameObject,skill.Id,SkillUsage(skill)+"\n\n"+skill.PrintedRule,true);
                button.GetComponent<BattleHoverInfo>().bodyFontSize=22;
            }
        }

        private void DrawEmbeddedUnitChoices()
        {
            W.Text(_root,"UnitLabel",$"部队 {_battle.Player.Units.Count} / 指挥槽 {_battle.Player.CommandSlots}  ·  激活后耗竭，承伤资格独立  ·  悬浮角色查看能力与费用",39,718,1320,36,22,W.Gold);
            var row=PartyChoiceBand("Units",_battle.Player.Units.Count);
            for(int i=0;i<_battle.Player.Units.Count;i++)
            {
                var unit=_battle.Player.Units[i];
                int columns=PartyChoiceColumns;
                var button=Button(row,"Btn_Unit_"+i,"",(i%columns)*320,(i/columns)*260,294,260,()=>
                {_recovery=false;_selectedUnit=unit;_selected=null;_choice=null;_bannerMode=false;_unitUtilityPanel=false;});
                StylePartyChoice(button,unit.Card.NameCn,string.Join("\n",UnitText(unit).Split('\n').Take(2)),unit==_selectedUnit,BattleUtilityGlyph.Kind.Party);
                button.transform.Find("ChoiceMedallion").gameObject.SetActive(false);
                button.transform.Find("ChoiceGlyph").gameObject.SetActive(false);
                var art=Resources.Load<Texture2D>("UI/UnitCharacters/embedded_v2/"+unit.Card.Id)
                    ?? Resources.Load<Texture2D>("UI/UnitCharacters/embedded_v1/"+unit.Card.Id);
                if(art!=null)
                {
                    float fit=Mathf.Min(100f/art.width,88f/art.height);
                    Texture(button.transform,"ChoiceUnitFigure",art,(294-art.width*fit)/2,0,art.width*fit,art.height*fit,Color.white);
                }
                string actions=string.Join("\n\n",unit.Card.Actions.Select(action=>action.PrintedRule+"\n印刷费用："+
                    (action.Cost.Length==0?"免费":string.Join("、",action.Cost.Select(ColorName)))));
                HoverInfo(button.gameObject,unit.Card.NameCn,UnitText(unit)+"\n\n原能力（印刷费用）：\n"+actions,true);
                button.GetComponent<BattleHoverInfo>().bodyFontSize=22;
            }
            if(_battle.Player.Units.Count==0)W.Text(row,"NoUnits","此出战配置没有部队。",10,90,1270,60,25,W.Cream);
        }
    }
}
