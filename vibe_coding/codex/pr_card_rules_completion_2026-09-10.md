# fix(cards): connect original costs, combat, unit damage and mist protection

## Why

Original card effects often changed counters without reaching the actual card zones, combat or movement consumers. Spells could skip costs; typed combat power could be lost; units could absorb an entire attack incorrectly. Mist Shroud set an unused wound flag and its all-resistance flag did not affect damage.

## What

Use the existing original card assets, images and ActionSystem. Restore printed choices and exact atomic mana costs; resolve healing against actual wound cards; preserve elemental/melee/ranged/siege contributions through combat, block triggers and split blocks. Add original unit ability/recruitment validation, round-based readiness, ordered unit damage, resistance, poison and paralysis. Preserve original hero skill identity and activation timing.

Mist Form now connects all-unit resistance and the hero’s first enemy-wound protection to the production damage resolver. Ignored wounds do not trigger poison/paralysis; later wounds still do. Battle end preserves turn effects, while turn end clears mist protection and temporary terrain overrides/bans. Invalid options reject before payment.

Scene suites use original SOs and artwork, independent expectations, real pointer hits, explicit follow-up operations, actual state/zone records, PNG captures, TMP overflow checks and suite hashes. Unique output directories and atomic reports/config writes avoid stale data and mapped-file truncation failures. This branch retains known failures and partial coverage.

## How to test

Generate outside Play Mode: `python tools/build_all_card_verification.py`, `python tools/build_unit_verification.py`, and `python tools/build_hero_skill_verification.py` for the existing skill suite. Open the project through Unity Hub; do not launch Unity directly or use batchmode.

Run Unity menus `Tools/Mage Knight/Adventure/Run All EditMode Tests`, `Tools/Mage Knight/Original Cards/Run All Batches`, `Tools/Mage Knight/Original Cards/Run Units`, and `Tools/Mage Knight/Adventure/Run All Four`. Verify exact fresh output directories with `tools/summarize_all_card_verification.py`, `tools/verify_mist_scene.py`, `tools/verify_unit_scenes.py` and `tools/verify_card_regression.py` using their output/scenes/tests arguments.

Latest evidence on 2026-09-11: EditMode243 passed; deed550 cases (142 passed,390 partial,18 failed),1897 pointer actions,7898 effect assertions,805 screenshots. Mist62 cases all expected checks passed, with full-card status still partial. Original490 case definitions unchanged;487 complete records unchanged, one intended fix and two unseeded random outcomes differ. Unit156 cases/6914 assertions passed. Adventure114 actions/477 assertions passed with118 identical images. Final actual Unity Console error reads twice zero. Exact run paths and visual comparisons: `vibe_coding/codex/worklog_2026-09-11_mist_form_resolution.md`.

## Risks

All-original-card completion remains unproven. Full card-zone lifecycle, unsafe-hex forced retreat, combinations, complex units and remaining skills require continued scene verification. Incomplete artifact modules and invalid source records remain explicitly reported. Do not infer whole-card certification from UI completion or passing immediate checks.

Remote upload is paused after VPN quota exhaustion. Source, concise summaries and hash indexes are committed locally; full captures/raw reports stay on disk. Review pending LFS bytes before any future push. No main or force push.

## Related issues

User-requested continuous verification of every valid original card; no issue number supplied. PR publication/merge remains pending; this is the reviewable local description.
