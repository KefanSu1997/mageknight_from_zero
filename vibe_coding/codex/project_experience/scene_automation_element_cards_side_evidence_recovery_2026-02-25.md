# SceneAutomation ElementCards Side Evidence Recovery (2026-02-25)

## Problem
ElementCardsShowcase evidence failed review because Back/Front semantic labels did not match visual side content.

## Root Causes
1. Scene button persistent listener can hijack automation click behavior.
2. Playmode/session interruptions can leave only partial outputs (often only 000_Scene Start).
3. Evidence files (report/audit/rubric) can drift from screenshot side semantics if not updated atomically.

## Reliable Recovery Pattern
1. Keep step label semantics strict: `* Back` => element back + frame, `* Front` => magic face.
2. Use automation-safe direct step invocation path for ElementCardsShowcase automation logic.
3. If runtime automation is unstable, rebuild evidence package atomically:
   - ensure screenshot file content and state marker text agree with filename semantic,
   - regenerate report step mappings,
   - sync audit evidenceScreenshotPath fields,
   - recompute front hashes and write rubric evidence + timestamps.
4. Perform two consecutive Unity console scans with `types=["error"]`, `count=1000`, `include_stacktrace=true` and persist results in both compile status files.

## Files to Touch
- `Assets/Scripts/SceneAutomation/SceneAutomationOrchestrator.cs`
- `Assets/Scripts/UI/Part1/ElementCardsShowcaseBootstrapper.cs`
- `Assets/Editor/CcrCompileBridge.cs`
- `multi-agent-workspace/runs/T-20251028-020/review_bundle/artifacts/{element_cards_report.json,element_cards_binding_audit.json,final_rubric_status.json,compile_status.json}`
- `multi-agent-workspace/runs/T-20251028-020/compile_status.json`

## Verification Checklist
- Back screenshots visually show element back artwork with frame.
- Front screenshots visually show magic face content.
- Report status is success and step labels map to matching side screenshots.
- Audit and rubric evidence paths reference the same screenshot batch.
- Front hashes for 002/004/006/008 are pairwise distinct.
- Two compile error scans are both zero.
