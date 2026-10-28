# PyTorch / SB3 recurrent training

The production entry point is `train_sb3_recurrent.py`. PPO collection, GAE,
sequence padding/minibatching, clipped losses, KL stopping, autograd and Adam
come from pinned SB3-Contrib 2.7.0 and PyTorch. The old NumPy runner is retained
for its active experiment and historical reproduction, not used by this entry.

Install `torch==2.6.0` from the official CUDA 12.4 wheel index into `.venv_rl`,
then install `requirements-sb3.txt`. The runner requires CUDA by default and
fails rather than silently falling back to CPU.

```powershell
& .venv_rl/Scripts/python.exe tools/BattleRl/train_sb3_recurrent.py `
  --out AutomationOutputs/BattleRl/NEW_UNIQUE_RUN `
  --workers 32 --n-steps 128 --batch-size 128 --updates 100 `
  --baseline --evaluation-every 20
```

32 independent native rule processes collect in parallel; one central GPU
policy performs inference and optimization. A rollout contains 4096 transitions
(32 environments × 128 steps), not 128 episodes. Upstream minibatches contain
128 time steps before recurrent padding. Four optimizer epochs are requested;
upstream KL stopping may stop them early. Episodes and recurrent memory can
span rollout boundaries; memory resets at episode boundaries.

The input encoder is 256/256/128 with separate 256-unit actor/critic LSTMs.
The action distribution scores the full catalog using shared public argument
and printed-card features, then masks illegal actions. No top-k action pruning.
This is a new PyTorch architecture initialized from scratch: it does not claim
to continue or exactly reproduce the NumPy actor/Transformer checkpoint.

The existing 4000/1000 composition-disjoint single-battle split is validated
against task IDs, rule DLL, public data, action catalog and initial states.
Unknown feasibility remains unknown. Training sampling uses a mixture of
uniform, under-visited and empirical difficulty weights, tracked per worker.
The old sampler's global visit counts are not inherited.

Every rollout buffer, behavior checkpoint, selected action/log-probability/value,
and full native before/after trace is saved. Partial episodes are retained as
partial, not counted as wins or losses. Completed episodes run the existing
rule audit. Reward version `single_battle_v2` uses gamma=1: terminal win +100,
failure -100, kill progress capped at +10, new physical wound cost 20W/(W+5),
and step cost 5/256 capped at 5. Initial wounds are excluded and healing does
not refund cumulative cost. Unit wounds are tracked separately. Native reward
is kept only in replay evidence, not in training reward or policy history.
Winning total return is at least 75; losing total return is at most -90.
Performance tracking reports victory, kills, new hero wounds, unit wounds,
and winning-only wound cost independently of the reward proxy.

`metrics.jsonl` includes optimizer/collection timings and critic EV/MSE before
and after each update on the same unnormalized GAE targets. Fixed evaluation
uses separate environments and restores training RNG; all 1000 tasks remain
in the denominator. It is validation, not a fresh final test set.

Resume with `--resume PRIOR_RUN` and a new output directory, keeping workers,
n-steps and batch-size unchanged. `--updates` specifies additional updates.
The adapter replays saved native prefixes and verifies exact states/rewards
before restoring sampler RNG, LSTM states and the saved PyTorch optimizer.

Passing framework gates or a short real rollout is not evidence of improved
win rate, full original-rule coverage, or autonomous Unity Demo acceptance.

Upstream references:
- https://sb3-contrib.readthedocs.io/en/master/modules/ppo_recurrent.html
- https://sb3-contrib.readthedocs.io/en/master/modules/ppo_mask.html

MaskablePPO does not support recurrent policies in this pinned stack. Therefore
the adapter uses upstream RecurrentPPO with a masked distribution, not a local
PPO optimizer or a claim that two unsupported algorithms can simply be combined.
