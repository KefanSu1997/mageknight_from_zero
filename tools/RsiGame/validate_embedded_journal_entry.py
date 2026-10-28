"""Exercise retained full journal and on-demand help after removing duplicate chrome."""
import argparse
import json
from pathlib import Path
from capture_evaluation_cycle import Collector,save
from capture_embedded_asset_cases import dismiss_hover
from structured_ui_ppo import state


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out',type=Path,required=True)
    p.add_argument('--live',type=Path,required=True)
    p.add_argument('--fixture',type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=False)
    c=Collector(args.out.resolve(),args.live.resolve())
    try:
        c.prepare(json.loads(args.fixture.read_text(encoding='utf8'))['fixture'])
        c.click('Btn_Next');c.click('Btn_UnitActor_1');c.click('Btn_UnitAbility_1')
        dismiss_hover(c);r=c.snapshot('after_actual_unit_consumption')
        before=state(c.observation)['audit.costNativeState']
        assert not any(x['name']=='Btn_ResolutionRecall' for x in c.observation['controls'])
        assert not any(x['name']=='HandReadingCue' for x in r['evaluation']['texts'])
        # Test actual button rectangles against the unchanged visible discard pile.
        discard=dict(x=1720,y=850,width=125,height=164)
        overlaps=[]
        for button in c.observation['controls']:
            if not button['clickable']:continue
            x=button['x']-button['width']/2;y=1080-button['y']-button['height']/2
            if min(x+button['width'],discard['x']+discard['width'])>max(x,discard['x']) and min(y+button['height'],discard['y']+discard['height'])>max(y,discard['y']):
                overlaps.append(button)
        assert not overlaps,overlaps
        c.click('Btn_CombatLog');r=c.snapshot('complete_journal_top')
        text=next(x for x in r['evaluation']['texts'] if x['path'].endswith('/ReadingViewport/FullText'))
        assert '操作反馈' in text['fullText'] and '操作后' in text['fullText'] and '格挡' in text['fullText']
        journal=text['fullText']
        c.click('Btn_ReadEnd');r=c.snapshot('complete_journal_end')
        text=next(x for x in r['evaluation']['texts'] if x['path'].endswith('/ReadingViewport/FullText'))
        assert text['fullText']==journal and text['fontSize']>=26
        last=text['glyphs'][-1]['screenRect']
        assert last['y']>=200 and last['y']+last['height']<=882,last
        c.click('Btn_ReturnCombat');dismiss_hover(c)
        assert state(c.observation)['audit.costNativeState']==before
        c.rpc('observe');control=next(x for x in c.observation['controls'] if x['name']=='Btn_GlobalWiki')
        c.rpc('hover',path=control['path'],expectedVersion=c.observation['version'])
        r=c.snapshot('rules_hover_help')
        text=next(x for x in r['evaluation']['texts'] if x['path'].endswith('/BattleHoverInfo/Body'))
        assert '悬浮手牌放大原卡' in text['fullText'] and '左侧' in text['fullText']
        assert not text['isTextOverflowing'] and text['hasVisibleGlyphs'] and text['fontSize']>=21
        c.rpc('unhover',path=control['path'],expectedVersion=c.observation['version'])
        c.click('Btn_GlobalWiki');c.click('Btn_WikiTopic_0');r=c.snapshot('rules_action_fulltext')
        text=next(x for x in r['evaluation']['texts'] if x['path'].endswith('/ReadingViewport/FullText'))
        assert '悬浮手牌放大原卡' in text['fullText'] and '顶部日志' in text['fullText']
        c.click('Btn_ReturnCombat');dismiss_hover(c);c.snapshot('returned_selection_preserved')
        assert state(c.observation)['audit.costNativeState']==before
        save(args.out/'manifest.json',dict(passed=True,captures=c.captures,journal_characters=len(journal),
            journal_last_glyph_rect=last,no_action_buttons_over_discard=True,reading_native_state_unchanged=True,
            scope='Actual button dispatch, full retained journal and last glyph visible through existing end-of-reading control; not physical wheel, human or whole-game acceptance.'))
    except Exception as error:
        save(args.out/'failure.json',dict(error=repr(error),captures=c.captures));raise


if __name__=='__main__':main()
