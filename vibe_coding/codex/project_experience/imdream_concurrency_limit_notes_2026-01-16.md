# Imdream concurrent limit handling (2026-01-16)

## Issue
- API returned code 50430: Request Has Reached API Concurrent Limit.

## Resolution
- Submit one task at a time.
- Wait for the current task to reach status=done via repeated imdream_query.ps1 calls (no polling sleep).
- Only after completion, submit the next task.

## Notes
- Avoid --poll in imdream_query.ps1 because it uses Start-Sleep.
- Manual re-query per task keeps each command under 10s and avoids the CLI timeout.
