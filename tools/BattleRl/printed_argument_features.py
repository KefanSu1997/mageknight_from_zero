"""Public original descriptions attached to existing action arguments."""
import numpy as np
from printed_entity_text import PrintedEntityText
from public_entity_tokens import PublicEntityTokens, original_vocabulary


def extend_arguments(actions, ordinary, layout=None):
    layout = layout or PublicEntityTokens(original_vocabulary())
    printed = PrintedEntityText(layout.ids)
    by_id = {identity: printed.table[i] for i, identity in enumerate(layout.ids)}
    extra = np.zeros((len(actions), 256), dtype=np.float32)
    for index, action in enumerate(actions):
        primary = by_id.get(action.get('Card', ''))
        if primary is not None:
            extra[index, :128] = primary
        fields = dict(part.split('=', 1) for part in action.get('Input', '').split(';') if '=' in part)
        for key in ('spellId', 'skill', 'advanced', 'material'):
            target = by_id.get(fields.get(key, ''))
            if target is not None:
                extra[index, 128:] = target
                break
    features = np.concatenate((ordinary, extra), axis=1)
    features.setflags(write=False)
    return features, printed.signature
