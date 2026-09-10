ElementCards Showcase automation: prevent flip ghosting

Problem
- Flip screenshots showed double-image/ghosting and mixed sequences when waits were too short.

What worked
- Increase automation waits in SceneAutomationQuickMenus.EnsureElementCardsShowcaseConfig.
- Raise initialDelaySeconds and per-step waitAfterSeconds.
- Rerun Tools/Scene Automation/Run ElementCards Showcase Automation after recompilation.
- Clear/backup screenshots dir before rerun to keep a single monotonic sequence (000-006).

Notes
- The menu overwrites the config JSON each run, so edit the C# defaults to persist wait changes.
