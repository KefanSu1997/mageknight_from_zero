"""Read existing finite acceptance evidence; never execute rules or Unity.

Only reports explicitly referenced by the accepted-card index/audits are opened.
Each source is read once. Historical failures and missing evidence remain visible.
This ledger is not a current-code full regression or a whole-demo completion claim.
"""
import json
import hashlib
from pathlib import Path
from collections import Counter

ROOT = Path(__file__).resolve().parents[1]
RECORDS = ROOT / 'vibe_coding/codex'
cache = {}


def accepted_checkpoint(run, case_id):
    """Use only explicitly audited successful prefixes of an interrupted run."""
    audit_path = run + '/checkpoint_audit.json'
    audit = read(audit_path)
    if not audit or case_id in audit.get('excludedCaseIds', []):
        return None, None
    entries = audit.get('cases', [audit] if audit.get('caseId') == case_id else [])
    entry = next((e for e in entries if e.get('id', e.get('caseId')) == case_id), None)
    if not entry:
        return None, None
    paths = [Path(entry['checkpoint'])] if entry.get('checkpoint') else sorted((ROOT / run / 'checkpoints').glob('*.json'))
    for path in paths:
        if not path.is_absolute():
            path = ROOT / path
        effect = read(path)
        if not effect or effect.get('id') != case_id:
            continue
        expected_hash = entry.get('sha256', entry.get('checkpointSha256'))
        if not expected_hash or hashlib.sha256(path.read_bytes()).hexdigest().lower() != expected_hash.lower():
            continue
        return effect, {'effectSource': path.relative_to(ROOT).as_posix(), 'checkpointAudit': audit_path}
    return None, None


def read(path):
    path = Path(path)
    if not path.is_absolute():
        path = ROOT / path
    if path not in cache:
        cache[path] = json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else None
    return cache[path]


