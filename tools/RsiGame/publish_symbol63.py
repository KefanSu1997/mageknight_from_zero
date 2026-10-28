"""Inspectable version-preserving UI report; no inferred overall score."""
import json,html,hashlib,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1';OUT=BASE/'attempt63'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
 candidate='payment_verified'
 assert read(OUT/candidate/'manifest.json')['passed']
 review=read(OUT/'independent_contrast_final.json') if (OUT/'independent_contrast_final.json').exists() else {'status':'pending'}
 audit=read(OUT/'geometry_and_information_audit.json') if (OUT/'geometry_and_information_audit.json').exists() else {'passed':False}
 details=read(OUT/'details_payment/manifest.json') if (OUT/'details_payment/manifest.json').exists() else {'passed':False}
 h=['<!doctype html><meta charset="utf-8"><title>63 · 符号、背景分离与按需全文</title><style>body{background:#17221f;color:#eee4d0;margin:28px;font:18px/1.55 system-ui}a{color:#e6c67e}img{width:100%;height:auto}section{margin:40px 0}select{font:inherit;padding:8px;background:#e1d3b2;color:#20352a}pre{white-space:pre-wrap;overflow-wrap:anywhere;padding:20px;background:#22332c}table{border-collapse:collapse}td,th{border:1px solid #607363;padding:10px}summary{cursor:pointer} .pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}</style>',
 '<h1>63 · 符号、背景分离与按需全文</h1><p>本轮把常驻黑底说明改为符号与必要数值，规则全文移入悬浮读物与图鉴；再修复浅线条融入背景、复杂图形挤成黑团的问题。画布保持1920×1080，原始立绘、手牌图片与UV保留。</p>',
 '<p><strong>评价已更新到v3.2：2.2“符号与数字的背景分离及辨识”设为硬门禁。</strong>天空、枝叶、石地、城墙与可用/选中/不可用状态分别检查；不能由其他高分抵消，也不能以加底座或脚本通过直接判定可辨认。</p>',
 '<p>本页保留黑底基线、弱对比试验、黑团失败候选与当前修正。只报告登记的有限场景；未给全32项或全游戏总分，整体UI目标仍未完成。</p>',
 '<p><a href="evaluation_v3_2/rubric.json">完整rubric</a> · <a href="evaluation_v3_2/judge_system.txt">独立评审prompt</a> · <a href="evaluation_v3_2/score_schema.json">输出schema</a> · <a href="contrast_registration.json">对比度检查登记</a></p>']
 versions=[('before_verified','黑底基线'),('after_verified','弱对比试验'),('resumed_verified','高对比初稿：黑团失败'),(candidate,'当前修正')]
 names={'neutral':'战斗常态','card':'卡牌模式与费用','unit':'佣兵状态与能力','target':'目标需求与投入','block':'格挡阶段','damage':'分配伤害'}
 for label,name in names.items():
  src=f'{candidate}/key_phases/{label}/screen.png';assert (OUT/src).exists()
  opts=''.join(f'<option value="{v}"'+(' selected' if v==candidate else '')+f'>{n}</option>' for v,n in versions)
  h.append(f'<section><h2>{name}</h2><select onchange="var p=this.parentNode;var u=this.value+\'/key_phases/{label}/screen.png\';p.querySelector(\'img\').src=u;p.querySelector(\'a.full\').href=u">{opts}</select><p><a class="full" href="{src}">打开原始截图</a></p><img loading="lazy" src="{src}"></section>')
 latest=read(OUT/'independent_payment_review.json') if (OUT/'independent_payment_review.json').exists() else {'status':'pending'}
 h.append('<p><strong>后续设计门禁拒绝：</strong>2.2局部4分只说明辨识度。新版v3.3独立复查将2.4空间密度评3分、3.4控件设计评2分，当前黄色几何底座方案不作为认可设计。见<a href="independent_density_design_v3_3.json">独立密度与设计诊断</a>。两种rubric分数不混成总分。</p>')
 h.append('<h2>独立评分与未通过记录</h2><p>初稿虽分离了背景，复杂元素图标仍被独立审查判为黑团；后续卡牌模式/来源页仍只有3分。旧评分原文保留，不转写为后续版本得分。</p><p><a href="independent_contrast_before.json">黑团候选完整评分</a> · <a href="independent_contrast_final.json">中间候选评分（卡牌页3分）</a> · <a href="independent_payment_review.json">当前修正评分</a></p><pre>'+html.escape(json.dumps(latest,ensure_ascii=False,indent=2))+'</pre>')
 h.append('<h2>实际验证</h2><ul><li>六组同原生状态：'+str(audit.get('passed',False))+'；枚举黑色说明衬底实际不可见，原始立绘及手牌几何/UV保留。</li><li>符号悬浮入口：'+str(len(read(OUT/candidate/'symbol_readers.json')['rows']))+'个已实际进入并保持原生状态。</li><li>完整规则展示、图鉴末行与返回、守卫发动：'+str(details.get('passed',False))+'；短全文实际一屏可容纳，未虚构滚动操作。</li></ul>')
 h.append('<p><strong>扩查新增失败：</strong>六场景通过不能覆盖七英雄。七英雄检查发现5个原始图片矩形左越界，至少高蒂克斯非透明边缘确实越界。原始失败保留于<a href="seven_heroes_complete/manifest.json">七英雄完整记录</a>；后续在64另行修复与复审。</p>')
 h.append('<p><a href="geometry_and_information_audit.json">几何与信息核对</a> · <a href="'+candidate+'/symbol_readers.json">逐入口验证</a> · <a href="details_payment/manifest.json">全文/图鉴/正式消费</a> · <a href="compile_payment_fix.json">本轮源码及运行版本</a> · <a href="glyph_negative_space_diagnostic.json">两处图形留白诊断（非奖励或验收分）</a></p>')
 for label in ['card_reader_top','trait_reader_top','guide_top','guide_end','guard_ready','guard_consumed']:
  src=f'details_payment/key_phases/{label}/screen.png'
  if (OUT/src).exists():h.append(f'<details><summary>{label} 原始证据</summary><a href="{src}"><img loading="lazy" src="{src}"></a></details>')
 h.append('<h2>保留的限制</h2><p>数字像素对比工程阈值不是玩家理解或无障碍认证。静态图不证明费用消费、时延或全角色状态；本次真实消费只覆盖一条守卫格挡链。徽章材质、局部密度、复杂技能与公共魔力完整支付流程仍需继续统一。</p>')
 text='\n'.join(h);(OUT/'comparison_report.html').write_text(text,encoding='utf8')
 links=sorted(set(re.findall(r'(?:href|src)="([^"#]+)"',text)));missing=[x for x in links if not (OUT/x).exists()]
 (OUT/'report_link_audit.json').write_text(json.dumps(dict(local_links=len(links),missing=missing,goal_complete=False),indent=2),encoding='utf8')
 print('Report published; missing links',missing)
if __name__=='__main__':main()
