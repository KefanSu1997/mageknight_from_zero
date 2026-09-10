# Experience: Missing run-root compile_status with unstable MCP session

## Symptom
AutoGate failed because runs/<runId>/compile_status.json was missing even though artifacts had partial evidence.

## Root Cause Pattern
1. unity-mcp session instability (no_unity_session / transport errors) interrupted normal automation->compile evidence sequence.
2. Artifacts could exist while run-root compile_status was absent, causing hard gate failure.

## Reliable Recovery Pattern
1. Restart HTTP dual-instance stack and re-select active instance explicitly.
2. Park interactive instance on placeholder_for_edit to avoid scene reload popup conflicts.
3. Execute compile guardrail scans using read_console:
   - types=["error"], count=1000, include_stacktrace=true
   - run twice and require zero errors both times
4. Build a schemaVersion=2 compile_status payload with two scans and dual-write it to:
   - runs/<runId>/compile_status.json
   - runs/<runId>/review_bundle/artifacts/compile_status.json
5. Immediately hash-compare both files and store match result in artifact_presence_check.json.
6. Ensure report/audit/rubric carry consistent runId + finishedAt + screenshot references.

## Guardrail
Never end a round with only artifacts-side compile_status; run-root compile_status is required for AutoGate acceptance.
