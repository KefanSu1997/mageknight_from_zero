# Evidence Metadata Closure (RR3/RR4) - 2026-02-27

## Context
Round rejected due metadata nulls and missing dual-copy compile_status, even though visuals and automation screenshots were valid.

## What worked
1. Treat evidence closure as a strict schema contract, not a best-effort log.
2. Populate run-scoped identity everywhere:
   - runId
   - status
   - startedAt/finishedAt
   - reportPath/screenshotsDirectory
3. Always dual-write compile_status to root and artifacts with identical payload bytes.
4. Add artifact_presence with machine-readable keys:
   - checkedAtUtc
   - required { report, audit, rubric, compileRoot, compileArtifact, shot000..shot008 }
5. Include compile_status SHA256(root/artifact) and match=true in presence file.

## Guardrails
- After writing metadata, run machine checks for:
  - null field detection
  - scan timestamps > report.finishedAt
  - compile_status SHA equality
  - required flags all true
- Keep all paths inside current run root to avoid cross-round leakage.

## Result
AutoGate-relevant artifacts are self-contained and machine-verifiable without external console context.
