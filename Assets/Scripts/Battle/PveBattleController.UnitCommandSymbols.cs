using System;
using MK.Logic.Core;
using MK.Logic.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Summarize typed, unconditional combat contributions only. Complex
        // choice/target/boost abilities retain their complete native controls.
        private bool TryUnitCombatSummary(Button button, out string summary)
        {
            summary = null;
            const string prefix = "Btn_UnitAbility_";
            if (_selectedUnit == null || _leadershipSkill != null || MK.Logic.Runtime.DelphanaMastersActions.IsMasters(_selectedUnit)
                || !button.name.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(button.name.Substring(prefix.Length), out int index)
                || index < 0 || index >= _selectedUnit.Card.Actions.Length) return false;
            var a = _selectedUnit.Card.Actions[index];
            if (a.Kind != UnitAbilityKind.Attack && a.Kind != UnitAbilityKind.Block && a.Kind != UnitAbilityKind.AttackOrBlock)
                return false;
            if (a.RedBlueBoost || a.SelfWounds != 0 || a.ReputationChange != 0 || a.OtherUnitAttackBonus != 0
                || a.AttunementBonus != 0 || a.RemoveTargetFortifications || a.RemoveTargetResistances) return false;
            bool block = a.Kind == UnitAbilityKind.Block || a.Kind == UnitAbilityKind.AttackOrBlock && _battle.Phase == Phase.Block;
            string element = a.Element == Element.Fire ? "火焰" : a.Element == Element.Ice ? "寒冰" : a.Element == Element.ColdFire ? "冰火" : "";
            string action = block ? "格挡" : a.AttackType == AttackType.Ranged ? "远程" : a.AttackType == AttackType.Siege ? "攻城" : "攻击";
            string fee = a.Cost.Length == 0 ? "无魔力费" : string.Join("、", Array.ConvertAll(a.Cost, ColorName));
            int influence = MK.Logic.Runtime.CardUnitActions.ActivationInfluenceCost(_battle.Context, _selectedUnit);
            if (influence > 0) fee += " · 影响" + influence;
            summary = (button.IsInteractable() ? "发动 · " : "不可发动 · ") + element + action + " " + a.Value + "\n" + fee;
            if (a.DoubleAgainstSwift) summary += " · 迅捷×2";
            return true;
        }

        private void StyleUnitCombatSymbol(Button button, TMP_Text caption, float width, float height)
        {
            int index = int.Parse(button.name.Substring("Btn_UnitAbility_".Length));
            var ability = _selectedUnit.Card.Actions[index];
            bool block = ability.Kind == UnitAbilityKind.Block || ability.Kind == UnitAbilityKind.AttackOrBlock && _battle.Phase == Phase.Block;
            QuietGraphics(button.transform);
            var hit = button.GetComponent<Graphic>();
            if (hit != null) { hit.enabled = true; hit.color = Color.clear; button.targetGraphic = hit; }
            // A small game object seal identifies the action; no full prose plate.
            var seal = W.Rect(button.transform, "UnitActionSeal", 0, 3, 50, 50).gameObject.AddComponent<BattleCardCommandGlyph>();
            seal.color = button.interactable ? new Color32(136, 104, 49, 255) : new Color32(69, 76, 66, 255);
            seal.crystal = false; seal.raycastTarget = false;
            var icon = W.Rect(button.transform, "UnitActionGlyph", 8, 9, 34, 34);
            if (block)
            {
                var g = icon.gameObject.AddComponent<BattlePartyStateGlyph>(); g.kind = 5; g.color = W.Cream; g.raycastTarget = false;
            }
            else
            {
                var g = icon.gameObject.AddComponent<BattleThreatSwords>(); g.color = W.Cream; g.raycastTarget = false;
            }
            if (!button.IsInteractable())
            {
                var locked = W.Rect(button.transform, "UnitActionLocked", 29, 27, 24, 24).gameObject.AddComponent<BattlePartyStateGlyph>();
                locked.kind = 6; locked.color = W.Cream; locked.raycastTarget = false;
            }
            caption.fontSize = 22; caption.alignment = TextAlignmentOptions.MidlineLeft;
            // Disabledness is shown by the closed seal, muted underline and actual
            // restriction reason; the rules themselves must remain readable.
            caption.color = W.Cream;
            caption.outlineColor = new Color32(11, 23, 20, 255);
            caption.outlineWidth = .18f;
            Place(caption.transform, 60, 0, width - 65, height);
            var line = W.Panel(button.transform, "UnitActionUnderline", 60, height - 2, width - 70, 1,
                button.interactable ? W.Gold : W.Muted);
            line.GetComponent<Graphic>().raycastTarget = false;
            var focus = W.Rect(button.transform, "UnitActionHover", 0, 0, 52, 55).gameObject.AddComponent<BattleRoundGraphic>();
            focus.hollow = true; focus.color = new Color(.85f, .68f, .3f, 0); focus.raycastTarget = false;
            if (button.IsInteractable()) button.gameObject.AddComponent<BattleCommandFocus>().glow = focus;
        }
    }

    public sealed class BattleUnitCommandEvidence : MonoBehaviour
    {
        public string fullCaption;
    }
}
