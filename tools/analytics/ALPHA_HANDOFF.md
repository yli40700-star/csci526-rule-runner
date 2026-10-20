# Alpha analytics handoff

## Current collection

One record is sent when a run finishes. An empty endpoint means console logging only, not Google Sheets collection.

| Metric | Available fields | Interpretation |
| --- | --- | --- |
| Run outcomes | `levelId`, `result`, `ranOutOfQi` | Outcomes include win, pit, spike, trap and timeout. Running out of qi is a separate flag, not an exclusive outcome. |
| Attempts | `sessionId`, `levelId`, `attempt` | Session ID resets when the game opens. This identifies sessions, not unique people. Existing summary divides recorded runs by sessions, including sessions that never won. |
| Hint usage | `hintsUsed` | Cumulative hints opened on the level before this run. Do not sum repeated cumulative values as independent clicks. |
| Remaining qi | `qiLeft`, `result`, `levelId`, `build` | Compare successful runs only when measuring solution efficiency. An optimal-solution benchmark for the same level version is still needed. |

`secondsSinceLevelStart` is elapsed time since opening the level, not the duration of an individual run. Records are not automatically retried after a failed upload.

## Release checks

1. Deploy the spreadsheet-bound `Code.gs` as a web app and record its `/exec` URL.
2. Set the URL and an Alpha build identifier in `analytics_config.json`.
3. Build using **Rule Runner > Build WebGL Alpha** (Unity 6000.6.0f1).
4. Publish through a reviewed feature-branch PR, preserving `docs/beta/`.
5. Run both a losing and winning attempt on the published Alpha. Match their session, level and outcome to new spreadsheet rows.
6. Run `buildSummary` and verify the charts against those rows. Keep setup tests separate from actual playtest analysis.
7. Share the sheet with the team using their confirmed Google accounts.
8. After the team's final source changes are merged, rebuild and verify before tagging the resulting published commit `alpha-final`. Do not tag the preliminary playtest build as final.
