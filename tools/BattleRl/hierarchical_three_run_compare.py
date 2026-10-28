"""Live hierarchical run versus two frozen, provenance-bound historical runs."""
import argparse
import html
import json
import time
from pathlib import Path
from live_training_dashboard import chart, lines, publish_page
from hierarchical_option_compare import timing

LABELS={'text':'新分层选项 PPO','entity_baseline':'实体文本基线 · 200 更新','baseline':'数值基线 · 299 更新'}

def render(root):
    train={a:lines(root/a/'metrics.jsonl') for a in LABELS}
    evaluations={a:lines(root/a/'evaluation_metrics.jsonl') for a in LABELS}
    plots=[]
    def draw(title,rows,value,ylabel):
        plots.append(chart(title,[(LABELS[a],[(r['update'],value(r)) for r in rs]) for a,rs in rows.items()], 'PPO 完整更新次数',ylabel))
    draw('训练回报 · 最近 100 个完整局',train,lambda r:r['recent_mean_reward'],'累计回报（×0.01）')
    draw('训练胜率 · 最近 100 个完整局',train,lambda r:100*r['performance_recent100']['win_rate'],'胜率 %')
    for field,label in [('value_before','更新前'),('value_after','更新后')]:
        draw('Explained variance · '+label,train,lambda r:r[field]['ev'],'EV（同批价值拟合）')
        draw('Value MSE · '+label,train,lambda r:r[field]['mse'],'MSE')
    for field,label,mult in [('win_rate','固定 1000 局验证胜率',100),('mean_kills','固定验证平均击杀',1),('mean_new_wound_cards','固定验证新增英雄伤牌',1),('mean_new_unit_wounds','固定验证新增单位伤',1)]:
        draw(label,evaluations,lambda r,f=field,m=mult:r['performance'][f]*m,'%' if field=='win_rate' else '每局数量')
    draw('固定验证平均回报',evaluations,lambda r:r['mean_reward'],'累计回报（×0.01）')
    status=[]
    for arm,label in LABELS.items():
        tr=train[arm];ev=evaluations[arm]
        s=f'{label}：已记录 {len(tr)} 个训练点、{len(ev)} 个固定验证点'
        if ev:s+=f"；最近 s{ev[-1]['update']} = {ev[-1]['wins']}/{ev[-1]['episodes']} 胜"
        if arm=='text':
            if (root/arm/'paused_for_batch_optimization.json').exists():
                s+='；已暂停，正在验证批处理提速；未完成评估不计为最终结果'
            for folder in sorted((root/arm).glob('eval_*')):
                if not (folder/'summary.json').exists():
                    p=folder/'progress.json';progress=json.loads(p.read_text()) if p.exists() else {'completed':0,'total':1000}
                    s+=f"；正在 {folder.name}：{progress['completed']}/{progress['total']}"
            data=timing(root/arm,tr)
            if data:s+=f"；最近采样 {data['collection']:.1f}s，优化 {data['optimization']:.1f}s，归档 {data['archive']:.1f}s"
        status.append('<li>'+html.escape(s)+'</li>')
    body='<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="15"><style>body{background:#111827;color:#eee;font:17px system-ui;padding:28px}svg{width:95%;max-width:1100px}li{margin:10px 0}</style><h1>分层方案与两版历史基线</h1><ul>'+''.join(status)+'</ul><p>蓝色：新分层方案；绿色：实体文本 200 更新；黄色：数值 299 更新。各曲线展示完整已记录范围，不补造缺失点。固定评估每 20 更新进行一次，新方案从独立 0 步评估开始。</p><p>实体文本 s200 完整 1000 局为 392 胜，最高 s160 为 419 胜；数值基线实际完成 299 更新，最后固定验证 s280 为 351 胜，最高 s220/s240 为 377 胜。中断的部分评估不计入曲线。</p><p>任务组成、环境动力学、奖励保持一致；架构与自适应采样轨迹不同，属于历史对照，不表示文字单因素因果效果或统计显著性。训练胜率、同批 EV 与固定验证能力分别展示。</p>'+''.join(plots)
    publish_page(root/'compare.html',body)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--once',action='store_true');a=p.parse_args()
    while True:
        try:render(Path(a.root))
        except (OSError,json.JSONDecodeError):
            if a.once:raise
        if a.once:break
        time.sleep(5)
