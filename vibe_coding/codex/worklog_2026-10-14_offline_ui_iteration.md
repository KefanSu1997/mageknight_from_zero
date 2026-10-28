# UI offline iteration, 2026-10-14

Latest user scope: iterate a real UI from historical behavior proportions and saved click paths; calculate new costs offline, provide screenshots, preserve canvas/readability and disclose regressions. No new bulk Unity rollout or timing benchmark.

## Delivered

- Actual Unity implementation: `PveBattleController.FluentUi.cs` and existing Draw/Journey/DemoControls integration. `optimizedInteractionUi` defaults true, switchable to the baseline from the battle menu.
- Direct public-mana access replaces the redundant top-level reset shortcut; original phase reset remains in its menu. Existing source selection, normal/extra quotas, wild color selection and formal payment stay intact.
- Separate ordinary-source-and-return button; original take-and-stay and extra-source buttons remain. Return cancels neither card nor mode. Force-auto-return candidate was rejected because 71 accepted cases gained clicks when taking successive dice.
- End-turn review: six original-sized JourneyHand card targets per page, original card sprites/hover, separate selection-state label so long card names retain full width, keep-all / select-all non-wounds / individual toggle / cancel / confirm. Batch selection changes only the selection list. Final mutation still calls the existing EndTurn and pending site/shared/reward handling. Approved physical-hand/discard references prevent repeated review during the same pending end-turn transaction; a changed hand or selection invalidates approval.
- Actual Unity screenshots: outside, battle, mana, mana return, review, selected review. No new policy training or changes to BattleRL weights/Adam/RNG.

## Offline evidence / calculation

`tools/BattleRl/iterate_ui_cost_design.py` streams 85 immutable result JSONs (~4.82 GB), checks all source SHA256, and reproduces old metrics for all 3,833 accepted fixed samples before rewriting any path. Cache signature binds selected IDs and source SHAs. Every batch rewrite checks the old physical discard slots equal all ordinary hand slots. Original plan/database/source results are read-only.

Outputs: `AutomationOutputs/UiFluency/ui_iteration/`:

- `original_routes.jsonl`, `counterfactual_routes.jsonl`, `route_manifest.json`
- `comparison.json`: baseline plus five candidate revisions; adopted v5
- `behavior_costs.csv`, `all_1009_pair_costs.csv` (976 groups have conditional costs; 33 remain unavailable/unknown, not zero)
- `layout_guards.json`, `text_guard_summary.json`, `rendered_preview_evidence.json`
- `report.html` and actual 1920x1080 PNGs

Historical weighted successful-context means, same 3,833 accepted cases / population mass 835,300.25 (98.2274% of 850,374 transitions):

| Metric | Baseline | Adopted v5 | Reduction |
| --- | ---: | ---: | ---: |
| B clicks | 3.031930 | 2.553375 | 15.7838% |
| A final click through B complete, px | 2387.842548 | 1832.487277 | 23.2576% |
| A+B view changes | 1.690863 | 1.565466 | 7.4150% |

Do not sum A+B metrics over adjacent transitions to claim episode totals. Distance includes the pure bridge and B internal motion once. First actions without a predecessor are outside this transition mean. These are counterfactual planned costs, not measured new-UI performance or human-time predictions.

Source→play: historical 19,683 transitions, four accepted sample contexts cover 98.92% of this group's weight. Old pair clicks7.3893→5.7785; A internal + transition distance3806.893→3711.242px; transition-only2424.036→1996.058px. Do not report transition-only reduction as the reduction of the whole two-step route, or equate every play in this group with enhancement.

## Guards and limitations

- 1920x1080 remains fixed. Existing card, enemy, text sizes stay unchanged. Review uses288x284 targets,156x225 card art and316x454 hover, matching JourneyHand. New buttons21px, body22/23/25px, title36px, existing-style page14px.
- Atlas checks7,083 existing relevant layouts: no new shortcut/return control overlap. Main-page additions apply only when those base end-turn controls are visible; occluding modal controls are not treated as simultaneous active base controls. Review controls and separate rule/resource text areas do not overlap. Two860x70 return controls replace one1760x70 control; height/font unchanged, ample label space retained.
- Six rendered states exported actual TMP font/overflow fields: no reported overflow. Dynamic execute_code is invoked via execute_custom_tool, not as a top-level tool. A same-filename image preview showed stale/partial text; raw PNG pixel inspection and a unique adopted_review.png copy displayed all text. No runtime missing-text fault was established; the speculative deferred TMP-refresh workaround was removed. Preserve separate audit and screenshot provenance; use unique adopted_*.png paths for final review.
- Unity compile/Console error and warning counts zero after fixes. Initial missing Collections.Generic import failed compilation; corrected, failed preview directories retained.
- All fixed cases have non-increasing B click count, but391 individual contexts increase transition distance. Aggregate per-kind distance increases for block/blockSelected/puppetKeep/puppetUse/soulGuideBlock, at most about31px. They remain in CSV and report.
- nextTurn macro discards all remaining ordinary cards, and its mean is not generic human end-turn cost. A+B view changes for this kind2.73→2.88. The initial design added one confirmation to human keep-all end turns. This regression was fixed with an explicit keep-hand-and-end direct entry: Outside header1564,108,306x64, battle End-phase header1430,139,450x43. It clears discard selections explicitly, retains hand, and calls the unchanged native end-turn path; pending hand/discard references prevent another review during the same transaction. This branch still takes1 click and is not counted as extra savings in the historic all-discard macro. Local distance-increasing branches remain disclosed.
- Original UI incompatibility/refusal mass1.22% and unknown0.55% are still excluded from successful-context means, never zero-filled. No claim of all dynamic contexts, all original card rules, player search/decision burden, or measured responsiveness acceptance.
