# Element card back dark band removal (Pillow crop)

## Issue
- Imdream card back image contained a dark horizontal band at the bottom that was visible in UI screenshots.

## Diagnosis
- Use Python + Pillow to compute per-row mean luminance and detect a low-luminance tail region.

## Fix
- Crop the bottom rows where mean luminance < 0.7 * overall mean.
- Resize the cropped image back to the original dimensions to preserve UI layout.

## Notes
- This is a post-process on the existing Imdream output (no re-generation required).
- Keep preserveAspect disabled on the Image to avoid letterboxing from aspect mismatch.
