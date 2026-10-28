"""Read-only evidence monitor. No model calls, Unity operations or process control."""
from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
import html
import json
import os
from pathlib import Path
import re
import tempfile
import time
from datetime import datetime, timezone, timedelta

DEFAULT = Path(__file__).resolve().parents[2] / 'AutomationOutputs/RsiGame/experiments/20261021_mageknight_unity_30'


def read_json(path, default=None):
    try:
        if path.stat().st_size > 8_000_000:
            return default
        return json.loads(path.read_text(encoding='utf-8-sig'))
    except (OSError, ValueError):
        return default


def process_liveness(pid):
    """Probe Windows process handle; never infer liveness from a status file."""
    if not isinstance(pid, int) or pid <= 0:
        return {'state': 'unknown', 'reason': 'missing PID'}
    if os.name != 'nt':
        return {'state': 'unknown', 'reason': 'Windows process probe unavailable'}
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.GetExitCodeProcess.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        error = ctypes.get_last_error()
        return {'state': 'dead' if error == 87 else 'unknown', 'winerror': error}
    try:
        code = wintypes.DWORD()
        if not kernel.GetExitCodeProcess(handle, ctypes.byref(code)):
            return {'state': 'unknown', 'winerror': ctypes.get_last_error()}
        return {'state': 'live' if code.value == 259 else 'dead', 'exit_code': code.value,
                'caveat': 'PID existence checked; identity reuse cannot be excluded'}
    finally:
        kernel.CloseHandle(handle)


def redact(text):
    text = re.sub(r'(?i)bearer\s+[^\s,;]+', 'Bearer [REDACTED]', text)
    text = re.sub(r'(?i)(authorization\s*[:=]\s*|bearer\s+|(?:api[_-]?key|access[_-]?token|password|secret)\s*[:=]\s*)[^\s,;]+', r'\1[REDACTED]', text)
    return re.sub(r'\bsk-[A-Za-z0-9_-]+', '[REDACTED]', text)


def tail(path):
    try:
        with path.open('rb') as stream:
            stream.seek(max(0, path.stat().st_size - 12000))
            return redact(stream.read().decode('utf8', errors='replace'))
    except OSError:
        return ''


def atomic_write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    name = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf8', dir=path.parent, delete=False) as stream:
            name = stream.name
            stream.write(text)
        for attempt in range(6):
            try:
                os.replace(name, path)
                break
            except PermissionError:
                if attempt == 5:
                    raise
                time.sleep(.05 * (attempt + 1))
    finally:
        if name and os.path.exists(name):
            try:
                os.unlink(name)
            except OSError:
                pass


def sanitize(value):
    if isinstance(value, str):
        return redact(value)
    if isinstance(value, list):
        return [sanitize(v) for v in value]
    if isinstance(value, dict):
        return {k: '[REDACTED]' if re.search(r'(?i)token|password|secret|api.?key|authorization', k) else sanitize(v)
                for k, v in value.items()}
    return value


