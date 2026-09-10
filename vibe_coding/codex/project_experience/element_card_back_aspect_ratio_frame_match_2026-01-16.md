# Element card back aspect ratio alignment with frame

## Issue
- Fire back showed left/right white bars and harsh edges.

## Cause
- Card back textures were square (2048x2048) or 3:4, while the frame texture is 1600x2400 (2:3).
- When PreserveAspect is off, scaling a square image into a 2:3 rect can expose hard edges and mismatched blending.

## Fix
- Regenerate all element back textures at 1600x2400 to match the frame aspect ratio.
- Add a subtle dark edge vignette in the prompt to blend under the frame.

## Result
- No white side bars; back art blends smoothly with the frame.
