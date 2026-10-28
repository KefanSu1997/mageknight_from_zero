"""Publish finite scene-grading and action-hierarchy evidence without claiming completion."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib,math,re

def main():
 c=OUT/'attempt43';checks=['card_validation_final/manifest.json','route_validation_final/manifest.json','rule_scroll_validation_final/manifest.json','validation_final/party_feedback_manifest.json','four_unit_validation_final/manifest.json','shortcut_validation_final/manifest.json','atmosphere_geometry_audit_final.json','phase_geometry_final.json','four_unit_geometry_final.json']
 for n in checks:assert read(c/n)['passed'],n
 costs=[]
 for route in read(c/'route_validation_final/manifest.json')['routes']:
  old=next(r for r in read(OUT/'attempt42/route_validation/manifest.json')['routes'] if r['route']==route['route'])
  points=lambda r:[e['point_unity_bottom_left'] for e in r['events']]
  assert points(old)==points(route) and old['before']==route['before'] and old['after']==route['after']
  costs.append(dict(route=route['route'],clicks=len(points(route)),click_center_chords_px=sum(math.dist(a,b) for a,b in zip(points(route),points(route)[1:])),change_px=0,same_actual_consumer=True))
 write(c/'route_costs.json',dict(passed=True,rows=costs,scope='6 actual routes versus42, same actual centers/native consumers. Excludes first pointer, hover detours, human time/global weighted cost. New shortcuts have separate open-return traces, not a cost-reduction claim.'))
 audit=read(c/'atmosphere_geometry_audit_final.json');actors=sorted({im['texture'] for r in audit['rows'] for im in r['actors']})
 write(c/'atmosphere_scope.json',dict(actual_matched_captures=len(audit['rows']),actual_displayed_identities=actors,unchanged_png_source_files=106,preset=dict(saturation=.9,upper=[1,.98,.94],ground=[.88,.92,.96]),limits='Fixed forest grading, not sampled/dynamic environment lighting or physically based lighting. Preserves source alpha/UV/state colors; no new bitmap art. Artistic fusion not improved in independent screenshot review; performance and all106scene coverage not measured.'))
 means=read(c/'independent_review_final/review.json')['paired_means']
 page=['<!doctype html><meta charset="utf-8"><title>战场与操作层级43</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:24px}a{color:#e9c980}img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:10px 0}.warning{padding:16px;background:#30261f}</style><h1>战场与操作层级43</h1>',
 '<p>技能、部队入口改为图形标记与原文字，保留命中区域；阶段推进采用与出牌一致的执行控件，有选中卡牌/技能/部队时弱化强调。展开列表的原英雄等级/技能数、部队数量/指挥槽/耗竭说明完整保留22pt，并移到卡牌区上方，避免与返回入口重叠。</p>',
 '<p>角色渲染加入固定林间氛围分级：轻微降饱和，上部暖光、下部冷色。此为运行时 shader，不是重绘素材；106原PNG SHA全部保持，完整UV、几何、原耗竭/死亡色调及透明通道逻辑保持。实际支持情况和参数从Unity逐帧捕获；不冒称动态光照、物理光照、全106实景验收或性能优化。</p>',
 f'<p class="warning">最终独立Sol逐张看13图：5配对×5项，每版25评分，{means["before"]:.2f}→{means["after"]:.2f}/5。改善主要是中性状态的阶段推进；角色融合未见明确改善，接地、锐利边缘及快捷入口可发现性仍不足。不能据shader已生效宣称美术融合完成。初审12图3.60→3.68另存，不与最终评分拼成曲线。</p>',
 '<p>实际6条卡牌/横置消费路线、25阶段加7四佣兵场景、2快捷入口展开返回及全文滚轮验证通过；46个匹配状态的原生状态、按钮命中与素材几何核对通过。卡牌6路线中心连线成本与42相同；没有全局/人类负担改善结论。接地阴影布局沿用既有版本，本轮没有新接地影子。</p>']
 for n in checks+['route_costs.json','atmosphere_scope.json','independent_review_final/protocol.md','independent_review_final/inputs.json','independent_review_final/review.md','independent_review/review.md','header_correction.json','compile_receipt_final.json','delayed_status_receipt.json']:
  page.append('<p>'+link(c/n,n)+'</p>')
 for group,oldgroup,label in [('card_validation_final','card_validation','focus_base'),('card_validation_final','card_validation','focus_strong'),('validation_final','validation','three_enemies_neutral'),('validation_final','validation','unit_selected_in_block'),('four_unit_validation_final','four_unit_validation','four_units_neutral')]:
  page.append('<h2>'+label+'</h2><div class="pair">'+picture(OUT/'attempt42'/oldgroup/'key_phases'/label/'screen.png','42')+picture(c/group/'key_phases'/label/'screen.png','43修正后')+'</div>')
 page.append('<h2>展开界面修正：初版失败保留</h2><div class="pair">'+picture(c/'shortcut_validation/key_phases/Btn_Roster_open/screen.png','初版：标题与入口重叠')+picture(c/'shortcut_validation_final/key_phases/Btn_Roster_open/screen.png','修正：同全文22pt移至卡牌区上方')+'</div>')
 for p in sorted((c/'shortcut_validation_final/key_phases').glob('*/screen.png')):page.append('<details><summary>'+p.parent.name+'</summary>'+picture(p,'实际快捷入口/悬浮/展开/返回')+'</details>')
 for group,oldgroup in [('validation_final','validation'),('four_unit_validation_final','four_unit_validation')]:
  for p in sorted((c/group/'key_phases').glob('*/screen.png')):
   old=OUT/'attempt42'/oldgroup/'key_phases'/p.parent.name/'screen.png';assert old.is_file()
   page.append('<details><summary>'+group+'/'+p.parent.name+'</summary><div class="pair">'+picture(old,'42')+picture(p,'43')+'</div></details>')
 page.append('<p>整体目标未完成。下一步优先处理真正可见的接地/光照与素材风格、敌人威胁标签、选中目标后的操作优先级、默认效果摘要的必要数量/条件、技能与部队操作及庞大悬浮说明。</p>')
 if not (c/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',c/'previous_index.html')
 html=''.join(page)
 for target in re.findall(r'(?:href|src)="([^"]+)"',html):assert (OUT/target).is_file(),target
 (OUT/'index.html').write_text(html,encoding='utf8');(c/'comparison_report.html').write_text(html.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">'),encoding='utf8')
 d=c/'source_after';d.mkdir(exist_ok=True);records=[]
 for name in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','Assets/Scripts/Battle/PveBattleController.cs','Assets/Scripts/SceneAutomation/StructuredUiPilot.Evaluation.cs','Assets/Resources/UI/BattleCharacterAtmosphere.shader','Assets/Resources/UI/BattleCharacterAtmosphere.shader.meta','tools/RsiGame/validate_party_shortcuts.py','tools/RsiGame/audit_character_atmosphere.py',__file__]:
  p=Path(name);shutil.copyfile(p,d/p.name);records.append(dict(path=str(p.resolve()),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),archived=relative(d/p.name)))
 write(c/'source_manifest.json',dict(files=records));print('Published43;13image final review,46states audit; scene fusion remains incomplete')
if __name__=='__main__':main()
