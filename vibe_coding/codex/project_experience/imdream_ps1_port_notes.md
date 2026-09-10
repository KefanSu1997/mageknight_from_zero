# Imdream PS1 Port Notes

## Context
- Windows needs native PowerShell scripts to avoid Git Bash / WSL dependency.
- Goal: keep the same flow as the Bash scripts (submit -> query -> save images).

## Key Points
- Load .env when IMDREAM_ACCESS_KEY / IMDREAM_SECRET_KEY are not set.
- Keep signing in tools/imdream_sign_helper.py; PowerShell only builds the JSON body.
- Manual $args parsing to support repeated --ref and long options.
- Query supports --poll/--interval/--timeout to wait for binary_data_base64 or image_urls.
- Base64 decoding requires padding before Convert.FromBase64String.

## Pitfalls
- Must include Host / X-Content-Sha256 / X-Date / Authorization headers or signature fails.
- PowerShell alias curl maps to Invoke-WebRequest; use Invoke-RestMethod explicitly.
- Ensure output directory exists before writing images.

## Outputs
- tools/generate_imdream_image.ps1
- tools/imdream_query.ps1
- tools/imdream_upload_ref.ps1
