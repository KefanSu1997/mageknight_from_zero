"""Preserve candidate40 evidence and publish its limited, matched comparisons."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib

def main():
 c=OUT/'attempt40'
 manifests=['card_validation_final/manifest.json','route_validation_retry/manifest.json','rule_scroll_validation/manifest.json','validation/party_feedback_manifest.json','four_unit_validation/manifest.json','entity_reader_validation/manifest.json','phase_geometry.json','four_unit_geometry.json','card_shelf_costs.json']
 for name in manifests: assert read(c/name)['passed'],name
 pairs=[]
 for label in ['focus_base','focus_strong','focus_base_hover_hero','focus_strong_hover_hero','focus_blue_consumed']:
  a=OUT/'attempt39/card_validation_final/key_phases'/label;b=c/'card_validation_final/key_phases'/label
  native=lambda p:{v['key']:v['value'] for v in read(p/'capture.json')['observation']['state']}['audit.costNativeState']
  assert native(a)==native(b)
  pairs.append((a,b,label))
 page=['<!doctype html><meta charset="utf-8"><title>卡牌操作台40</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:24px}a{color:#e9c980}img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:10px 0}</style><h1>卡牌操作台40：规则按需读取、操作靠近手牌</h1>',
 '<p>当前摘要22pt、规则全文悬浮22pt，原单位/英雄/怪物阅读21pt。禁用横置保留原因，模式切换位置固定；完整规则实际滚轮往返且原生状态不变。固定5点击中心连线1506.98→1281.83px，较39减少14.94%，与38的1282.05px基本持平。排除起点和悬浮绕行，不是人类时间或全局加权成本。</p>',
 '<p>6条真实消费路线：默认蓝、显式蓝/白/红/绿晶及另一牌横置。额外色路线的旧版成本来自离线几何规划，新版实际点击；横置没有同状态旧版成本，保留null。独立Sol实际看11图、4配对×5项：3.70→3.80/5，小幅改善。仍有绿色按钮文档感、摘要省略数量/条件、角色光照与素材辨识缺口；目标未完成。</p>']
 for name in manifests+['independent_review/protocol.md','independent_review/inputs.json','independent_review/review.md','compile_receipt_final.json','historical_priority_basis.json']:
  page.append('<p>'+link(c/name,name)+'</p>')
 for a,b,label in pairs:page.append('<h2>'+label+'</h2><div class="pair">'+picture(a/'screen.png','39')+picture(b/'screen.png','40')+'</div>')
 for label in ['focus_base_full_rule','focus_strong_full_rule']:
  page.append('<h2>'+label+'</h2>'+picture(c/'card_validation_final/key_phases'/label/'screen.png','实际完整规则悬浮'))
 for group,oldgroup in [('validation','validation_final'),('four_unit_validation','four_unit_validation_final')]:
  for p in sorted((c/group/'key_phases').glob('*/screen.png')):
   old=OUT/'attempt39'/oldgroup/'key_phases'/p.parent.name/'screen.png'
   assert old.is_file();page.append('<details><summary>'+group+'/'+p.parent.name+'</summary><div class="pair">'+picture(old,'39')+picture(p,'40')+'</div></details>')
 page.append('<p>保留失败：card_validation首次误用Button-only hover；route_validation第二次重复独占fixture文件名失败。改用hover_info和每路线独立目录后重测，失败未删除。</p>')
 if not (c/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',c/'previous_index.html')
 html=''.join(page);(OUT/'index.html').write_text(html,encoding='utf8');(c/'comparison_report.html').write_text(html.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">'),encoding='utf8')
 paths=['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','Assets/Scripts/SceneAutomation/StructuredUiPilot.cs','tools/RsiGame/validate_card_action_shelf.py','tools/RsiGame/validate_card_shelf_routes.py','tools/RsiGame/validate_card_rule_scroll.py',__file__]
 source=c/'source_after';source.mkdir(exist_ok=True);records=[]
 for name in paths:
  p=Path(name);target=source/p.name;shutil.copyfile(p,target);records.append(dict(path=str(p.resolve()),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),archived=relative(target)))
 write(c/'source_manifest.json',dict(files=records))
 print('Published candidate40; all completion claims remain scoped.')
if __name__=='__main__':main()
