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
    arms={name:lines(root/name/'evaluation_metrics.jsonl') for name in ('text','zero')}
    training={name:lines(root/name/'metrics.jsonl') for name in arms}
    plots=[]
    labels={'text':'冻结mmBERT文本','zero':'配对零文本'}
    plots.append(chart('逐次训练 reward（最近100个有效完成局）',[
        (labels[arm],[(r['update'],r['recent_mean_reward']) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','每局累计reward（×0.01）'))
    plots.append(chart('逐次训练胜率（最近100个有效完成局）',[
        (labels[arm],[(r['update'],100*r.get('performance_recent100',{}).get('win_rate',0)) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','胜率（%）'))
    for arm,rows in training.items():
        plots.append(chart(labels[arm]+' Value explained variance',[
            ('优化前',[(r['update'],r['value_before']['ev']) for r in rows]),
            ('优化后',[(r['update'],r['value_after']['ev']) for r in rows])],
            'PPO有效更新','EV（无量纲，同批拟合）'))
    plots.append(chart('逐次训练 Value MSE（优化前）',[
        (labels[arm],[(r['update'],r['value_before']['mse']) for r in rows])
        for arm,rows in training.items()],'PPO有效更新','MSE（缩放回报的平方）'))
    for key,label,unit in [('win_rate','固定验证胜率','%'),('mean_kills','平均击杀','每局数量'),
                          ('mean_new_wound_cards','新增英雄伤牌','每局数量'),('mean_new_unit_wounds','新增单位伤','每局数量')]:
        series=[]
        for arm,rows in arms.items():
            series.append(({'text':'冻结mmBERT文本','zero':'配对零文本'}[arm],[(r['update'],r['performance'][key]*(100 if key=='win_rate' else 1)) for r in rows]))
        plots.append(chart(label,series,'PPO有效更新',unit))
    plots.append(chart('固定验证累计reward',[(arm,[(r['update'],r['mean_reward']) for r in rows]) for arm,rows in arms.items()],'PPO有效更新','每局累计reward（×0.01）'))
    status=[];speed=[]
    for arm in arms:
        p=root/arm;rows=training[arm];count=rows[-1]['update'] if rows else 0
        state=f'{arm}: {count}/100更新'
        if not p.exists():state+='；等待前一臂完成'
        elif not (p/'initial_checkpoint.zip').exists():state+='；初始化规则环境和模型'
        pending=[d for d in sorted(p.glob('eval_*')) if not (d/'summary.json').exists()]
        if pending:
            progress=json.loads((pending[-1]/'progress.json').read_text()) if (pending[-1]/'progress.json').exists() else {'completed':0,'total':1000}
            state+=f"；{pending[-1].name}验证{progress['completed']}/{progress['total']}"
        if (p/'complete.json').exists():state+='；完成'
        status.append(state)
        data=timing(p,rows)
        if data:
            cycle=(f"最近{data['cycle_samples']}个相邻归档周期：中位{data['median_cycle']:.1f}秒，平均{data['mean_cycle']:.1f}秒，范围{data['min_cycle']:.1f}—{data['max_cycle']:.1f}秒" if data['cycle_samples'] else '完整周期数据不足')
            speed.append(f"{labels[arm]}第{data['update']}次：采样{data['collection']:.1f}秒＋优化{data['optimization']:.1f}秒＋归档{data['archive']:.1f}秒。{cycle}。")
    publish_page(root/'compare.html','<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="15"><style>body{background:#111827;color:#eee;font:17px system-ui;padding:28px}svg{width:95%;max-width:1000px}</style><h1>冻结mmBERT文本 vs 配对零文本</h1><p>'+html.escape(' / '.join(status))+'</p><p>'+html.escape(' '.join(speed))+'</p><p>周期以相邻完整rollout归档时间测量，包含期间优化、诊断、保存；跨20次固定验证的间隔排除。验证耗时另计。训练曲线每次更新一个点，固定验证仅0/20/40/60/80/100，不插值补造评估结果。</p><p>同初始可训练参数、4000训练/1000固定验证、每更新128有效完整局、32环境、γ=λ=1、reward×0.01。两版串行训练各100更新。原299次模型只作历史参考；这是文本特征消融，不是Jev/RLCD算法复现或成功概率校准验收。</p>'+''.join(plots))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',required=True);a=parser.parse_args();root=Path(a.root)
    while True:
        try:render(root)
        except (OSError,json.JSONDecodeError):pass
        time.sleep(5)
