"""Real-pointer checks for selection continuity and rejected double drawing."""
import argparse
import json
from pathlib import Path
import zipfile
from capture_evaluation_cycle import Collector, load_fixtures, compare, save, state


def run(out, live):
    c = Collector(out, live)
    f = next(x for x in load_fixtures() if x['label'] == 'source_play')
    c.prepare(f)
    p = f['provenance']
    with zipfile.ZipFile(p['archive']) as archive:
        trace = json.loads(archive.read(p['member']))['trace']
    pos = p['step']
    action = trace[pos + 1]['action']
    c.click(f"Btn_Card_{action['Card']}_0")
    c.click('Btn_Choice_' + action['Choice'])
    selected = state(c.observation)
    c.snapshot('selected_before_mana')
    c.click('Btn_QuickSource')
    color = ['Red', 'Blue', 'Green', 'White', 'Gold', 'Black'][trace[pos]['action']['Option']]
    index = state(c.observation)['source.faces'].split(',').index(color)
    c.click(f'Btn_Source_{index}')
    c.click('Btn_UseSource')
    c.snapshot('selection_preserved_after_mana')
    current = state(c.observation)
    same_selection = all(selected.get(k) == current.get(k) for k in ['ui.Heading', 'ui.Details', 'ui.cardMode'])
    returned = not any(x['name'] == 'Btn_CloseTools' and x['clickable'] for x in c.observation['controls'])
    _, diffs = compare(trace[pos + 1]['before'], c.observation)
    if not same_selection or not returned or diffs:
        raise AssertionError(dict(same_selection=same_selection, returned=returned, native_differences=diffs))
    c.click('Btn_Play')
    _, consumer_diffs = compare(trace[pos + 2]['before'], c.observation)
    if consumer_diffs:
        raise AssertionError(consumer_diffs)
    c.snapshot('consumer_after_return')
    c.click('Btn_QuickSource')
    dice = [x for x in c.observation['controls'] if x['name'].startswith('Btn_Source_') and x['clickable']]
    if not dice:
        raise AssertionError('Expected remaining source die in preserved fixture')
    c.click(dice[0]['name'])
    before = json.loads(state(c.observation)['audit.costNativeState'])
    rejection_count = int(state(c.observation)['rejections'])
    use = next(x for x in c.observation['controls'] if x['name'] == 'Btn_UseSource')
    if use['clickable']:
        c.click('Btn_UseSource', role='intentional_expected_rejection')
        after = json.loads(state(c.observation)['audit.costNativeState'])
        rejected = int(state(c.observation)['rejections']) == rejection_count + 1
        if before != after or not rejected:
            raise AssertionError(dict(resource_state_preserved=before == after, rejection_count_incremented=rejected))
        rejection = dict(dispatched=True, rejected=True, native_state_unchanged=True,
                         feedback=state(c.observation).get('ui.Feedback'))
    else:
        rejection = dict(dispatched=False, prevented_by_disabled_control=True, control=use)
    c.snapshot('repeat_draw_rejected_or_disabled')
    c.click('Btn_CloseTools')
    c.snapshot('returned_after_rejection')
    result = dict(passed=True, selection_preserved=same_selection, auto_return_verified=returned,
                  source_native_differences=diffs, consumer_native_differences=consumer_diffs,
                  repeat_draw=rejection, captures=c.captures, events=len(c.events))
    save(out / 'validation.json', result)
    return result


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--out', type=Path, required=True)
    ap.add_argument('--live', type=Path, required=True)
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    try:
        r = run(args.out.resolve(), args.live.resolve())
        print(json.dumps({k: v for k, v in r.items() if k != 'captures'}, ensure_ascii=False))
    except Exception as error:
        save(args.out / 'failure.json', dict(error=repr(error)))
        raise
