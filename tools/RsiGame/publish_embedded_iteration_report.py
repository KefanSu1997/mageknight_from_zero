"""Build a local evidence index from actual saved captures; no browser QA claim."""
import hashlib
import argparse
import html
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf8')


def picture(path, caption):
    assert path.is_file(), path
    return '<figure><a href="' + path.relative_to(OUT).as_posix() + '"><img loading="lazy" src="' + path.relative_to(OUT).as_posix() + '"></a><figcaption>' + html.escape(caption) + '</figcaption></figure>'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--attempt', default='attempt21')
    args = parser.parse_args()
    version = args.attempt
    attempt = OUT / version
    assert read(attempt / 'validation/party_feedback_manifest.json')['passed']
    old = OUT / 'attempt11/published_report_before_attempt18.html'
    if not old.exists():
        shutil.copy2(OUT / 'index.html', old)
        shutil.copy2(OUT / 'asset_coverage.json', old.with_name('published_asset_coverage.json'))
        shutil.copy2(OUT / 'current_audit.json', old.with_name('published_audit.json'))
    catalog = read(ROOT / 'resources/text_json/monster.json')
    coverage = []
    for item in catalog:
        identity = item['id']
        paths = [ROOT / 'Assets/Resources/UI/EnemyCharacters' / v / (identity + '.png') for v in ['embedded_v2', 'embedded_v1']]
        asset = next((p for p in paths if p.is_file()), None)
        if asset:
            coverage.append(dict(id=identity, name=item.get('monster_name', item.get('card_name_cn', identity)), asset=asset.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(asset.read_bytes()).hexdigest()))
    cases = []
    for folder in ['all_current_asset_cases', 'remaining_asset_cases_retry']:
        base = OUT / 'attempt16' / folder
        for result in sorted(base.glob('*_result.json')):
            record = read(result)
            assert record['binding_verified']
            screen = base / 'key_phases' / (record['kind'] + '_' + record['identity']) / 'screen.png'
            assert screen.is_file()
            cases.append(dict(**record, screenshot=screen.relative_to(OUT).as_posix()))
    assert len({x['identity'] for x in cases if x['kind'] == 'monster'}) == len(coverage) == 27
    assert len({x['identity'] for x in cases if x['kind'] == 'hero'}) == 7
    unit_assets = sorted((ROOT / 'Assets/Resources/UI/UnitCharacters/embedded_v1').glob('*.png'))
    unit_manifest = OUT / 'attempt19/new_unit_cases/manifest.json'
    if len(unit_assets) > 2:
        assert unit_manifest.exists() and read(unit_manifest)['passed'], 'New unit binding validation is required before publication'
    write(OUT / 'asset_coverage.json', dict(monsters=dict(converted=coverage, count=27, total=69, remaining=42),
        heroes=dict(count=7, total=7), units=dict(ids=[p.stem for p in unit_assets], count=len(unit_assets), total=30, remaining=30-len(unit_assets)),
        runtime_cases=cases, scope='Asset bindings and native archived-state equality; visual acceptance separate. Quarantined generations excluded.'))
    review = attempt / 'independent_review/review.json'
    audit = dict(latest_verified=version, completed=False,
        source_runtime=read(attempt / 'compile_receipt.json'),
        validation=read(attempt / 'validation/party_feedback_manifest.json'),
        independent_review=read(review) if review.exists() else 'pending; latest complete review: attempt19/independent_review/review.json',
        remaining=['42 monster sprites', str(30-len(unit_assets))+' unit sprites', 'Visual and interaction issues in independent review', 'Complex special-unit and multi-target acceptance'],
        canvas=[1920, 1080], note='No full-game or human acceptance; no global cost improvement claim.')
    patch_folder = OUT / 'attempt22'
    patch_manifest = patch_folder / 'validation/manifest.json'
    if patch_manifest.exists():
        audit.update(latest_verified='attempt22', full_validation_version=version,
            reading_patch_validation=read(patch_manifest), reading_patch_runtime=read(patch_folder / 'compile_receipt.json'))
        focused = patch_folder / 'independent_review/focused_recheck.json'
        if focused.exists():
            audit['reading_patch_independent_recheck'] = read(focused)
    write(OUT / 'current_audit.json', audit)
    parts = ['''<!doctype html><meta charset="utf-8"><title>战斗UI · attempt18</title><style>
body{background:#101b20;color:#e4e7df;font:18px system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}section{margin:42px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}figcaption{margin:8px 0 20px;color:#b9c7c5}h1,h2{color:#dcc492}p{line-height:1.7}details{margin:20px 0}table{border-collapse:collapse}td,th{padding:12px;border:1px solid #53615c}</style>
<h1>战斗 UI · 透明角色与信息层级</h1>
<p>当前候选 attempt18。英雄 7/7、怪物 27/69、佣兵 2/30 已接入；仍缺 42 种怪物、28 种佣兵，目标未完成。所有实机截图保持 1920×1080。角色按可见轮廓统一缩放与落脚位置，绘制完整纹理，不裁边、不拉伸、不缩小手牌。</p>
<p>七英雄和已接入的27种怪物均保存逐项实机截图，并核对实际纹理绑定及原生历史状态。attempt16 首次采集在一个缺少当前构筑合同的旧样本处失败；保留失败，再从完整真实轨迹重试完成，未捏造构筑。</p>
<p>本版实测：原有十类关键阶段及魔力强化链；佣兵原能力产生格挡4并耗竭；禁用按钮保持能力可读；格挡风险/所选佣兵全文实际打开、返回原生状态不变；手牌悬浮放大。此处只是脚本验证，不等于玩家体验、全能力或全游戏验收。</p>
<p><a href="asset_coverage.json">资源SHA与34项绑定证据</a> · <a href="current_audit.json">当前审计</a> · <a href="attempt18/validation/party_feedback_manifest.json">本版操作与截图</a> · <a href="attempt18/validation/source_intent.json">魔力强化路径</a> · <a href="attempt16/all_current_asset_cases/failure.json">保留的旧样本失败</a></p>''']
    parts.append('<h2>当前三敌战斗</h2>' + picture(attempt / 'validation/key_phases/three_enemies_neutral/screen.png', '真实三敌；英雄与佣兵独立立绘。该战斗为有出处的验证构筑，不是默认开局。'))
    parts.append('<p>attempt21：主操作改为深底浅字与金色标记；悬浮原卡在右侧临时阅读区完整显示，移出后恢复操作。保持原卡预览300×438，不缩字、不遮挡己方角色和敌人。</p>')
    parts.append('<h2>独立审查</h2><p>评分为独立静态审查，不与原30轮或旧rubrics分数混算；基线与候选局部状态差异不作因果对照。</p>')
    parts.append('<p>attempt19 独立评分（基线→候选）：操作重点3→4、信息可读完整3→4、构图美感2→4、角色融合1→3、相对比例2→4。该版本仍被要求修复浅金按钮对比与卡牌预览遮挡；分数不追认给后续版本。</p>')
    for target in [attempt / 'independent_review/review.md', OUT / 'attempt19/independent_review/review.md', OUT / 'attempt18/independent_review/hero7_review.md']:
        filename = target.name
        if target.exists():
            parts.append('<p><a href="' + target.relative_to(OUT).as_posix() + '">' + target.relative_to(OUT).as_posix() + '</a></p>')
        else:
            parts.append('<p>当前修订未重打完整五项评分；保留并展示上一版完整审查。</p>')
    recheck = attempt / 'independent_review/focused_recheck.md'
    if recheck.exists():
        parts.append('<p><a href="'+recheck.relative_to(OUT).as_posix()+'">当前修订的定向复核（不重打总分）</a></p>')
    for label, title in [('ranged','远程'),('block','格挡'),('assign_damage','承伤'),('melee_two_enemies','多敌近战'),('end','战后'),('won_result','胜利结算')]:
        parts.append('<section><h2>' + title + '</h2><div class="pair">' + picture(OUT / 'before/key_phases' / label / 'screen.png','优化前基线') + picture(attempt / 'validation/key_phases' / label / 'screen.png','attempt18') + '</div></section>')
    parts.append('<h2>佣兵状态与按需信息</h2>')
    for label, title in [('unit_unavailable_in_ranged','就绪但当前阶段无适用能力'),('unit_selected_in_block','激活能力并生成资源，再选择一次攻击结算'),('unit_consumed','耗竭后禁用；格挡资源4'),('wiki_block_risk','实际打开格挡与伤害规则'),('wiki_selected_unit_fulltext','实际打开所选佣兵全文'),('card_hover_after_unit_consumption','实际悬浮手牌放大，非点击触发'),('enemy_target_selected_after_unit_consumption','实际点击选中敌方目标')]:
        if not (attempt / 'validation/key_phases' / label / 'screen.png').exists():
            continue
        parts.append('<details><summary>' + title + '</summary>' + picture(attempt / 'validation/key_phases' / label / 'screen.png', title) + '</details>')
    for kind, heading in [('hero','七位英雄实机图'),('monster','27种怪物实机图')]:
        parts.append('<h2>' + heading + '</h2><p>attempt16，使用与本版相同的角色素材及轮廓缩放。当前布局提示改动不追认到这些旧截图。</p>')
        for case in cases:
            if case['kind'] == kind:
                parts.append('<details><summary>' + case['identity'] + '</summary>' + picture(OUT / case['screenshot'], '真实归档状态；绑定和状态对照通过，视觉评价另列') + '</details>')
    parts.append('<h2>保留版本与局限</h2><p>before、attempt01–18及失败证据保留。attempt17新增提示与攻击/资源文字重叠，已保留并在attempt18移动提示，字号未变。新一批生成中有边缘过紧或身份待核的素材，隔离保存，不计入已替换数量。当前仍需改善光照、接地、局部文字对比及复杂交互；未证明总体点击负担下降。</p><p><a href="attempt11/published_report_before_attempt18.html">此前发布报告（attempt11）</a> · <a href="generation_batch5_quarantined.json">本批未采用素材与完整生成prompt</a></p>')
    parts.append('<p>未替换敌人仍使用旧标记图。七英雄审查中的托瓦克场景、以及新增佣兵场景里的叛乱村民 monster_061 仍出现灰色矩形旧图，这是未完成项，不算融合通过。七英雄轮廓完整不等于整张场景全部替换；佣兵长名称摘要、三单位排列和光照接地仍需改进。</p>')
    if unit_manifest.exists():
        parts.append('<h2>新增佣兵实机图（attempt19）</h2><p>阿尔蒂姆守护者、阿尔蒂姆法师为新诠释立绘。逐一核对实际绑定及原生历史状态，不等于全部能力消费验收；长名称可悬浮查看全文，当前摘要仍有省略。<a href="generation_batch5_recomposed.json">完整生成prompt与来源</a></p>')
        for row in read(unit_manifest)['captures']:
            parts.append(picture(Path(row['screenshot']), row['label']))
    if patch_manifest.exists():
        assert read(patch_manifest)['passed']
        parts.insert(1, '<p>当前运行 attempt22：仅将 attempt21 的悬浮阅读区从半透明改为不透明。完整交互组仍是 attempt21 的证据；attempt22另通过实际悬浮/移出、原生状态不变和截图边栏像素均一性检查，未冒充重新跑完整组。</p>')
        parts.append('<h2>attempt22 · 不透明阅读区</h2>' + picture(patch_folder / 'validation/key_phases/opaque_card_reading/screen.png','实际悬浮：不再透出旧说明；人物、敌人和主按钮保留')
            + picture(patch_folder / 'validation/key_phases/after_unhover/screen.png','实际移出：恢复当前单位上下文，原生状态不变'))
        patch_review = patch_folder / 'independent_review/focused_recheck.md'
        if patch_review.exists():
            parts.append('<p><a href="'+patch_review.relative_to(OUT).as_posix()+'">不透明阅读区定向复核</a></p>')
    page = '\n'.join(parts).replace('<title>战斗UI · attempt18</title>', '<title>战斗UI · '+version+'</title>')
    page = page.replace('当前候选 attempt18', '当前候选 '+version).replace('<figcaption>attempt18</figcaption>', '<figcaption>'+version+'</figcaption>')
    page = page.replace('佣兵 2/30', '佣兵 '+str(len(unit_assets))+'/30').replace('28 种佣兵', str(30-len(unit_assets))+' 种佣兵')
    page = page.replace('before、attempt01–18及失败证据保留', 'before、attempt01–'+version.removeprefix('attempt')+'及失败证据保留')
    if patch_manifest.exists():
        page = page.replace('<title>战斗UI · '+version+'</title>', '<title>战斗UI · attempt22</title>')
        page = page.replace('当前候选 '+version, '主体设计 '+version+'（当前运行的阅读区修订为 attempt22）')
        page = page.replace('before、attempt01–'+version.removeprefix('attempt')+'及失败证据保留', 'before、attempt01–22及失败证据保留')
    (OUT / 'index.html').write_text(page, encoding='utf8')
    report_folder = patch_folder if patch_manifest.exists() else attempt
    (report_folder / 'comparison_report.html').write_text(page.replace('href="','href="../').replace('src="','src="../'), encoding='utf8')
    print('Report saved; 27 monster + 7 hero runtime cases indexed. Browser visual QA not claimed.')


if __name__ == '__main__':
    main()
