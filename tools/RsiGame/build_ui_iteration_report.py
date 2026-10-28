"""Publish a versioned UI iteration without manufacturing independent scores."""
import argparse
import csv
import hashlib
import html
import json
import os
from pathlib import Path
from persist_retrospective_review import evidence_path


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def build(root):
    meta = read(root/'cycle.json'); candidate = meta['candidate']
    review_dir = root/read(root/'active_review.json')['directory']
    packet = read(review_dir/'input.json'); protocol = read(review_dir/'protocol.json')
    proofs = [p for p in review_dir.glob('attempt_*/verification.json') if read(p).get('verified')]
    if len(proofs) > 1: raise ValueError('Multiple accepted finals')
    result = None
    if proofs:
        path = proofs[0].with_name('review.json')
        if hashlib.sha256(path.read_bytes()).hexdigest() != read(proofs[0])['actual_final_sha256']:
            raise ValueError('Final bytes changed')
        result = read(path)
    esc = lambda value: html.escape(str(value))
    def url(path): return os.path.relpath(path, root).replace('\\','/')
    def link(path, label): return f'<a href="{esc(url(path))}">{esc(label)}</a>'
    def pic(path): return f'<a href="{esc(url(path))}"><img loading="lazy" src="{esc(url(path))}"></a>'
    body = [f'<h1>UI评估迭代 {esc(meta["cycle"])}</h1>', '<p>'+esc(meta['context'])+'</p>',
            '<p>完整结算记录置于原有右侧详情区；保留原画布、怪物、手牌及操作入口。中间失败版本单独保留。</p>',
            '<p>'+' · '.join(link(p,label) for p,label in [(review_dir/'system_prompt.txt','SP与32项rubrics'),
                (review_dir/'input.json','冻结输入'), (root/'measured_facts_compact.json','原始测量摘要'),
                (root/candidate/'recall_checks/validation.json','真实回查交互'), (root/candidate/'required_information.json','有限必要信息清单测量'),
                (root/candidate/'compile_receipt.json','实际编译凭据'), (root/'scores.csv','完整评分CSV')])+'</p>']
    if (root/'missing_score_explanation.md').is_file():
        body += ['<p>'+link(root/'missing_score_explanation.md', '逐项查看：上一版19个空分的实际原因')+'</p>']
    facts = read(root/'measured_facts_compact.json')
    body += ['<h2>相同参考路径成本</h2><table><tr><th>指标</th><th>基线</th><th>候选</th></tr>']
    for label, field in [('点击次数','clicks'),('路径内距离 px（初始段未知）','within_path_distance_pixels')]:
        a=facts['versions']['before']['source_intent'][field]; b=facts['versions'][candidate]['source_intent'][field]
        body += [f'<tr><td>{label}</td><td>{a}</td><td>{b}</td></tr>']
    body += ['</table><p>本轮重点为必要信息保留；以上为经正式状态核对的固定参考路径，未证明全局最短。</p>']
    info=read(root/candidate/'required_information.json')['limited_required_role_checks']
    reader=read(root/'controlled_reader/verification.json')
    body += ['<h2>补齐的有限测量</h2><p>预声明必要文本角色：'+str(info['numerator'])+'/'+str(info['denominator'])+'通过结构、内容与生成完整性检查。这不是无遮挡可见率，滚动区域由独立原图复核。</p>',
             '<p>独立模型固定读图问题：'+str(reader['correct'])+'/'+str(reader['denominator'])+'正确。只代表本次模型读图，未测真人搜索时间或误操作率。</p>'+link(root/'controlled_reader/verification.json','独立阅读试验核验')]
    if result:
        body += ['<h2>独立 Sol 最终评分</h2><p>'+esc(result['gate_verdict'])+' · '+esc(result['summary'])+'</p>',
                 link(proofs[0].with_name('review.json'),'原始最终输出')+' · '+link(proofs[0],'执行与输入核验')]
    else: body += ['<h2>独立评分等待完成</h2><p>目前仅发布采集和测量结果，未由编辑或主代理填写评分。</p>']
    rows=[]
    with (root/'scores.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.writer(stream); writer.writerow(['id','name','score','status','value','unit','reason','evidence'])
        for spec in protocol['items']:
            item=next((x for x in result['items'] if x['id']==spec['id']),None) if result else None
            value=json.dumps(item.get('value'),ensure_ascii=False) if item else ''
            reason=item['reason'] if item else '等待独立最终输出'
            score=item['score'] if item else None; status=item['status'] if item else 'pending_review'
            citations='<br>'.join(link(evidence_path(p,result),Path(p).name) for p in item.get('evidence',[])) if item else ''
            writer.writerow([spec['id'],spec['name'],score,status,value,item.get('unit') if item else '',reason,json.dumps(item.get('evidence',[]) if item else [],ensure_ascii=False)])
            rows += [f'<tr><td>{esc(spec["id"]+" "+spec["name"])}</td><td>{score if score is not None else "—"}</td><td>{esc(status)}</td><td>{esc(value)}</td><td>{esc(reason)}</td><td>{citations}</td></tr>']
    body += ['<table><tr><th>项目</th><th>分数</th><th>状态</th><th>原始值</th><th>理由</th><th>证据</th></tr>'+''.join(rows)+'</table>',
             '<h2>17个相同情境前后对照</h2>']
    for p in sorted((root/candidate/'key_phases').glob('*/screen.png')):
        body += [f'<h3>{esc(p.parent.name)}</h3><div class="pair"><figure>{pic(root/"before/key_phases"/p.parent.name/"screen.png")}<figcaption>基线</figcaption></figure><figure>{pic(p)}<figcaption>候选</figcaption></figure></div>']
    body += ['<h2>回查与继续操作的实际截图</h2>']
    for p in sorted((root/candidate/'recall_checks/key_phases').glob('*/screen.png')):
        body += [f'<h3>{esc(p.parent.name)}</h3>'+pic(p)]
    body += ['<h2>空值的含义</h2><p>缺连续帧、真人行为或完整规则分母的项目，需要补相应证据；已有原始测量但没有固定分数阈值的项目保留数值，不任意换算成1—5分。模型读图与真人体验分开，不能平均分掩盖必要信息丢失。</p>']
    continuity=root/'post_freeze_measurements/card_entity_continuity.json'
    if continuity.is_file():
        data=read(continuity);all_pairs=data['all_saved_input_pairs'];boundary=data['phase_boundary_pairs']
        body += ['<h2>评分输入冻结后补充的测量</h2><p>卡牌实体连续性：'+str(all_pairs['actual_input_pairs'])+'个真实输入前后对，'+str(all_pairs['retained_existing_card_instance_pairs'])+'/'+str(all_pairs['expected_existing_card_instance_pairs'])+'个实体观察对保留身份；其中'+str(boundary['actual_input_pairs'])+'次跨阶段、'+str(boundary['retained_existing_card_instance_pairs'])+'/'+str(boundary['expected_existing_card_instance_pairs'])+'个卡牌实例保留。重复观察对不代表同等数量的不同卡牌；单位物理ID和全局转换分母仍缺失。</p><p>该测量晚于本次评分输入冻结，未提供给当前评审，不回填其评分。</p>'+link(continuity,'完整有限实体观察对证据')]
    recovery = root/'post_freeze_measurements/recovery_cost.json'
    if recovery.is_file():
        data = read(recovery)
        body += ['<h3>实际拒绝后的返回成本</h3><p>重复取骰被拒绝后，返回可选手牌的行动界面：'
                 + str(data['recovery_clicks'])+'次点击，鼠标移动'
                 + format(data['pointer_distance_pixels'], '.2f')+' px；拒绝和返回全段正式规则状态不变。'
                 '起点为被拒绝点击结束时的鼠标位置，不包含该次失败尝试本身。仅代表这一条保存路径；未测撤销已消费卡牌、真人恢复时间或全局最短路径。该项同样晚于评分冻结。</p>'
                 + link(recovery, '完整拒绝恢复路径证据')]
    style='body{background:#142426;color:#e6ede6;font:16px/1.6 system-ui;margin:28px}main{max-width:1600px;margin:auto}a{color:#e5c58a}table{width:100%;table-layout:fixed;border-collapse:collapse}td,th{border:1px solid #52645b;padding:8px;vertical-align:top;overflow-wrap:anywhere}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}figure{margin:0}img{width:100%;display:block}@media(max-width:900px){.pair{grid-template-columns:1fr}}'
    (root/'index.html').write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>'+esc(meta['cycle'])+'</title><style>'+style+'</style><main>'+''.join(body)+'</main></html>',encoding='utf8')
    progress=dict(cycle=meta['cycle'],candidate=candidate,independent_review_verified=bool(result),reviewed_items=len(result['items']) if result else 0,
                  numeric_scores=sum(x['score'] is not None for x in result['items']) if result else 0,gate_verdict=result.get('gate_verdict') if result else 'pending')
    (root/'progress.json').write_text(json.dumps(progress,ensure_ascii=False,indent=2),encoding='utf8')
    return progress


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True)
    print(json.dumps(build(parser.parse_args().root.resolve()),ensure_ascii=False))
