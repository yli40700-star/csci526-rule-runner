/**
 * Rule Runner analytics receiver (Google Apps Script web app).
 *
 * Setup (see README.md next to this file):
 *   1. Create a Google Sheet, then Extensions > Apps Script, paste this file.
 *   2. Deploy > New deployment > Web app: Execute as "Me", Who has access "Anyone".
 *   3. Put the /exec URL into Assets/RuleRunner/Analytics/Resources/analytics_config.json ("endpoint").
 *
 * The game POSTs one JSON object per event with Content-Type text/plain
 * (a "simple" request, so the browser does not send a CORS preflight):
 *   - eventType "level_start" when a level opens;
 *   - eventType "run_end" when a run finishes (rows from builds before Beta have no eventType and are runs).
 */
var SHEET_NAME = 'runs';
var COLUMNS = [
  'receivedAt', 'sessionId', 'build', 'levelId', 'attempt', 'result', 'qiLeft',
  'ruleCount', 'rules', 'steps', 'ranOutOfQi', 'secondsSinceLevelStart', 'timestampUtc', 'hintsUsed',
  'eventType', 'endX', 'endFloor', 'playerId'
];

/** buildSummary only uses rows whose build tag starts with this prefix. */
var SUMMARY_BUILD_PREFIX = 'beta';

/**
 * Team testing is tagged "<build>-internal" by the game (Editor, localhost, or a URL with ?internal=1).
 * true: count it in the summary (more data while there are few outside players); false: outside players only.
 */
var INCLUDE_INTERNAL = true;

/** Above this share of failed runs repeating the previous failure, a level is flagged (metric 3). */
var REPEAT_THRESHOLD = 0.4;

/**
 * Charts only show levels with at least this many data points (sessions, players, compared failures or wins);
 * with one or two players a rate jumps between 0% and 100%. The tables always list every level with its count.
 */
var MIN_SAMPLE = 3;

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
    // Sheets turns ids like "1-2" into the date Jan 2: store this row's level id again as plain text.
    var idCell = sheet.getRange(sheet.getLastRow(), COLUMNS.indexOf('levelId') + 1);
    idCell.setNumberFormat('@').setValue(String(data.levelId === undefined ? '' : data.levelId));
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

/** The runs tab, with its header row extended when COLUMNS gained new fields (older rows stay as they are). */
function getSheet_() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var sheet = ss.getSheetByName(SHEET_NAME) || ss.insertSheet(SHEET_NAME);
  if (sheet.getLastRow() === 0) {
    sheet.appendRow(COLUMNS);
  } else {
    var width = sheet.getLastColumn();
    if (width < COLUMNS.length) {
      sheet.getRange(1, width + 1, 1, COLUMNS.length - width).setValues([COLUMNS.slice(width)]);
    }
  }
  return sheet;
}


// ---------------------------------------------------------------------------------------------
// Summary: the four Beta metrics (GDD, Analytics). Run buildSummary from the Apps Script editor;
// it rebuilds the "summary" tab (tables + charts) from the runs tab every time.
// ---------------------------------------------------------------------------------------------

function buildSummary() {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var values = getSheet_().getDataRange().getValues();
  var m = computeMetrics_(values, SUMMARY_BUILD_PREFIX, ss.getSpreadsheetTimeZone(), INCLUDE_INTERNAL);

  var summary = ss.getSheetByName('summary') || ss.insertSheet('summary');
  summary.clear();
  summary.getCharts().forEach(function (c) { summary.removeChart(c); });

  summary.getRange(1, 1).setValue('Rule Runner Beta metrics: builds starting with "' + SUMMARY_BUILD_PREFIX +
    '", internal (team) runs ' + (INCLUDE_INTERNAL ? 'included' : 'excluded') + '. ' + m.sessions + ' sessions, ' + m.runs + ' runs. Updated ' + new Date());

  // Full tables side by side on row 3 (every level, with its sample size).
  writeTable_(summary, 3, 1, m.funnel);
  writeTable_(summary, 3, 7, m.runsToClear);
  writeTable_(summary, 3, 15, m.repeat);
  writeTable_(summary, 3, 21, m.diversity);
  writeTable_(summary, 3, 29, m.builds);
  // Chart data: the same tables without levels below MIN_SAMPLE, further right.
  var chartCol = 34;
  summary.getRange(2, chartCol).setValue('Chart data: levels with at least ' + MIN_SAMPLE + ' data points');
  var c1 = writeTable_(summary, 3, chartCol, chartRows_(m.funnel, 3, 3));
  var c2 = writeTable_(summary, 3, chartCol + 4, chartRows_(m.runsToClear, 6, 5));
  var c3 = writeTable_(summary, 3, chartCol + 10, chartRows_(m.repeat, 3, 2));
  var c4 = writeTable_(summary, 3, chartCol + 13, chartRows_(m.diversity, 5, 4));
  var chartRow = 4 + Math.max(m.funnel.length, m.runsToClear.length, m.repeat.length, m.diversity.length, m.builds.length);

  function chart(builder, range, row, col, title) {
    return range.getNumRows() > 1 && builder
      .addRange(range)
      .setNumHeaders(1)
      .setPosition(row, col, 0, 0)
      .setOption('title', title)
      .setOption('width', 720).setOption('height', 360);
  }
  var charts = [
    chart(summary.newChart().asLineChart().setOption('vAxis', { minValue: 0, maxValue: 100 }).setOption('pointSize', 5),
      c1, chartRow, 1, 'M1 Level completion funnel (% of sessions)'),
    // Candlestick: low, open, close, high = min, Q1, Q3, max of runs until the first clear.
    chart(summary.newChart().setChartType(Charts.ChartType.CANDLESTICK).setOption('legend', { position: 'none' }),
      c2, chartRow, 9, 'M2 Runs to clear (box = Q1 to Q3, whiskers = min to max; median in the table)'),
    chart(summary.newChart().asColumnChart().setOption('vAxis', { minValue: 0, maxValue: 100 })
      .setOption('legend', { position: 'none' }),
      c3, chartRow + 20, 1, 'M3 Repeat-mistake rate (%), flag above ' + REPEAT_THRESHOLD * 100 + '%'),
    chart(summary.newChart().asBarChart().setStacked().setOption('hAxis', { minValue: 0, maxValue: 100 }),
      c4, chartRow + 20, 9, 'M4 Solution diversity: share of wins per card set (%)')
  ];

  // A metric with no level above MIN_SAMPLE yet gets a note instead of an empty chart.
  var spots = [[chartRow, 1], [chartRow, 9], [chartRow + 20, 1], [chartRow + 20, 9]];
  charts.forEach(function (builder, i) {
    if (builder) summary.insertChart(builder.build());
    else summary.getRange(spots[i][0], spots[i][1]).setValue('M' + (i + 1) + ': not enough data yet (no level with ' +
      MIN_SAMPLE + '+ data points' + (i === 2 ? '; needs Beta runs that record endX / endFloor' : '') + ')');
  });
}

