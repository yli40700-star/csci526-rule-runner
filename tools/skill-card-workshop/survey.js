// Solves every level for every loadout. Writes survey.json: the plans the browser playthrough uses, and each level's route count.
const fs = require('fs');
process.env.CORE = process.env.CORE || __dirname + '/core.js';
const { surveyLevel, usedSkills, planText } = require('./solver.js');
const C = require(process.env.CORE);
const { LEVELS } = require(process.env.LEVELS || './levels.js');
const only = process.argv.slice(2);
const OUT = process.env.OUT || 'survey.json';
const out = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : {};
for (const l of LEVELS) {
  if (only.length && !only.includes(l.id)) continue;
  const t0 = Date.now();
  const { rows, total, truncated } = surveyLevel(l, { nodeLimit: +process.env.NODES || 400000, retryLimit: 4000000 });
  const routes = new Map(), three = new Map();
  let top = 0;
  for (const r of rows) for (const [st, b] of Object.entries(r.best)) {
    const key = usedSkills(b.plan).join('+') || '(none)';
    if (!routes.has(key) || b.cost < routes.get(key).cost) routes.set(key, b);
    top = Math.max(top, +st);
    if (+st === 3 && (!three.has(key) || b.cost < three.get(key).cost)) three.set(key, b);
  }
  const noCards = C.simulate(l, { cards: {}, portals: [] }).outcome;
  const list = [...routes.values()].sort((a, b) => a.cost - b.cost);
  const stars3 = [...three.entries()].sort((a, b) => a[1].cost - b[1].cost);
  console.log(`${l.id.padStart(2)} ${l.title}: slots ${l.slots} qi ${l.qi} | ${rows.length}/${total} loadouts win | ${routes.size} card sets | max ${top}★ | noCards=${noCards} | truncated=${truncated} | ${((Date.now() - t0) / 1000).toFixed(0)}s`);
  console.log(`     cheapest ${list[0].cost}qi: ${planText(list[0].plan)}`);
  stars3.slice(0, 3).forEach(([k, b]) => console.log(`     3★ ${b.cost}qi: ${planText(b.plan)}`));
  const skills = {};
  for (const key of routes.keys()) for (const s of key.split('+')) skills[s] = (skills[s] || 0) + 1;
  out[l.id] = { win: rows.length, total, routes: routes.size, top, noCards, three: stars3.map(([, b]) => ({ cost: b.cost, plan: b.plan })), cheapest: { cost: list[0].cost, plan: list[0].plan }, routeSkills: [...routes.keys()], threeSkills: stars3.map(([k]) => k), routePlans: [...routes.entries()].map(([k, b]) => ({ key: k, plan: b.plan, cost: b.cost })) };
  fs.writeFileSync(OUT, JSON.stringify(out));
}
