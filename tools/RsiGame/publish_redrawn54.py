"""The user's two corrections: independently redrawn skill icons and left-facing foes."""
import os,json,shutil,hashlib
from pathlib import Path
from publish_battlefield_readability import OUT,read,link,image,esc

def main():
    c=OUT/'attempt54'
    for rel in ['icon_validation_published/manifest.json','validation_published/party_feedback_manifest.json','skill_consumer_published/manifest.json','geometry_audit_published.json','skill_icon_generation/complete_provenance.json']:
        assert read(c/rel)['passed'],rel
    assert read(c/'icon_validation_published/manifest.json')['actual_source_count']==70
    for rel in ['monster_review/review.md','skill_review/material_review.md','skill_review/scene_review.md']:assert (c/rel).exists(),rel
    status=read(OUT/'normal_resume_after_attempt54/status.json');assert status['isPlaying'] and status['actualConsoleErrorCount']==0
    body=['<!doctype html><meta charset="utf-8"><title>54：重绘70技能图标与统一三怪面向</title>',
    '<style>body{background:#172521;color:#eee;font:17px/1.65 system-ui;max-width:1720px;margin:24px auto;padding:20px}a{color:#edca87}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{margin:4px 0 24px}img{width:100%}pre{white-space:pre-wrap;background:#21332c;padding:20px}.notice{background:#4e3a29;padding:18px}.icons{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:18px}.icons figure{text-align:center;padding:12px;background:#20352c}.icons img{width:96px;height:96px;object-fit:contain}.icons figcaption{font-size:15px}.icons details{text-align:left}.large img{width:170px;height:170px}button{font:inherit;background:#b8a06a;color:#172521;padding:8px 16px;border:0;cursor:pointer}</style>',
    '<h1>54：70技能独立重绘；三怪物统一朝左</h1>',
    '<p class="notice">针对本次两条反馈：当前技能入口不再裁用桌游原卡，70个正式技能各有独立重绘PNG；黄金龙、毒刃刺客、寒冰投石机换成清楚朝左的紧凑竖构图。第一版横构图把怪物缩小，明确拒绝并保留失败图。整体UI目标仍未完成，完整评分与不足见下方，不能把重绘数量当成人类可辨识性。</p>',
    '<h2>先看同一战斗场景</h2><div class="pair">'+image(c/'before/key_phases/three_enemies/screen.png','修改前：三怪物朝向不一致')+image(c/'validation_published/key_phases/three_enemies/screen.png','当前：头身/攻击方向向左，完整轮廓保留')+'</div>',
    '<h2>三怪物独立Sol审查</h2><p>'+link(c/'monster_review/inputs.json','9张原始输入与SHA')+' · '+link(c/'monster_review/protocol.md','审查协议')+' · '+link(c/'monster_review/review.json','逐项评分')+'</p><pre>'+esc((c/'monster_review/review.md').read_text())+'</pre>',
    '<h2>70张新技能图标</h2><p>每张分别由内置image_gen生成，按正式sourceId绑定；6个双面技能仍各用一个身份。名称、状态、频率、效果摘要和完整规则保留。下列默认以96px正方形查看（游戏使用108×96容器等比contain）；点击图片查看原PNG。图标只是识别线索，完整数值、费用和时机仍须读文字。</p><button onclick="document.getElementById(\'icons\').classList.toggle(\'large\')">切换96px / 放大查看</button><div id="icons" class="icons">']
    for r in read(c/'skill_icon_generation/asset_manifest.json')['rows']:
        from pathlib import Path
        url=esc(os.path.relpath(Path(r['path']),OUT).replace('\\','/'))
        body+=['<figure><a href="'+url+'"><img loading="lazy" src="'+url+'"></a><figcaption>'+esc(r['name'])+'<br><small>'+esc(r['sourceId'])+'</small></figcaption><details><summary>原规则与绑定</summary>'+esc(r['originalRule'])+'<p>SHA '+esc(r['sha256'])+'</p></details></figure>']
    body+=['</div><p>提示词要求1024方图与中央80%留白；工具实际给1254×1254，留白没有严格满足。保留原PNG，不通过裁图或改像素伪装达标；实际游戏中完整UV等比显示，边缘/复杂度的独立扣分继续保留。</p>',
    '<h2>技能原素材独立审查：70张全部读取</h2><p>'+link(c/'skill_review/actual_material_read_sha.json','实际读图SHA台账')+' · '+link(c/'skill_review/material_review.json','完整逐项结论')+'</p><pre>'+esc((c/'skill_review/material_review.md').read_text())+'</pre>',
    '<h2>实际小尺寸场景审查：28张全部读取</h2><p>'+link(c/'skill_review/scene_inputs.json','原始Unity截图与SHA')+' · '+link(c/'skill_review/scene_protocol.md','评分协议')+' · '+link(c/'skill_review/scene_review.json','完整场景评分')+'</p><pre>'+esc((c/'skill_review/scene_review.md').read_text())+'</pre>',
    '<h2>七英雄、70身份的实际技能区</h2><p>14次正式reset→fight，合法条件等级10、每次5个本英雄技能；每个sourceId都有真实资源绑定、完整UV和至少22pt全文悬浮逐字形可读检查，阅读不改变原生状态。是条件构筑，不是历史行为频率，也不是70技能全部真实发动。独立审查使用同一图标/绘制逻辑的前一程序集截图；最终程序集仅追加升级页图标说明，并已再次完成70项绑定/全文及25阶段验证。</p>']
    for hero in ['Arythea','Braevalar','Goldyx','Krang','Norowas','Tovak','Wolfhawk']:
        for group in ['0','1']:
            body+=['<h3>'+hero+' · 技能组 '+group+'</h3><div class="pair">'+image(c/'icon_validation_published'/hero/group/'key_phases/top/screen.png','顶部3项')+image(c/'icon_validation_published'/hero/group/'key_phases/bottom/screen.png','滚动后2项')+'</div>']
    body+=['<h2>历史技能区与规则保留</h2><p>历史51图仍完整保存，属于旧版本同名技能区示例，并非当前70条件构筑的严格同原生对照，不据此伪造提升分。</p><div class="pair">'+image(OUT/'attempt51/pressure/five_skills/key_phases/selected_0/screen.png','历史51：旧卡牌裁图/泛用图标')+image(c/'icon_validation_published/Goldyx/0/key_phases/top/screen.png','当前：本英雄重绘图标')+'</div>',
    image(c/'skill_consumer_published/key_phases/siege_skill_consumed/screen.png','正式005:00技能：原攻城1实际消费，非只点选'),
    '<details><summary>拒绝的横构图：不拿朝向正确抵消缩小退化</summary>'+image(c/'monster_trial/key_phases/three_enemies/screen.png','已拒绝、保存：宽图在窄槽中变成小怪物')+'<p>'+link(c/'monster_trial/rejection.json','拒绝原因')+'</p></details>',
    '<h2>重要阶段：优化前 / 最终程序集</h2>']
    for p in sorted((c/'validation_published/key_phases').glob('*/screen.png')):
        before=c/'before/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(before,'修改前') if before.exists() else '<p>没有同名旧图，不补造</p>')+image(p,'当前')+'</div>']
    body+=['<h2>复现、来源和范围</h2><ul>']
    for rel in ['source_manifest_published.json','published_compile_receipt.json','geometry_audit_published.json','icon_validation_published/manifest.json','validation_published/party_feedback_manifest.json','skill_consumer_published/manifest.json','skill_icon_generation/prompts.json','skill_icon_generation/complete_provenance.json','monster_portrait_jobs.json','monster_portrait_import.json','icon_validation_remaining/failure.json','next_primary_design_scope.json']:
        body+=['<li>'+link(c/rel,rel)+'</li>']
    body+=['</ul><p>54验证后正常场景已重新启动；随后进入55镜像修复，历史正常启动凭据明确记为54时点。原始卡图资源保留用于回退，embedded技能入口与各技能插图通道改用新图，完整规则由正式文字保留。资源未导入导致的真实缺图失败保留，已统一导入后在54最终程序集重新验证；没有用文件存在冒充运行时成功。</p>',
    '<h2>原始目标仍未完成</h2><p>本轮修正3只明确点名的怪物，不代表69怪物全部向左。69敌、7英雄、30佣兵的逐素材朝向清单已保存，正面/混合方向仍须逐一处理；左队朝右、英雄层级、耗竭/创伤状态符号、技能小尺寸细节简化及主战场文本负担继续推进。局部新图和条件采用评分不证明整体游戏、人类体验、完整32项或所有特殊弹层验收。</p><p>'+link(OUT/'attempt53/comparison_report.html','53原报告及全部失败历史')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf8')
    source=Path(__file__)
    shutil.copy2(source,c/'publication_tool.py')
    (c/'publication_tool_receipt.json').write_text(json.dumps(dict(path=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),scope='HTML publication only;54 product-source hashes remain frozen in source_manifest_published, not relabelled with55 code.'),indent=2),encoding='utf8')
    print('54 report published; full objective remains active')

if __name__=='__main__':main()
