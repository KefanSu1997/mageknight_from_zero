"""Read real hand-card previews with actual EventSystem scroll events, no rule commands."""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
import zipfile
from capture_evaluation_cycle import Collector,ROOT,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def spell_fixture():
    db=ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite'
    with sqlite3.connect(db.resolve().as_uri()+'?mode=ro',uri=True) as connection:
        rows=connection.execute("SELECT id,path,volume,sha256,reset_json FROM episodes WHERE json_extract(reset_json,'$.task.Build.AdditionalCards') LIKE '%magic_%' ORDER BY id LIMIT 128").fetchall()
    for episode,member,volume,sha,reset in rows:
        task=json.loads(reset)['task']
        if bool(task.get('OriginalOpening'))==(task.get('Build') is not None):continue
        archive=Path('H:/MageKnight_RL_Backups/20261012_before_reward_v2')/volume
        with zipfile.ZipFile(archive) as z:raw=z.read(member)
        assert hashlib.sha256(raw).hexdigest()==sha
        document=json.loads(raw)
        for pos,row in enumerate(document['trace'][:80]):
            spells=[i for i in row['before']['hand'] if i.startswith('magic_')]
            if row['before']['phase']=='Ranged' and spells:
                return dict(label='spell_reading',task=task,expected=row['before'],
                    prefix=[x['action'] for x in document.get('curriculum_prefix',[])]+[x['action'] for x in document['trace'][:pos]],
                    provenance=dict(archive=str(archive),member=member,source_sha256=sha,episode=episode,step=pos)),spells[0]
    raise ValueError('No actual archived spell-in-hand fixture')


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True)
    p.add_argument('--setups',type=Path,required=True);p.add_argument('--opening',type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    catalog={}
    for filename in ['basic_card.json','advanced_card.json','items.json','magic.json']:
        catalog.update({r['id']:r for r in json.loads((ROOT/'resources/text_json'/filename).read_text(encoding='utf8'))})
    def parts(identity):
        row=catalog.get(identity,{})
        if identity.startswith('magic_'):return [row['spell_top']['effect'],row['spell_bottom']['effect']]
        return [row[k] for k in ['base_effect','enhanced_effect','assign_effect','once_per_round'] if row.get(k)]
    fixtures=[json.loads(p.read_text(encoding='utf8'))['fixture'] for p in sorted(args.setups.glob('*.json'))]
    opening=json.loads(args.opening.read_text(encoding='utf8'))['fixture']
    cases=[('opening',opening,opening['expected']['hand'][0])]
    for label,prefix in [('complex_action','advanced_card_'),('artifact','items_'),('wound','w')]:
        found=[(fixture,identity) for fixture in fixtures for identity in fixture['expected']['hand'] if identity.startswith(prefix)]
        assert found,label
        fixture,identity=max(found,key=lambda pair:sum(map(len,parts(pair[1]))))
        cases.append((label,fixture,identity))
    fixture,identity=spell_fixture();cases.append(('spell',fixture,identity))
    c=Collector(args.out.resolve(),args.live.resolve());results=[]
    try:
        for label,fixture,identity in cases:
            fixture=dict(fixture,label='reading_'+label);c.prepare(fixture)
            dismiss_hover(c)
            other=next((x for x in c.observation['controls'] if x['name'].startswith('Btn_Card_') and not x['name'].startswith('Btn_Card_'+identity+'_') and x['clickable']),None)
            if other:c.click(other['name']);dismiss_hover(c)
            c.rpc('observe');control=next(x for x in c.observation['controls'] if x['name'].startswith('Btn_Card_'+identity+'_') and x['clickable'])
            before=state(c.observation)['audit.costNativeState']
            controls_before=sorted((x['path'],x['text'],x['interactable']) for x in c.observation['controls'])
            c.rpc('hover',path=control['path'],expectedVersion=c.observation['version'])
            c.snapshot(label+'_original_top')
            visited=set();needed=None;fulltext=None;scrolls=0;first_reading=False
            for tick in range(65):
                response=c.rpc('observe',evaluationDiagnostics=True)
                assert state(c.observation)['audit.costNativeState']==before
                title=next(x for x in response['evaluation']['texts'] if x['path'].endswith('/ReadingTitle'))
                assert title['fontSize']>=22 and not title['isTextOverflowing']
                assert title['fullText'].startswith('阅读 · ') and len(title['fullText'])>5
                for glyph in title['glyphs']:
                    r=glyph['screenRect']
                    assert 1532<=r['x'] and r['x']+r['width']<=1884 and 140<=r['y'] and r['y']+r['height']<=178
                progress=next(x for x in response['evaluation']['texts'] if x['path'].endswith('/ReadingProgress'))
                assert progress['fontSize']>=21 and not progress['isTextOverflowing']
                text=next((x for x in response['evaluation']['texts'] if x['path'].endswith('/PrintedRules')),None)
                if text:
                    assert text['fontSize']>=22 and not text['isTextOverflowing']
                    assert ''.join(g['character'] for g in text['glyphs'])==''.join(s for s in text['fullText'] if not s.isspace()),label
                    for part in parts(identity):assert part in text['fullText'],(identity,part,text['fullText'])
                    if identity=='w':assert '创伤' in text['fullText']
                    fulltext=text['fullText'];needed={g['sourceIndex'] for g in text['glyphs']}
                    for glyph in text['glyphs']:
                        r=glyph['screenRect']
                        if r['x']>=1542-1 and r['x']+r['width']<=1874+1 and r['y']>=186-1 and r['y']+r['height']<=624+1:
                            visited.add(glyph['sourceIndex'])
                    if not first_reading:
                        c.snapshot(label+'_reading_entered');first_reading=True
                    last=text['glyphs'][-1]['screenRect']
                    if needed<=visited and 186-1<=last['y'] and last['y']+last['height']<=624+1:break
                c.rpc('scroll',path=control['path'],expectedVersion=c.observation['version'],scrollY=-3)
                scrolls+=1
            else:raise AssertionError(('Failed to display every printed glyph',label,needed and len(needed-visited)))
            c.snapshot(label+'_reading_end')
            reading_title=title['fullText'];reading_progress=progress['fullText']
            for _ in range(scrolls+1):
                c.rpc('scroll',path=control['path'],expectedVersion=c.observation['version'],scrollY=3)
            response=c.rpc('observe',evaluationDiagnostics=True)
            top_progress=next(x for x in response['evaluation']['texts'] if x['path'].endswith('/ReadingProgress'))
            assert top_progress['fullText']=='原卡 · 向下阅读规则'
            assert state(c.observation)['audit.costNativeState']==before
            c.snapshot(label+'_original_revisited')
            c.rpc('unhover',path=control['path'],expectedVersion=c.observation['version']);c.rpc('observe')
            assert state(c.observation)['audit.costNativeState']==before
            assert state(c.observation)['ui.hoverPreviewVisible']=='False'
            assert controls_before==sorted((x['path'],x['text'],x['interactable']) for x in c.observation['controls'])
            c.snapshot(label+'_returned')
            result=dict(label=label,identity=identity,source_parts=parts(identity),fulltext=fulltext,scroll_events=scrolls,
                generated_nonspace_glyphs=len(needed),actually_visible_glyphs=len(visited),last_glyph_rect=last,
                all_glyphs_seen=True,native_state_and_action_controls_preserved=True,other_card_selected=other and other['name'],fixture=fixture['provenance'])
            result.update(sticky_reading_title=reading_title,reading_end_progress=reading_progress,original_revisited=True)
            results.append(result);save(args.out/(label+'_result.json'),result)
            print(label,identity,scrolls,'scroll events, all glyphs viewed',flush=True)
        save(args.out/'manifest.json',dict(passed=True,results=results,captures=c.captures,
            scope='Five real archived hand contexts, full source-effect substring checks, generated/actually visible glyph coverage via incremental EventSystem wheel events. No physical OS wheel, human reading or whole-card-catalog claim.'))
    except Exception as error:
        save(args.out/'failure.json',dict(error=repr(error),results=results,captures=c.captures));raise


if __name__=='__main__':main()
