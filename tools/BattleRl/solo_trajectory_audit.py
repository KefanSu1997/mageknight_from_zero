"""Independent score reconciliation for full-scenario archive records.

State digests must cover the entire native Markov state including clock, deck
order, RNG, supplies and pending obligations. They are private audit metadata.
This checker never labels a legal wound-taking action as cheating by itself.
"""
from math import isfinite
import json
import hashlib
from solo_conquest_contract import ScoreState, score


def audit_public_revisits(rows):
    """Triage only: hidden piles/RNG may differ despite identical public state.

    Remove only the top-level decision counter, never round/time/resource fields.
    Preserve every occurrence so trajectory reviewers can inspect the full cycle.
    """
    seen, warnings = {}, []
    total = 0.0
    def key(raw):
        observation = json.loads(raw)
        if not isinstance(observation, dict) or 'Step' not in observation:
            raise ValueError('Expected native public observation with Step')
        observation.pop('Step')
        return hashlib.sha256(json.dumps(observation, sort_keys=True,
            separators=(',', ':'), allow_nan=False).encode()).hexdigest()
    for i, row in enumerate(rows):
        before = key(row['ObservationBefore'])
        seen.setdefault(before, (i, total))
        reward = row['Transition']['Reward']
        if not isfinite(reward):
            raise ValueError('Nonfinite cycle reward')
        total += reward
        after = key(row['ObservationAfter'])
        if after in seen and not row['Transition']['Terminal']:
            start, prior = seen[after]
            warnings.append(dict(kind='public_state_revisit_review', start=start,
                step=i, cycle_steps=i-start+1, cycle_return=total-prior,
                public_state_sha256=after, proves_exploit=False))
        seen.setdefault(after, (i+1, total))
    return warnings