def collect(root, now=None, probe=process_liveness, stall_seconds=300):
    root = Path(root).resolve()
    now = time.time() if now is None else now
    run = root / 'development/runs/mageknight-unity'
    live = read_json(root / 'live_status.json', {}) or {}
    rounds_path = run / 'rounds.json'
    raw_rounds = read_json(rounds_path)
    rounds = raw_rounds if isinstance(raw_rounds, list) else []
    calls = []
    for directory in sorted((root / 'model_calls').glob('*')):
        if not directory.is_dir():
            continue
        request_path = directory / 'request.json'
        req = read_json(request_path, {}) or {}
        receipt = read_json(directory / 'receipt.json', {}) or {}
        response = read_json(directory / 'response.json', {}) or {}
        stack = req.get('caller_stack', [])
        calls.append({'id': directory.name, 'role': req.get('kind', 'unknown'),
                      'requested_model': req.get('requested_model', req.get('model')),
                      'executed_model': receipt.get('executed_model'),
                      'accepted': bool(receipt.get('raw_sha256') and receipt.get('executed_model') == req.get('requested_model', req.get('model'))),
                      'response_present': (directory / 'response.json').is_file(),
                      'error': bool(response.get('error')), 'request_path': str(request_path),
                      'receipt_path': str(directory / 'receipt.json'),
                      'caller_stage': ' → '.join(str(s.get('function', '?')) for s in stack if isinstance(s, dict)),
                      'request_mtime': request_path.stat().st_mtime if request_path.exists() else None})
    current = next((c for c in calls if str(Path(c['request_path'])) == str(Path(live.get('request', '')))), None)
    pending = bool(current and not current['accepted'])
    display_stage = ('回复已接收；运行正在推进（reply accepted; runtime advancing）'
                     if current and current['accepted'] and live.get('status') == 'waiting_for_model'
                     else live.get('status', 'unknown'))
    age = max(0, now - current['request_mtime']) if pending and current['request_mtime'] else None
    liveness = probe(live.get('pid'))
    flags = []
    if liveness['state'] == 'dead':
        flags.append('状态文件 PID 已退出；需要检查，保留失败历史')
    if age is not None and age > stall_seconds:
        flags.append('模型等待超过阈值；需要检查，不能据此判定失败或终止')
    if (root / 'round01_measurement_validity.json').is_file():
        flags.append('首轮旧图片库存/使用率统计无效，不能用于评估提升；以实际截图和独立审查为准')
    if (root / 'upstream_checkpoint_caveat.json').is_file():
        flags.append('原始 checkpoint 可能与自身配对；跨版本改善须另有基线/候选证据')
    if (root / 'upstream_settle_recheck_caveat.json').is_file():
        flags.append('原始等待复查可能把成功的局部操作误报为无响应；须核对实际前后帧与状态')
    if (root / 'user_steering/20261021_monster_art_regeneration/policy_epoch.json').is_file():
        flags.append('第9轮起采用用户修订：允许重生成怪物插画；裁剪、印刷残留和必要信息缺失为否决项。独立素材候选不代表已在游戏中验收')
    failed_attempts = list((root / 'failed_attempts').glob('*/recovery.json'))
    if failed_attempts:
        flags.append(f'已归档 {len(failed_attempts)} 次未提交的流程失败尝试，不计为完成轮数')
    directories = sorted(p for p in run.glob('round_*') if p.is_dir())
    artifacts = [str(p) for pattern in ('*caveat.json', 'worker_lineage_resolution.json',
                                      'completion_requirements.json', 'dispatch_policy.json', 'audits/*.json',
                                      'user_steering/20261021_monster_art_regeneration/policy_epoch.json',
                                      'user_steering/20261021_monster_art_regeneration/gate_calibration.json',
                                      'art_candidates/*/*manifest.json')
                 for p in sorted(root.glob(pattern)) if p.is_file()]
    outcomes = [{'round': r.get('round'), 'outcome': r['verification'], 'source': str(rounds_path)}
                for r in rounds if isinstance(r, dict) and 'verification' in r]
    # Deliberately bounded directory depth; never traverse blobs, messages or Unity caches.
    for directory in directories:
        audit = read_json(directory / 'root_verification_audit.json', {}) or {}
        if audit.get('visual_information_risk', {}).get('visual_complete') is False:
            flags.append(directory.name + ' 附加截图有必要信息截断风险；规则状态相等不代表文字完整，后续版本须复查')
        for pattern in ('*.json', 'verify/*.json', 'probe/*manifest.json', 'key_phases/manifest.json',
                        'input_feedback_validation/validation.json', 'input_feedback_validation/*.png',
                        'interaction_extras/validation.json', 'interaction_extras/*.png',
                        'inline_source_validation/validation.json', 'inline_source_validation/*.png',
                        'three_enemy_fixture/validation.json', 'three_enemy_fixture/screen.png',
                        'phase_transition_validation/*.json', 'phase_transition_validation/*.png',
                        'post_gate_fix/*.json', 'post_gate_fix/key_phases/manifest.json',
                        'post_gate_fix/phase_transition_validation/*.json',
                        'post_gate_fix/phase_transition_validation/*.png',
                        'post_gate_fix/three_enemy_fixture/validation.json',
                        'post_gate_fix/three_enemy_fixture/screen.png'):
            for p in sorted(directory.glob(pattern)):
                if p.name == 'tree_manifest.json' or p.suffix.lower() == '.png':
                    artifacts.append(str(p))
                elif any(x in p.name.lower() for x in ('verify', 'verification', 'outcome', 'checklist',
                                                      'manifest', 'guard', 'audit', 'receipt', 'validation', 'caveat', 'report')):
                    artifacts.append(str(p))
                    if 'verify' in str(p.relative_to(directory)).lower():
                        outcomes.append({'path': str(p), 'outcome': read_json(p)})
    baseline = read_json(root / 'baseline/key_phases/manifest.json', {}) or {}
    phases = []
    for cap in baseline.get('captures', []):
        label = cap.get('label', '?')
        candidates = []
        for directory in directories:
            for phase_root in (directory / 'key_phases', directory / 'post_gate_fix/key_phases'):
                manifest = read_json(phase_root / 'manifest.json', {}) or {}
                candidates.extend(c.get('screenshot') for c in manifest.get('captures', []) if c.get('label') == label and c.get('screenshot'))
        phases.append({'label': label, 'baseline': cap.get('screenshot'),
                       'baseline_sha256': cap.get('screenshot_sha256'),
                       'current': candidates[-1] if candidates else None})
    crash_rounds = [r for r in rounds if isinstance(r, dict) and (r.get('crashed') is True or r.get('crash') or r.get('status') in ('crashed', 'crash'))]
    return sanitize({'generated_utc': datetime.fromtimestamp(now, timezone.utc).isoformat(),
            'generated_local': datetime.fromtimestamp(now, timezone(timedelta(hours=8))).isoformat(),
            'timezone': 'Asia/Shanghai (UTC+08:00)', 'display_stage': display_stage,
            'status_claim': live.get('status', 'unknown'), 'pid': live.get('pid'), 'process': liveness,
            'current_call': current, 'pending_seconds': age, 'inspection_flags': flags,
            'counts': {'recorded_rounds': len(rounds), 'crash_rounds_explicit': len(crash_rounds),
                       'editor_attempts': sum(c['role'] == 'editor' for c in calls),
                       'accepted_calls': sum(c['accepted'] for c in calls), 'model_requests': len(calls),
                       'verify_round_outcomes': sum(isinstance(r, dict) and 'verification' in r for r in rounds),
                       'verify_outcome_files': sum('path' in o for o in outcomes)},
            'completion_audit': 'unknown — 尚未由最终完成审计确认；30 条记录也不自动等于完成',
            'rounds_source': str(rounds_path), 'rounds_source_state': 'readable' if isinstance(raw_rounds, list) else 'missing_or_unreadable',
            'calls': calls, 'rounds': rounds, 'verify_outcomes': outcomes, 'phases': phases,
            'artifacts': sorted(set(artifacts)), 'loop_log_tail': tail(run / 'loop.log')})


