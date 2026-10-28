"""Isolated IPC candidate: transmit persisted context once, reconstruct in parent.

Not enabled in live preparation. Measure decode/tree cost against IPC savings.
"""
from solo_a_compact_context_candidate import decode
from hierarchical_option_policy import ChoiceTree


def pack(packet):
    return {key: value for key, value in packet.items()
            if key not in ('support', 'document', 'tree')}


def unpack(packet):
    record = decode(packet['digest'], packet['payload'])
    tree = ChoiceTree(record['support']['operations'])
    if tree.leaves != len(record['support']['commands']):
        raise ValueError('Incomplete transported support')
    return dict(packet, support=record['support'], document=record['document'], tree=tree)