/** Header plus the rows whose column nCol is at least MIN_SAMPLE, cut to the first `width` columns. */
function chartRows_(table, nCol, width) {
  var out = [table[0].slice(0, width)];
  table.slice(1).forEach(function (r) { if (r[nCol] >= MIN_SAMPLE) out.push(r.slice(0, width)); });
  return out;
}

function writeTable_(sheet, row, col, table) {
  var range = sheet.getRange(row, col, table.length, table[0].length);
  sheet.getRange(row, col, table.length, 1).setNumberFormat('@');  // level ids stay text, not dates
  range.setValues(table);
  sheet.getRange(row, col, 1, table[0].length).setFontWeight('bold');
  return range;
}

/**
 * Pure computation (no Sheets calls), so it can be tested outside Apps Script.
 * values: the runs tab as a 2-D array, header row first. Returns the four metric tables, header row first.
 */
function computeMetrics_(values, buildPrefix, timeZone, includeInternal) {
  var header = values[0];
  var col = {};
  header.forEach(function (h, i) { col[h] = i; });
  function get(r, key) {
    if (col[key] === undefined) return '';
    var v = r[col[key]];
    return key === 'levelId' ? levelIdText_(v, timeZone) : v;
  }

  var buildRows = {};
  var rows = values.slice(1).filter(function (r) {
    var build = String(get(r, 'build'));
    var used = get(r, 'sessionId') !== '' && get(r, 'levelId') !== '' &&
      build.indexOf(buildPrefix) === 0 && (includeInternal || !/-internal$/.test(build));
    var b = buildRows[build] || (buildRows[build] = { rows: 0, used: used });
    b.rows++;
    return used;
  });
  var builds = [['build', 'rows', 'used in charts']];
  Object.keys(buildRows).sort().forEach(function (b) {
    builds.push([b || '(empty)', buildRows[b].rows, buildRows[b].used ? 'yes' : 'no']);
  });

  var sessions = {};
  var levels = {};  // id -> { opened: {sid}, won: {sid}, firstWin: {sid: attempt}, runs: [] }
  function level(id) {
    return levels[id] || (levels[id] = { opened: {}, won: {}, firstWin: {}, runs: [] });
  }
  var runCount = 0;
  rows.forEach(function (r) {
    var sid = String(get(r, 'sessionId'));
    var id = String(get(r, 'levelId'));
    var L = level(id);
    sessions[sid] = true;
    L.opened[sid] = true;  // a run also proves the level was opened (older builds sent no level_start)
    if (String(get(r, 'eventType')) === 'level_start') return;
    runCount++;
    var run = {
      sid: sid,
      attempt: Number(get(r, 'attempt')) || 0,
      result: String(get(r, 'result')),
      endX: get(r, 'endX'),
      endFloor: get(r, 'endFloor'),
      rules: String(get(r, 'rules'))
    };
    L.runs.push(run);
    if (run.result === 'win') {
      L.won[sid] = true;
      if (!(sid in L.firstWin) || run.attempt < L.firstWin[sid]) L.firstWin[sid] = run.attempt;
    }
  });

  var ids = Object.keys(levels).sort(compareLevelIds_);
  var nSessions = Object.keys(sessions).length;
  function pct(a, b) { return b ? Math.round(1000 * a / b) / 10 : 0; }

  // M1: reached = opened / all sessions; cleared = won / opened.
  var funnel = [['level', 'reached %', 'cleared %', 'sessions opened', 'sessions cleared']];
  ids.forEach(function (id) {
    var o = Object.keys(levels[id].opened).length, w = Object.keys(levels[id].won).length;
    funnel.push([id, pct(o, nSessions), pct(w, o), o, w]);
  });

  // M2: runs until the first win, per session; min / Q1 / Q3 / max for the candlestick, then median and n.
  var runsToClear = [['level', 'min', 'Q1', 'Q3', 'max', 'median', 'players']];
  ids.forEach(function (id) {
    var xs = Object.keys(levels[id].firstWin).map(function (s) { return levels[id].firstWin[s]; })
      .filter(function (a) { return a > 0; }).sort(function (a, b) { return a - b; });
    if (!xs.length) return;
    runsToClear.push([id, xs[0], quantile_(xs, 0.25), quantile_(xs, 0.75), xs[xs.length - 1],
      quantile_(xs, 0.5), xs.length]);
  });

  // M3: a failed run repeats when its cause and end tile match the same player's previous failed run on the level.
  var repeat = [['level', 'repeat %', 'repeated', 'failed runs after a failure', 'flag']];
  ids.forEach(function (id) {
    var bySession = {};
    levels[id].runs.forEach(function (run) { (bySession[run.sid] = bySession[run.sid] || []).push(run); });
    var compared = 0, repeated = 0;
    Object.keys(bySession).forEach(function (sid) {
      var prev = null;
      bySession[sid].sort(function (a, b) { return a.attempt - b.attempt; }).forEach(function (run) {
        if (run.result === 'win' || run.endX === '' || run.endX === -1) return;
        if (prev) {
          compared++;
          if (prev.result === run.result && prev.endX === run.endX && prev.endFloor === run.endFloor) repeated++;
        }
        prev = run;
      });
    });
    if (!compared) return;
    var rate = repeated / compared;
    repeat.push([id, pct(repeated, compared), repeated, compared, rate > REPEAT_THRESHOLD ? 'check feedback' : '']);
  });

  // M4: winning runs grouped by their exact ordered card list (tutorial levels T* are left out).
  var diversity = [['level', 'most common set %', '2nd set %', 'other sets %', 'distinct sets', 'wins', 'most common set']];
  ids.forEach(function (id) {
    if (/^T/.test(id)) return;
    var counts = {}, wins = 0;
    levels[id].runs.forEach(function (run) {
      if (run.result !== 'win') return;
      wins++;
      counts[run.rules] = (counts[run.rules] || 0) + 1;
    });
    if (!wins) return;
    var sets = Object.keys(counts).sort(function (a, b) { return counts[b] - counts[a]; });
    var top1 = counts[sets[0]], top2 = sets.length > 1 ? counts[sets[1]] : 0;
    diversity.push([id, pct(top1, wins), pct(top2, wins), pct(wins - top1 - top2, wins), sets.length, wins, sets[0]]);
  });

  return { builds: builds, sessions: nSessions, runs: runCount, funnel: funnel, runsToClear: runsToClear, repeat: repeat, diversity: diversity };
}

