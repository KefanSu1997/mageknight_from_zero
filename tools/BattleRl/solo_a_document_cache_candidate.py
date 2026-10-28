"""Exact bounded memoization of frozen public rule naming; opt-in only."""
import re
from functools import lru_cache

from candidate_option_runtime import LABELS


@lru_cache(maxsize=128)
def rule_replacements(rule_text):
    # Preserve dict insertion order and duplicate-key semantics of the original.
    rules = dict(line.split('：', 1) for line in rule_text.splitlines() if '：' in line)
    result = []
    for identity, text in rules.items():
        name = re.search(r'(?:card_name_cn|card_name|monster_name):([^|\n]+)', text)
        if name:
            result.append((re.compile(r'(?<![A-Za-z0-9_])' + re.escape(identity)
                                      + r'(?![A-Za-z0-9_])'),
                           name.group(1) + '（' + identity + '）'))
    return tuple(result)


@lru_cache(maxsize=8192)
def edge_text(edge, rule_text):
    key, value = edge
    for old, new in [('完整操作', '完整操作'), ('/Kind', '/操作类型'),
                     ('/Card', '/牌或技能'), ('/Choice', '/发动模式')]:
        key = key.replace(old, new)
    result = key + '=' + LABELS.get(value, value)
    # Sequential substitution is intentional, matching the reference exactly.
    for pattern, replacement in rule_replacements(rule_text):
        result = pattern.sub(replacement, result)
    return result


def node_document(tree, base, node):
    current = tree.nodes[node]
    prefix = '；'.join(edge_text(tuple(e), base['rules'])
                      for e in current['prefix']) or '尚未选择'
    options = [edge_text(tuple(e), base['rules']) for e in current['children']]
    if not options:
        options = ['唯一完整操作，直接执行']
    return dict(state=base['state'] + '\n本次完整操作已锁定（不可取消或回退）：' + prefix,
                rules=base['rules'],
                question='选择当前层的一项合法后继；全部参数确定后才执行完整操作。',
                options=options)
