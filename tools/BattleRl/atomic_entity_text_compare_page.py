"""Live paired comparison, with historical reference explicitly separated."""
import argparse
import html
import json
import re
import statistics
import time
from pathlib import Path
from live_training_dashboard import chart, lines, publish_page

def timing(root, rows):
    if not rows:return None
    stamps=[]
    for folder in root.glob('rollout_*.archive'):
        match=re.fullmatch(r'rollout_(\d+)_step\d+\.archive',folder.name)
        metadata=folder/'metadata.pkl.gz'
        if match and metadata.exists():stamps.append((int(match[1]),metadata.stat().st_mtime))
    stamps.sort()
    intervals=[b[1]-a[1] for a,b in zip(stamps,stamps[1:])
               if b[0]==a[0]+1 and (a[0]+1)%20!=0]
    recent=intervals[-10:];last=rows[-1]
    return dict(update=last['update'],collection=last['collection_seconds'],
        optimization=last['optimization_seconds'],archive=last['archive_seconds'],
        median_cycle=statistics.median(recent) if recent else None,
        mean_cycle=statistics.mean(recent) if recent else None,
        min_cycle=min(recent) if recent else None,max_cycle=max(recent) if recent else None,
        cycle_samples=len(recent))

def render(root):
    arms={name:lines(root/name/'evaluation_metrics.jsonl') for name in ('text','baseline')}
    training={name:lines(root/name/'metrics.jsonl') for name in arms}
    plots=[]
    labels={'text':'文本版（mmBERT实体注意力）','baseline':'历史数值基线'}
    plots.append(chart('逐次训练 reward（最近100个有效完成局）',[
        (labels[arm],[(r['update'],r['recent_mean_reward']) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','每局累计reward（×0.01）'))
    plots.append(chart('逐次训练胜率（最近100个有效完成局）',[
        (labels[arm],[(r['update'],100*r.get('performance_recent100',{}).get('win_rate',0)) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','胜率（%）'))
    text_update=training['text'][-1]['update'] if training['text'] else 0
    for field,label in [('value_before','优化前'),('value_after','优化后')]:
        plots.append(chart('Value explained variance · '+label+'（同更新范围对照）',[
            (labels[arm],[(r['update'],r[field]['ev']) for r in rows if r['update']<=text_update])
            for arm,rows in training.items()],'PPO有效更新（截至文本版当前进度）',
            'EV（无量纲，同批回报拟合）'))
    ev_status=[]
    for arm,rows in training.items():
        matched=[r for r in rows if r['update']<=text_update]
        if matched:
            row=matched[-1]
            before=row['value_before']['ev'];after=row['value_after']['ev']
            ev_status.append(f"{labels[arm]}第{row['update']}次：优化前EV {before:.4f}，优化后EV {after:.4f}" if before is not None and after is not None else f"{labels[arm]}EV目标方差为零，数值为空")
    plots.insert(2,'<section><h2>EV最新数值</h2><p>'+html.escape('；'.join(ev_status) or '文本版尚无已完成更新')+'</p><p>蓝线为文本版，绿线为历史基线；主EV图按文本版当前进度对齐。优化后EV只表示同批拟合，不等于胜率。</p></section>')
    plots.append(chart('逐次训练 Value MSE（优化前）',[
        (labels[arm],[(r['update'],r['value_before']['mse']) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','MSE（缩放回报的平方）'))
    for key,label,unit in [('win_rate','固定验证胜率','%'),('mean_kills','平均击杀','每局数量'),
                          ('mean_new_wound_cards','新增英雄伤牌','每局数量'),('mean_new_unit_wounds','新增单位伤','每局数量')]:
        series=[]
        for arm,rows in arms.items():
            series.append(({'text':'冻结mmBERT文本','baseline':'历史数值基线'}[arm],[(r['update'],r['performance'][key]*(100 if key=='win_rate' else 1)) for r in rows]))
        plots.append(chart(label,series,'PPO有效更新',unit))
    plots.append(chart('固定验证累计reward',[(arm,[(r['update'],r['mean_reward']) for r in rows]) for arm,rows in arms.items()],'PPO有效更新','每局累计reward（×0.01）'))
    status=[];speed=[]
    for arm in arms:
        p=root/arm;rows=training[arm];count=rows[-1]['update'] if rows else 0
        state=f'{arm}: {count}/300更新'
        if arm=='baseline':state='历史基线：完整299次更新，最后固定验证280次'
        if arm=='baseline':pass
        elif not p.exists():state+='；等待前一臂完成'
        elif not (p/'initial_checkpoint.zip').exists():state+='；初始化规则环境和模型'
        pending=[d for d in sorted(p.glob('eval_*')) if not (d/'summary.json').exists()]
        if pending:
            progress=json.loads((pending[-1]/'progress.json').read_text()) if (pending[-1]/'progress.json').exists() else {'completed':0,'total':1000}
            state+=f"；{pending[-1].name}验证{progress['completed']}/{progress['total']}"
        if (p/'complete.json').exists():state+='；完成'
        if (p/'paused_for_optimization.json').exists():state+='；已暂停，正在专项速度优化；未完成验证不计入结果'
        status.append(state)
        data=timing(p,rows)
        if data:
            cycle=(f"最近{data['cycle_samples']}个相邻归档周期：中位{data['median_cycle']:.1f}秒，平均{data['mean_cycle']:.1f}秒，范围{data['min_cycle']:.1f}—{data['max_cycle']:.1f}秒" if data['cycle_samples'] else '完整周期数据不足')
            speed.append(f"{labels[arm]}第{data['update']}次：采样{data['collection']:.1f}秒＋优化{data['optimization']:.1f}秒＋归档{data['archive']:.1f}秒。{cycle}。")
    publish_page(root/'compare.html','<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="15"><style>body{background:#111827;color:#eee;font:17px system-ui;padding:28px}svg{width:95%;max-width:1000px}</style><h1>实体文本PPO vs 历史基线</h1><p>'+html.escape(' / '.join(status))+'</p><p>'+html.escape(' '.join(speed))+'</p><p>周期以相邻完整rollout归档时间测量，包含期间优化、诊断、保存；跨20次固定验证的间隔排除。验证耗时另计。训练曲线每次更新一个点，固定验证仅0/20/40/60/80/100，不插值补造评估结果。</p><p>原子动作、环境动力学、4000训练/1000固定验证、每次128有效完整局、32环境、γ=λ=1和reward×0.01相同。新文本版fresh300，历史基线实际299次、验证到280次。新输入为冻结mmBERT规则文本＋实体角色/属性＋注意力融合；历史架构不同，这是同任务同预算的历史对照，不能作为仅文字单因素的因果或统计显著证明。</p>'+''.join(plots))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',required=True);a=parser.parse_args();root=Path(a.root)
    while True:
        try:render(root)
        except (OSError,json.JSONDecodeError):pass
        time.sleep(5)
