# ElementCards back title-strip leakage + manual-capture runner state (2026-03-02)

## Context
While fixing `Part1_ElementCardsShowcase` back/front exclusivity, back screenshots still showed front-only title strips.
At the same time, repeated manual-capture retries could enter a state where capture logs show `Started` but no new `Report written` output.

## What worked
- Dual compile guardrail remained reliable: clear console + two `types=["error"]` scans.
- Manual capture path (`Queue ElementCards Manual Capture`) can generate fresh source-path report/screenshots when session state is healthy.
- Reading `Editor.log` gave the most reliable runtime truth when MCP console was unstable.

## What did not fully work
- Presenter-level root toggles alone did not remove title-strip leakage.
- Repeated play-mode/manual-capture retries can leave runner effectively stalled (no final report write).

## Practical lessons
1. For back-side visual regressions, verify actual runtime sprite binding (`Img_Back`, `Img_Frame`) instead of trusting root active states.
2. If manual capture stops writing reports, inspect `Editor.log` for `Started`/`Report written` sequence before trusting menu success.
3. Treat `_mcp_worker` and source project artifact paths separately; mirror runs can hide output freshness if only source path is checked.

## Recommended next fix order
1. Log and assert runtime sprite identity for `Img_Back` and `Img_Frame` each automation step.
2. Hard-remove/replace legacy top-band/frame renderers in back mode if still present.
3. Add a runner reset guard to prevent re-entry/stuck manual-capture state between retries.