def main():
    accepted = read(RECORDS / 'basic_card_acceptance_index.json')
    historical = read(RECORDS / 'card_scene_evidence_index.json')
    definitions = {c['id']: c for c in read('Assets/Resources/CardVerification/cases.json')['cases']}
    rows = []
    for card in accepted['cards']:
        cid = card['cardId']
        if cid.startswith('items_'):
            continue
        audit = read(card['audit'])
        original = next(c for c in audit['cards'] if c['cardId'] == cid)
        extra = card.get('newEvidence', {})
        basic = card.get('basicEvidence', {})
        case_ids = list(dict.fromkeys(original.get('selectedCaseIds', []) + extra.get('caseIds', []) + basic.get('sceneCases', [])))
        cases = []
        for case_id in case_ids:
            candidates = []
            for run in reversed(extra.get('runs', [])):
                candidates.append(run if run.startswith('AutomationOutputs/') else 'AutomationOutputs/AllOriginalCards/' + run)
            candidates += [str(Path(p['path']).parent).replace('\\', '/') for p in basic.get('proofs', [])
                           if p.get('path', '').endswith('.json') and 'AllOriginalCards' in p['path']]
            ae = audit.get('evidence', {})
            if isinstance(ae, dict) and case_id in ae:
                candidates.append(ae[case_id].get('run'))
            candidates += [e.get('run') for e in original.get('evidence', []) if e.get('caseId') == case_id]
            if case_id in historical['cases']:
                candidates.append(historical['cases'][case_id].get('run'))
            rejected = []
            selected = None
            current = definitions.get(case_id, {})
            for run in dict.fromkeys(r for r in candidates if r):
                effects = read(run + '/effects.json')
                provenance = {'effectSource': run + '/effects.json'}
                if effects is None:
                    effect, checkpoint_provenance = accepted_checkpoint(run, case_id)
                    if effect is None:
                        rejected.append({'run': run, 'reason': 'effects_missing_or_no_audited_checkpoint'})
                        continue
                    provenance = checkpoint_provenance
                else:
                    effect = next((c for c in effects.get('cases', []) if c['id'] == case_id), None)
                if effect is None:
                    continue
                report = read(run + '/report.json')
                steps = [(i, s) for i, s in enumerate((report or {}).get('steps', []))
                         if s.get('label', '').startswith(case_id + '_')]
                checks = effect.get('checks', [])
                # The accepted finite-scope audits deliberately retain older "partial"
                # labels for untested extended combinations. Verify their actual checks,
                # while keeping the original status and limitations verbatim below.
                valid = effect.get('status') in ('passed', 'partial') and checks and all(c.get('passed') for c in checks)
                evidence_card = current.get('cardId', cid)
                valid = valid and effect.get('artwork') == evidence_card and steps and all(s.get('success') for _, s in steps)
                if not valid:
                    rejected.append({'run': run, 'reason': 'not_successful_effect_and_ui',
                                     'effectStatus': effect.get('status'), 'uiSteps': len(steps)})
                    continue
                actual_checks = {c['key']: str(c['expected']) for c in checks}
                mismatch = [e['key'] for e in current.get('expected', [])
                            if actual_checks.get(e['key']) != str(e['value'])]
                selected = dict(caseId=case_id, mode='enhanced' if current.get('enhanced') else 'base',
                                title=effect.get('title'), run=run, effectStatus=effect['status'],
                                evidenceCardId=evidence_card, sharedGuardOrConsumer=evidence_card != cid,
                                checkCount=len(checks), uiStepIndices=[i for i, _ in steps],
                                executionPath=effect.get('executionPath'), originalArtwork=effect['artwork'],
                                scopeAuthority='selected by existing finite basic audit, not a new full-card pass',
                                currentExpectedMismatchKeys=mismatch,
                                expectedRejection=bool(effect.get('expectedException')),
                                historicalLimitations=effect.get('limitations', []))
                selected.update(provenance)
                break
            if selected is None:
                selected = {'caseId': case_id, 'state': 'evidence_mapping_needed', 'attempts': rejected}
            cases.append(selected)
        gaps = []
        for mode in ('base', 'enhanced'):
            if not any(e.get('mode') == mode and not e.get('expectedRejection') and not e.get('sharedGuardOrConsumer')
                       and not e.get('currentExpectedMismatchKeys') for e in cases):
                gaps.append(mode + ': positive historical mode evidence not resolved')
        unresolved = [e['caseId'] for e in cases if e.get('state') == 'evidence_mapping_needed']
        mismatches = [e['caseId'] for e in cases if e.get('currentExpectedMismatchKeys')]
        if mismatches and gaps:
            gaps.append('changed expected values need dependency review: ' + ','.join(mismatches))
        if cid == 'magic_016':
            gaps.append('actual original seven-hex tile exploration consumer remains unverified; movement is separate')
        source = 'resources/text_json/' + ('basic_card' if cid.startswith('basic_') else 'advanced_card' if cid.startswith('advanced_') else 'magic') + '.json'
        rows.append(dict(cardId=cid, name=card['name'], originalDefinition='Assets/GameData/CardsAssets/' + cid + '.asset',
                         originalArt='Assets/GameData/cards/' + cid + '.png', originalRules=source,
                         sourcesPresent=all((ROOT / p).exists() for p in [source, 'Assets/GameData/CardsAssets/' + cid + '.asset', 'Assets/GameData/cards/' + cid + '.png']),
                         historicalBasicAccepted=card['basicAccepted'], audit=card['audit'],
                         historicalEvidenceState='verified_selected_effect_and_ui_records' if not gaps else 'needs_scope_or_dependency_review',
                         currentBackend='reuse referenced finite acceptance; not reexecuted or inferred from scene',
                         currentUiSources=['Assets/Scripts/Battle/PveBattleController.Deeds.cs', 'Assets/Scripts/Battle/PveBattleController.DeedInputs.cs',
                                           'Assets/Logic/Runtime/Battles/PveDeedInputs.cs'],
                         currentIntegrationState='shared original-card entry exists; individual input/consumer overlays must also be read',
                         basicRules=original.get('printedEffects') or [original.get('basePrintedRule'), original.get('enhancedPrintedRule')],
                         cases=cases, unresolvedSelectedLinks=unresolved, historicalExpectedDrift=mismatches,
                         subsequentEvidence=extra, finiteBasicEvidence=basic,
                         actualConsumerEvidence=original.get('additionalSceneEvidence', []), remaining=gaps))
    result = dict(scope='96 original action/advanced/spell historical primary modes and actual selected evidence; not whole-demo certification',
                  total=len(rows), categoryCounts=dict(Counter(r['cardId'].rsplit('_', 1)[0] for r in rows)),
                  rowsWithReview=sum(bool(r['remaining']) for r in rows), reportsReadOnce=len(cache),
                  completion=False, cards=rows)
    target = RECORDS / 'deed_primary_evidence_ledger_2026-09-29.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'cards'}, ensure_ascii=False))
    for row in rows:
        if row['remaining']:
            print(row['cardId'], '; '.join(row['remaining']))


if __name__ == '__main__':
    main()
