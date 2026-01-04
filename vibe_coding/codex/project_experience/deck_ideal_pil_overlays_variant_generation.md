# Deck Ideal PIL Overlay Variant Generation

## Context
- Network image generation was unavailable, so a local PIL pipeline was used to create dark fantasy variants.

## What worked
- Use existing deck UI textures (magic circle, highlight, border, background) as overlays, then tint to palette colors.
- Layer linear + radial gradients for depth, then add starfield and swirl arcs for starlight/magic motion.
- Reuse alpha channel from the current asset to preserve silhouette and avoid layout shifts.

## Practical tips
- Keep outputs RGBA and match exact sizes (board 1378x1204, face 1024x1024, back 640x880).
- Use low opacity for border/highlight overlays to avoid washing out the center.
- Generate 3 palette variants (blue, violet, emerald) and select based on rune contrast.

## Notes
- Record tool chain and outputs in review_bundle/extra_context.json for traceability.
