"""Publish preserved source-drawer candidates, actual evidence and independent review."""
import json,shutil
from publish_battlefield_readability import OUT,read,link,image,esc

def main():
    c=OUT/'attempt53'
    for rel in ['after_collision_fixed/party_feedback_manifest.json','collision_repair/geometry.json','collision_repair/evidence/manifest.json','collision_repair/journal/manifest.json','source_validation_collision_fixed/manifest.json','geometry_audit_collision_fixed.json']:
        assert read(c/rel)['passed'],rel
    assert (c/'scene_review_collision_fixed/review.md').exists()
    s=read(OUT/'normal_resume_after_attempt53/status.json')
    assert s['isPlaying'] and not s['isCompiling'] and s['actualConsoleErrorCount']==0
    m=read(c/'action_mapping_collision_fixed.json')['paths']
    body=['<!doctype html><meta charset="utf-8"><title>53：资源身份与战场内源骰</title>',
    '<style>body{background:#172521;color:#eee;font:17px/1.65 system-ui;max-width:1720px;margin:24px auto;padding:20px}a{color:#edca87}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 24px}img{width:100%}pre{white-space:pre-wrap;background:#21332c;padding:20px}.notice{background:#4e3a29;padding:18px}</style>',
    '<h1>53：六色身份、实际源骰与战斗上下文</h1>',
    '<p class="notice">整体目标未完成。本轮将六色名称和悬浮入口提示补回资源栏，把公共魔力从全屏文字列表改为战场右侧的源骰抽屉。独立审查发现阶段栏与结算条碰撞，原候选被拒绝；保留拒绝原图后修复，再补20张实际图独立复审。下方分别展示改善、代价及仍存问题。</p>',
    '<p>画布1920×1080；数字26pt，六色名称和解释22pt。源骰沿用真实原生有序索引与正式取用回调，普通/额外取用分开，额度由原生状态决定。取消或查看完整战术再返回，不消费资源、不清空当前所选卡牌。金骰与黑骰的原规则区别保留。</p>',
    '<p>参考仅复用已归档实机观察O1紧凑资源、O4按需详情：<a href="../../workflow_revisions/20261023_combat_focus_v3/reference_sts2/index.html">STS2原参考、7项观察及未验证范围</a>。没有声称复刻STS2具体美术或改变MageKnight规则。</p>',
    '<h2>独立Sol复审：完整原文和评分</h2><p>'+link(c/'scene_review_collision_fixed/protocol.md','审查协议')+' · '+link(c/'scene_review_collision_fixed/inputs.json','20张证据与SHA')+' · '+link(c/'scene_review_collision_fixed/review.json','逐项评分JSON')+'</p><pre>'+esc((c/'scene_review_collision_fixed/review.md').read_text(encoding='utf8'))+'</pre>',
    '<details><summary>原始16图审查：明确拒绝，不能覆盖</summary><pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf8'))+'</pre><p>'+link(c/'scene_review/review.json','原始评分JSON')+'</p></details>',
    '<h2>关键状态：52基线 / 修正53</h2>']
    for label in ['three_enemies','source_panel','source_taken','selected_action_payment','source_consumed']:
        body+=['<h3>'+esc(label)+'</h3><div class="pair">'+image(c/'before/key_phases'/label/'screen.png','52实际基线')+image(c/'after_collision_fixed/key_phases'/label/'screen.png','53当前修正')+'</div>']
    body+=['<h2>真实操作路径与成本</h2><p>同一公共源骰→同一强化卡链：'+str(m[0]['clicks'])+'→'+str(m[1]['clicks'])+'次点击；已知路径内指针距离 '+str(round(m[0]['known_pointer_distance_pixels']))+'→'+str(round(m[1]['known_pointer_distance_pixels']))+'px。点击数没有下降，主要改善是取骰时保留敌人和手牌上下文。首段指针未知，单条脚本不能外推全局负担、最短路径或真人体验。</p><p>'+link(c/'action_mapping_collision_fixed.json','action→实际有序骰索引→UI路径→真实点击点与原生SHA')+'</p>',
    '<h2>补齐三敌、悬浮、完整战术与日志证据</h2>']
    for label in ['three_enemies_source_open','three_enemies_tactic_details','Red_hover','Blue_hover','Green_hover','White_hover','Gold_hover','Black_hover']:
        body.append(image(c/'collision_repair/evidence/key_phases'/label/'screen.png',label))
    body+=['<p>六色悬浮实际展开，22pt完整字形生成，原生状态不变。完整战术与日志仍是按需阅读页，不代表所有内容都已转成符号。日志验证实际消费后打开全文、通过原有末行按钮读到最后字形、返回原状态；本轮没有物理滚轮或真人测试。</p>',image(c/'collision_repair/journal/key_phases/complete_journal_end/screen.png','实际日志末行全文'),
    '<h2>修复前的失败必须保留</h2><div class="pair">'+image(c/'after_final/key_phases/source_consumed/screen.png','被拒绝：阶段与结算条重叠')+image(c/'after_collision_fixed/key_phases/source_consumed/screen.png','修正：阶段y112，结算条y166，敌意图y210')+'</div><p>新增门禁比较完整33px结算条与阶段、敌意图矩形，而非只比较缩字后的文字。原生状态相同，阶段仍26pt。旧geometry_audit未覆盖这一碰撞，旧通过结果不作为无退化证明。</p>',
    '<h2>25个重要阶段与典型界面</h2>']
    for p in sorted((c/'after_collision_fixed/key_phases').glob('*/screen.png')):
        old=c/'before/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(old,'52同名实际场景') if old.exists() else '<p>缺同名旧图，不补造</p>')+image(p,'53当前实际场景')+'</div>']
    body+=['<h2>正式消费、源代码和审计</h2><p>普通取骰→原卡强化，额外白骰、黑骰转白、金骰均与SHA绑定的历史完整原生后态一致；选择本身不等于消费。当前修正只移动结算条，旧资源溢出/无尽红黑测试单独保留在resource_validation_final，不冒充当前程序集重测。不是全规则、所有骰数/战术或完整32项rubric验收。</p><ul>']
    for rel in ['collision_repair/compile_receipt.json','source_manifest_collision_fixed.json','geometry_audit_collision_fixed.json','collision_repair/geometry.json','after_collision_fixed/party_feedback_manifest.json','after_collision_fixed/source_intent.json','source_validation_collision_fixed/manifest.json','collision_repair/evidence/manifest.json','collision_repair/journal/manifest.json','resource_validation_final/manifest.json','source_schema_failure.json']:
        body+=['<li>'+link(c/rel,rel)+'</li>']
    body+=['</ul><h2>接下来必须推进的原始目标</h2><p>技能绿纸与泛用魔杖图标还未统一；右上规则段落和重复结算条仍有文本负担；英雄/佣兵/怪物的左右面向、英雄视觉层级、佣兵就绪/耗竭/受伤状态符号、环境光与接触感还需实际改进和审查。本轮的局部改进不代表这些要求已完成。</p><p>'+link(OUT/'attempt52/comparison_report.html','52版原报告')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf8')
    print('53 published; overall objective remains incomplete')

if __name__=='__main__':main()
