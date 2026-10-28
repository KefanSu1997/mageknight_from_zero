"""Preserved visual comparison of the party-state symbol iteration."""
import json,shutil,hashlib
from pathlib import Path
from html import escape
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt56'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    assert read(OUT/'geometry_audit.json')['passed']
    assert read(OUT/'symbol_validation_verified/manifest.json')['passed']
    def image(path,title):
        rel=str(path.relative_to(OUT)).replace('\\','/')
        return '<figure><figcaption>'+escape(title)+'</figcaption><a href="'+rel+'"><img loading="lazy" src="'+rel+'"></a></figure>'
    parts=['<!doctype html><meta charset="utf-8"><title>56 · 佣兵状态符号与英雄领袖标识</title>',
        '<style>body{background:#172521;color:#eee;font:17px/1.7 system-ui;max-width:1700px;margin:24px auto;padding:20px}a{color:#e7c987}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0 0 22px}img{width:100%}pre{white-space:pre-wrap;background:#20382d;padding:20px}</style>',
        '<h1>56 · 状态由符号表达，具体限制按需阅读</h1>',
        '<p>佣兵原“就绪／耗竭 · 伤N”常驻文字改为闪电／沙漏与血滴＋实际数字。符号形状和颜色共同区分状态；0创伤也保留数字。每个符号有独立悬浮入口：就绪不等于当前阶段一定能激活，耗竭也不代替独立承伤判定。英雄保留原310px对佣兵220px的布局高度，并添加冠徽及悬浮说明；未缩小角色来腾位置。</p>',
        '<p>25重要阶段同原生状态前后对照，按钮位置与可用性、全部立绘尺寸位置/UV、其余常驻文字保持一致。移除的是状态行文字，完整原生状态与限制保留在角色/符号悬浮和原能力界面。临时战斗反馈按时间消失，审计中独立列出，未冒充常驻信息删除。</p>',
        '<p>实际佣兵激活产生格挡4后，耗竭且原能力按钮禁用；另用真实带伤轨迹验证0/1创伤数字与7个状态悬浮入口。程序证据不证明玩家已理解符号。</p>',
        '<p><a href="geometry_audit.json">25组程序核对</a> · <a href="symbol_validation_verified/manifest.json">7个符号射线悬浮与带伤数字</a> · <a href="after_final/unit_consumer.json">实际激活及耗竭</a> · <a href="source_manifest.json">源码与程序集</a></p>']
    review=OUT/'scene_review/review.md'
    parts+=['<h2>独立Sol视觉审查</h2>',('<pre>'+escape(review.read_text(encoding='utf8'))+'</pre>') if review.exists() else '<p>审查进行中；不把程序门禁记为视觉通过。</p>']
    fresh=OUT/'scene_review_fresh/review.md'
    if fresh.exists():
        parts+=['<h2>第二位新上下文独立Sol：明确记录直观性退化</h2><p>信息易懂4→3，游戏美感3→4，英雄身份3→4，数值清楚4→4，信息保留4→4。两份审查都只条件采用，不能把视觉变简洁当成认知成本下降。下一轮先补正式符号图鉴与发现提示，再处理右侧黑底说明。</p><pre>'+escape(fresh.read_text(encoding='utf8'))+'</pre><p><a href="scene_review/review.json">18图审查原JSON</a> · <a href="scene_review/actual_read_sha.json">18图SHA</a> · <a href="scene_review_fresh/review.json">新上下文12图评分原JSON</a> · <a href="scene_review_fresh/actual_read_sha.json">12图SHA</a></p>']
    for label in ['three_enemies_neutral','unit_selected_in_block','unit_consumed','assign_damage','melee_two_enemies']:
        a=OUT/'before/key_phases'/label/'screen.png';b=OUT/'after_final/key_phases'/label/'screen.png'
        if not (a.exists() and b.exists()):continue
        parts+=['<h2>'+escape(label)+'</h2><div class="pair">'+image(a,'前')+image(b,'最终修正版')+'</div>']
    parts+=['<h2>创伤0与1的实际队伍</h2>',image(OUT/'symbol_validation_verified/key_phases/wounded_party_neutral/screen.png','不同状态同时呈现；创伤与就绪独立')]
    for p in sorted((OUT/'symbol_validation_verified/key_phases').glob('symbol_*/screen.png')):
        parts+=[image(p,p.parent.name+' · 实际悬浮')]
    if (OUT/'four_unit_symbol_geometry.json').exists():
        assert read(OUT/'four_unit_symbol_geometry.json')['passed']
        parts+=['<h2>四佣兵边界：正式激活与三敌同场</h2><p>条件等级7、4普通指挥槽；第四部队实际生成格挡5并耗竭。7张当前场景均有9个英雄/状态符号和4个真实23pt创伤数字；没有55同状态四佣兵截图，不伪造严格配对提升分。</p><p><a href="four_unit_validation/manifest.json">实际选中/激活/容量负例</a> · <a href="four_unit_symbol_geometry.json">状态符号及数字几何</a></p>']
        for label in ['four_units_neutral','fourth_unit_block5_consumed','four_units_three_enemies']:
            parts+=[image(OUT/'four_unit_validation/key_phases'/label/'screen.png',label)]
    parts+=['<details><summary>全部25阶段前后截图</summary>']
    for p in sorted((OUT/'before/key_phases').glob('*/screen.png')):
        parts+=['<h3>'+escape(p.parent.name)+'</h3><div class="pair">'+image(p,'前')+image(OUT/'after_final/key_phases'/p.parent.name/'screen.png','最终修正版')+'</div>']
    parts+=['</details><h2>拒绝并保留的首版</h2><p>首版23pt创伤数字放在30px高容器中，实际零字形，截图看不到0/1，已拒绝。修正为40px容器，字号仍23pt；最终逐字符验证实际字形。旧after、symbol_validation、source_trial完整保留。中间补测误用按钮hover接口，保留原失败；最终用现有hover_info真实射线入口通过。</p>',image(OUT/'after/key_phases/unit_consumed/screen.png','拒绝的首版：血滴后数字缺失'),
        '<h2>完整目标仍在推进</h2><p>冠徽和状态符号只是本轮局部改善。右侧黑底文字说明、上下文动作区仍需进一步符号化；魔力统一美术风格、剩余正面/冲突朝向、英雄光影层级及全部技能简化也未全部验收。不宣称点击成本下降、全目录视觉通过或人类理解通过。</p><p>实际投影身体高度也已核对：4英雄的本组场景中，英雄/最高佣兵比值最低仅1.076；310/220是布局上限，不能冒充全部7英雄都有足够体量优势。冠徽不等于英雄光影层级完成。</p><p><a href="hierarchy_geometry_inventory.json">实际身体高度清单</a> · <a href="requirements_audit.json">完整目标逐项审计</a> · <a href="symbol_reader_geometry.json">7状态说明全文字形实际掩膜</a></p>',
        '<p><a href="../attempt55/comparison_report.html">55全目录朝向检查与镜像</a> · <a href="../attempt54/comparison_report.html">54重绘70技能及三怪物</a></p>']
    content='\n'.join(parts);(OUT/'comparison_report.html').write_text(content,encoding='utf8')
    if not (OUT/'previous_index.html').exists():shutil.copy2(BASE/'index.html',OUT/'previous_index.html')
    (BASE/'index.html').write_text(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="attempt56/">',1),encoding='utf8')
    shutil.copy2(Path(__file__),OUT/'publication_tool.py')
    (OUT/'publication_tool_receipt.json').write_text(json.dumps(dict(path=str(Path(__file__)),sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),scope='HTML publication only; frozen product-source archive is never refreshed from later product code.'),indent=2),encoding='utf8')
    print(OUT/'comparison_report.html')
if __name__=='__main__':main()
