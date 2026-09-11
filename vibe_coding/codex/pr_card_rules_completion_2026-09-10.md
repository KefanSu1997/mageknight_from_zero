# fix(cards): connect original costs, combat, unit damage and crystal sacrifice

## Why

Original card effects often changed counters without reaching the actual card zones, combat or movement consumers. Spells could skip costs; typed combat power could be lost; units could absorb an entire attack incorrectly. Mist Shroud set an unused wound flag and its all-resistance flag did not affect damage.

## What

Use the existing original card assets, images and ActionSystem. Restore printed choices and exact atomic mana costs; resolve healing against actual wound cards; preserve elemental/melee/ranged/siege contributions through combat, block triggers and split blocks. Add original unit ability/recruitment validation, round-based readiness, ordered unit damage, resistance, poison and paralysis. Preserve original hero skill identity and activation timing.

Mist Form now connects all-unit resistance and the hero’s first enemy-wound protection to the production damage resolver. Ignored wounds do not trigger poison/paralysis; later wounds still do. Battle end preserves turn effects, while turn end clears mist protection and temporary terrain overrides/bans. Invalid options reject before payment.

Scene suites use original SOs and artwork, independent expectations, real pointer hits, explicit follow-up operations, actual state/zone records, PNG captures, TMP overflow checks and suite hashes. Unique output directories and atomic reports/config writes avoid stale data and mapped-file truncation failures. This branch retains known failures and partial coverage.

Sacrifice now discards the actual selected original-card instances, rewards their original colors, allows each artifact to choose its crystal color and validates the whole selection before payment. Strong Sacrifice pays the spell cost first, converts every remaining selected crystal pair into typed elemental siege/ranged power and returns matching mana tokens.

## How to test

For routine iterations, select only affected cards/cases with `python tools/build_focused_card_verification.py --cards magic_011` or `--cases <IDs>` and run Unity menu `Tools/Mage Knight/Original Cards/Run Focused Cases`. Verify its exact output with `tools/verify_sacrifice_scene.py --output <run>` and `tools/summarize_all_card_verification.py --output <run>`. Use `--failed-from <effects.json>` to remove completed checks from the daily queue; the full regression library stays intact. Never use stale or incomplete reports to prune the queue.

Regenerate the full case library only when fixtures change: `python tools/build_all_card_verification.py`. Run only necessary EditMode/scene regressions for the touched behavior; full-batch, unit and four-adventure regressions require an actual impact reason. Open through Unity Hub, never batchmode or direct Unity.exe.

Current focused result `20260911_213849_0b76d08a`: Sacrifice58 cases (old2 failures +new56),197 pointer actions,1411 effect assertions and83 captures passed. Source/printed text, actual zones, payment, fortification/resistance and screenshot readability verified. All58 completed checks now leave an empty daily queue; whole-card status still partial. Core EditMode260 passed, and actual Unity Console twice zero after the focused run. The earlier broad spell run was explicitly stopped following the user's minimum-necessary instruction and is not counted as completed evidence.

Historical baseline `20260911_205758_b83addf3` remains550 cases with142 passed/390 partial/18 failed. Old550 definitions were retained when extending to606. This focused run fixes the two Sacrifice failures; it is not a fresh full606 regression. Exact evidence and remaining scope: `vibe_coding/codex/worklog_2026-09-11_sacrifice_resolution.md`.

## Risks

All-original-card completion remains unproven. Full card-zone lifecycle, unsafe-hex forced retreat, combinations, complex units and remaining skills require continued scene verification. Incomplete artifact modules and invalid source records remain explicitly reported. Do not infer whole-card certification from UI completion or passing immediate checks.

Remote upload is paused after VPN quota exhaustion. Source, concise summaries and hash indexes are committed locally; full captures/raw reports stay on disk. Review pending LFS bytes before any future push. No main or force push.

## Related issues

User-requested continuous verification of every valid original card; no issue number supplied. PR publication/merge remains pending; this is the reviewable local description.
