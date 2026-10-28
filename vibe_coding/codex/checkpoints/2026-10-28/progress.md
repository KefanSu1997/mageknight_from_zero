# Project checkpoint — 2026-10-28

Requested by the user before further UI iteration. Previous HEAD: 4584803 (2026-09-11); branch feat/card-rules-completion had nine local commits ahead of its tracking branch before this checkpoint.

## Saved implementation

- Current Unity rules, combat demo, scene automation, original data, tests, RL tooling and RSI harness.
- Battle UI attempts through attempt63: independent character art, facing corrections, symbol-based battlefield, on-demand full readers, mana inventory and card-mode mounts.
- Rubric combat_focus_v3.2.0 adds explicit icon/numeral separation from scenery and recognizability gate at item 2.2.
- Automatic check results, screenshots, independent review outputs and compilation receipts stay local at the evidence root below, as requested by the user. They are excluded from this Git checkpoint. Code, product artwork, evaluation definitions and progress documentation are saved.

## Validation and unfinished work

- Last bound Unity receipt: runtime MVID e060f127-5e8b-49ab-8f06-884371143ae6, compilation errors 0, warnings 74. Receipt source SHA values are archived; checkpoint does not claim whole-project test acceptance.
- Latest six-phase capture is payment_verified. Previous independent local review rated five scenes 4 and card scene 3 on rubric 2.2; later payment-label changes still need their own independent review.
- Previous utility revision verified full readers, guide return with unchanged native state, and actual guard +4 block followed by exhaustion. These results must not be silently reused as validation of a newer source version.
- Latest validation/report completion, wider hero/phase coverage, remaining facing conflicts and visual polish remain open. This is a progress checkpoint, not final UI acceptance.

## Large evidence archive

Original evidence root (local): AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/
History and failed candidates are retained there. The local-only evidence manifest lists copied files and their SHA256; neither that manifest nor its evidence copy is uploaded. Model/trajectory archives and external H: storage remain separately managed and are not implied to be present in a fresh Git checkout.

## Local Git maintenance

LFS objects were previously moved to H:/MageKnightArchives/20261025_git_lfs_objects/objects through a junction, while the LFS temp directory remained on D:. This broke atomic rename during staging. Local lfs.storage now points to H:/MageKnightArchives/20261025_git_lfs_objects so new temporary and object files share the same drive; existing objects are retained. This machine-specific Git configuration is not required for another clone.

## Follow-up UI work after remote backup

- Current product source: attempt64 party margin fix, compiled MVID d8b7f02e-5daa-4a97-877a-8222c1f0a1d6. Seven identical archived hero entries retain original art/material/UV/dimensions, full-alpha edges now stay on canvas, and projected alpha>=64 overlap does not increase. Hero-only translation initially overlapped two allies; that rejected candidate remains locally archived, final translation preserves the party arrangement. This is not acceptance of all party compositions.
- Current symbolic UI remains visually rejected: independent v3.3 diagnostic rates six scenes 3 for density/grouping and 2 for control design. Crowding visual subscore is 2 for the card view and 3 elsewhere; full crowding item lacks complete hit geometry evidence. Earlier local contrast score4 does not imply an accepted UI.
- Active rubric combat_focus_v3.3.0 has33 items: new4.5 crowding/hit separation; expanded2.4 density and3.4 designed control language. Every visible edit must jointly review2.2/2.4/3.4/4.5. Prompt, output schema, workflow and version-aware verifier align;19 transport/render tests pass. Static screenshots still cannot establish human error, true interaction cost, latency or rules consumption.
- Ten ORIGINAL RSI pre-round1 contexts were recaptured under current product; all ten have exactly equal native state. Anonymous same-rubric independent comparison is prepared; final comparison results remain in local AutomationOutputs and are not part of the code backup.
- Fresh normal hero-selection pilot restored after fixture capture, zero Console errors. UI redesign goal remains active; next priority is semantic grouping, main-action hierarchy, and a cohesive designed visual language rather than repeated yellow primitive mounts.
