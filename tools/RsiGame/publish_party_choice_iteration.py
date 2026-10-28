"""Publish46 without replacing rejected reviews or relabelling older visual evidence."""
from pathlib import Path
import json,html,shutil
from publish_battlefield_readability import OUT,link,image,read,esc

def main():
 current=OUT/'attempt46'
 pages=['<!doctype html><meta charset="utf-8"><title>技能与部队操作带候选46</title>',
 '<style>body{background:#14201f;color:#e5e9df;font:17px/1.65 system-ui;margin:24px auto;max-width:1600px;padding:0 22px}a{color:#d8bd79}h1,h2{color:#e8ca89}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 20px}img{width:100%;height:auto}figcaption{font-size:15px}pre{white-space:pre-wrap;background:#1d2b29;padding:20px}.notice{border-left:5px solid #d8a466;padding:12px;background:#28302b}@media(max-width:900px){.pair{grid-template-columns:1fr}}</style>',
 '<h1>技能与部队操作带：候选46</h1>',
 '<p class="notice">目标未完成，也未宣布整体设计通过。修正版保留技能频次、数值和规则；部队详细能力与费用可以悬浮阅读。独立审查仍指出首次入口发现性不足（2/5），场景接地、角色光照、目标双环与素材身份问题仍需继续改善。</p>',
 '<p>技能从单张大说明页翻页，改为同一操作带并列显示。本固定例同屏可选技能由1项变3项；规则22号字、名称/状态26号字，不以删数值或缩字换空白。部队使用已有的独立全身素材，显示当前等级、护甲、创伤与就绪状态，完整原能力和印刷费用通过悬浮滚动查看。角色在主战场的尺寸未变。</p>',
 '<p>反馈文字与底衬现在共用一个可见性容器：显示期间保持不透明，到期一起隐藏。实际普通格挡消费后检查了显示、保持、消失和日志回查。操作带翻动位置也随重绘保存；新测试场景会重置。</p>',
 '<h2>技术证据与范围</h2>',
 '<p>'+link(current/'geometry_audit.json','46组同状态/点击区域/主战场图像核对；69个操作带文字块')+' · '+link(current/'source_manifest.json','候选源码SHA')+' · '+link(current/'compile_receipt_scroll_fixed.json','最终程序集')+' · '+link(current/'completed_runtime_status.json','验证结束Console状态')+'</p>',
 '<p>'+link(current/'validation_final/party_feedback_manifest.json','重要阶段、公共魔力及真实格挡消费')+' · '+link(current/'four_unit_validation_final/manifest.json','四佣兵布局及正式能力消费')+' · '+link(current/'card_validation_final/manifest.json','原卡全文与蓝魔力消费')+' · '+link(current/'damage_tray_scroll_fixed/manifest.json','第四单位可达与快捷承伤计划增删')+'</p>',
 '<p>'+link(current/'seven_hero_corrected/manifest.json','七英雄正式构筑入口检查（其中Wolfhawk零技能）')+' · '+link(current/'wolfhawk_learned_verified/manifest.json','补充原归档Wolfhawk的两个已学技能入口')+'。合计七英雄14个已学技能入口打开/返回；不是全部70技能的发动或规则验收。</p>',
 '<p>'+link(current/'choice_validation_final_compatibility/manifest.json','操作带选择、规则滚动与反馈生命周期')+' · '+link(current/'roster_information_access/manifest.json','两部队全文每个非空白字形实际阅读覆盖')+' · '+link(current/'roster_complete_tail/manifest.json','补充100%阅读尾帧')+'</p>',
 '<p>46组关闭操作带的最终运行截图保持原生状态、正式控件点击几何和图像不变；可选操作带文字几何审计对应compatibility阶段截图，实际路径已登记，未把不同程序集的截图混作同一批。106份PNG文件SHA未变；最近一次全部106种实机场景为'+link(OUT/'attempt45/asset_cases/manifest.json','第45版')+'，没有标成第46版重测。</p>',
 '<h2>独立评分：保留否定结论</h2>',
 '<p>初版13图：候选3.56，对照3.64，因技能频次/数值和身份辨识退化被否定。修正版13图：候选3.44，对照3.36，仍指出部队信息在这组静态图内未等价呈现。两个独立审查的基线分不同，不能串联为提升曲线；这些五维专项评分也没有替代完整32项rubrics。</p>']
 for folder,label in [('independent_review','初版原始审查'),('independent_review_corrected','修正版静态审查'),('information_access_review','首次按需信息审查'),('information_access_review_complete','补齐中间/尾帧后的信息审查')]:
  base=current/folder;pages+=['<h3>'+label+'</h3><p>'+link(base/'inputs.json','输入截图与SHA')+' · '+link(base/'protocol.md','审查协议')+' · '+link(base/'review.json','逐项评分JSON')+'</p><pre>'+esc((base/'review.md').read_text(encoding='utf8'))+'</pre>']
 pages+=['<p>补充序列的独立信息审查为：决策可读4、按需全文保留4、入口发现性2、身份辨识4、美感3。未发现可确证永久缺失或缩字；但这仍不证明全部30部队、原规则源完整性、真实操作或真人体验。真实操作证据另列在技术凭据中，不能由截图分数代替。</p>',
 '<h2>五组前后对比</h2><p>这部分采用独立修正版审查的原截图，随后仅修复优化交互模式的英雄名称兼容与翻动状态；后续实际场景另列，审查分没有重新贴到后续程序集上。</p>']
 for name in ['neutral','skills_open','skills_selected','roster_open','roster_selected']:
  pages+=['<h3>'+name+'</h3><div class="pair">'+image(current/'before/key_phases'/name/'screen.png','基线45')+image(current/'choice_validation_corrected/key_phases'/name/'screen.png','修正版46审查画面')+'</div>']
 pages+=['<h2>全文与反馈动态证据</h2>']
 for group in ['roster_information_access','roster_complete_tail','damage_tray_scroll_fixed','choice_validation_final_compatibility/feedback']:
  pages+=['<h3>'+group+'</h3>']
  for p in sorted((current/group/'key_phases').glob('*/screen.png')):pages+=[image(p,p.parent.name)]
 pages+=['<h2>重要阶段与典型界面留档</h2>']
 for group,oldgroup in [('validation_final','validation_final'),('four_unit_validation_final','four_unit_validation')]:
  for p in sorted((current/group/'key_phases').glob('*/screen.png')):
   old=OUT/'attempt45'/oldgroup/'key_phases'/p.parent.name/'screen.png'
   if old.exists():pages+=['<h3>'+p.parent.name+'</h3><div class="pair">'+image(old,'45原生同状态')+image(p,'46最终运行')+'</div>']
 pages+=['<h2>留存失败与待办</h2><p>21号字初版、字体参数编译错误、归档序列化错误、把交互输入面板误当普通技能选中的脚本失败、快捷模式英雄名称空引用、翻动后重绘复位，均保留在本目录。没有把失败改成成功。默认关闭的旧快捷模式需显式启用后测试；承伤规划增删没有冒充伤害结算。</p>',
 '<p>下一重点应回到可见的角色接地/光照与有身份依据的素材修订，同时补齐技能/部队首次悬浮入口提示。当前数字槽位仍缺专属技能美术；新操作带不是最终美术方案。</p>',
 '<p>'+link(OUT/'attempt45/comparison_report.html','第45版完整归档')+' · '+link(OUT/'normal_resume_after_attempt46/status.json','恢复普通试玩后的实际状态')+'</p>']
 content='\n'.join(pages)
 destination=current/'comparison_report.html'
 with destination.open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
 shutil.copy2(OUT/'index.html',current/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf8')
 print('46 candidate report published; incomplete design acceptance retained')

if __name__=='__main__':main()
