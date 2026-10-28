"""Scoped same-native, actual-renderer audit of the skill-tray redesign."""
import json
from pathlib import Path
from structured_ui_ppo import state
from validate_entity_hover_reader import inside,intersect

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt50'
def read(p): return json.loads(p.read_text(encoding='utf-8'))

def main():
    rows=[]
    for label in ['neutral','skills_open','skills_selected','roster_open','roster_selected']:
        before=read(OUT/'before/key_phases'/label/'capture.json')
        after=read(OUT/'after_wide_final/key_phases'/label/'capture.json')
        assert state(before['observation'])['audit.costNativeState']==state(after['observation'])['audit.costNativeState']
        a=before['evaluation'];b=after['evaluation']
        assert (b['screenWidth'],b['screenHeight'])==(1920,1080)
        ai={x['path']:x for x in a['rawImages']};bi={x['path']:x for x in b['rawImages']}
        added=bi.keys()-ai.keys()
        assert all(bi[p]['name']=='OriginalSkillPictogram' for p in added)
        for p in ai:
            assert p in bi,p
            for k in ['texture','sourceWidth','sourceHeight','screenRect','uvRect','componentColor','shader','atmosphereSaturation','atmosphereTop','atmosphereGround','projectionOpacity','projectionShear']:
                assert ai[p][k]==bi[p][k],(label,p,k)
        at={t['path']:t for t in a['texts']};bt={t['path']:t for t in b['texts']}
        def edited(p): return '/Skills/' in p or p.endswith('/SkillLabel') or '/Inspector/Actions/Btn_SkillChoice_' in p or '/Inspector/Actions/Btn_UseSkill/' in p or p.endswith('/SkillDecisionRule')
        for p in at.keys()|bt.keys():
            if edited(p):continue
            assert p in at and p in bt,(label,p)
            for k in ['fullText','fontSize','screenRect','isTextOverflowing']:
                assert at[p][k]==bt[p][k],(label,p,k)
        actual=[]
        for t in bt.values():
            if t['name'] not in ['ChoiceIdentity','ChoiceUsage','ChoiceFrequency','ChoiceEffectSummary','ChoiceRuleHint']:continue
            if '/Skills/' not in t['path']:continue
            assert t['fontSize'] >= (26 if t['name'] in ['ChoiceIdentity','ChoiceUsage'] else 22)
            assert not t['isTextOverflowing'],t['path']
            required={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
            assert required.issubset({g['sourceIndex'] for g in t['glyphs']}),t['path']
            assert all(inside(g['screenRect'],t['screenRect'],3) for g in t['glyphs']),t['path']
            assert inside(t['screenRect'],dict(x=0,y=0,width=1920,height=1080)),t['path']
            actual.append(dict(path=t['path'],font=t['fontSize'],text=t['fullText'],rect=t['screenRect']))
        for t in actual:
            for other in actual:
                if t['path']>=other['path']:continue
                assert not intersect(t['rect'],other['rect']),(t['path'],other['path'])
        rows.append(dict(label=label,native_identical=True,unchanged_existing_raw_images=len(ai),added_original_pictograms=len(added),skill_texts=actual))
    result=dict(passed=True,pairs=rows,scope='Five actual same-native1920x1080 pairs. Existing actor assets, UV, size, colors/shader parameters unchanged. Skill names/status26pt and frequency/effect/hint22pt, full generated glyphs, no mutual rectangle overlap or canvas clipping. Skills tray geometry and selected-skill action placement intentionally changed; not all controls stationary, not all70 runtime rules or human understanding. Five-skill clipping/scroll audited separately with measured mask.')
    (OUT/'geometry_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Five same-native geometry pairs passed')

if __name__=='__main__':main()
