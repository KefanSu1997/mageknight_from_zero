"""Joint categorical over complete formal operations. PPO remains SB3-Contrib."""
import torch
from sb3_battle_policy import BattleRecurrentPolicy
from sparse_battle_policy import GlobalLegalCategorical
from atomic_action_registry import registry

class AtomicBattlePolicy(BattleRecurrentPolicy):
    def __init__(self,*args,registry_file,**kwargs):
        self.registry_file=registry_file
        super().__init__(*args,**kwargs)

    def _bind_mask(self,obs,padded=False):
        self._supports=obs['support_id'].detach().cpu().numpy().reshape(-1).astype('int64')

    def _get_action_dist_from_latent(self,latent_pi):
        supports=[registry(self.registry_file).get(int(i)) for i in self._supports]
        width=max(map(len,supports));device=latent_pi.device
        ids=torch.arange(width,device=device).expand(len(supports),-1)
        valid=torch.zeros(ids.shape,dtype=torch.bool,device=device)
        owners=[];components=[];weights=[];row_ids=[];col_ids=[]
        for row,support in enumerate(supports):
            valid[row,:len(support)]=True
            for col,sequence in enumerate(support):
                owner=len(row_ids);row_ids.append(row);col_ids.append(col)
                for n,component in enumerate(sequence):
                    owners.append(owner);components.append(component)
                    # The actual consumer and its entire parameter bundle have
                    # distinct weights; order inside each primitive is encoded
                    # by the original public action descriptor (e.g. unit order).
                    weights.append(1. if n==len(sequence)-1 else .5/max(1,len(sequence)-1))
        encoded=self.argument_encoder(self.descriptors[torch.tensor(components,device=device)])
        joint=encoded.new_zeros((len(row_ids),encoded.shape[-1]))
        joint.index_add_(0,torch.tensor(owners,device=device),encoded*encoded.new_tensor(weights)[:,None])
        rows=torch.tensor(row_ids,device=device);cols=torch.tensor(col_ids,device=device)
        query=self.action_net(latent_pi)
        logits=query.new_zeros(ids.shape)
        logits[rows,cols]=(query[rows]*joint).sum(-1)
        return GlobalLegalCategorical(ids,valid,logits)
