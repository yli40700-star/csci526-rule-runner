// Plays random plans through the rules and, at every step where water moves, compares where the rules put it with the independent model.
// node audit-water.js [plans per level, default 200] [flip: also play Flip cards] [examples to print per level]
const C = require('./core.js'), { LEVELS } = require('./levels.js'), { settleRef, misplaced } = require('./refwater.js');
// Six small maps that stress the water rule: two pits, a burst dam, connected vessels, an overflow, a stepped waterfall and lava in two parts.
const EXTRA = [
  { id: 'x1', title: '两坑一水', dir: 1, slots: 3, qi: 9, map: ['######################', '#....................#', '#S...................#', '###~~~~~##.....#...###', '###~~~~~##.....#...###', '######################'] },
  { id: 'x2', title: '浅坑深坑', dir: 1, slots: 3, qi: 9, map: ['########################', '#......................#', '#S..........Xx~~~~~~~~~#', '#####.###.###~~~~~~~~~~#', '#####.###.##############', '#########.##############', '########################'] },
  { id: 'x3', title: '连通器', dir: 1, slots: 3, qi: 9, map: ['####################', '#..................#', '#S...#~~~~~~#......#', '#####=~~~~~~=####..#', '#....=======.....F.#', '#..................#', '####################'] },
  { id: 'x4', title: '溢流', dir: 1, slots: 3, qi: 9, map: ['######################', '#....................#', '#S.~~~~~.............#', '###~~~~~..#...#...####', '##########...#...#####', '######################'] },
  { id: 'x5', title: '台阶瀑布', dir: 1, slots: 3, qi: 9, map: ['######################', '#S.#~~~~~~...........#', '#..=~~~~~~...........#', '####======...........#', '#..........#.........#', '#.......######.......#', '#..........#####.....#', '######################'] },
  { id: 'x6', title: '岩浆两段', dir: 1, slots: 3, qi: 9, map: ['######################', '#....................#', '#S...................#', '###~~~~~~~##&&&#&&&###', '######################'] },
];
const RUNS = +process.argv[2] || 200, FLIPS = process.argv[3] === 'flip';
let seed = 20261104; const rnd = n => { seed = (seed * 1664525 + 1013904223) >>> 0; return (seed >>> 7) % n; };
const list = LEVELS.concat(EXTRA).filter(l => l.map.join('').match(/[~&]/));

function jobFor(l, prev, f) {
  const w = l.map[0].length, h = l.map.length, size = w * h;
  const here = { ...f, w, h }, start = new Uint8Array(size), open = new Uint8Array(size), burn = new Set();
  let before = 0, after = 0, cooled = 0;
  for (let i = 0; i < size; i++) {
    const x = i % w, y = (i - x) / w;
    if (prev.cells[i] === '&' && f.cells[i] === 'o') { burn.add(i); cooled++; open[i] = 1; }
    else open[i] = C.waterOpen(here, x, y) && f.cells[i] !== '&' ? 1 : 0;
    if (f.cells[i] === '&') burn.add(i);
    if (prev.wet[i] === '1') { before++; if (open[i]) start[i] = 1; }
    if (f.wet[i] === '1') after++;
  }
  for (const i of burn) open[i] = 1;
  const spat = f.mouth === 'spit' && prev.belly === 'W', added = spat ? after + cooled - before : 0;
  if (added < 0 || (!spat && before !== after + cooled)) return null;   // Water left the world some other way: a drink, or a stone pushing it out.
  const ax = prev.x + prev.dir, canPour = open[prev.y * w + ax];
  return { w, h, g: f.g, start, open, burn, cooled, pour: spat ? { x: canPour ? ax : prev.x, y: prev.y, n: added, dir: prev.dir } : null };
}

const draw = (l, wetAt) => l.map.map((row, y) => [...row].map((c, x) => wetAt(y * row.length + x)).join('')).join('\n');
const out = [], counts = {};
let events = 0;
for (const l of list) {
  const world = C.makeWorld(l), open = [];
  for (let y = 0; y < world.h; y++) for (let x = 0; x < world.w; x++) if (C.canPlaceAt(l, world, x, y)) open.push(C.cellKey(x, y));
  for (let k = 0; k < RUNS; k++) {
    const skills = ['gulp', 'gulp', 'gulp', 'gulp', 'fire', 'fire', 'jump', 'jump', 'turn', 'decoy'].concat(FLIPS ? ['flip', 'flip', 'flip'] : []), cards = {};
    for (let j = 0, n = 2 + rnd(6); j < n; j++) {
      const walk = C.simulate(l, { cards, portals: [] }).frames.map(f => C.cellKey(f.x, f.y)).filter(c => open.includes(c) && !cards[c]);
      if (!walk.length) break;
      cards[walk[rnd(walk.length)]] = skills[rnd(skills.length)];
    }
    const r = C.simulate(l, { cards, portals: [] });
    r.frames.forEach((f, t) => {
      if (!t || f.wet === r.frames[t - 1].wet) return;
      const prev = r.frames[t - 1], heavy = q => q.objs.filter(o => o.t === 'M' || o.t === 'U').map(o => o.id + ':' + o.x + ',' + o.y).join();
      if (heavy(prev) !== heavy(f)) return;   // Stone moved in the same step: the water went before or after it, and the model only sees where it ended up.
      const job = jobFor(l, prev, f);
      if (!job) return;
      const key = l.id + '|' + prev.wet + '|' + f.wet + '|' + f.g + '|' + f.cells;
      if (counts[key]) return;
      counts[key] = 1;
      events++;
      const { mass } = settleRef(job, 1 + t), off = misplaced(job.w, job.h, job.open, mass, f.wet);
      out.push({ l, id: l.id, t, cards, off, prev, f, mass, spit: !!job.pour });
    });
  }
}
out.sort((a, b) => b.off - a.off);
const LIMIT = 2.5, big = out.filter(e => e.off >= LIMIT);
const by = {}, seen = {};
for (const e of out) by[e.id] = by[e.id] || { events: 0, off: 0, worst: 0 };
for (const e of out) { by[e.id].events++; if (e.off >= LIMIT) by[e.id].off++; by[e.id].worst = Math.max(by[e.id].worst, +e.off.toFixed(1)); }
console.log('distinct water moves', events, 'disagreeing', big.length);
console.log(JSON.stringify(by));
for (const e of big) {
  if ((seen[e.id] = (seen[e.id] || 0) + 1) > (+process.argv[4] || 1)) continue;
  console.log(`\n--- level ${e.id} step ${e.t} off ${e.off.toFixed(1)} ${e.spit ? 'spit' : ''} g=${e.f.g} objs ${JSON.stringify(e.f.objs.map(o => o.t + o.x + ',' + o.y))} cards ${JSON.stringify(e.cards)}`);
  const row = (fn) => draw(e.l, fn).split('\n');
  const before = row(i => (e.prev.wet[i] === '1' ? '~' : '#=%xoDF&'.includes(e.f.cells[i]) ? e.f.cells[i] : '.'));
  const rules = row(i => (e.f.wet[i] === '1' ? '~' : '#=%xoDF&'.includes(e.f.cells[i]) ? e.f.cells[i] : '.'));
  const model = row(i => (e.mass[i] >= 0.5 ? '~' : e.mass[i] >= 0.12 ? '-' : '#=%xoDF&'.includes(e.f.cells[i]) ? e.f.cells[i] : '.'));
  console.log('before'.padEnd(before[0].length + 2) + 'rules'.padEnd(before[0].length + 2) + 'model');
  before.forEach((q, y) => console.log(q + '  ' + rules[y] + '  ' + model[y]));
}
