"""Publish versioned actual captures and separately scoped independent reviews."""
from pathlib import Path
import json,html,shutil,hashlib

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def esc(v):return html.escape(str(v))
def link(p,label):return '<a href="'+esc(p.relative_to(OUT).as_posix())+'">'+esc(label)+'</a>'
def image(p,label):return '<figure><img loading="lazy" src="'+esc(p.relative_to(OUT).as_posix())+'"><figcaption>'+esc(label)+'</figcaption></figure>'

def page(version):
 current=OUT/f'attempt{version}';prior=OUT/f'attempt{version-1}'
 body=['<!doctype html><meta charset="utf-8"><title>战斗界面 '+str(version)+'</title>',
 '<style>body{background:#14201f;color:#e5e9df;font:17px/1.65 system-ui;margin:24px auto;max-width:1600px;padding:0 22px}a{color:#d8bd79}h1,h2{color:#e8ca89}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 20px}img{width:100%;height:auto}figcaption{font-size:15px}pre{white-space:pre-wrap;background:#1d2b29;padding:20px}.catalog{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}@media(max-width:900px){.pair,.catalog{grid-template-columns:1fr}}</style>',
 f'<h1>战斗界面：attempt{version-1} → attempt{version}</h1>']
 if version==44:
  body+=['<p>攻击提示依据实际角色头顶位置排布，并增加独立装饰投影；目标已选时降低阶段推进的视觉优先级。人物原 PNG、完整 UV、尺寸和正式操作保持。</p>',
  '<p>独立六场景审查：3.4000 → 3.5333 / 5，每版30项。收益主要是信息与对象的关联；没有证明接地融合或整体美感改善。宽提示胶囊、底部信息块、双重目标环仍是问题。</p>',
  '<p>'+link(current/'projection_contract_audit.json','46组同状态、点击几何、完整字形及投影合同核对')+'</p>']
 else:
  audit=read(current/'compact_layout_audit.json');items=[t for r in audit['records'] for t in r['threats']]
  ratio=1-sum(t['after_width'] for t in items)/sum(t['before_width'] for t in items)
  body+=['<p>攻击提示按实际文字宽度排列并为相邻角色留间距；敌人数值只保留一层贴合文字的衬底。保留原字号、完整文字、人物 PNG/尺寸和正式点击区域。摘要从原生完整读数派生，以保留条件名望及原护甲信息。</p>',
  f'<p>46组同状态截图中，{len(items)}条攻击文字容器的累计宽度减少 {ratio:.1%}，没有缩字或删字。这仅衡量布局空白，不能等同于真人负担、操作成本或美感提升。</p>',
  '<p>'+link(current/'compact_layout_audit.json','46组完整几何与字形核对')+' · '+link(current/'compact_layout_catalog_audit.json','106场景扩展核对')+' · '+link(current/'enemy_hud_collision_audit.json','106场景敌人HUD字形交叠检查')+'</p>']
 body+=['<p>本页五维评分是局部截图专项审查，没有替代原完整rubrics，也没有构成完整游戏验收。不同独立审查者给出的44版基线分不同，不能跨审查串联成连续提升曲线。</p>',
 '<p>目标仍未完成：角色与环境的光照/接触关系、目标双环、技能与部队展开区的文档感，以及决定性费用/效果的突出展示仍需继续优化。当前阴影是 UI 装饰投影，没有物理光照或性能验收。</p>',
 '<p>'+link(current/'compile_receipt.json','实际程序集与Console')+' · '+link(current/'asset_cases/manifest.json','106种角色原生场景与绑定')+' · '+link(current/'independent_review/review.json','独立评分JSON')+' · '+link(current/'independent_review/inputs.json','独立输入与SHA')+'</p>']
 review=current/'independent_review/review.md'
 if review.exists():body+=['<h2>独立截图审查原文</h2><pre>'+esc(review.read_text(encoding='utf8'))+'</pre>']
 if version==45:body+=['<p>审查发现四单位中立图的开战提示明显变暗。代码检查发现文字有CanvasGroup淡出，而衬底为独立同级对象；这是一项待做动态修复和复核的实现问题。原截图、评分及未通过的旧脚本输入/全文期望记录均保留，没有覆盖成绿色结果。</p>']
 if version==45:body+=['<p>'+link(current/'run_recipe.json','复现输入与执行顺序')+' · '+link(current/'source_manifest.json','候选源码SHA')+' · '+link(OUT/'normal_resume_after_attempt45/status.json','恢复普通场景后的实际状态')+'</p>']
 body+=['<h2>重要阶段与典型界面对比</h2>']
 mappings=[('validation_final' if version==45 else 'validation','validation' if version==45 else 'validation_final'),('four_unit_validation','four_unit_validation' if version==45 else 'four_unit_validation_final'),('card_validation_final' if version==45 else 'card_validation','card_validation' if version==45 else 'card_validation_final')]
 for group,old_group in mappings:
  for capture in sorted((current/group/'key_phases').glob('*/screen.png')):
   before=prior/old_group/'key_phases'/capture.parent.name/'screen.png'
   if before.exists():body+=['<h3>'+esc(capture.parent.name)+'</h3><div class="pair">'+image(before,'基线 '+str(version-1))+image(capture,'候选 '+str(version))+'</div>']
 body+=['<h2>当前106种角色实机场景</h2><p>7英雄、69怪物、30部队。每张来自正式原生任务/轨迹上下文，绑定通过不代表全规则消费、印刷身份、美感或真人体验验收。</p>']
 if version==44:
  for name in ['enemy_a','enemy_b','party']:
   p=current/'catalog_review'/name/'review.md'
   if p.exists():body+=['<p>'+link(p,name+' 当前版本单版视觉审查（无基线，不作为提升分）')+' · '+link(p.with_suffix('.json'),'完整逐项评分')+'</p>']
 body+=['<div class="catalog">']
 for capture in sorted((current/'asset_cases/key_phases').glob('*/screen.png')):body+=[image(capture,capture.parent.name)]
 body+=['</div><p>前版记录：'+link(prior/'comparison_report.html',f'attempt{version-1}')+'</p>']
 return '\n'.join(body)

def main():
 for version in [44,45]:
  current=OUT/f'attempt{version}'
  assert read(current/'asset_cases/manifest.json')['passed']
  content=page(version)
  dest=current/'comparison_report.html'
  if dest.exists():raise FileExistsError(dest)
  dest.write_text(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1),encoding='utf8')
 current=OUT/'attempt45';shutil.copy2(OUT/'index.html',current/'previous_index.html')
 (OUT/'index.html').write_text(page(45),encoding='utf8')
 print('Published immutable44/45 reports and current index')

if __name__=='__main__':main()
