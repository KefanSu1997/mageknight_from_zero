"""Publish immutable material comparison including independent rejection findings."""
import json,shutil
from publish_battlefield_readability import OUT,link,image,esc,read

def main():
    c=OUT/'attempt47'; review=read(c/'scene_review/review.json')
    body=['<!doctype html><meta charset="utf-8"><title>角色素材候选47</title>',
        '<style>body{background:#14201f;color:#eee;font:17px/1.65 system-ui;margin:30px auto;max-width:1600px;padding:20px}a{color:#edc67e}.pair{display:grid;grid-template-columns:1fr 1fr;gap:15px}img{width:100%}figure{margin:0}pre{white-space:pre-wrap}table{border-collapse:collapse}td,th{border:1px solid #68746c;padding:8px}</style>',
        '<h1>46 → 47：金龙落地姿态、九头蛇完整身份</h1>',
        '<p>仅局部改善，整体未通过。独立审查确认金龙落地姿态更清楚、九头蛇九个头可辨；三个选中态的青色反馈环穿过躯干仍是拒绝项。没有以新素材通过来代替整体游戏界面验收。</p>',
        '<p>五份生成候选与原提示词全部保留：八头、1像素留边等失败未覆盖。导入的两张PNG与生成文件逐字节一致，完整UV；旧版素材和旧版布局元数据保持，便于回退。浅肤金发弓手与原印刷图相符，没有只因名称改成绿皮。</p>',
        '<p>'+link(c/'generation_manifest.json','全部生成候选及拒绝理由')+' · '+link(c/'candidate_import.json','两份实际导入与SHA')+' · '+link(c/'original_reference_audit.json','原卡参考依据')+' · '+link(c/'reference_review/review.json','原图/素材独立审查')+'</p>',
        '<h2>独立截图评分</h2><p>1严重失败，2明显问题，3基本可用但问题可见，4良好，5优秀。五维专项，不替代完整32项rubrics或真人测试。完整意见包含仍存在的拼贴感、拥挤和细文可读性限制。</p>',
        '<p>'+link(c/'scene_review/protocol.md','审查协议')+' · '+link(c/'scene_review/inputs.json','16张输入图与SHA')+' · '+link(c/'scene_review/review.json','完整逐项分数')+'</p>',
        '<table><tr><th>场景</th><th>完整/身份</th><th>环境融合</th><th>必要信息</th><th>操作焦点</th><th>美感</th></tr>']
    for g in review['groups']:
        body+=['<tr><td>'+esc(g['group'])+'</td>'+''.join('<td>'+str(g['before'][m])+' → '+str(g['after'][m])+'</td>' for m in review['metrics'])+'</tr>']
    body+=['</table><pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf-8'))+'</pre>',
        '<h2>七组同状态实际截图</h2>']
    for g in read(c/'after/manifest.json')['captures']:
        label=g['label'];body+=['<h3>'+esc(label)+'</h3><div class="pair">'+image(c/'before/key_phases'/label/'screen.png','46')+image(c/'after/key_phases'/label/'screen.png','47实际候选')+'</div>']
    body+=['<h2>按需信息入口</h2><p>部队操作带增加“悬浮角色查看能力与费用”提示，22号字完整生成。独立发现性2→3；这不证明全部部队全文可达。此前46的真实全文滚动消费者证据保留，没有重贴47评分。</p>',image(c/'entry_hint/key_phases/roster_open/screen.png','47悬浮入口提示'),
        '<h2>规则与退化检查</h2><p>'+link(c/'geometry_audit.json','七组同状态、点击区域、文字与图像核对')+' · '+link(c/'validation/party_feedback_manifest.json','25阶段/界面与普通格挡消费')+' · '+link(c/'four_unit_validation/manifest.json','四佣兵与实际第四单位能力消费')+' · '+link(c/'card_validation/manifest.json','14卡牌模式/全文界面与蓝魔力消费')+' · '+link(c/'source_manifest.json','源码SHA')+'</p>',
        '<p>1920×1080未变，匹配场景的正式点击区域与可用性未变；声明替换的角色造型宽高不同，不宣称人物尺寸完全相同。未变化角色保持图像尺寸/UV。新材质仅七个场景专项验证，未冒充106素材全部重审。局部装饰投影不是物理光照。</p>',
        '<h2>后续必须修复</h2><p>青色输入反馈仍沿用旧脚底高度，穿过腹部；与金色选中环分离。必须对齐实际角色脚底，并核对敌人/英雄/佣兵反馈与选择可辨性。整体光照、环境接触、技能专属素材、拥挤上下文与完整rubrics仍未完成。</p>',
        '<p>'+link(OUT/'attempt46/comparison_report.html','46完整归档')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf-8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf-8')
    print('47 comparison published, overall rejection retained')

if __name__=='__main__': main()
