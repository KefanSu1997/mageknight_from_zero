"""Visible UI view classification from raycast-valid controls, including tabs."""
from structured_ui_ppo import state
import json

VERSION=4
def panel(obs):
    controls=[c for c in obs['controls'] if c['clickable']]
    names={c['name'] for c in controls}
    if 'Btn_CloseTools' in names:return 'JourneyTools'
    if 'Btn_CloseBattleMenu' in names:return 'BattleMenu'
    for view in ('ManaPayment','DeedInputs','AttackSelection','BlockSelection','EnemyDefend','OwnedUnits','SharedSkills',
                 'ContributionSkill','Conversions','CopyPicker','PendingCopy','EnemyTokenSkill','HarvestInputs',
                 'HeadbandSocial','HeadbandTarget','JourneyInteraction','SiteSpellShop','LearningSupply','DungeonIntro','SiteReward',
                 'JourneySupport','JourneyHand','JourneyMap','HeroStart','Camp'):
        if any('/'+view+'/' in c['path'] for c in controls):return view
    if any('/Units/' in c['path'] and c['name'].startswith('Btn_Unit_') for c in controls):return 'Battle.Units'
    if any('/Skills/' in c['path'] and c['name'].startswith('Btn_Skill_') for c in controls):return 'Battle.Skills'
    values=state(obs);phase=values.get('phase')
    if values.get('audit.costNativeState'):phase=json.loads(values['audit.costNativeState'])['phase']
    return 'Outside' if phase=='Outside' else 'Battle.Hand'
