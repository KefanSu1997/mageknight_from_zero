"""Join historical action demand, actual pointer costs and explicit mapping gaps."""
import collections
import csv
import hashlib
import html
import json
from pathlib import Path
from analyze_trajectory_ui_priorities import ROOT, group
from compare_unity_state_report import compare_report


def read(path):return json.loads(path.read_text(encoding='utf-8'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    out=ROOT/'AutomationOutputs/UiFluency/20261012_trajectory_priorities'
    r=read(out/'statistics.json');total=r['steps'];counts=r['action_counts']
    baseline=read(ROOT/'AutomationOutputs/UiFluency/20261012_baseline/metrics.json')
    cases=[];failures=[]
    actual_root=ROOT/'AutomationOutputs/UiFluency/20261012_public_source_actual_retry'
    for path in sorted(actual_root.glob('trial_*/cases.json')):
        for c in read(path)['cases']:
            if c['completed']:cases.append(dict(path=str(path),case=c))
            else:failures.append(dict(path=str(path),steps=len(c['steps']),
                reason='script selected movement-only strong card in Melee; fixture mapping failure, retained'))
    comparison=compare_report({'status':'success','steps':[
        dict(success=s['actual']['success'],hitObject=s['actual']['click']['hitObject'],
             beforeState=s['actual']['before']['state'],afterState=s['actual']['observation']['state'])
        for x in cases for s in x['case']['steps']]})
    (out/'public_source_shared_state_verification.json').write_text(json.dumps(comparison,indent=2),encoding='utf-8')
    assert comparison['status']=='passed',comparison
    phase=list(csv.DictReader((out/'phase_actions.csv').open(encoding='utf-8-sig')))
    source_phases={x['phase']:int(x['count']) for x in phase if x['kind']=='source'}
    battle_source=sum(v for k,v in source_phases.items() if k!='Outside')
    source_click_estimate=battle_source*5+source_phases.get('Outside',0)*4
    families=collections.Counter()
    for k,v in counts.items():families[group(k)]+=v
    action_rows=[]
    for k,v in counts.items():
        if k=='source':
            cost='战斗5次；回合总览4次（当前源码路径估算）';panel='战斗3次；回合总览2次（路径估算）';distance='战斗实测 1581–1825 px，2个实例'
        elif k=='advance':cost='已测战斗阶段1次；逐轨迹UI可达性待验';panel='已测路径0次';distance='单次点击无意图内部距离，首段未知'
        elif k=='sideways':cost='已测首页卡2次；翻页/战外入口待验';panel='已测路径0次';distance='已测固定实例1637 px；不能套用所有手牌位置'
        elif k=='play':cost='普通首页基础出牌2次起；选强化/分支/目标/材料另计';panel='复杂输入未知';distance='未知，需按卡牌/分支/位置映射'
        elif k=='attack':cost='已测单敌全贡献2次；显式分配5次';panel='对应0/2次';distance='对应804/2258 px，仅固定实例'
        elif k=='unitDamage':cost='未知；一动作可指定整条部队承伤顺序';panel='未知';distance='未知，不能将规则动作算一次点击'
        else:cost='未知';panel='未知';distance='未知'
        action_rows.append(dict(kind=k,family=group(k),count=v,share=v/total,click_cost=cost,
            panel_cost=panel,distance=distance,evidence='actual_examples_plus_conditional_source_estimate' if k in ('source','advance','sideways','attack') else 'unmapped_or_conditional'))
    with (out/'action_ui_cost_catalog.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(action_rows[0]));w.writeheader();w.writerows(action_rows)
    diagnostics=dict(family_counts=dict(families),source_by_phase=source_phases,
        source_cost_projection=dict(battle_occurrences=battle_source,outside_occurrences=source_phases.get('Outside',0),
            total_clicks_conditional_current_routes=source_click_estimate,
            hypothetical_battle_5_to_2_click_saving=battle_source*3,
            assumptions='independent operations start with relevant overlays closed; no paging or extra color steps; estimate, not historic UI telemetry or measured redesigned UI'),
        actual_public_source=dict(completed_route_instances=len(cases),failed_script_fixture_instances=failures,
            early_schema_failure=str(ROOT/'AutomationOutputs/UiFluency/20261012_public_source_actual/trial_00'),
            full_intent_clicks=[c['case']['metrics']['clicks'] for c in cases],
            full_intent_distance_px=[c['case']['metrics']['pointer_distance_within_intent_pixels'] for c in cases],
            source_only_distance_px=[c['case']['source_only_metrics']['pointer_distance_within_intent_pixels'] for c in cases],
            panel_transitions=[c['case']['metrics']['panel_transitions'] for c in cases],
            mode_retained=[c['case']['verification']['mode_after_return']=='strong' for c in cases],
            scope='two scripted basic_card_007 strong instances; no complete policy/human/general-card claim'),
        priority=['公共魔力入口与取骰确认：高频、5次点击，当前已补实际实例',
                  '出牌的强化/费用/材料/目标复合流程：高频，按原卡与分支补映射',
                  '承伤预选与整条顺序：高频但代理可能循环，先测完整意图而非按动作逐次惩罚',
                  '普通出牌与横置的手牌—右侧确认往返：高频，先保留规则和默认选择再做A/B',
                  '攻击分配：保留2点击快捷路径与可选详细路径，不强制所有攻击5点击'],
        source_bindings={str(p.relative_to(ROOT)):sha(p) for p in [
            ROOT/'tools/BattleRl/analyze_trajectory_ui_priorities.py',ROOT/'tools/BattleRl/build_trajectory_ui_report.py',
            ROOT/'tools/BattleRl/measure_public_source_intent.py',ROOT/'Assets/Scripts/Battle/PveBattleController.cs',
            ROOT/'Assets/Scripts/Battle/PveBattleController.DemoControls.cs',ROOT/'Assets/Scripts/Battle/PveBattleController.Journey.cs']})
    (out/'ui_priority_results.json').write_text(json.dumps(diagnostics,ensure_ascii=False,indent=2),encoding='utf-8')
    def table(headers,rows):
        return '<table><thead><tr>'+''.join('<th>'+html.escape(str(h))+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+html.escape(str(x))+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table>'
    content='''<!doctype html><html lang="zh"><meta charset="utf-8"><title>BattleRL 轨迹与 UI 代价</title><style>
body{font:16px/1.65 system-ui;margin:36px auto;max-width:1180px;padding:0 24px;color:#253140;background:#f5f7fa}table{border-collapse:collapse;width:100%;background:white;margin:20px 0}th,td{text-align:left;padding:10px;border-bottom:1px solid #dce3ed}th{background:#e6edf5}h1,h2{color:#123354}.note{background:#fff0cc;padding:14px}details{margin:20px 0}code{word-break:break-all}</style>'''
    content+='<h1>BattleRL 轨迹 → 行为分布 → 当前 UI 代价</h1>'
    content+=f'<p>归档中 {r["candidate_archive_exports"]:,} 份候选导出，经相同字节 SHA 去重 {r["duplicate_byte_exports"]:,} 份；{r["episodes"]:,} 条非空轨迹，{total:,} 个规则动作，{len(counts)} 个行为类型。候选中有1份空轨迹，单独留存。</p>'
    content+='<p class="note">这是历史训练及集成样本的行为需求分布，不是玩家行为频率。不同训练版本/课程分别统计；评估、诊断、发现目录排除，但部分 validation smoke 仍作为独立集成层保留。只统计 trace，不把 curriculum_prefix、未完局或失败批猜成成功。成本由当前 UI 的真实实例或有条件的源码路径估算提供，未映射项保留未知。</p>'
    content+='<h2>行为分布</h2>'+table(['行为','次数','占规则动作比例','点击代价','界面切换','鼠标距离'],[[x['kind'],f"{x['count']:,}",f"{x['share']:.2%}",x['click_cost'],x['panel_cost'],x['distance']] for x in action_rows[:20]])
    content+='<h2>公共魔力：此前漏测的高频意图</h2>'
    content+=f'<p>普通取骰 {counts["source"]:,} 次，占 {counts["source"]/total:.2%}。其中战斗内 {battle_source:,} 次，战外 {source_phases["Outside"]:,} 次；额外取骰 {counts["extraSource"]:,} 次另计。单一规则动作 source 隐藏了战斗中的五次点击：菜单 → 魔力源/战术 → 选骰 → 普通额度取骰 → 返回当前行动。跨 Battle→BattleMenu→JourneyTools→Battle 三次界面边界。</p>'
    content+=table(['实测实例','完整意图点击','完整意图距离','仅取魔力的距离','跨界面','返回后强化选择'],[[i+1,c['case']['metrics']['clicks'],f"{c['case']['metrics']['pointer_distance_within_intent_pixels']:.0f} px",f"{c['case']['source_only_metrics']['pointer_distance_within_intent_pixels']:.0f} px",c['case']['metrics']['panel_transitions'],c['case']['verification']['mode_after_return']] for i,c in enumerate(cases)])
    content+='<p>完整意图为选原牌 → 强化 → 上述五步取魔力 → 确认出牌，共8次。两个实例均为原敏捷 basic_card_007，白色源骰，实际得到白魔力1再付款归零，手牌同ID少1、played多1、无规则拒绝；返回保留原牌和强化模式。这里的问题主要是入口深度和往返距离，两个实例没有出现选择丢失。第三次脚本错选近战阶段不可强化的移动牌，作为未完成的映射失败保留；早期共享快照缺 source 键的脚本失败也保留，没有当作 UI 故障或从全部尝试中抹掉。</p>'
    chains=r['source_consumer_chains']
    content+=f'<p>跨动作候选意图：同回合 source 后，观察到强化牌实体提交且魔力标记减少的链 {chains.get("source_to_strong_play",0):,} 条；其他标记消费者 {chains.get("source_to_other_token_consumer",0):,} 条。其余保留为跨回合前未观察到消费或轨迹截止。这个判据只能证时间顺序与库存变化，不能证明每一点标记都来自前面的那颗源骰，也不是玩家主观意图识别。</p>'
    content+=f'<p>条件估算：若每次战斗取骰从关闭菜单状态开始按5点击完成、战外从总览按4点击完成，历史取骰需求在当前布局下对应约 {source_click_estimate:,} 次点击。它甚至高于 {counts["advance"]:,} 次阶段推进。若战斗取骰未来能从5次缩至2次，按同样历史需求可少约 {battle_source*3:,} 次点击。这是方案比较的量级估算，不是实际历史点击总数或已实现的优化收益。</p>'
    content+='<h2>下一步优化重点</h2><ol>'+''.join('<li>'+html.escape(x)+'</li>' for x in diagnostics['priority'])+'</ol>'
    content+='<p>排序先看“实际/条件频率 × 同意图多余操作”，再加失败、规则风险和用户反馈；未知成本不是零。公共魔力纳入优先级，承伤/材料/付款需要继续映射，而不能由少数脚本推断全 UI 的流畅度。规则状态和费用守恒作为硬约束；不要为降低点击数剥夺配色、目标或反悔选择。</p>'
    content+='<details><summary>各训练/集成层分母（防止单一长训练淹没其他任务）</summary>'+table(['层','轨迹','规则步','取骰','出牌','承伤预选'],[[k,v['episodes'],v['steps'],v['kinds'].get('source',0),v['kinds'].get('play',0),v['kinds'].get('unitDamage',0)] for k,v in sorted(r['runs'].items(),key=lambda x:x[1]['steps'],reverse=True)])+'</details>'
    content+='<details><summary>完整行为及未知成本</summary>'+table(['行为','次数','占比','点击代价','界面','距离'],[[x['kind'],x['count'],f"{x['share']:.2%}",x['click_cost'],x['panel_cost'],x['distance']] for x in action_rows])+'</details>'
    content+='<p>可追溯附件：statistics.json（原统计）、sources.jsonl（逐轨迹归档位置和校验SHA）、action_distribution.csv、phase_actions.csv、action_transitions.csv、action_ui_cost_catalog.csv、ui_priority_results.json。距离为相邻点击中心欧氏距离之和（1920×1080），首个点击之前的鼠标位置未知；不是人类轨迹距离或耗时。已测样本不足以外推所有手牌位置、颜色许可、材料和部队分支。</p></html>'
    (out/'report.html').write_text(content,encoding='utf-8')
    print(json.dumps(dict(episodes=r['episodes'],steps=total,source_cost_projection=diagnostics['source_cost_projection'],comparison=comparison),ensure_ascii=False))


if __name__=='__main__':main()
