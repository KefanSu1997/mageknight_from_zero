# Experience: Save Scene Before Automation (2026-01-28)

## Problem
- Running scene automation after unsaved hierarchy edits caused the scene to reload from disk and lose FrontRoot/BackRoot changes.

## Fix
- Apply hierarchy edits via MCP.
- Save the scene immediately with manage_scene(action="save").
- Only then run Play Mode or scene automation.

## Takeaway
- In this project, Play Mode/automation can reset unsaved scene edits. Always save before automation.
