# Real combat → PPO → Unity replay

## Persistent starting-deck training

`--serve-journey-builds` now selects v12: original 16-card starts mixed with explicitly configured legal midgame 21/26-card inventories. Advanced/spell cards are sampled without duplicates using an independent seeded substream; no foreign hero basics or out-of-range crystals are allowed. This is inventory coverage, not proof of earning the initial cards. The same formal player/deck persists across turns and night. v10 also added actual material/supply choices, original Learning purchases and both original spell/deed definitions needed by the formal play API. The catalog has 15,752 actions and 1,607 features. Artifacts, units, specialized/shared skills and original map/site contexts remain incomplete.

Static profiles and semantic indices are cached; legality still previews real rules with full restoration. Every completed training episode is audited. Periodic evidence uses fresh completed training trajectories per hero instead of running seven extra games solely for the same checks; checkpoint policy evaluation remains separate. First real supply acquisition and first spell attack consumer are retained once per run. Unexpected protocol failures retain state/action/trace and stop training. Inventory families, distinct starting decks, effect use and consumers are separate evidence.

v9 exposes actual Improvisation material cards, Mana Draw/Mana Pull conversion dice and colors, advanced 021/040 basic colors, and earned extra-source consumers. Empty input semantic keys and feature prefixes remain compatible with earlier models. Identical material IDs currently resolve to another actual hand instance, rather than exposing every identical physical-card choice. New observations include extra quotas and real turn claims; `BATTLE_RL_DLL` selects an independently built rules assembly without overwriting a running version. Coverage records actual input profiles and extra-source consumers separately. Printed conversion fees are read from original data in targeted checks; Mana Pull costs white. This adds real inputs but does not finish specialized skills, spells, artifacts, units, legal varied decks or the new UI replay.

v8 rejects insufficient attack proposals without spending contributions or committing provisional Defend bonuses (original rulebook p9.6), and injects a separate round-shuffle random source into the formal engine. Historical pre-v8 trajectories used an unseeded night shuffle; their exact cross-round action prefixes cannot be reconstructed by seed alone. They also allowed failed attacks to spend contributions: old zero-error audit results did not cover this original-rule defect. Preserve those failures and re-evaluate models under current rules. New complete trajectories must replay identically, rather than merely repeat a similar victory rate.

v6 adds 1–3 original enemies, actual target/attack slots, intermediate attack contribution groups, persistent surviving enemy instances, and actual contribution consumer coverage. v7 additionally exposes original enemy Defend source/target choices and separate provisional/applied observations. These mixed encounters are a combat curriculum, not accepted original map/site scenarios. Specialized cards, spells, artifacts, units, shared skills, map contexts and the new Unity replay remain incomplete.

v5 appends the 70 original skills' ownership/turn-use/round-use/flip observations and exposes owned skill activation through the formal battle/outside entry points. Legal level-reward selection varies by seed. Actual `skills` effect options are recorded separately from acquisition. Specialized card/unit/map/multiplayer inputs and additional enemy attack slots remain gaps. Warm starts preserve the previous observation prefix and match actions by semantic key; they do not restore optimizer state.

`--serve-journey` now runs the v4 curriculum: seven original heroes and their actual shuffled 16-card decks, 69 original enemy IDs, outside actions/rest, ordinary source-die selection, formal level rewards and day/night round transitions through `PveJourneySession`. No player/deck/resource reset occurs between battles. A surviving enemy keeps its physical identity; a defeated enemy is replaced for the next battle. Original advanced actions enter through actual level rewards. Reward selections are deterministic curriculum choices; skill acquisition is not counted as skill-use coverage.

```powershell
$env:BATTLE_RL_PROTOCOL='--serve-journey'
python tools/BattleRl/train_ppo.py --steps 32768 --eval-episodes 7 --audit-seconds 60 --out AutomationOutputs/BattleRl/current
python tools/BattleRl/evaluate_journey.py --model AutomationOutputs/BattleRl/current/policy_ppo.npz --out AutomationOutputs/BattleRl/current
```

