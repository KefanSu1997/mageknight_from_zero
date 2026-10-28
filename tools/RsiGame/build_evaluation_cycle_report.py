"""Publish measured cycle evidence and only verified independent ratings."""
import argparse
import csv
import hashlib
import html
import json
import os
from pathlib import Path
from persist_retrospective_review import evidence_path


def build(root):
    read = lambda p: json.loads(p.read_text(encoding='utf8'))
    facts = read(root / 'measured_facts_compact.json')
    review_name = read(root/'active_review.json')['directory'] if (root/'active_review.json').is_file() else 'review_v2'
    review_dir = root/review_name
    protocol = read(review_dir/'protocol.json')
    accepted = []
    for proof_path in review_dir.glob('attempt_*/verification.json'):
        proof = read(proof_path)
        path = proof_path.with_name('review.json')
        if proof.get('verified') and path.is_file():
            if hashlib.sha256(path.read_bytes()).hexdigest() != proof['actual_final_sha256']:
                raise ValueError('Independent raw final changed')
            accepted.append((path, proof))
    if len(accepted) > 1:
        raise ValueError('Multiple reviews require explicit version selection')
    result = read(accepted[0][0]) if accepted else None
    esc = lambda s: html.escape(str(s))
    def url(path):
        return os.path.relpath(path, root).replace('\\', '/')
    def link(path, label=None):
        return '<a href="' + esc(url(path)) + '">' + esc(label or Path(path).name) + '</a>'
    def picture(path):
        return '<a href="' + esc(url(path)) + '"><img loading="lazy" src="' + esc(url(path)) + '"></a>'
    before, after = facts['versions']['before'], facts['versions']['after_v2']
    rows = []
    for name, a, b, unit in [
        ('公共魔力→强化出牌：实际点击', before['source_intent']['clicks'], after['source_intent']['clicks'], '次'),
        ('路径内鼠标移动（首段未知）', before['source_intent']['within_path_distance_pixels'], after['source_intent']['within_path_distance_pixels'], 'px'),
        ('该意图的面板切换', before['panel_transitions'], after['panel_transitions'], '次'),
        ('取骰成功→开始选卡的衔接', before['source_commit_to_next_card']['clicks'], after['source_commit_to_next_card']['clicks'], '次')]:
        rows.append(f'<tr><td>{name}</td><td>{a:.2f}</td><td>{b:.2f}</td><td>{unit}</td></tr>')
    summary = ['<h1>基于32项评估的新迭代 · cycle01</h1>',
        '<p>本轮真实优化：公共魔力直接入口、成功后返回原行动、阶段指导。美术完整适配与能力分行早已存在于本轮before源码，不冒充本轮新增成果。</p>',
        '<p>历史12轮384项中108项有分、276项无数值分。缺项来自动态证据不足、未做几何/像素计算、缺固定评分锚点；本轮补采原始值，仍不将未知补0或把脚本速度当真人体验。</p>',
        '<p>' + ' · '.join(link(root / p, label) for p,label in [
            (review_name+'/system_prompt.txt','SP与完整rubrics'),(review_name+'/input.json','冻结评审输入'),
            ('measured_facts_compact.json','测量摘要'),('after_v2/interaction_checks/validation.json','保留选择/拒绝守恒'),
            ('after_v2/compile_receipt.json','实际编译与Console'),('normal_restore.json','正常开局恢复'),
            ('README.md','复现步骤'),('scores.csv','本轮32项CSV')]) + '</p>',
        '<h2>同场景、同卡牌、同正式结果的参考路径</h2><table><tr><th>指标</th><th>before</th><th>after_v2</th><th>单位</th></tr>' + ''.join(rows) + '</table>',
        '<p>点击8→6（减少25%），路径内移动约减少16.5%。这是该固定意图的真实参考路径，未证明全局最短，也不外推全部行为的总收益。A→B衔接已包含在总路径中，不重复计费。</p>',
        '<h2>被拒绝的中间版本</h2><p>after初稿使格挡Details自动字号从33.15降到30.8，因此拒绝晋升。after_v2缩短指导，字号35.6，保留原画布、字号参数和控件尺寸。</p>' + link(root/'after/rejection.json','拒绝记录') + ' · ' + link(root/'after/key_phases/block/capture.json','原始缩字证据')]
    summary += ['<h2>补充的回归与阅读测试</h2><p>17/17同场景正式native状态一致；匹配的文字区域无缩小、无位移，全部画布保持1920×1080。</p>' + link(root/'anti_regression_checks.json','逐项回归证据')]
    reader_path=root/'controlled_reader/verification.json'
    if reader_path.is_file():
        reader=read(reader_path)
        if reader.get('verified'):
            summary += ['<p>独立模型固定截图阅读：'+str(reader['correct'])+'/'+str(reader['denominator'])+'正确，仅单次模型读图，不是人类搜索时间或误操作率。该证据晚于review_v2冻结，独立呈现，不改动已派发输入。</p>'+link(reader_path,'阅读测试核验')]
    historical_path=Path(__file__).resolve().parents[2]/'AutomationOutputs/RsiGame/experiments/20261021_mageknight_unity_30/reviews/rounds_01_to_12/rescore_v1/results.json'
    if historical_path.is_file():
        historical=read(historical_path)['results']
        missing=[]
        for spec in protocol['items']:
            old=[next(i for i in value['items'] if i['id']==spec['id']) for value in historical.values()]
            reason=next(i for i in historical['12']['items'] if i['id']==spec['id'])['reason']
            missing.append('<tr><td>'+esc(spec['id']+' '+spec['name'])+'</td><td>'+str(sum(i['score'] is None for i in old))+'/12</td><td>'+esc(reason)+'</td></tr>')
        summary += ['<details><summary>为什么旧评分有276项空分：32项逐条查看</summary><p>下表保留原评分，不因新版本补证据而回填历史。最后一列是R12原始评审的缺证据说明。</p><table><tr><th>项目</th><th>历史空分数量</th><th>R12原始说明</th></tr>'+''.join(missing)+'</table></details>']
    if result:
        summary += ['<h2>独立Sol的32项最终结论</h2><p>门禁：' + esc(result['gate_verdict']) + '。' + esc(result['summary']) + '</p>',
                    '<p>' + link(accepted[0][0],'实际原始最终输出') + ' · ' + link(accepted[0][0].with_name('verification.json'),'输入、图像和执行核验') + '</p>']
    else:
        summary += ['<h2>独立评审进行中</h2><p>当前只有测量结果；32项模型评分等待真实最终输出，未由主代理补写。</p>']
    with (root/'scores.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.writer(stream);writer.writerow(['id','name','score','status','value','unit','reason','evidence','confidence'])
        table=[]
        for spec in protocol['items']:
            item=next((x for x in result['items'] if x['id']==spec['id']),None) if result else None
            score=item['score'] if item else None; status=item['status'] if item else 'pending_review'
            reason=item['reason'] if item else '等待独立最终输出'
            evidence=item['evidence'] if item else []
            value=item.get('value') if item else None
            unit=item.get('unit') if item else None
            confidence=item.get('confidence') if item else None
            writer.writerow([spec['id'],spec['name'],score,status,json.dumps(value,ensure_ascii=False),unit,reason,json.dumps(evidence,ensure_ascii=False),confidence])
            citations='<br>'.join(link(evidence_path(path,result),Path(path).name) for path in evidence) if result else ''
            table.append('<tr><td>'+esc(spec['id']+' '+spec['name'])+'</td><td>'+esc(score if score is not None else '—')+'</td><td>'+esc(status)+'</td><td>'+esc(json.dumps(value,ensure_ascii=False))+' '+esc(unit or '')+'</td><td>'+esc(reason)+'</td><td>'+citations+'</td></tr>')
    summary += ['<table><tr><th>项目</th><th>评分</th><th>证据状态</th><th>原始值</th><th>原因</th><th>证据</th></tr>'+''.join(table)+'</table>',
                '<h2>重要阶段与典型界面对照</h2><p>每张图可点击原1920×1080 PNG。左before为当前继承版本，右after_v2为本轮候选。</p>']
    for capture in sorted((root/'after_v2/key_phases').glob('*/screen.png')):
        label=capture.parent.name
        summary += ['<h3>'+esc(label)+'</h3><div class="pair"><figure>'+picture(root/f'before/key_phases/{label}/screen.png')+'<figcaption>before</figcaption></figure><figure>'+picture(capture)+'<figcaption>after_v2</figcaption></figure></div>']
    summary += ['<h2>仍需补证据</h2><p>真人搜索时间与误操作率、首个有效反馈帧、动画期间实际阻塞、全游戏压力与长期性能、全部规则/素材覆盖，以及客观测量到1—5的固定阈值仍有缺口。60帧是指定编辑器闲置样本；像素对比是已定义采样proxy；full UV只证纹理适配，不证主体语义。</p>']
    style='body{background:#142426;color:#e5ece6;font:16px/1.6 system-ui;margin:28px}main{max-width:1600px;margin:auto}a{color:#e5c58a}table{border-collapse:collapse;width:100%;table-layout:fixed}td,th{border:1px solid #52645b;padding:9px;text-align:left;vertical-align:top;overflow-wrap:anywhere}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:0}img{width:100%;display:block}@media(max-width:900px){.pair{grid-template-columns:1fr}}'
    (root/'index.html').write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>UI评估迭代 cycle01</title><style>'+style+'</style><main>'+''.join(summary)+'</main></html>',encoding='utf8')
    progress=dict(independent_review_verified=bool(result),measured_screenshots_each_version=17,source_clicks_before=8,source_clicks_after=6,
                  reviewed_items=len(result['items']) if result else 0,numeric_scores=sum(x['score'] is not None for x in result['items']) if result else 0)
    (root/'progress.json').write_text(json.dumps(progress,ensure_ascii=False,indent=2),encoding='utf8')
    return progress


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    print(json.dumps(build(parser.parse_args().root.resolve()),ensure_ascii=False))
