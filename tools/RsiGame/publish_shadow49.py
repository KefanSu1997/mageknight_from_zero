"""Archive a rejected environment-integration candidate and verified rollback."""
import shutil
from publish_battlefield_readability import OUT,link,image,read,esc

def main():
    c=OUT/'attempt49'
    body=['<!doctype html><meta charset="utf-8"><title>49投影实验：未采纳，已回退</title>',
    '<style>body{background:#14201f;color:#eee;font:17px/1.65 system-ui;margin:30px auto;max-width:1700px;padding:20px}a{color:#edc67e}.triple{display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}img{width:100%}figure{margin:0}pre{white-space:pre-wrap}.notice{background:#49392c;padding:15px}</style>',
    '<h1>49：地面投影候选未采纳，已恢复48视觉方案</h1>',
    '<p class="notice">没有确认融合评分提升，独立审查建议回退。两份产品视觉源码已逐字节恢复，新增的投影参数诊断字段保留用于回查。当前游戏视觉为48方案，49截图是失败候选，不是新采用设计。整体目标仍未完成。</p>',
    '<p>实验尝试将投影脚部锚在人物脚底，轮廓向庭院前方右侧铺开并逐渐减弱；人物原PNG、完整UV、尺寸、文字和点击区域保持。七组实际场景中，审查仅看到轻微暗化，仍有明显贴片感和整齐排列感，不能证明这条方法带来足够收益。</p>',
    '<p>'+link(c/'candidate_decision.json','拒绝采纳及源码回退决定')+' · '+link(c/'geometry_audit.json','候选七组同状态几何与实际投影参数')+' · '+link(c/'rollback_audit.json','回退后七组实机参数与图像恢复')+'</p>',
    '<h2>完整独立评分</h2><p>'+link(c/'scene_review/protocol.md','协议')+' · '+link(c/'scene_review/inputs.json','14张图与SHA')+' · '+link(c/'scene_review/review.json','逐项评分JSON')+'</p>',
    '<pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf-8'))+'</pre>',
    '<p>归因限制：独立审查在一组静态图中看到青环→金环，焦点分4→3。青环为限时输入反馈，金环为持续目标标记，本次未修改两者逻辑。采样时刻混杂未消除；保留该负面截图评分，但不把它当作投影导致退化的因果证明。拒绝采用的充分依据仍是未确认环境融合收益。</p>',
    '<h2>优化前 / 未采纳候选 / 实际回退</h2>']
    for row in read(c/'after/manifest.json')['captures']:
        label=row['label'];body+=['<h3>'+esc(label)+'</h3><div class="triple">'+image(c/'before/key_phases'/label/'screen.png','48：实验前')+image(c/'after/key_phases'/label/'screen.png','49：未采纳')+image(c/'rollback/key_phases'/label/'screen.png','49回退后：实际恢复48方案')+'</div>']
    body+=['<h2>重要阶段与典型界面：候选实验留档</h2><p>下列候选测试已通过正式消费检查；功能可用不能代替视觉收益，因此仍拒绝本候选。</p>']
    for p in sorted((c/'validation/key_phases').glob('*/screen.png')):
        before=OUT/'attempt48/validation/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(before,'48同一场景') if before.exists() else '<p>无同名旧图</p>')+image(p,'49未采纳候选')+'</div>']
    body+=['<p>'+link(c/'validation/party_feedback_manifest.json','候选25典型阶段与真实普通格挡消费')+' · '+link(c/'compile_receipt.json','候选编译MVID')+' · '+link(c/'rollback_compile_receipt.json','回退编译MVID')+' · '+link(OUT/'normal_resume_after_attempt49/status.json','恢复普通试玩状态')+'</p>',
    '<h2>后续方向</h2><p>继续叠加细微阴影不能解决主要观感。下一版应重做技能选择操作区：突出技能对象、可用状态与消费选择，长规则通过明确的悬浮入口阅读，并保留完整费用/次数与返回路径。角色光照、尺度和场景构图也仍需整体处理。本文不以局部实验或回退声称目标完成。</p>',
    '<p>'+link(OUT/'attempt48/comparison_report.html','当前采用48方案的完整对比')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf-8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf-8')
    print('Rejected49 and rollback report published')

if __name__=='__main__':main()
