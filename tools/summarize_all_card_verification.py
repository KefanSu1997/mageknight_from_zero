"""Audit actual Unity reports without turning UI completion into rule acceptance."""
from collections import Counter
from pathlib import Path
from datetime import datetime
import json
import argparse
import hashlib

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'AutomationOutputs/AllOriginalCards/20260910'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    global OUTPUT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT, help='Exact scene run directory; never combine separate runs.')
    parser.add_argument('--sources', type=Path, default=OUTPUT / 'source_inventory.json')
    args = parser.parse_args()
    OUTPUT = args.output.resolve()
    planned = read(ROOT / 'Assets/Resources/CardVerification/cases.json')['cases']
    suite_hash = hashlib.sha256((ROOT / 'Assets/Resources/CardVerification/cases.json').read_text(encoding='utf-8-sig').encode('utf-8')).hexdigest()
    summary = {'status': 'incomplete', 'scope': '125 existing card records (121 readable faces); staged scene verification, not complete rules certification', 'batches': [], 'cards': []}
    all_cases = []
    total_ops = total_asserts = total_shots = 0
    for batch in ['basic_card', 'advanced_card', 'magic', 'items']:
        directory = OUTPUT / batch
        if not (directory / 'effects.json').exists() or not (directory / 'report.json').exists():
            summary['batches'].append({'batch': batch, 'status': 'not_run'})
            continue
        effects = read(directory / 'effects.json')
        assert effects.get('suiteSha256') == suite_hash, f'{batch}: Unity used a stale or unidentified case suite'
        ui = read(directory / 'report.json')
        effect_start = datetime.fromisoformat(effects['startedAt'].replace('Z', '+00:00'))
        ui_start = datetime.fromisoformat(ui['startedAt'].replace('Z', '+00:00'))
        assert abs((effect_start - ui_start).total_seconds()) < 20, f'{batch}: stale effect/UI report pair'
        expected = [c for c in planned if c['batch'] == batch]
        assert {c['id'] for c in effects['cases']} == {c['id'] for c in expected}, f'{batch}: case coverage mismatch'
        assert len(effects['cases']) == len(expected), f'{batch}: duplicate cases'
        assert ui['status'] == 'success', f'{batch}: input automation failed'
        screenshots = {}
        for step in ui['steps']:
            assert step['success'], f"{batch}: {step['label']}"
            total_asserts += len(step.get('assertions', []))
            assert all(a['passed'] for a in step.get('assertions', []))
            if step.get('buttonPath'):
                total_ops += 1
                assert step.get('hitObject'), 'Missing actual pointer raycast evidence'
            if step.get('screenshotPath'):
                p = Path(step['screenshotPath'])
                assert p.is_file() and p.stat().st_size > 10000, 'Missing/empty capture'
                total_shots += 1
                if step['label'].endswith('_execute'):
                    screenshots[step['label'][:-8]] = p
        for case in effects['cases']:
            assert case['id'] in screenshots, f"{case['id']}: no execution screenshot"
            failed = [c for c in case['checks'] if not c['passed']]
            unexpected_error = (case.get('exception') or '') != (case.get('expectedException') or '')
            assert case['status'] == 'failed' if failed or unexpected_error else case['status'] in ['passed', 'partial']
            case['capture'] = screenshots[case['id']].relative_to(ROOT).as_posix()
        all_cases.extend(effects['cases'])
        summary['batches'].append({'batch': batch, 'caseCount': len(expected), 'result': dict(Counter(c['status'] for c in effects['cases'])),
                                   'cardCount': len({c['cardId'] for c in expected}), 'effectsReport': str(directory / 'effects.json')})
        detail = ['# ' + batch + '：场景执行明细', '', 'UI流程完成不等于规则全通过。partial 表示即时数值断言通过，但仍有明确未验证范围。', '']
        for case in effects['cases']:
            detail += [f"## {case['id']} · {case['title']} · {case['status']}", '', case['printedEffect'] or '原效果文本缺失。', '',
                       f"[实际执行截图]({screenshots[case['id']].as_posix()})", '', '| 检查项 | 执行前 | 实际值 | 独立预期 | 结果 |', '|---|---|---|---|---|']
            before = {v['key']: v['value'] for v in case['before']}
            for check in case['checks']:
                detail.append(f"| {check['key']} | {before.get(check['key'], '—')} | {check['actual']} | {check['expected']} | {'通过' if check['passed'] else '不符'} |")
            if case.get('exception'): detail += ['', '实际异常：' + case['exception']]
            if case.get('limitations'): detail += ['', '未覆盖：' + '；'.join(case['limitations'])]
            detail += ['']
        (directory / '逐例验收.md').write_text('\n'.join(detail), encoding='utf-8')
    for card_id in sorted({c['cardId'] for c in all_cases}):
        cases = [c for c in all_cases if c['cardId'] == card_id]
        result = '发现不符' if any(c['status'] == 'failed' for c in cases) else '待联验' if any(c['status'] == 'partial' for c in cases) else '已测分支通过'
        summary['cards'].append({'id': card_id, 'status': result, 'cases': len(cases),
                                 'failures': [{'case': c['id'], 'checks': [x for x in c['checks'] if not x['passed']], 'exception': c.get('exception', '')} for c in cases if c['status'] == 'failed'],
                                 'remaining': sorted({lim for c in cases for lim in c.get('limitations', [])})})
    summary.update(cardCount=len(summary['cards']), caseCount=len(all_cases), caseResults=dict(Counter(c['status'] for c in all_cases)),
                   cardResults=dict(Counter(c['status'] for c in summary['cards'])), uiOperations=total_ops,
                   uiAssertions=total_asserts, effectAssertions=sum(len(c['checks']) for c in all_cases), screenshots=total_shots)
    if len(all_cases) == len(planned):
        summary['status'] = 'audit_complete_with_findings'
    sources = read(args.sources)
    assert all(c['matchesRepositoryOriginal'] for c in sources), 'Original artwork provenance failed'
    nonplayable = {c['id'] for c in sources if c['category'] != 'playable_face'}
    summary['playableFaces'] = sum(c['id'] not in nonplayable for c in summary['cards'])
    summary['nonplayableRecords'] = sorted(nonplayable)
    (OUTPUT / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    text = ['# 原版卡全库场景验收记录', '', f"范围：{summary['cardCount']} 条现有行动/法术/宝物卡资产，{summary['caseCount']} 个案例。", '',
            f"人工核对原图后，实际有 {summary['playableFaces']} 张可读卡面；advanced_card_044～046 是近黑空图，047 是牌背，这4条不是可用行动牌。其失败属于数据资产问题。", '',
            '**这不是全卡规则全部验收通过。** 本轮记录既有实现的真实结果；未接入的后续规则保持待联验，部队/技能卡及所有非法操作边界未计入这125条资产记录。', '',
            f"实际按钮操作 {total_ops} 次；UI断言 {total_asserts} 个；效果断言 {summary['effectAssertions']} 个；截图 {total_shots} 张。", '',
            f"案例结果：{summary['caseResults']}。卡片结果：{summary['cardResults']}。", '', '| 卡牌 ID | 本轮结论 | 案例数 |', '|---|---|---:|']
    text += [f"| {c['id']} | {c['status']} | {c['cases']} |" for c in summary['cards']]
    text += ['', '## 复现', '', '1. 用 Unity Hub 打开 Unity 6000.6.0f1 项目。', '2. 执行 Tools/Mage Knight/Original Cards/Run All Batches；也可以从该菜单运行单独一批。',
             '3. 每个批次读取 effects.json 与逐例验收.md，再看执行截图。report.json 只判定输入、断言记录及截图流程。',
             '4. 原卡图保持不变；数据与效果修复见本轮代码提交和工作记录。预期文件由 tools/build_all_card_verification.py 生成；修改规则预期前必须核对原卡面与夹具，不能用实际输出回填预期。']
    (OUTPUT / '验收总览.md').write_text('\n'.join(text) + '\n', encoding='utf-8')
    print(json.dumps({k: summary[k] for k in ['status', 'cardCount', 'caseCount', 'caseResults', 'cardResults', 'uiOperations', 'effectAssertions', 'screenshots']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
