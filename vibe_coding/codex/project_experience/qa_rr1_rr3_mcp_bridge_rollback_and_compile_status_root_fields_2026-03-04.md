# QA RR1/RR3 Recovery: targeted rollback + compile_status root contract

## Context
- QA round failed on:
  - RR1: `Assets/Editor/McpHttpBridgeMenu.cs` changed while round note claimed untouched.
  - RR3: `compile_status.json` missed required top-level summary fields.

## What worked
- Use targeted rollback for RR1 file:
  - `git restore --source=HEAD -- "Assets/Editor/McpHttpBridgeMenu.cs"`
  - Verify with `git diff -- Assets/Editor/McpHttpBridgeMenu.cs` (must be empty).
- Rebuild compile evidence with strict root fields:
  - Required top-level fields: `commit`, `errorCount`, `scannedAtUtc`.
  - Keep `scans[]` for two `read_console(types=["error"], count=1000, include_stacktrace=true)` checks.
  - Write identical JSON to both required paths and compare SHA256.

## Pitfalls
- `Assets/Refersh` menu path is invalid; use `Assets/Refresh`.
- Calling `refresh_unity` can fail in some sessions with unsupported-command errors; keep compile proof anchored on `read_console` scans.
- Error scans can be polluted by tool operation errors; clear console before official two-scan evidence window.

## Reusable checklist
1. Audit target file against `HEAD` and rollback if rubric requires no diff.
2. Trigger refresh, then run two error-only scans with stacktrace enabled.
3. Write dual `compile_status.json` with root summary + scans.
4. Validate two files are byte-identical (hash).
5. Export real `git diff` patch for reviewer audit trail.
