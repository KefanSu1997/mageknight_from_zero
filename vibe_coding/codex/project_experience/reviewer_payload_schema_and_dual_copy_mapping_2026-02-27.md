# Reviewer Payload Schema Guardrail + Dual-Copy Mapping

## Context
Round gate failed with:
- `Reviewer response missing review JSON payload`

Existing evidence artifacts were mostly complete, but reviewer output parsing failed due to malformed JSON text in the prior response stream.

## What Worked
1. Build a strict machine-readable payload file instead of relying only on free-form response text.
2. Keep payload keys explicit and non-null:
   - `decision`
   - `summary`
   - `roundRubrics`
   - `finalRubrics`
   - `evidenceFiles`
   - `timestamp`
3. Write payload to dual locations for reviewer compatibility:
   - run root
   - `review_bundle/artifacts`
4. Add a dedicated final-rubric evidence mapping file with per-rubric trace metadata:
   - include run id, source json paths, screenshot directory, compile hash pair, mapping timestamp.
5. Add a self-check JSON that validates:
   - parse success
   - required keys present
   - referenced files exist
   - dual-copy SHA match

## Guardrails
1. Avoid markdown-style quoting in JSON-producing responses.
2. Avoid backticks or mixed quote styles inside JSON values that can break parser extraction.
3. If console contains non-compile errors (for example MCP menu failures), clear console before dual compile scans so `compile_status` is not polluted.
4. Keep compile evidence dual-written and hash-checked before generating reviewer payload.

## Reusable Checklist
1. `read_console(types=[\"error\"], count=1000, include_stacktrace=true)` twice -> both zero.
2. Dual-write `compile_status.json` (root + artifacts).
3. Generate `reviewer_payload.json` (root + artifacts).
4. Generate `final_rubric_evidence_map.json` (root + artifacts).
5. Generate `review_payload_self_check.json` (root + artifacts).
6. Confirm all SHA pairs match.

