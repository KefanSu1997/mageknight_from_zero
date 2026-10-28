"""Render only observed PyTorch training and fixed-validation metrics."""
import html
import json
from live_training_dashboard import chart, lines, publish_page

def render(output, stage):
    scale=json.loads((output/'manifest.json').read_text(encoding='utf-8')).get('reward_scale',1.)
    reward_note=f'训练奖励固定缩放 ×{scale:g}：胜利 +{100*scale:g}，失败 −{100*scale:g}；不做动态均值方差归一化。PPO标准化 advantage。'
    rows=lines(output/'metrics.jsonl');ev=lines(output/'evaluation_metrics.jsonl')
    cards=[chart('训练 reward（最近100个已完成局）',[('reward',[(r['update'],r['recent_mean_reward']) for r in rows])],'PPO更新',f'平均每局累计训练 reward（×{scale:g}）'),
        chart('胜率',[('训练最近100局',[(r['update'],100*r.get('performance_recent100',{}).get('win_rate',0)) for r in rows]),
                    ('固定1000验证',[(r['update'],100*r['wins']/r['episodes']) for r in ev])],'PPO更新','胜率（%）'),
        chart('Value explained variance',[('更新前',[(r['update'],r['value_before']['ev']) for r in rows]),
            ('更新后',[(r['update'],r['value_after']['ev']) for r in rows])],'PPO更新','EV（无量纲）')]
    for key,label in [('mean_kills','平均击杀'),('mean_new_wound_cards','平均新增英雄伤牌'),
                      ('mean_new_unit_wounds','平均单位受伤'),('winning_mean_new_wound_cards','胜利局平均新增伤牌')]:
        cards.append(chart(label,[('训练最近100局',[(r['update'],r.get('performance_recent100',{}).get(key)) for r in rows])],'PPO更新',label+'（每局数量）'))
    publish_page(output/'live.html','<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="15">'
        '<style>body{background:#111827;color:#eee;font:17px system-ui;padding:28px}section{margin:24px 0}svg{width:95%;max-width:1000px} .muted{color:#aaa}</style>'
        '<h1>PyTorch CUDA · 单场 reward v2</h1><p>'+html.escape(stage)+
        '</p><p><strong>'+html.escape(reward_note)+'</strong></p><p>32环境 × 128步 = 4096 transitions/更新；minibatch 128；目标100更新。新初始化，非旧NumPy续训。训练4000/验证1000组合隔离。</p>'+''.join(cards))
