# Imdream PowerShell Notes: Signing + Upload

## Key Lessons
- PowerShell pipelines add a trailing newline when piping strings into native commands, which breaks request-body hashes used for signing. Avoid piping JSON directly into the Python sign helper for PS; sign inside PowerShell or pass bytes without newline.
- In Windows PowerShell 5.1, `Invoke-RestMethod -Form` is not available. Use `System.Net.Http.HttpClient` with `MultipartFormDataContent` to upload files.
- Avoid using `Host` as a parameter name in PowerShell functions because `$Host` is a read-only automatic variable.
- Volcengine signature in this project worked only when the signing key prefix was empty; keep a fallback to try both empty prefix and `VC3`.

## Applied Fixes
- Moved signing into `tools/imdream_auth.ps1` (VC3 prefix, optional base64 decode).
- Updated submit/query scripts to use PS signing.
- Updated upload script to use HttpClient and normalize tmpfiles download URLs.
