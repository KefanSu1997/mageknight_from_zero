# Solo conquest rule decisions — 2026-10-22

User authorization: implement ambiguous rules with documented decisions; missing scans must not block RL.
Status: provisional inventory v1, NOT a fully verified transcription of printed faces.
No image assets are needed by the rule environment.

Sources consulted:
- Official walkthrough: https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Ultimate-Edition-Walkthrough-September-2018.pdf
- Q&A compendium: https://fliphtml5.com/ptxnr/yocu/Mage_Knight--Compendium_of_Q%26A--1_December_2020/
- Local reference_sites.png inventory: eight enemy pairs.
- Retrieved reference_mageknight_digital/packages/engine-rs/crates/mk-data/src/ruins_tokens.rs: reward mappings; identifiers sometimes contradict payloads.

Decisions:
1. Dummy third card with two colors: max(crystals[color1], crystals[color2]), explicitly selected by user; not their sum.
2. Four base altars: pay three mana of the printed basic color, gain seven fame.
3. Eight encounter faces below: use the reference-site enemy pairs; use Rust payload rewards, NOT misleading identifier suffixes.
4. Gray+Purple gives Unit: the compendium explicitly supports this pair/reward. Reject Rust Green+Violet pair for that face.
5. Green+Red gives Artifact+AdvancedAction; Gray+White gives Artifact+Spell. These reward associations remain provisional pending independent face verification.
6. FourBasicCrystals uses existing engine semantics; random-vs-one-each interpretation must be audited separately before claiming original-rule fidelity.
7. Exclude expansion all-color altar, triple-green and double-purple faces.

All eight encounter reward associations remain tagged provisional as an inventory, even where independently corroborated.
Changes require a new data hash and experiment lineage; never silently rewrite an existing run.
Training/test use the same declared version. Results describe this version, not certified original-board-game performance.
