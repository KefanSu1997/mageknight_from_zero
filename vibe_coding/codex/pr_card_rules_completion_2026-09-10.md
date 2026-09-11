# fix(cards): restore original choices, costs, combat and unit damage

## Why

Healing updated a separate integer without removing wound cards; poison/hero damage could duplicate cards once that count was corrected. Scene auditing stopped with Win32 IO 1224 while truncating an existing memory-mapped report.

Spells skipped base costs, several original spell records had wrong or missing colors, and unaffordable enhanced actions silently executed their base effect. Multi-color payment could deduct the first color before rejecting the second.

## What

Wound counts now come from actual hand/discard cards. Healing and draw-per-heal share one service that uses the actual number removed. Cure and Holy Grail activate their triggers before healing. Each audit run gets a unique directory, immutable per-case checkpoints and a final atomically published effect report.

Exact-color payment validates the full cost before deduction. Spells pay their original top/bottom costs, powered spells enforce night or an explicit artifact exception, and unaffordable actions reject without downgrade. Printed spell costs are restored in JSON and existing SOs. Green crystallization pays ManaColor directly. Scene suites are explicitly imported and identified by SHA256; EditMode and adventure regression runs also use unique output directories.

Combat contributions now retain their element and melee/ranged/siege type, including boosts and conversions. The shared combat calculator fixes physical resistance, aggregate rounding, cold-fire group resistance and fortification phase rules. Existing ranged/battle/adventure consumers share it; real kill indices come from the production resolver. Card scenes separate play from combat confirmation and show cost, power, resistance-adjusted value, required defense/armor, kills, fame and wound-card totals.

Instinct restores its missing original text, red cost and four mutually exclusive choices. Earth Strength restores exclusive movement/healing/block choices and unmodified day/night terrain block. Focus restores its green-crystal choice. Cold Toughness contributes ice block and resolves its conditional bonus against the actual enemy when blocking. These effects validate choices before payment. Crystal rewards store at most three per basic color and convert overflow into same-color tokens, following the printed rulebook.

Druidic Paths now distinguishes a chosen hex from a chosen terrain type and uses the real movement-cost/pool service. Intimidate grants a paid ready action limited to owned level-1/2 units. Unit readiness and wound cards are independent: readying does not heal, healing does not ready, and starting the next turn does not ready spent units. Actual adventure consumers check CanActivate. Turn-end clears the new temporary discounts and ready permission. Each downstream move, ready or turn-end has a separate scene confirmation and numeric checks.

Ice Shield and Burning/Exploding Shield now bind success triggers to their actual block contribution and target. Failed blocks do not grant bonuses; direct enemy effects respect immunity, fire attack rewards retain their element, and duplicate destroy contributions reward a target only once. Flame Wall keeps fire attack/block choices. Mana Bolt preserves all four original color-dependent attack types and pays the printed and extra costs atomically. Sequential scene operations display Chinese result labels, armor changes, payment, kills and wounds.

Nature Force and Wrath Horn now create actual siege contributions. Horn dice retain all six faces at day/night and roll once per chosen bonus; actual rolled colors and wound cards are audited in scenes. Necro Shield splits block into separate contributions for distinct attacks, preserving unused parts and rejecting reuse on the same attack. Strong blocks retain cold-fire. Full multi-attack enemy and artifact lifecycle integration remains pending.

Hero skills now preserve original source identity and printed timing. A shared owned-skill entry validates choices before execution and tracks turn/round uses on the player; reloading the same definition cannot refresh usage. Burning Power retains siege and physical/fire choices; Hot Sword retains physical/fire melee. The original skill sheet is shown in an independent scene suite. The source audit retains 82 skill records and 32 unit records, including two unit backs. The missing army_003 JSON rules have now been restored from its original face; the previous inventory remains a historical audit.

## How to test

Use the installed Unity 6000.6.0f1 editor via Hub. Run Tools/Mage Knight/Original Cards/Run All Batches, Tools/Mage Knight/Adventure/Run All EditMode Tests, and Tools/Mage Knight/Adventure/Run All Four. No batchmode or package changes.

Reproduce source corrections with `python tools/restore_spell_costs.py` and `python tools/restore_instinct_card.py`, and generated fixtures with `python tools/build_all_card_verification.py`. Summarize the exact new directory with `python tools/summarize_all_card_verification.py --output AutomationOutputs/AllOriginalCards/<run>`. Verify regressions with `python tools/verify_card_regression.py --scenes <adventure-run> --tests <editmode-run>`.

