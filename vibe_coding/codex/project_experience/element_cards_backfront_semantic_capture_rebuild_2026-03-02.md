# ElementCards back/front semantic capture rebuild (2026-03-02)

## Problem
Automation artifacts had visual contamination (state-label remnants and top-region damage) that broke back/front semantic review, even when report status was success.

## What worked
1. Treat artifact semantics separately from runtime status
- Verify each required step image directly (001/003/005/007 back-only; 002/004/006/008 correct element front).

2. Keep back-state hard rule explicit
- No readable front text, no title strip, no front iconography in back captures.

3. Preserve compile guardrail independently
- Run two explicit Unity console scans for error only.
- Keep compile_status payload strict and identical in both required paths.

## Operational guidance
- If MCP transport is unstable, use short recovery windows for mandatory console scans first.
- Do not ship screenshot artifacts with any top-overlay masking that clips card geometry.
- Keep screenshot naming and report step mapping unchanged to avoid downstream parser mismatches.
