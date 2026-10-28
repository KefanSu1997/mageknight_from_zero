"""Conditional A->B costs from continuous actual pointer exchanges.

Anchor A at a successful native commit, B at the next successful native commit.
Clicks between anchors prepare B; B's commit click is counted separately.
This partition must not be added to old canonical per-action costs (which may
already include return/selection clicks). No edge crosses resets or sessions.
"""
import argparse
import collections
import csv
import hashlib
import html
import json
import math
from pathlib import Path
from structured_ui_ppo import ROOT, state


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def panel(obs):
    names={c['name'] for c in obs['controls'] if c['clickable']}
    if 'Btn_CloseTools' in names:return 'JourneyTools'
    if 'Btn_CloseBattleMenu' in names:return 'BattleMenu'
    paths=[c['path'] for c in obs['controls'] if c['clickable']]
    for p in ('ManaPayment','DeedInputs','AttackSelection','BlockSelection','JourneyHand','HeroStart','Camp'):
        if any('/'+p+'/' in x for x in paths):return p
    return 'Battle'


def native(obs):
    s=state(obs)
    return {k:v for k,v in json.loads(s.get('rl.sharedRuleState','{}')).items()
            if not k.startswith('ui')}


def classify(response, name):
    b,a=state(response['before']),state(response['observation'])
    rb,ra=native(response['before']),native(response['observation'])
    if not rb or not ra:return 'barrier','missing shared rule snapshot'
    if int(a.get('rejections',0))>int(b.get('rejections',0)):
        return 'barrier','native rejection retained separately'
    if name.startswith('Btn_Reset') or name in ('Btn_Camp','Btn_BattleHero'):
        return 'barrier','reset/setup is not a BattleRL action'
    if name in ('Btn_UseSource','Btn_UseExtraSource') and rb!=ra:
        return ('source' if name=='Btn_UseSource' else 'extraSource'),'actual mana acquisition'
    if name=='Btn_Sideways' and len(rb.get('hand',[]))>len(ra.get('hand',[])):
        return 'sideways','actual hand consumption'
    if name in ('Btn_Play','Btn_ConfirmInputs','Btn_ArtifactPlay') and int(a.get('played',0))>int(b.get('played',0)):
        return ('artifact' if name=='Btn_ArtifactPlay' else 'play'),'actual played increment'
    if name=='Btn_Next' and rb.get('phase')!=ra.get('phase'):
        return 'advance','actual phase transition'
    if name=='Btn_EndTurn' and rb!=ra:return 'nextTurn','actual end turn'
    if name=='Btn_Resolve' and rb!=ra:return 'attack','actual combat state change'
    if name.startswith('Btn_JourneyEncounter_') and ra.get('inBattle') and not rb.get('inBattle'):
        return 'fight','actual encounter entry'
    if rb!=ra:return 'barrier','unmapped native change; do not assume navigation'
    return None,'UI-only selection/navigation'


