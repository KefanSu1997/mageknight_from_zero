"""Independent original-skill expectations; no game execution is used to generate them."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def build():
    skills = json.loads((ROOT / 'resources/text_json/skill.json').read_text(encoding='utf-8-sig'))
    cases = []

    def values(**items):
        return [{'key': k, 'value': str(v)} for k, v in items.items()]

    def step(kind, label, option=0, **expected):
        return dict(kind=kind, label=label, option=option, expected=values(**expected))

    def power(index, day, option, crystals=1):
        if index == 0:
            return dict(move=1 if day == 'Day' else 2)
        if index == 1:
            return dict(siege=1, ranged=0, melee=0, element='Physical' if option == 0 else 'Fire')
        if index == 2:
            return dict(melee=2, siege=0, ranged=0, element='Physical' if option == 0 else 'Fire')
        if index == 3:
            return dict(influence=2 if day == 'Day' else 3)
        return dict(redCrystal=crystals, redToken=1 if option == 0 else 0, blackToken=option)

    def add(index, day, title, steps, **kwargs):
        cases.append(dict(id=f'skill_{len(cases):03}', skillIndex=index, day=day, title=title, steps=steps, **kwargs))

    for index in range(5):
        options = (0, 1) if index in (1, 2, 4) else (0,)
        for day in ('Day', 'Night'):
            for option in options:
                expected = power(index, day, option)
                refusal = '该技能本轮已经翻面使用' if index == 4 else '该技能本回合已经使用'
                steps = [step('use', '发动所选技能', option, exception='', used='True', **expected),
                         step('use', '尝试重复发动', option, exception=refusal, used='True', **expected),
                         step('reloadUse', '重载同一技能后尝试发动', option, exception=refusal, used='True', **expected)]
                if index == 0:
                    steps.append(step('move', '移动至平原（费用2）', outcome='True' if day == 'Night' else 'False',
                                      move=0 if day == 'Night' else 1, position=1 if day == 'Night' else 0, exception=''))
                elif index in (1, 2):
                    steps.append(step('attack', '结算原类型攻击', effective=1 if index == 1 else 2,
                                      kills=1, fame=2, siege=0, melee=0, ranged=0, exception=''))
                steps.append(step('nextTurn', '结束回合并开始下一回合', used='True' if index == 4 else 'False',
                                  move=0, influence=0, melee=0, siege=0, redToken=0, blackToken=0, exception=''))
                if index == 4:
                    steps.append(step('use', '下一回合尝试翻面技能', option, exception=refusal, used='True', redCrystal=1, redToken=0, blackToken=0))
                else:
                    steps.append(step('use', '下一回合重新发动', option, exception='', used='True', **expected))
                steps.append(step('nextRound', '进入下一轮恢复标记', used='False', exception=''))
                steps.append(step('use', '下一轮发动技能', option, exception='', used='True', **power(index, day, option, 2)))
                add(index, day, f'{"白昼" if day == "Day" else "黑夜"}·选项{option}·完整次数生命周期', steps)

        for invalid in (-1, max(options) + 1):
            add(index, 'Day', f'非法选项{invalid}不产生效果且不消耗次数', [
                step('use', '尝试非法选项', invalid, exception='技能选项无效', used='False', move=0, influence=0, siege=0, melee=0, redCrystal=0, redToken=0, blackToken=0),
                step('use', '随后发动合法选项', exception='', used='True', **power(index, 'Day', 0))])
        add(index, 'Day', '未持有的技能不能发动', [step('use', '尝试未持有技能', exception='只能使用玩家持有的技能', used='False',
                                                     move=0, influence=0, melee=0, siege=0, redCrystal=0, redToken=0, blackToken=0)], unowned=True)

    for index in (1, 2):
        for option in (0, 1):
            for resistance in ('PhysicalResist', 'FireResist'):
                # Resistance halves matching power (floor). Fire bypasses physical resistance.
                printed = 1 if index == 1 else 2
                effective = printed // 2 if resistance == ('PhysicalResist' if option == 0 else 'FireResist') else printed
                kills = int(effective >= printed)
                add(index, 'Day', f'选项{option}·{resistance}·实际战斗折算', [
                    step('use', '发动所选攻击', option, exception='', **power(index, 'Day', option)),
                    step('attack', '对抗有抗性的目标', effective=effective, kills=kills, fame=kills * 2, siege=0, melee=0, exception='')], resistance=resistance)

    suite = dict(cases=cases)
    suite_path = ROOT / 'Assets/Resources/CardVerification/skill_cases.json'
    write(suite_path, suite)
    digest = hashlib.sha256(suite_path.read_bytes()).hexdigest()
    config = dict(scenePath='Assets/Scenes/Verification/hero_skills.unity', useRunSubfolder=False, captureInitialView=True,
                  initialDelaySeconds=1.5, defaultWaitAfterSeconds=0.05, maxRunSeconds=900, steps=[])

    def checks(items):
        return [dict(key=k, expected=str(v), rule='原始技能文本及独立预期；通过实际按钮操作核对') for k, v in items.items()]

    for i, case in enumerate(cases):
        if i:
            config['steps'].append(dict(label=case['id'] + '_prepare', buttonPath='Canvas/UIRoot/Btn_Next', skipScreenshot=True,
                                        after=checks(dict(caseId=case['id'], step=0))))
        config['steps'].append(dict(label=case['id'] + '_select', buttonPath='Canvas/UIRoot/Btn_Select', skipScreenshot=True,
                                    after=checks(dict(selected='True', art='skill_000', suiteSha256=digest, sourceId=f"hero_skill_pool_000:{case['skillIndex']:02}"))))
        for j, action in enumerate(case['steps']):
            expected = {v['key']: v['value'] for v in action['expected']}
            config['steps'].append(dict(label=f"{case['id']}_{j:02}_{action['kind']}", buttonPath='Canvas/UIRoot/Btn_Execute',
                                        before=checks(dict(caseId=case['id'], step=j)), after=checks(dict(step=j + 1, **expected))))
    write(ROOT / 'AutomationConfigs/AllOriginalCards/hero_skills.json', config)

    # Keep all remaining unit/skill records visible, including missing art and compound timings.
    unit_records = []
    for name in ('army', 'army_2'):
        data = json.loads((ROOT / f'resources/text_json/{name}.json').read_text(encoding='utf-8-sig'))
        for item in data:
            status = 'back_artwork_not_playable' if item['id'] in ('army_015', 'army_2_015') else (
                'printed_card_missing_json_rules' if item['id'] == 'army_003' else 'pending_scene_verification')
            unit_records.append(dict(id=item['id'], name=item.get('card_name_cn', ''),
                                     artworkExists=(ROOT / f"Assets/GameData/cards/{item['id']}.png").exists(), status=status))
    inventory = dict(unitRecords=unit_records, heroPools=len(skills),
                     skillRecords=[dict(sourceId=f"{pool['id']}:{i:02}", name=item['skill_name'],
                                        status='first_five_scene_cases_added' if pool['id'] == 'hero_skill_pool_000' and i < 5 else 'pending_scene_verification')
                                   for pool in skills for i, item in enumerate(pool['skills'])],
                     sceneCases=len(cases), scope='独立于既有125条行动/法术/宝物记录；新增场景案例不代表整张技能已全面验收。')
    write(ROOT / 'vibe_coding/codex/hero_unit_inventory_2026-09-10.json', inventory)
    print(json.dumps(dict(cases=len(cases), operations=len(config['steps']), units=len(unit_records),
                          skills=len(inventory['skillRecords']), pools=len(skills)), ensure_ascii=False))


if __name__ == '__main__':
    build()
