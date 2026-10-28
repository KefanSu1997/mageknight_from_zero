"""Render a verified independent review without deriving new scores."""
import argparse
import hashlib
import html
import json
from pathlib import Path


def load_actual_review(review_dir, proof):
    raw = (review_dir / 'actual_final.txt').read_bytes()
    if not proof.get('verified'):
        raise ValueError('Verified actual FINAL required')
    descriptor = proof.get('actual_final')
    expected = descriptor['sha256'] if descriptor else proof.get('actual_final_sha256')
    if hashlib.sha256(raw).hexdigest() != expected:
        raise ValueError('Verified actual FINAL required')
    if descriptor and (Path(descriptor['path']).resolve() != (review_dir / 'actual_final.txt').resolve()
                       or descriptor['bytes'] != len(raw)):
        raise ValueError('Actual FINAL descriptor mismatch')
    saved = (review_dir / 'review.json').read_bytes()
    if descriptor:
        if json.loads(saved) != json.loads(raw):
            raise ValueError('Review differs from actual FINAL')
    elif saved != raw:
        raise ValueError('Review differs from actual FINAL')
    return json.loads(raw)


def render(review_dir, output, mapping=None, packet_path=None):
    review_dir, output = Path(review_dir), Path(output)
    proof = json.loads((review_dir / 'verification.json').read_text(encoding='utf8'))
    review = load_actual_review(review_dir, proof)
    rubric_items = {}
    packet = {}
    if packet_path:
        packet_path = Path(packet_path).resolve()
        packet_raw = packet_path.read_bytes()
        expected_input = proof['input']['sha256'] if 'input' in proof else proof['input_sha256']
        if hashlib.sha256(packet_raw).hexdigest() != expected_input:
            raise ValueError('Report input does not match verified review')
        packet = json.loads(packet_raw)
        rubric_path = Path(packet['rubric_path'])
        if not rubric_path.is_absolute():
            rubric_path = packet_path.parent / rubric_path
        rubric_raw = rubric_path.read_bytes()
        if hashlib.sha256(rubric_raw).hexdigest() != packet['rubric_sha256']:
            raise ValueError('Frozen rubric SHA mismatch')
        rubric_items = {item['id']: item for item in json.loads(rubric_raw)['items']}
    names = {'A': '匿名 A', 'B': '匿名 B'}
    if mapping:
        key = json.loads(Path(mapping).read_text(encoding='utf8'))
        names = {side: ('迭代版' if key[side]['role'] == 'candidate' else '基线版')
                 for side in ('A', 'B')}
    esc = lambda value: html.escape(str(value))
    pretty = lambda value: esc(json.dumps(value, ensure_ascii=False, indent=2))
    blocks = ['<!doctype html><meta charset="utf-8"><title>Combat focus v3 独立评分</title>',
              '<style>body{font:16px/1.6 system-ui;margin:32px auto;max-width:1500px;padding:0 20px;color:#203030}table{border-collapse:collapse;width:100%;table-layout:fixed}td,th{border:1px solid #bbb;padding:12px;vertical-align:top}pre{white-space:pre-wrap;overflow-wrap:anywhere}th:first-child{width:60px}.score{font-size:24px;font-weight:bold}details{margin:8px 0}</style>',
              '<h1>Combat focus v3：独立评分完整结果</h1>',
              '<p>仅展示真实独立 FINAL 的逐项结果，不计算总分或平均分。null 表示未评分，原因见各项缺件。读取验证证明输入传输与格式一致，不证明判断一定正确。</p>',
              '<p>门禁结论：<strong>' + esc(review['gate_verdict']) + '</strong></p>']
    if mapping:
        blocks.append('<p>审查完成后解盲：A＝' + esc(names['A']) + '；B＝' + esc(names['B']) + '。上方门禁针对整份评审包，不能把基线的硬失败直接算到迭代版。</p>')
        for side in ('B', 'A'):
            failures = [item for item in review['hard_failures'] if item.get('side') == side]
            blocks.append('<p>' + esc(names[side]) + '：' +
                          ('记录了 ' + str(len(failures)) + ' 条硬失败。' if failures else
                           '未记录硬失败；这不等于通过，仍须检查下方缺件与未验证门禁。') + '</p>')
    for label, field in [('硬失败', 'hard_failures'), ('未验证门禁', 'unverified_gates'),
                         ('结论范围', 'scope_limits'), ('评审对比', 'paired_comparison')]:
        blocks.append('<details open><summary>' + label + '</summary><pre>' + pretty(review[field]) + '</pre></details>')
    if 'evidence_method' in review:
        blocks.append('<details open><summary>证据阅读方法与个人直接查看范围</summary><pre>' + pretty(review['evidence_method']) + '</pre></details>')
    blocks.append('<table><thead><tr><th>条目</th><th>' + esc(names['B']) + '</th><th>' + esc(names['A']) + '</th></tr></thead><tbody>')
    rows = {side: {item['id']: item for item in review['sides'][side]} for side in ('A', 'B')}
    for group in range(1, 9):
        for number in range(1, 5):
            rid = f'{group}.{number}'
            definition = rubric_items.get(rid, {})
            blocks.append('<tr><th>' + rid + '<br>' + esc(definition.get('name', '')) + '</th>')
            for side in ('B', 'A'):
                item = rows[side][rid]
                score = '未评分' if item['score'] is None else str(item['score']) + '/5'
                blocks.append('<td><div class="score">' + score + '</div><div>' + esc(item['status']) + '</div><p>' + esc(item['reason']) + '</p>')
                if definition:
                    blocks.append('<details><summary>评分锚点与原登记分母</summary><pre>' + pretty(definition) + '</pre></details>')
                for label, field in [('缺件', 'missing_reasons'), ('分母', 'denominator'), ('原始指标', 'raw_metrics'),
                                     ('视觉观察', 'visual_observations'), ('逐场景结果', 'unit_results'), ('证据', 'evidence')]:
                    blocks.append('<details' + (' open' if field == 'missing_reasons' and item[field] else '') + '><summary>' + label + '</summary><pre>' + pretty(item[field]) + '</pre></details>')
                blocks.append('</td>')
            blocks.append('</tr>')
    blocks.append('</tbody></table>')
    if packet:
        gallery = {}
        image_entries = packet['required_images']
        if 'manifest' in packet:
            manifest_entry = packet['manifest']
            manifest_raw = Path(manifest_entry['path']).read_bytes()
            if hashlib.sha256(manifest_raw).hexdigest() != manifest_entry['sha256']:
                raise ValueError('Team manifest SHA mismatch')
            image_entries = [image for task in json.loads(manifest_raw)['tasks'] for image in task['images']]
            blocks.append('<p>以下图库为分片审查团队的全部冻结原图；最终评分者个人直接查看的图片仅限上方 evidence_method 所列范围。</p>')
        for entry in image_entries:
            path = Path(entry['path'])
            if not path.is_absolute():
                path = packet_path.parent / path
            path = path.resolve()
            if hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
                raise ValueError('Frozen image SHA mismatch: ' + str(path))
            side = path.parent.name
            if side in names:
                gallery.setdefault(path.name, {})[side] = path.as_uri()
        blocks.append('<h2>冻结原图对照</h2><p>点击原图打开完整尺寸。空白一侧表示该冻结包没有对应截图，不能据此认定功能不存在。</p><table><thead><tr><th>场景</th><th>' + esc(names['B']) + '</th><th>' + esc(names['A']) + '</th></tr></thead><tbody>')
        for label, sides in sorted(gallery.items()):
            blocks.append('<tr><th>' + esc(label) + '</th>')
            for side in ('B', 'A'):
                uri = sides.get(side)
                blocks.append('<td>' + ('<a href="' + esc(uri) + '"><img loading="lazy" style="width:100%" src="' + esc(uri) + '"></a>' if uri else '无对应冻结截图') + '</td>')
            blocks.append('</tr>')
        blocks.append('</tbody></table>')
    blocks.append('<details><summary>完整读取记录</summary><pre>' + pretty(review['read_ledger']) + '</pre></details>')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text('\n'.join(blocks), encoding='utf8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review-dir', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--mapping', help='Operator-only identity mapping, after blind review finishes')
    parser.add_argument('--input', help='Frozen reviewed packet; verify SHA and include rubric definitions')
    args = parser.parse_args()
    render(args.review_dir, args.out, args.mapping, args.input)
