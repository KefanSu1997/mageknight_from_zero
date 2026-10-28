"""Archive utility hierarchy iteration using paired actual captures."""
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative
from pathlib import Path
import shutil,hashlib,json

def native(r):return {v['key']:v['value'] for v in r['observation']['state']}['audit.costNativeState']
def main():
 cur=OUT/'attempt38';pairs=[]
 for p in [cur/'validation_final/party_feedback_manifest.json',cur/'utility_validation_compact/manifest.json',cur/'four_unit_validation_final/manifest.json',cur/'ground_layout_geometry.json']:assert read(p)['passed'],p
 for p in sorted((cur/'validation_final/key_phases').glob('*/capture.json')):
  old=OUT/'attempt37/validation/key_phases'/p.parent.name/'capture.json';a,b=read(old),read(p);assert native(a)==native(b)
  control=lambda r:{c['path']:{k:c[k] for k in ['x','y','width','height','text','interactable','clickable']} for c in r['observation']['controls']}
  raw=lambda r:{im['path']:{k:im[k] for k in ['texture','uvRect','screenRect']} for im in r['evaluation']['rawImages']}
  assert control(a)==control(b),p
  assert raw(a)==raw(b),p
  pairs.append(dict(label=p.parent.name,before=relative(old.parent/'screen.png'),after=relative(p.parent/'screen.png'),native_equal=True,outer_controls_equal=True,full_raw_images_equal=True))
 write(cur/'pairing_audit.json',dict(passed=True,pairs=pairs,scope='25 actual regular states: all exposed outer control rectangles, captions, enabled/clickable and full raw artwork geometry unchanged; not all possible states.'))
 review=read(cur/'independent_review/review.json')
 page=['<!doctype html><meta charset="utf-8"><title>战场工具入口 · attempt38</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}h1,h2{color:#dcc492}details{margin:24px 0}</style><h1>战场工具入口 · attempt38</h1>',
 '<p>顶部公共魔力、规则、日志、回退和菜单改为独立线条图标与原文字，取消实心绿色按钮板；小范围柔和暗底保持图标和文字对比度，悬浮着色并按需说明。规则与日志说明保留原全文、21pt正文，移到顶部宽提示区；实际五种提示均核对全文字形和人物透明素材边界无相交（仅当前三敌构筑）。当前不可用入口保留原正式禁用状态与灰色标识。</p>',
 '<p>图标使用代码原生绘制；未生成或替换角色素材。画布仍为1920×1080，文字、点击矩形、原正式处理函数和角色/卡图尺寸保持。阶段主动作仍保留较强按钮层级。本轮仅改善全局工具与主操作的相对层级，不能称整个界面已完成游戏化。</p>']
 for p,label in [(cur/'independent_review/review.md','独立Sol逐图评分及不足'),(cur/'independent_review/inputs.json','初审证据组织与SHA'),(cur/'independent_review/compact_recheck.md','规则/日志宽提示修正后三图独立复查'),(cur/'independent_review/compact_recheck_inputs.json','复查输入与SHA'),(cur/'utility_validation_compact/manifest.json','实际悬浮、三个入口打开/返回'),(cur/'pairing_audit.json','25组相同原生状态及原控件参数'),(cur/'ground_layout_geometry.json','实际重要阶段几何核对'),(cur/'validation_final/party_feedback_manifest.json','公共魔力正式取用/消费与单位格挡4'),(cur/'four_unit_validation_final/manifest.json','四单位、正式第四单位消费与容量负例'),(cur/'compile_receipt_final.json','最终运行程序集与Console'),(cur/'canvas_edge_inventory.json','画布边缘透明像素审计，完整UV不等于全像素入画布'),(cur/'renderer_failure_recovery.json','首次缺图标失败与修正')]:page.append('<p>'+link(p,label)+'</p>')
 page.append('<h2>本轮独立审查原始结果</h2><pre>'+__import__('html').escape(json.dumps(review.get('paired_means',review.get('summary',{})),ensure_ascii=False,indent=2))+'</pre><p>3.60→4.20是初审原三组配对分，初审发现的规则提示遮挡已另行修正、复查。初审截图和最终25阶段图分目录保存，运行版本见各自凭据；不把旧额外图评分套到修正图。评分只用于本轮固定证据配对；不串接历史不同审查者的分数，也不代表真人能力或总体交互成本。</p>')
 for label in ['three_enemies_neutral','unit_selected_in_block','enemy_target_selected_after_unit_consumption','four_units_neutral']:
  old=OUT/'attempt37/validation/key_phases'/label;new=cur/'validation_final/key_phases'/label
  if not new.exists():old=OUT/'attempt37/four_unit_validation/key_phases'/label;new=cur/'four_unit_validation_final/key_phases'/label
  if new.exists():page.append('<h2>'+label+'</h2><div class="pair">'+picture(old/'screen.png','attempt37')+picture(new/'screen.png','attempt38')+'</div>')
 page.append('<h2>按需入口实机</h2>')
 for p in sorted((cur/'utility_validation_compact/key_phases').glob('*/screen.png')):page.append('<details><summary>'+p.parent.name+'</summary>'+picture(p,p.parent.name)+'</details>')
 page.append('<h2>全部重要阶段前后留档</h2>')
 for pair in pairs:page.append('<details><summary>'+pair['label']+'</summary><div class="pair">'+picture(OUT/pair['before'],'attempt37')+picture(OUT/pair['after'],'attempt38')+'</div></details>')
 page.append('<h2>仍未完成</h2><p>角色光照与接地、部分素材身份质量、常驻属性与操作层级、长说明像文档的问题仍存在。另查到克朗/高蒂克斯部分低透明度边缘越出左画布；已单列原PNG像素alpha审计，不能用UV未裁切冒充全部像素入画布。八选项卡牌悬浮比较仍只有返回提示。完整106种素材视觉接受、全部英雄和复杂构筑、5+单位、真人审查及总加权交互负担均未证实。下一步优先改善具体可操作对象及其效果/费用的呈现，而不是继续只改工具栏。</p><p>'+link(OUT/'attempt37/comparison_report.html','上一版完整对照')+' · '+link(OUT/'attempt33/comparison_report.html','106种素材与绑定来源')+'</p>')
 text='\n'.join(page)
 if not(cur/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',cur/'previous_index.html')
 (OUT/'index.html').write_text(text,encoding='utf8');(cur/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
 dest=cur/'source_after';dest.mkdir(exist_ok=True);entries=[]
 for p in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','Assets/Scripts/Battle/PveBattleController.CombatKnowledge.cs','tools/RsiGame/validate_utility_entries.py','tools/RsiGame/publish_utility_hierarchy_report.py']:
  p=Path(p);target=dest/p.name;shutil.copyfile(p,target);entries.append(dict(source=str(p),snapshot=str(target),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
 write(cur/'source_manifest.json',entries);print('Published utility hierarchy with',len(pairs),'pairs; goal incomplete')
if __name__=='__main__':main()
