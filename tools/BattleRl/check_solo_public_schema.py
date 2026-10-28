import argparse
import copy
import json
from pathlib import Path
from solo_public_schema import PublicSchema


def check(directory):
    directory=Path(directory)
    schema=PublicSchema(directory/'public_dto_schema.json')
    unit=json.loads((directory/'native_unit_binding_fixtures.json').read_text(encoding='utf-8'))
    skill=json.loads((directory/'native_skill_binding_fixtures.json').read_text(encoding='utf-8'))
    observations=[unit['observation']]+[row['observation'] for row in skill]
    for observation in observations:schema.validate(observation)
    commands=0
    for row, name in [(unit,'SoloUnitInput')]+[(row,'SoloSkillInput') for row in skill]:
        for command in row['commands']:
            schema.validate(command,'MK.Logic.Runtime.Battles.'+name);commands+=1
    for mutation in ('hidden','missing','bool_as_int'):
        changed=copy.deepcopy(observations[0])
        if mutation=='hidden':changed['Seeds']={'Tiles':1}
        elif mutation=='missing':del changed['World']
        else:changed['Step']=True
        try:schema.validate(changed)
        except ValueError:pass
        else:raise AssertionError('Invalid observation accepted: '+mutation)
    result=dict(passed=True,types=len(schema.document['types']),observations=len(observations),
                commands=commands,schema_sha256=schema.signature,training_started=False)
    (directory/'public_schema_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('directory');args=parser.parse_args();check(args.directory)