Earlier full-suite evidence: AutomationOutputs\AllOriginalCards\20260910_232109_6fc276ee. 194 EditMode tests pass. All 490 existing case definitions remain unchanged: 136 passed, 19 failed, 335 partial; 1617 pointer actions, 6674 effect assertions, 645 captures. Case statuses are unchanged; 488 complete records are identical, with two retained random artifact outcomes differing. Separate new hero-skill scene: 39 cases, 240 pointer actions, 1614 assertions and 164 captures pass (AutomationOutputs\HeroSkills\20260910_232750_4614deb3). Full card certification remains pending.

Four adventure regressions pass 114 pointer actions and 477 assertions. All 118 screenshots and actual state snapshots match the previous verified baseline exactly. Exact final Console responses, test XML/JSON and all captures are archived. Original artwork remains unchanged and intermediate checkpoints remain local.

## Risks

This is an intermediate repair, not full original-card certification. Remaining failures and partial coverage are explicit. Wild-mana source selection, full action transactions after target/effect failure, lifecycle integration, targets, downstream effects, unit and skill coverage still require work. AdventureSession has a separate presentation card-instance collection; current regression passes, broader lifecycle integration is still pending. Intermediate checkpoints remain local; final reports and all final card captures and 118 regression captures are archived.

## Related issues

User request: continuously iterate until every original card passes in-scene validation. No issue number supplied. Remote PR creation/merge is pending authenticated GitHub access; the branch can be archived over SSH 443.


## Original unit batch (2026-09-11, local archive)

Why: original unit data omitted or misread abilities, resistance and recruitment icons; the legacy turn entry readied units every turn. What: restore eight original definitions, load typed paid abilities, validate ownership/readiness/wounds/options before payment, recruit from the actual offer and consume influence, and ready units only at the new-round boundary. Reuse original artwork. Complex abilities, unit damage allocation and complete combat-phase integration remain pending.

How to test: run `python tools/restore_original_unit_data.py` and `python tools/build_unit_verification.py` outside Play Mode, then the Unity menu `Tools/Mage Knight/Original Cards/Run Units`. Verify the exact output with `python tools/verify_unit_scenes.py --output <run>`. The new run is `AutomationOutputs/OriginalUnits/20260911_193332_c99cb476`: 110 cases, 612 operations, 2937 assertions, 213 captures passed. EditMode212 passed. Hero39 cases and adventure4 scenes passed with all actual states and 282 regression images identical to their previous runs. Final Console two zero-error reads are retained. The older 490-deed result is a baseline, not a fresh run or a completion claim.

Risks: this covers eight units' recruitment and active abilities, not all 30 valid unit cards or every rule. Existing failed/partial deed cases and unverified skills remain open. Related issues: user-requested all-original-card verification; no linked issue ID.

Archive policy: remote push is paused following VPN quota exhaustion. Full captures and reports stay local; review and local commit contain source, concise evidence summaries and hashes. Estimate pending LFS transfers before any future push. No main/force push.


## Unit damage batch (2026-09-11, local archive)

Why: a unit incorrectly swallowed an entire enemy attack by taking ceil(damage/armor) wounds, ignoring resistance and damage left for the hero. Paralyzed units continued occupying command slots. What: ordered unit assignments validate actual ownership and legal targets before mutation; each unit absorbs its armor (twice for matching resistance), with one wound or poison's two only when wounded. Paralyze destroys/removes the unit only when it would be wounded. Exhaustion does not prevent damage assignment; even an unwounded resistant unit can receive damage only once per battle. Explicit battle end clears that marker and temporary resistance without healing or readying. Old ForUnitIndex callers use the same remainder calculation.

How to test: the unit generator preserves all 110 old case definitions and adds 46 rulebook-derived damage cases. Reproduce with `python tools/build_unit_verification.py`; use `Tools/Mage Knight/Original Cards/Run Units` and verify the new directory with `python tools/verify_unit_scenes.py --output <run>`. The final run `20260911_201151_0dece5c6` has 156 cases, 865 actual clicks, 6914 assertions and 275 captures passed. TMP text truncation/overflow is asserted after real layout, following a visually failed first run. EditMode231 passed; four adventure scenes passed with 118 identical images; final Console twice zero.

Fresh deed regression `20260911_201543_752adb07`: all490 cases retain 136 passed / 335 partial / 19 failed. The new empty DamageAssignedUnits count is recorded as an additive snapshot field;489 otherwise complete records are identical and one unseeded Horn die outcome differs. This is not full-card completion.

Risks: other22 valid units, positive elemental-resistance matrices, full adventure phase UI and original effects such as UnitsResistAll/FirstWoundIgnored remain to be audited and connected. Direct NatureForce output-to-damage coverage is a module check, not a full paid-card acceptance. Remote push stays paused; only local source and concise evidence commits, with full screenshots/reports retained on disk.
