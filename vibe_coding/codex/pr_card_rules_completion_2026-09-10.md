# fix(cards): restore original card choices, costs and combat resolution

## Why

Healing updated a separate integer without removing wound cards; poison/hero damage could duplicate cards once that count was corrected. Scene auditing stopped with Win32 IO 1224 while truncating an existing memory-mapped report.

Spells skipped base costs, several original spell records had wrong or missing colors, and unaffordable enhanced actions silently executed their base effect. Multi-color payment could deduct the first color before rejecting the second.

## What

Wound counts now come from actual hand/discard cards. Healing and draw-per-heal share one service that uses the actual number removed. Cure and Holy Grail activate their triggers before healing. Each audit run gets a unique directory, immutable per-case checkpoints and a final atomically published effect report.

Exact-color payment validates the full cost before deduction. Spells pay their original top/bottom costs, powered spells enforce night or an explicit artifact exception, and unaffordable actions reject without downgrade. Printed spell costs are restored in JSON and existing SOs. Green crystallization pays ManaColor directly. Scene suites are explicitly imported and identified by SHA256; EditMode and adventure regression runs also use unique output directories.

Combat contributions now retain their element and melee/ranged/siege type, including boosts and conversions. The shared combat calculator fixes physical resistance, aggregate rounding, cold-fire group resistance and fortification phase rules. Existing ranged/battle/adventure consumers share it; real kill indices come from the production resolver. Card scenes separate play from combat confirmation and show cost, power, resistance-adjusted value, required defense/armor, kills, fame and wound-card totals.

Instinct restores its missing original text, red cost and four mutually exclusive choices. Earth Strength restores exclusive movement/healing/block choices and unmodified day/night terrain block. Focus restores its green-crystal choice. Cold Toughness contributes ice block and resolves its conditional bonus against the actual enemy when blocking. These effects validate choices before payment. Crystal rewards store at most three per basic color and convert overflow into same-color tokens, following the printed rulebook.

Druidic Paths now distinguishes a chosen hex from a chosen terrain type and uses the real movement-cost/pool service. Intimidate grants a paid ready action limited to owned level-1/2 units. Unit readiness and wound cards are independent: readying does not heal, healing does not ready, and starting the next turn does not ready spent units. Actual adventure consumers check CanActivate. Turn-end clears the new temporary discounts and ready permission. Each downstream move, ready or turn-end has a separate scene confirmation and numeric checks.

## How to test

Use the installed Unity 6000.6.0f1 editor via Hub. Run Tools/Mage Knight/Original Cards/Run All Batches, Tools/Mage Knight/Adventure/Run All EditMode Tests, and Tools/Mage Knight/Adventure/Run All Four. No batchmode or package changes.

Reproduce source corrections with `python tools/restore_spell_costs.py` and `python tools/restore_instinct_card.py`, and generated fixtures with `python tools/build_all_card_verification.py`. Summarize the exact new directory with `python tools/summarize_all_card_verification.py --output AutomationOutputs/AllOriginalCards/<run>`. Verify regressions with `python tools/verify_card_regression.py --scenes <adventure-run> --tests <editmode-run>`.

Latest full-suite evidence: AutomationOutputs/AllOriginalCards/20260910_162121_0c45bf3c. 145 EditMode tests pass. 413 scene cases: 123 passed, 31 failed and 259 partial; 1308 pointer actions, 4956 effect assertions and 490 captures. All 383 previous cases remain, with unchanged numeric expectations. Two Druidic Paths checks observe real selected-hex cost (still 2), replacing an incorrect global-override field contract; test_contract_corrections.json records the change and added counterexamples. Three old failures are repaired with no new regressions; all assertions in 30 added cases pass. Full card certification is still pending.

The final UI-only change labels the fixture as the state before the current operation. Its separate 183-case basic-card recheck retains identical assertions and actual states, with 239 supplementary captures, without combining batches into a fabricated full run. Four adventure regressions pass 114 actions and 477 assertions; all 118 captures are byte-identical to the previous archive. Two final Unity Console scans return zero errors. All 847 full-suite/recheck/regression captures and test XML/JSON are archived with verification_provenance.json.

## Risks

This is an intermediate repair, not full original-card certification. Remaining failures and partial coverage are explicit. Wild-mana source selection, full action transactions after target/effect failure, lifecycle integration, targets, downstream effects, unit and skill coverage still require work. AdventureSession has a separate presentation card-instance collection; current regression passes, broader lifecycle integration is still pending. Intermediate checkpoints remain local; final reports and all final card captures and 118 regression captures are archived.

## Related issues

User request: continuously iterate until every original card passes in-scene validation. No issue number supplied. Remote PR creation/merge is pending authenticated GitHub access; the branch can be archived over SSH 443.
