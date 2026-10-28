"""Versioned resource HUD evidence; rejected candidates never overwrite reviews."""
import shutil,json
from publish_battlefield_readability import OUT,ROOT,read,link,image,esc
def main():
    c=OUT/'attempt52'
    assert read(c/'resource_validation_contrast/manifest.json')['passed']
    assert read(c/'validation_contrast/party_feedback_manifest.json')['passed']
    assert read(c/'geometry_contrast_audit.json')['passed']
    assert (c/'scene_review_contrast/review.md').is_file()
    body=['<!doctype html><meta charset="utf-8"><title>52 魔力符印与晶体资源栏</title>',
    '<style>body{background:#172521;color:#eee;font:17px/1.65 system-ui;max-width:1720px;margin:24px auto;padding:20px}a{color:#edca87}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}.triple{display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px}figure{margin:4px 0 24px}img{width:100%}pre{white-space:pre-wrap;background:#21332c;padding:20px}.notice{background:#4e3a29;padding:18px}</style>',
    '<h1>52：魔力符印 / 库存晶体 / 公共源骰分开表达</h1>',
    '<p class="notice">顶部六色椭圆和斜杠比值改为两行资源：上行为当前魔力符印，下行为可储存晶体。数量26pt，分类与具体悬浮解释22pt。首个可运行方案被独立审查拒绝：亮天空上白字不清、—含义不明；原图与拒绝评分完整保留，当前版增加一层共享HUD衬底、数字粗体及明确“不可存”。整体UI目标仍未完成。</p>',
    '<p>六个符印有不同轮廓，名称、数量及限制可以悬浮查看。金黑两色原规则禁止储存晶体，显示“不可存”；若观察到非零库存，代码仍显示实际数值，不隐藏数据。公共魔力入口采用骰子标识，实际取骰/付款按钮与回调保持。没有把MageKnight的独立魔力规则替换为STS2能量系统。</p>',
    '<p>参考来自既有实机记录 O1“紧凑顶部资源”和 O4“按需详情”，本轮没有新跑参考游戏：<a href="../../workflow_revisions/20261023_combat_focus_v3/reference_sts2/index.html">STS2参考与证据范围</a></p>',
    '<h2>当前修正的独立复审</h2><p>'+link(c/'scene_review_contrast/protocol.md','协议')+' · '+link(c/'scene_review_contrast/inputs.json','15张原图与SHA')+' · '+link(c/'scene_review_contrast/review.json','全部逐项评分')+'</p>',
    '<pre>'+esc((c/'scene_review_contrast/review.md').read_text(encoding='utf8'))+'</pre>',
    '<details><summary>保留：裸底版本19图独立审查与拒绝</summary><p>'+link(c/'scene_review/review.json','原始拒绝JSON')+' · '+link(c/'scene_review/inputs.json','原始19图输入')+'</p><pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf8'))+'</pre></details>',
    '<h2>原椭圆 / 未采纳裸底 / 当前修正</h2>']
    for label in ['three_enemies','source_taken','selected_action_payment','source_consumed']:
        body+=['<h3>'+esc(label)+'</h3><div class="triple">'+image(c/'before/key_phases'/label/'screen.png','原椭圆：优化前')+image(c/'validation_quantity/key_phases'/label/'screen.png','裸底：被拒绝的候选')+image(c/'validation_contrast/key_phases'/label/'screen.png','共享HUD衬底：当前')+'</div>']
    body+=['<h2>六色悬浮说明</h2><p>以下6色均通过实际hover_info进入并退出；22pt完整字形生成，原生状态和选中对象不变。悬浮存在不等于真人首次发现率或已学会识别符号。</p>']
    for color in ['Red','Blue','Green','White','Gold','Black']:
        body.append(image(c/'resource_validation_contrast/readers/key_phases'/(color+'_hover')/'screen.png',color+' 当前数量/储存规则'))
    body+=['<h2>真实戒指消费与无尽魔力</h2><p>从SHA核实的历史episode5、step22实际状态重放正式前缀；先核对旧原生快照，再由真实UI打出红宝石戒指。普通效果：红晶3保持3，原本的1魔力与晶体溢出合计红魔力+2、名望+1。焚毁效果：正式产生本回合无尽红/黑魔力，临时标记和晶体库存不被伪改；界面显示∞，悬浮明确回合结束失效。</p>']
    for mode,label in [('normal','mixed_inventory_before'),('normal','ring_consumed'),('burn','ring_consumed'),('burn','infinite_Red_hover')]:
        body.append(image(c/'resource_validation_contrast'/mode/'key_phases'/label/'screen.png',mode+' · '+label))
    body+=['<p>'+link(c/'ring_archive_overflow_fixture.json','原轨迹SHA、正式前缀与预期状态')+' · '+link(c/'resource_validation_contrast/manifest.json','数量读取与两条正式消费检查')+'</p>',
    '<h2>重要阶段和典型界面：优化前 / 当前</h2><p>当前25阶段及消费后状态完整留档。优化前17个基础阶段在本轮重拍；额外佣兵/知识场景使用51版同名实际截图。只有几何审计登记的5组是严格同原生配对，不将所有阶段截图当成逐像素因果证明。</p>']
    for p in sorted((c/'validation_contrast/key_phases').glob('*/screen.png')):
        old=c/'before/key_phases'/p.parent.name/'screen.png'
        if not old.exists():old=OUT/'attempt51/validation/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(old,'优化前同名实际场景') if old.exists() else '<p>没有同名旧图，不补造</p>')+image(p,'当前实际场景')+'</div>']
    body+=['<h2>可复现证据和边界</h2><p>当前MVID '+esc(read(c/'compile_contrast_receipt.json')['runtimeModuleVersionId'])+'。5组同原生对照中，原有正式操作按钮的几何、可用性与回调入口保持，人物/卡牌PNG、UV、尺寸和色调保持；非资源文字全文/字号/位置保持。新增资源信息命中面与旧圆珠不同，不能声称所有悬浮区域不变。</p><ul>']
    for p in ['review_contract.json','symbol_registry.json','geometry_contrast_audit.json','source_contrast_manifest.json','compile_contrast_receipt.json','validation_contrast/manifest.json','validation_contrast/party_feedback_manifest.json','validation_contrast/source_intent.json','resource_validation_contrast/manifest.json','renderer_repair.json','quantity_box_failure.json','ring_seed_search_failure.json','contrast_repair.json','delayed_startup_receipt.json']:
        body.append('<li>'+link(c/p,p)+'</li>')
    body+=['</ul><p>失败保留：最初新Graphic缺CanvasRenderer；随后26pt数字放入34px容器导致零字形；修正时扩大容器而非缩字。32个开局种子未抽到追加戒指，原观察全部留档，改用真实归档状态正式重放；没有直接注入卡牌/魔力。初次启动观察超时后读取原请求成功响应，没有重启或重复执行消费。</p>',
    '<h2>还需继续</h2><p>资源符号首次识别/悬浮发现性、公共魔力面板的文字长条、技能绿色纸图与泛用图标混用仍需改进。英雄/佣兵/敌人面向、英雄层级、状态符号、整体环境光与接触感仍未完成全面验收。局部UI检查和截图评分不证明真人表现、全规则覆盖或总体目标完成。</p>',
    '<p>'+link(OUT/'attempt51/comparison_report.html','51版技能区和历史全部证据')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf8')
    print('52 resource report published; whole goal incomplete')
if __name__=='__main__':main()
