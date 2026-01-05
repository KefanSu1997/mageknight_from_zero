# Deck Ideal traceability + automation evidence checklist (2026-01-05)

## Goal
- Ensure art candidates are fully traceable even when generated offline.
- Produce a verifiable automation evidence chain (report.json + captures + after screenshot).

## Offline traceability fields (per image)
- Generation type: local_procedural (no external Task ID).
- Task ID: none.
- Prompt: N/A (state target style keywords).
- Parameters: width, height, seed, style, any other generator flags.
- Command line: full invocation used to generate the image.
- Output path: exact file path on disk.

## Automation evidence checklist
- Scene automation config path recorded in worklog.
- Output directory recorded in worklog.
- report.json exists and status == success.
- captures directory contains initial view and step images (if steps are configured).
- Copy or rename a clear "after" screenshot to review_bundle/artifacts/screenshots/ with the required name pattern.