def edges_from_exchanges(paths, cohort):
    edges,failures,sources=[],[],[]
    anchor=None;between=[];last=None
    for path in sorted(paths):
        doc=json.loads(path.read_text(encoding='utf-8'))
        req,response=doc['request'],doc['response']
        sources.append(dict(path=str(path),sha256=sha(path)))
        if req['operation'] in ('observe','stop'):
            # Observe doesn't establish an input-action anchor; stop ends session.
            if req['operation']=='stop':anchor=None;between=[];last=None
            continue
        if req['operation']!='click' or not response.get('success'):
            anchor=None;between=[];last=None;continue
        name=req['path'].split('/')[-1]
        if last is not None and native(last['observation'])!=native(response['before']):
            failures.append(dict(path=str(path),reason='unobserved native-state discontinuity'))
            anchor=None;between=[]
        last=response
        kind,reason=classify(response,name)
        row=dict(path=str(path),name=name,kind=kind,actual=response)
        if kind=='barrier':
            failures.append(dict(path=str(path),button=name,reason=reason));anchor=None;between=[];continue
        if kind is None:
            if anchor:between.append(row)
            continue
        if anchor:
            route=between+[row]
            def point(x):return (x['actual']['click']['clickX'],x['actual']['click']['clickY'])
            points=[point(anchor)]+[point(x) for x in route]
            distance=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
            panels=sum(panel(x['actual']['before'])!=panel(x['actual']['observation']) for x in route)
            prep_distance=sum(math.dist(a,b) for a,b in zip(points[:-1],points[1:-1]))
            bstate=state(row['actual']['before'])
            edges.append(dict(cohort=cohort,from_kind=anchor['kind'],to_kind=kind,
                a_commit=anchor['path'],b_commit=str(path),
                a_end_panel=panel(anchor['actual']['observation']),b_commit_panel=panel(row['actual']['before']),
                a_phase=native(anchor['actual']['observation']).get('phase'),
                b_phase=native(row['actual']['before']).get('phase'),
                preparation_clicks=len(between),b_commit_clicks=1,total_clicks_after_a=len(route),
                preparation_buttons=[x['name'] for x in between],b_commit_button=name,
                distance_until_b_commit_px=distance,preparation_distance_px=prep_distance,
                final_approach_to_b_commit_px=math.dist(points[-2],points[-1]),
                panel_transitions_until_b_commit=panels,b_card_mode_before_commit=bstate.get('ui.cardMode'),
                measurement_scope='actual recorded click centres; intermediate noncommit UI operations may also prepare later actions; no human thinking/physical cursor/timing claim'))
        anchor=row;between=[]
    return edges,failures,sources


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--out',type=Path,default=ROOT/'AutomationOutputs/UiFluency/20261012_transition_costs')
    args=parser.parse_args()
    previous=ROOT/'AutomationOutputs/UiFluency/20261012_trajectory_priorities'
    out=args.out.resolve()
    out.mkdir(parents=True,exist_ok=False)
    old=json.loads((previous/'statistics.json').read_text(encoding='utf-8'))
    with (previous/'action_transitions.csv').open(encoding='utf-8-sig') as f:pairs=list(csv.DictReader(f))
    outgoing=collections.Counter()
    for r in pairs:outgoing[r['from']]+=int(r['count'])
    n=sum(outgoing.values())
    assert n==old['steps']-old['episodes'],'Only within-episode adjacent edges'
    for r in pairs:
        r['count']=int(r['count']);r['p_b_given_a_has_successor']=r['count']/outgoing[r['from']]
        r['share_of_all_transitions']=r['count']/n
        r['ui_cost']=None
    cohorts=[]
    cohorts += [(p,'public_source_script') for p in sorted((ROOT/'AutomationOutputs/UiFluency/20261012_public_source_actual_retry').glob('trial_*/exchanges'))]
    cohorts += [(p,'baseline_script') for p in sorted((ROOT/'AutomationOutputs/UiFluency/20261012_baseline/actual_ready').glob('trial_*/exchanges'))]
    cohorts += [(p,'fresh_context_reproduction') for p in sorted((ROOT/'AutomationOutputs/UiFluency/20261012_transition_context_reproduction').glob('trial_*/exchanges'))]
    for run in ('20261011/train_verified','20261011/frozen_live','20261011_frozen_text/train_live',
                '20261011_frozen_text/frozen_live','20261011_frozen_text/frozen_deed_inputs'):
        cohorts += [(p,'historical_ui_policy:'+run) for p in sorted((ROOT/'AutomationOutputs/StructuredUiPpo'/run).glob('episode_*/exchanges'))]
    measured,barriers,files=[],[],[]
    for path,cohort in cohorts:
        e,b,s=edges_from_exchanges(path.glob('*.json'),cohort)
        measured.extend(e);barriers.extend(b);files.extend(s)
    groups=collections.defaultdict(list)
    for e in measured:groups[(e['cohort'],e['from_kind'],e['to_kind'],e['a_end_panel'],e['b_phase'])].append(e)
    grouped=[]
    for k,rows in groups.items():
        grouped.append(dict(cohort=k[0],from_kind=k[1],to_kind=k[2],a_end_panel=k[3],b_phase=k[4],n=len(rows),
            preparation_clicks_min=min(x['preparation_clicks'] for x in rows),
            preparation_clicks_max=max(x['preparation_clicks'] for x in rows),
            distance_px_min=min(x['distance_until_b_commit_px'] for x in rows),
            distance_px_max=max(x['distance_until_b_commit_px'] for x in rows),
            panel_transitions_min=min(x['panel_transitions_until_b_commit'] for x in rows),
            panel_transitions_max=max(x['panel_transitions_until_b_commit'] for x in rows)))
    result=dict(historical_episodes=old['episodes'],historical_rule_steps=old['steps'],
        historical_transitions=n,transition_types=len(pairs),historical_pairs=pairs,
        actual_edges=measured,actual_grouped=grouped,barriers=barriers,
        definition='A last successful native commit -> B next successful native commit; intermediate clicks prepare B; B commit counted once',
        caveats=['Kinds are observable actions, not subjective player intent.',
            'P(B|A) denominator only A occurrences with a successor, not all A occurrences.',
            'Historic aggregate pairs lack UI state; actual UI costs are contextual examples, not a population average.',
            'Canonical standalone source previously included close; semantic-commit source ends at UseSource. Never add both accounting schemes.',
            'Preparation is not automatically redundant; card selection, targeting and strategic choices can be necessary.',
            'Intermediate clicks can inspect information or prepare a later C, not necessarily prepare B; labels alone do not infer subjective intent.',
            'Resets, rejections, unknown native changes and sessions break edges and remain in barriers.',
            'No RPC settle time or elapsedSeconds is interpreted as human transition latency.'])
    (out/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    with (out/'source_files.jsonl').open('w',encoding='utf-8') as f:
        for s in files:f.write(json.dumps(s)+'\n')
    with (out/'transition_distribution.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(pairs[0]));w.writeheader();w.writerows(pairs)
    def table(head,rows):
        return '<table><tr>'+''.join('<th>'+html.escape(str(x))+'</th>' for x in head)+'</tr>'+''.join('<tr>'+''.join('<td>'+html.escape(str(x))+'</td>' for x in row)+'</tr>' for row in rows)+'</table>'
    page='''<!doctype html><html lang="zh"><meta charset="utf-8"><title>A→B 衔接代价</title><style>
body{font:16px/1.65 system-ui;background:#f5f7fa;color:#24354a;max-width:1100px;margin:40px auto;padding:0 24px}table{width:100%;border-collapse:collapse;background:white;margin:20px 0}th,td{padding:10px;text-align:left;border-bottom:1px solid #dce2eb}.note{background:#fff0ce;padding:14px}code{background:white;padding:4px}details{margin-top:20px}</style>'''
    page+='<h1>从 A 完成到 B 执行：条件交互成本</h1>'
    page+=f'<p>原历史分母 {old["episodes"]:,} 条非空轨迹、{old["steps"]:,} 规则步。仅同一轨迹内相邻动作，共 {n:,} 次转移、{len(pairs):,} 种 Kind 组合。850,374 = 867,821 − 17,447，未把不同局的末尾和开头相接。</p>'
    page+='<p class="note">成本应依赖 A 结束后的真实 UI 状态：当前面板、选牌、模式、目标、分页及鼠标位置。相同 source→play，已选牌且已强化，与尚未选牌的成本不同。旧规则轨迹没有这些 UI 信息，不能把少数 UI 实例的成本乘全部历史频次，当成实际平均成本。</p>'
    page+='<h2>怎样计算</h2><p>先确定统一边界：A 在最后一次成功规则提交结束，B 在下一次成功规则提交执行。两者之间的选牌、返回、切面板等非提交点击单列；B 的提交点击计1次。内部操作可以分成“核心提交”和“依赖上下文的准备”，每一次实际点击只分配一次。既有独立任务成本常含返回/选牌，不可以再加整段衔接成本。</p>'
    page+='<p><code>移动距离 = Σ‖pᵢ − pᵢ₋₁‖</code>，从 A 提交点击位置开始，经过所有中间点击，到 B 提交点击位置；界面切换数取这个区间内实际面板边界的次数。时间只计界面响应、等待输入解锁等系统延迟；思考、脚本等待或 RPC 延迟不能当 UI 卡顿。目标/牌/强化模式重选次数需要再区分“必要新目标”与“同一未完成意图丢失后重建”。</p>'
    def coverage(a,b):
        actual=[e for e in measured if e['from_kind']==a and e['to_kind']==b]
        if not actual:return '尚未 UI 复现；不是无法计算'
        return f'已测{len(actual)}个条件实例；中间点击{min(e["preparation_clicks"] for e in actual)}–{max(e["preparation_clicks"] for e in actual)}，B提交1次；不能当全部历史上下文平均'
    page+='<h2>高频转移</h2>'+table(['A→B','次数','占有后继的 A','UI 成本状态'],[[x['from']+' → '+x['to'],f"{x['count']:,}",f"{x['p_b_given_a_has_successor']:.2%}",coverage(x['from'],x['to'])] for x in pairs[:22]])
    selected=[x for x in grouped if x['cohort'] in ('public_source_script','baseline_script') and x['from_kind'] in ('source','sideways')]
    page+='<h2>从实际连续点击中补算的衔接</h2>'+table(['A→B','实例数','中间点击','B提交','移动距离到B提交','面板切换'],[[x['from_kind']+' → '+x['to_kind'],x['n'],str(x['preparation_clicks_min'])+'–'+str(x['preparation_clicks_max']),1,f"{x['distance_px_min']:.0f}–{x['distance_px_max']:.0f} px",str(x['panel_transitions_min'])+'–'+str(x['panel_transitions_max'])] for x in selected])
    page+='<p>source→play 两个实例：A 是 Btn_UseSource 实际获得白标记的点击，随后 CloseTools 返回，再 Btn_Play 支付。中间1点击，B提交1点击，共1106.5px/1次面板切换。原牌与强化模式已提前选好且保留；此结论只适用于这一上下文。若把 A 定义为“取骰并返回”，返回已计入A，剩余到出牌按钮的首段约665.1px，不能再重复收一次返回成本。</p>'
    fresh=[]
    for path in sorted((ROOT/'AutomationOutputs/UiFluency/20261012_transition_context_reproduction').glob('trial_*/cases.json')):
        for c in json.loads(path.read_text(encoding='utf-8'))['cases']:
            if c['completed'] and c['intent_id'].startswith('source_to_play'):
                start=next(i for i,x in enumerate(c['steps']) if x['name']=='Btn_UseSource')
                route=c['steps'][start:]
                pts=[(x['actual']['click']['clickX'],x['actual']['click']['clickY']) for x in route]
                fresh.append(dict(trial=path.parent.name,context=c['intent_id'],
                    intermediate_clicks=len(route)-2,b_commit_clicks=1,
                    distance_px=sum(math.dist(a,b) for a,b in zip(pts,pts[1:])),
                    panel_transitions=sum(panel(x['actual']['before'])!=panel(x['actual']['observation']) for x in route[1:]),
                    buttons=[x['name'] for x in route[1:]],source=str(path),source_sha256=sha(path)))
    (out/'fresh_context_costs.json').write_text(json.dumps(fresh,ensure_ascii=False,indent=2),encoding='utf-8')
    if fresh:
        page+='<h2>新增同起点配对 UI 复现：可以算出具体成本</h2><p>两个新开局，分别用真实阶段重置核对相同公开规则状态与骰面，再跑已选牌和未选牌两条路径。当前实例是Ranged阶段原敏捷，费用来自白骰或白天金骰，四个完整流程均8点击且最终规则状态相等。不同点是选牌/强化发生在取魔力之前还是之后，导致A→B间隔不同，而整条意图没有多出点击。</p>'
        page+=table(['开局','A结束时上下文','中间操作','B提交','距离','界面切换'],[[x['trial'],'已选牌/强化' if x['context'].endswith('preselected') else '尚未选牌',x['intermediate_clicks'],x['b_commit_clicks'],f"{x['distance_px']:.0f}px",x['panel_transitions']] for x in fresh])
        page+='<p>这说明之前“未知”是尚未按上下文复现，不是无法计算。现在应显示“已复现的具体条件与数值”或“尚未复现”。相同原生A→B仍可能因卡位、源骰颜色/位置、材料和选择状态而异；实际轨迹逐条复现才能得到这批轨迹在当前UI的整体投影，少数条件测量不能称全部历史实际遥测。</p>'
    page+='<p>sideways→sideways 15个脚本实例：上次确认在右侧，下一张牌在左侧，再到右侧确认，3274.5px。之前单张横置1637px只覆盖“选牌→确认”，遗漏了从 A 结束点到 B 第一张牌的首段。这说明连续操作的布局负担不能只看单动作内部距离。当前样本都采用相同左侧首张选择，不能推断其他手牌位置也相同。</p>'
    page+='<p>sideways→attack 9个脚本实例：中间选目标1次，或另加贡献分配/全选/完成共4次，再执行攻击1次；1713–3167px、0–2次面板切换。这是不同可选路径，不自动把详细分配叫多余操作。</p>'
    page+='<h2>优化排序如何变化</h2><ol><li>按“高频转移 × 可避免衔接成本”看热点，而不是只按高频按钮排序。先定位 source↔play、play→play、sideways→sideways 的面板往返与确认位置。</li><li>公共魔力从已选牌上下文直接取用，并保留原牌/强化/目标，值得做同任务 A/B。当前两个实例已保留选择，主要负担是返回与跨屏距离。</li><li>连续出牌/横置可测试就近确认或快捷键，验证新的鼠标路线与误操作，不把按钮数少当全部流畅度。</li><li>unitDamage→unitDamage 57,744次，占有后继承伤预选73.83%。可能是策略规划循环，也可能是有用改计划，需要对相同攻击目标/顺序变化/实际提交进行细分，不自动惩罚所有改选。</li></ol>'
    page+=f'<p>实际记录提取 {len(measured)} 个受支持提交间隔；重置、拒绝和未映射的规则变化均作为边界隔断，并留在 barriers。已知成本以当前/历史 UI 实例分层保存。数据见 results.json、source_files.jsonl（输入 SHA）、transition_distribution.csv。</p>'
    page+='<details><summary>全部实际条件组</summary>'+table(['层','A','B','A结束面板','B阶段','样本数','中间点击范围','距离范围'],[[x['cohort'],x['from_kind'],x['to_kind'],x['a_end_panel'],x['b_phase'],x['n'],str(x['preparation_clicks_min'])+'–'+str(x['preparation_clicks_max']),f"{x['distance_px_min']:.0f}–{x['distance_px_max']:.0f}"] for x in grouped])+'</details></html>'
    (out/'report.html').write_text(page,encoding='utf-8')
    print(json.dumps(dict(transitions=n,types=len(pairs),actual_edges=len(measured),
        targeted=[x for x in grouped if x['cohort'] in ('public_source_script','baseline_script') and x['from_kind'] in ('source','sideways')]),ensure_ascii=False))


if __name__=='__main__':main()
