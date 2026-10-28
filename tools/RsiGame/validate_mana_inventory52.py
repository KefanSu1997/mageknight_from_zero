"""Actual public inventory reading and native ruby-ring consumers."""
import argparse,copy,json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from validate_four_unit_battle_layout import FIGHT
from validate_entity_hover_reader import inside
from structured_ui_ppo import state
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1'
COLORS=['Red','Blue','Green','White','Gold','Black']
def inspect(record, unavailable_label='不可存'):
    e=record['evaluation'];native=json.loads(state(record['observation'])['audit.costNativeState'])
    assert (e['screenWidth'],e['screenHeight'])==(1920,1080)
    rows=[]
    for color in COLORS:
        prefix='Canvas/BattleTable/ManaInventory_'+color+'/'
        tok=next(t for t in e['texts'] if t['path']==prefix+'ManaTokenAmount')
        gem=next(t for t in e['texts'] if t['path']==prefix+'ManaCrystalAmount')
        infinite=native['artifactFlags']['infinite']
        expected='∞' if color in infinite else str(native['tokens'].get(color,0))
        crystals=native['crystals'].get(color,0)
        assert tok['fullText']==expected,(color,tok['fullText'],expected)
        assert gem['fullText']==(unavailable_label if color in ['Gold','Black'] and crystals==0 else str(crystals))
        for t in [tok,gem]:
            assert t['fontSize']>=26 and not t['isTextOverflowing']
            required={i for i,ch in enumerate(t['fullText']) if not ch.isspace()}
            assert required.issubset({g['sourceIndex'] for g in t['glyphs']})
            assert all(inside(g['screenRect'],t['screenRect'],2) for g in t['glyphs'])
        glyphs=[v for v in e['presentationSurfaces'] if v['path'].startswith(prefix) and v['name'] in ['ManaTokenSigil','ManaCrystalGem']]
        assert len(glyphs)==(1 if color in ['Gold','Black'] and crystals==0 else 2) and all(v['effectiveRendererAlpha']>.99 for v in glyphs)
        rows.append(dict(color=color,mana=expected,crystals=gem['fullText'],glyphs=glyphs))
    return rows
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,required=True);parser.add_argument('--live',type=Path,required=True);parser.add_argument('--unavailable-label',default='不可存');args=parser.parse_args()
    inspect_now=lambda record:inspect(record,unavailable_label=args.unavailable_label)
    out=args.out.resolve();c=Collector(out/'readers',args.live.resolve());fixture=json.loads((BASE/'attempt34/validation_corrected/setups/three_enemies.json').read_text())['fixture']
    c.prepare(fixture);dismiss_hover(c);native=state(c.observation)['audit.costNativeState'];rows=[]
    for color in COLORS:
        path='Canvas/BattleTable/ManaInventory_'+color;c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);r=c.snapshot(color+'_hover');inspect_now(r)
        body=next(t for t in r['evaluation']['texts'] if t['path'].endswith('/BattleHoverInfo/Body'))
        assert body['fontSize']>=22 and not body['isTextOverflowing']
        required={i for i,ch in enumerate(body['fullText']) if not ch.isspace()}
        assert required.issubset({g['sourceIndex'] for g in body['glyphs']})
        assert state(c.observation)['audit.costNativeState']==native
        c.rpc('unhover_info',path=path,expectedVersion=c.observation['version']);rows.append(dict(color=color,body=body['fullText'],font=body['fontSize']))
    save(out/'readers/manifest.json',dict(passed=True,rows=rows,captures=c.captures,native_unchanged=True))
    # Actual archived ring-in-hand state. Its full formal prefix is replayed and
    # compared by Collector.prepare, rather than injecting a card or resource.
    ring=json.loads((BASE/'attempt52/ring_archive_overflow_fixture.json').read_text())
    consumers=[]
    for mode in ['normal','burn']:
        c=Collector(out/mode,args.live.resolve());c.prepare(ring);dismiss_hover(c)
        before=json.loads(state(c.observation)['audit.costNativeState']);r=c.snapshot('mixed_inventory_before');inspect_now(r)
        c.click('Btn_Card_items_004_0');dismiss_hover(c)
        if mode=='burn':c.click('Btn_ArtifactBurn');dismiss_hover(c)
        c.snapshot('ring_selected');c.click('Btn_ArtifactPlay');dismiss_hover(c);r=c.snapshot('ring_consumed');after=json.loads(state(c.observation)['audit.costNativeState']);values=inspect_now(r)
        assert before['hand'].count('items_004')-after['hand'].count('items_004')==1
        if mode=='normal':
            # Red crystal stock is already3. The native extra crystal overflows
            # to a token, in addition to the ring's ordinary red token.
            assert after['crystals']['Red']==before['crystals']['Red']==3
            assert after['tokens'].get('Red',0)-before['tokens'].get('Red',0)==2
            assert after['fame']-before['fame']==1
        else:
            assert set(after['artifactFlags']['infinite'])==set(before['artifactFlags']['infinite'])|{'Red','Black'}
            assert after['tokens']==before['tokens'] and after['crystals']==before['crystals']
            path='Canvas/BattleTable/ManaInventory_Red';c.rpc('hover_info',path=path,expectedVersion=c.observation['version']);recalled=c.snapshot('infinite_Red_hover')
            body=next(t for t in recalled['evaluation']['texts'] if t['path'].endswith('/BattleHoverInfo/Body'))
            assert '本回合该色魔力无尽' in body['fullText'] and '回合结束失效' in body['fullText']
            assert json.loads(state(c.observation)['audit.costNativeState'])==after
            c.rpc('unhover_info',path=path,expectedVersion=c.observation['version'])
        consumers.append(dict(mode=mode,before=before,after=after,values=values,captures=c.captures))
    save(out/'manifest.json',dict(passed=True,readers=rows,consumers=consumers,ring_fixture=ring['provenance'],scope='6actual22pt quantity/rule hovers, same native state. SHA-bound archived ruby-ring-in-hand fixture: formal prefix compared to native; normal token+crystal overflow and burn infiniteRed/Black through actual UI, no native field injection. Not all resource rules or human icon comprehension.'))
    print('52 six readers, normal overflow and actual infinity consumers passed',flush=True)
if __name__=='__main__':main()
