using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using W = MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private bool _enemyDefendOpen;
        private int[] _enemyDefendTargets = Array.Empty<int>();
        private int _enemyDefendSource = -1;
        private readonly List<string> _enemyDefendPlanLabels = new();

        private string DefendEnemyName(int index) => "#" + (index + 1) + " " +
            EnemyView(_battle.Enemies[index].Id).displayName;

        private void ResolveAttackWithDefend()
        {
            var targets = _targets.OrderBy(i => i).ToArray();
            if (SelectedAttackParts(targets).Length == 0)
                throw new InvalidOperationException("请先打出并选择本次实际攻击来源，再分配敌人防护。");
            if (_battle.DefendChoices(targets).Count > 0)
            {
                _enemyDefendTargets = targets;
                _enemyDefendSource = -1;
                _enemyDefendPlanLabels.Clear();
                _enemyDefendOpen = true;
                return;
            }
            _battle.AttackSelected(targets, SelectedAttackParts(targets));
            ClearAttackSelection();
            _targets.Clear();
        }

        private void DrawEnemyDefend()
        {
            var panel = W.Panel(_root, "EnemyDefend", 0, 0, 1920, 1080, new Color(.025f,.065f,.06f,1));
            panel.GetComponent<Image>().raycastTarget = true;
            W.Text(panel, "Title", "敌人防护 · 选择本次受保护目标", 40, 25, 1810, 65, 36, W.Gold);
            W.Text(panel, "Rule", "防护在首次攻击时增加目标护甲，并持续本场战斗。每个来源只能使用一次；不同来源保护不同目标。\n选择只作预览，确认攻击才应用并消费攻击贡献；取消可返回调整出牌。", 40, 105, 1810, 110, 25, W.Cream);
            var choices = _battle.DefendChoices(_enemyDefendTargets);
            var sources = choices.Select(c => c.SourceIndex).Distinct().ToArray();
            if (sources.Length == 1) _enemyDefendSource = sources[0];
            if (!sources.Contains(_enemyDefendSource)) _enemyDefendSource = -1;
            W.Text(panel, "SourceTitle", "提供防护的实际敌人", 40, 235, 850, 45, 27, W.Gold);
            for (int i = 0; i < sources.Length; i++)
            {
                int source = sources[i];
                int value = choices.First(c => c.SourceIndex == source).Value;
                var button = Button(panel, "Btn_Source_" + source, DefendEnemyName(source) + " · 防护 " + value,
                    40, 300 + i * 150, 850, 125, () => _enemyDefendSource = source, source == _enemyDefendSource);
                var art = EnemyView(_battle.Enemies[source].Id);
                Texture(button.transform, "OriginalToken", art.originalToken, 15, 12, 100, 100);
            }
            W.Text(panel, "TargetTitle", "此次攻击中的受保护目标", 990, 235, 850, 45, 27, W.Gold);
            var targets = choices.Where(c => c.SourceIndex == _enemyDefendSource).Select(c => c.TargetIndex).Distinct().ToArray();
            for (int i = 0; i < targets.Length; i++)
            {
                int target = targets[i];
                int source = _enemyDefendSource;
                var button = Button(panel, "Btn_Target_" + target, DefendEnemyName(target),
                    990, 300 + i * 150, 850, 125, () =>
                    {
                        int value = choices.First(c => c.SourceIndex == source && c.TargetIndex == target).Value;
                        _battle.ChooseDefend(source, target, _enemyDefendTargets);
                        _enemyDefendPlanLabels.Add(DefendEnemyName(source) + " → " + DefendEnemyName(target) + " 护甲 +" + value);
                    });
                var art = EnemyView(_battle.Enemies[target].Id);
                Texture(button.transform, "OriginalToken", art.originalToken, 15, 12, 100, 100);
            }
            W.Text(panel, "SelectionState", string.Join("\n", _enemyDefendPlanLabels) + "\n" + (choices.Count > 0 ? "请选择防护来源和目标，再确认攻击。" : "分配已确定；本次护甲合计 " + _battle.AttackArmorPreview(_enemyDefendTargets)), 40, 760, 1800, 120, 24, W.Cream);
            W.Text(panel, "Feedback", _feedback ?? "同名敌人按编号区分，选择绑定实际敌人实例。", 40, 885, 1800, 65, 24, W.Cream);
            Button(panel, "Btn_Cancel", "取消选择，返回出牌", 40, 990, 880, 60, () =>
            {
                _battle.ClearDefendChoices();
                _enemyDefendOpen = false;
            });
            Button(panel, "Btn_Confirm", "确认分配并结算攻击", 960, 990, 880, 60, () =>
            {
                _battle.AttackSelected(_enemyDefendTargets, SelectedAttackParts(_enemyDefendTargets));
                ClearAttackSelection();
                _enemyDefendOpen = false;
                _enemyDefendTargets = Array.Empty<int>();
                _targets.Clear();
            }, true).interactable = choices.Count == 0;
        }
    }
}
