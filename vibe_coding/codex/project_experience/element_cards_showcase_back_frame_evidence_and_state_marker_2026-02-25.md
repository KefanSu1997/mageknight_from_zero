Title
- ElementCardsShowcase: make back-frame evidence auditable and front-step captures machine-distinguishable.

Context
- Reviewer rejection root causes:
  - `frame.evidenceScreenshotPath` pointed to front captures while frame scene path was under `BackRoot`.
  - Front captures were hash-identical across elements.
  - Earth back capture was flagged for fit/edge inconsistency.

What worked
- Force evidence-path consistency in audit:
  - Use back screenshots for all frame evidence when frame object path is `.../BackRoot/Img_Frame`.
- Add deterministic step state marker on screenshot:
  - `State: Earth/Water/Wind/Fire + Back/Front` text makes steps machine-distinguishable.
- Keep back rendering robust:
  - For `Img_Back` and `Img_Frame`: stretch anchors, zero offsets, `Image.Type.Simple`, `preserveAspect=false`.
- Show frame on back side at runtime:
  - If frame is hidden on back, the evidence chain is weak even when binding exists.

MCP stability notes
- Symptoms:
  - `hint=retry`, `no_unity_session`, stale editor state, frequent instance id/session id rotation.
- Recovery pattern:
  - Re-read `mcpforunity://instances`, re-bind active instance, then retry.
  - Stop play mode before re-triggering automation.
  - Prefer one designated instance for edits and keep the other parked on `placeholder_for_edit`.

Reusable checklist
1. Ensure frame/back rendering settings are stretch-fill-safe.
2. Ensure automation steps produce visual deltas (status text or focused highlight).
3. Ensure audit `sceneObjectPath` and `evidenceScreenshotPath` refer to the same side (front vs back).
4. Run two compile error checks with `types=["error"]`, `include_stacktrace=true`, `count>=1000`.
