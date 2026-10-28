"""Auditable baseline calculations. Historic policy diagnostics are separate."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from structured_ui_ppo import ROOT, state, write_json
from measure_ui_fluency import panel

LABELS = {'sideways_block': '横置一张牌作格挡', 'restore_phase_checkpoint': '恢复当前阶段起点',
    'commit_hero_damage': '确认英雄承伤', 'attack_all_short': '全部贡献攻击：短路径',
    'attack_all_explicit': '全部贡献攻击：显式分配路径',
    'insufficient_attack_expected_rejection': '攻击不足的预期拒绝探测',
    'recover_attack_add_two_cards': '失败后补两张牌并重新攻击'}


def distribution(values):
    return {'n': len(values), 'p50': float(np.percentile(values, 50)),
            'p95': float(np.percentile(values, 95)), 'max': float(max(values))} if values else {'n': 0}


def interaction_fingerprint(observation):
    public = state(observation)
    rule = json.loads(public.get('rl.sharedRuleState', '{}'))
    payload = {'rule': rule, 'panel': panel(observation),
        'public_inspector': {k: public.get(k) for k in ('ui.cardMode', 'ui.Heading', 'ui.Details',
                                                      'ui.TargetPreview', 'ui.Feedback')},
        'hand_control_positions': [(c['path'], c['x'], c['y']) for c in observation['controls']
                                  if '/Hand/' in c['path']],
        'selected_choices': [c['path'] for c in observation['controls'] if '●' in c['text']]}
    return hashlib.sha256(json.dumps(payload, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


def historical_policy_diagnostics():
    paths = []
    for run in ('20261011/train_verified', '20261011/frozen_live',
                '20261011_frozen_text/train_live', '20261011_frozen_text/frozen_live',
                '20261011_frozen_text/frozen_deed_inputs'):
        paths += list((ROOT/'AutomationOutputs/StructuredUiPpo'/run).glob('episode_*/trajectory.json'))
    results = []
    for path in sorted(paths):
        data = json.loads(path.read_text(encoding='utf-8'))
        visited, same, revisits, rejections, selected, consumed = set(), 0, 0, 0, 0, 0
        for row in data['steps']:
            actual = row['actual']
            b, a = state(actual['before']), state(actual['observation'])
            before_hash = interaction_fingerprint(actual['before'])
            after_hash = interaction_fingerprint(actual['observation'])
            visited.add(before_hash)
            same += before_hash==after_hash
            revisits += after_hash in visited
            visited.add(after_hash)
            rejections += max(0, int(a.get('rejections', 0))-int(b.get('rejections', 0)))
            selected += row['selected']['name'].startswith('Btn_Card_')
            consumed += max(0, int(a.get('played', 0))-int(b.get('played', 0)))
        results.append({'source': str(path.relative_to(ROOT)), 'source_sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'clicks': len(data['steps']), 'unchanged_observation_proxy': same, 'revisit_proxy': revisits,
            'rule_rejections': rejections, 'card_selection_clicks': selected, 'actual_played_increments': consumed})
    return {'episodes': results, 'clicks': sum(r['clicks'] for r in results),
        'unchanged_observation_proxy': sum(r['unchanged_observation_proxy'] for r in results),
        'revisit_proxy': sum(r['revisit_proxy'] for r in results),
        'rule_rejections': sum(r['rule_rejections'] for r in results),
        'scope': 'historic weak-policy diagnostics, different UI/source versions; not current UI completion or human error rates'}


def analyze(root):
    root = Path(root).resolve()
    paths = sorted((root/'actual_ready').glob('trial_*/cases.json'))
    cases = [c for path in paths for c in json.loads(path.read_text(encoding='utf-8'))['cases']]
    groups = {}
    for identity, label in LABELS.items():
        items = [c for c in cases if c['intent_id']==identity]
        if not items:
            continue
        groups[identity] = {'label': label, 'attempts': len(items), 'completed': sum(c['completed'] for c in items),
            'clicks': distribution([c['metrics']['clicks'] for c in items]),
            'panel_transitions': distribution([c['metrics']['panel_transitions'] for c in items]),
            'pointer_distance_pixels': distribution([c['metrics']['pointer_distance_within_intent_pixels'] for c in items]),
            'handler_ms': distribution([v for c in items for v in c['metrics']['handler_ms']]),
            'first_rendered_frame_ms': distribution([v for c in items for v in c['metrics']['first_rendered_frame_ms']]),
            'post_frame_ready_probe_upper_bound_ms': distribution([v for c in items for v in c['metrics']['post_frame_block_ms']]),
            'next_control_timeouts': sum(c['metrics']['next_control_timeouts'] for c in items),
            'rule_rejections': sum(c['metrics']['rule_rejections'] for c in items),
            'raw_shared_control_movements': sum(len(c['metrics']['raw_shared_control_movements']) for c in items),
            'screenshots': [r['actual']['screenshotPath'] for c in items for r in c['steps'] if r['actual']['screenshotPath']]}
    normal = [c for c in cases if c['intent_id']!='insufficient_attack_expected_rejection']
    equivalents, target_resets, previews = [], [], []
    for path in paths:
        by_id = {c['intent_id']: c for c in json.loads(path.read_text(encoding='utf-8'))['cases']}
        short, explicit = by_id['attack_all_short'], by_id['attack_all_explicit']
        for side, index in (('before', 0), ('observation', -1)):
            a = json.loads(state(short['steps'][index]['actual'][side])['rl.sharedRuleState'])
            b = json.loads(state(explicit['steps'][index]['actual'][side])['rl.sharedRuleState'])
            equivalents.append({'trial': path.parent.name, 'side': side, 'shared_rule_snapshot_equal': a==b,
                'differences': [k for k in a if a[k]!=b.get(k)]})
        selection = by_id['recover_attack_add_two_cards']['steps'][0]['actual']
        a = json.loads(state(selection['before'])['rl.sharedRuleState'])
        b = json.loads(state(selection['observation'])['rl.sharedRuleState'])
        target_resets.append({'trial': path.parent.name, 'before': a['uiSelectedTargets'], 'after': b['uiSelectedTargets'],
                              'action': selection['click']['buttonPath']})
        for identity in ('attack_all_short', 'attack_all_explicit', 'insufficient_attack_expected_rejection'):
            row = by_id[identity]['steps'][-1]['actual']
            before, after = state(row['before']), state(row['observation'])
            text = ''.join(before.get('ui.TargetPreview', '').split())
            expected_attack = 1 if identity=='insufficient_attack_expected_rejection' else 3
            expected_win = expected_attack==3
            passed = ('护甲3' in text and f'有效攻击{expected_attack}' in text and
                      ('可击败所选目标' in text if expected_win else '还差2攻击' in text))
            passed = passed and after['kills']==('1' if expected_win else '0')
            passed = passed and after['melee']==('0' if expected_win else '1')
            previews.append({'trial': path.parent.name, 'intent': identity, 'public_preview': before.get('ui.TargetPreview'),
                'actual_kills': after['kills'], 'actual_melee': after['melee'], 'matched': passed})
    assert all(row['shared_rule_snapshot_equal'] for row in equivalents)
    assert all(row['matched'] for row in previews)
    no_change_by_intent = {identity: sum(interaction_fingerprint(r['actual']['before']) ==
        interaction_fingerprint(r['actual']['observation']) for c in cases if c['intent_id']==identity for r in c['steps'])
        for identity in LABELS}
    inspector_shifts = sum('/Inspector/' in move['path'] for c in cases
        for move in c['metrics']['raw_shared_control_movements'])
    baseline = {'status': 'measured', 'trials_with_cases': len(paths), 'declared_tasks': len(normal),
        'completed_tasks': sum(c['completed'] for c in normal),
        'intent_completion_rate': sum(c['completed'] for c in normal)/len(normal),
        'measured_task_clicks': sum(len(c['steps']) for c in cases),
        'handler_ms': distribution([v for c in cases for v in c['metrics']['handler_ms']]),
        'first_rendered_frame_ms': distribution([v for c in cases for v in c['metrics']['first_rendered_frame_ms']]),
        'next_control_probe': distribution([v for c in cases for v in c['metrics']['post_frame_block_ms']]),
        'intentional_rejection_probes': sum(c['intent_id']=='insufficient_attack_expected_rejection' for c in cases),
        'unexpected_rejections': sum(c['metrics']['rule_rejections'] for c in normal),
        'groups': groups, 'historical_policy': historical_policy_diagnostics(),
        'attack_path_equivalence': equivalents, 'recovery_target_resets': target_resets,
        'single_enemy_attack_preview_spotchecks': previews,
        'baseline_no_change_proxy_by_intent': no_change_by_intent,
        'persistent_inspector_control_shift_events': inspector_shifts,
        'unmeasured': ['human thinking/search/readability/control satisfaction', 'hardware input-to-display photon latency',
                       'full original-element UI entry coverage', 'systematic numeric outcome preview consistency'],
        'timing_scope': 'editor EventSystem dispatch to first completed render frame; no bridge/model/0.15s delay; load-dependent software proxy',
        'startup_failures_separate': ['actual/trial_00 observation timeout before any task',
                                     'actual_ready/trial_02 menu timeout; attachment records recovery if completed'],
        'method': {'clicks': 'count actual pointer success records within each declared intent',
            'panel_transitions': 'sum panel(before)!=panel(after); UI redraw alone does not count',
            'pointer_distance': 'sum Euclidean distance between successive actual click positions within intent; first approach unknown',
            'rule_rejections': 'sum positive after.rejections-before.rejections',
            'latency': 'Stopwatch immediately before pointer events to first WaitForEndOfFrame; no physical-display claim',
            'reference': 'actual verified path in current UI, not proven global minimum',
            'ready_probe': 'upper bound includes lookup/raycast cost; do not equate with animation blocking'}}
    write_json(root/'metrics.json', baseline)
    lines = ['# 当前基础 Demo 的操作基线', '', f"已测 {len(paths)} 组、{len(normal)} 个操作意图，完成 {baseline['completed_tasks']} 个。",
        '这些是固定脚本任务，不是模型胜率或真人满意度。每项点击都有实际命中和前后状态。', '',
        '| 操作 | 完成/尝试 | 点击 | 面板切换 | 意图内指针距离(px) | 首渲染帧P50/P95(ms) | 你的体验 |',
        '|---|---:|---:|---:|---:|---:|---|']
    for identity, row in groups.items():
        t = row['first_rendered_frame_ms']
        lines.append(f"| {row['label']} | {row['completed']}/{row['attempts']} | {row['clicks']['p50']:.0f} | {row['panel_transitions']['p50']:.0f} | {row['pointer_distance_pixels']['p50']:.0f} | {t['p50']:.1f}/{t['p95']:.1f} | 待你对照 |")
    t = baseline['first_rendered_frame_ms']
    h = baseline['handler_ms']
    lines += ['', f"全部任务点击 n={t['n']}，输入事件到首完成渲染帧 P50={t['p50']:.1f}ms、P95={t['p95']:.1f}ms、最大={t['max']:.1f}ms。",
        f"同步事件处理 P50={h['p50']:.1f}ms、P95={h['p95']:.1f}ms。P95为当前小样本描述，未作人群统计推断。",
        '', '## 计算口径', '',
        '- 完成率：满足该任务全部正式后置断言的次数/实际尝试次数。预期拒绝探测单列，启动失败也单列。',
        '- 点击：意图开始后成功送达实际控件的指针事件数；准备、重打及开局前缀不计入该意图。',
        '- 面板切换：Battle/AttackSelection等逻辑面板改变的次数；重新绘制同一面板不计。',
        '- 指针距离：意图内连续点击中心的距离之和；单位是1920×1080 Game View像素，不是手的实际时间。',
        '- 延迟：软件事件分发到首完成渲染帧。系统硬件显示延迟、模型推理和人为等待均不计入这个数。',
        '- 额外阻塞：这里仅探测预先指定的下一控件是否可点击。探测耗时不能直接当动画等待。',
        '', '## 初步优化方向', '',
        '1. 全部贡献攻击已经有2点击路径；显式分配路径是5点击和2次面板切换。若已经决定全用，可提示已有短路径；若要检查或改子集，额外步骤有用途。',
        '2. 单牌横置需跨手牌和右侧操作区；补两张牌再攻击的鼠标往返最明显。优先试就近操作或快捷键，并保留原输入语义。',
        '3. 失败后补牌仍需重新选目标。应评估保留兼容目标及明确失效原因，不能不分规则地保留所有旧输入。',
        '4. 承伤提交和贡献面板的慢帧值得剖析。当前是编辑器、同机其他任务未停止，不能仅凭这些数值断定渲染或规则层是根因。',
        '', '## 局部预览和旧策略诊断', '',
        f"单物理敌护甲3的攻击预览：{sum(r['matched'] for r in previews)}/{len(previews)}与实际结果一致。只覆盖该固定场景，不代表复杂费用、抗性或全部预览通过。",
        '三组中，选另一张牌都会将原已选敌人清空；失败后的攻击贡献保留。两种攻击路径的共享正式快照在操作前后均一致。',
        f"本次固定任务的无变化点击代理：{sum(no_change_by_intent.values())}次（按意图分组见metrics.json）；重复存在的Inspector控件位移事件{inspector_shifts}次。手牌消耗后的紧凑排列与选牌抬起属于有含义的移动，另存原值，不当布局缺陷。",
        '历史模型记录另算：171点击中27次没有观察变化、50次重访、4次规则拒绝。它们来自不同旧UI版本和弱策略，不能当普通玩家误操作率或当前UI缺陷率。',
        '', '## 和你的体验对照', '',
        '请在同类局面尝试横置一张牌、使用全部贡献攻击、打开贡献选择、攻击不足后补牌再攻击。关注：来回移动是否烦、默认贡献是否清楚、出错后是否要重选、提交承伤是否有明显停顿。表中最后一列可记录你的感受。',
        '你的总操作时间还包含阅读与战略思考；不会强行用机器人时间替代。字体可读性、理解难度、控制感和系统性预览一致性仍未测。',
        '', '## 实际截图', '']
    for identity in ('attack_all_explicit', 'insufficient_attack_expected_rejection', 'recover_attack_add_two_cards'):
        screenshot = groups[identity]['screenshots'][0]
        lines += [f"![{groups[identity]['label']}]({Path(screenshot).as_posix()})", '']
    (root/'baseline_report.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in baseline.items() if k in ('trials_with_cases','declared_tasks','completed_tasks',
        'measured_task_clicks','first_rendered_frame_ms','handler_ms','unexpected_rejections')}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--directory', required=True)
    analyze(parser.parse_args().directory)
