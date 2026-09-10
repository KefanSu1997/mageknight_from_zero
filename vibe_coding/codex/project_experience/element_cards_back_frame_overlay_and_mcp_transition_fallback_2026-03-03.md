# ElementCards Back Frame Overlay + MCP Transition Fallback (2026-03-03)

## Context
- Earth back had visible light edge artifacts.
- Frame assets existed but runtime back-side filtering force-disabled frame rendering.
- MCP session instability caused repeated instance reconnects and stale playmode-transition state during automation reruns.

## What worked
1. Keep one clear white-edge fix path
- Confirm source was opaque light border pixels on Earth back asset edges.
- Use Earth-only overscan crop (`RectMask2D` + inset) as the only fix path.

2. Restore frame as back overlay without fogging center
- Do not clear/hide `Img_Frame` in back-state logic.
- Keep `Img_Back` + `Img_Frame` as allowed graphics on back side.
- Keep frame on top layer; center stays transparent by frame alpha.

3. Preserve compile guardrails under unstable MCP transport
- Clear console noise first.
- Perform two direct `read_console` error-only scans with required query shape.
- Dual-write identical `compile_status.json` to run root and artifacts path.

## Operational lesson
- Under repeated `no_unity_session` reconnects, menu-triggered automation can get stuck with report `running` and 0 steps.
- If fresh rerun is blocked by transport instability, keep artifact contract complete using a clearly documented fallback, then rerun with a stabilized session in the next round.