def audit_native_journal(rows):
    """Audit the actual C# v3 journal. Private graph hashes are not cycle keys.

    UnitPoints is already aggregated by native code, so this verifies accounting
    and transition chaining, not the underlying physical unit inventory.
    """
    if not rows:
        raise ValueError('Empty native journal')
    errors = []
    def points(s):
        inv = s['Inventory']
        inventory = (inv['Fame'] + 2*inv['Spells'] + inv['AdvancedActions'] +
                     2*inv['Artifacts'] + inv['Crystals']//2 + inv['UnitPoints'] - 2*inv['DeckWounds'])
        total = (inventory + 2*s['FortifiedSites'] + 2*s['AdventureSites'] + 10*s['Cities'] +
                 (15 if s['Cities'] == 2 else 0) + s['EarlyRounds'] + s['DummyCards'] + s['NoAnnouncement'])
        if inventory != inv['Total'] or total != s['Total']:
            raise ValueError('score component mismatch')
        if not s['Terminal'] and any(s[k] for k in ('EarlyRounds','DummyCards','NoAnnouncement')):
            raise ValueError('time bonus before terminal')
        return total
    total_reward = 0.0
    initial, scale = rows[0]['InitialScore'], rows[0]['RewardScale']
    previous = None
    for i, row in enumerate(rows):
        try:
            transition = row['Transition']
            if row['Version'] != 3 or row['BeforeStep'] != i or transition['Index'] != i+1:
                raise ValueError('decision index/version mismatch')
            for key in ('PrivateStateBefore', 'PrivateStateAfter'):
                digest = row[key]
                if not isinstance(digest, str) or len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
                    raise ValueError('invalid private state digest')
            if row['InitialScore'] != initial or row['RewardScale'] != scale or not isfinite(scale) or scale <= 0:
                raise ValueError('reward contract changed')
            if not 0 <= row['SelectedIndex'] < len(row['CommandsJson']):
                raise ValueError('selected command outside archived support')
            if previous and (previous['PrivateStateAfter'] != row['PrivateStateBefore'] or
                             previous['ObservationAfter'] != row['ObservationBefore'] or
                             previous['Transition']['Score'] != row['ScoreBefore'] or
                             previous['Transition']['Terminal']):
                raise ValueError('broken journal chain or action after terminal')
            before, after = points(row['ScoreBefore']), points(transition['Score'])
            if i == 0 and before != initial:
                raise ValueError('initial score mismatch')
            reward = transition['Reward']
            if not isfinite(reward) or abs(reward-scale*(after-before)) > 1e-8:
                raise ValueError('reward does not match score delta')
            if transition['Terminal'] != transition['Score']['Terminal']:
                raise ValueError('terminal flag mismatch')
            if transition['Terminal']:
                # Independent public-state oracle: either participant can announce.
                # Merely re-summing native score components misses a shared rule bug.
                world=json.loads(row['ObservationAfter'])['World']
                announced=world['PlayerAnnounced'] or world['Dummy']['Announced']
                if transition['Score']['NoAnnouncement'] != (0 if announced else 5):
                    raise ValueError('no-announcement bonus contradicts player/dummy public announcement')
                if transition['Score']['DummyCards'] != world['Dummy']['Remaining']:
                    raise ValueError('dummy-card bonus contradicts public remaining count')
            total_reward += reward
        except (KeyError, TypeError, ValueError) as exc:
            errors.append(dict(step=i, detail=str(exc)))
        previous = row
    if not rows[-1]['Transition']['Terminal']:
        errors.append(dict(detail='scenario not completed'))
    elif abs(total_reward-scale*(rows[-1]['Transition']['Score']['Total']-initial)) > 1e-8:
        errors.append(dict(detail='return does not telescope'))
    try:
        warnings = audit_public_revisits(rows)
    except (KeyError, TypeError, ValueError) as exc:
        errors.append(dict(detail='public revisit audit: '+str(exc)))
        warnings = []
    return dict(passed=not errors, errors=errors, warnings=warnings, steps=len(rows), return_scaled=total_reward,
                full_state_cycle_audited=False, private_digest_chain_checked=True,
                scope='Native accounting and public/private chain; digest contents verified by native replay')


def state(value):
    value = dict(value)
    value['units'] = tuple(tuple(x) for x in value['units'])
    return ScoreState(**value)


def audit_episode(rows, scale=0.01):
    if not rows:
        raise ValueError('Empty trajectory cannot prove a completed scenario')
    errors, warnings = [], []
    total = 0.0
    initial = sum(score(state(rows[0]['before_score'])).values())
    seen = {}
    previous = None
    final_score = None
    for i, row in enumerate(rows):
        before, after = state(row['before_score']), state(row['after_score'])
        before_points = sum(score(before).values())
        ending = row['ending']
        terminal = ending.get('terminal', False)
        reward = row['reward']
        if not isfinite(reward):
            errors.append(dict(step=i, kind='nonfinite_reward'))
            continue
        if previous is not None:
            if row['before_digest'] != previous['after_digest'] or row['before_score'] != previous['after_score']:
                errors.append(dict(step=i, kind='broken_state_chain'))
            if previous['ending'].get('terminal', False):
                errors.append(dict(step=i, kind='action_after_terminal'))
        try:
            components = score(after, **ending)
        except (ValueError, TypeError) as exc:
            errors.append(dict(step=i, kind='invalid_settlement', detail=str(exc)))
            previous = row
            continue
        after_points = sum(components.values())
        expected = scale*(after_points-before_points)
        if abs(reward-expected) > 1e-8:
            errors.append(dict(step=i, kind='reward_mismatch', actual=reward, expected=expected))
        if components != row['score_components']:
            errors.append(dict(step=i, kind='score_component_mismatch'))
        if row['before_digest'] not in seen:
            seen[row['before_digest']] = (i, total)
        total += reward
        if row['after_digest'] in seen and not terminal:
            start, old_return = seen[row['after_digest']]
            cycle_return = total-old_return
            item = dict(step=i, start=start, cycle_return=cycle_return)
            if cycle_return > 1e-8:
                errors.append(dict(kind='positive_reward_same_state_cycle', **item))
            else:
                warnings.append(dict(kind='repeated_full_state_review', **item))
        if terminal:
            final_score = after_points
        previous = row
    if final_score is None:
        # Incomplete/hardware-cut trajectories remain evidence, not score-zero losses.
        errors.append(dict(kind='scenario_not_completed'))
    elif abs(total-scale*(final_score-initial)) > 1e-8:
        errors.append(dict(kind='episode_return_not_final_score_difference'))
    return dict(passed=not errors, errors=errors, warnings=warnings,
                steps=len(rows), return_scaled=total, final_score=final_score,
                scope='Score/chain/cycle audit only; does not prove native rules or hidden-input isolation')
