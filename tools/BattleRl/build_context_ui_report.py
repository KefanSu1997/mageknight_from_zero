"""Actual paired screenshot gallery; incomplete goals remain explicitly incomplete."""
import argparse,html,json
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);p.add_argument('--version',required=True);a=p.parse_args();out=a.out.resolve()
    manifest=json.loads((out/'paired_manifest.json').read_text(encoding='utf8'));cost=json.loads((out/'cost_comparison.json').read_text(encoding='utf8'))
    checks=[]
    for pair in manifest['pairs']:
        for v in ('baseline','candidate'):
            r=json.loads(Path(pair[v]).with_suffix('.json').read_text(encoding='utf8'));s={v['key']:v['value'] for v in r['observation']['state']}
            checks.append(dict(label=r['label'],version=v,hover=s.get('ui.hoverPreviewVisible'),declared_hover=bool(pair.get('hover_preview',False)),truncated=s.get('ui.truncated',''),discard_slots=r.get('discard_slots',[])))
    labels={r['label'] for r in manifest['pairs']}
    pending=['全部独立截图审查与界面清单最终验收','未覆盖成本的计算与全量目标核对（条件均值达标不能代替全量达标）']
    if not {'level_reward_required','unit_supply'}<=labels:pending.append('奖励/供给等适用的强制选择界面覆盖核对')
    if 'long_hand_page_2' not in labels:pending.append('长手牌翻页等可达边界状态')
    for label,description in [('gold_payment_action_2','金色魔力实际付款'),('black_extra_taken','黑骰转换及额外源骰正式提交'),('empty_discard_ended','全创伤手牌从手牌页直接结束回合')]:
        if label not in labels:pending.append(description)
    if cost['versions']['candidate']['b_clicks']>cost['versions']['baseline']['b_clicks']*.7:pending.append('点击数降低30%')
    if cost['versions']['candidate']['transition_px']>cost['versions']['baseline']['transition_px']*.8:pending.append('鼠标路径降低20%')
    status=dict(status='CANDIDATE_NOT_ADOPTED',canvas=[1920,1080],paired_screenshots=len(manifest['pairs']),capture_errors=manifest['errors'],all_important_views_complete=False,primary_images_no_hover=all(x['hover']=='False' for x in checks if not x['declared_hover']),declared_hover_matches=all(x['hover']==str(x['declared_hover']) for x in checks),no_text_truncation=all(not x['truncated'] for x in checks),pending=pending,capture_checks=checks)
    (out/'coverage_status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf8')
    esc=html.escape
    body=['<!doctype html><meta charset="utf-8"><title>'+esc(a.version)+' UI 对比</title><style>body{background:#111b20;color:#eee;font:17px system-ui;margin:30px}section{margin:30px 0;border-top:1px solid #59676d}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}img{width:100%}a{color:#b8d9fb}.pending{color:#ffdb9b}td,th{padding:8px;text-align:left}h1{font-size:28px}</style><h1>'+esc(a.version)+' · 迭代候选，尚未采用</h1><p class="pending">完整目标：点击数降低30%、鼠标路径降低20%、界面数量不增加、各阶段无明显信息损失。目前尚未证明全部满足；默认仍为基线。</p><p>卡牌放大由鼠标悬停触发，移开即消失。旧截图的真实鼠标残留在手牌上，不能将该临时状态判为正常取魔力时的常驻遮挡。本组主态截图已逐张排除悬停；悬停场景单独补存。</p>']
    body.append(f'<p>{len(manifest["pairs"])} 组成对实际 Unity 截图，逐对核对原生规则状态与有序物理弃牌槽一致，1920×1080。无悬停：{status["primary_images_no_hover"]}；文字无溢出：{status["no_text_truncation"]}；采集错误：{len(manifest["errors"])}。不代表全界面已验收。</p>')
    inherited=sum(bool(pair.get('inherited_from')) for pair in manifest['pairs'])
    if inherited:body.append(f'<p>其中{len(manifest["pairs"])-inherited}组为本次重采；{inherited}组明确沿用上一版本未受本次代码改动影响的界面原图。清单保留来源，未将沿用图声称为新采集。</p>')
    body.append('<p><a href="coverage_status.json">覆盖与缺口</a> · <a href="paired_manifest.json">配对清单</a> · <a href="all_transition_costs.csv">1009种转移成本表</a> · <a href="cost_comparison.json">离线口径与来源SHA</a></p>')
    if (out/'goal_coverage_audit.json').exists() and not (out/'complete_cost_summary.json').exists():
        audit=json.loads((out/'goal_coverage_audit.json').read_text(encoding='utf8'))
        sensitivity=next(r['full_population_reduction'] for r in audit['unchanged_missing_click_sensitivity'] if r['assumed_old_and_new_missing_clicks']==3)
        body.append(f'<p class="pending"><a href="goal_coverage_audit.json">全量目标核对</a>：{100*audit["covered_click_reduction"]:.2f}%仅为已覆盖路线的降幅。若缺失部分前后均为3点击，全量降幅约{100*sensitivity:.2f}%（仅敏感性假设，不是测量结果）。尚不能宣称全量点击目标达成。</p>')
    if (out/'complete_cost_summary.json').exists():
        merged=json.loads((out/'complete_cost_summary.json').read_text(encoding='utf8'))
        planned=merged['evidence'].get('offline_planned_UI_verified',{}).get('samples',0)
        body.append(f'<section><h2>最新补全成本账本</h2><p><a href="complete_transition_costs.csv">全部1009种组合及可完成/不可完成/未知权重</a> · <a href="complete_cost_summary.json">汇总与证据分类</a> · <a href="uncovered_complete_resolutions.json">原250条缺口逐条依据</a></p><p>新增{planned}条UI选择验证路线、2条离线几何投影，均与3833条既有执行证据分列。可计算权重 {merged["finite_weight"]:,.2f} / {merged["population"]:,}；原界面无法完成的权重 {merged["unavailable_weight"]:,.2f}；仍未解决 {merged["unresolved_weight"]:,.2f}。未用0填补后两类。</p><p>加入补算后的有限成本条件均值：B点击 {merged["versions"]["baseline"]["b_clicks"]:.4f} → {merged["versions"]["candidate"]["b_clicks"]:.4f}（降低 {100*merged["reductions"]["b_clicks"]:.2f}%）；A末击至B完成路径 {merged["versions"]["baseline"]["transition_px"]:.1f} → {merged["versions"]["candidate"]["transition_px"]:.1f}px（降低 {100*merged["reductions"]["transition_px"]:.2f}%）。无法完成的操作没有有限完成成本，因此这仍不是全历史总体的有限均值。</p><p>补充路线涉及既有读心结果页与神器页；旧面板分类器遗漏这两类，故未合并新旧面板切换均值。界面数量约束须独立按页面清单与源代码差异核对。</p></section>')
    reviews=sorted((set(out.glob('*review.json'))|set(out.glob('review_*.json')))-{out/'review_inheritance.json'});body.append('<p>本版独立审查：'+(' · '.join(f'<a href="{r.name}">{esc(r.stem)}</a>' for r in reviews) if reviews else '尚未完成，不得宣布通过。')+'</p>')
    if (out/'review_inheritance.json').exists():
        body.append('<p><a href="review_inheritance.json">沿用图的历史独立审查范围与来源SHA</a>：仅适用于未改动的同一截图，不是另一位审查者或一次新审查。</p>')
    if (out/'visual_review_coverage.json').exists():
        coverage=json.loads((out/'visual_review_coverage.json').read_text(encoding='utf8'))
        body.append(f'<p><a href="visual_review_coverage.json">逐对审查覆盖核对</a>：{coverage["covered_pairs"]}/{coverage["pairs"]}组已有实际审查记录，沿用图须字节SHA完全一致。此计数不代表所有发现已解决。</p>')
    body.append('<p>离线计算基于3833条既有成功路线，覆盖历史转移质量98.23%；拒绝或未知不填0。频率来自历史RL行为，不等于人类频率；不推算人类耗时。A→B路径包括A末次点击到B首击及B内部移动；成对点击数不能在连续轨迹重复累加。</p><table><tr><th>加权条件均值</th><th>基线</th><th>候选</th><th>降低</th></tr>')
    for k,t in [('b_clicks','B行为点击数'),('transition_px','A末次点击到B完成路径 px'),('panels','A+B面板切换次数')]:
        x=cost['versions']['baseline'][k];y=cost['versions']['candidate'][k];body.append(f'<tr><td>{t}</td><td>{x:.3f}</td><td>{y:.3f}</td><td>{100*(1-y/x):.1f}%</td></tr>')
    body.append(f'</table><p>{cost["distance_regression_samples"]}条样本的路径增长，保留在逐路线数据中。未新增全屏页面；局部面板与控件的变化仍计入切换，不通过改名隐藏代价。全界面清单仍待完整核对。</p>')
    for pair in manifest['pairs']:
        label=pair['label'];c=pair['context'];role='悬停阅读状态（临时预览）' if pair.get('hover_preview') else '无悬停主状态';body.append(f'<section><h2>{esc(label)}</h2><p>同一存档 episode {c["episode"]} / step {c["step"]} · 规则状态与弃牌槽一致 · {role}</p><div class="pair">')
        if pair.get('comparison_limit'):body.append('<p class="pending">对比范围：'+esc(pair['comparison_limit'])+'</p>')
        for v,t in [('baseline','优化前基线'),('candidate','迭代候选')]:body.append(f'<div><p>{t}</p><a href="{v}/{label}.png"><img loading="lazy" src="{v}/{label}.png"></a><a href="{v}/{label}.json">状态、控件与SHA</a></div>')
        body.append('</div></section>')
    body.append('<section><h2>尚待补齐</h2><p>'+esc('；'.join(pending))+'</p></section>');(out/'report.html').write_text(''.join(body),encoding='utf8');print(out/'report.html')
if __name__=='__main__':main()
