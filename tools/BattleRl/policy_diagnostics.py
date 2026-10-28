"""Policy quality from actual completed operations; no extra rule simulation."""


def summarize_policy_trace(trace):
    planning = {'unitDamage', 'defend', 'clearDefend', 'bowChoice',
                'killCrystal', 'protectionCleanup', 'roundReturn',
                'payment', 'cancelPayment'}
    streak = longest = count = wins = started = 0
    for row in trace:
        action = row['action']
        reserves = action['Kind'] in planning or (
            action['Kind'] == 'bannerUse' and action['Card'] == 'items_018')
        count += reserves
        streak = streak + 1 if reserves else 0
        longest = max(longest, streak)
        wins += bool(row['after'].get('won')) and not row['before'].get('won', False)
        started += action['Kind'] == 'fight'
    return {'operations': len(trace), 'started_battles': started,
            'won_battles': wins, 'planning_operations': count,
            'longest_consecutive_planning': longest,
            'stop_reason': trace[-1]['after'].get('stopReason', '') if trace else '',
            'scope': 'policy quality only; legal planning is not a rule violation'}
