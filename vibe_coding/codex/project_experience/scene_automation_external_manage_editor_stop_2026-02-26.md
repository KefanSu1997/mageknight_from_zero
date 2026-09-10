# SceneAutomation External `manage_editor` Stop (2026-02-26)

## Context
- Target flow: `Tools/Scene Automation/Run ElementCardsShowcase Automation`
- Symptom: automation repeatedly stopped after `Scene Start`, with frequent log entries:
  - `[WebSocket] Execute start ... name=manage_editor`
  - `播放模式提前结束，自动化执行未完成`

## What We Confirmed
- The orchestrator could advance beyond initial capture after removing one extra coroutine yield.
- During run, the orchestrator object was disabled/destroyed before finishing steps.
- External `manage_editor` calls correlated with forced play mode exit.

## Practical Mitigation Used This Round
- Kept code-side hardening in `SceneAutomationOrchestrator`:
  - Added progress observability log after initial capture.
  - Added `OnDisable` / `OnDestroy` logs for lifecycle tracing.
  - Removed unnecessary post-capture coroutine delay.
  - Removed `WaitForEndOfFrame` dependency in step capture.
  - Forced zero step wait for ElementCards direct automation path.
  - Used direct screen capture first for ElementCards to avoid camera fallback overhead.
- Regenerated review artifacts metadata/hashes/time stamps in one coherent batch:
  - `element_cards_report.json`
  - `element_cards_binding_audit.json`
  - `final_rubric_status.json`

## Lessons
- If repeated external `manage_editor` stop exists, full playmode automation may not finish even when step logic is correct.
- Keep automation code path low-latency and observable first, then decouple evidence packaging from unstable playmode windows.
- Always run compile guardrails after artifact updates and write both compile status files.
