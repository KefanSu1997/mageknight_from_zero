using System;
using System.Collections.Generic;
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
        // Keep physical contributions, not record equality: two identical sideways cards are distinct.
        private readonly List<AttackProfile> _reservedAttacks = new();
        private readonly List<BlockAllocation> _reservedBlocks = new();
        private bool _blockSelectionOpen;
        private int _blockSelectionPage;
        private bool _attackSelectionOpen;
        private int _attackSelectionPage;
        private int _handPage;
        private const int HandPageSize = 7;
        private readonly List<(AttackProfile Part, string Label)> _skillAttackLabels = new();

        private void UseBattleSkill(SkillCard skill, int option, int target, DeedCard wound, int attackIndex)
        {
            var before = _battle.Context.CombatPower.Attacks.ToArray();
            _battle.UseSkill(skill, option, target, wound, attackIndex);
            // Display provenance only. The actual pool remains the rule engine's source of truth.
            foreach (var part in _battle.Context.CombatPower.Attacks.Where(a => !before.Any(b => ReferenceEquals(a, b))))
                _skillAttackLabels.Add((part, skill.Id));
        }

        private AttackProfile[] SelectedAttackParts(int[] targets) => _battle.Context.CombatPower
            .AvailableAttacks(_battle.Phase, targets, _battle.Enemies, _battle.Context.AllAttacksSiege)
            .Where(a => !_reservedAttacks.Any(r => ReferenceEquals(r, a))).ToArray();

        private string AttackSource(AttackProfile part) => part.SourceCard is DeedCard card ? CardName(card)
            : part.SourceCard is SpellCard spell ? deedCatalog.FirstOrDefault(d => d.id == spell.Id)?.source.NameCn ?? spell.Id
            : part.SourceSkill?.Id ?? part.SourceUnit?.Card.NameCn
                ?? _skillAttackLabels.FirstOrDefault(x => ReferenceEquals(x.Part, part)).Label ?? "效果产生的攻击";

        private void ClearAttackSelection()
        {
            _reservedAttacks.Clear(); _attackSelectionOpen = false; _attackSelectionPage = 0;
            _reservedBlocks.Clear(); _blockSelectionOpen = false; _blockSelectionPage = 0;
            _skillAttackLabels.RemoveAll(x => _battle == null || !_battle.Context.CombatPower.Attacks.Any(a => ReferenceEquals(a, x.Part)));
        }

        private BlockAllocation[] SelectedBlockParts()
        {
            if (_targets.Count != 1) return Array.Empty<BlockAllocation>();
            int target = _targets.Single();
            return _battle.Context.CombatPower.AvailableBlocks(target, _attackSlot, _battle.Enemies[target])
                .Where(b => !_reservedBlocks.Any(r => ReferenceEquals(r, b))).ToArray();
        }

        private string BlockSource(BlockAllocation part) => part.SourceCard is DeedCard card ? CardName(card)
            : part.SourceCard is SpellCard spell ? deedCatalog.FirstOrDefault(d => d.id == spell.Id)?.source.NameCn ?? spell.Id
            : part.SourceSkill?.Id ?? part.SourceUnit?.Card.NameCn ?? "效果产生的格挡";

        private void DrawBlockSelection()
        {
            if (_battle.Phase != Phase.Block || _targets.Count != 1) { _blockSelectionOpen = false; return; }
            int target = _targets.Single();
            var all = _battle.Context.CombatPower.AvailableBlocks(target, _attackSlot, _battle.Enemies[target]);
            _blockSelectionPage = Math.Min(_blockSelectionPage, Math.Max(0, (all.Length - 1) / 6));
            var panel = W.Panel(_root, "BlockSelection", 220, 110, 1480, 870, W.Ink, true);
            var shield = W.Panel(panel, "InputShield", -220, -110, 1920, 1080, new Color(0, 0, 0, .72f));
            shield.GetComponent<Image>().raycastTarget = true; shield.SetAsFirstSibling();
            W.Panel(panel, "Surface", 1, 1, 1478, 868, new Color32(12, 29, 28, 255));
            W.Text(panel, "Title", "分配本次格挡", 32, 22, 1400, 55, 34, W.Gold);
            W.Text(panel, "Instruction", "点选来源：金色用于本次，未选格挡保留给另一笔攻击。每笔不可拆分；不足时不能减伤。", 32, 85, 1400, 65, 23, W.Cream);
            for (int slot = 0; slot < 6 && _blockSelectionPage * 6 + slot < all.Length; slot++)
            {
                int index = _blockSelectionPage * 6 + slot; var part = all[index];
                bool selected = !_reservedBlocks.Any(b => ReferenceEquals(b, part));
                var button = Button(panel, "Btn_Part_" + index, "", 32 + slot % 2 * 715, 160 + slot / 2 * 132, 695, 115, () =>
                {
                    int found = _reservedBlocks.FindIndex(b => ReferenceEquals(b, part));
                    if (found < 0) _reservedBlocks.Add(part); else _reservedBlocks.RemoveAt(found);
                }, selected);
                W.Text(button.transform, "Source", (selected ? "本次 · " : "保留 · ") + BlockSource(part), 20, 12, 650, 38, 25, W.Cream);
                W.Text(button.transform, "Power", $"{ElementLabel(part.Element)} · 格挡 {part.Value}" + (selected ? "  · 本次使用" : "  · 保留"), 20, 58, 650, 40, 22, W.Muted);
            }
            W.Text(panel, "Preview", TargetPreview(), 32, 575, 1400, 100, 27, W.Gold);
            if (all.Length > 6)
            {
                Button(panel, "Btn_Previous", "上一页", 32, 688, 260, 48, () => _blockSelectionPage--).interactable = _blockSelectionPage > 0;
                Button(panel, "Btn_Next", "下一页", 315, 688, 260, 48, () => _blockSelectionPage++).interactable = (_blockSelectionPage + 1) * 6 < all.Length;
            }
            Button(panel, "Btn_All", "全部用于本次", 620, 688, 380, 48, () => _reservedBlocks.Clear());
            Button(panel, "Btn_None", "全部保留", 1020, 688, 428, 48, () => { _reservedBlocks.Clear(); _reservedBlocks.AddRange(all); });
            Button(panel, "Btn_Done", "完成分配，返回战场", 32, 780, 1416, 58, () => _blockSelectionOpen = false, true);
        }

        private void DrawAttackSelection()
        {
            var targets = _targets.OrderBy(i => i).ToArray();
            var all = _battle.Context.CombatPower.AvailableAttacks(_battle.Phase, _battle.Context.AllAttacksSiege);
            var eligible = _battle.Context.CombatPower.AvailableAttacks(_battle.Phase, targets, _battle.Enemies, _battle.Context.AllAttacksSiege);
            _attackSelectionPage = Math.Min(_attackSelectionPage, Math.Max(0, (all.Length - 1) / 6));
            var panel = W.Panel(_root, "AttackSelection", 220, 110, 1480, 870, W.Ink, true);
            // A full-screen shield prevents clicks through the selection surface.
            var shield = W.Panel(panel, "InputShield", -220, -110, 1920, 1080, new Color(0, 0, 0, .72f));
            shield.GetComponent<Image>().raycastTarget = true; shield.SetAsFirstSibling();
            W.Panel(panel, "Surface", 1, 1, 1478, 868, new Color32(12, 29, 28, 255));
            W.Text(panel, "Title", "分配本次攻击", 32, 22, 1400, 55, 34, W.Gold);
            W.Text(panel, "Instruction", "点选来源：金色用于本次，未选攻击保留给另一只敌人。每笔不可拆分；超出护甲的点数不会返还。", 32, 85, 1400, 65, 23, W.Cream);
            for (int slot = 0; slot < 6 && _attackSelectionPage * 6 + slot < all.Length; slot++)
            {
                int index = _attackSelectionPage * 6 + slot; var part = all[index];
                bool legal = eligible.Any(a => ReferenceEquals(a, part));
                bool selected = legal && !_reservedAttacks.Any(a => ReferenceEquals(a, part));
                var button = Button(panel, "Btn_Part_" + index, "", 32 + slot % 2 * 715, 160 + slot / 2 * 132, 695, 115, () =>
                {
                    int found = _reservedAttacks.FindIndex(a => ReferenceEquals(a, part));
                    if (found < 0) _reservedAttacks.Add(part); else _reservedAttacks.RemoveAt(found);
                }, selected);
                button.interactable = legal;
                W.Text(button.transform, "Source", (selected ? "本次 · " : "保留 · ") + AttackSource(part), 20, 12, 650, 38, 25, W.Cream);
                W.Text(button.transform, "Power", $"{ElementLabel(part.Element)} · {(part.Type == AttackType.Melee ? "攻击" : part.Type == AttackType.Ranged ? "远程" : "攻城")} {part.Value}" + (legal ? selected ? "  · 本次使用" : "  · 保留" : "  · 不适用于所选目标"), 20, 58, 650, 40, 22, W.Muted);
            }
            if (all.Length == 0) W.Text(panel, "Empty", "尚无攻击。先打出卡牌、横置手牌或发动技能。", 32, 180, 1400, 90, 27, W.Cream);
            W.Text(panel, "Preview", TargetPreview(), 32, 575, 1400, 100, 27, W.Gold);
            if (all.Length > 6)
            {
                Button(panel, "Btn_Previous", "上一页", 32, 688, 260, 48, () => _attackSelectionPage--).interactable = _attackSelectionPage > 0;
                Button(panel, "Btn_Next", "下一页", 315, 688, 260, 48, () => _attackSelectionPage++).interactable = (_attackSelectionPage + 1) * 6 < all.Length;
            }
            Button(panel, "Btn_All", "全部用于本次", 620, 688, 380, 48, () => _reservedAttacks.Clear());
            Button(panel, "Btn_None", "全部保留", 1020, 688, 428, 48, () => { _reservedAttacks.Clear(); _reservedAttacks.AddRange(all); });
            Button(panel, "Btn_Done", "完成分配，返回战场", 32, 780, 1416, 58, () => _attackSelectionOpen = false, true);
        }
    }
}
