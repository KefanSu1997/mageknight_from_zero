import argparse
import json
from pathlib import Path
import numpy as np
from solo_text_assets import SoloTextAssets
from solo_candidate_entities import combat_entities, unit_entities, skill_entities, lifecycle_entities, reward_entities, interaction_entities
from solo_candidate_entities import equipment_entities
from solo_candidate_entities import copied_effect_entities


def check(directory, evidence):
    assets = SoloTextAssets(directory, Path(__file__).resolve().parents[2])
    counts = dict(candidates=0, references=0, wound_references=0)
    by_kind = {}
    committed_cleanup_candidates = 0
    for name, kind in [('combat', None), ('unit', 'Unit'), ('skill', 'Skill'), ('lifecycle', 'Lifecycle'), ('reward', 'Reward'), ('equipment', 'Equipment')]:
        document = json.loads((Path(evidence)/f'native_{name}_binding_fixtures.json').read_text(encoding='utf-8'))
        rows = [dict(document, kind=kind)] if kind == 'Unit' else document
        for row in rows:
            for command in row['commands']:
                bindings = (unit_entities(row['observation'], command) if kind == 'Unit' else
                    skill_entities(row['observation'], command) if kind == 'Skill' else
                    reward_entities(row['observation'], row['kind'], command) if row.get('kind') in ('EndTurn','LevelReward','CityArtifactReward') else
                    interaction_entities(row['observation'], row['kind'], command) if row.get('kind') in ('Site','TacticInput','TacticAbility') else
                    equipment_entities(row['observation'], row['kind'], command) if row.get('kind') in ('Artifact','Banner','Shapeshift') else
                    copied_effect_entities(row['observation'], row['kind'], command) if row.get('kind') in ('MaterialArtifact','Repeated') else
                    lifecycle_entities(row['observation'], row['kind'], command) if kind == 'Lifecycle' else
                    combat_entities(row['observation'], row['kind'], command))
                encoded = assets.encode_bindings(bindings)
                assert len(encoded) == len(bindings)
                for original, output in zip(bindings, encoded):
                    assert original['parameter'] == output['parameter']
                    assert output['vectors'].shape == (len(output['identities']),384)
                    assert not output['vectors'].flags.writeable
                    assert np.isfinite(output['vectors']).all()
                    counts['references'] += len(output['identities'])
                    counts['wound_references'] += output['identities'].count('w')
                counts['candidates'] += 1
                operation = row.get('kind') or kind
                by_kind[operation] = by_kind.get(operation,0)+1
                if operation == 'EndTurn' and row['observation']['Offers']['CommittedRewardEnd'] is not None:
                    committed_cleanup_candidates += 1
    try:
        assets.get('advanced_card_044')
    except ValueError:
        pass
    else:
        raise AssertionError('Unbound placeholder must not receive a rule vector')
    assert counts['wound_references'] > 0, 'Native wound fixtures must reach the text branch'
    return dict(passed=True, **counts, entities=len(assets.index),
        candidate_kinds=by_kind, committed_cleanup_candidates=committed_cleanup_candidates,
        catalog_signature=assets.catalog.signature, assets_directory=str(Path(directory).resolve()),
        parameter_sha256=assets.manifest['parameter_sha256'],
        scope='frozen vector lookup on controlled native fixtures; neural policy not yet integrated',
        training_started=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--assets', required=True)
    parser.add_argument('--evidence', required=True)
    parser.add_argument('--out')
    args = parser.parse_args()
    result = check(args.assets, args.evidence)
    target = Path(args.out) if args.out else Path(args.evidence)/'solo_text_assets_gate.json'
    target.write_text(json.dumps(result,indent=2), encoding='utf-8')
    print(json.dumps(result))
