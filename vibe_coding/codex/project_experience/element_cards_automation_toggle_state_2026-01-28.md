# ElementCards automation toggle evidence

Problem:
- SceneAutomationOrchestrator forced ElementCards to front after each click.
- This masked real toggle behavior and made "back" screenshots remain front.

Fix:
- Remove forced SetFront/SetExclusiveFront in automation.
- Read presenter state and write report message as CardName:front/back.

Result:
- Double-click steps now show front then back in report and screenshots.
