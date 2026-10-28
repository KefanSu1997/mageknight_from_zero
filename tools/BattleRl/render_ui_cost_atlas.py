"""A local viewer of extracted UI geometry; diagrams are not screenshots."""
import argparse
import html
import json
from pathlib import Path
import sqlite3


def main():
    p=argparse.ArgumentParser();p.add_argument('atlas',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    summary=json.loads((a.atlas/'summary.json').read_text(encoding='utf-8'))
    if summary['status']!='passed':raise ValueError('Only a passed offline recomputation can be shown')
    db=sqlite3.connect(a.atlas/'atlas.sqlite');views=[]
    # Read each graph table once instead of repeating an unindexed graph join
    # for every view. Public-context JSON stays out of this counting pass.
    nodes={nid:lid for nid,lid in db.execute('SELECT id,layout FROM nodes')}
    from collections import Counter
    observed=Counter(nodes[nid] for (nid,) in db.execute('SELECT before_node FROM edges'))
    examples={};counts=Counter()
    for lid,view in db.execute('SELECT id,view FROM layouts'):
        counts[view]+=1
        if lid not in observed:continue
        if view not in examples or observed[lid]>observed[examples[view]]:examples[view]=lid
    # One real geometry example per view, selected by observed edge count.
    representatives={}
    for nid,lid in nodes.items():representatives.setdefault(lid,nid)
    for view,lid in sorted(examples.items()):
        total=counts[view];edges=observed[lid]
        geometry=db.execute('SELECT geometry_json FROM layouts WHERE id=?',(lid,)).fetchone()[0]
        node=db.execute('SELECT id,public_context_json FROM nodes WHERE id=?',(representatives[lid],)).fetchone()
        text=json.loads(node[1])['control_text']
        views.append(dict(view=view,variants=total,observed_edges=edges,layout=json.loads(geometry),text=text))
    db.close()
    payload=json.dumps(views,ensure_ascii=False).replace('</','<\\/')
    page='''<!doctype html><meta charset="utf-8"><title>离线 UI 成本图谱</title>
<style>body{font:16px system-ui;background:#101b23;color:#e8f0f4;margin:28px;max-width:1250px}select{padding:10px;font-size:16px}svg{width:100%;background:#172c38;border:1px solid #557380;margin-top:16px}rect{fill:#27525e;stroke:#8fbdc8;stroke-width:2}rect.disabled{fill:#263039;stroke:#59646b}text{fill:white;font-size:17px;pointer-events:none}.note{color:#b4c8d4}#detail{white-space:pre-wrap;padding:14px;background:#223640}a{color:#8fddff}</style>
<h1>离线 UI 成本图谱</h1><p>CONTROL_SUMMARY</p>
<p class="note">下面是从已核验点击记录提取的控件几何示意图，并非游戏截图。每类展示一个实例；不同手牌、分页和控件可用条件仍保留为不同布局。点击控件可查看原始路径和标签。</p>
<select id="view"></select><p id="counts"></p><div id="canvas"></div><pre id="detail">请选择控件</pre>
<p class="note">已观察路径可以离线重算点击数、切换数和距离；这项核验尚未证明未观察路径的预测能力。响应时间继续使用实测数据。未测样本不填零。</p>
<script>const data=PAYLOAD;const sel=document.querySelector('#view');
data.forEach((d,i)=>{const o=document.createElement('option');o.value=i;o.textContent=d.view;sel.append(o)});
function draw(){const d=data[sel.value];document.querySelector('#counts').textContent=`该视图保留 ${d.variants} 种布局；当前示例 ${d.layout.width}×${d.layout.height}`;
const ns='http://www.w3.org/2000/svg',svg=document.createElementNS(ns,'svg');svg.setAttribute('viewBox',`0 0 ${d.layout.width} ${d.layout.height}`);
for(const c of d.layout.controls){const g=document.createElementNS(ns,'g'),r=document.createElementNS(ns,'rect'),t=document.createElementNS(ns,'text');
const y=d.layout.height-c.y;r.setAttribute('x',c.x-c.width/2);r.setAttribute('y',y-c.height/2);r.setAttribute('width',c.width);r.setAttribute('height',c.height);if(!c.clickable)r.setAttribute('class','disabled');
t.setAttribute('x',c.x);t.setAttribute('y',y);t.setAttribute('text-anchor','middle');t.textContent=(d.text[c.path]||c.name).slice(0,18);
g.append(r,t);g.addEventListener('click',()=>{document.querySelector('#detail').textContent=JSON.stringify({...c,text:d.text[c.path]},null,2)});svg.append(g)}
document.querySelector('#canvas').replaceChildren(svg)}sel.addEventListener('change',draw);draw();</script>'''
    sentence=f"已离线重算 {summary['accepted_routes']:,} 条已核验路径、{summary['clicks']:,} 次点击；点击数、界面切换和距离与入库成本一致。保留 {summary['layouts']:,} 种布局、{summary['exact_nodes']:,} 个观察状态。"
    page=page.replace('CONTROL_SUMMARY',html.escape(sentence)).replace('PAYLOAD',payload)
    a.out.parent.mkdir(parents=True,exist_ok=True);a.out.write_text(page,encoding='utf-8')
    print(json.dumps(dict(report=str(a.out.resolve()),views=len(views)),ensure_ascii=False))


if __name__=='__main__':main()
