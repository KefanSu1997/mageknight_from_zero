"""Batched A behavior with task-local RNG and saved pre-decision LSTM state."""
import hashlib,time
from pathlib import Path
import numpy as np
import torch
from sb3_contrib.common.recurrent.type_aliases import RNNStates


class ScenarioBehavior:
    def __init__(self,policy,inputs,runtime,renderer):
        self.policy=policy;self.inputs=inputs;self.runtime=runtime;self.renderer=renderer
        policy.bind_runtime(runtime);policy.set_training_mode(False)

    def begin(self,seed):
        def pair():return (torch.zeros(1,1,256,device=self.policy.device),torch.zeros(1,1,256,device=self.policy.device))
        return dict(memory=RNNStates(pair(),pair()),generator=torch.Generator(device=self.policy.device).manual_seed(seed),
                    history=np.zeros(97,np.float32),rows=[],pending=None,started=False)

    def select(self,state,context):return self.select_many([state],[context])[0]

    def select_many(self,states,contexts):
        if not states or len(states)!=len(contexts):raise ValueError('Behavior batch mismatch')
        started=time.perf_counter();observations=[];supports=[]
        for state,context in zip(states,contexts):
            if context['pending'] is not None:raise ValueError('Native transition must complete before next selection')
            obs,support=self.inputs.encode(state,context['history'])
            if support is None:raise ValueError('Cannot select at terminal')
            document=self.renderer.render(state['observation'],support)
            obs['context_id'][0]=self.runtime.add(support,document)
            observations.append(obs);supports.append(support)
        prepare=time.perf_counter()-started;started=time.perf_counter();device=self.policy.device
        tensors={k:torch.as_tensor(np.stack([o[k] for o in observations]),device=device) for k in observations[0]}
        def cat(branch):return tuple(torch.cat([c['memory'][branch][j] for c in contexts],dim=1) for j in range(2))
        memories=RNNStates(cat(0),cat(1));starts=torch.tensor([not c['started'] for c in contexts],device=device,dtype=torch.float32)
        with torch.inference_mode():
            self.policy._bind(tensors)
            features=self.policy.extract_features(tensors)
            if not isinstance(features,torch.Tensor):raise ValueError('A requires shared feature encoder')
            actor,actor_state=self.policy._process_sequence(features,memories.pi,starts,self.policy.lstm_actor)
            critic,critic_state=self.policy._process_sequence(features,memories.vf,starts,self.policy.lstm_critic)
            actor=self.policy.mlp_extractor.forward_actor(actor);critic=self.policy.mlp_extractor.forward_critic(critic)
            values=self.policy.value_net(critic).flatten()
            distribution=self.policy._get_action_dist_from_latent(actor)
            actions=distribution.sample_rows(generators=[c['generator'] for c in contexts])
            logs=distribution.log_prob(actions)
            packed=torch.stack((actions.float(),logs,values),1).cpu().numpy()
        if not np.isfinite(packed).all():raise ValueError('Nonfinite behavior')
        inference=time.perf_counter()-started;choices=[]
        for i,(state,c,obs,support,(action,logp,value)) in enumerate(zip(states,contexts,observations,supports,packed)):
            def cpu_pair(pair):return tuple(x.detach().cpu().clone() for x in pair)
            before=RNNStates(cpu_pair(c['memory'].pi),cpu_pair(c['memory'].vf))
            row=dict(observation=obs,action=int(action),value=float(value),log_probability=float(logp),
                     lstm_before=before,episode_start=not c['started'],terminated=False)
            c['pending']=dict(row=row,step=state['observation']['Step'],support_sha256=state['support_sha256'],
                              next_history=support['history_features'][int(action)].copy())
            c['memory']=RNNStates(tuple(x[:,i:i+1].clone() for x in actor_state),tuple(x[:,i:i+1].clone() for x in critic_state))
            c['started']=True
            choices.append(dict(index=int(action),log_probability=float(logp),value=float(value),
                                prepare_seconds=prepare/len(states),inference_seconds=inference/len(states)))
        return choices

    def transition(self,result,context):
        pending=context['pending']
        if pending is None:raise ValueError('Unexpected native transition')
        native=result['trajectory'];row=pending['row']
        if native['BeforeStep']!=pending['step'] or native['SupportSha256']!=pending['support_sha256'] or native['SelectedIndex']!=row['action']:
            raise ValueError('Executed action differs from behavior')
        for field,key in (('BehaviorLogProbability','log_probability'),('ValuePrediction','value')):
            if native[field]!=row[key]:raise ValueError('Native behavior metadata mismatch')
        reward=float(native['Transition']['Reward'])
        if not np.isfinite(reward):raise ValueError('Nonfinite native reward')
        row['reward']=reward;row['terminated']=bool(result['state']['observation']['Terminal'])
        context['rows'].append(row);context['history']=np.concatenate((pending['next_history'],np.array([reward],np.float32)))
        context['pending']=None

    def snapshot(self,context):
        memory=RNNStates(*(tuple(x.detach().cpu().clone() for x in pair) for pair in context['memory']))
        return dict(memory=memory,generator=context['generator'].get_state(),generator_device=str(self.policy.device),
                    history=context['history'].copy(),rows=context['rows'],pending=context['pending'],started=context['started'])

    def save_policy(self,path):
        self.policy.save(path)
        return dict(sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),schema_sha256=self.inputs.schema.signature,
                    catalog_signature=self.inputs.text_assets.catalog.signature,kind='baseline_A_solo_recurrent')
