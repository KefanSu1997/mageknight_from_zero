# Element card frame canvas sorting (2026-01-14)

## Issue
- Card back could visually cover the frame when flipping, and inactive face still left faint artifacts.

## Fix
- Add a Canvas component to Img_Frame with overrideSorting=true and sortingOrder=50 to force top rendering.
- Hide the inactive face using CanvasRenderer.cull + Clear + SetAlpha(0) plus GameObject.SetActive(false).

## Result
- Frame stays on top of both faces, and the hidden side does not render.
