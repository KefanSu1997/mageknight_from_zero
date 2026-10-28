using System;
using System.Linq;
using MK.Logic.Core;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _headbandDuelInput;
        private int _headbandDuelEnemy = -1, _headbandDuelAttack, _headbandDuelPage;

        private bool HeadbandDuelAttackAvailable(int enemy, int attack)
        {
            return _battle != null && enemy >= 0 && enemy < _battle.Enemies.Count
                && attack >= 0 && attack < _battle.Enemies[enemy].AttackCount
                && !_battle.Context.DefeatedEnemyIndices.Contains(enemy)
                && !_battle.Context.SkipAttackIndices.Contains(enemy)
                && !_battle.Context.IsAttackCancelled(enemy, attack) && !_battle.IsBlocked(enemy, attack);
        }

        private void DrawHeadbandDuelInput()
        {
            if (!_headbandDuelInput || _headbandSkill == null || _battle == null) return;
            var panel = W.Panel(_root, "HeadbandDuel", 0, 0, 1920, 1080, new Color(.025f, .065f, .06f, 1));
            panel.GetComponent<Image>().raycastTarget = true;
            W.Text(panel, "Title", "启迪头环 · 决斗", 40, 25, 1810, 65, 36, W.Gold);
            W.Text(panel, "Instruction", "两次效果共同作用于同一敌人的同一次攻击：格挡2，近战对该敌攻击2；若没有部队作用于该敌，击败额外名望2。公共技能仍留在供应。", 40, 105, 1810, 100, 25, W.Cream);
            var enemies = _battle.Enemies;
            _headbandDuelPage = Math.Min(_headbandDuelPage, Math.Max(0, (enemies.Count - 1) / 4));
            for (int slot = 0; slot < 4 && _headbandDuelPage * 4 + slot < enemies.Count; slot++)
            {
                int index = _headbandDuelPage * 4 + slot;
                var enemy = enemies[index];
                var art = worldEnemyCatalog.Concat(Encounter.enemies).FirstOrDefault(e => e.originalId == enemy.Id);
                var button = Button(panel, "Btn_Enemy_" + index, "", 40, 230 + slot * 130, 910, 120, () =>
                { _headbandDuelEnemy = index; _headbandDuelAttack = 0; }, _headbandDuelEnemy == index);
                if (art?.originalToken != null) Texture(button.transform, "OriginalToken", art.originalToken, 12, 10, 100, 100);
                W.Text(button.transform, "EnemyName", (art?.displayName ?? enemy.Id) + " · 护甲 " + enemy.Armor, 130, 24, 755, 70, 25, W.Cream);
                button.interactable = !_battle.Context.DefeatedEnemyIndices.Contains(index);
            }
            Texture(panel, "OriginalSkill", SkillReference(_headbandSkill), 1000, 230, 250, 365);
            W.Text(panel, "PrintedRule", _headbandSkill.PrintedRule, 1290, 230, 580, 465, 25, W.Cream);
            Button(panel, "Btn_Previous", "上一页", 40, 758, 440, 45, () => _headbandDuelPage--).interactable = _headbandDuelPage > 0;
            Button(panel, "Btn_Next", "下一页", 510, 758, 440, 45, () => _headbandDuelPage++).interactable = (_headbandDuelPage + 1) * 4 < enemies.Count;
            bool selected = _headbandDuelEnemy >= 0 && _headbandDuelEnemy < enemies.Count;
            if (selected)
                for (int i = 0; i < enemies[_headbandDuelEnemy].AttackCount; i++)
                {
                    int attackIndex = i;
                    var attack = enemies[_headbandDuelEnemy].GetAttack(i);
                    Button(panel, "Btn_Attack_" + i, "攻击" + (i + 1) + " · " + attack.Element + " " + attack.Value,
                        40 + i * 300, 825, 282, 53, () => _headbandDuelAttack = attackIndex, _headbandDuelAttack == i).interactable = HeadbandDuelAttackAvailable(_headbandDuelEnemy, i);
                }
            W.Text(panel, "Feedback", _feedback ?? "选择当前尚未处理的实际攻击；确认前不消耗头环。", 40, 900, 1830, 65, 23, W.Gold);
            Button(panel, "Btn_Back", "返回原头环", 40, 990, 880, 60, ClearHeadbandInput);
            Button(panel, "Btn_Confirm", "确认同敌同攻击的两次决斗效果", 960, 990, 880, 60, () =>
            {
                _journey.UseHeadbandDuel(_headbandPhysical, _headbandSkill, _battle.Enemies[_headbandDuelEnemy], _headbandDuelAttack);
                AfterHeadbandInput();
                _feedback = "头环已打出；决斗格挡与近战奖励绑定所选原敌。";
            }, true).interactable = selected && _battle.Phase == Phase.Block && HeadbandDuelAttackAvailable(_headbandDuelEnemy, _headbandDuelAttack);
        }
    }
}
