"""Explicit static/dynamic document contract for the next policy candidate.

This compiler does not change the live encoder or assert equivalence to joint
bidirectional attention. Static records stay separate; fusion is trainable.
"""
import hashlib
import json


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                    separators=(',', ':'), allow_nan=False).encode()).hexdigest()


def split_document(document, *, encoder_signature):
    if set(document) != {'state', 'rules', 'question', 'options'}:
        raise ValueError('Unexpected joint document fields')
    if not encoder_signature:
        raise ValueError('An immutable encoder binding is required')
    if any(not isinstance(document[k], str) for k in ('state', 'rules', 'question')):
        raise ValueError('Invalid document text')
    if not isinstance(document['options'], list) or not document['options']:
        raise ValueError('Every legal option must be retained')
    if any(not isinstance(s, str) for s in document['options']):
        raise ValueError('Invalid option text')
    # Retain exact records and order, including repeated identities. Cache keys
    # bind the text itself rather than a context ID or identity alone.
    static = []
    for line in document['rules'].splitlines(keepends=True):
        static.append(dict(text=line, key=digest([encoder_signature, line])))
    dynamic = {k: document[k] for k in ('state', 'question')}
    dynamic['options'] = list(document['options'])
    return dict(version=1, encoder_signature=encoder_signature, static=static,
                dynamic=dynamic, dynamic_key=digest([encoder_signature, dynamic]),
                source_sha256=digest(document),
                scope='Input organization only; separate encoding/fusion requires its own gate')


def reconstruct_document(split):
    if split['version'] != 1:
        raise ValueError('Unsupported split document')
    binding = split['encoder_signature']
    for record in split['static']:
        if record['key'] != digest([binding, record['text']]):
            raise ValueError('Changed static text/cache binding')
    if split['dynamic_key'] != digest([binding, split['dynamic']]):
        raise ValueError('Changed dynamic text/cache binding')
    result = dict(split['dynamic'], rules=''.join(r['text'] for r in split['static']))
    if digest(result) != split['source_sha256']:
        raise ValueError('Split document lost original text or option ordering')
    return result
