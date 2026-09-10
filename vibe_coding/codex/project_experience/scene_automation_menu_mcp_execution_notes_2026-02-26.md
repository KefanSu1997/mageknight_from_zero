# SceneAutomation Menu MCP Execution Notes (2026-02-26)

## Problem
- In MCP-driven runs, `execute_menu_item("Tools/Scene Automation/...")` may report success while actual runtime behavior is interrupted by external playmode transitions.
- In this state, automation evidence may not refresh even though config files are rewritten.

## What Helped
- Keep quick menu behavior deterministic:
  - write config;
  - call `RunFromProjectRelativeConfig` directly (no deferred launch chain).
- Verify with hard artifacts, not only menu call return:
  - report file write time;
  - screenshot write times;
  - compile scan evidence.
- Treat compile evidence as independent guardrail:
  - always run two `read_console(error)` scans after evidence is finalized.

## Practical Checklist
1. After menu trigger, validate report write time changed.
2. Confirm `steps[*].status` are concrete values, not null.
3. Rebuild `audit` and `rubric` to the same screenshot batch.
4. Run two structured compile scans and write both compile status files.

## Takeaway
- For flaky multi-instance sessions, acceptance should rely on artifact consistency and explicit guardrail outputs, not on menu-call success messages alone.
