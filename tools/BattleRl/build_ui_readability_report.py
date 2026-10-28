"""Publish the actual readability regression captures and complete cost ledger."""
import argparse,hashlib,html,json
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--version',default='v9');args=p.parse_args();out=args.out.resolve()
    read=lambda rel:json.loads((out/rel).read_text(encoding='utf8'))
    costs=read('complete_cost_summary.json');manifest=read('phases_01/paired_manifest.json');gate=read('phases_01/design_constraints.json');runtime=read('runtime_loading_audit.json')
    supplemental=(out/'summon_01/paired_manifest.json').exists()
    if supplemental:
        extra=read('summon_01/design_constraints.json')
        manifest['pairs']+=read('summon_01/paired_manifest.json')['pairs']
        gate['errors']+=extra['errors'];gate['candidate_truncated']+=extra['candidate_truncated'];gate['baseline_truncated']+=extra['baseline_truncated'];gate['shape_checks']+=extra['shape_checks']
    assert not gate['errors'] and not gate['candidate_truncated']
    assert runtime['loaded_bridge_v3'] and runtime['candidate_source_sha256']==read('cost_comparison.json')['design_source_sha256']['PveBattleController.cs']
    single_review='capture_01/review_three_enemies.json' if (out/'capture_01/review_three_enemies.json').exists() else 'phases_01/review_three_enemies.json'
    pending=[];review_paths=[single_review,'phases_01/review_phases.json']
    if supplemental:review_paths.append('summon_01/review_phases.json')
    if any(not (out/r).exists() for r in review_paths):pending.append('新版关键阶段截图的独立审查尚未全部完成')
    reviewed=set();blocking=[];score_rows=[];review_hashes={}
    for rel in review_paths:
        if not (out/rel).exists():continue
        review=read(rel);review_hashes[rel]=hashlib.sha256((out/rel).read_bytes()).hexdigest()
        inspected={Path(label).stem for label in review.get('actual_labels_inspected',[])}
        reviewed|=inspected;blocking+=review.get('blocking_findings',[])
        records=review.get('pairs',[])
        if not records and 'baseline' in review and len(inspected)==1:
            records=[dict(label=next(iter(inspected)),baseline=review['baseline'],candidate=review['candidate'])]
        score_rows+=records
    missing_reviews=sorted({r['label'] for r in manifest['pairs']}-reviewed)
    if missing_reviews and len(review_hashes)==len(review_paths):pending.append('独立审查标签未完整覆盖本轮截图')
    if blocking:pending.append('独立审查仍有未解决的阻断发现')
    pending.append('全历史操作中原UI无法完成部分的最终验收口径仍待用户明确；不能将其填0或当成有限成本')
    status=dict(version='v9_enemy_readability',adopted=False,goal_complete=False,actual_runtime_loaded=True,phase_pairs=len(manifest['pairs']),matched_control_dimensions=gate['shape_checks'],candidate_truncation_errors=gate['candidate_truncated'],baseline_truncation_findings=gate['baseline_truncated'],no_added_full_canvas_page=gate['no_added_full_canvas_page'],click_reduction_on_finite_cost_rows=costs['reductions']['b_clicks'],path_reduction_on_finite_cost_rows=costs['reductions']['transition_px'],unresolved_population_weight=costs['unresolved_weight'],pending=pending)
    status['version']=args.version+'_enemy_readability'
    status['independent_review']=dict(reviewed_labels=sorted(reviewed),missing_labels=missing_reviews,blocking_findings=blocking,review_sha256=review_hashes)
    status['candidate_visual_gates_pass']=not missing_reviews and not blocking and not gate['errors'] and not gate['candidate_truncated']
    (out/'acceptance_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf8')
    esc=html.escape
    body=['<!doctype html><meta charset="utf-8"><title>v9 实际界面与成本</title><style>body{background:#111b20;color:#eee;font:17px system-ui;margin:30px}h1{font-size:28px}a{color:#b8d9fb}section{border-top:1px solid #59676d;margin-top:28px;padding-top:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}img{width:100%}td,th{text-align:left;padding:10px}.pending{color:#ffdb9b}</style><h1>v9：实际可读性修正，尚未采用</h1><p>目标保持为点击降低30%、路径降低20%、界面不增加、各阶段无明显信息损失。当前有限成本指标达标；整体目标尚未宣告完成。</p><p>Unity按用户要求重启后，已验证新版程序集加载、恢复本地MCP连接，Console错误0。保留原怪物图片、卡牌、点击区域和字号，将怪物属性分行并补全“刺杀”中文标签。v8真实失败截图与各历史版本均保留。</p>']
    body.append('<p><a href="runtime_loading_audit.json">运行程序集与加载证据</a> · <a href="phases_01/design_constraints.json">尺寸、截断与界面约束</a> · <a href="acceptance_status.json">当前验收状态</a> · <a href="../v7_hand_commit/report.html">前一版64组完整对比及独立审查</a> · <a href="../v8_enemy_readability/capture_02/validation.json">v8已加载但截断的失败记录</a> · <a href="../../design_versions/registry.json">全部版本归档</a></p>')
    body.append(f'<p>本轮重采{len(manifest["pairs"])}组关键阶段，{gate["shape_checks"]}个同路径卡牌／怪物／部队点击区域尺寸一致。候选文字截断0；基线的怪物属性截断仍保留作对照。这里只把本轮原图称为v9截图，旧64组保存为上轮档案。鼠标悬浮放大属临时状态，未当常驻缺失。</p>')
    if supplemental:
        body.append('<p>补查17447局的构筑：102局的召唤可能将三敌扩展为四名防御敌人。因此另保存同一历史存档的四敌格挡、四敌承伤配对，未以三敌截图替代拥挤场景。<a href="enemy_count_scope_audit.json">敌人数量与召唤范围审计</a> · <a href="summon_01/design_constraints.json">四敌尺寸与文字检查</a>。源代码命名面板新增局部源骰托盘1个、移除TurnReview 1个，净增0；没有新增全屏页面。</p>')
    body.append('<section><h2>重新计算后的操作负担</h2><p><a href="complete_transition_costs.csv">全部1009种转移成本表</a> · <a href="complete_cost_summary.json">分母与证据分类</a> · <a href="uncovered_complete_resolutions.json">250条原缺口的逐条结论</a></p><table><tr><th>有限完成成本的历史频率加权均值</th><th>基线</th><th>v9</th><th>降低</th></tr>')
    for key,title in [('b_clicks','B行为点击数'),('transition_px','A末击至B完成路径（px）')]:
        a=costs['versions']['baseline'][key];b=costs['versions']['candidate'][key];body.append(f'<tr><td>{title}</td><td>{a:.4f}</td><td>{b:.4f}</td><td>{costs["reductions"][key]*100:.2f}%</td></tr>')
    body.append(f'</table><p>历史转移总数{costs["population"]:,}，有限成本权重{costs["finite_weight"]:,.2f}，原UI无法完成权重{costs["unavailable_weight"]:,.2f}，尚未分类权重{costs["unresolved_weight"]:,.2f}。无法完成的项没有有限的完成成本，保持空值，未用0补齐。195条证据仅证明基线不可完成；另49条有两版源码依据，不能混称候选可达性已全部测过。</p><p>频率来自历史RL，不等于人类行为频率。3833条既有路线与4条只验证UI选择的规划、2条几何投影分开标记；不推算人类耗时。v9沿用v7操作布局，仅调整怪物文字，重新计算结果一致。无新增全屏页面；新增局部源骰面板在源代码清单中单列。旧面板分类器漏掉部分既有特殊页，因此未混合其切换均值作界面数量证明。</p></section>')
    reviews=[r for r in review_paths if (out/r).exists()]
    body.append('<p>新版独立审查：'+(' · '.join(f'<a href="{r}">{esc(Path(r).stem)}</a>' for r in reviews) or '进行中')+'</p>')
    if score_rows:
        body.append('<table><tr><th>独立截图评分（主观0—10）</th><th>信息：前→后</th><th>美感：前→后</th></tr>')
        for row in score_rows:
            a,b=row['baseline'],row['candidate'];ai=a.get('information_acquisition',a.get('information'));bi=b.get('information_acquisition',b.get('information'))
            body.append(f'<tr><td>{esc(row["label"])}</td><td>{ai} → {bi}</td><td>{a["aesthetics"]} → {b["aesthetics"]}</td></tr>')
        body.append('</table><p>分数仅适用于对应原图；两位审查者的评分尺度可能不同，不混作全游戏质量分。候选源骰面板占用原英雄大图区域，必要英雄数值仍可读；此设计差异与怪物文字排版本身分别评价。</p>')
    names={'ranged':'远程／攻城','block':'格挡','assign_damage':'分配伤害','melee_two_enemies':'双敌近战','won_result':'胜利战果','three_enemy_details':'三敌属性与能力','split_block_after_first':'分次格挡后的剩余攻击'}
    names.update(four_enemy_block='召唤扩展后的四敌格挡',four_enemy_damage='召唤扩展后的四敌承伤')
    for pair in manifest['pairs']:
        label=pair['label'];c=pair['context'];body.append(f'<section><h2>{esc(names.get(label,label))}</h2><p>同一存档 {c["episode"]}/{c["step"]}，原生状态相等；仅恢复截图，未点击规则结算。</p><div class="pair">')
        for version,title in [('baseline','原始基线'),('candidate','v9候选')]:
            rel=Path(pair[version]).relative_to(out).as_posix();body.append(f'<div><p>{title}</p><a href="{rel}"><img src="{rel}" loading="lazy"></a><a href="{Path(rel).with_suffix(".json").as_posix()}">状态与SHA</a></div>')
        body.append('</div></section>')
    body.append('<section><h2>尚待完成</h2><p class="pending">'+esc('；'.join(pending))+'</p></section>')
    (out/'report.html').write_text(''.join(body).replace('v9',esc(args.version)),encoding='utf8');print(str(out/'report.html'))

if __name__=='__main__':main()
