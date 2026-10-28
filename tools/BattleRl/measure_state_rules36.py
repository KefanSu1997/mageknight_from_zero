"""Measure the actual review state plus verbatim source rules with mmBERT tokenizer."""
import sys,json
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
from joint_option_text import tokenizer
p=Path('optimization_stage_20261014')
example=json.loads((p/'green_training_example36.json').read_text())
state=(p/'semantic_state_review36.txt').read_text()
rules=example['original_document']['rules']
rows=rules.splitlines()
groups={
 'hand_rules':'\n'.join(x for x in rows if x.startswith('basic_card_')),
 'unit_rules':'\n'.join(x for x in rows if x.startswith('army_')),
 'enemy_rules':'\n'.join(x for x in rows if x.startswith('monster_')),
}
assert sum(len(v.splitlines()) for v in groups.values())==len(rows)==9
tok=tokenizer()
def measure(text):return dict(characters=len(text),tokens=len(tok.encode(text,add_special_tokens=False)))
expanded=state+'\n\n手牌原始说明：\n'+groups['hand_rules']+'\n\n单位原始说明：\n'+groups['unit_rules']+'\n\n敌人原始说明：\n'+groups['enemy_rules']
# Actual current card-selection node, with rules referenced instead of duplicated.
options=['使用敏捷（basic_card_007）','使用威胁（basic_card_022）','使用本能（basic_card_027）']
full=expanded+'\n\n已锁定操作：出牌。\n从以下合法选项中选择一项；选项对应的原规则已列在上方。\n'+'\n'.join(f'选项{i+1:03d}：{x}' for i,x in enumerate(options))+'\n请对以上编号选项作出决策。'
result=dict(tokenizer_revision='abc32620dd4f6ab06f5fbe905dc25f310618e09f',state=measure(state),**{k:measure(v) for k,v in groups.items()},expanded_state=measure(expanded),complete_card_selection_prompt=measure(full),complete_with_bos_eos=len(tok.encode(full,add_special_tokens=False))+2,scope='Actual context36 state and verbatim existing source-rule glossary. Group counts separate; assembled totals measured directly. Full prompt uses concise names/IDs referring to rules once, a proposed deduplicated format, not the previously recorded full prompt.')
(p/'state_rules36_expanded.txt').write_text(expanded)
(p/'state_rules36_prompt.txt').write_text(full)
(p/'state_rules36_length.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
