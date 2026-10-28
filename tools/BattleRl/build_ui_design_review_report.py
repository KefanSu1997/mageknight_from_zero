"""Publish review evidence without promoting a rejected UI candidate."""
import html,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/UiFluency/design_review/v2_context_preserving'
def main():
    manifest=json.loads((OUT/'paired_manifest.json').read_text(encoding='utf8'))
    costs=json.loads((OUT/'cost_comparison.json').read_text(encoding='utf8'))
    reviews=[json.loads((OUT/f).read_text(encoding='utf8')) for f in ['final_review.json','independent_review.json']]
    names={'outside':'场外主界面','outside_mana':'场外魔力源','ranged':'远程/攻城','ranged_selected_card':'远程选牌','ranged_mana_open':'战斗内魔力源','ranged_mana_selected':'选中源骰','block':'格挡','block_units':'格挡部队页','assign_damage':'分配伤害','damage_units':'承伤部队页','melee_two_enemies':'多敌近战','melee_target':'近战目标选择','end':'战斗结束','end_discard_plan':'回合末弃牌计划'}
    data={'status':'REVISE_NOT_ACCEPTED','paired_screenshots':len(manifest['pairs']),'canvas':[1920,1080],'all_important_views_complete':False,'covered':list(names.values()),'pending':['非空非创伤弃牌与混合保留','源骰提交后的继续出牌','技能页','获胜战果','轮末与战术选择','奖励/供给及强制选择','长文本、最大敌群、手牌和源骰翻页、黑骰转换、额外源骰等边界状态'],'withdrawn_findings':['放大卡牌遮挡源骰控件：截图残留悬停，不能判为常驻遮挡'],'hard_failures':['源骰紧凑页遗漏部分临时魔力与支付提示'],'no_adoption':True}
    (OUT/'coverage_status.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
    pieces=['<!doctype html><meta charset="utf-8"><title>UI v2 对比审查</title><style>body{background:#111b20;color:#eee;font:17px system-ui;margin:30px}section{margin:30px 0;border-top:1px solid #59676d} .pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}img{width:100%}a{color:#b8d9fb}pre{white-space:pre-wrap} .fail{color:#ffb6a4} summary{cursor:pointer} h1{font-size:28px}</style><h1>UI v2：待修正，未采用</h1><p class="fail">历史审查曾将悬停放大图误判为常驻遮挡，该项结论撤回；紧凑魔力页的信息保留问题仍需独立审查。此版保留为历史候选，未采用。</p><p>14 组成对实际 Unity 截图；每对核对原生规则状态一致，1920×1080。不代表全部界面已经验收。</p>']
    pieces+=['<p><a href="coverage_status.json">覆盖与缺口</a> · <a href="final_review.json">审查一与评分</a> · <a href="independent_review.json">审查二与评分</a> · <a href="all_transition_costs.csv">1009种转移成本表</a> · <a href="cost_comparison.json">离线计算口径</a></p><p>离线代价基于 3833 条既有成功路线，覆盖历史转移质量的98.23%；其余拒绝或未知保持缺失。频率来自历史RL行为，不等于人类频率；未计算人类耗时。</p><table><tr><th>指标</th><th>基线</th><th>候选</th><th>变化</th></tr>']
    for key,title in [('b_clicks','B行为点击数'),('transition_px','A末次点击到B完成路径 px'),('panels','A+B面板切换次数')]:
        a=costs['versions']['baseline'][key];b=costs['versions']['candidate'][key]
        pieces.append(f'<tr><td>{title}</td><td>{a:.3f}</td><td>{b:.3f}</td><td>{(b/a-1)*100:.1f}%</td></tr>')
    pieces+=['</table><p>均为按历史分层权重计算的条件均值。面板打开仍计一次切换；无新增全屏复核页。337条样本的路径反而变长，不能只看平均收益；当前计算尚未因审查通过而正式采用。</p>']
    for pair in manifest['pairs']:
        label=pair['label'];context=pair['context']
        pieces.append(f'<section><h2>{html.escape(names.get(label,label))}</h2><p>同一存档 episode {context["episode"]} / step {context["step"]} · 规则状态一致</p><div class="pair">')
        for version,title in [('baseline','优化前基线'),('candidate','迭代候选')]:
            pieces.append(f'<div><p>{title}</p><a href="{version}/{label}.png"><img loading="lazy" src="{version}/{label}.png"></a><a href="{version}/{label}.json">状态、控件与SHA</a></div>')
        pieces.append('</div></section>')
    pieces.append('<section><h2>还需补齐的截图</h2><p>'+html.escape('；'.join(data['pending']))+'</p><p>缺少适用的重要界面，或必要信息、可读性未过门禁时，不得宣布整套UI优化完成。</p></section>')
    (OUT/'report.html').write_text(''.join(pieces),encoding='utf8')
    print(OUT/'report.html')
if __name__=='__main__':main()
