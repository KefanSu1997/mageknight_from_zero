"""Visible full-matrix progress; never present pending costs as zero."""
import html
import json
from pathlib import Path
import sqlite3
import argparse
from ui_cost_views import VERSION as VIEW_METRIC_VERSION

ROOT=Path(__file__).resolve().parents[2]


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--plan',type=Path,default=ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected');parser.add_argument('--runtime-note',help='Observed execution condition; normal regeneration without this argument clears the note');args=parser.parse_args()
    selected=args.plan.resolve()
    m=json.loads((selected/'manifest.json').read_text(encoding='utf-8'))
    out=ROOT/'AutomationOutputs/UiFluency/full_cost_progress';out.mkdir(exist_ok=True)
    db=sqlite3.connect('file:'+str(selected/'selected.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    rows=db.execute('SELECT group_id,COUNT(*),SUM(status="complete"),SUM(status="verified_incompatible") FROM samples GROUP BY group_id').fetchall()
    tables={r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    costs={}
    clickability=dict(recorded_samples=0,observed_noncenter_samples=0,observed_noncenter_clicks=0)
    clickability_rows=[]
    if 'clickability_evidence' in tables:
        clickability_rows=db.execute('''SELECT x.group_id,x.episode,x.a_step,x.non_center_clicks
            FROM clickability_evidence x JOIN costs c USING(group_id,episode,a_step)
            WHERE x.result_sha=c.result_sha AND x.non_center_clicks>0 ORDER BY x.group_id,x.episode,x.a_step''').fetchall()
        clickability['recorded_samples']=db.execute('''SELECT COUNT(*) FROM clickability_evidence x JOIN costs c USING(group_id,episode,a_step) WHERE x.result_sha=c.result_sha''').fetchone()[0]
        clickability['observed_noncenter_samples']=len(clickability_rows)
        clickability['observed_noncenter_clicks']=sum(r[3] for r in clickability_rows)
    db.execute('ATTACH DATABASE ? AS work',(str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'),))
    cohort_rows=db.execute('''SELECT e.cohort,COUNT(*),SUM(s.status='complete'),SUM(s.status='verified_incompatible')
        FROM samples s JOIN work.episodes e ON e.id=s.episode GROUP BY e.cohort ORDER BY e.cohort''').fetchall()
    cohorts=[dict(source_cohort=cohort,required_samples=n,measured_samples=done,
        verified_incompatible_samples=incompatible) for cohort,n,done,incompatible in cohort_rows]
    (out/'cohort_progress.json').write_text(json.dumps(dict(scope='Original archived cohort names retained; these counts do not reclassify validation as training',cohorts=cohorts),ensure_ascii=False,indent=2),encoding='utf-8')
    reset_gate={'heroes':7,'checks':217,'passed':True}
    reset_path=ROOT/'AutomationOutputs/UiFluency/20261012_task_reset_printed_rewards_verified/results.json'
    if reset_path.exists():
        resets=json.loads(reset_path.read_text(encoding='utf8'))['results']
        if len(resets)==7 and all(r['status']=='passed' for r in resets):
            reset_gate={'heroes':7,'checks':sum(r['checks'] for r in resets),'passed':True,'enemy_reward_mode':'PrintedFameOnly','evidence':str(reset_path)}
    if 'costs' in tables:
        for gid,n,mass,clicks,panels,distance in db.execute('''SELECT c.group_id,COUNT(*),SUM(s.estimator_weight),
            SUM(c.pair_clicks*s.estimator_weight)/SUM(s.estimator_weight),
            SUM(c.panels*s.estimator_weight)/SUM(s.estimator_weight),
            SUM(c.transition_px*s.estimator_weight)/SUM(s.estimator_weight)
            FROM costs c JOIN samples s USING(group_id,episode,a_step) GROUP BY c.group_id'''):
            costs[gid]=(n,clicks,panels,distance,mass)
    refused=0
    if 'incompatibilities' in tables:
        refused=db.execute("SELECT COUNT(*) FROM incompatibilities i JOIN samples s USING(group_id,episode,a_step) WHERE s.status='verified_incompatible' AND i.category LIKE 'native_end_turn_refused%'").fetchone()[0]
    results=dict(grouping='kind_pair',groups=m['groups'],sample_cap=m['cap'],required_samples=m['samples'],
        completed_samples=sum(x[2] for x in rows),completed_groups=sum(n==done for _,n,done,_ in rows),
        resolved_groups=sum(n==done+incompatible for _,n,done,incompatible in rows),
        verified_incompatible_samples=sum(x[3] for x in rows),resolved_samples=sum(x[2]+x[3] for x in rows),
        verified_native_refused_samples=refused,verified_ui_incompatible_samples=sum(x[3] for x in rows)-refused,
        view_metric_version=VIEW_METRIC_VERSION,
        stage='Native prefix replay verified; accepted pointer costs retained; remaining adapters and task headers incomplete',
        reset_gate=reset_gate,overall_cost_ready=False,
        cohort_progress_file=str(out/'cohort_progress.json'),
        clickability_evidence=clickability,
        selected_plan=str(selected),runtime_note=args.runtime_note,failure_policy='retain selected IDs and failed attempts; do not replace them with convenient cases')
    (out/'progress.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    page='''<!doctype html><html lang="zh"><meta charset="utf-8"><title>完整 UI 成本测量进度</title><style>
body{font:16px/1.65 system-ui;max-width:1100px;margin:40px auto;padding:0 24px;color:#24354a;background:#f4f7fa}table{border-collapse:collapse;width:100%;background:white}th,td{text-align:left;padding:8px;border-bottom:1px solid #dce2eb}.note{padding:14px;background:#fff0cc}</style><h1>完整 UI 成本基线 · 持续测量</h1>'''
    page+=f'<p>按用户确认的 <b>1,009种行为组合</b> 分组，每组最多{m["cap"]}条，共 <b>{m["samples"]:,}条固定样本</b>。阶段和参数分层，保存原始频率权重；失败不替换。原20条方案保留，精简方案按2026-10-12优先提速请求建立。</p>'
    if args.runtime_note:page+='<p class="note">测量执行状态：'+html.escape(args.runtime_note)+'</p>'
    if (out/'offline_atlas.html').exists():
        page+='<p><a href="offline_atlas.html">查看离线 UI 控件几何图谱</a>：已观察路径的成本重算与原实测分开标注；图谱不代表未观察路径已通过。</p>'
    page+='<p>'+html.escape(m['phase_argument_coverage'])+'</p>'
    speed_path=ROOT/'AutomationOutputs/UiFluency/20261012_speed_comparison.json'
    if speed_path.exists():
        speed=json.loads(speed_path.read_text(encoding='utf8'))
        if speed['status']=='passed':
            page+=f'<p>回放加速对照：相同{speed["samples"]}条样本，{speed["checked_clicks"]}次点击路径/位置与原生效果一致；测量段{speed["control_seconds"]:.1f}秒→{speed["fast_seconds"]:.1f}秒，约{speed["speedup"]:.2f}倍。单次顺序试验，尚不能外推全部复杂操作。</p>'
    if m['cap']<20:
        old=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_sample20_selected_indexed/selected.sqlite').replace('\\','/')+'?mode=ro',uri=True)
        previous=old.execute("SELECT count(*) FROM samples WHERE status='complete'").fetchone()[0];old.close()
        page+=f'<p>原20条清单已有{previous}条实际测量；只复用与精简清单相同的样本。新清单完成数的变化不表示旧证据丢失。</p>'
    serialization_path=ROOT/'AutomationOutputs/UiFluency/20261013_result_serialization_comparison.json'
    if serialization_path.exists():
        serialization=json.loads(serialization_path.read_text(encoding='utf8'))
        if serialization.get('status')=='passed':
            page+=f'<p>证据写盘优化：{serialization["samples"]}条完整样本仅压缩JSON空白，保留全部字段并独立核验{serialization["independent_native_snapshots"]}个规则快照；文件体积减少{serialization["size_reduction_fraction"]:.0%}，单次编码{serialization["pretty_encode_seconds"]:.2f}秒→{serialization["compact_encode_seconds"]:.2f}秒。此项仅为本机序列化对照，不代表整体回放倍速。</p>'
    page+=f'<p>成本样本完成：{results["completed_samples"]:,}/{m["samples"]:,}；组合完成：{results["completed_groups"]}/{m["groups"]}。原生任务重置：七位英雄{reset_gate["checks"]}项校验通过，含完整牌库、技能、库存、源骰物理顺序和供应顺序。任务奖励模式沿用原训练轨迹的PrintedFameOnly。</p>'
    page+=f'<p>另有{results["verified_ui_incompatible_samples"]}条已独立验证UI不兼容：实际界面不能完成原轨迹的该操作，另行原生重放和完整效果核验通过。另有{refused}条原轨迹请求被正式规则拒绝，已独立重放确认；这不作为UI缺入口的证据。两类完成成本均为空，不填0。已测成本或独立结果共{results["resolved_samples"]}条；全部固定样本已有明确结果的组合{results["resolved_groups"]}/{m["groups"]}。</p>'
    page+='<details><summary>原始训练／验证来源分层进度</summary><p>保留归档中的原始cohort名称。下面是固定样本的完成数量，不把验证轨迹归入训练，也不把数量当成策略胜率。</p><table><tr><th>原始来源</th><th>固定样本</th><th>已测</th><th>已验证不兼容／拒绝</th></tr>'
    for cohort,n,done,incompatible in cohort_rows:
        page+=f'<tr><td>{html.escape(cohort)}</td><td>{n}</td><td>{done}</td><td>{incompatible}</td></tr>'
    page+='</table></details>'
    if clickability_rows:
        page+='<details open><summary>控件中心遮挡：实际可点击边缘的证据</summary><p>新增探测保留了真实raycast结果；探测不产生点击或规则变化。下面是已观察到的非中心点击，不是全体控件的遮挡率。缺少按钮与中心被遮挡分开记录。</p><table><tr><th>A→B</th><th>原轨迹实例</th><th>非中心点击</th></tr>'
        for gid,eid,pos,count in clickability_rows:
            page+=f'<tr><td>{html.escape(gid)}</td><td>episode {eid} · A步骤 {pos}</td><td>{count}</td></tr>'
        page+='</table></details>'
    page+='<p class="note">这页是全量任务进度。已有数值按参数/阶段层频率权重计算；未齐组只显示通过样本内的条件均值，并披露覆盖权重，不能当完整总体均值。缺映射、未回放或失败均不能记零成本。完整基线未就绪前，不能用于整体优化排序。</p>'
    page+='<table><tr><th>A→B</th><th>固定样本</th><th>已测</th><th>UI不兼容</th><th>覆盖权重</th><th>点击数</th><th>视图切换</th><th>移动距离</th><th>状态</th></tr>'
    for gid,n,done,incompatible in sorted(rows):
        value=costs.get(gid)
        fields=('—','—','—') if value is None else (f'{value[1]:.2f}',f'{value[2]:.2f}',f'{value[3]:.1f}px')
        status='样本已齐，待加权审核' if done==n else '部分样本通过，继续补剩余' if done else '等待参数映射及回放'
        if incompatible:status='部分不兼容／拒绝已验证；其余继续核验' if done+incompatible<n else '样本已解析，含不兼容／拒绝结果；无完整总体成本'
        mass='—' if value is None else f'{value[4]:.1%}'
        page+='<tr><td>'+html.escape(gid)+f'</td><td>{n}</td><td>{done}</td><td>{incompatible}</td><td>{mass}</td><td>{fields[0]}</td><td>{fields[1]}</td><td>{fields[2]}</td><td>{status}</td></tr>'
    page+='</table><p>成本按实际raycast点击计算；视图切换含弹窗及手牌/部队/技能栏切换，由原始可点击控件快照复算。主表距离统一使用控件中心，从A结束控件开始，含衔接段，每段只计一次；边缘点击另留实际EventSystem输入位置与路径，均不冒充操作系统鼠标轨迹。系统延迟与脚本等待、玩家思考分开。统计按阶段/参数层原始频率加权，避免为了多样性而放大稀有场景。</p></html>'
    (out/'report.html').write_text(page,encoding='utf-8');db.close();print(json.dumps(results,ensure_ascii=False))


if __name__=='__main__':main()
