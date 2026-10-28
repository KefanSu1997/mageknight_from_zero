"""Publish exact entity-text reader comparisons and native phase evidence."""
import shutil
from publish_embedded_hierarchy_report import OUT, read, write, relative, link, picture

CURRENT=OUT/'attempt36'


def native(r):return {v['key']:v['value'] for v in r['observation']['state']}['audit.costNativeState']


def main():
    reader=CURRENT/'entity_reader_validation_measured'
    for p in [reader/'manifest.json',CURRENT/'validation/party_feedback_manifest.json',CURRENT/'four_unit_validation/manifest.json',CURRENT/'ground_layout_geometry.json']:
        assert read(p)['passed'],p
    pairs=[]
    for actor in ['hero','unit','enemy']:
        before=OUT/'attempt35/compact_validation_selected_fix/key_phases'/(actor+'_focus_hover')
        after=reader/'key_phases'/(actor+'_top')
        a,b=read(before/'capture.json'),read(after/'capture.json');assert native(a)==native(b),actor
        pairs.append(dict(actor=actor,before=relative(before/'screen.png'),after=relative(after/'screen.png'),same_native_state=True))
    write(CURRENT/'hover_pairing_audit.json',dict(passed=True,pairs=pairs,scope='Three actual archived native contexts; old text preserved as prefix, native printed active abilities appended for units, mask-aware reading and unchanged selection separately verified.'))
    page=['<!doctype html><meta charset="utf-8"><title>实体详情与战斗分层 · attempt36</title><style>body{background:#101b20;color:#e4e7df;font:18px/1.7 system-ui;max-width:1500px;margin:40px auto;padding:0 24px}a{color:#e9c980}img{width:100%;height:auto}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:12px 0}figcaption{color:#bac6c4}h1,h2{color:#dcc492}details{margin:24px 0}</style><h1>实体详情与战斗分层 · attempt36</h1>',
        '<p>英雄、佣兵和敌人的悬浮正文移入右侧已有阅读区，避免左侧友军头部被长框盖住。明确显示“悬浮查看”与“已选对象”；查看期间暂时覆盖原动作详情，移开恢复同一选择。阶段推进按钮仍在下方，查看层不拦截指针。</p>',
        '<p>保留原21pt正文、原悬浮说明与角色尺寸，佣兵另附实际加载的原主动能力及印刷费用。长内容在角色上滚轮阅读，有阅读进度，支持到末行再回顶部。既有Wiki仍提供主动查询，短阶段帮助沿用上一版顶部位置。没有缩字、删掉原规则或通过重设任务拼接阅读返回。</p>',
        '<p>'+link(reader/'manifest.json','三种实体完整正文、逐字形可见、真实滚轮与返回')+' · '+link(CURRENT/'hover_pairing_audit.json','三组同原生状态截图对照')+' · '+link(CURRENT/'ground_layout_geometry.json','阶段文字与图片参数核对')+' · '+link(CURRENT/'validation/party_feedback_manifest.json','正常阶段与单位实际消费')+' · '+link(CURRENT/'four_unit_validation/manifest.json','第四单位及真实容量负例')+' · '+link(CURRENT/'compile_receipt.json','实际运行程序集/Console')+'</p>']
    review=CURRENT/'independent_review/review.md'
    page.append('<p>'+link(review,'本轮独立Sol审查、评分与不足')+'</p>' if review.exists() else '<p>本轮独立审查待完成。</p>')
    page.append('<p>独立审查实际看8图，三个配对五维均分3.2667→3.7333/5，每版15项；额外末行和回顶图不入均值。人物遮挡和悬浮/已选区分改善，但同时比较已选动作的便利性下降。内容、位置和阅读方式同时改变，不作单因素因果或真人结论。</p>')
    page.append('<p>'+link(CURRENT/'unchanged_controls_and_figures.json','25组正常状态的控件及完整图像参数保全')+' · '+link(CURRENT/'printed_ability_source.json','勇者三项原主动能力与印刷费用源记录')+' · '+link(CURRENT/'reader_failure_recovery.json','字形边缘、长正文高度及编译错误修正记录')+'</p>')
    page.append('<p>实际视口字形消费：英雄41/41、勇者190/190、黄金龙64/64；勇者向下、向上各5次滚轮事件覆盖全文并回顶。退出后原选守卫实际产生格挡4并耗竭。脚本EventSystem事件不冒充物理鼠标或真人阅读能力。</p>')
    for pair in pairs:
        page.append('<h2>'+pair['actor']+'</h2><div class="pair">'+picture(OUT/pair['before'],'attempt35 · 长框遮挡友军')+picture(OUT/pair['after'],'attempt36 · 右侧悬浮查看与已选对象区分')+'</div>')
        for suffix,title in [('end','正文末行'),('top_returned','向上滚回第一行'),('returned','移开后原选择与动作恢复')]:
            page.append('<details><summary>'+pair['actor']+' · '+title+'</summary>'+picture(reader/'key_phases'/(pair['actor']+'_'+suffix)/'screen.png',title)+'</details>')
    page.append('<h2>重要阶段及典型状态前后留档</h2>')
    for p in sorted((CURRENT/'validation/key_phases').glob('*/capture.json')):
        old=OUT/'attempt35/validation/key_phases'/p.parent.name/'capture.json';assert old.is_file() and native(read(old))==native(read(p)),p
        page.append('<details><summary>'+p.parent.name+'</summary><div class="pair">'+picture(old.parent/'screen.png','attempt35')+picture(p.parent/'screen.png','attempt36')+'</div></details>')
    page.append('<h2>限制与后续工作</h2><p>当前验证仅3种实际实体上下文，不代表全部106种正文或所有构筑。Mask字形核对是正文逐段进入视口的证据，不是像素级对比或真人可读性测试。右侧查看会临时覆盖已选动作信息，需评估上下文切换；阶段按钮可达不等于这种设计已被真人接受。角色光照、接地、素材身份质量、敌人旧环形标记语义与操作重点仍未完成。</p><p>'+link(OUT/'attempt35/comparison_report.html','上一版短说明与角色焦点档案')+' · '+link(OUT/'attempt33/comparison_report.html','106种素材来源与绑定档案')+'</p>')
    text='\n'.join(page)
    if not (CURRENT/'previous_index.html').exists():shutil.copyfile(OUT/'index.html',CURRENT/'previous_index.html')
    (OUT/'index.html').write_text(text,encoding='utf8')
    (CURRENT/'comparison_report.html').write_text(text.replace('href="','href="../').replace('src="','src="../'),encoding='utf8')
    print('Published candidate36; goal incomplete, evidence scope retained')


if __name__=='__main__':main()
