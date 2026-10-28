"""Resolve source-proven unavailable routes and one archive-layout projection.

No Unity calls, new gameplay simulation, original database mutation, or zero
imputation. Model projections remain distinct from pointer-executed evidence.
"""
import argparse,hashlib,json,sqlite3,zipfile
from pathlib import Path
from iterate_ui_cost_design import cost
from structured_ui_ppo import ROOT,write_json

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    p=argparse.ArgumentParser();p.add_argument('--out',type=Path,required=True);a=p.parse_args();out=a.out.resolve()
    work=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_full_cost_workset/workset.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    selected=sqlite3.connect('file:'+str(ROOT/'AutomationOutputs/UiFluency/20261012_sample5_selected/selected.sqlite').replace('\\','/')+'?mode=ro',uri=True)
    rows=list(map(json.loads,(out/'uncovered_routes.jsonl').open(encoding='utf8')))
    journey=ROOT/'Assets/Scripts/Battle/PveBattleController.Journey.cs';controller=ROOT/'Assets/Scripts/Battle/PveBattleController.cs';adapter=ROOT/'Assets/Scripts/Battle/CostPrefixAdapter.cs'
    text=journey.read_text(encoding='utf8');assert 'if(_journey.Game.Map.OriginalCells.Count>0)DrawOriginalJourneyOverview();' in text
    assert 'Button(_root,"Btn_JourneyEncounter_"+i' in text
    assert 'case "fight":Journey.BeginBattle(PendingEnemies);break;' in adapter.read_text(encoding='utf8')
    observed=json.loads((out/'offline_supplement_03/planned_routes.json').read_text(encoding='utf8'))['results']
    assert {r['version'] for r in observed if r['key'][1]==15299 and r['status']=='unavailable_current_UI' and r['archived_before_after_equal']}=={'baseline','candidate'}
    geometries=[c for f in (out/'candidate').glob('*.json') for c in json.loads(f.read_text(encoding='utf8'))['observation']['controls']]
    def validate(name,x,y,w,h):
        assert any(c['name'].startswith(name) and max(abs(x-c['x']),abs(y-c['y']),abs(w-c['width']),abs(h-c['height']))<.1 for c in geometries),(name,x,y,w,h)
    def point(name,path,x,y,w,h,intent):return dict(name=name,path=path,x=x,y=y,w=w,h=h,intent=intent,before='Battle.Hand',after='Battle.Hand')
    documents={}
    def trace_for(r):
        eid=r['key'][1]
        if eid not in documents:
            path,volume,h=work.execute('select path,volume,sha256 from episodes where id=?',(eid,)).fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==h==r['episode_source_sha256'];documents[eid]=json.loads(raw)['trace']
        return documents[eid]
    defense=ROOT/'Assets/Scripts/Battle/PveBattleController.EnemyDefend.cs';defense_text=defense.read_text(encoding='utf8')
    assert '"EnemyDefend", 0, 0, 1920, 1080' in defense_text and 'raycastTarget = true' in defense_text
    assert 'Button(panel, "Btn_Cancel"' in defense_text and '_battle.ClearDefendChoices();' in defense_text
    assert '"Btn_Confirm", "确认分配并结算攻击"' in defense_text
    for r in rows:
        reset=json.loads(work.execute('select reset_json from episodes where id=?',(r['key'][1],)).fetchone()[0]);task=reset.get('task')
        if r['status']=='verified_incompatible':
            evidence=selected.execute('select category,result_sha,source_path from incompatibilities where group_id=? and episode=? and a_step=?',r['key']).fetchone();assert evidence
            r.update(resolution='verified_unavailable_in_baseline',reason=evidence[0],original_evidence_sha256=evidence[1],original_evidence_path=evidence[2]);continue
        if task and task.get('OriginalWorld') and 'fight' in (r['A'],r['B']):
            r.update(resolution='source_proven_unavailable_both_profiles',reason='Original-world overview replaces off-map declared-task encounter entry; map combat has different physical enemy/location semantics.',source_evidence=dict(journey_sha256=sha(journey),adapter_sha256=sha(adapter),observed_anchor='offline_supplement_03/planned_routes.json'));continue
        t=trace_for(r);pos=r['key'][2]
        blocked=next((intent for intent,kind,s in [('A',r['A'],t[pos]['before']),('B',r['B'],t[pos+1]['before'])] if s.get('defenderSelected') and kind not in ('defend','clearDefend','attack')),None)
        if blocked:
            r.update(resolution='source_proven_unavailable_both_profiles',reason=f'{blocked}: pending enemy-defense preview shields the main UI; cancel clears selected defenses and confirm consumes an attack. Requested interleaved action preserves the pending plan, so neither exit is equivalent.',source_evidence=dict(defense_ui_sha256=sha(defense),controller_sha256=sha(controller),native_archive_sha256=r['episode_source_sha256']));continue
        if r['B']=='unit' and r['action_B']['Card']=='army_2_004' and r['action_B']['Choice']=='2' and t[pos+1]['before']['phase'] in ('Ranged','AssignDamage'):
            example=selected.execute("select result_sha,source_path from incompatibilities where category='unit_movement_influence_phase_scope_mismatch' limit 1").fetchone();assert example
            r.update(resolution='source_proven_unavailable_both_profiles',reason='army_2_004 ability2 is MovementOrInfluence; combat utility filters only Influence and the general combat filter excludes MovementOrInfluence. Same missing ability family as independently verified5931/5.',source_evidence=dict(controller_sha256=sha(controller),verified_family_result_sha256=example[0],verified_family_path=example[1],native_archive_sha256=r['episode_source_sha256']));continue
        if r['key'][1:]==[233,54]:
            path,volume,expected_sha=work.execute('select path,volume,sha256 from episodes where id=233').fetchone()
            with zipfile.ZipFile('H:/MageKnight_RL_Backups/20261012_before_reward_v2/'+volume) as z:raw=z.read(path)
            assert hashlib.sha256(raw).hexdigest()==expected_sha==r['episode_source_sha256'];t=json.loads(raw)['trace'];before=t[54]['before'];after=t[55]['before']
            assert before['phase']==after['phase']=='Block' and before['hand']==['basic_card_022','basic_card_021'] and after['hand']==['basic_card_022']
            assert not before['nextEnhanced'] and not after['nextEnhanced']
            # DrawHand uses min(190,1300/min(7,n)); n=2 and n=1 both
            # yield190. Slot centers126/316; original Inspector is1390,206.
            validate('Btn_Card_',126,185.5,180,267);validate('Btn_Sideways',1749,401,216,52);validate('Btn_Play',1522,401,214,52)
            steps=[point('Btn_Card_basic_card_021_0','Canvas/BattleTable/Hand/Btn_Card_basic_card_021_0',316,185.5,180,267,'A'),point('Btn_Sideways','Canvas/BattleTable/Inspector/Btn_Sideways',1749,401,216,52,'A'),point('Btn_Card_basic_card_022_0','Canvas/BattleTable/Hand/Btn_Card_basic_card_022_0',126,185.5,180,267,'B'),point('Btn_Play','Canvas/BattleTable/Inspector/Btn_Play',1522,401,214,52,'B')]
            r.update(resolution='offline_geometry_projected',baseline_cost=cost(steps),candidate_cost=cost(steps),steps=steps,source_evidence=dict(controller_sha256=sha(controller),archive_sha256=expected_sha),assumptions=['Canonical neutral UI entry on hand page zero, matching sampled protocol.','A sideways clears selected card/choice; B base is the first native choice.','No gameplay pointer execution or exact legacy reset claimed.']);continue
        if r['key'][1:]==[233,51]:
            before=t[51]['before'];after=t[52]['before'];assert before['phase']==after['phase']=='Block'
            assert before['hand']==after['hand']==['basic_card_014','basic_card_022','basic_card_016','basic_card_021']
            assert len(before['enemies'])==1 and len(before['enemies'][0]['attacks'])==1 and before['enemies'][0]['defend']==0
            assert len(before['blocks'])==2 and not after['blocks'] and after['enemies'][0]['attacks'][0]['blocked']
            assert all(b['element']=='Physical' and b['distinctGroup']==0 for b in before['blocks'])
            validate('Btn_Target',898.5,721.5,284,241);validate('Btn_Resolve',1636,401,442,52)
            steps=[point('Btn_Target','Canvas/BattleTable/Enemy_0/Btn_Target',898.5,721.5,284,241,'A'),point('Btn_Resolve','Canvas/BattleTable/Inspector/Btn_Resolve',1636,401,442,52,'A'),point('Btn_Card_basic_card_016_0','Canvas/BattleTable/Hand/Btn_Card_basic_card_016_0',506,185.5,180,267,'B'),point('Btn_Sideways','Canvas/BattleTable/Inspector/Btn_Sideways',1749,401,216,52,'B')]
            r.update(resolution='offline_geometry_projected',baseline_cost=cost(steps),candidate_cost=cost(steps),steps=steps,source_evidence=dict(controller_sha256=sha(controller),archive_sha256=r['episode_source_sha256']),assumptions=['Canonical neutral page-zero UI, no reserved block contributions.','The sole physical attack uses both visible physical block sources, matching the archived consumed list.','No gameplay pointer execution or exact legacy reset claimed.']);continue
        r['resolution']='unresolved_not_imputed'
    counts={};weights={}
    for r in rows:
        tag=r['resolution'];counts[tag]=counts.get(tag,0)+1;weights[tag]=weights.get(tag,0)+r['population_weight']
    assert abs(sum(weights.values())-15073.75)<.01
    write_json(out/'uncovered_offline_resolutions.json',dict(rows=rows,counts=counts,weights=weights,scope='Availability and offline projections, separate from3833 accepted pointer-evidence routes. Unavailable completion has no finite numeric cost; unresolved stays null.'))
    write_json(out/'uncovered_resolution_summary.json',dict(counts=counts,weights=weights,unresolved_population_pct=weights.get('unresolved_not_imputed',0)/850374*100))
    print(json.dumps(dict(counts=counts,weights=weights),ensure_ascii=False))
if __name__=='__main__':main()
