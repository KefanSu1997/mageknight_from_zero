"""Real non-button information hover: raycast, visible guidance, unchanged battle."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector, save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out',type=Path,required=True);p.add_argument('--live',type=Path,required=True)
    p.add_argument('--fixture',type=Path,required=True);args=p.parse_args()
    args.out.mkdir(parents=True,exist_ok=False);c=Collector(args.out.resolve(),args.live.resolve())
    c.prepare(json.loads(args.fixture.read_text(encoding='utf8'))['fixture']);c.click('Btn_Next');dismiss_hover(c)
    neutral=c.snapshot('block_neutral')
    assert not any(t['name'] in ['MinimalDecisionCue','BlockRiskCue'] for t in neutral['evaluation']['texts'])
    results=[]
    for label,path,phrase in [
        ('phase','Canvas/BattleTable/PhaseTrack/PhaseHelp','每次攻击须完整格挡'),
        ('unit','Canvas/BattleTable/Inspector/Actions/ContextInfoHover','激活能力生成格挡资源')]:
        if label=='unit':c.click('Btn_UnitActor_1');dismiss_hover(c)
        prior=state(c.observation);before=c.snapshot(label+'_before')
        response=c.rpc('hover_info',path=path,expectedVersion=c.observation['version'])
        assert response['raycastProbes'][0]['valid']
        capture=c.snapshot(label+'_hover')
        text=next(t for t in capture['evaluation']['texts'] if t['path'].endswith('/BattleHoverInfo/Body'))
        start=text['fullText'].index(phrase);indices={g['sourceIndex'] for g in text['glyphs']}
        assert set(range(start,start+len(phrase))).issubset(indices),phrase
        assert text['fontSize']>=21
        current=state(c.observation)
        assert current['audit.costNativeState']==prior['audit.costNativeState']
        assert current['audit.costFixture']==prior['audit.costFixture']
        c.rpc('unhover_info',path=path,expectedVersion=c.observation['version'])
        after=c.snapshot(label+'_returned')
        assert not any('/BattleHoverInfo/' in t['path'] for t in after['evaluation']['texts'])
        assert state(c.observation)['audit.costFixture']==prior['audit.costFixture']
        results.append(dict(label=label,path=path,raycast=response['raycastProbes'],visible_phrase=phrase,native_unchanged=True,selection_unchanged=True))
    c.click('Btn_UnitAbility_1');dismiss_hover(c);consumed=c.snapshot('consumed_next_decision')
    cue=next(t for t in consumed['evaluation']['texts'] if '/PhaseTrack/' in t['path'] and '选择一次攻击' in t['fullText'])
    assert cue['fontSize']>=26 and not cue['isTextOverflowing'] and cue['hasVisibleGlyphs']
    assert int(state(c.observation)['block'])==4
    save(args.out/'manifest.json',dict(passed=True,checks=results,captures=c.captures,scope='Two actual EventSystem hover surfaces with raycast and visible guidance; not OS pointer, whole Wiki text or human discovery acceptance.'))


if __name__=='__main__':main()
