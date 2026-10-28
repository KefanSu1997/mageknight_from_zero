# Default baseline and historical reference (2026-10-12)

User requests update0 baseline in plots and baseline enabled by default in future experiments.
Current dashboard baseline_reference.json preserves source and original reward: historical29/1000=2.9%, Outside entry and old horizon/different initial policy. It is a separate unconnected historical reference series, not current update0. Reward is display-converted by0.01 with explicit label; no training values changed.
Future fresh experiment entry is tools/BattleRl/train_sb3_episodes_default.py: baseline=True by default, explicit --no-baseline opt-out. Resume does not rerun initial validation or mislabel a trained checkpoint update0. Existing live trainer remains unchanged. Use this entry for future fresh runs.
