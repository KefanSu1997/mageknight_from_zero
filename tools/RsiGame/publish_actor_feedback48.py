"""Publish48 narrowly: actual foot-aligned response, not whole-UI approval."""
import shutil,json
from publish_battlefield_readability import OUT,link,image,read,esc

def main():
    c=OUT/'attempt48'
    body=['<!doctype html><meta charset="utf-8"><title>战斗点击反馈48</title>',
    '<style>body{background:#14201f;color:#eee;font:17px/1.65 system-ui;margin:30px auto;max-width:1600px;padding:20px}a{color:#edc67e}.pair{display:grid;grid-template-columns:1fr 1fr;gap:15px}img{width:100%}figure{margin:0}pre{white-space:pre-wrap}</style>',
    '<h1>47 → 48：点击反馈跟随角色脚底</h1>',
    '<p>修复前，敌人青色输入反馈固定在旧布局Y=350，横穿金龙和九头蛇腹部。修复后读取该角色实际ActorHoverFocus的几何，英雄、佣兵与敌人共享此依据。正式点击区域、图片UV/尺寸、原文字与字号保持。</p>',
    '<p>独立复审以实际出现青环的即时帧验证局部修复。后续青环消失的画面只能检查稳定布局，不能单独证明反馈位置正确。整体目标仍未完成：光照/接触、拥挤、技能美术和全套rubrics未因本修复通过。</p>',
    '<h2>独立审查与全部输入</h2><p>'+link(c/'scene_review/inputs.json','15张图路径和SHA')+' · '+link(c/'scene_review/protocol.md','协议')+' · '+link(c/'scene_review/review.json','逐项评分与局限')+'</p>',
    '<pre>'+esc((c/'scene_review/review.md').read_text(encoding='utf-8'))+'</pre>',
    '<h2>实际出现反馈的即时点击帧</h2><p>一次click+screenshot请求内捕获。右侧“暴徒”悬浮文字属于当时物理悬浮上下文干扰，不据此宣称默认界面或新回归；输入焦点/悬浮冲突仍需另验。选择不等于能力发动。</p>']
    for label in ('enemy','unit','hero'):body+=[image(c/'immediate_feedback/key_phases'/label/'screen.png',label+'点击反馈实际帧')]
    body+=['<h2>补齐九头蛇与拥挤场景即时帧</h2><p>原15图审查的缺口单独补证，原评审不覆盖、不回写。'+link(c/'scene_review_extended/inputs.json','追加三图及SHA')+' · '+link(c/'scene_review_extended/review.json','补充独立审查')+'</p>',
        '<pre>'+esc((c/'scene_review_extended/review.md').read_text(encoding='utf-8'))+'</pre>']
    for label in ('hydra','crowded_gold','crowded_hydra'):body+=[image(c/'immediate_extended/key_phases'/label/'screen.png',label+'即时帧')]
    body+=['<h2>六组同状态稳定界面对照</h2><p>before来自47实际归档原图拷贝，非新拍摄。青环为短时反馈，部分after帧已消失，不能以未出现来证明修复。</p>']
    for row in read(c/'after/manifest.json')['captures']:
        n=row['label'];body+=['<h3>'+esc(n)+'</h3><div class="pair">'+image(c/'before/key_phases'/n/'screen.png','47')+image(c/'after/key_phases'/n/'screen.png','48')+'</div>']
    body+=['<h2>重要阶段与典型界面归档</h2>']
    for p in sorted((c/'validation/key_phases').glob('*/screen.png')):
        prior=OUT/'attempt47/validation/key_phases'/p.parent.name/'screen.png'
        body+=['<h3>'+esc(p.parent.name)+'</h3><div class="pair">'+(image(prior,'47同一验证场景') if prior.exists() else '<p>无对应旧图</p>')+image(p,'48实际')+'</div>']
    body+=['<h2>技术证据及限度</h2><p>'+link(c/'geometry_audit.json','图像/文字/画布核对')+' · '+link(c/'click_geometry_audit.json','实际点击中心和宽高核对')+' · '+link(c/'immediate_feedback/manifest.json','三种演员真实点击，规则状态不变')+' · '+link(c/'validation/party_feedback_manifest.json','25场景与普通格挡真实消费')+' · '+link(c/'compile_receipt.json','实际编译与MVID')+' · '+link(c/'source_manifest.json','源码SHA')+'</p>',
    '<p>一个九头蛇中性帧的限时toast已过期，首次严格文字集合比较失败已保留；修正版单独记录此可见性差异，不把它当作永久信息缺失。未删除日志回查。原始几何审计中的点击字段已核为实际x/y/width/height，避免使用不存在的screenRect字段。</p>',
    '<p>'+link(OUT/'attempt47/comparison_report.html','47素材前后对比与原拒绝结论')+' · '+link(OUT/'normal_resume_after_attempt48_final/status.json','恢复普通试玩状态')+'</p>']
    content='\n'.join(body)
    with (c/'comparison_report.html').open('x',encoding='utf-8') as f:f.write(content.replace('<meta charset="utf-8">','<meta charset="utf-8"><base href="../">',1))
    shutil.copy2(OUT/'index.html',c/'previous_index.html');(OUT/'index.html').write_text(content,encoding='utf-8')
    print('48 published, whole goal remains incomplete')

if __name__=='__main__':main()