This curriculum is incomplete: full advanced-card coverage, spells, artifacts, units, skill activation, multiple-enemy groups, map/site contexts and UI replay still need work. The 16-turn/256-step training cutoff is not proof of completing both rounds. `won` means at least one battle was won, not that every battle was won. Legality previews are uncached. Attack candidates expose individual contributions and all contributions, not arbitrary intermediate groups. Target-dependent/card-material choices still need the broader input adapter. Formal denials and time-limit cutoffs have separate `stopReason` values.

Every completed training trajectory is audited, with additional seven-hero reviews approximately every 60 seconds. Failures preserve reproduction JSON and stop training. Checks currently cover physical card identities, source colors/amounts, rests, turn/round lifecycle, Rage's printed attack/block/fee and contribution consumption, plus delayed formal level rewards. These are selective independent oracles, not full original-rule acceptance. See `review_trajectory.py --help` to replay a saved policy or an exact historical failed action prefix.

v2 and v3 lacked formal level rewards: their victory metrics remain historical evidence, not a usable-model acceptance result. v4 fixes the failure where fame changed level immediately before end turn; record and regression are in `vibe_coding/codex/worklog_2026-10-03_ppo_trajectory_review.md`.

`training_coverage.json` records actual effect IDs invoked and encountered hero/enemy IDs. Encountering an enemy does not prove defeating it, and invoking an effect is not independent rule acceptance. `pair_evaluation.json` evaluates all 483 hero/enemy pairs with fresh shuffle seeds; it is not a held-out deck/element generalization test. The v1 Unity replay exporter must not be used for these different traces.

**The following trainer is only the pilot, not the final coverage goal.** The user requires all original combat/hero/card elements in training and persistent multi-turn/multi-round episodes. See `vibe_coding/codex/combat_ppo_full_scope_2026-10-01.md`. Generate the complete uncovered inventory with `dotnet run --project tools/BattleRl -- --coverage-plan AutomationConfigs/rl_full_coverage_inventory.json`; this exports requirements, not training evidence.

Run from the repository root. Uses existing .NET 8, NumPy and the project's Newtonsoft DLL; no downloads or Unity package changes.

```powershell
dotnet build tools/BattleRl -v quiet
python tools/BattleRl/train_ppo.py --steps 32768 --out AutomationOutputs/BattleRl/my_run
python tools/BattleRl/validate_replay.py AutomationOutputs/BattleRl/my_run
python tools/build_pve_rl_replay.py AutomationOutputs/BattleRl/my_run/replay.json
```

Then use the open Unity editor menu **Tools / Mage Knight / Battle / Run Acceptance**. Do not launch Unity.exe directly or use batchmode. The exporter writes the existing `AutomationConfigs/pve_battle.json`; the menu creates a separate local report directory. The generated steps click actual original cards, mode tabs, owned skills, target instances and contribution selectors. Compare the scene report and key screenshots; a backend win alone is not scene acceptance.

The saved `policy_ppo.npz` is a PPO actor/critic (45 inputs → shared 64-unit tanh → masked 33-action actor and scalar critic). Training uses clipped PPO, GAE, entropy regularization, minibatches, Adam and gradient norm clipping. `gradient_check` compares analytical and numerical derivatives, including active/clipped branches. PPO runs on CPU/NumPy; the JSON-lines rule process can be reused by a future CUDA/PyTorch trainer without duplicating rules.

`environment.json` gives ordered feature names and semantic action catalog. `replay.json` is produced by loading the saved PPO weights, rather than a hand-authored winning script. `ui_mapping.json` associates each semantic action with its actual UI steps and before/after states. `validation.json` verifies denial without mutation, model reload, selected actions and exact states.

## Declared scope

`dual032_arythea_level2_two_original_cards_v1`: Arythea level 2, her actual Flame Swordsmanship skill, original March and Rage, red/blue crystal 1 each, two separate original `monster_032` instances. It is explicitly a small-deck rehearsal. Only hand order varies by seed. It is not a standard opening deck, a full-game agent, or evidence of generalization to other enemies/constructs.

