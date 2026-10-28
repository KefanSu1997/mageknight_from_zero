"""Replay the verified recruitment prefix, select the formerly offscreen fourth unit.

Refresh Unity after --prepare; restore its seed with --restore after completion.
"""
import json, runpy, sys
from pathlib import Path

args = sys.argv[1:]
sys.argv = ['tools/build_pve_headband_leadership_acceptance.py'] + [x for x in args if x in ('--prepare', '--restore')]
runpy.run_path(sys.argv[0], run_name='__main__')
p = Path('AutomationConfigs/pve_battle.json')
config = json.loads(p.read_text(encoding='utf-8'))
prefix = config['steps'][:60]
for step in prefix:
    step['after'] = []
    step['skipScreenshot'] = True
steps = prefix

def click(path, label, checks=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label,
      waitAfterSeconds=.3, skipScreenshot=not capture,
      after=[dict(key=k, expected=str(v), rule='分页保留实际供应实体选择；确认焚毁前无消费，取得后使用原能力')
             for k,v in {'ui.truncated':'', **(checks or {})}.items()]))

click('SpecialArtifact/Btn_FreeOfferNext', '第二页实际第四张凶蛮僧侣', capture=True)
click('SpecialArtifact/Btn_FreeOffer_3', '点选原第四张供应实体', {'units.count':1, 'removed.ids':''}, True)
click('SpecialArtifact/Btn_FreeOfferPrevious', '返回第一页仍保留实际选择')
click('SpecialArtifact/Btn_FreeOfferNext', '再次翻页原选择标记保留', capture=True)
suffix = config['steps'][61:]
for step in suffix:
    step['label'] = step['label'].replace('北境僧侣', '凶蛮僧侣')
    step['skipScreenshot'] = True
suffix[0]['after'].append(dict(key='offer.ids', expected='army_2_001,army_2_005,army_2_007', rule='只移走第二页实际凶蛮僧侣，其他供应保留'))
suffix[0]['skipScreenshot'] = False
for step in suffix:
    if step['label'] == '僧侣原攻击3加领导2':
        step['skipScreenshot'] = False
steps.extend(suffix)
if '--from' in args:
    for step in steps[:int(args[args.index('--from')+1])]:
        step['after'] = []; step['skipScreenshot'] = True
config['steps'] = steps
p.write_text(json.dumps(config, ensure_ascii=False, indent=2), encoding='utf-8')
print('Command offer pages',len(steps),'steps',sum(len(s['after']) for s in steps),'incremental assertions')
