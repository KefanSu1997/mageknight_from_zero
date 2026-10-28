"""Publish preserved Unity before/after scenes and independent facing decisions."""
import json,hashlib,shutil
from pathlib import Path
from html import escape

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
OUT=BASE/'attempt55'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def main():
    assert read(OUT/'geometry_audit.json')['passed']
    assert read(OUT/'v5_regression_corrected/manifest.json')['passed']
    rows=['<!doctype html><meta charset="utf-8"><title>55 · 角色朝向镜像修复</title>',
        '<style>body{background:#172521;color:#eee;font:17px/1.7 system-ui;margin:28px auto;max-width:1700px;padding:20px}a{color:#e6c886}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}img{width:100%}figure{margin:0}pre{white-space:pre-wrap;background:#21382e;padding:18px}table{width:100%;border-collapse:collapse}td,th{text-align:left;border-bottom:1px solid #496253;padding:8px}</style>',
        '<h1>55 · 全目录检查后的镜像修复</h1>',
        '<p>已检查69怪物、7英雄、30佣兵的原素材。当前13项候选（9怪物、4佣兵）通过渲染UV水平镜像试修，不改原PNG，不镜像文字和按钮；图像与投影同步镜像，并补偿透明留白使可见身体仍居中。原尺寸和1920×1080画布保留。</p>',
        '<p>15组实际Unity同原生状态前后截图已保存，规则状态、点击区域、文字内容/字号/位置一致；角色宽高与纵坐标误差小于0.001px。新绘制的黄金龙、毒刃刺客、寒冰投石机独立回归，保持原图UV，不继承旧图镜像。剩余正面/混合朝向仍有待处理，不能称全目录已修复。</p>',
        '<p><a href="geometry_audit.json">几何与原生状态核对</a> · <a href="mirror_registry.json">13项版本/SHA清单</a> · <a href="../attempt54/next_primary_design_scope.json">54重绘记录与待办范围</a></p>']
    if (OUT/'enemy_review/review.json').exists() and (OUT/'party_review/review.json').exists():
        er=read(OUT/'enemy_review/review.json');pr=read(OUT/'party_review/review.json')
        rows+=['<p><strong>独立视觉结果：8只怪物、2种佣兵认可镜像改善；3项已镜像仍不确定，另2项未翻转对照也未解决。</strong>不是13项全部修复，更不是106项全部方向通过。每项1–5分、观察依据与限制见下方。</p>']
        assert er['summary']['accept']==8 and pr['summary']['accept']==2
    for group,title in [('enemy','怪物'),('party','佣兵')]:
        path=OUT/(group+'_review')/'review.md'
        rows.append('<h2>'+title+' · 独立Sol审查</h2>')
        rows.append('<pre>'+escape(path.read_text(encoding='utf8'))+'</pre>' if path.exists() else '<p>独立逐图审查进行中。当前只是实机镜像候选，不将程序检查冒充美术通过。</p>')
    for before in sorted((OUT/'before/key_phases').glob('*/screen.png')):
        label=before.parent.name
        rows+=['<h2>'+escape(label)+'</h2><div class="pair"><figure><figcaption>修改前</figcaption><a href="before/key_phases/'+label+'/screen.png"><img src="before/key_phases/'+label+'/screen.png"></a></figure><figure><figcaption>镜像候选后（060 / 2_012 为未翻转对照）</figcaption><a href="after/key_phases/'+label+'/screen.png"><img src="after/key_phases/'+label+'/screen.png"></a></figure></div>']
    rows+=['<h2>为什么没有把所有素材统一翻转</h2><p>兽人攻城兵（060）与乌顿弩手（2_012）存在头部、装备或攻击方向冲突。翻转会同时反转本来合理的部分，因此保留原图作为对照；是否修复以独立场景审查为准。正面朝向也无法用一次镜像变成明确侧向。</p>',
        '<p>所有前后截图、条件构筑来源、原生快照、生成素材与旧源码均保留。这里是美术方向修复，未声称减少点击次数、全局操作成本或完成人类体验验收。</p>']
    rows+=['<h2>106项逐素材清单：原图方向与当前处理</h2><p>以下原图方向来自54的独立素材审查；原素材keep不等于完整实景通过，front/ambiguous仍待进一步处理。3项新v5另有9图审查，不沿用原图结论。</p><table><tr><th>身份</th><th>名称</th><th>期望方向</th><th>原图观察</th><th>当前处理</th></tr>']
    mirrored={r['id'] for r in read(OUT/'mirror_registry.json')['items']}
    for group in ['enemies','party']:
        for r in read(BASE/f'attempt54/facing_inventory/{group}/review.json')['items']:
            identity=r['id']
            status=('新v5向左；54独立场景采用' if identity in ['monster_037','monster_065','monster_068']
                else '55已镜像；场景独立结论见上方' if identity in mirrored
                else '原图保留；素材方向符合，场景仍须结合判断' if r.get('recommendation')=='keep'
                else '未镜像；正面/混合方向待处理')
            rows+=['<tr>'+''.join('<td>'+escape(str(v))+'</td>' for v in [identity,r.get('name',''),r.get('desired_direction',''),r.get('observed_direction',''),status])+'</tr>']
    rows+=['</table>']
    content='\n'.join(rows)
    (OUT/'comparison_report.html').write_text(content,encoding='utf8')
    previous=OUT/'previous_index.html'
    if not previous.exists():shutil.copy2(BASE/'index.html',previous)
    (BASE/'index.html').write_text(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="attempt55/">',1),encoding='utf8')
    paths=[ROOT/'Assets/Scripts/Battle'/n for n in ['PveBattleController.EmbeddedBattle.cs','PveBattleController.BattlefieldReadability.cs']]
    paths += [ROOT/'Assets/Resources/UI/CharacterFacing/mirrors_v1.json']
    paths += [ROOT/'tools/RsiGame'/n for n in ['prepare_character_mirrors55.py','audit_character_mirrors55.py','publish_character_mirrors55.py']]
    dest=OUT/'source_final';dest.mkdir(exist_ok=True);records=[]
    manifest=OUT/'source_manifest.json'
    if not manifest.exists():
        for path in paths:
            shutil.copy2(path,dest/path.name);records.append(dict(path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        manifest.write_text(json.dumps(dict(files=records,compile=read(OUT/'compile_status.json')),ensure_ascii=False,indent=2),encoding='utf8')
    else:
        # Historical HTML can be republished after the product has moved on.
        # Never bind later product code to an older runtime receipt.
        for record in read(manifest)['files']:
            saved=dest/Path(record['path']).name
            assert hashlib.sha256(saved.read_bytes()).hexdigest()==record['sha256'],saved
    print(OUT/'comparison_report.html')
if __name__=='__main__':main()
