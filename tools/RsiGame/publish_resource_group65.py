"""Display actual scoped review and native evidence without inventing scores."""
import hashlib
import html
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT = BASE / 'attempt65'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    review_dir = OUT / 'rows_review'
    packet = read(review_dir / 'input.json')
    review = read(review_dir / 'review.json')
    mapping = read(OUT / 'private_rows_mapping.json')
    old = next(s for s, role in mapping.items() if role == 'before')
    new = next(s for s, role in mapping.items() if role == 'after')
    assert review['overall_numeric_score'] is None
    assert len(review['scores']) == 8 and review['read_ledger']['images_viewed_original']
    for e in packet['required_images'] + packet['required_text']:
        assert hashlib.sha256(Path(e['path']).read_bytes()).hexdigest() == e['sha256']
    for e in packet['required_images']:
        assert any(x['side'] == e['side'] and x['scene_id'] == e['scene_id']
                   and x['sha256'] == e['sha256'] and x['matched'] for x in review['read_ledger']['images'])
    assert read(OUT / 'audit_grouped_rows.json')['passed']
    assert read(OUT / 'mana_consumers_rows/manifest.json')['passed']
    assert read(OUT / 'hover_panel_audit.json')['passed']
    assert read(BASE / 'normal_resume_after_attempt65/normal_launch.json')['passed']
    esc = lambda x: html.escape(str(x))
    pretty = lambda x: esc(json.dumps(x, ensure_ascii=False, indent=2))
    css = 'body{background:#172820;color:#eee5d3;margin:30px;font:18px/1.6 system-ui}a{color:#e7c77c}img{width:100%}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #526b58;padding:10px;text-align:left}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#22372b;padding:18px}select{font:inherit;padding:8px}figure{margin:0}summary{cursor:pointer}@media(max-width:1000px){.pair{grid-template-columns:1fr}}'
    h = ['<!doctype html><meta charset="utf-8"><title>65 · 结算恢复与整体资源栏</title><style>' + css + '</style>',
         '<h1>65 · 结算恢复与整体资源栏</h1><p>恢复明确的胜负、击败数、名望、手牌创伤和后续操作提示。魔力库存撤下逐格黄色底座，使用新绘制的整体材质，保留六色列与魔力/晶体两行。数量仍来自正式规则。</p>',
         '<p><strong>全画面仍未通过。</strong>四组同场景独立复评：资源区域四项视觉子分达到4，结算即时反馈2→4；全画面设计2→3，辨识度、密度和拥挤仍只有3。敌人/佣兵黄色标记、胶囊主动作及叠场景标签仍需继续统一。</p>',
         '<p>这是本次四组原图的局部静态比较，不与上轮十场景分数拼接。完整背景/状态、像素、命中与日志回查仍未验证，无总分或真人表现结论。</p>',
         '<p><a href="rows_review/input.json">冻结输入</a> · <a href="rows_review/system_prompt.txt">独立SP</a> · <a href="rows_review/rubric_subset.json">原rubric相关六项</a> · <a href="rows_review/review.json">独立原始结果</a></p>',
         '<h2>同范围全画面视觉子分</h2><table><tr><th>条目</th><th>64</th><th>65</th></tr>']
    names = {'1.1': '当前必要信息', '2.2': '背景分离', '2.4': '密度与主次', '3.4': '控件设计', '4.5': '视觉拥挤', '6.1': '结算即时反馈（仅两结算图）'}
    for rid, name in names.items():
        h.append('<tr><td>' + esc(rid + ' ' + name) + '</td><td>' + str(review['worst_visual_scores'][old][rid]) + '</td><td>' + str(review['worst_visual_scores'][new][rid]) + '</td></tr>')
    h.append('</table><h2>保留全部重要场景</h2><p>左侧64，右侧可切换65的失败候选与当前版本。点击图片打开原图。</p>')
    labels = ['ranged', 'block', 'source_play', 'assign_damage', 'damage_toggle', 'melee_two_enemies', 'won_result', 'end', 'end_mixed', 'outside']
    opts = ''.join('<option value="' + x + '">' + x + '</option>' for x in labels)
    h.append('<select id="scene">' + opts + '</select><select id="version"><option value="grouped_rows">65当前 · 分行</option><option value="final">65并排试验 · 语义较弱</option><option value="initial">65初稿 · 标签未生成（拒绝）</option></select><div class="pair"><figure><figcaption>64</figcaption><a id="oldlink"><img id="oldimg"></a></figure><figure><figcaption>65</figcaption><a id="newlink"><img id="newimg"></a></figure></div>')
    old_root = ROOT / 'AutomationOutputs/RsiGame/workflow_revisions/20261028_density_and_visual_v3_3/original_vs_current'
    rel = os.path.relpath(old_root, OUT).replace('\\', '/')
    h.append('<script>function show(){let s=document.getElementById("scene").value,v=document.getElementById("version").value;document.getElementById("oldimg").src=document.getElementById("oldlink").href="' + rel + '/key_phases/"+s+"/screen.png";document.getElementById("newimg").src=document.getElementById("newlink").href=v+"/key_phases/"+s+"/screen.png";}document.getElementById("scene").onchange=show;document.getElementById("version").onchange=show;show();</script>')
    h += ['<h2>实际规则与几何核验</h2><ul><li>十组同原生状态；九组战斗HUD、一组战外。原立绘/手牌图片、大小、UV、材质与按钮命中矩形保留。</li><li>六个实际资源说明：22pt全文字形位于真实面板内，读取不改状态。</li><li>普通戒指：红晶满3时溢出转魔力，实际红魔力+2、名望+1；烧毁戒指：实际红/黑无尽，数量显示与原生状态一致。</li><li>胜利、未胜与混合结束三组：显式字段逐一匹配正式状态，字形完整；未恢复常驻黑色长说明板。</li></ul>',
          '<p><a href="audit_grouped_rows.json">十场景几何/信息</a> · <a href="mana_consumers_rows/manifest.json">实际规则消费</a> · <a href="hover_panel_audit.json">六说明面板核验</a> · <a href="compile_distinct_rows.json">编译与源码绑定</a></p>',
          '<h2>无尽资源实际消费截图</h2><a href="mana_consumers_rows/burn/key_phases/ring_consumed/screen.png"><img src="mana_consumers_rows/burn/key_phases/ring_consumed/screen.png"></a>',
          '<h2>素材与协议留档</h2><p>使用内置image_gen，新PNG保留原始透明度。HUD的九宫格区域和边框合同有单独记录，不应用于人物插画。</p><p><a href="asset_prompt.txt">完整生成prompt</a> · <a href="asset_generation.json">原素材SHA、UI区域与来源</a> · <a href="independent_review/review.json">首次审查：并排数量语义不足</a></p>',
          '<details><summary>当前完整独立审查、每场景事实与未验证门禁</summary><pre>' + pretty(review) + '</pre></details>',
          '<p>正常英雄选择入口已重新启动，测试fixture未留作正常开局。整体UI优化目标继续，当前不作完整设计验收。</p>']
    text = '\n'.join(h)
    (OUT / 'comparison_report.html').write_text(text, encoding='utf-8')
    missing = [x for x in set(re.findall(r'(?:href|src)="([^"#]+)"', text)) if not (OUT / x).exists()]
    assert not missing, missing
    decision = dict(goal_complete=False, global_design_accepted=False, review_source='rows_review/review.json',
                    review_sha256=hashlib.sha256((review_dir / 'review.json').read_bytes()).hexdigest(),
                    mapping_after_review=mapping, scope='Four registered paired static contexts; scoped ROI4 and full-scene3 cannot imply global acceptance.',
                    remaining=review['next_steps'], local_links_valid=True)
    (OUT / 'decision.json').write_text(json.dumps(decision, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Published65 version-preserving report; global acceptance remains false')


if __name__ == '__main__':
    main()
