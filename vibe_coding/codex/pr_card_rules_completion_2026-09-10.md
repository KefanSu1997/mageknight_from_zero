# fix(cards): synchronize actual wounds and make scene audits resumable to inspect

## Why

Healing updated a separate integer without removing wound cards; poison/hero damage could duplicate cards once that count was corrected. Scene auditing stopped with Win32 IO 1224 while truncating an existing memory-mapped report.

## What

Wound counts now come from actual hand/discard cards. Healing and draw-per-heal share one service that uses the actual number removed. Cure and Holy Grail activate their triggers before healing. Each audit run gets a unique directory, immutable per-case checkpoints and a final atomically published effect report.

## How to test

Use the installed Unity 6000.6.0f1 editor via Hub. Run Tools/Mage Knight/Original Cards/Run All Batches, Tools/Mage Knight/Adventure/Run All EditMode Tests, and Tools/Mage Knight/Adventure/Run All Four. No batchmode or package changes.

Reproduce generated fixtures with `python tools/build_all_card_verification.py`. Summarize the exact new directory with `python tools/summarize_all_card_verification.py --output AutomationOutputs/AllOriginalCards/<run>`.

Evidence: AutomationOutputs/AllOriginalCards/20260910_140343_c3df5a04. 95 EditMode tests pass. 316 scene cases: 85 passed, 92 failed, 139 partial; 944 pointer actions, 2792 effect assertions, 320 captures. Four adventure regressions: 114 actions, 477 assertions and 118 captures pass. Unity Console has zero errors on two final scans.

## Risks

This is an intermediate repair, not full original-card certification. Remaining failures and partial coverage are explicit. Spell costs, lifecycle integration, targets, downstream effects, unit and skill coverage still require work. AdventureSession has a separate presentation card-instance collection; current regression passes, broader lifecycle integration is still pending. Intermediate checkpoints remain local; final reports and all 438 captures are archived.

## Related issues

User request: continuously iterate until every original card passes in-scene validation. No issue number supplied. Remote PR creation/merge is pending authenticated GitHub access; the branch can be archived over SSH 443.