def render(state, root):
    e = lambda x: html.escape(str(x))
    def link(path, label=None):
        if not path:
            return '未知 / 尚无证据'
        p = Path(path)
        url = os.path.relpath(p, root).replace('\\', '/') if p.is_relative_to(root) else p.as_uri()
        return '<a href="' + e(url) + '">' + e(label or p.name) + '</a>'
    def thumb(path):
        if not path or not Path(path).is_file():
            return '未采集'
        p = Path(path)
        url = os.path.relpath(p, root).replace('\\', '/') if p.is_relative_to(root) else p.as_uri()
        return '<a href="' + e(url) + '"><img loading="lazy" src="' + e(url) + '"></a>'
    labels = {'recorded_rounds': '已记录轮数', 'crash_rounds_explicit': '明确标记崩溃轮数',
              'editor_attempts': 'Editor 请求次数', 'accepted_calls': '已接收模型调用',
              'model_requests': '模型请求总数', 'verify_outcome_files': '验证目录证据文件',
              'verify_round_outcomes': '含验证结果的轮次（含跳过）'}
    counts = ''.join('<li>' + e(labels[k]) + '：<b>' + str(v) + '</b></li>' for k, v in state['counts'].items())
    calls = ''.join('<tr>' + ''.join('<td>' + e(c.get(k)) + '</td>' for k in ('id', 'role', 'requested_model', 'executed_model', 'accepted', 'caller_stage')) + '<td>' + link(c['request_path'], 'request') + ' ' + (link(c['receipt_path'], 'receipt') if Path(c['receipt_path']).exists() else '无 receipt') + '</td></tr>' for c in state['calls'])
    phases = ''.join('<tr><td>' + e(p['label']) + '</td><td>' + thumb(p['baseline']) + '</td><td>' + thumb(p['current']) + '</td></tr>' for p in state['phases'])
    return '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta http-equiv="refresh" content="15"><title>MageKnight 实验监控</title><style>body{font:16px system-ui;margin:24px;color:#173047;background:#f5f7fa}table{border-collapse:collapse;width:100%;background:white}td,th{border:1px solid #ccd5df;padding:8px;text-align:left;overflow-wrap:anywhere}img{max-width:360px;width:100%}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#e9eef4;padding:16px}.flag{color:#a13714}</style><h1>MageKnight 30 轮实验：实际证据监控</h1>''' + '<p>更新时间：' + e(state['generated_local']) + ' ' + e(state['timezone']) + '</p><p>状态文件：' + e(state['status_claim']) + '；当前阶段：' + e(state['display_stage']) + '；实际进程：' + e(state['process']) + '</p><p>当前调用：' + e(state['current_call']) + '</p><ul>' + counts + '</ul><p>模型调用与迭代轮数分别计数；崩溃仅统计记录中明确标记，缺少字段不证明无崩溃。Editor 请求不等于成功修改；验证文件不等于验证通过。</p><p>完成审计：' + e(state['completion_audit']) + '</p><p class="flag">' + e('；'.join(state['inspection_flags'])) + '</p><h2>模型角色与实际路由</h2><table><tr><th>调用</th><th>角色</th><th>请求模型</th><th>receipt 执行模型</th><th>接收</th><th>调用阶段</th><th>证据</th></tr>' + calls + '</table><h2>关键阶段图：基线 / 当前实际采集</h2><p>没有当前采集时保留空缺，不以基线冒充新版本。</p><table><tr><th>阶段</th><th>基线</th><th>当前</th></tr>' + phases + '</table><h2>源码快照、清单与验证证据</h2><p>' + link(state['rounds_source'], 'rounds.json') + '：' + e(state['rounds_source_state']) + '</p><ul>' + ''.join('<li>' + link(p, p) + '</li>' for p in state['artifacts']) + '</ul><h2>原始轮次记录（保留失败）</h2><pre>' + e(json.dumps(state['rounds'], ensure_ascii=False, indent=2)) + '</pre><h2>loop.log 末尾（已遮盖凭据）</h2><pre>' + e(state['loop_log_tail']) + '</pre></html>'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--experiment', '--root', type=Path, default=DEFAULT)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--once', action='store_true')
    mode.add_argument('--watch', action='store_true')
    parser.add_argument('--interval', type=float, default=10)
    parser.add_argument('--stall-seconds', type=float, default=300)
    args = parser.parse_args()
    if args.interval <= 0:
        parser.error('--interval must be positive')
    root = args.experiment.resolve()
    last_write_error = None
    while True:
        state = collect(root, stall_seconds=args.stall_seconds)
        if last_write_error:
            state['inspection_flags'].append('上一周期监控输出写入失败，将继续重试：' + last_write_error)
        try:
            atomic_write(root / 'monitor_state.json', json.dumps(state, ensure_ascii=False, indent=2))
            atomic_write(root / 'index.html', render(state, root))
            last_write_error = None
        except OSError as error:
            if not args.watch:
                raise
            last_write_error = str(error)
            print('MONITOR_WRITE_RETRY ' + last_write_error, flush=True)
        if not args.watch:
            break
        time.sleep(args.interval)


if __name__ == '__main__':
    main()
