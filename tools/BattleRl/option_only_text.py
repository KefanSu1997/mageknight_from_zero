"""Candidate: numeric state/history stay in green network, not duplicated in text.

Must use a fresh context/cache namespace when deployed. Existing cached vectors
belong to the previous text contract and cannot be relabelled or reused.
"""
from structured_option_text import ReferencedOptionRenderer

class OptionOnlyRenderer(ReferencedOptionRenderer):
    def render(self,features,previous,choices):
        document=super().render(features,previous,choices)
        document['state']='公开数值状态及历史由独立结构化分支提供。此处读取原规则和当前合法选项；锁定选择不可反悔。'
        return document

def without_numeric_text(document):
    """Archived-document counterpart, preserving every non-state field."""
    result=dict(document)
    result['state']='公开数值状态及历史由独立结构化分支提供。此处读取原规则和当前合法选项；锁定选择不可反悔。'
    return result
