// Tests for the pure calculations in Code.gs (no Sheets needed).
// Run: node tools/analytics/test_metrics.js
const assert = require('assert');
const path = require('path');

// Code.gs is plain JavaScript and exports its pure functions when `module` exists (Node treats .gs as .js).
const g = require(path.join(__dirname, 'Code.gs'));
const C = g.COLUMNS;

function row(o) { return C.map(k => (o[k] === undefined ? '' : o[k])); }
function table(rows) { return [C].concat(rows.map(row)); }
const start = (sid, lvl, build = 'beta-playtest') =>
  ({ sessionId: sid, build, eventType: 'level_start', levelId: lvl, attempt: 0, endX: -1, endFloor: -1 });
const run = (sid, lvl, attempt, result, endX, rules, build = 'beta-playtest', endFloor = 0) =>
  ({ sessionId: sid, build, eventType: 'run_end', levelId: lvl, attempt, result, endX, endFloor, rules });

let passed = 0;
function test(name, fn) { fn(); passed++; console.log('ok  ' + name); }

// s1: T1 empty-hand pit, then win; 1-1: same death twice, a different death, then a win.
// s2: T1 first try; opens 1-1 without running. s3: 1-1 first try; 2-0 two pits on different floors.
// Plus rows that must be left out: an Alpha row and internal rows.
const base = table([
  start('s1', 'T1'), run('s1', 'T1', 1, 'pit', 3, ''), run('s1', 'T1', 2, 'win', 9, 'IF pit THEN jump'),
  start('s1', '1-1'), run('s1', '1-1', 1, 'pit', 5, 'A'), run('s1', '1-1', 2, 'pit', 5, 'A'),
  run('s1', '1-1', 3, 'spike', 7, 'B'), run('s1', '1-1', 4, 'win', 10, 'C'),
  start('s2', 'T1'), run('s2', 'T1', 1, 'win', 9, 'IF pit THEN jump'), start('s2', '1-1'),
  start('s3', '1-1'), run('s3', '1-1', 1, 'win', 10, 'D'), start('s3', '2-0'),
  run('s3', '2-0', 1, 'pit', 4, 'X'), run('s3', '2-0', 2, 'pit', 4, 'X', 'beta-playtest', 1),
  run('alpha1', '1-1', 1, 'win', 10, 'Z', 'alpha-playtest'),
  run('int1', '1-1', 1, 'win', 10, 'Z', 'beta-playtest-internal'), start('int1', '3-5', 'beta-playtest-internal'),
]);
const m = g.computeMetrics_(base, 'beta');

test('build filter and counts', () => {
  assert.strictEqual(m.sessions, 3);
  assert.strictEqual(m.runs, 10);
  assert.deepStrictEqual(m.builds, [['build', 'rows', 'rows used in charts'], ['alpha-playtest', 1, 0],
    ['beta-playtest', 16, 16], ['beta-playtest-internal', 2, 0]]);
});

test('M1 funnel', () => {
  assert.deepStrictEqual(m.funnel.map(r => r[0]), ['level', 'T1', '1-1', '2-0']);
  assert.deepStrictEqual(m.funnel[1], ['T1', 66.7, 100, 2, 2]);
  assert.deepStrictEqual(m.funnel[2], ['1-1', 100, 66.7, 3, 2]);
  assert.deepStrictEqual(m.funnel[3], ['2-0', 33.3, 0, 1, 0]);
});

test('M2 runs to clear skips empty-hand runs', () => {
  assert.deepStrictEqual(m.runsToClear[1], ['T1', 1, 1, 1, 1, 1, 2]);  // s1's empty-hand pit is not counted
  assert.deepStrictEqual(m.runsToClear[2], ['1-1', 1, 1.75, 3.25, 4, 2.5, 2]);
});

test('M3 repeat mistakes, floors compared', () => {
  assert.deepStrictEqual(m.repeat[1], ['1-1', 50, 1, 2, 'check feedback']);
  assert.deepStrictEqual(m.repeat[2], ['2-0', 0, 0, 1, '']);
});

test('M4 solution diversity', () => {
  assert.strictEqual(m.diversity.length, 2);
  assert.deepStrictEqual(m.diversity[1], ['1-1', 50, 50, 0, 2, 2, 'C']);
});

test('empty-hand runs are never compared in M3; an empty-hand win still counts in M2', () => {
  const e = table([
    run('e1', 'T4', 1, 'pit', 3, ''), run('e1', 'T4', 2, 'pit', 3, ''),
    run('e1', 'T4', 3, 'pit', 8, 'A'), run('e1', 'T4', 4, 'pit', 8, 'B'), run('e1', 'T4', 5, 'win', 11, 'C'),
    run('e2', 'T4', 1, 'win', 11, ''),
  ]);
  const me = g.computeMetrics_(e, 'beta');
  assert.deepStrictEqual(me.runsToClear[1], ['T4', 1, 1.5, 2.5, 3, 2, 2]);
  assert.deepStrictEqual(me.repeat[1], ['T4', 100, 1, 1, 'check feedback']);  // only A vs B
});

test('internal rows counted when includeInternal is true', () => {
  const mi = g.computeMetrics_(base, 'beta', undefined, true);
  assert.deepStrictEqual(mi.builds.find(b => b[0] === 'beta-playtest-internal'), ['beta-playtest-internal', 2, 2]);
  assert.strictEqual(mi.sessions, 4);
});

test('SUMMARY_SINCE keeps only rows received from that moment on', () => {
  const t = table([
    Object.assign(run('a', 'T3', 1, 'win', 9, 'old T3'), { receivedAt: '2026-11-20T03:00:00-08:00' }),
    Object.assign(run('b', 'T3', 1, 'win', 9, 'new T3'), { receivedAt: '2026-11-20T13:00:00-08:00' }),
    Object.assign(run('c', 'T3', 1, 'win', 9, 'no time'), { receivedAt: '' }),
  ]);
  const ms = g.computeMetrics_(t, 'beta', undefined, false, new Date('2026-11-20T12:00:00-08:00'));
  assert.strictEqual(ms.runs, 1);
  assert.deepStrictEqual(ms.builds[1], ['beta-playtest', 3, 1]);
  assert.strictEqual(g.computeMetrics_(t, 'beta').runs, 3);   // no cutoff: every row
});

test('rows from older builds (no eventType) count as runs', () => {
  const old = [C.slice(0, 14), ['d', 'o1', 'beta2-playtest', '1-2', 1, 'win', 0, 1, 'R', 5, false, 3, 't', 0]];
  const m2 = g.computeMetrics_(old, 'beta');
  assert.strictEqual(m2.runs, 1);
  assert.deepStrictEqual(m2.funnel[1], ['1-2', 100, 100, 1, 1]);
});

test('level ids that Sheets turned into dates', () => {
  assert.strictEqual(g.levelIdText_(new Date(2026, 0, 2)), '1-2');
  assert.strictEqual(g.levelIdText_(new Date(2026, 1, 4)), '2-4');
  assert.strictEqual(g.levelIdText_('T1'), 'T1');
});

test('level order and chart rows', () => {
  assert.deepStrictEqual(['3-1', 'T2', '1-10', '1-2', '2-0', 'T1'].sort(g.compareLevelIds_),
    ['T1', 'T2', '1-2', '1-10', '2-0', '3-1']);
  assert.deepStrictEqual(g.chartRows_(m.funnel, 3, 3), [['level', 'reached %', 'cleared %'], ['1-1', 100, 66.7]]);
});

console.log(passed + ' tests passed');
