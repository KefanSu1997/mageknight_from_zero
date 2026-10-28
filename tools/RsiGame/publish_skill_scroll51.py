"""Archive the pressure-review-driven selection and hint repair."""
import shutil
from publish_battlefield_readability import OUT,read,link,image,esc
def main():
    c=OUT/'attempt51';prev=OUT/'attempt50'
    checks=['after/manifest.json','pressure/five_skills/manifest.json','skill_readers/manifest.json','validation/party_feedback_manifest.json','geometry_audit.json']
    assert all(read(c/p)['passed'] for p in checks)
    assert (c/'scene_review/review.md').is_file()
    body=['<!doctype html><meta charset="utf-8"><title>51 技能滚动定位修复</title>',
    '<style>body{background:#172521;color:#eee;font:17px/1.65 system-ui;max-width:1720px;margin:24px auto;padding:20px}a{color:#edca87}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 24px}img{width:100%}pre{white-space:pre-wrap;background:#21332c;padding:20px}.notice{background:#4e3a29;padding:18px}</style>',
    '<h1>51：选中技能完整入视口，移除重复提示</h1>',
    '<p class="notice">本版修复独立压力审查发现的选中行裁切和旧提示残留。图标风格、魔力椭圆、角色环境融合等总体目标仍未完成，不将局部功能验证当作最终UI采纳。</p>',
    '<p>选中后定位到所在整排；删除每列相同的悬浮提示，页头保留完整规则/费用的悬浮入口说明，多排时明确显示总排数与左侧箭头。3技能现在一排直接显示，无需额外滚动。5技能实际选择后，各选中点击区域完整落在实测260px裁剪窗口内；名称、状态、频率、精确效果20个文字块仍完整可达，未缩字。</p>',
    '<p>50版完整改动、70摘要语义审查、7英雄14技能与负面评分均保留：'+link(prev/'comparison_report.html','查看50版完整档案')+'</p>',
    '<h2>独立复审原文（10图、5对专项）</h2><p>'+link(c/'scene_review/protocol.md','协议')+' · '+link(c/'scene_review/inputs.json','原图SHA清单')+' · '+link(c/'scene_review/review.json','完整逐项评分')+'</p>',
    '<pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf8'))+'</pre>',
    '<h2>50 / 51 实机对照</h2>']
    for label in ['skills_open','skills_selected']:
        body+=['<h3>'+label+'</h3><div class="pair">'+image(prev/'after_wide_final/key_phases'/label/'screen.png','50：之前')+image(c/'after/key_phases'/label/'screen.png','51：当前')+'</div>']
    for label in ['top','bottom','selected_4']:
        body+=['<h3>5技能：'+label+'</h3><div class="pair">'+image(prev/'pressure_measured/five_skills/key_phases'/label/'screen.png','50：之前')+image(c/'pressure/five_skills/key_phases'/label/'screen.png','51：当前')+'</div>']
    body+=['<h2>当前完整规则与真实消费</h2>']
    for label in ['skill_0_hover_top','skill_1_hover_top','skill_2_hover_top','siege_skill_before','siege_skill_consumed','siege_skill_used_full_rule_recalled']:
        body.append(image(c/'skill_readers/key_phases'/label/'screen.png',label))
    body+=['<p>3原规则实际22pt全部读取；正式物理攻城1的消费仅改变攻击贡献与该技能回合已用标记。已用后仍可回查全文，直接确认禁用。7英雄14技能入口/全文来自50版验证，不冒充51版全部重测，更不外推70技能发动覆盖。</p>',
    '<h2>25个典型界面留档：50 / 51</h2>']
    for p in sorted((c/'validation/key_phases').glob('*/screen.png')):
        old=prev/'validation_wide_final/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(old,'50同名阶段') if old.exists() else '<p>无旧图</p>')+image(p,'51实机阶段')+'</div>']
    body+=['<h2>证据与范围</h2><ul>']
    for p in checks+['compile_receipt.json','source_manifest.json','geometry_initial_failure.json']:
        body.append('<li>'+link(c/p,p)+'</li>')
    body+=['</ul><p>5组同原生状态检查人物与图标原素材、尺寸、UV、色调及既有可见文字字号/位置保持；实际改变的是技能排布局、点击高度与全局提示，不宣称全部点击几何不变。原先几何脚本误把可选滚动层级与空按钮Label尺寸变化当作图像变化，原错误保留并显式修正映射。所有旧设计与负面审查留档。</p>',
    '<h2>尚需继续</h2><p>亮绿色旧纸图与通用技能图形仍不统一。顶部六色椭圆需要重新设计成可识别的魔力/晶体/公共骰资源系统，保留数值与付款路径。英雄、佣兵的层级和面向、角色与场景的光照接触仍须整体审查。不可用原因与悬浮层避让仍有改进空间；未进行完整32rubric或真人验收。</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf8')
    print('51 report published; goal remains incomplete')
if __name__=='__main__':main()
