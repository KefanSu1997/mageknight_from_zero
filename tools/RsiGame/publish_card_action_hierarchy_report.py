"""Archive same-state card action redesign, tradeoffs, and independent review."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib,json,math

def native(r):return {v['key']:v['value'] for v in r['observation']['state']}['audit.costNativeState']
def main():
 cur=OUT/'attempt39';pairs=[];regular=[]
 for p in [cur/'card_validation_final/manifest.json',cur/'validation_final/party_feedback_manifest.json',cur/'four_unit_validation_final/manifest.json',cur/'entity_reader_validation_final/manifest.json',cur/'card_geometry_final.json']:assert read(p)['passed'],p
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  a=cur/'baseline_card/key_phases'/label;b=cur/'card_validation_final/key_phases'/label;assert native(read(a/'capture.json'))==native(read(b/'capture.json'));pairs.append(dict(label=label,before=relative(a/'screen.png'),after=relative(b/'screen.png'),same_native=True))
 for p in sorted((cur/'validation_final/key_phases').glob('*/capture.json')):
  old=OUT/'attempt38/validation_final/key_phases'/p.parent.name/'capture.json';a,b=read(old),read(p);assert native(a)==native(b)
  def signature(r):return [(c['name'],'横置使用' if c['name']=='Btn_Sideways' and not c['interactable'] else c['text'],c['interactable']) for c in r['observation']['controls']]
  assert signature(a)==signature(b),p
  raw=lambda r:{im['path']:{k:im[k] for k in ['texture','uvRect','screenRect']} for im in r['evaluation']['rawImages']}
  assert raw(a)==raw(b),p
  regular.append(dict(label=p.parent.name,before=relative(old.parent/'screen.png'),after=relative(p.parent/'screen.png'),same_native=True,control_ids_captions_enabled_equal_except_disclosed_disabled_caption=True,full_raw_images_equal=True))
 write(cur/'pairing_audit.json',dict(passed=True,card_pairs=pairs,regular_pairs=regular,scope='5 focus card states plus25 normal states; semantic control IDs/captions/enabledness unchanged except explicit sideways disabled wording; hit geometry intentionally changes for card options. Not all game states.'))
 geo=[read(cur/n) for n in ['phase_geometry_final.json','four_unit_geometry_final.json']];write(cur/'ground_layout_geometry.json',dict(passed=True,records=sum([d['records'] for d in geo],[]),scope='25phase/party plus7four-unit actual capture comparisons; not32normal phases.'))
 costs=read(cur/'fixed_click_center_cost_final.json');r=costs['rows'];direct=[math.dist(x['points'][0],x['points'][-1]) for x in [r[0],r[-1]]]
 write(cur/'default_blue_two_click_plan.json',dict(clicks=2,before_px=direct[0],after_px=direct[1],change_percent=100*(direct[1]/direct[0]-1),scope='Offline default-blue select-card/confirm two-center plan using actual archived matching centers. Mode/blue are already selected; this exact2click-only sequence was not separately replayed. Not measured physical cursor or human cost.'))
 write(cur/'base_printed_fee_scope.json',dict(source='Assets/Logic/Runtime/ActionSystem.cs PlayCore',source_sha256=hashlib.sha256(Path('Assets/Logic/Runtime/ActionSystem.cs').read_bytes()).hexdigest(),basis='Action printed mana cost initializes empty and assigns RequiredCrystals for paid enhanced Action only. Additional/contextual rule fees append separately. New basic label refers only to printed mana; complete original effect remains visible/on-demand and original payment handler unchanged.',verified_consumer='basic_card_006 base blue: native token+1 and card removed, same before/after as baseline; not all fee branches.'))
 review=read(cur/'independent_review_final/review.json');means=review['paired_means']
 page=['<!doctype html><meta charset="utf-8"><title>卡牌操作分组与当前效果 · attempt39</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}h1,h2{color:#dcc492}details{margin:24px 0}.warning{border-left:4px solid #e4ad75;padding:12px 20px;background:#30261f}</style><h1>卡牌操作分组与当前效果 · attempt39</h1>',
 '<p>原来的整列按钮改为“固定模式入口 → 当前效果与印刷费用 → 具体短分支 → 确认/横置”。短选项两列，长标签保留整行，动作文字21pt、效果22pt；保留原控件ID、处理函数与正式消费。画布、人物/敌人/卡图原尺寸和完整UV保持。</p>',
 '<p>当前效果保留原规则全文，移除即时预览中重复的通用提交说明；原CardText和所选详情仍完整保留。基础行动明确“印刷魔力：无”，并不代表规则额外费用为零。悬浮其他角色时保留当前模式、分支与印刷费用；长规则摘要仍需“所选详情”读全文，不是全部8项同时完整比较。</p>',
 f'<p class="warning">成本退化须保留：固定5次点击序列（选卡→强化→基础→蓝色→确认）的中心连线距离 {r[0]["sum_click_center_chords_px"]:.1f}→{r[-1]["sum_click_center_chords_px"]:.1f}px，增加{costs["change_percent"]:.1f}%，点击数仍5。仅默认蓝色的2点击规划 {direct[0]:.1f}→{direct[1]:.1f}px（不是单独实测2点击轨迹）。均排除起始鼠标位置、悬浮绕行和人类耗时。本轮改善信息呈现，尚未证明总体负担下降，不按美观分掩盖移动代价。</p>',
 f'<p>最终独立Sol审查实际看9图：{means["before"]:.2f}→{means["after"]:.2f}/5，每版20项，4配对×5维；额外结算图不入均分。初审另存2.85→3.60，不拼成连续曲线。最终审查仍指出强化正文文档感、双列紧凑与禁用弱化不足。</p>']
 for p,label in [(cur/'independent_review_final/protocol.md','最终独立审查完整SP/rubrics'),(cur/'independent_review_final/inputs.json','最终输入组织和逐图SHA'),(cur/'independent_review_final/review.md','最终逐项评分与不足'),(cur/'independent_review/review.md','保留初审，不覆盖'),(cur/'card_validation_final/manifest.json','实际基础/强化/悬浮返回与蓝色正式消费'),(cur/'fixed_click_center_cost_final.json','固定5点击成本含退化'),(cur/'default_blue_two_click_plan.json','默认分支2点击离线规划'),(cur/'base_printed_fee_scope.json','基础印刷费用标签来源及边界'),(cur/'card_geometry_final.json','固定模式位置/结算文字不遮威胁/原图保全'),(cur/'pairing_audit.json','同状态及原语义控件核对'),(cur/'ground_layout_geometry.json','25阶段加7四单位几何'),(cur/'entity_reader_validation_final/manifest.json','三实体全文/滚轮/原守卫对照回归'),(cur/'validation_final/party_feedback_manifest.json','原公共魔力与格挡4消费'),(cur/'four_unit_validation_final/manifest.json','第四单位真实消费及容量负例'),(cur/'compile_receipt_final.json','最终实际程序集与Console')]:page.append('<p>'+link(p,label)+'</p>')
 for pair in pairs:page.append('<h2>'+pair['label']+'</h2><div class="pair">'+picture(OUT/pair['before'],'实际attempt38基线')+picture(OUT/pair['after'],'attempt39最终版')+'</div>')
 page.append('<h2>重要阶段/典型界面前后留档</h2>')
 for pair in regular:page.append('<details><summary>'+pair['label']+'</summary><div class="pair">'+picture(OUT/pair['before'],'attempt38')+picture(OUT/pair['after'],'attempt39')+'</div></details>')
 page.append('<h2>保留的失败与限制</h2><p>基线消费后，验证脚本把tokens字典当数组、hand列表当字典，发生KeyError；原failure.json保留。按实际已存截图状态修正断言后回读验证，没有伪造动作。首次几何审计误要求确认/横置也精确固定，实测其两状态差3.15px；目前只模式按钮位置完全固定，不冒称四按钮精确固定。</p><p>当前验证只覆盖一张全神贯注的两模式和基础蓝色正式消费，强化费用消费、全部卡/参数、复杂长选项与所有构筑尚未通过。原生输入和印刷全文不是策略已理解。素材光照接地、身份质量、全场操作层级、原环形标记语义、5+单位、真人接受及整体加权成本仍未完成。</p><p>'+link(OUT/'attempt38/comparison_report.html','上一版完整留档')+' · '+link(OUT/'attempt33/comparison_report.html','106种素材绑定与来源')+'</p>')
 text='\n'.join(page)
 if not(cur/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',cur/'previous_index.html')
 (OUT/'index.html').write_text(text,encoding='utf8');(cur/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
 snapshot=cur/'source_after';snapshot.mkdir(exist_ok=True);entries=[]
 for p in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','Assets/Scripts/Battle/PveBattleController.cs','tools/RsiGame/validate_card_action_hierarchy.py','tools/RsiGame/audit_card_action_hierarchy.py','tools/RsiGame/publish_card_action_hierarchy_report.py']:
  p=Path(p);target=snapshot/p.name;shutil.copyfile(p,target);entries.append(dict(source=str(p),snapshot=str(target),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
 write(cur/'source_manifest.json',entries);print('Published attempt39 with5card/25regular paired states; cost regression explicitly recorded; goal incomplete')
if __name__=='__main__':main()