All effects, phases, wound creation, armor, costs and rewards execute `Assets/Logic`. Legality is obtained through reversible previews of actual formal operations. Preview restoration checks observable state and original hand/enemy identity. Cached legality is keyed by complete **declared-scope** observation; it must be expanded/versioned for additional cards, identical card instances, units, permissions, targets, attack slots, supply or random effects. The present fixed vocabulary limits attack pools to three contributions and rejects unsupported scope rather than silently dropping actions.

The compact vector includes phase, resources, hand membership, skill usage, enemy life/armor/attack/block state and contribution values/provenance. Fixed original enemy abilities/element metadata remain in the structured state and scope, not guessed from images. Changing the scope requires adding the relevant dynamic feature dimensions and UI mapping.

The legal-action previews invoke the same rules being tested: a rule incorrectly rejecting an action can also mask it from learning. A win therefore does not prove original-rule correctness. Keep original-card oracle checks, resource/identity invariants and UI/state assertions as independent gates; use RL to find and replay new operation sequences.

## Sampling and evaluation

`--serve-journey-parties` (v14) configures declared legal level 1/3/5 inventories with 1/2/3 distinct original units and 0/1/2 own-hero original skills. Original fame, armor, command slots and skill acquisition hooks are retained. Actual unit slot identities and ordered enemy-attack damage reservations enter observations; actions can reserve every ordered subset up to three slots, cancel, heal different units or use printed ready support on another unit. This is not evidence of actual opening acquisition, map recruitment, same-ID physical units or complete specialized skill coverage. `verify_party_inputs.py` exercises an actual Herbs→Barbarians ready/reuse consumer and ordered damage replacement.

Checked v14 appends persistent unit resistance/attack/block/armor/charge/other-unit bonuses and all-attack conversion permissions. `verify_unit_modifiers.py` verifies printed Guards green payment, resistance visibility and formal turn expiry. Its training reward adds +20 on the formal whole-group win rising edge, retaining older step/kill/wound/failure shaping. This is an explicitly versioned training objective, never a rule or assertion change; frozen earlier batches remain unchanged.

`--serve-journey-units` (v13) appends original-unit observations and actions to varied original deed inventories. A declared midgame snapshot owns one original unit within its command slot; its acquisition is not proven. Original abilities use formal session commands, including exact fees/options/boost colors, exhaustion, damage reservation/cancellation and actual healing. Familiar round upkeep uses actual crystals or formal removal. Unit IDs, abilities and subsequent attack/damage/healing consumers are separate coverage. One-unit snapshots cannot cover multi-unit support, recruitment/map scouting or all specialized skill inputs; artifacts and the current-scope Unity replay remain unfinished. Frozen rules DLLs must not be overwritten while running.

Journey protocol top-level `won` is a legacy `kills > 0` flag. Historical evaluation `wins/win_rate` therefore measure at-least-one-kill episodes, not whole battles won. Current Python evaluation counts rising edges of the formal state `won` (`Phase.End` and all actual enemies defeated), separately recording killing episodes and whole-group clears. Do not use completed battle count as winning battle count. The frozen v12 rules DLL remains compatible; game rules and PPO weights are unchanged by this reporting correction.

Reward: step -0.02, each actual kill +2, each new wound -0.02, completed win +10, loss/24-step cutoff -4. Trial actions used to build a mask receive no training reward and leave no resources or identity changes.

Training and evaluation use separate persistent rule processes. Never evaluate by resetting the live rollout process; this invalidates the stored PPO observation and action mask. Evaluation seeds differ from training seeds but only vary hand order in this first family. Random baseline also uses the same formal action mask. Greedy evaluation is deterministic; 200/200 here is not 200 different game situations.

The pilot yielded ~1,121 steps/s after masks warmed in a small 178-state cache. Cold reset+mask cost ~0.169s. Broad unseen-state workloads with many candidate choices can be much slower. UI rendering is reserved for representative final replays, not thousands of learning episodes. All model weights, metrics and traces remain local and are excluded from Git; no LFS upload or remote push.
