"""Render actual full-scenario metrics; no synthetic points or missing-value zeros."""
import argparse
import html
import json
import math
from pathlib import Path
import os
import time
import uuid


def read(path,default):
    if not path.exists():return default
    return json.loads(path.read_text(encoding='utf-8'))


def chart(title,points):
    points=[(x,y) for x,y in points if isinstance(y,(int,float)) and math.isfinite(y)]
    if not points:return '<section><h2>'+html.escape(title)+'</h2><p>尚无已完成的数据</p></section>'
    xmin=min(x for x,y in points);xmax=max(x for x,y in points);lo=min(y for x,y in points);hi=max(y for x,y in points)
    if lo==hi:lo-=.5;hi+=.5
    xy=[(45+550*(x-xmin)/max(1,xmax-xmin),25+160*(hi-y)/(hi-lo)) for x,y in points]
    line=' '.join(f'{x:.2f},{y:.2f}' for x,y in xy)
    dots=''.join(f'<circle cx="{a:.2f}" cy="{b:.2f}" r="3"><title>更新 {x}：{y:.6g}</title></circle>' for (x,y),(a,b) in zip(points,xy))
    return f'<section><h2>{html.escape(title)}</h2><svg viewBox="0 0 630 220"><text x="0" y="25">{hi:.3g}</text><text x="0" y="185">{lo:.3g}</text><polyline points="{line}"/>{dots}<text x="45" y="215">{xmin}</text><text x="575" y="215">{xmax}</text></svg></section>'


