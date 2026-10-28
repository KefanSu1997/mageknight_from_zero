"""Publish scoped functional progress without claiming visual acceptance."""
import json,shutil,hashlib
from publish_battlefield_readability import ROOT,OUT,read,link,image,esc

def main():
    c=OUT/'attempt50'
    checks=['after_wide_final/manifest.json','skill_readers_wide_final/manifest.json','inventory_wide_final/seven_hero_manifest.json','pressure_measured/five_skills/manifest.json','validation_wide_final/party_feedback_manifest.json','geometry_audit.json']
    assert all(read(c/p)['passed'] for p in checks)
    body=['<!doctype html><meta charset="utf-8"><title>50 技能对象、精确摘要与完整规则</title>',
    '<style>body{background:#172521;color:#eee;font:17px/1.65 system-ui;max-width:1720px;margin:24px auto;padding:20px}a{color:#edca87}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 24px}img{width:100%}pre{white-space:pre-wrap;background:#21332c;padding:20px}table{border-collapse:collapse;width:100%}td,th{border:1px solid #617064;padding:8px;text-align:left}.notice{background:#4e3a29;padding:18px}</style>',
    '<h1>50：技能区功能与信息改进，视觉目标仍未达成</h1>',
    '<p class="notice">保留精确效果、使用次数与状态，将长规则放到明确的悬浮阅读入口；当前独立审查确认信息与入口改善，但技能图标统一性 D、美感 E 仍为 2/5。此版是可回查的功能进展，不是最终视觉采纳或整体完成。</p>',
    '<p>原有数字槽改为技能名称、状态和身份图形；首次去掉精确效果的方案被审查指出信息退化，因此补回原文绑定的简短摘要。名称和状态26pt，频率、摘要和完整阅读22pt。原画布1920×1080、人物尺寸/UV与战斗规则保持。</p>',
    '<p>70条摘要按技能SourceId与完整原文精确匹配；不匹配就回退当前原文。独立语义审查修正5条条件/范围，并核对6条双面技能只采用当前原生载入的side1。70条静态一致性不等于70条实际消费验收。19条原图标区域映射也不等于新生成素材；主场景2张旧绿色图加1个通用图形仍被判不协调。</p>',
    '<h2>独立评分：同一审查者的5对截图</h2><p>A即时精确效果/费用/次数；B按需全文与发现性；C可操作对象及状态一致性；D身份图标与风格；E美感与文档感。1严重失败，2明显问题，3基本可用，4良好，5优秀；—为不适用。仅16图专项，不替代完整32条rubric，不与旧审查拼成连续曲线。</p>',
    '<table><tr><th>场景</th><th>之前 A/B/C/D/E</th><th>当前 A/B/C/D/E</th></tr>']
    review=read(c/'scene_review_wide_final/review.json')
    for row in review['pairs']:
        score=lambda v:' / '.join('—' if v[k] is None else str(v[k]) for k in 'ABCDE')
        body.append('<tr><td>'+esc(row['id'])+'</td><td>'+score(row['before'])+'</td><td>'+score(row['after'])+'</td></tr>')
    body+=['</table><p>'+link(c/'scene_review_wide_final/protocol.md','审查协议')+' · '+link(c/'scene_review_wide_final/inputs.json','16图输入及SHA')+' · '+link(c/'scene_review_wide_final/review.json','完整逐项JSON')+'</p>',
    '<pre>'+esc((c/'scene_review_wide_final/review.md').read_text(encoding='utf8'))+'</pre>',
    '<h2>优化前 / 当前版本</h2>']
    for label in ['neutral','skills_open','skills_selected','roster_open','roster_selected']:
        body+=['<h3>'+esc(label)+'</h3><div class="pair">'+image(c/'before/key_phases'/label/'screen.png','优化前：实际同原生状态')+image(c/'after_wide_final/key_phases'/label/'screen.png','当前：实机截图')+'</div>']
    body+=['<h2>完整规则、实际消费与消费后回查</h2>']
    for label in ['skill_0_hover_top','skill_1_hover_top','skill_2_hover_top','siege_skill_before','siege_skill_consumed','siege_skill_used_full_rule_recalled']:
        body.append(image(c/'skill_readers_wide_final/key_phases'/label/'screen.png',label))
    body+=['<p>正式使用冰封之力后，仅 attacks 和 usedSkillsTurn 改变：增加1点物理攻城、记录精确SourceId；本轮已用白晶和本回合已用冰封的直接确认均禁用。特殊储存/贡献入口保持独立处理，没有将所有已用技能入口一概封锁。</p>',
    '<h2>7英雄与5技能压力场景</h2><p>7位英雄各2项，共14项当前学习技能的原文绑定摘要、22pt全文全部字形和实际入口/返回。不是全70技能发动或全部条件分支。</p>']
    for hero in ['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']:
        body.append('<p>'+link(c/'inventory_wide_final'/hero/'formal_entry.json',hero+' 正式构筑与来源')+'</p><div class="pair">'+image(c/'inventory_wide_final'/hero/'key_phases/skill_0_reader_top/screen.png',hero+' 技能1全文')+image(c/'inventory_wide_final'/hero/'key_phases/skill_1_reader_top/screen.png',hero+' 技能2全文')+'</div>')
    for label in ['top','bottom','selected_4']:
        body.append(image(c/'pressure_measured/five_skills/key_phases'/label/'screen.png','实际等级10、5技能：'+label))
    if (c/'scene_review_wide_final/pressure_review.md').exists():
        body+=['<p>以下3图补审单列，不改变上方16图评分分母。</p><pre>'+esc((c/'scene_review_wide_final/pressure_review.md').read_text(encoding='utf8'))+'</pre>']
    body+=['<h2>重要阶段和典型界面：前后留档</h2><p>25个当前实机阶段保留；以下同名旧图来自50前的实际验证，不将不同状态或不同程序集伪称逐像素因果对照。5组严格同原生配对见上文几何审计。</p>']
    for p in sorted((c/'validation_wide_final/key_phases').glob('*/screen.png')):
        baseline=OUT/'attempt49/validation/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(baseline,'优化前的同名阶段（旧版本）') if baseline.exists() else '<p>没有同名优化前图；不补造基线</p>')+image(p,'当前实际阶段')+'</div>']
    body+=['<h2>可复现检查与版本范围</h2><p>产品候选MVID a2d298d9；后续d7cfc93f仅增加ChoiceViewport实际裁剪边界的诊断导出，未修改产品布局/规则。3技能读取/消费、7英雄与截图审查属于前者；测量后的5技能与25阶段属于后者。</p><ul>']
    for p in checks+['summary_semantic_review_corrected/review.json','source_manifest.json','preserved_failures.json']:
        body.append('<li>'+link(c/p,p)+'</li>')
    body+=['</ul><p>失败变体、负面评分与源文件均留存。物理鼠标触发手牌预览导致即时反馈检查失败，已用真实Windows操作移到标题栏，在同一产品版本重新通过；未改时钟或伪造反馈。</p>',
    '<p>'+link(c/'scene_review/review.json','首次截图审查')+' · '+link(c/'scene_review_corrected/review.json','隐藏精确效果的负面审查')+' · '+link(c/'summary_semantic_review/review.json','摘要原始语义审查（含失败项）')+' · '+link(OUT/'attempt49/comparison_report.html','49未采纳投影与48回退')+'</p>',
    '<h2>继续优化的具体方向</h2><p>统一技能图形的材质、轮廓与语义；撤去每列重复提示。重新设计顶部六色椭圆，区分临时魔力、永久晶体和公共源骰，同时保留数量、费用与实际消费路径。角色面向、英雄/佣兵层级、环境光与接地感仍需整体审查；不能以本轮功能检查通过宣称完成。对不可用操作给出实际条件原因，并避免悬浮层遮挡仍需核对的选择信息。</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html')
    (OUT/'index.html').write_text(content,encoding='utf8')
    print('50 report published; whole goal incomplete')

if __name__=='__main__':main()
