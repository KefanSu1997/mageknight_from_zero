# Worklog 2025-12-30 - Imdream PS1 Scripts

## Plan
- Add PowerShell equivalents for Imdream submit/query/upload.
- Update experience index + notes.
- Verify Unity compile errors via MCP console check.

## Progress
- Added tools/generate_imdream_image.ps1, tools/imdream_query.ps1, tools/imdream_upload_ref.ps1.
- Updated AGENTS.md experience index and added experience note.

## Next
- Run Unity console error check (required by repo rule).
- Provide standard flow summary to user.

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Fix
- Corrected PowerShell variable interpolation for requestUrl in both Imdream scripts.

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Fix
- Added Python resolver to use python or py executables for signing helper.

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Fix
- Load .env values even when env vars exist but are empty.

## Update
- Switched PowerShell signing to native implementation to avoid stdin newline drift.
- Added tools/imdream_auth.ps1 for shared signing utilities (VC3 prefix, optional base64 decode).
- Added tools/imdream_submit.ps1 and tools/imdream_generate.ps1 as Windows entrypoints.
- Updated tools/imdream_query.ps1 to use PS signing and req_json string payload.
- Updated tools/imdream_upload_ref.ps1 to use HttpClient multipart (PS 5.1 compatible) and normalize tmpfiles URL.

## Status
- Submit still returns SignatureDoesNotMatch with current .env AK/SK; likely credential format issue.
- Will run Unity console compile check after Imdream flow is finalized (per user request).

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors

## Update
- Found signature success when sign prefix is empty (no VC3). Added automatic fallback to try "" and "VC3" prefixes.
- Submit/query now succeed and return image_urls when using empty prefix.
- Added "接入说明" section in AGENTS.md (env, entry scripts, sign prefix override, output path).
- Added "限制条件" section in AGENTS.md (input/output caps, URL validity, parameter bounds).
- Added "输入图要求" section in AGENTS.md (public URL requirement, upload helper, format suggestion).
- Expanded Imdream docs: input format/size/ratio limits, output rules, latency/price notes, request specs.
- Added prompt length/symbol guidance and size vs width/height priority rules.
- Added size/ratio rules, recommended resolutions, and scale behavior details.
- Added force_single/min_ratio/max_ratio/seed defaults, group image guidance, and detailed submit/query response notes.
- Added extended business error codes + SDK/HTTP notes; python signer now defaults to empty prefix.
- Verified text-to-image and image-to-image flows; both returned image_urls successfully.
- Downloaded generated images to AutomationOutputs/Imdream/imdream_result_0.png and imdream_result_1.png for review.
- Added fixed-name download support in imdream_generate.ps1 and optional --download-name in imdream_query.ps1.
- Moved official Imdream API notes out of AGENTS.md into project_experience/imdream_api_official_reference.md.
- Simplified AGENTS.md Imdream section to usage-only flow with local download examples.
- Verified OutputName downloads: t2i_magic_tower_0.png and i2i_magic_tower_0.png saved under AutomationOutputs/Imdream/.

## Compile Check (Unity Console)
- Preview logs: 0 entries
- Error check #1: 0 errors
- Error check #2: 0 errors
