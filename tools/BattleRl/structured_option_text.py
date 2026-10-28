"""Candidate option-local printed rules; retain every original action parameter.

Exact catalog identities bind source text. Unknown IDs stay explicit rather than
guessing a card, material, target, fee or effect from a physical slot number.
"""
import re
from joint_option_text import JointRenderer


class StructuredOptionRenderer(JointRenderer):
    def referenced_rules(self, action):
        references = set()
        for field, value in action.items():
            if field == 'Key' or value in ('', None):
                continue
            # Catalog IDs use letters, digits and underscores. Field punctuation
            # separates IDs; substring matches such as card_01/card_010 cannot bind.
            references.update(token for token in re.findall(r'[A-Za-z0-9_]+', str(value))
                              if token in self.rules and token != '_public_global_')
        return [(identity, self.rules[identity]) for identity in sorted(references)]

    def primitive(self, action):
        original = super().primitive(action)
        references = self.referenced_rules(action)
        if not references:
            return original
        printed = '；'.join(identity + '：' + text for identity, text in references)
        return original + '。【上述实体的原印刷规则：' + printed + '；实际发动模式、付款、材料及目标以本项完整参数为准，不额外执行未选模式。】'


class ReferencedOptionRenderer(StructuredOptionRenderer):
    """Option-local names and exact pointers to the document's shared rules.

    Original printed rules remain in the full joint glossary once per entity.
    The inline variant is retained for evidence, but exceeds the token limit on
    some archived full-support documents and must not be deployed.
    """
    def primitive(self, action):
        original = JointRenderer.primitive(self, action)
        references = self.referenced_rules(action)
        if not references:
            return original
        labels = []
        for identity, text in references:
            name = re.search(r'card_name:([^|]+)', text)
            if name:
                labels.append(name.group(1))
        # The original fields already contain the exact glossary IDs. Add only
        # their source names, avoiding repeated instructions at every primitive.
        return original + ('（' + '、'.join(labels) + '）' if labels else '')
