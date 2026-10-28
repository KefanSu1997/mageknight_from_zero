"""Candidate35: retain exact screenshot pairs and finite hover/focus evidence."""
import json
import shutil
from pathlib import Path
from publish_embedded_hierarchy_report import OUT, read, write, relative, link, picture

CURRENT=OUT/'attempt35'


def native(record):return {v['key']:v['value'] for v in record['observation']['state']}['audit.costNativeState']


def main():
    compact=CURRENT/('compact_validation_selected_fix' if (CURRENT/'compact_validation_selected_fix/manifest.json').exists() else 'compact_validation_final')
    for p in [compact/'manifest.json',CURRENT/'validation/party_feedback_manifest.json',CURRENT/'four_unit_validation/manifest.json',CURRENT/'ground_layout_geometry.json']:
        assert read(p)['passed'],p
    pairs=[]
    for label in ['phase_hover','unit_hover']:
        before=OUT/'attempt34/information_validation_corrected/key_phases'/label
        after=compact/'key_phases'/label
        a,b=read(before/'capture.json'),read(after/'capture.json');assert native(a)==native(b),label
        def fixture(r):return json.loads({v['key']:v['value'] for v in r['observation']['state']}['audit.costFixture'])
        fa,fb=fixture(a),fixture(b)
        for key in ['uiUnit','uiTargets']:assert fa[key]==fb[key],(label,key)
        pairs.append(dict(label=label,before=relative(before/'screen.png'),after=relative(after/'screen.png'),native_equal=True,selected_unit_and_targets_equal=True))
    write(CURRENT/'hover_pairing_audit.json',dict(passed=True,pairs=pairs))
    page=['<!doctype html><meta charset="utf-8"><title>战斗悬浮与实体焦点 · attempt35</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}figcaption{color:#bac6c4}h1,h2{color:#dcc492}details{margin:24px 0}</style><h1>战斗悬浮与实体焦点 · attempt35</h1>',
        '<p>短操作说明改为资源栏下方的横向信息区，保留21pt正文与24pt标题。精简重复措辞，保留生成资源→选择一次攻击→确认格挡及完整格挡/迅捷/元素条件；完整规则仍在Wiki。问号移近当前阶段并增加深色圆底，未缩放角色或画布。</p>',
        '<p>英雄、佣兵和敌人脚下增加仅悬浮时显示的浅色焦点圈；保留原有选中反馈（单位金圈，敌人仍有既有标记）；已选中的单位或敌人不再叠加浅色圈。浅色圈表示指针所指实体，不表示该实体当前具备合法发动能力。移出后消失，正式选择与规则状态不变。</p>',
        '<p>'+link(compact/'manifest.json','4个短说明场景的真实边界/alpha框检查及3种实体焦点进入退出')+' · '+link(CURRENT/'hover_pairing_audit.json','悬浮配对原生状态与单位/目标核对')+' · '+link(CURRENT/'ground_layout_geometry.json','25组阶段字形和原图尺寸/完整UV核对')+' · '+link(CURRENT/'validation/party_feedback_manifest.json','正式阶段/单位消费与按需阅读')+' · '+link(CURRENT/'four_unit_validation/manifest.json','第四单位消费与容量负例')+' · '+link(CURRENT/'compile_receipt.json','实际程序集与Console')+' · '+link(CURRENT/'evidence_versions.json','阶段/悬浮证据各自的运行版本')+'</p>']
    review=CURRENT/'independent_review/review.md'
    page.append('<p>'+link(review,'本轮独立Sol截图审查')+'</p>' if review.exists() else '<p>独立审查待完成，未宣称视觉验收。</p>')
    page.append('<p>独立审查实际看8图；两组短说明配对五维均分2.8→4.0/5，各10项，额外角色图不混入均值。原评分发现的焦点圈叠加另行修正、另看3图复核；此分数不代表修正后整体界面重评分或全游戏验收。阶段截图保留修正前真实程序集，最终只复跑受影响的悬浮/焦点套件，版本边界见上方凭据。</p>')
    recheck=CURRENT/'independent_review/focus_recheck.md'
    if recheck.exists():page.append('<p>'+link(recheck,'避免悬浮与选中圈叠加的独立复核')+'</p>')
    page.append('<p>'+link(CURRENT/'unchanged_controls_and_figures.json','25组动作控件与所有原图绘制参数保全')+' · '+link(CURRENT/'compact_validation/failure.json','第一版短说明的边界失败与修正')+'</p>')
    for pair in pairs:page.append('<h2>'+pair['label']+'</h2><div class="pair">'+picture(OUT/pair['before'],'attempt34 · 固定左侧说明')+picture(OUT/pair['after'],'attempt35 · 顶部短说明')+'</div>')
    page.append('<h2>英雄、佣兵、敌人的指针焦点</h2>')
    for label in ['hero','unit','enemy']:
        page.append('<details><summary>'+label+'</summary><div class="pair">'+picture(compact/'key_phases'/(label+'_focus_hover')/'screen.png','实际悬浮 · 浅色焦点圈')+picture(compact/'key_phases'/(label+'_focus_returned')/'screen.png','移出后 · 焦点圈关闭')+'</div></details>')
    for label,title in [('selected_unit_focus_hover','悬浮已选佣兵：不叠加浅色圈'),('selected_enemy_focus_hover','悬浮已选敌人：保留既有选中反馈'),('four_unit_phase_hover','四单位条件场景：阶段帮助'),('four_unit_guidance_hover','四单位条件场景：单位操作说明')]:
        page.append('<details><summary>'+title+'</summary>'+picture(compact/'key_phases'/label/'screen.png',title)+'</details>')
    page.append('<h2>重要阶段前后留档</h2>')
    for path in sorted((CURRENT/'validation/key_phases').glob('*/capture.json')):
        old=OUT/'attempt34/validation_corrected/key_phases'/path.parent.name/'capture.json';assert old.is_file() and native(read(old))==native(read(path)),path
        page.append('<details><summary>'+path.parent.name+'</summary><div class="pair">'+picture(old.parent/'screen.png','attempt34')+picture(path.parent/'screen.png','attempt35')+'</div></details>')
    page.append('<h2>限制与下一步</h2><p>严格遮挡检查仅覆盖4个短帮助上下文，以PNG所有alpha&gt;0像素的保守外框核对，不证明全游戏、所有英雄或长实体详情均不遮挡。普通实体长悬浮仍沿用旧布局，必须继续改善。106种素材沿用上一版，未重新声称本轮逐项绑定；光照、接地、身份对应和行动重点仍有未完成项。没有真人或全局交互成本验收。</p><p>'+link(OUT/'attempt34/comparison_report.html','完整上一版本、原评分及修正审查')+' · '+link(OUT/'attempt33/comparison_report.html','106种素材与生成记录')+'</p>')
    text='\n'.join(page)
    if not (CURRENT/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',CURRENT/'previous_index.html')
    (OUT/'index.html').write_text(text,encoding='utf8')
    (CURRENT/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
    print('Published candidate35 with real finite scope; goal remains incomplete')


if __name__=='__main__':main()