/**
 * Sheets auto-converts ids like "1-2" into the date Jan 2 (older rows were stored that way).
 * A date becomes "month-day" again; anything else is used as text.
 */
function levelIdText_(v, timeZone) {
  if (Object.prototype.toString.call(v) !== '[object Date]') return String(v);
  if (typeof Utilities !== 'undefined' && timeZone) return Utilities.formatDate(v, timeZone, 'M-d');
  return (v.getMonth() + 1) + '-' + v.getDate();
}

/** Linear-interpolated quantile of a sorted array. */
function quantile_(sorted, q) {
  var pos = (sorted.length - 1) * q, lo = Math.floor(pos), hi = Math.ceil(pos);
  return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
}

/** Tutorial levels (T1, T2, ...) first, then chapter levels by chapter and number (1-1 ... 3-5). */
function compareLevelIds_(a, b) {
  function key(id) {
    var t = /^T(\d+)$/.exec(id);
    if (t) return [0, Number(t[1])];
    var c = /^(\d+)-(\d+)$/.exec(id);
    if (c) return [Number(c[1]), Number(c[2])];
    return [999, 0];
  }
  var ka = key(a), kb = key(b);
  return ka[0] - kb[0] || ka[1] - kb[1] || (a < b ? -1 : a > b ? 1 : 0);
}

// Lets a local Node test load the pure functions; Apps Script has no `module` and ignores this.
if (typeof module !== 'undefined') {
  module.exports = { chartRows_: chartRows_, MIN_SAMPLE: MIN_SAMPLE, computeMetrics_: computeMetrics_, levelIdText_: levelIdText_, quantile_: quantile_, compareLevelIds_: compareLevelIds_, COLUMNS: COLUMNS };
}
