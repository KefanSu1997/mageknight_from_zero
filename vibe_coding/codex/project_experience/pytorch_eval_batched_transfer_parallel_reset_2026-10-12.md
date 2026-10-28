# Batched evaluation actions and concurrent resets (2026-10-12)

User requested only these two optimizations; training-environment restoration is unchanged.
New parallel_combat_eval_fast.py preserves per-task CUDA Generators and multinomial calls, gathers global action IDs on GPU and transfers all actions once per wave. Task assignments remain ordered; reset futures are submitted together after completed steps and joined before the next wave. No environment gets step/reset concurrently.
CUDA test_parallel_combat_eval_fast.py passes: old/new per-task actions and outcomes equal, 1/3-worker equality, actual reset overlap. These are small integration tests, not a full 1000-task speed claim.
New train_sb3_combat_fast.py binds migration to the exact parent manifest and checks all old source hashes. Resume from update22 (90112 transitions) with full Adam/RNG/LSTM/environment state, 78 remaining updates. Old training PID110340 stopped only after complete checkpoint/continuation and behavior22 existed; unfinished later work is not counted. Parent evidence retained.
Launch 2026-10-12 17:36:02 Python223988, output H:/MageKnight_RL_Runs/20261012_combat_fast_eval_100. Actual new update and full evaluation speed must still be observed. Previous fixed evaluation20 was109/1000, preserved as history. User agreed no extra update0 run; old Outside baseline is historical only.
