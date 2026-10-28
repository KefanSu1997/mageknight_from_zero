"""Trainable candidate-specific fusion of independently encoded text.

Frozen vectors are inputs, not parameters. All referenced static rules remain
individual keys/values until a state- and option-conditioned attention step.
No candidate pruning, contextual encoder reuse, or pretrained transfer claimed.
"""
import torch
from torch import nn


class StaticDynamicFusion(nn.Module):
    def __init__(self, latent_width=128, text_width=384, width=128):
        super().__init__()
        self.query = nn.Sequential(nn.Linear(latent_width + text_width, width), nn.GELU())
        self.key = nn.Linear(text_width, width, bias=False)
        self.value = nn.Linear(text_width, width, bias=False)
        self.output = nn.Sequential(nn.Linear(2 * width, width), nn.GELU(), nn.Linear(width, 1))
        self.width = width
        self.latent_width, self.text_width = latent_width, text_width

    def forward(self, latent, dynamic, rules, present):
        n = len(latent)
        if (latent.shape != (n, self.latent_width) or dynamic.shape != (n, self.text_width)
                or rules.ndim != 3 or rules.shape[0] != n or rules.shape[2] != self.text_width
                or present.shape != rules.shape[:2] or present.dtype != torch.bool):
            raise ValueError('Invalid candidate/static-rule binding')
        if not torch.isfinite(latent).all() or not torch.isfinite(dynamic).all():
            raise ValueError('Nonfinite dynamic input')
        # Padding may contain arbitrary values. Remove it before projection,
        # including NaNs, rather than multiplying a NaN projection by zero.
        clean = torch.where(present[..., None], rules, torch.zeros_like(rules))
        if not torch.isfinite(clean).all():
            raise ValueError('Nonfinite referenced rule vector')
        q = self.query(torch.cat((latent, dynamic), dim=-1))
        # Append an empty fallback, enabled ONLY for candidates with no rules.
        # This keeps softmax finite and does not dilute nonempty references.
        empty = ~present.any(dim=1, keepdim=True)
        mask = torch.cat((present, empty), dim=1)
        clean = torch.cat((clean, clean.new_zeros(n, 1, self.text_width)), dim=1)
        scores = (self.key(clean) * q[:, None]).sum(-1) / self.width ** .5
        weights = scores.masked_fill(~mask, -torch.inf).softmax(-1)
        context = (weights[..., None] * self.value(clean)).sum(1)
        return self.output(torch.cat((q, context), dim=-1)).squeeze(-1)
