"""Render only trace-verified independent finals; never derive new ratings."""
import argparse
import csv
import hashlib
import html
import json
import os
from pathlib import Path


def build(root):
    protocol = json.loads((root / 'protocol.json').read_text(encoding='utf8'))
    results, proofs, packets = {}, {}, {}
    for n in range(1, 13):
        rd = root / f'round_{n:02}'
        packets[str(n)] = json.loads((rd / 'input.json').read_text(encoding='utf8'))
        accepted = []
        for p in sorted(rd.glob('*/verification.json')):
            proof = json.loads(p.read_text(encoding='utf8'))
            review = p.with_name('review.json')
            if proof.get('verified') and review.is_file():
                if hashlib.sha256(review.read_bytes()).hexdigest() != proof['actual_final_sha256']:
                    raise ValueError('Accepted review differs from actual final: ' + str(review))
                accepted.append((review, proof))
        if len(accepted) > 1:
            raise ValueError('Multiple verified repeats need explicit selection; no cherry picking')
        if accepted:
            p, proof = accepted[0]
            results[str(n)] = json.loads(p.read_text(encoding='utf8'))
            proofs[str(n)] = dict(proof, raw_path=str(p), proof_path=str(p.with_name('verification.json')))
    progress = dict(verified_reviews=len(results), total_rounds=12,
                    scored_items=sum(sum(x.get('score') is not None for x in r['items']) for r in results.values()),
                    reviewed_items=32 * len(results), expected_items=384,
                    unverified_items=sum(sum(x.get('status') == 'unverified' for x in r['items']) for r in results.values()),
                    pending_rounds=[n for n in range(1, 13) if str(n) not in results])
    (root / 'verified_progress.json').write_text(json.dumps(progress, ensure_ascii=False, indent=2), encoding='utf8')
    payload = dict(protocol=protocol, results=results, proofs=proofs, packets=packets, progress=progress)
    (root / 'results.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding='utf8')
    with (root / 'scores.csv').open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['round', 'item_id', 'group', 'item', 'score', 'status', 'value', 'unit', 'reason', 'evidence', 'confidence'])
        for n in range(1, 13):
            r = results.get(str(n))
            for spec in protocol['items']:
                it = next((x for x in r['items'] if x['id'] == spec['id']), {}) if r else {}
                writer.writerow([n, spec['id'], spec['group'], spec['name'], it.get('score'),
                                 it.get('status', 'pending_review'), json.dumps(it.get('value'), ensure_ascii=False),
                                 it.get('unit'), it.get('reason', '该轮尚无已核验最终评分'),
                                 json.dumps(it.get('evidence', []), ensure_ascii=False), it.get('confidence')])
    # Browser paths are prepared server-side; no local filesystem access from JS.
    def relative(p):
        return os.path.relpath(Path(p), root).replace('\\', '/')
    for packet in packets.values():
        for item in packet['required_images'] + packet['required_text']:
            item['url'] = relative(item['path'])
    for proof in proofs.values():
        proof['raw_url'] = relative(proof['raw_path'])
        proof['proof_url'] = relative(proof['proof_path'])
    lookup = {}
    declared_roots = [Path(r[k]) for r in results.values()
                      for k in ('evidence_path_root', 'path_base') if r.get(k)]
    for packet in packets.values():
        for item in packet['required_images'] + packet['required_text'] + packet['optional_observations']:
            path = Path(item['path'])
            lookup[path.as_posix()] = relative(path)
            if path.is_relative_to(Path.cwd()):
                lookup[path.relative_to(Path.cwd()).as_posix()] = relative(path)
            for declared_root in declared_roots:
                if path.is_relative_to(declared_root):
                    lookup[path.relative_to(declared_root).as_posix()] = relative(path)
    for p in [root / 'system_prompt.txt'] + list(root.glob('round_*/input.json')):
        lookup[p.as_posix()] = relative(p)
        if p.is_relative_to(Path.cwd()):
            lookup[p.relative_to(Path.cwd()).as_posix()] = relative(p)
        for declared_root in declared_roots:
            if p.is_relative_to(declared_root):
                lookup[p.relative_to(declared_root).as_posix()] = relative(p)
    payload['path_lookup'] = lookup
    template = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>前12轮 · 独立32项复评</title>
