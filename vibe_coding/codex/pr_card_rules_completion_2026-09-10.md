# fix(cards): synchronize wound cards and validate original spell payments

## Why

Healing updated a separate integer without removing wound cards; poison/hero damage could duplicate cards once that count was corrected. Scene auditing stopped with Win32 IO 1224 while truncating an existing memory-mapped report.

Spells skipped base costs, several original spell records had wrong or missing colors, and unaffordable enhanced actions silently executed their base effect. Multi-color payment could deduct the first color before rejecting the second.

## What

Wound counts now come from actual hand/discard cards. Healing and draw-per-heal share one service that uses the actual number removed. Cure and Holy Grail activate their triggers before healing. Each audit run gets a unique directory, immutable per-case checkpoints and a final atomically published effect report.

Exact-color payment validates the full cost before deduction. Spells pay their original top/bottom costs, powered spells enforce night or an explicit artifact exception, and unaffordable actions reject without downgrade. Printed spell costs are restored in JSON and existing SOs. Green crystallization pays ManaColor directly. Scene suites are explicitly imported and identified by SHA256; EditMode and adventure regression runs also use unique output directories.

## How to test

Use the installed Unity 6000.6.0f1 editor via Hub. Run Tools/Mage Knight/Original Cards/Run All Batches, Tools/Mage Knight/Adventure/Run All EditMode Tests, and Tools/Mage Knight/Adventure/Run All Four. No batchmode or package changes.

Reproduce source corrections with `python tools/restore_spell_costs.py` and generated fixtures with `python tools/build_all_card_verification.py`. Summarize the exact new directory with `python tools/summarize_all_card_verification.py --output AutomationOutputs/AllOriginalCards/<run>`. Verify regressions with `python tools/verify_card_regression.py --scenes <adventure-run> --tests <editmode-run>`.

Latest evidence: AutomationOutputs/AllOriginalCards/20260910_142756_642511ad. 98 EditMode tests pass with matching XML/JSON. 325 scene cases: 95 passed, 65 failed, 165 partial; 971 pointer actions, 3369 effect assertions, 329 captures. Nine rejection cases assert all mana colors and real card zones remain unchanged. Four adventure regressions: 114 actions, 477 assertions and 118 captures pass. Unity Console has zero errors on two final scans. Earlier wound-only evidence remains in 20260910_140343_c3df5a04.

## Risks

This is an intermediate repair, not full original-card certification. Remaining failures and partial coverage are explicit. Wild-mana source selection, full action transactions after target/effect failure, lifecycle integration, targets, downstream effects, unit and skill coverage still require work. AdventureSession has a separate presentation card-instance collection; current regression passes, broader lifecycle integration is still pending. Intermediate checkpoints remain local; final reports and all 447 latest captures are archived.

## Related issues

User request: continuously iterate until every original card passes in-scene validation. No issue number supplied. Remote PR creation/merge is pending authenticated GitHub access; the branch can be archived over SSH 443.
