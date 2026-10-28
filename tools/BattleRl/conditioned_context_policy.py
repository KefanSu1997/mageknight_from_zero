"""Train-statistic text conditioning with an output-preserving warm migration.

Only text state/V readouts are reparameterized; no numeric observation bypass.
The transformed first linear's Adam state is explicitly reset, not falsely
claimed equivalent. All other parameter objects and optimizer states survive.
"""
import torch
from torch import nn
from context_state_policy import ContextStatePolicy

class ConditionedNorm(nn.Module):
    def __init__(self,norm):
        super().__init__();self.norm=norm
        p=next(norm.parameters())
        self.register_buffer('center',torch.zeros(384,device=p.device,dtype=p.dtype))
        self.register_buffer('scale',torch.ones(384,device=p.device,dtype=p.dtype))
    def forward(self,x):return (self.norm(x)-self.center)/self.scale

def install(sequence):
    if isinstance(sequence[0],ConditionedNorm):raise ValueError('Already conditioned')
    sequence[0]=ConditionedNorm(sequence[0])

def fit_preserving_output(sequence,train_pooled):
    norm,linear=sequence[0],sequence[1]
    assert isinstance(norm,ConditionedNorm) and isinstance(linear,nn.Linear)
    if not torch.equal(norm.center,torch.zeros_like(norm.center)) or not torch.equal(norm.scale,torch.ones_like(norm.scale)):
        raise ValueError('Migration must be applied exactly once')
    with torch.no_grad():
        z=norm.norm(train_pooled)
        center=z.mean(0);scale=z.std(0,unbiased=False).clamp_min(.01)
        # W'((z-m)/s)+b' = Wz+b. No clipping, so equivalence holds for all z.
        old_weight=linear.weight.clone()
        linear.bias.add_(old_weight@center)
        linear.weight.mul_(scale[None,:])
        norm.center.copy_(center);norm.scale.copy_(scale)
    return [linear.weight,linear.bias]

class ConditionedContextPolicy(ContextStatePolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs);self.install_conditioning()
    def install_conditioning(self):
        install(self.features_extractor.state_adapter);install(self.token_value)
    def fit_conditioning(self,state_pooled,value_pooled):
        changed=[]
        for sequence,x in [(self.features_extractor.state_adapter,state_pooled),(self.token_value,value_pooled)]:
            changed.extend(fit_preserving_output(sequence,x))
        reset=0
        for p in changed:
            if p in self.optimizer.state:
                del self.optimizer.state[p];reset+=1
        return dict(reparameterized_parameters=len(changed),optimizer_states_reset=reset,
                    reason='Centering mixes linear weight/bias gradients; old Adam second moments cannot be transformed exactly without cross moments.')