def render(root):
    root=Path(root);metrics=read(root/'metrics.json',[])
    evaluations=[read(p,{}) for p in sorted(root.glob('eval_*/summary.json'))]
    requested=list((root/'evaluation_requests').glob('*.json'))
    review=read(root/'review_required.json',None)
    status='待检查：存在 review_required' if review else ('训练循环已完成，评估另行核对' if (root/'training_complete.json').exists() else '无进程存活判断；以下只显示已保存结果')
    text=f'<h1>单人征服 · A 系列完整任务</h1><p>{html.escape(root.name)} · {html.escape(status)}</p>'
    synced=read(root/'sync_status.json',None)
    if synced:
        text+='<p>最近成功同步：'+html.escape(time.strftime('%Y-%m-%d %H:%M:%S',time.localtime(synced['last_success_unix'])))+'；同步结果不代表训练进程仍存活。</p>'
    text+=f'<p>完成训练更新 {len(metrics)}；完成评估 {len(evaluations)}／请求 {len(requested)}。验证：100 世界 × 7 英雄；测试集不用于此页面调参。</p>'
    progress_rows=[]
    paths=sorted(root.glob('rollout_*/progress.json'))[-1:]+sorted(root.glob('eval_*/collection/progress.json'))
    for path in paths:
        progress=read(path,{})
        if not progress:continue
        is_eval=path.parent.name=='collection'
        label=path.parent.parent.name if is_eval else path.parent.name
        if is_eval and (path.parent.parent/'summary.json').exists():continue
        active=progress.get('active',[])
        progress_rows.append('<tr>'+''.join('<td>'+html.escape(str(v))+'</td>' for v in
            (label,str(progress.get('finished',0))+'/'+str(700 if is_eval else 128),len(active),
             f"{progress.get('elapsed_seconds',0)/60:.1f} 分钟",
             max((row.get('decisions',0) for row in active),default=0)))+'</tr>')
    if progress_rows:
        text+='<h2>最近保存的采样进度</h2><table><thead><tr><th>阶段</th><th>已结束任务</th><th>活动环境</th><th>采样已耗时</th><th>活动任务最长决策数</th></tr></thead><tbody>'+''.join(progress_rows)+'</tbody></table><p>已结束任务可能包含错误；是否有效以完整性审计为准。采样耗时尚不含随后PPO、保存；此处不是完成时间预测或进程存活证明。</p>'
    text+='<p>EV：更新前行为 critic 对完整任务回报；目标方差为零时留空。0.5 是排查阈值，并非能力保证。横轴为 PPO 更新次数，悬停查看数值。</p>'
    specs=[('训练奖励','reward'),('EV（pre-update）','pre_update_ev'),('价值 MSE','mse'),('预测偏差','bias'),('目标方差','target_variance'),('官方分数','mean_official_score'),('声望','mean_fame'),('剩余伤牌','mean_remaining_wounds'),('攻克城市','mean_conquered_cities'),('实际 Adam 步数','accepted_optimizer_steps')]
    panels=[chart(title,[(r['update'],r.get(key)) for r in metrics]) for title,key in specs]
    for key in ('collection','ppo','save','archive','full_update_wall'):
        panels.append(chart(key+' 秒',[(r['update'],r.get('timing_seconds',{}).get(key)) for r in metrics]))
    for key in ('evaluation_wall','evaluation_queue_wait'):
        panels.append(chart(key+' 秒',[(r['checkpoint']['update'],r.get(key)) for r in evaluations]))
    for title,key in [('固定验证官方分数','mean_official_score'),('固定验证剩余伤牌','mean_remaining_wounds')]:
        panels.append(chart(title,[(r['checkpoint']['update'],(r.get('completed_scenario_metrics') or {}).get(key)) for r in evaluations]))
    text+='<div class="grid">'+''.join(panels)+'</div>'
    text+='<p>archive 为并行 worker 保存原生轨迹的累计工作秒数，不是墙钟时间，不含回传 H 盘；不与 collection 重复相加。含错误的评估，其分数仅表示完成子集，不能当完整 700 局成绩。</p>'
    text+='<h2>逐次评估完整性</h2><pre>'+html.escape(json.dumps([dict(update=r['checkpoint']['update'],completed=r['completed'],errors=r['errors'],denominator=r['denominator']) for r in evaluations],ensure_ascii=False,indent=2))+'</pre>'
    text+='<h2>诊断</h2><pre>'+html.escape(json.dumps(review or read(root/'diagnostics.json',{}),ensure_ascii=False,indent=2))+'</pre>'
    text+='<h2>最近阶段记录（不代表进程仍存活）</h2><pre>'+html.escape(json.dumps(read(root/'phase.json',{}),ensure_ascii=False,indent=2))+'</pre>'
    optimizer=read(root/'optimizer_progress.json',None)
    if optimizer:
        text+='<h2>当前 PPO 进度</h2><p>已执行 Adam 数与已处理样本来自实际优化器。主机分段时间不是 GPU 核心耗时；forward 包含 prepare，不能重复相加。</p><pre>'+html.escape(json.dumps(optimizer,ensure_ascii=False,indent=2))+'</pre>'
    text+='<h2>逐批轨迹审查</h2>'+''.join(f'<p><a href="{html.escape(p.name)}">{html.escape(p.name)}</a></p>' for p in sorted(root.glob('trajectory_review_*.json')))
    return '<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="30"><title>Solo conquest tracking</title><style>body{background:#101827;color:#e4eaf4;font:16px system-ui;margin:28px}a{color:#80bfff}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(380px,1fr));gap:16px}section{background:#182338;padding:15px}h2{font-size:18px}svg{width:100%}text{fill:#bbc8da;font-size:12px}polyline{fill:none;stroke:#64a8ff;stroke-width:2}circle{fill:#64a8ff}pre{white-space:pre-wrap}</style>'+text


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--run',required=True);p.add_argument('--out',required=True);p.add_argument('--watch',action='store_true');args=p.parse_args()
    target=Path(args.out)
    while True:
        content=render(args.run);temporary=target.with_name(target.name+'.'+uuid.uuid4().hex+'.pending')
        try:
            temporary.write_text(content,encoding='utf-8');os.replace(temporary,target)
        except OSError:
            try:temporary.unlink(missing_ok=True)
            except OSError:pass
            if not args.watch:raise
        if not args.watch:break
        time.sleep(10)
