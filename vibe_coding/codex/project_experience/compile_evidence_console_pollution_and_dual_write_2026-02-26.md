# Compile Evidence: Console Pollution and Dual-Write Guardrail (2026-02-26)

## Context
During compile verification, a failed menu execution (`Assets/Refersh`) can inject non-compile error logs into Unity Console. If not isolated, this can break `compile_ok` evidence even when C# compilation is clean.

## What worked
1. Run `refresh_unity(wait_for_ready=true)` before compile scans.
2. If operational/tooling errors appear in error logs, clear console first, then re-run refresh.
3. Perform two explicit error scans with strict query:
   - `types=["error"]`
   - `count=1000`
   - `include_stacktrace=true`
4. Write one identical structured JSON payload to both required paths:
   - run root `compile_status.json`
   - `review_bundle/artifacts/compile_status.json`
5. Ensure both `scannedAtUtc` values are later than automation report `finishedAt`.

## Pitfalls
- Relying on a possibly missing menu item for compile trigger can create false error entries.
- Writing only one compile status file causes reviewer gate failure.
- Minimal payload like `{\"ok\":true}` is rejected by gate checks.

## Reusable checklist
- [ ] Refresh Unity and wait ready.
- [ ] Run scan #1 (`error`, `count=1000`, stacktrace).
- [ ] Run scan #2 (`error`, `count=1000`, stacktrace).
- [ ] Confirm both scans are zero errors.
- [ ] Build structured payload (`ok`, `scans[2]`, query, `errorCount`, `errors`).
- [ ] Dual-write payload to both expected paths.
- [ ] Verify file hashes are equal.
