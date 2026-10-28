# PyTorch CUDA + upstream SB3 recurrent PPO migration

User requested replacing the hand-written CPU NumPy learner with an established
RL framework, at minimum PyTorch. Added isolated `.venv_rl` with PyTorch
2.6.0+cu124, SB3/SB3-Contrib 2.7.0 and Gymnasium 1.2.1. CUDA identifies the
RTX 4060 Laptop GPU. Existing NumPy trainer PID 55060 remains untouched.

New entry: `tools/BattleRl/train_sb3_recurrent.py`; usage and boundaries in
`tools/BattleRl/SB3_TRAINING.md`. Upstream RecurrentPPO owns collection, GAE,
sequence minibatches, PPO/value/entropy losses, KL stopping, autograd and Adam.
Custom code handles native rules, public inputs, masked action distribution,
evidence and checkpoint hooks. MaskablePPO does not support recurrence; do not
claim it was combined automatically with RecurrentPPO.

Three-layer state encoder, separate actor/critic LSTMs, full catalog mask and
shared printed-action features. This is fresh PyTorch initialization, not an
equivalent migration of the old NumPy Transformer actor. Per-worker task
curriculum differs from old global sampling. Keep experiments separate.

Evidence:
- `20261012_sb3_cuda_gate`: 32 native environments, 512 transitions, 2 updates.
- `20261012_sb3_cuda_resume_gate`: native prefix replay/state/reward validation,
  exact initial weights and Adam states, restored RNG/LSTM, additional update
  to 768 cumulative transitions. 23 parameter optimizer states compared exactly.
- `20261012_sb3_cuda_full_batch_gate`: 32 × 128 steps, minibatch 128, four epochs,
  two actual updates/8192 transitions, 162 completed episodes/1 win, 32 retained
  partial episodes. Every action/probability record joins to native before/after
  trace; 162 completed-episode audits report no errors. This is a finite audit,
  not complete original-rule acceptance.
- Collection: 63.72 s and 42.31 s; GPU optimization: 16.10 s and 17.43 s.
  Archival/diagnostics/startup are additional. No apples-to-apples NumPy speedup
  claim because its rollout sizes and architecture differ.
- Same-batch value MSE: 61.79→17.32 and 8.59→2.79; post-update EV ~0.00138 and
  -0.000042. Mostly mean/bias fitting; no policy-improvement claim. No independent
  fixed validation was run in these explicitly bounded integration gates.
- Two unit tests verify legal masks, temporal state/reset, actual LSTM updates,
  save/load, external descriptor integrity and rejection after tampering.

Immutable descriptors are SHA-bound once per run, avoiding duplicated public
features in every checkpoint. New weight/Adam checkpoint is ~33 MB, compared
with ~400 MB in the initial gate. Full buffers, probabilities and native traces
remain retained. Check capacity before starting long runs: uncompressed native
traces are substantial. No old evidence was deleted.

Unity Console checks both returned MCP HTTP 502. No C# changes in this migration;
nevertheless do not claim a verified clean Console or Unity Demo acceptance.

Reports are in each output directory under `AutomationOutputs/BattleRl/`.
The full-batch directory also contains a SHA-bound snapshot of migration source.
