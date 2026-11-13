const C = require(process.env.CORE || './core.js');
const { SKILLS, SKILL_IDS, cellKey, parseKey, inPortalRange, makeWorld, makeState, cloneState, step, settle, simulate, canPlaceAt, worldKey } = C;

/** Walks every run for one loadout, deciding at each new cell whether a card goes there. Wins are re-checked with simulate. */
function searchLoadout(level, skills, opts = {}) {
  const qiTotal = opts.qi !== undefined ? opts.qi : level.qi;
  const nodeLimit = opts.nodeLimit || 150000;
  const world = makeWorld(level);
  const empty = makeState(level, { cards: {}, portals: [] });
  settle(empty);
  const openCells = [];
  for (let y = 0; y < world.h; y++) for (let x = 0; x < world.w; x++) if (canPlaceAt(level, world, x, y)) openCells.push(cellKey(x, y));
  const startKey = cellKey(world.startX, world.startY);
  const memo = new Map();
  const best = {};   // stars -> { cost, plan }
  let nodes = 0;

  function record(s, qi, plan) {
    const cost = qiTotal - qi;
    const real = { cards: { ...plan.cards }, portals: plan.portals.slice() };
    const r = simulate(level, real);
    if (r.outcome !== 'win') return;
    const stars = r.stars;
    if (!best[stars] || cost < best[stars].cost) best[stars] = { cost, plan: real, steps: r.steps };
  }

  function run(s, qi, decided, plan) {
    while (true) {
      if (nodes++ > nodeLimit) return;
      if (s.outcome) { if (s.outcome === 'win') record(s, qi, plan); return; }
      if (s.t > 200) return;
      const key = worldKey(s) + '#' + decided.size % 1;
      const prev = memo.get(key);
      if (prev !== undefined && prev >= qi) return;
      memo.set(key, qi);

      const k = cellKey(s.x, s.y);
      if (!decided.has(k)) {
        decided = new Set(decided);
        decided.add(k);
        if (openCells.includes(k)) {
          for (const skill of skills) {
            const cost = SKILLS[skill].cost;
            if (cost > qi) continue;
            if (skill === 'portal') {
              if (k === startKey) continue;
              for (const exit of openCells) {
                if (exit === k || decided.has(exit) || !inPortalRange(k, exit)) continue;
                const s2 = cloneState(s);
                s2.links[k] = exit; s2.links[exit] = k;
                [s2.x, s2.y] = parseKey(exit);
                s2.queue = [];
                run(s2, qi - cost, new Set(decided).add(exit), { cards: plan.cards, portals: plan.portals.concat([[k, exit]]) });
              }
              continue;
            }
            const s2 = cloneState(s);
            s2.pending[k] = skill;
            step(s2);
            run(s2, qi - cost, decided, { cards: { ...plan.cards, [k]: skill }, portals: plan.portals });
          }
        }
      }
      step(s);
    }
  }
  run(empty, qiTotal, new Set(), { cards: {}, portals: [] });
  return { best, truncated: nodes > nodeLimit, nodes };
}

function combos(list, n) {
  if (n === 0) return [[]];
  if (list.length < n) return [];
  const [h, ...t] = list;
  return combos(t, n - 1).map(c => [h, ...c]).concat(combos(t, n));
}

/** Tries every loadout that fills the level's slots. */
function surveyLevel(level, opts = {}) {
  const all = combos(opts.skills || SKILL_IDS, level.slots);
  const rows = [];
  let truncated = 0;
  for (const load of all) {
    let r = searchLoadout(level, load, opts);
    if (r.truncated && opts.retryLimit) r = searchLoadout(level, load, { ...opts, nodeLimit: opts.retryLimit });
    if (r.truncated) truncated++;
    const stars = Object.keys(r.best).map(Number);
    if (stars.length) rows.push({ load, best: r.best, top: Math.max(...stars), minCost: Math.min(...Object.values(r.best).map(b => b.cost)) });
  }
  return { rows, total: all.length, truncated };
}

const planText = p => Object.entries(p.cards).map(([k, v]) => `${v}@${k}`).concat(p.portals.map(([a, b]) => `portal@${a}>${b}`)).join(' ') || '(no cards)';

function usedSkills(p) {
  const set = new Set(Object.values(p.cards));
  if (p.portals.length) set.add('portal');
  return [...set].sort();
}

function report(level, opts = {}) {
  const t0 = Date.now();
  const { rows, total, truncated } = surveyLevel(level, opts);
  const byStars = {};
  for (const r of rows) for (const [st, b] of Object.entries(r.best)) {
    (byStars[st] = byStars[st] || []).push({ load: r.load, ...b });
  }
  console.log(`\n${level.id} ${level.title}  slots=${level.slots} qi=${level.qi}  loadouts that win ${rows.length}/${total}  truncated=${truncated}  ${((Date.now() - t0) / 1000).toFixed(1)}s`);
  for (const st of Object.keys(byStars).sort()) {
    const list = byStars[st].sort((a, b) => a.cost - b.cost);
    const distinct = new Map();
    list.forEach(e => { const u = usedSkills(e.plan).join('+') || '(none)'; if (!distinct.has(u) || e.cost < distinct.get(u).cost) distinct.set(u, e); });
    console.log(`  ${st} stars: ${distinct.size} distinct skill sets, cheapest ${list[0].cost} qi`);
    [...distinct.values()].sort((a, b) => a.cost - b.cost).slice(0, opts.show || 6).forEach(e => console.log(`     ${e.cost}qi  ${planText(e.plan)}`));
  }
  return { rows, byStars };
}

/** Prints the map with the run drawn on it, for designing levels by eye. */
function draw(level, plan) {
  const r = simulate(level, plan);
  const g = level.map.map(row => row.replace('S', '.').split(''));
  r.frames.forEach(f => { if (f.y >= 0 && f.y < g.length && '.~*'.includes(g[f.y][f.x])) g[f.y][f.x] = f.g > 0 ? 'o' : 'c'; });
  console.log(g.map(row => row.join('')).join('\n'));
  const fired = r.frames.filter(f => f.firedSkill).map(f => `${f.firedSkill}${f.echoOf ? '(' + f.echoOf + ')' : ''}${f.note ? '/' + f.note : ''}`);
  console.log(`outcome=${r.outcome} stars=${r.stars} steps=${r.steps} fired=[${fired.join(' ')}]`);
  return r;
}

module.exports = { searchLoadout, surveyLevel, report, draw, combos, planText, usedSkills };
