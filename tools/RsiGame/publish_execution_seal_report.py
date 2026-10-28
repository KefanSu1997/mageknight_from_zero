"""Archive candidate42's actual primary-action correction and material attempts."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib,math,re

def main():
 c=OUT/'attempt42';checks=['card_validation/manifest.json','route_validation/manifest.json','rule_scroll_validation/manifest.json','validation/party_feedback_manifest.json','four_unit_validation/manifest.json','card_geometry.json','phase_geometry.json','four_unit_geometry.json']
 for n in checks:assert read(c/n)['passed'],n
 costs=[]
 for route in read(c/'route_validation/manifest.json')['routes']:
  old=next(r for r in read(OUT/'attempt41/route_validation_final/manifest.json')['routes'] if r['route']==route['route'])
  points=lambda r:[e['point_unity_bottom_left'] for e in r['events']]
  assert points(old)==points(route) and old['before']==route['before'] and old['after']==route['after']
  costs.append(dict(route=route['route'],clicks=len(points(route)),click_center_chords_px=sum(math.dist(a,b) for a,b in zip(points(route),points(route)[1:])),change_px=0,same_actual_consumer=True))
 write(c/'route_costs.json',dict(passed=True,rows=costs,scope='6 actual routes versus41, same centers and native resource/card consumers. Excludes initial pointer/hover detours/human time/global burden.'))
 means=read(c/'independent_review/review.json')['paired_means']
 page=['<!doctype html><meta charset="utf-8"><title>主操作层级42</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:24px}a{color:#e9c980}img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:10px 0}</style><h1>主操作层级42</h1>',
 '<p>修正旧的统一按钮后处理覆盖新样式的问题：模式选中标记保留金色；确认出牌采用独立金色执行控件，与资源宝石、次级模式区别。仍使用原Button、合法性、参数和正式处理函数，文字21pt、效果22pt，点击范围、画布、角色/卡图尺寸保持。</p>',
 f'<p>独立Sol实际看11图、4配对×5项：{means["before"]:.2f}→{means["after"]:.2f}/5。改善集中于确认入口可识别性；完整信息与悬浮态没有改善，主按钮文字对比、次级禁用和整体文档感仍有不足。此配对不与其它审查分数拼接。</p>',
 '<p>6条实际路线与41的点击数、中心和消费结果相同。没有通过缩小或移动角色换得降低成本；也没有新的人类耗时/全局加权成本结论。完整效果22pt按需读取及真实滚轮往返通过。</p>',
 '<p>素材：只读检查当前106份优先加载PNG（7英雄/69怪物/30佣兵）；均RGBA，alpha≥128可见主体没有触素材边，最小5px。93份alpha≥1细边触边，已与主体分开记录；这不证明所有身份、光照、画布边界或视觉效果合格。黄金龙候选与原图、失败和重试提示词分别留档，42运行时尚未接入新素材。</p>']
 for n in checks+['route_costs.json','independent_review/protocol.md','independent_review/inputs.json','independent_review/review.md','compile_receipt.json','catalog_alpha_inventory.json']:
  page.append('<p>'+link(c/n,n)+'</p>')
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  page.append('<h2>'+label+'</h2><div class="pair">'+picture(OUT/'attempt41/card_validation_final/key_phases'/label/'screen.png','41')+picture(c/'card_validation/key_phases'/label/'screen.png','42')+'</div>')
 for group,oldgroup in [('validation','validation_final'),('four_unit_validation','four_unit_validation_final')]:
  for p in sorted((c/group/'key_phases').glob('*/screen.png')):
   a=OUT/'attempt41'/oldgroup/'key_phases'/p.parent.name/'screen.png';assert a.is_file()
   page.append('<details><summary>'+group+'/'+p.parent.name+'</summary><div class="pair">'+picture(a,'41')+picture(p,'42')+'</div></details>')
 page.append('<h2>素材候选原样存档（不是已接入版本）</h2>')
 for p in sorted((c/'material_candidate').glob('*.txt')):page.append('<p>'+link(p,p.name)+'</p>')
 for p in sorted((c/'material_candidate').glob('*.json')):page.append('<p>'+link(p,p.name)+'</p>')
 for p in sorted((c/'material_candidate').glob('*.png')):page.append('<details><summary>'+p.name+'</summary>'+picture(p,'生成候选，接入需另行实际场景审查')+'</details>')
 page.append('<p>整体目标未完成。下一步：素材战场融合、摘要的必要数量/条件、英雄技能/佣兵/阶段操作的整体层级和悬浮面板的文档感。</p>')
 if not (c/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',c/'previous_index.html')
 html=''.join(page)
 for target in re.findall(r'(?:href|src)="([^"]+)"',html):assert (OUT/target).is_file(),target
 (OUT/'index.html').write_text(html,encoding='utf8');(c/'comparison_report.html').write_text(html.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">'),encoding='utf8')
 d=c/'source_after';d.mkdir(exist_ok=True);records=[]
 for name in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs',__file__]:
  p=Path(name);shutil.copyfile(p,d/p.name);records.append(dict(path=str(p.resolve()),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),archived=relative(d/p.name)))
 write(c/'source_manifest.json',dict(files=records));print('Published42, limited primary hierarchy progress; goal incomplete')
if __name__=='__main__':main()
