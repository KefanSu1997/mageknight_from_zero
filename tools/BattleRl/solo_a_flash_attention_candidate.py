"""Isolated frozen encoder candidate; importing does not register attention."""
import torch


def attention(module, q, k, v, attention_mask, dropout=0., scaling=None, **kwargs):
    if dropout or torch.is_grad_enabled():
        raise ValueError('Frozen inference only')
    if q.dtype not in (torch.float16, torch.bfloat16) or not q.is_cuda:
        raise ValueError('Native half precision CUDA tensors required; no implicit cast')
    from flash_attn import flash_attn_varlen_func
    lengths = module.config._hierarchical_lengths
    batch, heads, width, dim = q.shape
    valid = torch.arange(width, device=q.device)[None, :] < lengths[:, None]
    indices = valid.flatten().nonzero().flatten()
    cumulative = torch.cat((lengths.new_zeros(1), lengths.cumsum(0))).to(torch.int32)
    def pack(x):
        return x.transpose(1, 2).reshape(batch * width, heads, dim).index_select(0, indices)
    window = module.config.sliding_window if module.sliding_window is not None else None
    packed = flash_attn_varlen_func(
        pack(q), pack(k), pack(v), cumulative, cumulative, width, width,
        dropout_p=0., softmax_scale=scaling, causal=False,
        window_size=(-1, -1) if window is None else (window, window))
    result = q.new_zeros((batch * width, heads, dim))
    result.index_copy_(0, indices, packed)
    # Padded query outputs are unused, and never keys in subsequent layers.
    return result.reshape(batch, width, heads, dim), None
