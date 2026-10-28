"""Single tokenization with the original attention, batch order and markers."""
import os
import time
import numpy as np
import torch
from hierarchical_batch_encoder import BatchedReader
from joint_option_text import encode_document


def encode_many(self, documents, batch_size=None):
    batch_size = batch_size or int(os.environ.get('BATTLE_RL_ENCODER_BATCH', '4'))
    started = time.perf_counter()
    encoded = [encode_document(self.tok, d, self.max_tokens) for d in documents]
    self.last_lengths = [len(x[0]) for x in encoded]
    token_seconds = time.perf_counter() - started
    result = [None] * len(encoded)
    order = sorted(range(len(encoded)), key=lambda i: len(encoded[i][0]))
    self.model.config._attn_implementation = 'hierarchical_batch'
    for layer in self.model.layers:
        layer.mlp.forward = layer.mlp._joint_original_forward
    model_started = time.perf_counter()
    with torch.inference_mode():
        for offset in range(0, len(order), batch_size):
            indices = order[offset:offset + batch_size]
            lengths = [len(encoded[i][0]) for i in indices]
            x = torch.full((len(indices), max(lengths)), self.tok.pad_token_id,
                           device='cuda', dtype=torch.long)
            for j, i in enumerate(indices):
                x[j, :lengths[j]] = torch.tensor(encoded[i][0], device='cuda')
            self.model.config._hierarchical_lengths = torch.tensor(lengths, device='cuda')
            hidden = self.model(input_ids=x, attention_mask={
                'full_attention': None, 'sliding_attention': None}).last_hidden_state
            for j, i in enumerate(indices):
                result[i] = hidden[j, [0] + encoded[i][1]].float().cpu().numpy()
    if not all(np.isfinite(v).all() for v in result):
        raise ValueError('Nonfinite batched encoding')
    self.last_profile = dict(tokenization_seconds=token_seconds,
                             model_and_transfer_seconds=time.perf_counter()-model_started,
                             total_seconds=time.perf_counter()-started)
    return result


class SingleTokenizationReader(BatchedReader):
    encode_many = encode_many
