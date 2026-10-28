"""Publish selected-action comparison evidence without relabelling older scores."""
import shutil,hashlib
from publish_embedded_hierarchy_report import OUT,read,write,link,picture,relative

def native(r):return {v['key']:v['value'] for v in r['observation']['state']}['audit.costNativeState']
def main():
 cur=OUT/'attempt37';reader=cur/'entity_reader_validation';pairs=[];invariants=[]
 for p in [reader/'manifest.json',cur/'validation/party_feedback_manifest.json',cur/'four_unit_validation/manifest.json',cur/'ground_layout_geometry.json',cur/'comparison_edge_cases_retry/manifest.json']:
  assert read(p)['passed'],p
 for actor in ['hero','unit','enemy']:
  old=OUT/'attempt36/entity_reader_validation_measured/key_phases'/(actor+'_top');new=reader/'key_phases'/(actor+'_top')
  a,b=read(old/'capture.json'),read(new/'capture.json');assert native(a)==native(b)
  body=lambda r:next(t['fullText'] for t in r['evaluation']['texts'] if t['path'].endswith('/EntityHoverViewport/Body'))
  assert body(a)==body(b)
  pairs.append(dict(actor=actor,before=relative(old/'screen.png'),after=relative(new/'screen.png'),same_native=True,full_body_equal=True))
 for p in sorted((cur/'validation/key_phases').glob('*/capture.json')):
  old=OUT/'attempt36/validation/key_phases'/p.parent.name/'capture.json';a,b=read(old),read(p);assert native(a)==native(b)
  controls=lambda r:{c['path']:(c['interactable'],c['clickable']) for c in r['observation']['controls']}
  images=lambda r:{im['path']:{k:im[k] for k in ['texture','uvRect','screenRect']} for im in r['evaluation']['rawImages']}
  assert controls(a)==controls(b),p
  assert images(a)==images(b),p
  invariants.append(dict(label=p.parent.name,controls_equal=True,raw_images_equal=True,native_equal=True))
 write(cur/'unchanged_controls_and_figures.json',dict(passed=True,pairs=invariants,scope='25 actual regular-phase captures: exposed control path/enabled/clickability and image texture/fullUV/geometry unchanged; not whole-game proof.'))
 write(cur/'hover_pairing_audit.json',dict(passed=True,pairs=pairs))
 write(cur/'harness_setup_correction.json',dict(initial='comparison_edge_cases guessed Btn_Card_0, which was absent; failed before claimed card selection',repair='comparison_edge_cases_retry uses first actual exposed clickable Btn_Card_*; actual eight-option card preserved',deleted_evidence=False))
 page=['<!doctype html><meta charset="utf-8"><title>已选动作与悬浮比较 · attempt37</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}h1,h2{color:#dcc492}details{margin:24px 0}</style><h1>已选动作与悬浮比较 · attempt37</h1>',
 '<p>临时悬浮查看英雄、佣兵或敌人时，右栏同时保留已选守卫的实际动作、费用和就绪状态。内容直接复制最终动作按钮及状态文字，不在提示里重算规则。移开恢复原操作；角色、手牌和敌方威胁仍留在场上。</p>',
 '<p>本轮配对独立审查：3.00→3.80/5，3组×5维，每版15项；额外耗竭和八选项卡图不入均分。独立审查重新给attempt36基线打分，不能与历史评分拼接为连续提升曲线。</p>',
 '<p>代价与不足：正文21pt、对照22pt，未缩字；勇者阅读视口变短，实际上下各需6次滚轮事件（上一版各5次）。八项卡牌只显示数量与移开提示，并未完成同屏比较。长段文字、素材光照接地、操作视觉主次仍需改善。</p>']
 for p,label in [(reader/'manifest.json','三实体全文与真实滚轮'),(reader/'disabled_comparison.json','实际耗竭禁用对照'),(cur/'comparison_edge_cases_retry/manifest.json','八选项退路与返回'),(cur/'independent_review/review.md','独立Sol完整评分'),(cur/'independent_review/inputs.json','审查输入与SHA'),(cur/'ground_layout_geometry.json','40个实际捕获几何对照'),(cur/'unchanged_controls_and_figures.json','25组控件及原图参数'),(cur/'validation/party_feedback_manifest.json','阶段与正式消费'),(cur/'four_unit_validation/manifest.json','四单位与容量负例'),(cur/'compile_receipt.json','编译运行版本')]:page.append('<p>'+link(p,label)+'</p>')
 page.append('<p>三种实际正文可消费字形41/41、190/190、64/64；查看不改原生状态和选择，返回后守卫实际产生格挡4并耗竭。EventSystem脚本事件不等于物理鼠标或真人体验。40组几何对照含不同验证目录，不能称40个正常阶段。</p>')
 for pair in pairs:page.append('<h2>'+pair['actor']+'</h2><div class="pair">'+picture(OUT/pair['before'],'attempt36')+picture(OUT/pair['after'],'attempt37 · 保留已选守卫动作')+'</div>')
 for folder,label in [(reader/'key_phases/exhausted_guard_comparison','实际耗竭状态'),(reader/'key_phases/unit_end','勇者原能力和印刷费用末段'),(cur/'comparison_edge_cases_retry/key_phases/card_selected_hover_hero','八项卡牌：仅退路提示，比较未完成')]:page.append('<h2>'+label+'</h2>'+picture(folder/'screen.png',label))
 page.append('<h2>重要阶段前后留档</h2>')
 for p in sorted((cur/'validation/key_phases').glob('*/capture.json')):
  old=OUT/'attempt36/validation/key_phases'/p.parent.name
  page.append('<details><summary>'+p.parent.name+'</summary><div class="pair">'+picture(old/'screen.png','attempt36')+picture(p.parent/'screen.png','attempt37')+'</div></details>')
 page.append('<p>目标未完成。本轮不代表106种实体的全部正文、所有卡牌模式、全部构筑或真人接受；也没有证明总体加权交互成本下降。</p><p>'+link(OUT/'attempt36/comparison_report.html','上一版完整留档')+' · '+link(OUT/'attempt33/comparison_report.html','106种素材与绑定来源')+'</p>')
 text='\n'.join(page)
 if not(cur/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',cur/'previous_index.html')
 (OUT/'index.html').write_text(text,encoding='utf8');(cur/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
 source=cur/'source_after';source.mkdir(exist_ok=True);sha=[]
 for p in ['Assets/Scripts/Battle/PveBattleController.EmbeddedBattle.cs','tools/RsiGame/validate_entity_hover_reader.py','tools/RsiGame/validate_selected_card_comparison.py','tools/RsiGame/publish_selected_comparison_report.py']:
  p=__import__('pathlib').Path(p);dest=source/p.name;shutil.copyfile(p,dest);sha.append(dict(source=str(p),snapshot=str(dest),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
 write(cur/'source_manifest.json',sha)
 print('Published attempt37; native, action and full image invariants passed:',len(invariants))
if __name__=='__main__':main()
