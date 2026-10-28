"""Publish actual asset expansion and layout evidence, retaining every capture version."""
import hashlib
import argparse
import html
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
CURRENT=OUT/'attempt33'
CASE=CURRENT/'asset_cases'
VALIDATION=CURRENT/'validation'


def read(p):return json.loads(p.read_text(encoding='utf8'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
def rel(p):return Path(p).relative_to(OUT).as_posix()
def link(p,title):
    assert p.is_file(),p
    return '<a href="'+rel(p)+'">'+html.escape(title)+'</a>'
def picture(p,title):
    assert p.is_file(),p
    return '<figure><a href="'+rel(p)+'"><img loading="lazy" src="'+rel(p)+'"></a><figcaption>'+html.escape(title)+'</figcaption></figure>'


def main():
    global CURRENT, CASE, VALIDATION
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--attempt',default='attempt33')
    parser.add_argument('--case-directory',default='asset_cases')
    parser.add_argument('--validation-directory',default='validation')
    args=parser.parse_args()
    CURRENT=OUT/args.attempt;CASE=CURRENT/args.case_directory;VALIDATION=CURRENT/args.validation_directory
    journal_base=CURRENT/('journal_validation_retry' if (CURRENT/'journal_validation_retry/manifest.json').exists() else 'journal_validation')
    full=read(VALIDATION/'party_feedback_manifest.json');assert full['passed']
    assert read(CASE/'manifest.json')['passed']
    names={x['id']:x['monster_name'] for x in read(ROOT/'resources/text_json/monster.json')}
    for name in ['army.json','army_2.json']:
        names.update({x['id']:x['card_name_cn'] for x in read(ROOT/'resources/text_json'/name) if 'card_name_cn' in x})
    inventory={}
    for kind,category in [('hero','HeroCharacters'),('monster','EnemyCharacters'),('unit','UnitCharacters')]:
        assets={}
        for folder in ['embedded_v1','embedded_v2']:
            assets.update({p.stem:p for p in (ROOT/'Assets/Resources/UI'/category/folder).glob('*.png')})
        inventory[kind]=[dict(id=i,name=names.get(i,i),asset=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for i,p in sorted(assets.items())]
    cases={}
    for folder in ['attempt16/all_current_asset_cases','attempt16/remaining_asset_cases_retry','attempt19/new_unit_cases','attempt23/after_assets','attempt24/asset_cases','attempt25/asset_cases_retry','attempt26/asset_cases','attempt27/asset_cases','attempt28/asset_cases','attempt29/asset_cases','attempt30/asset_cases',rel(CASE)]:
        base=OUT/folder
        for p in sorted(base.glob('*_result.json')):
            row=read(p);assert row['binding_verified']
            screen=base/'key_phases'/(row['kind']+'_'+row['identity'])/'screen.png';assert screen.is_file()
            cases[row['kind'],row['identity']]=dict(**row,screenshot=rel(screen),result=rel(p),evidence_version=folder.split('/')[0])
    for kind,rows in inventory.items():assert all((kind,row['id']) in cases for row in rows),kind
    counts={k:len(v) for k,v in inventory.items()};totals=dict(hero=7,monster=69,unit=30)
    write(OUT/'asset_coverage.json',dict(resources=inventory,counts=counts,totals=totals,remaining={k:totals[k]-counts[k] for k in totals},runtime_cases=list(cases.values()),scope='Actual binding/native snapshot checks, not all-ability or visual acceptance. Each capture retains its real evidence version.'))
    pairs=[]
    for row in read(OUT/'generation_batch6.json'):
        identity=row['id'];kind='monster' if identity.startswith('monster_') else 'unit';label=kind+'_'+identity
        before=OUT/'attempt23/before_assets';after=CASE
        a,b=read(before/'setups'/(label+'.json')),read(after/'setups'/(label+'.json'))
        assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
        pairs.append(dict(id=identity,before=rel(before/'key_phases'/label/'screen.png'),after=rel(after/'key_phases'/label/'screen.png'),same_fixture=True,same_native_state=True))
    write(CURRENT/'pairing_audit.json',dict(passed=True,pairs=pairs,scope='Exact same archived fixture and actual native snapshot; images include asset and layout changes.'))
    expansion_pairs=[]
    expansion_before=OUT/'attempt27/before_assets'
    if (expansion_before/'manifest.json').exists() and all((CASE/(('monster_' if row['id'].startswith('monster_') else 'unit_')+row['id']+'_result.json')).exists() for row in read(OUT/'generation_batch7.json')):
        for row in read(OUT/'generation_batch7.json'):
            identity=row['id'];kind='monster' if identity.startswith('monster_') else 'unit';label=kind+'_'+identity
            before=expansion_before;after=CASE
            a,b=read(before/'setups'/(label+'.json')),read(after/'setups'/(label+'.json'))
            assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
            expansion_pairs.append(dict(id=identity,before=rel(before/'key_phases'/label/'screen.png'),after=rel(after/'key_phases'/label/'screen.png'),same_fixture=True,same_native_state=True))
        write(CURRENT/'expansion_pairing_audit.json',dict(passed=True,pairs=expansion_pairs))
    review=CURRENT/'independent_review/review.json'
    source_cost=read(VALIDATION/'source_intent.json')
    old_cost=read(OUT/'attempt25/validation/source_intent.json')
    distance_change=100*(source_cost['within_path_distance_pixels']/old_cost['within_path_distance_pixels']-1)
    write(OUT/'current_audit.json',dict(latest_verified=CURRENT.name,completed=False,counts=counts,totals=totals,source_runtime=read(CURRENT/('final_compile_receipt.json' if (CURRENT/'final_compile_receipt.json').exists() else 'compile_receipt.json')),validation=full,asset_cases=rel(CASE/'manifest.json'),pairing_audit=rel(CURRENT/'pairing_audit.json'),independent_review=read(review) if review.exists() else 'pending',latest_material_review='attempt23/independent_review/material_review.json',canvas=[1920,1080],remaining=['Enemy ground contact, lighting, proportions and identity fidelity','On-demand information and actionable-object clarity','Four allies limited conditional cases only; larger and complex contexts unproved','Whole-game and human acceptance unproved']))
    page=['''<!doctype html><meta charset="utf-8"><title>战斗UI · attempt25</title><style>body{background:#101b20;color:#e4e7df;font:18px system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}figcaption{margin:8px 0 20px;color:#b9c7c5}h1,h2{color:#dcc492}p{line-height:1.7}details{margin:20px 0}section{margin:40px 0}</style><h1>透明角色与战斗信息层级 · attempt25</h1>''',
        f'<p>已接入英雄 {counts["hero"]}/7、怪物 {counts["monster"]}/69、佣兵 {counts["unit"]}/30。资源绑定不等于场景融合、全部交互或最终视觉验收；目标仍未完成。本页为 UI 候选编号，不是原 RSI 实验轮次。</p>',
        '<p>画布1920×1080，保留英雄、手牌和角色绘制尺寸。佣兵在地面排布，21pt长姓名全文门禁继续生效。右侧动作区按实际文字高度重排，完整结算保留阅读区。第四单位的有限条件实例见本轮独立章节，不外推全部构筑。</p>',
        '<p>实际验证：关键阶段、公共魔力强化同一张牌、佣兵格挡4与耗竭禁用、规则/所选全文打开返回、手牌悬浮/移出、敌人点击选中。脚本与静态审查均不替代真人、全能力或全游戏验收。</p>',
        f'<p>交互代价取舍：同一公共魔力强化路径当前为{source_cost["clicks"]}次点击，已知段鼠标行程从attempt25的{old_cost["within_path_distance_pixels"]:.0f}px变为当前的{source_cost["within_path_distance_pixels"]:.0f}px（{distance_change:+.1f}%）。不能用构图得分代替成本改善；起始鼠标位置未知，未计算完整意图总距离。</p>',
        '<p>'+' · '.join([link(OUT/'asset_coverage.json','资源SHA与逐项绑定'),link(OUT/'current_audit.json','当前审计'),link(VALIDATION/'party_feedback_manifest.json','本版操作验证'),link(CURRENT/'pairing_audit.json','8组同状态对照'),link(OUT/'generation_batch6.json','完整生成prompt与来源')])+'</p>',
        '<h2>当前三敌战斗</h2>'+picture(VALIDATION/'key_phases/three_enemies_neutral/screen.png','真实验证构筑，非默认开局。')]
    page.append('<h2>独立审查与完整评分</h2><p>不同范围和评分锚点不合成同一总分，旧分数不迁移到新版本。</p>')
    if CURRENT.name=='attempt33':
        batch11_pairs=[]
        for row in read(OUT/'generation_batch11_selected.json'):
            identity=row['id'];label=('monster_' if identity.startswith('monster_') else 'unit_')+identity
            before=CURRENT/'before_assets';a,b=read(before/'setups'/(label+'.json')),read(CASE/'setups'/(label+'.json'))
            assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
            batch11_pairs.append(dict(id=identity,before=rel(before/'key_phases'/label/'screen.png'),after=rel(CASE/'key_phases'/label/'screen.png'),same_native_state=True))
        write(CURRENT/'batch11_pairing_audit.json',dict(passed=True,pairs=batch11_pairs,scope='24 identical archived/native contexts; simultaneous visible art changes are disclosed, no isolated causal claim.'))
        page.append('<h2>本轮补齐24种角色素材 · attempt33</h2><p>16种怪物与8种佣兵分别以原卡为身份参考，勇者三种原图不同，分别生成。106种资源绑定仍不等于全部规则、交互场景或最终美术验收。</p><p>'+link(OUT/'generation_batch11_selected.json','最终PNG、prompt与来源')+' · '+link(CURRENT/'import_audit.json','alpha、边距及原始PNG SHA')+' · '+link(CURRENT/'batch11_pairing_audit.json','24组同状态对照')+' · '+link(CURRENT/'review_protocol.md','独立审查prompt与输入组织')+'</p>')
        page.append('<p>三位独立Sol审查各实际查看24图（8原图＋8前＋8后），五维1–5分：怪物A组2.800→4.200，怪物B组2.800→4.150，佣兵组1.000→4.225（均40项）；佣兵四个去重构筑另算1.000→4.1583（20项）。这些是素材静态评分，不与后面的五维布局评分合并，也不证明交互质量。066头数对应、063火焰线索、暗部与接地问题仍保留在各自审查中。</p>')
        for pair in batch11_pairs:
            page.append('<details><summary>'+pair['id']+'</summary><div class="pair">'+picture(OUT/pair['before'],'替换前 · attempt32')+picture(OUT/pair['after'],'替换后 · attempt33')+'</div></details>')
        for name,title in [('enemy_a','8种怪物独立审查A'),('enemy_b','8种怪物独立审查B'),('unit_material','8种佣兵独立审查')]:
            p=CURRENT/'independent_review'/(name+'.md')
            page.append('<p>'+link(p,title)+'</p>' if p.exists() else '<p>'+title+'：待完成。</p>')
        page.append('<p>'+link(OUT/'attempt32/comparison_report.html','上一版12种素材、接地阴影及独立评分')+' · '+link(OUT/'attempt31/comparison_report.html','固定阅读卡名、滚动进度与实测')+'</p>')
    batch10_before=OUT/'attempt32/before_assets'
    batch10_manifest=OUT/'generation_batch10_selected.json'
    if CURRENT.name=='attempt32' and batch10_manifest.exists():
        batch10_pairs=[]
        for row in read(batch10_manifest):
            identity=row['id'];label=('monster_' if identity.startswith('monster_') else 'unit_')+identity
            a,b=read(batch10_before/'setups'/(label+'.json')),read(CASE/'setups'/(label+'.json'))
            assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
            batch10_pairs.append(dict(id=identity,before=rel(batch10_before/'key_phases'/label/'screen.png'),after=rel(CASE/'key_phases'/label/'screen.png'),same_native_state=True))
        write(CURRENT/'batch10_pairing_audit.json',dict(passed=True,pairs=batch10_pairs,scope='12 same archived fixtures and actual native states. Other party/enemy art can change simultaneously; not isolated causal effects.'))
        page.append('<h2>本轮12张透明角色 · attempt32</h2><p>火焰骷髅、火焰恶魔、兽人冰术士、寒冰卫士、装甲战士、红龙、飞天兽人、巨牙蠕虫，以及勇者、北地僧侣、农民、赤袍僧侣。所有PNG直接使用生成原件，保留原卡并以完整UV绘制。</p><p>'+link(batch10_manifest,'生成提示与原图来源')+' · '+link(CURRENT/'import_audit.json','原始PNG与alpha校验')+' · '+link(CURRENT/'batch10_pairing_audit.json','12组同状态证据')+' · '+link(CURRENT/'review_protocol.md','独立评分prompt与输入组织')+' · '+link(CURRENT/'alpha_gate_correction.json','alpha254误拒绝修正与边界')+' · '+link(OUT/'generation_batch10.json','初版生成记录（含未选版本）')+'</p>')
        page.append('<p>接地阴影改为按可见立绘宽度绘制柔边与窄接触核心，立绘绘制尺寸不变。'+link(CURRENT/'new_material_geometry.json','12组新增素材的原生状态、完整UV与文字核对')+'。勇者采用原卡前景持剑女性代表该部队；未选三人组及alpha254误判记录均保留。alpha最高254的透明原件直接使用，没有改像素凑255。</p>')
        for pair in batch10_pairs:
            page.append('<details><summary>'+pair['id']+'</summary><div class="pair">'+picture(OUT/pair['before'],'替换前 · attempt31运行时')+picture(OUT/pair['after'],'替换后 · attempt32运行时')+'</div></details>')
        for name,title in [('enemy_material','本轮8怪物独立评分'),('unit_material','本轮4佣兵独立评分')]:
            p=CURRENT/'independent_review'/(name+'.md')
            page.append('<p>'+link(p,title)+'</p>' if p.exists() else '<p>'+title+'：待完成。</p>')
        page.append('<p>阅读区沿用'+link(OUT/'attempt31/comparison_report.html','attempt31固定卡名/进度与五类实测')+'；该轮阅读评分不冒称本轮全界面评分。</p>')
    batch9_before=OUT/'attempt30/before_assets'
    if (batch9_before/'manifest.json').exists():
        batch9_pairs=[]
        for row in read(OUT/'generation_batch9.json'):
            identity=row['id'];label=('monster_' if identity.startswith('monster_') else 'unit_')+identity
            a,b=read(batch9_before/'setups'/(label+'.json')),read(CASE/'setups'/(label+'.json'))
            assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
            batch9_pairs.append(dict(id=identity,before=rel(batch9_before/'key_phases'/label/'screen.png'),after=rel(CASE/'key_phases'/label/'screen.png'),same_native_state=True))
        write(CURRENT/'batch9_pairing_audit.json',dict(passed=True,pairs=batch9_pairs,scope='12 same archived/native states; repeated ally builds and simultaneous enemy/ally replacements are disclosed in independent reviews.'))
        page.append('<h2>新增12张透明角色素材 · attempt30</h2><p>8种怪物、4种佣兵，原始生成PNG直接导入。英雄7/7、怪物45/69、佣兵18/30。巨鹰本体偏小、魔宠后方小个体难辨仍待改善；九头蛇原图明确可数5头，未按名称凭空要求9头。部分构筑同时更换敌人与队友，不作单素材因果评分。</p><p>'+link(OUT/'generation_batch9.json','完整prompt与来源')+' · '+link(OUT/'attempt30/import_audit.json','12张导入SHA与透明边距')+' · '+link(CURRENT/'batch9_pairing_audit.json','逐项同状态对照')+'</p>')
        for pair in batch9_pairs:
            page.append('<details><summary>'+pair['id']+'</summary><div class="pair">'+picture(OUT/pair['before'],'替换前')+picture(OUT/pair['after'],'attempt30实际绑定')+'</div></details>')
        for name,title in [('enemy_material','8怪物独立审查（均分2.60→3.93）'),('unit_material','4单位独立审查（2个去重构筑，2.4→3.4）')]:
            page.append('<p>'+link(OUT/'attempt30/independent_review'/(name+'.md'),title)+'</p>')
    if (CURRENT/'reading_validation/manifest.json').exists():
        reading=read(CURRENT/'reading_validation/manifest.json');assert reading['passed']
        page.append('<h2>按需悬浮阅读 · 原卡与大字规则</h2><p>保留300×438完整原卡与22pt规则正文；在手牌上滚轮阅读，移出后恢复正式选择。实际验证基础牌、高级行动、神器、创伤、法术5类上下文，逐字形进入视口且原生状态与动作控件不变。EventSystem滚轮事件不冒充物理鼠标或全291牌验证。</p><p>'+link(CURRENT/'reading_validation/manifest.json','全文、字形、滚轮与返回记录')+'</p>')
        for label,title in [('opening','基础牌'),('complex_action','高级行动'),('artifact','神器'),('wound','创伤'),('spell','法术')]:
            base=CURRENT/'reading_validation/key_phases'
            page.append('<details><summary>'+title+'</summary><div class="pair">'+picture(base/(label+'_original_top')/'screen.png','初次悬浮：完整原卡')+picture(base/(label+'_reading_end')/'screen.png','滚动后的大字规则')+'</div>')
            revisited=base/(label+'_original_revisited')/'screen.png'
            if revisited.exists():page.append(picture(revisited,'向上滚动后回看原卡；正式选中对象保持'))
            page.append('</details>')
    if (CURRENT/'ground_layout_geometry.json').exists():
        geometry=read(CURRENT/'ground_layout_geometry.json')
        page.append('<p>敌人立绘放在庭院地面，名称/选中状态在上方，护甲能力保留脚下。'+link(CURRENT/'ground_layout_geometry.json',str(geometry['matched_captures'])+'组实际文字全文、字号与图片UV对照')+'：未换素材尺寸相等；已声明的素材替换单列，原牌与完整立绘比例不同，不冒称尺寸相等。仍需改善上下扫视、光照和全量上下文。</p>')
    if (CURRENT/'final_phase_geometry.json').exists():
        page.append('<p>'+link(CURRENT/'final_phase_geometry.json','最终运行时25组阶段截图几何对照')+' · '+link(CURRENT/'evidence_versions.json','素材、日志与最终交互证据各自的真实版本')+'。不同版本证据未追认为同一次运行。</p>')
    if (journal_base/'manifest.json').exists():
        journal=read(journal_base/'manifest.json');assert journal['passed']
        page.append('<h2>日志入口与按需帮助</h2><p>去掉挡住弃牌堆的重复操作记录按钮，顶部日志仍可打开完整反馈。底部常驻提示转入规则悬浮和正文；新手发现成本尚未验证。</p><p>'+link(journal_base/'manifest.json',f'实际日志打开、{journal["journal_characters"]}字符保留、末行可见与返回状态验证')+'</p>')
        page.append('<div class="pair">'+picture(OUT/'attempt28/validation/key_phases/unit_consumed/screen.png','上一版：重复日志条与弃牌堆叠压')+picture(journal_base/'key_phases/after_actual_unit_consumption/screen.png','当前：同原生状态，日志只保留顶部入口')+'</div>')
        for label,title in [('complete_journal_top','完整日志开头'),('complete_journal_end','实际点击阅读末行后的画面'),('rules_hover_help','规则入口实际悬浮'),('rules_action_fulltext','规则正文保留操作说明')]:
            page.append('<details><summary>'+title+'</summary>'+picture(journal_base/'key_phases'/label/'screen.png',title)+'</details>')
    batch8_before=OUT/'attempt29/before_assets'
    if (batch8_before/'manifest.json').exists() and all((CASE/(('monster_' if row['id'].startswith('monster_') else 'unit_')+row['id']+'_result.json')).exists() for row in read(OUT/'generation_batch8.json')):
        batch8_pairs=[]
        for row in read(OUT/'generation_batch8.json'):
            identity=row['id'];label=('monster_' if identity.startswith('monster_') else 'unit_')+identity
            a,b=read(batch8_before/'setups'/(label+'.json')),read(CASE/'setups'/(label+'.json'))
            assert a['fixture']==b['fixture'] and a['native_state']==b['native_state'],identity
            pair=dict(id=identity,before=rel(batch8_before/'key_phases'/label/'screen.png'),after=rel(CASE/'key_phases'/label/'screen.png'),same_native_state=True)
            batch8_pairs.append(pair)
        write(CURRENT/'batch8_pairing_audit.json',dict(passed=True,pairs=batch8_pairs,scope='Same archived fixture and actual native state; UI help/log cleanup also differs. Repeated unit builds are not independent scenes.'))
        page.append('<h2>新补8组素材 · 同原生状态前后</h2><p>巫妖、僵尸村民、人类战士、骷髅战士、护林人、守护者魔像、药草师、幻术师。四敌人去掉旧徽记；单位原先无立绘，不能称为去掉单位旧背景。护林人/守护者/药草师为重复构筑，分数单列去重。</p><p>'+link(OUT/'generation_batch8.json','内置image_gen完整prompt与源图')+' · '+link(OUT/'attempt29/import_audit.json','8张原始PNG导入SHA和透明安全边距')+' · '+link(CURRENT/'batch8_pairing_audit.json','8组同原生状态凭据')+'</p>')
        for pair in batch8_pairs:
            page.append('<details><summary>'+pair['id']+'</summary><div class="pair">'+picture(OUT/pair['before'],'替换前')+picture(OUT/pair['after'],'当前')+'</div></details>')
        for name,title in [('enemy_material','四敌人独立评分'),('unit_material','四单位独立评分（含去重）')]:
            path=OUT/'attempt29/independent_review'/(name+'.md')
            page.append('<p>'+link(path,title)+'</p>' if path.exists() else '<p>'+title+'：待完成。</p>')
    if expansion_pairs:
        page.append('<h2>新增4组独立素材 · 实际同状态前后</h2><p>骨龙、钢铁魔像去掉旧徽记背景；火焰魔像、火焰法师从无独立立绘改为显示。完整生成版本、过窄安全边距失败和重新生成均保留。透明替换不等于场景融合验收。</p>')
        page.append('<p>'+link(CURRENT/'expansion_pairing_audit.json','4组原生状态相等凭据')+' · '+link(OUT/'attempt27/import_audit.json','导入SHA与透明边距')+' · '+link(OUT/'generation_batch7_retry.json','重新生成的完整prompt与来源')+'</p>')
        for pair in expansion_pairs:
            page.append('<section><h2>'+pair['id']+'</h2><div class="pair">'+picture(OUT/pair['before'],'同布局替换前')+picture(OUT/pair['after'],'本版替换后')+'</div></section>')
        material_review=OUT/'attempt27/independent_review/material_review.md'
        page.append('<p>'+link(material_review,'attempt27素材替换审查（不外推后续布局）')+'</p>' if material_review.exists() else '<p>本批独立素材审查：待完成。</p>')
        page.append('<p>'+link(OUT/'attempt26/independent_review/review.md','上一版同排布局审查（不作为本版整体评分）')+'</p>')
    for path,title in [(CURRENT/'independent_review/review.md','当前界面独立审查'),(OUT/'attempt23/independent_review/material_review.md','历史attempt23素材评分（不外推当前）'),(OUT/'attempt19/independent_review/review.md','历史五项评分 attempt19'),(OUT/'attempt22/independent_review/focused_recheck.md','历史阅读区复核')]:
        page.append('<p>'+link(path,title)+'</p>' if path.exists() else '<p>'+title+'：待完成，未宣称通过。</p>')
    page.append('<h2>较早8组至今 · 同一原生状态</h2><p>素材-only对照另存于attempt23；这里包含后续姓名和排列修订。</p>')
    for pair in pairs:
        page.append('<details><summary>'+pair['id']+'</summary><div class="pair">'+picture(OUT/pair['before'],'替换前：attempt23 before_assets')+picture(OUT/pair['after'],'当前：'+CURRENT.name)+'</div></details>')
    for label,title in [('ranged','远程'),('block','格挡'),('assign_damage','承伤'),('melee_two_enemies','多敌近战'),('end','战后'),('won_result','胜利结算')]:
        page.append('<section><h2>'+title+'</h2><div class="pair">'+picture(OUT/'before/key_phases'/label/'screen.png','最初基线')+picture(VALIDATION/'key_phases'/label/'screen.png',CURRENT.name)+'</div></section>')
    page.append('<h2>实际操作与按需信息</h2>')
    for label,title in [('unit_unavailable_in_ranged','就绪但本阶段无能力'),('unit_selected_in_block','部队就绪与原能力'),('unit_consumed','格挡4、耗竭、禁用'),('wiki_block_risk','实际打开格挡规则'),('wiki_selected_unit_fulltext','所选佣兵全文'),('card_hover_after_unit_consumption','悬浮原卡，不透明阅读区'),('enemy_target_selected_after_unit_consumption','实际选中敌人')]:
        page.append('<details><summary>'+title+'</summary>'+picture(VALIDATION/'key_phases'/label/'screen.png',title)+'</details>')
    for kind,title in [('hero','英雄'),('monster','怪物'),('unit','佣兵')]:
        page.append('<h2>'+title+'逐项运行时证据</h2><p>各图标出真实版本。绑定通过不代表视觉和全部能力通过。</p>')
        for row in inventory[kind]:
            case=cases[kind,row['id']]
            page.append('<details><summary>'+html.escape(row['name'])+' · '+row['id']+'</summary>'+picture(OUT/case['screenshot'],case['evidence_version']+' · '+row['id'])+link(OUT/case['result'],'原始绑定/字形记录')+'</details>')
    four_base=CURRENT/('four_unit_validation_final' if (CURRENT/'four_unit_validation_final/manifest.json').exists() else 'four_unit_validation_retry')
    if (four_base/'manifest.json').exists():
        four=read(four_base/'manifest.json');assert four['passed']
        page.append('<h2>四佣兵同排 · 原生等级与指挥槽条件实例</h2><p>托瓦克名望48、等级7、四个实际普通指挥槽；原生战斗状态相等，角色尺寸、完整UV及21pt姓名保持。移除第四位悬在上方的布局，将四佣兵放到地面。实际点击四单位、第四位烈焰枪手产生格挡5并耗竭；三个敌人的实际点击路径分别核对。等级5带四单位与等级7带五单位仍由真实指挥槽规则拒绝。该实例通过条件构筑准备，不冒充历史轨迹覆盖、全部能力或真人结果。</p><p>'+link(four_base/'manifest.json','点击、消费、负例及原生状态凭据')+'</p>')
        page.append('<div class="pair">'+picture(CURRENT/'four_unit_before/key_phases/four_units_neutral/screen.png','条件实例旧布局：第四位悬在上方')+picture(four_base/'key_phases/four_units_neutral/screen.png','当前：四佣兵同排，保持尺寸')+'</div>')
        for label,title in [('four_units_three_enemies','四佣兵与三敌人'),('fourth_unit_selected','实际选择第四位'),('fourth_unit_block5_consumed','第四位格挡5并耗竭')]:
            page.append('<details><summary>'+title+'</summary>'+picture(four_base/'key_phases'/label/'screen.png',title)+'</details>')
        review_path=CURRENT/'independent_review/four_unit_layout.md'
        page.append('<p>'+link(review_path,'四佣兵截图独立比较审查（含能力行溢出缺陷）')+'</p>' if review_path.exists() else '<p>四佣兵独立视觉审查待完成。</p>')
        followup=CURRENT/'independent_review/four_unit_caption_recheck.md'
        if followup.exists():page.append('<p>'+link(followup,'能力行修复后独立复核')+'</p>')
    page.append('<h2>仍需推进</h2><p>透明角色目录已补齐；接地、光照、比例及可点击提示仍需改进，不能把绑定通过当作全目录美术验收。四单位仅验证上述有限条件；更多单位、复杂能力、全游戏和真人评估未完成；未证明总体点击负担下降。评分仅限实际审查图，重复构筑单列去重结果。</p>')
    page.append('<h2>保留版本与失败</h2><p>before与全部先前attempt均保留。attempt24的54px姓名框仍产生省略号，运行时几何已保存；当前60px框通过逐项全文门禁。历史素材失败、审查退化均未删除。</p><p>'+link(OUT/'attempt22/comparison_report.html','上一轮发布报告')+' · '+link(OUT/'attempt23/transparent_compression.json','NTFS透明压缩与SHA核验')+'</p>')
    page[0]=page[0].replace('attempt25',CURRENT.name)
    text='\n'.join(page);(OUT/'index.html').write_text(text,encoding='utf8')
    (CURRENT/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
    audit=read(OUT/'current_audit.json')
    if (OUT/'attempt27/independent_review/material_review.json').exists():
        audit['latest_material_review']='attempt27/independent_review/material_review.json'
    if (CURRENT/'ground_layout_geometry.json').exists():
        audit['ground_layout_geometry']=rel(CURRENT/'ground_layout_geometry.json')
    material_base=CURRENT if (CURRENT/'independent_review').is_dir() else OUT/'attempt30'
    audit['material_reviews']=[rel(p) for p in [material_base/'independent_review/enemy_material.json',material_base/'independent_review/enemy_a.json',material_base/'independent_review/enemy_b.json',material_base/'independent_review/unit_material.json'] if p.exists()]
    audit['latest_material_review']=audit['material_reviews']
    audit['asset_case_compile_receipt']=rel(CURRENT/'compile_receipt.json')
    if (four_base/'manifest.json').exists():audit['four_unit_validation']=rel(four_base/'manifest.json')
    if (CURRENT/'independent_review/four_unit_layout.json').exists():audit['four_unit_layout_review']=rel(CURRENT/'independent_review/four_unit_layout.json')
    if (CURRENT/'reading_validation/manifest.json').exists():audit['reading_validation']=rel(CURRENT/'reading_validation/manifest.json')
    if (journal_base/'manifest.json').exists():
        audit['journal_validation']=rel(journal_base/'manifest.json')
    write(OUT/'current_audit.json',audit)
    print('Published '+CURRENT.name+':',counts,'with versioned actual evidence; no browser visual QA claim.')


if __name__=='__main__':main()
