"""Publish immutable visual comparison, including rejection when applicable."""
import json,html,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt61'
def main():
 before=json.loads((OUT/'before/manifest.json').read_text(encoding='utf8'))
 after=json.loads((OUT/'after/manifest.json').read_text(encoding='utf8'))
 review=json.loads((OUT/'independent_review.json').read_text(encoding='utf8')) if (OUT/'independent_review.json').exists() else {'status':'pending'}
 parts=['<!doctype html><meta charset="utf-8"><title>61 · 七英雄光影层级试验</title><style>body{background:#17221f;color:#eee5d4;font:18px system-ui;margin:32px}section{margin:40px 0} .pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}img{width:100%}pre{white-space:pre-wrap;background:#23332d;padding:20px}a{color:#e7c986}</style>',
 '<h1>61 · 七英雄光影层级试验</h1><p>同一原生任务、正式开战后的修改前与候选对比。仅英雄增加暖色主光及内轮廓提亮；角色尺寸、完整UV、原始素材、文字和按钮不变。此页保留候选试验结果，不表示完整目标验收。</p>',
 '<p>七个场景的原生状态相等，全部立绘尺寸/UV和全部按钮位置、可用性相等；四佣兵条件场景实际发动通过。范围不包含所有阶段、动画或玩家理解测试。</p>',
 '<p><a href="before/manifest.json">修改前原始证据</a> · <a href="after/manifest.json">候选原始证据</a> · <a href="compile.json">编译与源码指纹</a> · <a href="four_unit_validation/manifest.json">四佣兵实际验证</a> · <a href="independent_review.json">独立评分原文</a></p>']
 links=[]
 for row in before['rows']:
  hero=row['hero'];parts.append(f'<section><h2>{hero}</h2><div class="pair">')
  for version,label in [('before','修改前（60）'),('after','候选（61）')]:
   src=f'{version}/key_phases/{hero}/screen.png';links.append(dict(path=src,sha256=hashlib.sha256((OUT/src).read_bytes()).hexdigest()))
   parts.append(f'<div><p>{label}</p><a href="{src}"><img src="{src}" loading="lazy"></a></div>')
  parts.append('</div></section>')
 parts.insert(3,'<p><strong>候选已拒绝并回退：7/7英雄的光影处理未通过独立美学审查。</strong>下方“候选”是保留的失败试验，不是当前生效版本。<a href="rollback_compile.json">回退源码与编译</a> · <a href="rollback_validation/manifest.json">实际回退场景验证</a></p>' if review.get('overall',{}).get('decision')=='拒绝' else '')
 parts.append('<h2>独立审查</h2><pre>'+html.escape(json.dumps(review,ensure_ascii=False,indent=2))+'</pre>')
 parts.append('<h2>仍需推进</h2><p>公共魔力与支付的统一交互；复杂能力与费用的对应展示；滚动边缘半行裁切；剩余朝向冲突。英雄层级效果需以本轮独立审查为准，不能用材质名称或编译通过代替审美验收。</p>')
 (OUT/'comparison_report.html').write_text('\n'.join(parts),encoding='utf8')
 (OUT/'evidence_inventory.json').write_text(json.dumps(dict(images=links,before_passed=before['passed'],after_passed=after['passed'],goal_complete=False),indent=2),encoding='utf8')
 print('Published61 comparison; full goal incomplete')
if __name__=='__main__':main()
