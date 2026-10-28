"""Strict initial semantic-to-pointer mapping. Unsupported semantics fail closed."""

ROOT='Canvas/BattleTable/'


def action_steps(action,state):
    if action['Kind']=='attack':
        if action.get('Input','') or action.get('Card','') or action.get('Choice','') or action.get('Option',0)!=0:
            raise ValueError('Attack carries unsupported payment/card/slot semantics')
        targets=action.get('Targets',0)
        if targets<=0 or targets.bit_count()!=1 or state['phase'] not in ('Ranged','Melee'):
            raise ValueError('Only a mapped single-target attack is currently supported')
        index=targets.bit_length()-1
        enemies=state['enemies']
        if index>=len(enemies) or enemies[index]['defeated'] or any(enemy['defend'] for enemy in enemies):
            raise ValueError('Defeated/out-of-range or defender followup requires explicit mapping')
        parts=state['attacks']
        if not parts or any(part['bound'] for part in parts) or action.get('Parts',0)!=(1<<len(parts))-1:
            raise ValueError('Bound or selected-subset contribution mapping remains a gap')
        if state.get('uiSelectedTargets')!=[] or state.get('uiActionContextNeutral') is not True:
            raise ValueError('Current UI targets must be empty and card/unit/skill context neutral')
        steps=[dict(buttonPath=ROOT+'Enemy_'+str(index)+'/Btn_Target',label='语义attack · 选择实际敌人',skipScreenshot=True)]
        if len(parts)>1:
            steps += [dict(buttonPath=ROOT+'Inspector/Btn_AttackParts',label='打开真实贡献选择',skipScreenshot=True),
                      dict(buttonPath=ROOT+'AttackSelection/Btn_All',label='明确全部贡献用于本次',skipScreenshot=True),
                      dict(buttonPath=ROOT+'AttackSelection/Btn_Done',label='确认贡献选择',skipScreenshot=True)]
        elif state.get('uiReservedAttackSlots')!=[]:
            raise ValueError('Single contribution has an unknown/reserved UI selection')
        return steps+[dict(buttonPath=ROOT+'Inspector/Btn_Resolve',label='语义attack · 正式消费贡献',skipScreenshot=True)]
    if action.get('Input','') or any(action.get(key,0)!=0 for key in ('Targets','Parts','Option')):
        raise ValueError('Payment/material/target/slot semantics require an explicit UI mapping')
    kind=action['Kind']
    if kind=='advance':
        if action.get('Card','') or action.get('Choice','') or state['phase'] not in ('Ranged','Block','Melee'):
            raise ValueError('Advance phase is not mapped or has extra semantics')
        return [dict(buttonPath=ROOT+'Inspector/Btn_Next',label='语义advance · '+state['phase'],skipScreenshot=True)]
    if kind=='sideways':
        card=action['Card']
        if action.get('Choice','') or state['phase'] not in ('Block','Melee') or card not in state['hand'] or card=='w':
            raise ValueError('Sideways requires an actual non-wound card in a mapped phase')
        index=state['hand'].index(card)  # Formal play-by-ID selects first physical occurrence.
        if index>=7:  # Existing PveBattleController HandPageSize is seven.
            raise ValueError('Hand pagination needs explicit current UI page mapping')
        return [dict(buttonPath=ROOT+'Hand/Btn_Card_'+card+'_0',label='选择语义动作的实际手牌 '+card,skipScreenshot=True),
                dict(buttonPath=ROOT+'Inspector/Btn_Sideways',label='语义sideways · '+card,skipScreenshot=True)]
    raise ValueError('Unsupported action kind remains a visible UI coverage gap: '+kind)
