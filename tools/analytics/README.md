# Rule Runner analytics (Google Sheets)

The game sends one JSON row per finished run (`AnalyticsTracker`, design doc section 7):

| field | example |
|---|---|
| sessionId | random id, new every time the game is opened |
| build | `beta` |
| levelId | `1-4` |
| attempt | which run on this level in this session |
| result | `win` / `pit` / `spike` / `trap` / `timeout` |
| qiLeft | `3` |
| ruleCount | `2` |
| rules | `IF pit THEN bigjump; IF NOT ground THEN jump` |
| steps | `18` |
| ranOutOfQi | `false` |
| secondsSinceLevelStart | `74` |

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

After some play-testing, open the Apps Script editor and run `buildSummary` once. It creates a `summary`
tab with:

- **Average runs per player, by level**: which levels make people retry most.
- **Run results by level** (stacked): win vs. pit / spike / trap / timeout, i.e. where and how players fail.

## Notes

- The request uses `Content-Type: text/plain` on purpose: JSON content type would trigger a CORS preflight
  that Apps Script does not answer.
- Redeploy (Manage deployments > edit > new version) after changing `Code.gs`; the URL stays the same.
