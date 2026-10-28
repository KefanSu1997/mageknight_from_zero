"""Publish candidate41, including its rejected low-contrast first attempt."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib,math,re

def main():
 c=OUT/'attempt41'
 checks=['card_validation_final/manifest.json','route_validation_final/manifest.json','rule_scroll_validation_final/manifest.json','validation_final/party_feedback_manifest.json','four_unit_validation_final/manifest.json','card_geometry_final.json','phase_geometry_final.json','four_unit_geometry_final.json']
 for name in checks:assert read(c/name)['passed'],name
 costs=[]
 for route in read(c/'route_validation_final/manifest.json')['routes']:
  old=next(r for r in read(OUT/'attempt40/route_validation_retry/manifest.json')['routes'] if r['route']==route['route'])
  points=lambda r:[e['point_unity_bottom_left'] for e in r['events']]
  assert points(old)==points(route) and [e['name'] for e in old['events']]==[e['name'] for e in route['events']]
  assert old['before']==route['before'] and old['after']==route['after']
  costs.append(dict(route=route['route'],clicks=len(points(route)),chords_px=sum(math.dist(a,b) for a,b in zip(points(route),points(route)[1:])),change_px=0,same_native_consumer=True))
 write(c/'route_costs_final.json',dict(passed=True,rows=costs,scope='6 actual routes versus40, actual click centers/native consumers identical. Excludes first pointer position, hover detours, human time and global weighted cost.'))
 review=read(c/'independent_review_final/review.json')['paired_means']
 page=['<!doctype html><meta charset="utf-8"><title>操作对象层级41</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:24px}a{color:#e9c980}img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:10px 0}.warning{background:#30261f;padding:16px}</style><h1>操作对象层级41：模式、魔力、执行</h1>',
 '<p>卡牌模式改为选中下划线，魔力分支改为带原文字的彩色刻面宝石，确认出牌使用独立执行箭头。仍是原正式按钮与处理函数，文字21pt、效果22pt，原点击区域和角色/卡图尺寸保持。操作组使用一层柔边暗幕，避免文字直接混入石阶枝叶；长分支保留原完整文字与滚动布局。本轮未更换角色素材，也未完成整体游戏化。</p>',
 '<p class="warning">失败版本保留：41初版直接移除绿色底板，独立11图审查4.00→3.80，指出可点击性与对比度退化。随后增加统一柔边暗幕；初版截图、源码、评分独立保存，未用新图覆盖。</p>',
 f'<p>最终独立Sol实际看11图，4配对×5项、每版20评分：{review["before"]:.2f}→{review["after"]:.2f}/5。与初审分别报告，不拼成连续曲线。截图评分不能证明实际交互或全部卡牌合格。完整逐项理由见下方审查。</p>',
 '<p>6条实际路线与40的点击数、点击中心及原生消费保持一致，几何代价未增加。40的固定5点击1281.83px基本回到38基线；该5点击包含来回切换模式，不是最短操作或人类时间。本轮没有全局加权成本结论。</p>']
 for name in checks+['route_costs_final.json','independent_review_final/protocol.md','independent_review_final/inputs.json','independent_review_final/review.md','independent_review/review.md','compile_receipt_final.json','card_geometry_filter_failure.json']:
  page.append('<p>'+link(c/name,name)+'</p>')
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  a=OUT/'attempt40/card_validation_final/key_phases'/label/'screen.png';b=c/'card_validation_final/key_phases'/label/'screen.png'
  page.append('<h2>'+label+'</h2><div class="pair">'+picture(a,'40基线')+picture(b,'41修正后')+'</div>')
 page.append('<details><summary>保留低对比度初版</summary>'+picture(c/'card_validation/key_phases/focus_base/screen.png','41初版，未直接采用')+'</details>')
 for label in ['focus_base_full_rule','focus_strong_full_rule']:page.append('<h2>'+label+'</h2>'+picture(c/'card_validation_final/key_phases'/label/'screen.png','实际完整规则'))
 for group,oldgroup in [('validation_final','validation'),('four_unit_validation_final','four_unit_validation')]:
  for p in sorted((c/group/'key_phases').glob('*/screen.png')):
   a=OUT/'attempt40'/oldgroup/'key_phases'/p.parent.name/'screen.png';assert a.is_file()
   page.append('<details><summary>'+group+'/'+p.parent.name+'</summary><div class="pair">'+picture(a,'40')+picture(p,'41')+'</div></details>')
 page.append('<p>后续仍需解决：必要数量/条件摘要、技能与部队操作、阶段执行入口、怪物威胁标签、材质光照/接地以及部分素材身份问题。106项绑定并非106项视觉验收；整体目标未完成。</p>')
 if not (c/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',c/'previous_index.html')
 html=''.join(page)
 for target in re.findall(r'(?:href|src)="([^"]+)"',html):assert (OUT/target).is_file(),target
 (OUT/'index.html').write_text(html,encoding='utf8');(c/'comparison_report.html').write_text(html.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">'),encoding='utf8')
 after=c/'source_after';after.mkdir(exist_ok=True);records=[]
 for name in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','tools/RsiGame/audit_card_command_objects.py',__file__]:
  p=Path(name);shutil.copyfile(p,after/p.name);records.append(dict(path=str(p.resolve()),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),archive=relative(after/p.name)))
 write(c/'source_manifest.json',dict(files=records))
 print('Published41: actual evidence and rejected initial review retained; goal incomplete')
if __name__=='__main__':main()