<style>body{margin:0;background:#111e24;color:#e3eae6;font:15px/1.6 system-ui}main{padding:26px;max-width:1800px;margin:auto}h1{font-size:28px}a{color:#e2c588}button,select{padding:8px;background:#29403d;color:white;border:1px solid #8a9a87;font-size:16px}table{border-collapse:collapse;width:100%;background:#1b2d30}td,th{border:1px solid #425651;padding:8px;text-align:left;vertical-align:top}th{background:#2b423e}td.cell{text-align:center;cursor:pointer;min-width:42px}.notice{background:#293b37;padding:16px;border-left:4px solid #d8ba78}.fail{color:#ffb19a}.pending{color:#a5b6b9}.scroll{overflow-x:auto}.evidence{font-size:12px;overflow-wrap:anywhere}.controls{padding:16px 0;position:sticky;top:0;background:#111e24f5}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}.grid img{width:100%;display:block}.grid figure{margin:0}details{background:#203633;padding:12px;margin:12px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere}small{color:#b8c8bf}@media(max-width:900px){.grid{grid-template-columns:1fr}main{padding:12px}}</style><main>
<h1>前12轮 · 8类32项独立复评</h1><div class="notice" id="progress"></div><p>固定SP + 每轮SHA256证据包 + 每轮全新Sol独立评审。只有实际最终输出通过输入/模型/格式核验后才显示评分。主代理不改分；缺证据标“未验证”，尚未完成的评审标“待评”，两者不能混用。</p>
<p><a href="system_prompt.txt">完整SP（含rubrics）</a> · <a href="protocol.json">评分协议</a> · <a href="run_manifest.json">输入哈希清单</a> · <a href="scores.csv">完整384项CSV</a> · <a href="results.json">原始结果与证据JSON</a> · <a href="README.md">复现流程</a></p>
<h2>32项 × 12轮</h2><p>数字为独立评审分（1—5）；—为未给数值分，须查看原因；待评为尚无核验结果。不合成总分、不用平均值抵消硬失败。点击单元格查看对应轮次。</p><div class="scroll"><table id="matrix"></table></div>
<div class="controls"><label>详细轮次 <select id="round"></select></label></div><h2 id="title"></h2><div id="summary"></div><div id="failures"></div><div class="scroll"><table id="detail"></table></div><h2>该轮实际证据包</h2><p>点击原图查看1920×1080截图；图像来自历史原文件，原始与修正版均明确标记。</p><div class="grid" id="images"></div></main><script>const D=__DATA__;const $=id=>document.getElementById(id);const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function link(path){const url=D.path_lookup[String(path).replaceAll('\\\\','/')];return url?'<a target="_blank" href="'+esc(url)+'">'+esc(path)+'</a>':esc(path)}
$('progress').textContent='已核验完成 '+D.progress.verified_reviews+'/12轮；逐项结论 '+D.progress.reviewed_items+'/384；其中数值评分 '+D.progress.scored_items+'项。未完成前不视为最终交付。';
let matrix='<thead><tr><th>项目</th>'+Array.from({length:12},(_,i)=>'<th>R'+(i+1)+'</th>').join('')+'</tr></thead><tbody>';for(const spec of D.protocol.items){matrix+='<tr><td>'+esc(spec.id+' '+spec.group+' · '+spec.name)+'</td>';for(let n=1;n<=12;n++){let r=D.results[n],it=r?.items.find(x=>x.id===spec.id);matrix+='<td class="cell" data-round="'+n+'" title="'+esc(it?.reason||'待评')+'">'+(it?(it.score??'—'):'待评')+'</td>'}matrix+='</tr>'}$('matrix').innerHTML=matrix+'</tbody>';$('matrix').onclick=e=>{let n=e.target.dataset.round;if(n){$('round').value=n;show();$('round').scrollIntoView()}};
for(let n=1;n<=12;n++)$('round').add(new Option('R'+n+(D.results[n]?' · 已核验':' · 待评'),n));
function show(){let n=$('round').value,r=D.results[n],p=D.packets[n],proof=D.proofs[n];$('title').textContent='R'+n+'：'+(r?'独立最终评分':'待评审完成');$('summary').innerHTML=r?'<p>'+esc(r.summary)+'</p><p>门禁：<b>'+esc(r.gate_verdict)+'</b>；评审模型：'+esc(proof.model.join(','))+'；实际worker：'+esc(proof.worker)+'</p><p><a href="'+esc(proof.raw_url)+'">完整原始评分</a> · <a href="'+esc(proof.proof_url)+'">输入与执行核验</a> · <a href="round_'+String(n).padStart(2,'0')+'/input.json">输入包</a></p><p>基线比较：'+esc(r.comparison_to_baseline)+'</p><p>限制：'+esc((r.scope_limits||[]).join('；'))+'</p>':'<p class="pending">该轮没有已核验最终输出，不展示推测分数。输入包已冻结。</p>';
$('failures').innerHTML=r?'<h3>硬失败</h3>'+(r.hard_failures?.length?r.hard_failures.map(x=>'<p class="fail">'+esc(x.item_id+' / '+x.version+'：'+x.reason)+'</p><div class="evidence">'+(x.evidence||[]).map(link).join('<br>')+'</div>').join(''):'<p>评审未列出硬失败；仍需结合未验证范围。</p>'):'';
let detail='<thead><tr><th>项目</th><th>分数/状态</th><th>测量值</th><th>判据与扣分原因</th><th>证据</th></tr></thead><tbody>';for(const spec of D.protocol.items){let it=r?.items.find(x=>x.id===spec.id);detail+='<tr><td>'+esc(spec.id+' '+spec.name)+'</td><td>'+esc(it?(it.score??'未给数值')+' / '+it.status:'待评')+'</td><td>'+esc(it?JSON.stringify(it.value)+' '+(it.unit||''):'')+'</td><td>'+esc(it?.reason||'尚无最终评审')+'</td><td class="evidence">'+(it?.evidence||[]).map(link).join('<br>')+'</td></tr>'}$('detail').innerHTML=detail+'</tbody>';
$('images').innerHTML=p.required_images.map(x=>'<figure><a href="'+esc(x.url)+'" target="_blank"><img loading="lazy" src="'+esc(x.url)+'"></a><figcaption>'+esc(x.role+' · '+x.path.split(/[\\\\/]/).slice(-3).join('/'))+'</figcaption><small>'+esc(x.sha256)+'</small></figure>').join('')};$('round').onchange=show;show();</script>'''
    (root / 'index.html').write_text(template.replace('__DATA__', json.dumps(payload, ensure_ascii=False).replace('</', '<\\/')), encoding='utf8')
    return progress


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--root', type=Path, required=True)
    args = ap.parse_args()
    print(json.dumps(build(args.root.resolve()), ensure_ascii=False))
