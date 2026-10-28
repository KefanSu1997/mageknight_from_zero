"""Design report and geometric guards; these are not player usability tests."""
import csv
import html
import json
import sqlite3
from pathlib import Path
from iterate_ui_cost_design import OUT, ROOT


def rect(c):
    return c['x']-c['width']/2,c['y']-c['height']/2,c['x']+c['width']/2,c['y']+c['height']/2


def overlap(a,b):
    return min(a[2],b[2])>max(a[0],b[0])+.01 and min(a[3],b[3])>max(a[1],b[1])+.01


def main():
    comparison=json.loads((OUT/'comparison.json').read_text(encoding='utf8'))
    originals=json.loads((ROOT/'AutomationOutputs/UiFluency/computed_costs/costs.json').read_text(encoding='utf8'))
    atlas=sqlite3.connect('file:H:/MageKnight_RL_Backups/UiFluency_future/20261013_offline_atlas1/atlas.sqlite?mode=ro',uri=True)
    checked=0;errors=[]
    for lid,view,data in atlas.execute('SELECT id,view,geometry_json FROM layouts'):
        layout=json.loads(data)
        if view=='JourneyTools':
            # The modal occludes the underlying battle controls. Check controls
            # on this modal, including disabled targets, not hidden underlay.
            existing=[c for c in layout['controls'] if '/JourneyTools/' in c['path'] and c['name']!='Btn_CloseTools']
            added=[dict(x=472,y=114,width=860,height=70),dict(x=1372,y=114,width=860,height=70)]
        elif view in ('Battle.Hand','Battle.Units','Battle.Skills'):
            existing=[c for c in layout['controls'] if c['name']!='Btn_ResetPhase']
            added=[dict(x=1537.5,y=1006,width=215,height=52)]
            if any(c['name']=='Btn_EndTurn' and '/Inspector/' in c['path'] and c['clickable'] for c in existing):
                added.append(dict(x=1655,y=919.5,width=450,height=43))
        elif view=='Outside':
            existing=layout['controls']
            added=[dict(x=1717,y=940,width=306,height=64)] if any(c['name']=='Btn_EndTurn' and c['clickable'] for c in existing) else []
        else:continue
        checked+=1
        for a in added:
            assert 0<=rect(a)[0] and rect(a)[2]<=1920 and 0<=rect(a)[1] and rect(a)[3]<=1080
            for c in existing:
                if overlap(rect(a),rect(c)):
                    errors.append(dict(layout=lid,view=view,control=c['path']))
    # New review layout: separate card controls, selection controls, and text.
    card_rects=[(40+i%3*309,190+i//3*300,328+i%3*309,474+i//3*300) for i in range(6)]
    controls=card_rects+[(40,801,320,850),(658,801,946,850),(1010,686,1870,750),(1010,760,1870,824),(1010,876,1870,933),(40,1001,1870,1057)]
    text_rects=[(40,25,1850,80),(40,89,1850,184),(1010,190,1870,400),(1010,410,1870,500),(1010,520,1870,680),(347,853,617,873),(40,943,1850,987)]
    for i,a in enumerate(controls):
        assert 0<=a[0]<a[2]<=1920 and 0<=a[1]<a[3]<=1080
        assert not any(overlap(a,b) for b in controls[i+1:]+text_rects),a
    guards=dict(canvas=[1920,1080],checked_existing_layouts=checked,new_target_overlap_errors=errors,
                new_review_controls=len(controls),new_review_control_text_overlap=False,
                fonts='Existing scene font sizes unchanged; new button text 21px, review body 22/23/25px, title36px, page14px (matches original page label)',
                artwork='Battle cards unchanged; review cards use original JourneyHand 288x284 targets and156x225 art, plus316x454 original hover preview',
                close_button_change='One1760x70 return control becomes two860x70 controls; font21 and height70 preserved, both labels fully readable. This is not a global scale reduction.',
                runtime_scope='Geometric atlas checks plus a few rendered preview states; not all dynamic contexts or human comprehension certified')
    (OUT/'layout_guards.json').write_text(json.dumps(guards,ensure_ascii=False,indent=2),encoding='utf8')
    if errors:raise ValueError(errors[:5])
    names={0:'基线',1:'强制自动返回',2:'批量弃牌按钮靠上',3:'批量按钮靠近确认',4:'可选返回按钮靠上',5:'可选返回按钮靠下 · 当前采用'}
    status={0:'基线',1:'淘汰：连续取骰多点击',2:'淘汰：规则区重叠且连续取骰退化',3:'淘汰：连续取骰退化',4:'淘汰：取魔力→出牌距离增加',5:'采用：点击不增加，频率加权负担下降；局部距离退化仍披露'}
    base=comparison['versions']['0'];new=comparison['versions']['5']
    page='<!doctype html><html lang="zh"><meta charset="utf-8"><title>UI迭代 · 相同行为序列成本</title><style>body{font:16px/1.7 system-ui;background:#f5f7fb;color:#23364b;max-width:1280px;margin:36px auto;padding:0 24px}table{width:100%;border-collapse:collapse;background:white}td,th{padding:9px;border-bottom:1px solid #dde3ec;text-align:left}img{width:100%;height:auto;border:1px solid #738590}.note{background:#fff0cd;padding:16px}a{color:#1856a0}details{margin:20px 0}svg{width:100%;height:240px}</style><h1>UI迭代 · 降低同一行为序列的操作负担</h1>'
    page+=f'<p>已接入Unity：公共魔力直接入口、可选取骰并返回、可保留/逐张/批量选择的回合确认页。可在战斗菜单切换原基线布局。固定1920×1080画布，不改游戏规则、卡牌尺寸或已有正文/按钮字号。</p><p class="note">新成本是离线规划值，并非新UI的大规模实测。使用全部3,833条已有点击轨迹，对应历史频率权重覆盖98.23%；所有版本使用相同样本、相同参数与相同弃牌集合。1.22%原UI不兼容/拒绝及0.55%未知另列，未填0。频率来自RL，不代表玩家频率。截图来自实际Unity渲染，只作设计检查。</p>'
    page+='<p><a href="behavior_costs.csv">各行为成本CSV</a> · <a href="all_1009_pair_costs.csv">全部1,009种转移CSV</a> · <a href="comparison.json">各版计算结果</a> · <a href="counterfactual_routes.jsonl">逐例新点击规划</a> · <a href="layout_guards.json">防退化约束检查</a></p>'
    page+='<h2>迭代对比</h2><p>点击数按B归属，避免把A+B组合重复累加。距离含A最后点击→B首点及B内部路径；不额外再加A→B桥段。视图切换列覆盖A+B，不能逐转移累加当整局切换数。</p><table><tr><th>版本</th><th>B点击</th><th>衔接+B移动px</th><th>A+B视图切换</th><th>决定</th></tr>'
    for version in range(6):
        v=comparison['versions'][str(version)]
        page+=f'<tr><td>{names[version]}</td><td>{v["b_clicks"]:.3f}</td><td>{v["transition_px"]:.0f}</td><td>{v["panels"]:.3f}</td><td>{status[version]}</td></tr>'
    page+='</table><p>当前方案：加权点击减少 <b>'+f'{(1-new["b_clicks"]/base["b_clicks"])*100:.2f}%</b>，衔接+B移动减少 <b>{(1-new["transition_px"]/base["transition_px"])*100:.2f}%</b>；A+B视图切换均值减少{(1-new["panels"]/base["panels"])*100:.2f}%。保留三个指标，不用任意换算权重掩盖退化。</p>'
    page+='<h2>新UI · 实际Unity截图</h2>'
    for filename,label in [('adopted_battle.png','战斗页：原卡与敌人保持原大小；公共魔力直接入口'),('adopted_mana.png','公共魔力页：保留取骰并停留；底部取骰并返回与不取骰返回分开'),('adopted_outside.png','战斗外：保留手牌直接结束，仍只需1点击'),('adopted_review.png','回合确认页：原卡、资源、保留/待弃状态；批量选择与最终确认分开')]:
        page+=f'<details open><summary>{label}</summary><img src="{filename}" alt="{label}"></details>'
    page+='<h2>各行为与局部退化</h2><table><tr><th>B行为</th><th>旧点击→新点击</th><th>旧移动→新移动px</th><th>旧A+B视图→新视图</th><th>说明</th></tr>'
    for k,old in sorted(comparison['intents']['0'].items(),key=lambda item:item[1]['b_clicks']*item[1]['weight'],reverse=True):
        after=comparison['intents']['5'][k]
        note='距离增加，保留披露' if after['transition_px']>old['transition_px']+1 else ''
        if k=='nextTurn':note='原宏丢弃全部剩余非创伤牌；不是所有玩家的普通结束回合'
        page+=f'<tr><td>{html.escape(k)}</td><td>{old["b_clicks"]:.2f} → {after["b_clicks"]:.2f}</td><td>{old["transition_px"]:.0f} → {after["transition_px"]:.0f}</td><td>{old["panels"]:.2f} → {after["panels"]:.2f}</td><td>{note}</td></tr>'
    page+='</table>'
    source_key=next(k for k in comparison['pairs']['0'] if k.startswith('source') and k.endswith('play'))
    sp=comparison['pairs']['0'][source_key];sn=comparison['pairs']['5'][source_key]
    page+=f'<h2>公共魔力→出牌</h2><p>同4条已知可完成样本：两步合计点击{sp["pair_clicks"]:.2f}→{sn["pair_clicks"]:.2f}；整段移动{sp["internal_a_px"]+sp["transition_px"]:.0f}→{sn["internal_a_px"]+sn["transition_px"]:.0f}px；A结束→B完成部分{sp["transition_px"]:.0f}→{sn["transition_px"]:.0f}px。两段指标区分，不能只报下降更大的那一段。此组包含多种出牌参数，不能全部解释成强化。</p>'
    page+=f'<p>当前无固定样本增加B点击，但{comparison["regressions"].get("v5_distance_samples",0)}条样本的衔接+B距离增加。平均略增加的行为包括block、blockSelected、puppetKeep、puppetUse、soulGuideBlock，最多约31px。nextTurn的A+B视图均值2.73→2.88，新增确认页带来局部切换负担。整体加权下降不代表每一个上下文都改善。</p><p class="note">历史nextTurn宏选择全部剩余非创伤牌；不能把它的节省推广到所有玩家。早期设计让全部保留的简单结束回合1→2点击，已据此增加明确的“保留手牌·直接结束”入口：保留全部仍只需1点击。选择弃牌才进入批量复核。该直接入口不属于历史全弃宏，未额外计入上表节省。下一轮仍需优化少数距离增加的衔接分支。</p>'
    page+=f'<h2>防止优化指标被钻空子</h2><ul><li>画布固定1920×1080，不能缩放整页或减少正常显示信息。</li><li>保留原卡尺寸、敌人图像、费用/模式/目标/来源及最终确认；不得省略材料或多目标步骤。</li><li>几何检查覆盖{checked:,}种已有相关布局，新入口没有新增控件重叠；新确认页控件与规则/资源文字区域分离。</li><li>批量选择仅选择原生非创伤手牌实体，不自动弃牌；页面可逐张修改、清除选择与取消。最终仍调用原EndTurn。</li><li>原取骰并停留路径保留。新增取骰并返回只合并明确的普通取骰和关闭界面，不猜颜色、不替玩家选骰。</li><li>未测/不兼容不变成0；每版保留逐例映射、来源SHA与原频率权重，退化样本不删除。</li></ul></html>'
    (OUT/'report.html').write_text(page,encoding='utf8')
    # Complete 1,009-row inventory, retaining unavailable/unknown groups.
    with (OUT/'all_1009_pair_costs.csv').open('w',encoding='utf-8-sig',newline='') as f:
        wr=csv.writer(f);wr.writerow(['A','B','population','measured_weight','incompatible_weight','unknown_weight','old_B_clicks','new_B_clicks','old_transition_px','new_transition_px','new_pair_views'])
        for r in originals['pairs']:
            key=next((k for k in comparison['pairs']['5'] if k==r['a']+'→'+r['b']),None)
            n=comparison['pairs']['5'].get(key,{})
            wr.writerow([r['a'],r['b'],r['population'],r['measured_mass'],r['incompatible_mass'],r['unknown_mass'],r['b_clicks_conditional_mean'],n.get('b_clicks'),r['transition_px_conditional_mean'],n.get('transition_px'),n.get('panels')])
    print(json.dumps(guards,ensure_ascii=False))


if __name__=='__main__':main()
