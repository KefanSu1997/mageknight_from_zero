"""Publish candidate34's matched screenshots without relabelling catalog evidence."""
import html
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
CURRENT=OUT/'attempt34'


def read(p):return json.loads(p.read_text(encoding='utf8'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
def relative(p):return Path(p).relative_to(OUT).as_posix()
def link(p,label):
    assert p.is_file(),p
    return '<a href="'+relative(p)+'">'+html.escape(label)+'</a>'
def picture(p,label):
    assert p.is_file(),p
    return '<figure><a href="'+relative(p)+'"><img loading="lazy" src="'+relative(p)+'"></a><figcaption>'+html.escape(label)+'</figcaption></figure>'


def main():
    validation=CURRENT/('validation_corrected' if (CURRENT/'validation_corrected/party_feedback_manifest.json').exists() else 'validation')
    information=CURRENT/('information_validation_corrected' if (CURRENT/'information_validation_corrected/manifest.json').exists() else 'information_validation_final')
    geometry=CURRENT/('ground_layout_geometry_corrected.json' if (CURRENT/'ground_layout_geometry_corrected.json').exists() else 'ground_layout_geometry.json')
    four=CURRENT/('four_unit_validation_corrected' if (CURRENT/'four_unit_validation_corrected/manifest.json').exists() else 'four_unit_validation')
    for manifest in [validation/'party_feedback_manifest.json',information/'manifest.json',four/'manifest.json',geometry]:
        assert read(manifest)['passed'],manifest
    baseline=OUT/'attempt33/validation_final/key_phases';candidate=validation/'key_phases'
    pairs=[]
    for capture in sorted(candidate.glob('*/capture.json')):
        before=baseline/capture.parent.name/'capture.json'
        assert before.is_file(),before
        a,b=read(before),read(capture)
        sa={v['key']:v['value'] for v in a['observation']['state']};sb={v['key']:v['value'] for v in b['observation']['state']}
        assert sa['audit.costNativeState']==sb['audit.costNativeState'],capture
        pairs.append(dict(label=capture.parent.name,before=relative(before.parent/'screen.png'),after=relative(capture.parent/'screen.png'),same_native=True))
    write(CURRENT/'pairing_audit.json',dict(passed=True,pairs=pairs,scope='Same archived/native checkpoints. Copies are not relabelled as same runtime; no human or shortest-path claim.'))
    page=['<!doctype html><meta charset="utf-8"><title>战斗信息层级 · attempt34</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}figcaption{color:#bac6c4}h1,h2{color:#dcc492}details{margin:24px 0}</style><h1>战斗信息层级 · attempt34</h1>',
        '<p>目标仍进行中。本轮去掉常驻重复操作说明，将其移入实际阶段／所选单位标题悬浮；当前阶段、敌人攻击、有效护甲、抗性、资源与可执行动作保持可见。独立审查发现格挡后续动作发现性下降后，已补上有格挡资源时的“选择一次攻击”，并给阶段帮助增加问号；单位操作悬浮改为短指南，避免重复状态和技术文字。敌人姓名和攻击只保留一层贴合文字的底衬，去掉重复宽黑底。画布1920×1080、角色尺寸和字号保持。</p>',
        '<p>沿用既有杀戮尖塔2参考的信息分层方向；本页只陈述本项目实际实现与证据。</p>',
        '<p>'+link(CURRENT/'compile_receipt.json','实际程序集与Console')+' · '+link(CURRENT/'pairing_audit.json','逐图原生状态对照')+' · '+link(geometry,'敌人文字／字号及完整图像几何')+' · '+link(information/'manifest.json','两个悬浮信息入口的真实raycast与返回')+' · '+link(four/'manifest.json','第四单位消费、字符边界和指挥槽负例')+'</p>',
        '<p>角色素材仍为已验证的106种，'+link(OUT/'attempt33/comparison_report.html','上一版素材、独立评分与完整档案')+'。素材绑定记录属于attempt33，未冒称本轮重新验证106种素材；本轮没有更换PNG。</p>']
    review=CURRENT/'independent_review/review.md'
    page.append('<p>'+link(review,'初版信息层级独立审查（原3.50→3.65评分，不冒称修正后评分）')+'</p>' if review.exists() else '<p>本轮独立审查待完成。</p>')
    recheck=CURRENT/'independent_review/correction_recheck.md'
    if recheck.exists():page.append('<p>'+link(recheck,'恢复当前决策提示、精简悬浮后的独立复核')+'</p>')
    inventory=CURRENT/'visible_information_inventory.json'
    if inventory.exists():
        page.append('<h2>可见文字登记</h2><p>'+link(inventory,'实际渲染字形与动作控件对照')+'：四个配对场景的动作控件路径、可用状态与可点状态相同。字形数量仅描述画面信息量，不能当作认知负担或真人表现。</p><table><tr><th>场景</th><th>基线字形</th><th>当前字形</th></tr>')
        for row in read(inventory)['records']:
            page.append('<tr><td>'+row['label']+'</td><td>'+str(row['before_visible_glyphs'])+'</td><td>'+str(row['after_visible_glyphs'])+'</td></tr>')
        page.append('</table>')
    for label in ['three_enemies_neutral','unit_selected_in_block','unit_consumed','ranged','assign_damage','melee_two_enemies','won_result','end_mixed']:
        pair=next(r for r in pairs if r['label']==label)
        page.append('<h2>'+html.escape(label)+'</h2><div class="pair">'+picture(OUT/pair['before'],'attempt33基线')+picture(OUT/pair['after'],'attempt34候选')+'</div>')
    page.append('<h2>按需说明与返回</h2>')
    for label in ['phase_before','phase_hover','phase_returned','unit_before','unit_hover','unit_returned']:
        page.append('<details><summary>'+label+'</summary>'+picture(information/'key_phases'/label/'screen.png',label)+'</details>')
    page.append('<h2>其余重要阶段与典型状态</h2>')
    for pair in pairs:
        page.append('<details><summary>'+html.escape(pair['label'])+'</summary><div class="pair">'+picture(OUT/pair['before'],'attempt33')+picture(OUT/pair['after'],'attempt34')+'</div></details>')
    costs=read(validation/'source_intent.json')
    page.append('<h2>边界与下一步</h2><p>公共魔力同牌强化仍为'+str(costs['clicks'])+'次点击，未证明总体负担下降。阶段帮助的悬浮可达不等于新手易发现；正文全文仍由全局Wiki提供。仍需改善角色光照与接地、素材身份线索、可用动作视觉重点及复杂上下文。静态模型评分与脚本验证不代替真人验收。</p>')
    text='\n'.join(page)
    backup=CURRENT/'previous_index.html'
    if not backup.exists():shutil.copyfile(OUT/'index.html',backup)
    (OUT/'index.html').write_text(text,encoding='utf8')
    (CURRENT/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
    write(CURRENT/'publication.json',dict(passed=True,goal_complete=False,paired_screenshots=len(pairs),runtime=read(CURRENT/'compile_receipt.json'),previous='attempt33',source_intent=costs,scope='Publication and actual checks; independent visual review retains its own evidence inventory.'))
    print('Published attempt34,',len(pairs),'actual same-state screenshot pairs')


if __name__=='__main__':main()
