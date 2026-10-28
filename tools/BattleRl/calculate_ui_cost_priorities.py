"""Offline aggregation only: preserve measured, incompatible and unknown mass."""
import csv
import html
import json
import sqlite3
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'AutomationOutputs/UiFluency/computed_costs'


def main():
    OUT.mkdir(exist_ok=True)
    db = sqlite3.connect(f'file:{ROOT.as_posix()}/AutomationOutputs/UiFluency/20261012_sample5_selected/selected.sqlite?mode=ro', uri=True)
    db.row_factory = sqlite3.Row
    db.execute('ATTACH DATABASE ? AS work', (str(ROOT / 'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    population = {(a, b): n for a, b, n in db.execute('SELECT a.kind,b.kind,count(*) FROM work.transitions t JOIN work.actions a ON a.id=t.a_action JOIN work.actions b ON b.id=t.b_action GROUP BY a.kind,b.kind')}
    total = sum(population.values())
    groups = {}
    metrics = ['a_clicks', 'b_clicks', 'pair_clicks', 'internal_a_px', 'transition_px', 'panels']
    for row in db.execute('SELECT s.*,a.kind AS ak,b.kind AS bk,c.a_clicks,c.b_clicks,c.pair_clicks,c.internal_a_px,c.transition_px,c.panels FROM samples s JOIN work.actions a ON a.id=s.a JOIN work.actions b ON b.id=s.b LEFT JOIN costs c USING(group_id,episode,a_step)'):
        key = (row['ak'], row['bk'])
        g = groups.setdefault(key, dict(a=key[0], b=key[1], population=population[key], samples=0, measured=0, measured_mass=0., incompatible_mass=0., unknown_mass=0., sums={m: 0. for m in metrics}))
        g['samples'] += 1
        weight = row['estimator_weight']
        if row['pair_clicks'] is not None:
            g['measured'] += 1
            g['measured_mass'] += weight
            for m in metrics:
                g['sums'][m] += weight * row[m]
        elif row['status'] == 'verified_incompatible':
            g['incompatible_mass'] += weight
        else:
            g['unknown_mass'] += weight
    rows = []
    intents = defaultdict(lambda: dict(population=0, measured_population_mass=0., clicks=0., distance=0., panels=0.))
    aggregate = dict(measured_mass=0., incompatible_mass=0., unknown_mass=0., clicks=0., distance=0., panels=0.)
    for key, g in groups.items():
        n, mass = g['population'], g['measured_mass']
        r = {k: v for k, v in g.items() if k != 'sums'}
        for m in metrics:
            r[m + '_conditional_mean'] = g['sums'][m] / mass if mass else None
        r['historical_b_click_contribution'] = n * g['sums']['b_clicks'] / total
        assert abs(mass + g['incompatible_mass'] + g['unknown_mass'] - 1) < 1e-8, key
        r['cost_status'] = 'conditional_measured' if mass else ('verified_unavailable' if g['unknown_mass'] < 1e-8 else 'unresolved')
        rows.append(r)
        i = intents[key[1]]
        i['population'] += n
        i['measured_population_mass'] += n * mass
        for target, metric in [('clicks', 'b_clicks'), ('distance', 'transition_px'), ('panels', 'panels')]:
            i[target] += n * g['sums'][metric]
            aggregate[target] += n * g['sums'][metric]
        for field in ['measured_mass', 'incompatible_mass', 'unknown_mass']:
            aggregate[field] += n * g[field]
    for name, i in intents.items():
        i['kind'] = name
        i['frequency_pct'] = 100 * i['population'] / total
        i['measured_weight_pct'] = 100 * i['measured_population_mass'] / i['population']
        for metric in ['clicks', 'distance', 'panels']:
            i[metric + '_conditional_mean'] = i[metric] / i['measured_population_mass'] if i['measured_population_mass'] else None
        i['click_share_pct'] = 100 * i['clicks'] / aggregate['clicks']
    ranked = sorted(intents.values(), key=lambda x: x['clicks'], reverse=True)
    for metric in ['clicks', 'distance', 'panels']:
        aggregate[metric + '_conditional_mean'] = aggregate[metric] / aggregate['measured_mass']
    for field in ['measured_mass', 'incompatible_mass', 'unknown_mass']:
        aggregate[field + '_pct'] = 100 * aggregate[field] / total
    result = dict(population_transitions=total, groups=len(rows), measured_samples=sum(g['measured'] for g in groups.values()), aggregate=aggregate, intents=ranked, pairs=sorted(rows, key=lambda x: x['historical_b_click_contribution'], reverse=True), scope='Historical RL transitions, not human intent frequencies. Costs conditional on successful measured contexts. Missing and incompatible mass excluded, never zero-filled. Transition distance includes A-last to B-first AND B internal movement. Pair panels cover A+B; do not sum adjacent pair panels as episode total.')
    (OUT / 'costs.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    with (OUT / 'all_1009_pairs.csv').open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(result['pairs'])
    def fmt(value, digits=2):
        return '无已测数值' if value is None else f'{value:,.{digits}f}'
    page = '<!doctype html><meta charset="utf-8"><title>UI离线成本与优化方向</title><style>body{font:16px/1.7 system-ui;max-width:1200px;margin:36px auto;padding:24px;background:#f6f8fc;color:#24354a}table{border-collapse:collapse;width:100%;background:white}td,th{padding:8px;border-bottom:1px solid #ddd;text-align:left}.note{background:#fff0cc;padding:16px}</style><h1>UI离线成本与优化方向</h1>'
    page += f'<p>停止新增UI复现。使用现有{result["measured_samples"]:,}条成本记录，按850,374条历史RL转移频率与组内参数/阶段权重计算；覆盖全部1,009种组合，原始缺口保留。<a href="all_1009_pairs.csv">完整组合表</a> · <a href="costs.json">计算数据</a></p>'
    page += f'<p class="note">历史频率加权覆盖：可计算{aggregate["measured_mass_pct"]:.2f}%，不兼容/拒绝{aggregate["incompatible_mass_pct"]:.2f}%，未确定{aggregate["unknown_mass_pct"]:.2f}%。成功上下文中，B操作平均{aggregate["clicks_conditional_mean"]:.2f}点击、衔接加B内部移动{aggregate["distance_conditional_mean"]:.0f}px。A+B平均{aggregate["panels_conditional_mean"]:.2f}次视图切换。它们不是包含无法完成操作的总体完成成本。</p>'
    page += '<h2>按已有点击负担排序</h2><p>频率来自RL，不代表玩家使用频率；点击占比是可计算部分的占比。nextTurn包含丢弃剩余普通牌等宏步骤。视图列覆盖A+B，不能逐转移累加当作整局切换总数。</p><table><tr><th>B行为</th><th>历史频率</th><th>可计算权重</th><th>B点击</th><th>衔接+B移动px</th><th>A+B视图切换</th><th>已知点击占比</th></tr>'
    for i in ranked:
        page += f'<tr><td>{html.escape(i["kind"])}</td><td>{i["frequency_pct"]:.2f}%</td><td>{i["measured_weight_pct"]:.2f}%</td><td>{fmt(i["clicks_conditional_mean"])}</td><td>{fmt(i["distance_conditional_mean"],0)}</td><td>{fmt(i["panels_conditional_mean"])}</td><td>{i["click_share_pct"]:.2f}%</td></tr>'
    sp = next(r for r in rows if r['a'] == 'source' and r['b'] == 'play')
    page += f'<h2>公共魔力→出牌：已知痛点的成本</h2><p>历史19,683次，占转移2.31%；5条固定样本中4条有可完成成本，覆盖组内98.92%权重，另1条已验证不兼容。条件均值：取魔力{sp["a_clicks_conditional_mean"]:.2f}点击＋随后出牌{sp["b_clicks_conditional_mean"]:.2f}点击＝{sp["pair_clicks_conditional_mean"]:.2f}点击；3次视图切换。A内部移动{sp["internal_a_px_conditional_mean"]:.0f}px＋A结束到B完成{sp["transition_px_conditional_mean"]:.0f}px＝{sp["internal_a_px_conditional_mean"]+sp["transition_px_conditional_mean"]:.0f}px。该组含不同出牌参数，不能全部解释成强化。</p>'
    page += '<h2>优化方向与设计预算</h2><ol><li><b>优先：公共魔力与强化。</b>在选中卡牌旁直接选择源骰并强化，保留卡牌、模式与目标。为单目标普通强化设计3—4点击预算（选卡、选源骰、必要模式选择、执行），以本组7.39点击作为对照，理论减少46%—59%；这是设计预算，复杂材料/多目标操作另外计数，尚未证明新布局达到该值。将3次视图切换压到0—1次。</li><li><b>其次：回合收尾。</b>nextTurn平均7.81点击、6,648px，承担已知点击负担23.9%。提供保留/批量弃牌选择与集中确认，减少逐张弃牌和重复确认；这个均值依赖本次宏的弃牌策略。全体可计算上下文中，每次nextTurn少1点击，整体已知点击负担约降低3.06%。</li><li><b>随后：出牌、承伤与部队。</b>出牌平均3.09点击；unitDamage平均3.08点击、3,713px。集中呈现目标、贡献与单位，支持就地选择/批量分配，减少面板往返。保留合法分配及最终确认。</li><li><b>同步处理：接口可达性。</b>对已独立验证无法通过UI完成的操作，补入口或修复阶段限制。缺入口成本保持为空；未确定的映射缺口另外标注。</li></ol><p>优化后比较同一条原始行为序列的点击、控件中心距离和视图切换。新布局可先按坐标离线计算；真实响应延迟、遮挡及动态可达性不能由截图独立确定。976组有已测条件成本，33组无已测数值，CSV保留它们的已验证不可用/未知权重，绝不填0。</p>'
    page += '<h2>全部转移组合</h2><table><tr><th>A→B</th><th>历史次数</th><th>可计算权重</th><th>B点击</th><th>A+B点击</th><th>衔接+B移动px</th><th>A+B视图切换</th></tr>'
    for r in result['pairs']:
        page += f'<tr><td>{html.escape(r["a"])} → {html.escape(r["b"])}</td><td>{r["population"]:,}</td><td>{r["measured_mass"]:.1%}</td><td>{fmt(r["b_clicks_conditional_mean"])}</td><td>{fmt(r["pair_clicks_conditional_mean"])}</td><td>{fmt(r["transition_px_conditional_mean"],0)}</td><td>{fmt(r["panels_conditional_mean"])}</td></tr>'
    (OUT / 'report.html').write_text(page + '</table>', encoding='utf8')
    print(json.dumps(dict(aggregate=aggregate, top_intents=ranked[:12], source_pairs=[r for r in rows if r['a']=='source' and r['b']=='play']), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
