/**
 * Rule Runner analytics receiver (Google Apps Script web app).
 *
 * Setup (see README.md next to this file):
 *   1. Create a Google Sheet, then Extensions > Apps Script, paste this file.
 *   2. Deploy > New deployment > Web app: Execute as "Me", Who has access "Anyone".
 *   3. Put the /exec URL into Assets/RuleRunner/Analytics/Resources/analytics_config.json ("endpoint").
 *
 * The game POSTs one JSON object per finished run with Content-Type text/plain
 * (a "simple" request, so the browser does not send a CORS preflight).
 */
var SHEET_NAME = 'runs';
var COLUMNS = [
  'receivedAt', 'sessionId', 'build', 'levelId', 'attempt', 'result', 'qiLeft',
  'ruleCount', 'rules', 'steps', 'ranOutOfQi', 'secondsSinceLevelStart', 'timestampUtc'
];

function doPost(e) {
  var lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    var data = JSON.parse(e.postData.contents);
    var sheet = getSheet_();
    var row = COLUMNS.map(function (key) {
      if (key === 'receivedAt') return new Date();
      var v = data[key];
      return v === undefined ? '' : v;
    });
    sheet.appendRow(row);
    return ContentService.createTextOutput(JSON.stringify({ ok: true }))
      .setMimeType(ContentService.MimeType.JSON);
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({ ok: false, error: String(err) }))
      .setMimeType(ContentService.MimeType.JSON);
  } finally {
    lock.releaseLock();
  }
}

/** Lets you open the /exec URL in a browser to check the deployment is alive. */
function doGet() {
  return ContentService.createTextOutput('Rule Runner analytics endpoint is running.');
}

function getSheet_() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var sheet = ss.getSheetByName(SHEET_NAME) || ss.insertSheet(SHEET_NAME);
  if (sheet.getLastRow() === 0) sheet.appendRow(COLUMNS);
  return sheet;
}

/**
 * Run once from the Apps Script editor (after some data exists) to build the summary tab and charts:
 *   - average runs per level (attempts until the player stopped / cleared)
 *   - failure reasons per level (stacked)
 */
function buildSummary() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var rows = getSheet_().getDataRange().getValues();
  var header = rows.shift();
  var col = {};
  header.forEach(function (h, i) { col[h] = i; });

  var outcomes = ['win', 'pit', 'spike', 'trap', 'timeout'];
  var levels = {};
  rows.forEach(function (r) {
    var id = String(r[col.levelId]);
    if (!id) return;
    var L = levels[id] || (levels[id] = { runs: 0, sessions: {}, results: {} });
    L.runs++;
    L.sessions[r[col.sessionId]] = true;
    var res = String(r[col.result]);
    L.results[res] = (L.results[res] || 0) + 1;
  });
  var ids = Object.keys(levels).sort();

  var avgTable = [['level', 'avg runs per player']];
  var resultTable = [['level'].concat(outcomes)];
  ids.forEach(function (id) {
    var L = levels[id];
    avgTable.push([id, L.runs / Object.keys(L.sessions).length]);
    resultTable.push([id].concat(outcomes.map(function (o) { return L.results[o] || 0; })));
  });

  var summary = ss.getSheetByName('summary') || ss.insertSheet('summary');
  summary.clear();
  summary.getCharts().forEach(function (c) { summary.removeChart(c); });
  summary.getRange(1, 1, avgTable.length, 2).setValues(avgTable);
  summary.getRange(1, 4, resultTable.length, outcomes.length + 1).setValues(resultTable);

  summary.insertChart(summary.newChart().asColumnChart()
    .addRange(summary.getRange(1, 1, avgTable.length, 2))
    .setNumHeaders(1)
    .setPosition(avgTable.length + 3, 1, 0, 0)
    .setOption('title', 'Average runs per player, by level')
    .build());

  summary.insertChart(summary.newChart().asColumnChart()
    .addRange(summary.getRange(1, 4, resultTable.length, outcomes.length + 1))
    .setNumHeaders(1)
    .setStacked()
    .setPosition(avgTable.length + 3, 8, 0, 0)
    .setOption('title', 'Run results by level (win / pit / spike / trap / timeout)')
    .build());
}
