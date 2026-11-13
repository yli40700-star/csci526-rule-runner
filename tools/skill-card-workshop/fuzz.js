// Random plans on every level. Each frame of each run has to obey the world's invariants.
const C = require(process.env.CORE || './core.js');
const { LEVELS } = require(process.env.LEVELS || './levels.js');
const { SKILL_IDS, makeWorld, makeState, step, settle, simulate, canPlaceAt, cellKey, inPortalRange } = C;
const RUNS = +process.argv[2] || 12000;
let seed = 20261104; const rnd = n => { seed = (seed * 1664525 + 1013904223) >>> 0; return (seed >>> 7) % n; };
const issues = {};
const note = (k, ex) => { issues[k] = issues[k] || { n: 0, ex }; issues[k].n++; };
const units = f => [...f.wet].filter(c => c === '1').length;
let runs = 0, wins = 0;
const outcomes = {};
for (const l of LEVELS) {
  const w = makeWorld(l), open = [];
  for (let y = 0; y < w.h; y++) for (let x = 0; x < w.w; x++) if (canPlaceAt(l, w, x, y)) open.push(cellKey(x, y));
  const startUnits = units(w);
  for (let i = 0; i < RUNS; i++) {
    const cards = {}, portals = [], n = rnd(9);
    for (let j = 0; j < n; j++) {
      const k = open[rnd(open.length)], skill = SKILL_IDS[rnd(SKILL_IDS.length)];
      if (skill === 'portal') { const e = open[rnd(open.length)]; const used = portals.flat(); if (e !== k && inPortalRange(k, e) && !used.includes(k) && !used.includes(e) && !cards[k] && !cards[e]) portals.push([k, e]); }
      else if (!portals.flat().includes(k)) cards[k] = skill;
    }
    const plan = { cards, portals };
    let r;
    try { r = simulate(l, plan); } catch (e) { note('throw: ' + e.message, { l: l.id, plan }); continue; }
    runs++;
    outcomes[r.outcome] = (outcomes[r.outcome] || 0) + 1;
    if (r.outcome === 'timeout') note('timeout (no loop caught in 400 steps)', { l: l.id, plan });
    if (r.frames.length !== r.steps + 1) note('frame count', { l: l.id, plan });
    const s = makeState(l, plan);
    settle(s);
    let water = units(s);   // Counts the water in the world and in the belly together.
    r.frames.forEach((f, t) => {
      const cellsSeen = new Set();
      for (const o of f.objs) {
        const k = o.x + ',' + o.y;
        if (cellsSeen.has(k)) note('two objects in one cell', { l: l.id, plan, t });
        cellsSeen.add(k);
        if (o.x < 0 || o.y < 0 || o.x >= w.w || o.y >= w.h) { note('object off map', { l: l.id, plan, t }); continue; }
        const c = f.cells[o.y * w.w + o.x];
        if ('#=%xo'.includes(c) || '()AV'.includes(c)) note('object inside terrain', { l: l.id, plan, t, c });
        if (c === '&' && o.t !== 'M') note('something other than stone sitting in lava', { l: l.id, plan, t, o });
        if (c === 'F') note('object on the finish', { l: l.id, plan, t });
      }
      for (let i2 = 0; i2 < f.wet.length; i2++) {
        if (f.wet[i2] !== '1') continue;
        const c = f.cells[i2];
        if ('#=%xoDF'.includes(c) || '()AV'.includes(c)) note('water inside terrain', { l: l.id, plan, t, c });
        if (c === '&') note('water left standing on lava', { l: l.id, plan, t });
        const ox = i2 % w.w, oy = (i2 - ox) / w.w;
        if (f.objs.some(o => (o.t === 'M' || o.t === 'U') && o.x === ox && o.y === oy)) note('water inside a heavy object', { l: l.id, plan, t });
      }
      if (f.y >= 0 && f.y < w.h && f.x >= 0 && f.x < w.w) {
        const c = f.cells[f.y * w.w + f.x];
        if ('#=%xo'.includes(c) || '()AV'.includes(c)) note('runner inside terrain', { l: l.id, plan, t, c });
        if (c === '&' && r.outcome !== 'lava') note('runner in lava and alive', { l: l.id, plan, t });
      } else if (r.outcome !== 'fell' || t !== r.frames.length - 1) note('runner off map', { l: l.id, plan, t });
      if (f.stars > 3) note('more than three stars', { l: l.id });
      // A jump is 1 high: anything higher needs a slime, flip, water or a portal.
      if (t > 0 && Math.abs(f.y - r.frames[t - 1].y) > 1 && !f.teleported && !f.wet.includes('1')) note('moved more than one row in a step', { l: l.id, plan, t });
    });
    // Water can only leave the map through the belly, so no drink or pour means an exact count.
    const usesGulp = Object.values(cards).some(k => k === 'gulp' || k === 'echo');
    if (!usesGulp) { const last = r.frames[r.frames.length - 1]; const seenChange = new Set(r.frames.map(units)); if (seenChange.size > 1 && startUnits > 0) { const extra = [...seenChange].some(u => u > startUnits); if (extra) note('water grew without a pour', { l: l.id, plan }); } }
    if (r.outcome === 'win') wins++;
    const again = simulate(l, plan);
    if (again.outcome !== r.outcome || again.steps !== r.steps || again.stars !== r.stars) note('run is not deterministic', { l: l.id, plan });
  }
}
console.log('runs', runs, 'wins', wins, JSON.stringify(outcomes));
const keys = Object.keys(issues);
if (!keys.length) console.log('no invariant broken');
for (const k of keys) console.log(String(issues[k].n).padStart(7), k, JSON.stringify(issues[k].ex).slice(0, 300));
