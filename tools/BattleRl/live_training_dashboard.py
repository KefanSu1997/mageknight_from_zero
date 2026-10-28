"""Read-only local observer. Never controls or modifies a training process."""
import argparse
import ctypes
from ctypes import wintypes
import html
import json
import os
from pathlib import Path
import shutil
import time
import uuid


def read_json(path, default=None):
    try:return json.loads(path.read_text(encoding='utf-8-sig'))
    except (OSError, ValueError):return default


def lines(path):
    try:
        result=[]
        for line in path.read_text(encoding='utf-8').splitlines():
            try:result.append(json.loads(line))
            except ValueError:pass
        return result
    except OSError:return []


def publish_page(path, document):
    """Keep the last complete page when a Windows reader denies replacement."""
    temporary=path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
    for attempt in range(3):
        try:
            temporary.write_text(document,encoding='utf-8')
            os.replace(temporary,path)
            return True
        except OSError:
            if attempt < 2:time.sleep(.1)
    # The normal observer cycle retries with fresh data, without touching training.
    try:temporary.unlink(missing_ok=True)
    except OSError:pass
    return False


def chart(title, series, xlabel, ylabel='数值'):
    valid=[(name,[(x,y) for x,y in points if y is not None]) for name,points in series]
    points=[p for _,values in valid for p in values]
    if not points:return f'<section><h2>{html.escape(title)}</h2><p class="muted">尚无已保存数据，不表示指标为零。</p></section>'
    xs,ys=zip(*points);xmin,xmax=min(xs),max(xs);ymin,ymax=min(ys),max(ys)
    dx=max(1,xmax-xmin)
    if ymax-ymin<1e-8:
        pad=max(.01,abs(ymax)*.05);ymin-=pad;ymax+=pad
    dy=ymax-ymin
    drawings=[];legend=[]
    for j in range(5):
        y=195-j*37.5;value=ymin+dy*j/4
        drawings.append(f'<path d="M100 {y} H750" stroke="#334155"/><text x="92" y="{y+4}" text-anchor="end">{value:.4g}</text>')
    for i,(name,values) in enumerate(valid):
        color=['#60a5fa','#34d399','#fbbf24','#c084fc'][i%4]
        values=values[::max(1,len(values)//500)]
        xy=[(100+650*(x-xmin)/dx,195-150*(y-ymin)/dy) for x,y in values]
        line=' '.join(f'{x:.2f},{y:.2f}' for x,y in xy)
        drawings.append(f'<polyline points="{line}" fill="none" stroke="{color}" stroke-width="2"/>')
        if len(xy)==1:drawings.append(f'<circle cx="{xy[0][0]}" cy="{xy[0][1]}" r="4" fill="{color}"/>')
        legend.append(f'<span style="color:{color}">{html.escape(name)}</span>')
    return f'<section><h2>{html.escape(title)}</h2><p>{" · ".join(legend)}</p><svg viewBox="0 0 800 260" style="fill:#e2e8f0;font-size:13px"><text x="100" y="20">纵轴：{html.escape(ylabel)}</text><path d="M100 35 V195 H750" stroke="#64748b" fill="none"/>'+''.join(drawings)+f'<text x="100" y="216">{xmin:g}</text><text x="750" y="216" text-anchor="end">{xmax:g}</text><text x="425" y="246" text-anchor="middle">{html.escape(xlabel)}</text></svg></section>'


class Process:
    def __init__(self, pid):
        self.api=ctypes.WinDLL('kernel32',use_last_error=True)
        self.api.OpenProcess.restype=wintypes.HANDLE
        self.api.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD]
        self.api.GetExitCodeProcess.argtypes=[wintypes.HANDLE,ctypes.POINTER(wintypes.DWORD)]
        self.api.GetProcessTimes.argtypes=[wintypes.HANDLE]+[ctypes.POINTER(wintypes.FILETIME)]*4
        self.handle=self.api.OpenProcess(0x1000,False,pid)
    def status(self):
        code=wintypes.DWORD()
        if not self.handle or not self.api.GetExitCodeProcess(self.handle,ctypes.byref(code)):return False,None
        times=[wintypes.FILETIME() for _ in range(4)]
        ok=self.api.GetProcessTimes(self.handle,*[ctypes.byref(t) for t in times])
        cpu=sum((t.dwHighDateTime<<32)+t.dwLowDateTime for t in times[2:])/1e7 if ok else None
        return code.value==259,cpu


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--run',required=True);parser.add_argument('--pid',type=int,required=True)
    parser.add_argument('--stderr',help='Actual trainer stderr file for this run')
    parser.add_argument('--interval',type=float,default=10);args=parser.parse_args()
    run=Path(args.run);process=Process(args.pid);cache={};started=time.time()
    def episodes(folder):
        rows=[]
        for path in folder.glob('episode_*.json'):
            if str(path) not in cache:
                document=read_json(path)
                if not document:continue
                trace=document.get('trace',[])
                if not trace:continue
                cache[str(path)]=dict(index=document['episode'],reward=sum(t['training_reward'] for t in trace),
                    won=bool(trace[-1]['after'].get('taskProgress',{}).get('sequenceCompleted')),steps=len(trace),
                    modified=path.stat().st_mtime)
            rows.append(cache[str(path)])
        return sorted(rows,key=lambda r:r['index'])
    while True:
        alive,cpu=process.status();training=lines(run/'metrics.jsonl');evaluations=lines(run/'evaluation_metrics.jsonl')
        train_rows=episodes(run/'rollouts');eval_dirs=sorted(run.glob('eval_[0-9][0-9][0-9][0-9]'))
        current_eval=eval_dirs[-1] if eval_dirs else None
        eval_rows=episodes(current_eval/'rollouts') if current_eval else []
        eval_summary=read_json(current_eval/'summary.json') if current_eval else None
        update=training[-1]['update'] if training else 0
        failure_paths=list(run.glob('*failure*.json'))
        for directory in eval_dirs:failure_paths.extend(directory.glob('*failure*.json'))
        stderr=Path(args.stderr) if args.stderr else run.parent/'stderr.log'
        try:error=stderr.read_text(encoding='utf-8')[-2200:]
        except OSError:error=''
        if not alive:phase='进程已退出：请核对完成报告或异常'
        elif current_eval and not eval_summary:phase=f'冻结评估 {current_eval.name}：已保存 {len(eval_rows)}/1000 局'
        elif not train_rows and not evaluations:phase='启动校验 / 构建32个环境（尚无完成的rollout）'
        else:phase=f'采样或优化：已记录 {update}/100 次更新，已保存 {len(train_rows)} 个训练rollout'
        plotted=eval_rows if current_eval and not eval_summary else train_rows
        label='当前评估已完成局' if plotted is eval_rows else '训练已完成局'
        rolling=[]
        for i in range(len(plotted)):
            window=plotted[max(0,i-49):i+1]
            rolling.append((i+1,sum(r['reward'] for r in window)/len(window),sum(r['won'] for r in window)/len(window)))
        panels=[chart('实时 rollout reward（最近50个已保存局）',[(label,[(i,r) for i,r,_ in rolling])],'已完成局数，非PPO更新数'),
                chart('实时 rollout 胜率（最近50个已保存局）',[(label,[(i,w) for i,_,w in rolling])],'已完成局数；未完成全批可能有完成顺序偏差'),
                chart('训练批 reward',[('训练奖励',[(r['update'],r['mean_training_return']) for r in training])],'PPO更新'),
                chart('训练批胜率',[('训练胜率',[(r['update'],r['task_success_rate']) for r in training])],'PPO更新'),
                chart('训练 value explained variance',[('更新前',[(r['update'],r.get('value_exp_var_before')) for r in training]),('更新后',[(r['update'],r.get('value_exp_var_after')) for r in training])],'PPO更新；仅保存真实预测后产生点'),
                chart('固定1000任务评估胜率',[('冻结评估',[(r['update'],r['win_rate']) for r in evaluations])],'PPO更新；只画完整评估'),
                chart('固定1000任务评估 reward',[('冻结评估',[(r['update'],r['mean_training_reward']) for r in evaluations])],'PPO更新'),
                chart('固定1000任务评估 value EV',[('冻结评估',[(r['update'],r['critic']['explained_variance']) for r in evaluations])],'PPO更新')]
        free=shutil.disk_usage(run).free/2**30
        stamp=time.strftime('%Y-%m-%d %H:%M:%S')
        state=dict(updated=stamp,pid=args.pid,alive=alive,cpu_seconds=cpu,phase=phase,updates=update,training_rollouts=len(train_rows),current_evaluation_rollouts=len(eval_rows),failures=len(failure_paths),free_gib=free)
        (run/'live_status.json').write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8')
        body=f'<h1>5000任务 PPO 实时监控</h1><p class="status">{phase}</p><p>每10秒刷新 · {stamp} · PID {args.pid} · CPU累计 {cpu if cpu is not None else "未知"} 秒 · 磁盘剩余 {free:.1f} GiB</p><p>4000训练 / 1000固定评估；32环境、128局/更新、目标100次更新。实时采样曲线不等于固定评估能力。</p>'
        if failure_paths or error:body+=f'<section class="error"><h2>异常证据：{len(failure_paths)}份失败文件</h2><pre>{html.escape(error)}</pre></section>'
        body+='<div class="grid">'+''.join(panels)+'</div>'
        document='<meta charset="utf-8"><meta http-equiv="refresh" content="10"><title>PPO实时训练监控</title><style>body{background:#0f172a;color:#e2e8f0;font:16px system-ui;margin:32px}h1{font-size:28px}h2{font-size:18px}.status{font-size:22px;color:#34d399}.muted{color:#94a3b8}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:18px}section{background:#1e293b;padding:20px;border-radius:12px}svg{width:100%}text{fill:#94a3b8;font-size:13px}.error{border:1px solid #f87171}pre{white-space:pre-wrap}</style>'+body
        published=publish_page(run/'live.html',document)
        state['page_publish_deferred']=not published
        (run/'live_status.json').write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8')
        if not alive:break
        time.sleep(args.interval)


if __name__=='__main__':main()
