"""Check new token coordinates against real public state; no training coverage."""
import json
import os
from collections import Counter
from pathlib import Path
import sys
import numpy as np
from public_entity_tokens import PublicEntityTokens, original_vocabulary
from entity_transformer import EntityTransformerBlock
from task_feasibility import file_hash


def main():
    out = Path(sys.argv[1])
    if out.exists():
        raise ValueError('Preserve token projection evidence')
    out.mkdir(parents=True)
    dll = 'tmp/battle_rl_original_world_bound_consumers_v2/BattleRl.dll'
    os.environ.update(BATTLE_RL_DLL=str(Path(dll).resolve()), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1', BATTLE_RL_SHARED_SKILLS='1', BATTLE_RL_SHAPESHIFT='1', BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    pool = json.loads(Path('AutomationOutputs/BattleRl/20261008_original_world_mixed_pool.json').read_text())
    rules = Rules()
    current = None
    checks = 0
    records = []
    def check(current, layout):
        nonlocal checks
        tokens, mask, identities = layout.pack(np.array([current['features']]))
        numbers = {identity: tokens[0, index, layout.numeric_start:] for index, identity in enumerate(identities[0])}
        state = current['state']  # Independent oracle only, never passed to tokenizer.
        hand = Counter(state['hand'])
        for identity, coordinate, scale in layout.card_counts:
            assert np.isclose(current['features'][coordinate] * scale, hand[identity]), ('hand', identity)
            checks += 1
        skill_ids = {key[0] for key in numbers if key[1] == 'skill'}
        assert skill_ids == set(state['skills'])
        checks += 1
        for identity in skill_ids:
            np.testing.assert_array_equal(numbers[(identity, 'skill', None)][:4], [1.,
                identity in state['usedSkillsTurn'], identity in state['usedSkillsRound'], identity in state['flippedSkills']])
            checks += 1
        for slot, unit in enumerate(state['units']):
            row = numbers[(unit['id'], 'unit', slot)]
            np.testing.assert_allclose(row[:4], [1., unit['ready'], unit['wounds'] / 4., unit['destroyed']])
            checks += 1
        for enemy in state['enemies']:
            row = numbers[(enemy['id'], 'enemy', enemy['index'])]
            np.testing.assert_allclose(row[:3], [1., enemy['defeated'], enemy['armor'] / 20.])
            if state['inBattle']:
                np.testing.assert_allclose(row[41:43], [enemy['effectiveArmor'] / 20., enemy['effectiveFortified']])
            checks += 1
        block = EntityTransformerBlock(layout.width)
        pooled, cache = block.forward(tokens, mask)
        assert np.isfinite(pooled).all()
        _, gradients = block.backward(np.ones_like(pooled), cache)
        assert all(np.isfinite(g).all() for g in gradients.values())
        records.append(dict(hero=state['hero'], phase=state['phase'], entities=int(mask.sum()), original_ids=identities[0]))
    try:
        rows = pool['tasks'] + pool['unknown_tasks']
        for hero in ('Goldyx', 'Tovak', 'Braevalar'):
            row = next(r for r in rows if r['hero'] == hero)
            current = rules.reset_task(row['task']['definition'])
            layout = PublicEntityTokens(original_vocabulary(), rules.info['featureSchema'], len(current['features']))
            check(current, layout)
            fight = next(i for i in current['legal'] if rules.actions[i]['Kind'] == 'fight')
            current = rules.step(fight)
            check(current, layout)
        (out / 'report.json').write_text(json.dumps(dict(status='public_feature_token_projection_checked_not_PPO',
            checks=checks, states=records, dll_sha256=file_hash(dll), token_width=layout.width,
            gaps=['GRU/PPO integration', 'complete entity temporal checkpoint', 'original costs and consumers', 'Unity Console and actual operations']),
            ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps(dict(states=len(records), checks=checks, token_width=layout.width)))
    except Exception as error:
        (out / 'failure.json').write_text(json.dumps(dict(error=repr(error), current=current, states=records),
            ensure_ascii=False, indent=2), encoding='utf-8')
        raise
    finally:
        rules.close()


if __name__ == '__main__':
    main()
