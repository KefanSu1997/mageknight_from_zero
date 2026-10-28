"""Balance certified tasks using actual PPO episodes, never witness actions.

Mode events require a committed removal from the hand. This is invocation
evidence only: detailed fees, materials and downstream consumption stay gaps.
"""
from collections import Counter


def desired_modes(certificate):
    cards = certificate['task'].get('definition', {}).get('Build', {}).get('AdditionalCards', [])
    return {f'card_mode:{card}:{mode}' for card in cards
            for mode in (('normal', 'burn') if card.startswith('items_') else ('base', 'strong'))}


def actual_events(trace):
    for step, row in enumerate(trace):
        before, after, action = row['before'], row['after'], row['action']
        kind, card = action['Kind'], action['Card']
        if kind in ('play', 'artifact') and card and before['hand'].count(card) > after['hand'].count(card):
            mode = action['Choice'].split(':')[0]
            # Preserve original mode spelling; no counting options/selection as use.
            yield step, f'card_mode:{card}:{mode}'
        if kind=='skillShapeshift':
            physical=before['handOrder'][action['Parts']]
            host=next((c for c in before['zones']['hand'] if c['instance']==physical),None)
            if host and any(c['instance']==physical for c in after['zones']['played']):
                mode='strong' if action['Choice']=='strong' or before['nextEnhanced'] else 'base'
                yield step, f'card_mode:{host["id"]}:shapeshift_{mode}'
        if kind == 'unit':
            old = next((u for u in before['units'] if u['id']==card), None)
            new = next((u for u in after['units'] if u['id']==card), None)
            # Delphana Masters may commit another distinct paid ability while
            # already exhausted. Selection/planning is not an activation.
            used_now = set(new.get('usedAbilities', [])) - set(old.get('usedAbilities', [])) if old and new else set()
            exact_new_ability = str(action['Choice']).isdigit() and int(action['Choice']) in used_now
            if old and new and ((old['ready'] and not new['ready']) or exact_new_ability):
                yield step, f'unit_invocation:{card}:{action["Choice"]}:{action["Option"]}'
        if kind in ('skill', 'skillSideways', 'skillFork', 'skillBullseye',
                    'puppetKeep', 'puppetUse', 'sharedUse', 'skillShapeshift', 'worldFly'):
            def markers(state):
                return set(state['usedSkillsTurn']) | set(state['usedSkillsRound']) | set(state['flippedSkills'])
            if card in markers(after)-markers(before):
                yield step, f'skill_invocation:{card}:{action["Option"]}'
        if (kind == 'unitLeadership'
                and 'hero_skill_pool_002:06' not in before['usedSkillsTurn']
                and 'hero_skill_pool_002:06' in after['usedSkillsTurn']):
            yield step, f'skill_invocation:hero_skill_pool_002:06:{action["Option"]}'
        payment = before.get('paymentSelection')
        if (kind in ('play', 'unit') and payment
                and after.get('paymentSelection') is None
                and payment['skill'] not in before['usedSkillsTurn']
                and payment['skill'] in after['usedSkillsTurn']):
            yield step, f'skill_invocation:{payment["skill"]}:{action["Option"]}'
        shared=before.get('sharedSkills')
        if (shared and shared.get('enhancementColor') is not None
                and kind in ('play','unit','artifact','repeat')
                and 'hero_skill_pool_003:09' not in before['usedSkillsRound']
                and 'hero_skill_pool_003:09' in after['usedSkillsRound']):
            yield step, 'skill_invocation:hero_skill_pool_003:09:0'
        if kind == 'fight' and not before['inBattle'] and after['inBattle']:
            for enemy in after['enemies']:
                yield step, f'hero_enemy_encounter:{after["hero"]}:{enemy["id"]}'
        if kind == 'worldRecruit' and before.get('world', {}).get('loyaltySelected'):
            old_ids = {unit['id'] for unit in before['units']}
            new_ids = {unit['id'] for unit in after['units']}
            if (card not in old_ids and card in new_ids
                    and after.get('world', {}).get('loyaltyBound') == card
                    and not after['world']['loyaltyAvailable']):
                yield step, 'skill_consumer:hero_skill_pool_002:07:loyalty_recruit'
        if kind in ('nextTurn', 'roundFinish'):
            if after['turns'] > before['turns']:
                yield step, 'formal_next_turn'
            if after['dayPart'] != before['dayPart']:
                yield step, f'day_transition:{before["dayPart"]}:{after["dayPart"]}'


class CertifiedTaskSampler:
    def __init__(self, certificates, state=None):
        self.certificates = certificates
        self.by_id = {c['task_id']: c for c in certificates}
        if len(self.by_id) != len(certificates):
            raise ValueError('Duplicate certified tasks')
        self.attempts, self.events = Counter(), Counter()
        self.first_evidence = {}
        if state is not None:
            if state.get('schema_version') != 1 or state.get('task_ids') != sorted(self.by_id):
                raise ValueError('Sampler checkpoint task pool differs')
            self.attempts.update(state['attempts'])
            self.events.update(state['events'])
            self.first_evidence.update(state['first_evidence'])
            if any(k not in self.by_id or v < 0 for k,v in self.attempts.items()):
                raise ValueError('Invalid task attempt history')

    def select(self, random):
        hero_counts = Counter()
        for task_id, attempts in self.attempts.items():
            hero_counts[self.by_id[task_id]['hero']] += attempts
        def priority(c):
            missing = sum(self.events[key] == 0 for key in desired_modes(c))
            return hero_counts[c['hero']], self.attempts[c['task_id']], -missing
        best = min(priority(c) for c in self.certificates)
        choices = [c for c in self.certificates if priority(c)==best]
        result = choices[int(random.integers(len(choices)))]
        self.attempts[result['task_id']] += 1
        return result

    def observe(self, episode, episode_index):
        for step, key in actual_events(episode['trace']):
            self.events[key] += 1
            self.first_evidence.setdefault(key, dict(episode=episode_index, step=step,
                                                     task_id=episode['task_id']))

    def state(self):
        return dict(schema_version=1, task_ids=sorted(self.by_id), attempts=dict(self.attempts),
                    events=dict(self.events), first_evidence=self.first_evidence,
                    scope='Actual PPO invocation/encounter events, not full fees/materials/consumers acceptance')
