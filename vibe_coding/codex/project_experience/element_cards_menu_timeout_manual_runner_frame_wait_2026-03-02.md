Title
- ElementCards menu automation timeout: stabilize mapping evidence and avoid realtime-based screenshot waits

Context
- Scene: `Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity`
- Task required strict Earth/Water semantic correctness and real menu-based automation evidence.
- Repeated runs showed playmode watchdog timeout before full step completion.

Symptoms
- Unity log repeatedly showed:
  - `[SceneAutomation] Runner started`
  - then no full step chain, followed by
  - `Automation exceeded 180.0s` and `Automation timed out`.
- MCP session frequently dropped (`no_unity_session`, `hint=retry`) during playmode windows.

What worked
- Centralized mapping source in bootstrapper:
  - one mapping table drives both card binding and step sequence.
  - this prevents Earth/Water drift between setup and automation labels.
- Runtime evidence logs:
  - emit `element -> frontPath -> frontSprite -> titleHint`.
  - helps prove semantic binding even when screenshot rerun is unstable.
- Compile evidence guardrail:
  - clear console pollution first.
  - run dual `types=["error"]` scans.
  - dual-write identical `compile_status.json`.

What did not work reliably
- Realtime-based waits in unstable playmode windows can stall long enough to hit watchdog.
- Frequent MCP interactions during active playmode increase session drop risk and can interrupt automation progression.

Practical guardrails
- Prefer frame-bound waiting for screenshot file existence in manual capture routines.
- Keep ElementCards automation waits at zero and reduce capture-path complexity.
- During one automation pass, avoid extra MCP traffic until report timestamp updates.
- If session drops occur, recover with:
  - `refresh_unity(wait_for_ready=true)` before each retry
  - active exponential retry pattern
  - session rebind/restart after consecutive failures.

