# Rule Runner analytics (Google Sheets)

The game sends one JSON row when a level opens and one when a run finishes (`AnalyticsTracker`, design doc section 7):

| field | example |
|---|---|
| sessionId | random id, new every time the game is opened |
| playerId | random id kept in browser storage: the same person across reloads (recorded only; the summary still counts sessions) |
| build | `beta-playtest`; `beta-playtest-internal` for team testing (Editor, localhost, or a URL with `?internal=1`) |
| eventType | `level_start` (only session, build, level and attempt are filled) or `run_end` |
| levelId | `1-4` |
| attempt | which run on this level in this session |
| result | `win` / `pit` / `spike` / `trap` / `timeout` |
| qiLeft | `3` |
| ruleCount | `2` |
| rules | `IF pit THEN bigjump; IF NOT ground THEN jump` |
| steps | `18` |
| ranOutOfQi | `false` |
| secondsSinceLevelStart | `74` |
| hintsUsed | `1` (one-line hints opened on this level before the run) |
| endX | `5`: tile where the run ended (the death tile on a failed run); `-1` on `level_start` |
| endFloor | `0` upper floor, `1` lower floor; `-1` on `level_start` |

Rows from builds before Beta have no `eventType`, `endX`, `endFloor` or `playerId`; they count as `run_end`. When `Code.gs`
gains columns, the header row of the existing `runs` tab is extended on the next request.

## Setup (about 5 minutes)

1. Create a Google Sheet (any name).
2. **Extensions > Apps Script**. Replace the code with [`Code.gs`](Code.gs) and save.
3. **Deploy > New deployment**, type **Web app**:
   - Execute as: **Me**
   - Who has access: **Anyone**
4. Copy the `/exec` URL into `Assets/RuleRunner/Analytics/Resources/analytics_config.json`:
   ```json
   { "enabled": true, "endpoint": "https://script.google.com/macros/s/XXXX/exec", "build": "beta" }
   ```
5. Rebuild WebGL (`Rule Runner > Build WebGL Beta`) and push.

With an empty `endpoint` the game only prints each record to the browser console (`[Analytics] {...}`).

## Charts

After some play-testing, open the Apps Script editor and run `buildSummary`. It rebuilds the `summary` tab
(tables and charts) from rows whose build starts with `SUMMARY_BUILD_PREFIX` (`beta`). `-internal` rows (team testing)
are counted while `INCLUDE_INTERNAL` is `true`; set it to `false` to see outside players only:

- **M1 Level completion funnel**: per level, % of sessions that opened it and % of openers that cleared it.
- **M2 Runs to clear**: runs until the first win, per player (candlestick of min / Q1 / Q3 / max; median in the table).
- **M3 Repeat-mistake rate**: % of failed runs that end with the same cause on the same tile and floor as the
  player's previous failed run on that level; levels above 40% are flagged.
- **M4 Solution diversity**: share of winning runs by exact ordered card list (top set, 2nd set, others), chapter levels only.

Charts only show levels with at least `MIN_SAMPLE` (3) data points; the tables list every level with its count,
and a `build` table shows how many rows each build tag has and whether it was used.

The calculations live in `computeMetrics_`, which makes no Sheets calls, so it can be tested with Node.

## Notes

- The request uses `Content-Type: text/plain` on purpose: JSON content type would trigger a CORS preflight
  that Apps Script does not answer.
- Redeploy (Manage deployments > edit > new version) after changing `Code.gs`; the URL stays the same.
