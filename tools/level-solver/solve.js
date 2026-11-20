// Brute-force every program within the slot limit and group the winners by route.
// Usage: node tools/level-solver/solve.js docs/proto/<level>.html   (default: docs/proto/crack-2.html)
const fs = require('fs'), path = require('path');
function load(arg) {
  if (arg && arg.endsWith('.json')) return { SIM:require('./sim.js'), level:JSON.parse(fs.readFileSync(arg, 'utf8')) };
  const html = fs.readFileSync(arg || path.join(__dirname, '../../docs/proto/crack-2.html'), 'utf8');
  const sim = html.slice(html.indexOf('// SIM-BEGIN'), html.indexOf('// SIM-END'));
  const lvl = html.slice(html.indexOf('// LEVEL-BEGIN'), html.indexOf('// LEVEL-END'));
  return new Function(sim + lvl + '\nreturn { SIM, level:LEVEL };')();
}
const { SIM, level } = load(process.argv[2]);

const conds = level.ifs, acts = level.thens;
const sides = [];
for (const c of conds) for (const n of [false, true]) sides.push({ c, n });
const ifs = sides.map(s => ({ if:s.c, not:s.n }));
if (level.and) for (let a = 0; a < sides.length; a++) for (let b = a + 1; b < sides.length; b++)
  if (sides[a].c !== sides[b].c) ifs.push({ if:sides[a].c, not:sides[a].n, if2:sides[b].c, not2:sides[b].n });
const cards = [];
for (const i of ifs) for (const t of acts) cards.push({ ...i, then:t });
const name = c => 'IF ' + (c.not ? 'NOT ' : '') + c.if + (c.if2 ? ' AND ' + (c.not2 ? 'NOT ' : '') + c.if2 : '') + ' THEN ' + c.then;

const wins = [];
let runs = 0;
function rec(prog) {
  if (prog.length) {
    runs++;
    const r = SIM.run(level, prog);
    if (r.outcome === 'win') {
      // Drop programs where some card never fires: they are a shorter program plus a passenger.
      if (r.hits.every(h => h > 0)) wins.push({ prog:prog.slice(), r });
    }
  }
  if (prog.length < level.slots) for (const c of cards) { prog.push(c); rec(prog); prog.pop(); }
}
rec([]);

const star2 = (p, r) => level.star2.kind === 'cards' ? p.length <= level.star2.n : r.qi >= level.star2.n;
const star3 = r => r.gold;
const groups = new Map();
for (const w of wins) {
  const key = w.r.frames.map(f => f.l + ':' + f.x).join(' ') + (w.r.coin ? ' coin' : '') + (w.r.gold ? ' GOLD' : '');
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push(w);
}
console.log(`runs ${runs}, winning programs (every card fires) ${wins.length}, routes ${groups.size}`);
const summary = { routes:[], any3:false, starsAll:0 };
for (const [key, ws] of groups) {
  const g = ws[0].r;
  const pathStr = g.frames.map(f => (f.l ? 'b' : 't') + f.x + (f.action !== 'walk' ? f.action[0].toUpperCase() : '')).join(' ');
  const byLen = {};
  ws.forEach(w => { (byLen[w.prog.length] = byLen[w.prog.length] || []).push(w); });
  console.log(`\n== route: floors ${g.floors.join('>')} coin=${g.coin} gold=${g.gold} orbs=${g.orbs}  (${ws.length} programs)`);
  console.log('   ' + pathStr);
  for (const len of Object.keys(byLen)) {
    const list = byLen[len];
    const qis = list.map(w => w.r.qi);
    const andUse = list.filter(w => w.prog.some(c => c.if2)).length;
    console.log(`  ${len} cards: ${list.length} programs, energy left ${Math.min(...qis)}..${Math.max(...qis)}, with AND ${andUse}`);
    const show = [...list].sort((a, b) => b.r.qi - a.r.qi);
    const seen = new Set();
    for (const w of show) {
      const s = w.prog.map(name).join(' | ');
      const k = (w.prog.some(c => c.if2) ? 'A' : 'N');
      if (seen.has(k) && seen.size >= 2) continue;
      if ([...seen].filter(z => z === k).length) continue;
      seen.add(k);
      const st = 1 + (star2(w.prog, w.r) ? 1 : 0) + (star3(w.r) ? 1 : 0);
      console.log(`     e.g. [${s}] qi=${w.r.qi} stars=${st}`);
    }
  }
  for (const w of ws) {
    const st = 1 + (star2(w.prog, w.r) ? 1 : 0) + (star3(w.r) ? 1 : 0);
    if (st === 3) { summary.any3 = true; summary.starsAll++; }
  }
}
console.log(`\nprograms with all three stars: ${summary.starsAll}`);
// Slack check: the most Big Jumps any winning program uses.
let maxBig = 0;
for (const w of wins) { const b = w.r.frames.filter(f => f.action === 'bigjump').length; if (b > maxBig) maxBig = b; }
console.log('most Big Jumps in a winning run:', maxBig);
// Single-card "big jump at every obstacle" programs.
for (const c of cards.filter(c => c.then === 'bigjump' && !c.if2)) {
  const r = SIM.run(level, [c]);
  if (r.outcome === 'win') console.log('ONE-CARD BIG JUMP WINS:', name(c));
}
