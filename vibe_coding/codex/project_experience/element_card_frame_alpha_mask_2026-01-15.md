# Frame Sprite Alpha Mask Fix (2026-01-15)

Problem
- Element card flip screenshots showed a dark horizontal band on the back side and the frame was not clearly visible.

Root Cause
- The frame sprite (element_card_frame_imdream.png) was RGB with no alpha, so the center area was opaque and overlaid both front/back.

Fix
- Rebuilt the frame sprite as RGBA and forced a transparent center region, keeping only the border visible.
- This removes the overlay band on the back and keeps the frame readable on both faces.

Implementation Notes
- Used PowerShell + System.Drawing to add an alpha mask.
- Inner rectangle alpha = 0, border alpha = 255.
- Border thickness used: 150 px on each side for the 2272x1824 source image.

Verification
- Unity automation report updated for ElementCardsShowcase run on 2026-01-15.
- Compile check: read_console errors = 0.
