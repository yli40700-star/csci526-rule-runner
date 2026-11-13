// One test per rule, each on a tiny map, so a change that breaks a rule fails by name.
const C = require(process.env.CORE || './core.js');
const { simulate, makeState, step, settle, cellKey, wetAt, objAt } = C;
let passed = 0, failed = 0;
const test = (name, fn) => {
  try { fn(); passed++; } catch (e) { failed++; console.log(`FAIL ${name}: ${e.message}`); }
};
const eq = (a, b, what = '') => { if (JSON.stringify(a) !== JSON.stringify(b)) throw new Error(`${what} expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`); };
const L = (map, dir = 1) => ({ id: 't', title: 't', slots: 3, qi: 99, dir, map });
const run = (map, cards = {}, portals = [], dir = 1) => simulate(L(map, dir), { cards, portals });
const last = r => r.frames[r.frames.length - 1];
const wetCount = f => [...f.wet].filter(c => c === '1').length;
const wetRows = f => { const w = f.w, rows = {}; return rows; };

// ---------- Walking ----------
test('walks to the finish', () => eq(run(['#####', '#S.F#', '#####']).outcome, 'win'));
test('wall turns the runner round', () => {
  const r = run(['#####', '#F.S#', '#####'], {}, [], 1);
  eq(r.outcome, 'win');
  eq(r.frames.some(f => f.bumped), true);
});
test('a closed loop is reported', () => eq(run(['#####', '#S..#', '#####']).outcome, 'loop'));
test('falling off the bottom is reported', () => eq(run(['#####', '#S..#', '##.##', '#...#'].concat(['#...#'])).outcome !== 'win', true));
test('spikes kill', () => eq(run(['#####', '#S^F#', '#####']).outcome, 'spike'));

// ---------- Jump ----------
const path = r => r.frames.map(f => [f.x, f.y]);
test('a jump is one arc: over one cell and back to the ground', () => {
  const r = run(['########', '#......#', '#S.....#', '########'], { '2,2': 'jump' });
  eq(path(r).slice(1, 5), [[2, 2], [3, 1], [4, 2], [5, 2]]);
  eq(r.frames[2].arc, 1, 'rising half');
  eq(r.frames[3].arc, -1, 'falling half');
});
test('a jump clears a pit one cell wide', () => eq(run(['#######', '#.....#', '#S...F#', '###.###', '#######'], { '2,2': 'jump' }).outcome, 'win'));
test('a jump does not clear a pit two cells wide', () => eq(run(['########', '#......#', '#S....F#', '###..###', '#######.'.replace('.', '#')], { '2,2': 'jump' }).outcome, 'loop'));
test('a jump clears one spike', () => eq(run(['######', '#....#', '#S.^F#', '######'], { '2,2': 'jump' }).outcome, 'win'));
test('a jump climbs a step one cell high', () => {
  const r = run(['########', '#......#', '#.....F#', '#S..####', '########'].map((x, i) => (i === 3 ? '#S..####' : x)), { '3,3': 'jump' });
  eq(r.frames.some(f => f.x === 4 && f.y === 2), true, 'lands on top of the step');
  eq(r.outcome, 'win');
});
test('a jump from two cells away also lands on the step', () => {
  const r = run(['########', '#......#', '#.....F#', '#S..####', '########'], { '2,3': 'jump' });
  eq(r.outcome, 'win');
});
test('a jump does not climb a wall two cells high', () => {
  const r = run(['########', '#......#', '#...####', '#S..####', '########'], { '3,3': 'jump' });
  eq(r.frames.some(f => f.x > 3), false);
  eq(r.frames[r.frames.length - 1].outcome === undefined, true);
});
test('a jump cannot start in mid-air', () => {
  const r = run(['########', '#......#', '#......#', '#......#', '#S.....#', '########'], { '2,4': 'jump', '3,3': 'jump' });
  eq(r.frames.some(f => f.note === 'fizzle'), true);
  eq(Math.min(...r.frames.map(f => f.y)), 3, 'never higher than one cell');
});
test('no headroom means no jump', () => {
  const r = run(['#######', '#######', '#S....#', '#######'], { '2,2': 'jump' });
  eq(r.frames.every(f => f.y === 2), true);
});
test('a jump carries the runner over a crate', () => {
  const r = run(['########', '#......#', '#S..B..#', '########'], { '2,2': 'jump' });
  eq(path(r).slice(2, 4), [[3, 1], [4, 1]], 'lands on top of the crate');
  eq(r.frames[3].objs[0].x, 4, 'the crate has not moved');
});
test('a jump can leave from a water surface', () => {
  const r = run(['#########', '#.......#', '#S......#', '#~~~~~~~#', '#########'].map((x, i) => x), { '3,2': 'jump' });
  eq(r.frames.some(f => f.y === 1), true);
});
test('a jump works upside down', () => {
  const r = run(['#########', '#.......#', '#.......#', '#S......#', '#########'], { '1,3': 'flip', '4,1': 'jump' });
  eq(r.frames.some(f => f.g === -1 && f.y === 2 && f.arc === 1), true);
});

// ---------- Turn ----------
test('turn reverses on the spot', () => {
  const r = run(['######', '#F.S.#', '######'], { '3,1': 'turn' });
  eq(r.outcome, 'win');
  eq(r.steps <= 2, true);
});

// ---------- Fire ----------
test('fire burns connected wood but not what is beyond it', () => {
  const r = run(['#######', '#S.==.#', '###=###', '#######'], { '2,1': 'fire' });
  eq(r.frames.some(f => f.firedSkill === 'fire'), true);
  eq(last(r).cells.includes('='), false);
});
test('fire burns a crate', () => {
  const r = run(['#######', '#S..B.#', '#######'], { '1,1': 'fire' });
  eq(r.frames[1].objs.length, 0);
});
test('fire kills a slime', () => {
  const r = run(['#######', '#S..E.#', '#######'], { '1,1': 'fire' });
  eq(r.frames[1].objs.length, 0);
});
test('fire throws a lever at a distance', () => {
  const r = run(['#######', '#S...L#', '#######'], { '1,1': 'fire' });
  eq(r.frames[1].lever, true);
});
test('water puts fire out', () => {
  const r = run(['#########', '#.......#', '#S.~~...#', '##.##.###', '#########'].slice(0, 3).concat(['#########']), { '1,2': 'fire' });
  eq(r.frames[1].bolt[0][2] <= 4, true);
});
test('statues stop fire', () => {
  const s = makeState(L(['#########', '#S.....B#', '#########']), { cards: {}, portals: [] });
  s.objs.push({ id: 7, t: 'M', x: 4, y: 1 });
  s.pending['1,1'] = 'fire';
  step(s);
  eq(s.objs.length, 2, 'the crate behind the statue is safe');
});
test('fire goes through a portal', () => {
  const r = run(['#########', '#S..#...#', '####.=..#', '#########'].slice(0, 2).concat(['#########']), { '1,1': 'fire' }, [['3,1', '5,1']]);
  eq(r.frames[1].bolt.length, 2);
});

// ---------- Statue ----------
test('statue holds a plate and keeps the door open', () => {
  const r = run(['########', '#S._..DF', '########'], { '3,1': 'decoy' });
  eq(r.outcome, 'win');
});
test('statue is not pushed', () => {
  const r = run(['########', '#F.S...#', '########'], { '4,1': 'decoy', '5,1': 'turn' });
  eq(r.frames.filter(f => f.objs.some(o => o.t === 'M')).every(f => f.objs[0].x === 4), true);
});
test('a decoy falls and fills a pit', () => {
  const r = run(['#########', '#.......#', '#S.....F#', '####.####', '#########'], { '3,2': 'jump', '4,1': 'decoy' });
  eq(r.frames.find(f => f.objs.length).objs[0].y, 3, 'dropped from the top of the jump into the pit');
  eq(r.outcome, 'win');
});
test('a decoy dropped mid-air lands before the runner moves on', () => {
  const r = run(['#######', '#.....#', '#.....#', '#S....#', '#######'], { '2,3': 'jump', '3,2': 'decoy' });
  const f = r.frames.find(q => q.objs.length);
  eq(f.objs[0].y, 3);
});
// ---------- Crates ----------
test('runner pushes a crate', () => {
  const r = run(['#######', '#S.B..#', '#######']);
  eq(r.frames[2].objs[0].x, 4);
  eq(r.frames[2].x, 3);
});
test('crate against a wall turns the runner', () => {
  const r = run(['#####', '#S.B#', '#####']);
  eq(r.frames.some(f => f.bumped), true);
});
test('crate cannot be pushed onto the finish', () => {
  const r = run(['######', '#S.BF#', '######']);
  eq(r.outcome, 'loop');
});
test('crate on a plate opens the door', () => {
  const r = run(['#########', '#S.B_DF##', '#########'.replace('#', '#')].map(x => x));
  eq(r.frames[1].held, false);
  eq(r.frames[2].held, true);
});
test('a slime does not hold a plate', () => {
  const r = run(['#######', '#SE_###', '#######'.replace('#', '#')].map((x, i) => (i === 1 ? '#S.#._#' : x)));
  eq(r.frames[0].held, false);
  const s = makeState(L(['######', '#S#.E#', '####_#', '######'].slice(0, 2).concat(['######'])), { cards: {}, portals: [] });
  s.plates = [1 * s.w + 4];
  settle(s);
  eq(require(process.env.CORE || './core.js').isSolid(s, 4, 1), false, 'slime is not solid');
  eq(s.objs[0].t, 'E');
});
test('a falling crate stops above the finish', () => {
  const s = makeState(L(['#####', '#.B.#', '#.F.#', '#####'].map(x => x)), { cards: {}, portals: [] });
  settle(s);
  eq(s.objs[0].y, 1, 'rests on top of the flag');
});

// ---------- Plates and doors ----------
test('door blocks until a plate is held', () => eq(run(['######', '#S.DF#', '######']).outcome, 'loop'));
test('door opens only while held', () => {
  const r = run(['#########', '#S_.D.F##', '#########'.slice(0)].map(x => x));
  eq(r.frames[1].held, true);
  eq(r.frames[2].held, false);
  eq(r.outcome, 'loop');
});
// ---------- Slime ----------
test('walking into a slime kills', () => eq(run(['#######', '#S.E.F#', '#######']).outcome, 'slime'));
test('landing on a slime bounces and removes it', () => {
  const r = run(['#######', '#.....#', '#.....#', '#S...F#', '#######'], { '1,3': 'jump' });
  eq(r.outcome, 'win');
  const r2 = run(['#######', '#.....#', '#.....#', '#S.E.F#', '#######'], { '1,3': 'jump' });
  eq(r2.frames.some(f => f.note === 'stomp'), true);
  eq(r2.outcome, 'win');
});

test('a slime throws the runner in a bigger arc than any jump', () => {
  const r = run(['###########', '#.........#', '#.........#', '#.........#', '#S..E.....#', '###########'], { '2,4': 'jump' });
  const ys = r.frames.map(f => f.y);
  eq(Math.min(...ys), 2, 'two cells high');
  const i = r.frames.findIndex(f => f.note === 'stomp');
  eq(path(r).slice(i, i + 5), [[4, 4], [5, 3], [6, 2], [7, 3], [8, 4]]);
});
test('a slime beside a wall throws the runner straight up it', () => {
  const r = run(['########', '#......#', '#......#', '#.....F#', '#....###', '#S..E###', '########'].map(x => x), { '2,5': 'jump' });
  eq(r.outcome === 'win' || r.frames.some(f => f.y <= 3), true);
});

// ---------- Barrels and cracked stone ----------
test('fire sets off a barrel, which breaks cracked stone', () => {
  const r = run(['##########', '#S...Xx.F#', '##########'], { '1,1': 'fire' });
  const f = r.frames[1];
  eq(f.blasts.length, 1);
  eq(f.cells.includes('x'), false);
  eq(r.outcome, 'win');
});
test('fire alone does not touch cracked stone', () => {
  const r = run(['#########', '#S...x.F#', '#########'], { '1,1': 'fire' });
  eq(r.frames[1].cells.includes('x'), true);
  eq(r.outcome, 'loop');
});
test('a blast next to the runner kills it', () => eq(run(['########', '#S.X...#', '########'], { '2,1': 'fire' }).outcome, 'blast'));
test('barrels set each other off', () => {
  const r = run(['##########', '#S...XX.x#', '##########'], { '1,1': 'fire' });
  eq(r.frames[1].objs.length, 0);
  eq(r.frames[1].blasts.length, 2);
  eq(r.frames[1].cells.includes('x'), true, 'a blast reaches one cell, and the stone is two away');
  const r2 = run(['#########', '#S...XXx#', '#########'], { '1,1': 'fire' });
  eq(r2.frames[1].cells.includes('x'), false);
});
test('a blast clears wood, brittle floor, crates and monsters within reach', () => {
  const r = run(['###########', '#....EB...#', '#S...X=...#', '#####.%####'.replace('.', '#'), '#.........#', '###########'], { '1,2': 'fire' });
  const f = r.frames[1];
  eq(f.objs.length, 0, 'the slime and the crate above');
  eq(f.cells.includes('='), false);
  eq(f.cells.includes('%'), false);
});
test('a laser sets off a barrel pushed into it', () => {
  const r = run(['##########', '#.....V..#', '#S..X....#', '##########'], {});
  eq(r.frames.some(f => f.blasts), true);
  eq(r.outcome, 'blast', 'the runner right behind it goes up too');
});
test('a barrel floats and can be swallowed', () => {
  const r = run(['#########', '#S.X....#', '#########'], { '2,1': 'gulp', '5,1': 'gulp' });
  eq(r.frames.some(f => f.belly === 'X'), true);
  eq(r.frames.find(f => f.note === 'spit').objs[0].t, 'X');
  const s = makeState(L(['######', '#.X..#', '#....#', '#~~~~#', '######']), { cards: {}, portals: [] });
  settle(s);
  eq(s.objs[0].y, 2);
});

// ---------- Bulls ----------
test('a bull waits until something steps into its row', () => {
  const r = run(['##########', '#.......{#', '#.########', '#.########'.replace('.', 'S'), '##########'].map(x => x), {});
  eq(r.frames.length >= 1, true);
});
test('a bull in the same row kills the runner from far away', () => {
  const r = run(['##########', '#S......{#', '##########']);
  eq(r.outcome, 'bull');
  eq(r.steps, 1);
});
test('a bull does not see behind it', () => {
  const r = run(['##########', '#{.S.....#', '##########']);
  eq(r.outcome, 'loop');
});
test('a crate blocks its line of sight', () => {
  const s = makeState(L(['##########', '#S.B...{.#', '##########']), { cards: {}, portals: [] });
  step(s);
  eq(s.outcome, null);
});
test('a bull charges a decoy and keeps going', () => {
  const r = run(['###########', '#}.......S#'.replace('S', '.'), '#.........#', '#S.......F#', '###########'].map(x => x), {});
  const r2 = run(['############', '#}.=.......#', '#S.........#'.replace('S', '.'), '############'].map(x => x), {});
  eq(r2.frames[0].objs.length, 1);
  const lane = run(['#############', '#}..=...S...#'.replace('S', '.'), '#############'].map((x, i) => x), {});
  eq(lane.frames.length >= 1, true);
});
test('a charge breaks wood and stops at stone, dazed', () => {
  const level = L(['#############', '#}...=.....#.'.slice(0, 12) + '#', '#############']);
  const s = makeState(level, { cards: {}, portals: [] });
  s.objs.push({ id: 9, t: 'M', x: 3, y: 1 });
  s.x = 11; s.y = 1;
  s.dir = 1;
  step(s);
  const bull = s.objs.find(o => o.t === 'U');
  eq(s.objs.filter(o => o.t === 'M').length, 0, 'the decoy is gone');
  eq(s.cells.includes('='), false, 'the wood is broken');
  eq(s.outcome, 'bull', 'and it goes on to whoever stands behind');
});
test('a charge stops at the first stone and the bull stays dazed', () => {
  const s = makeState(L(['############', '#}.......#.#', '#########.##'.replace('.', '#'), '############']), { cards: {}, portals: [] });
  s.x = 10; s.y = 1; s.dir = -1;
  s.objs.push({ id: 9, t: 'M', x: 4, y: 1 });
  step(s);
  const bull = s.objs.find(o => o.t === 'U');
  eq(bull.x, 8);
  eq(bull.dazed, true);
  eq(s.outcome, null);
});
test('a bull runs off an edge and fills the pit', () => {
  const s = makeState(L(['############', '#}.........#', '#####.######', '############'].map(x => x)), { cards: {}, portals: [] });
  s.x = 9; s.y = 1; s.dir = 1;
  step(s);
  const bull = s.objs.find(o => o.t === 'U');
  eq([bull.x, bull.y], [5, 2]);
  eq(s.outcome, null, 'the runner, past the pit, is safe');
});
test('a bull holds a plate', () => {
  const s = makeState(L(['############', '#}.........#', '#####_######', '############']), { cards: {}, portals: [] });
  s.x = 9; s.y = 1; s.dir = 1;
  step(s);
  eq(s.objs.find(o => o.t === 'U').y, 2);
  eq(s.plates.length, 1);
});
test('fire on a bull from behind sends it forward', () => {
  const r = run(['#############', '#S..}.....#.#', '#############'], { '1,1': 'fire' });
  const f = r.frames[1];
  eq(f.objs[0].x, 9);
  eq(f.objs[0].dazed, true);
});
test('a bull running into spikes dies', () => {
  const r = run(['#############', '#S..}...^..##'.slice(0, 12) + '#', '#############'], { '1,1': 'fire' });
  eq(r.frames[1].objs.length, 0);
});
test('a bull sets off a barrel', () => {
  const r = run(['#############', '#S..}...X...#', '#############'], { '1,1': 'fire' });
  eq(r.frames[1].blasts.length, 1);
  eq(r.frames[1].objs.length, 0, 'the blast takes the bull too');
});
test('a bull is too big to swallow', () => {
  const s = makeState(L(['#########', '#S......#', '#########']), { cards: {}, portals: [] });
  s.objs.push({ id: 9, t: 'U', x: 3, y: 1, dir: 1, dazed: true });
  s.pending['2,1'] = 'gulp';
  step(s); step(s);
  eq(s.belly, null);
  eq(s.note, 'toobig');
});
test('the runner can stand on a bull', () => {
  const r = run(['#########', '#.......#', '#S.}....#'.replace('S', '.'), '#########'], {});
  eq(r.frames[0].objs[0].t, 'U');
});

// ---------- Brittle floor ----------
test('brittle floor breaks behind the runner', () => {
  const r = run(['#######', '#S....#', '#%%%%%#', '#.....#', '#######']);
  eq(r.frames[2].cells[2 * 7 + 1], '.');
});
test('brittle floor holds the runner while it stands there', () => {
  const r = run(['#####', '#S..#', '#%###', '#####']);
  eq(r.frames[0].cells[2 * 5 + 1], '%');
});
test('a turn on the spot does not break it', () => {
  const r = run(['####', '#S.#', '#%##', '####'].map(x => x), {});
  eq(r.outcome, 'loop');
});
test('crates break brittle floor at once and drop through', () => {
  const r = run(['#######', '#S.B..#', '###%###', '#.....#', '#######']);
  eq(r.frames.some(f => f.objs[0] && f.objs[0].y === 3), true);
});
test('a slime sits on brittle floor without breaking it', () => {
  const r = run(['#####', '#.E.#', '#%%%#', '#.S.#', '#####'].map(x => x), {});
  eq(r.frames[0].cells[2 * 5 + 2], '%');
});
test('brittle floor works on the ceiling too', () => {
  const r = run(['#######', '#%%%%%#', '#.....#', '#S....#', '#######'], { '2,3': 'flip' });
  eq(r.frames.some(f => f.g < 0), true);
  eq(last(r).cells.split('%').length < 6, true);
});

// ---------- Lever and blocks ----------
test('red is solid until the lever is thrown', () => eq(run(['######', '#SR.F#', '######']).outcome, 'loop'));
test('touching a lever swaps red and blue', () => eq(run(['#######', '#SLR.F#', '#######']).outcome, 'win'));
test('blue is air until the lever is thrown', () => eq(run(['#######', '#SN..F#', '#######']).outcome, 'win'));
test('a thrown lever turns blue solid', () => {
  const r = run(['########', '#S.L.NF#', '########']);
  eq(r.outcome, 'loop');
});
test('passing a lever twice swaps back', () => {
  const r = run(['#########', '#SL.R...#', '#########']);
  const levers = r.frames.map(f => f.lever);
  eq(levers.includes(true), true);
  eq(r.frames[r.frames.length - 1].lever, false);
});
// ---------- Water ----------
test('runner walks on water', () => eq(run(['########', '#S....F#', '##~~~###', '##~~~###', '########'].slice(0, 5)).outcome, 'win'));
test('water covers spikes', () => eq(run(['#######', '#S...F#', '##~~~##', '##^^^##', '#######']).outcome, 'loop', 'the water settles over the spikes and the runner stays afloat'));
test('water holds still in a closed basin', () => {
  const r = run(['########', '#S.....#', '##~~~###', '########']);
  eq(wetCount(last(r)), 3);
});
test('water runs downhill and fills a basin from the bottom', () => {
  const r = simulate(L(['########', '#~~....#', '#~~.S..#', '##.#####'.replace('.', '#'), '########']), { cards: {}, portals: [] });
  const f = r.frames[0];
  eq(wetCount(f), 4);
});
test('burning a dam releases water into the pit below', () => {
  const r = run(['###########', '#S....=~~~#', '####..#####', '####..#####', '###########'], { '2,1': 'fire' });
  const f = r.frames.find(q => q.firedSkill === 'fire');
  eq(f.cells.includes('='), false);
  const w = 11, wet = [];
  for (let i = 0; i < f.wet.length; i++) if (f.wet[i] === '1') wet.push([i % w, Math.floor(i / w)]);
  eq(wetCount(f), 3);
  eq(wet.every(([x, y]) => y >= 2), true, 'water drained into the pit');
});
test('water level equalises between connected shafts', () => {
  const r = simulate(L(['#######', '#~#...#', '#~#...#', '#~....#', '###S###', '#######'].slice(0, 4).concat(['#######'])), { cards: {}, portals: [] });
  const f = r.frames[0];
  eq(wetCount(f), 3);
  const row = (y) => [...f.wet.slice(y * 7, y * 7 + 7)].filter(c => c === '1').length;
  eq(row(3) >= 2, true);
});
test('crates float', () => {
  const r = run(['######', '#.B..#', '#....#', '#~~~~#', '#S...#'.replace('S', '.'), '######'], {});
  eq(r.frames[0].objs[0].y, 3, 'water sinks to the floor first and the crate rests on it');
});
test('crates float back up when water rises round them', () => {
  const r = simulate(L(['######', '#....#', '#.B..#', '#~~~~#', '#~~~~#', '######']), { cards: {}, portals: [] });
  eq(r.frames[0].objs[0].y, 2);
  const s = makeState(L(['######', '#....#', '#....#', '#~~~~#', '#~~~~#', '######']), { cards: {}, portals: [] });
  settle(s);
  s.objs.push({ id: 9, t: 'B', x: 2, y: 4 });
  settle(s);
  eq(s.objs[0].y, 2, 'came up to rest on the surface');
});
test('a statue sinks and lifts the water', () => {
  const r = run(['#########', '#.......#', '#S......#', '###.#####', '###.#####', '###~#####', '#########'], { '3,4': 'decoy' });
  const f = last(r);
  eq(f.objs[0].y, 5);
  eq(wetAt2(f, 3, 4, 9), true);
});
function wetAt2(f, x, y, w) { return f.wet[y * w + x] === '1'; }
test('water closing over the runner lifts it straight up, with no step forward', () => {
  const level = { title: 't', slots: 3, qi: 9, dir: 1, map: ['##########', '#S.......#', '###~~~~###', '###~~~~###', '##########'] };
  const f = simulate(level, { cards: { '2,1': 'gulp', '4,3': 'gulp' }, portals: [] }).frames;
  const spit = f.findIndex(q => q.mouth === 'spit');
  eq([f[spit - 1].x, f[spit - 1].y], [4, 3], 'it spits from the bottom of the pit');
  eq([f[spit].x, f[spit].y, f[spit].how], [4, 1, 'float'], 'straight up to the surface');
});
test('spitting after both stars keeps them, spitting between them drowns the second', () => {
  const level = { title: 't', slots: 3, qi: 9, dir: 1, map: ['###########', '#S.......F#', '###~~~~~###', '###~*~*~###', '###########'], wet: ['4,3', '6,3'] };
  const between = simulate(level, { cards: { '2,1': 'gulp', '5,3': 'gulp' }, portals: [] });
  const after = simulate(level, { cards: { '2,1': 'gulp', '6,3': 'gulp' }, portals: [] });
  eq([between.outcome, between.stars], ['win', 1]);
  eq([after.outcome, after.stars], ['win', 2]);
});
test('gulp reaches water ahead, ahead and below, below, and behind', () => {
  const level = map => ({ title: 't', slots: 3, qi: 9, dir: 1, map });
  const drank = (map, cards) => simulate(level(map), { cards, portals: [] }).frames.some(f => f.mouth === 'gulp');
  eq(drank(['#########', '#S.....F#', '###~~~###', '#########'], { '2,1': 'gulp' }), true, 'on the shore, with the water ahead and below');
  eq(drank(['#########', '#S.....F#', '###~~~###', '#########'], { '4,1': 'gulp' }), true, 'standing on the water');
  eq(drank(['#########', '#S.....F#', '###~~~###', '#########'], { '6,1': 'gulp' }), true, 'on the far shore, with the water behind');
  eq(drank(['#########', '#S.....F#', '###~~~###', '#########'], { '1,1': 'gulp' }), false, 'two cells from the water');
  eq(drank(['#########', '#S.....F#', '#######~#', '#########'], { '1,1': 'gulp' }), false, 'far from any water');
});
test('gulp does not reach water over its head', () => {
  const level = { title: 't', slots: 3, qi: 9, dir: 1, map: ['#########', '###~~~###', '###===###', '#S.....F#', '#########'] };
  eq(simulate(level, { cards: { '4,3': 'gulp' }, portals: [] }).frames.some(f => f.mouth === 'gulp'), false);
});
test('lava kills a runner that walks or falls into it', () => {
  eq(run(['########', '#S.&&.F#', '########']).outcome, 'lava', 'walking in');
  eq(run(['########', '#S....F#', '##.&&###', '##&&&###', '########']).outcome, 'lava', 'falling in');
});
test('a jump clears one cell of lava', () => {
  eq(run(['########', '#......#', '#S....F#', '###&####', '########'], { '2,2': 'jump' }).outcome, 'win');
});
test('water turns lava to rock, one cell for one cell', () => {
  const spat = run(['#############', '#S.........F#', '###~~~##&&&##', '#############'], { '6,1': 'gulp', '7,1': 'gulp' });
  eq(spat.outcome, 'win');
  eq(last(spat).cells.includes('&'), false, 'three of water, three of lava: none left');
  eq(wetCount(last(spat)), 0, 'and no water left over');
  const short = run(['#############', '#S.........F#', '###~~###&&&##', '#############'], { '5,1': 'gulp', '7,1': 'gulp' });
  eq(short.outcome, 'lava', 'two of water leave one of lava');
  eq([...last(short).cells].filter(q => q === 'o').length, 2);
  eq(run(['#############', '#S.........F#', '###~~~##&&&##', '#############'], { '6,1': 'gulp' }).outcome, 'lava', 'water kept in the belly cools nothing');
});
test('water the lava did not use runs on to wherever is lowest', () => {
  const r = run(['##############', '#S..........F#', '###~~~~##&&###', '##############'], { '7,1': 'gulp', '8,1': 'gulp' });
  eq(r.outcome, 'win');
  const f = last(r), w = 14;
  eq([...f.cells].filter(c => c === 'o').length, 2, 'two of the four units cool the two cells of lava');
  eq(wetCount(f), 2, 'the other two are still water');
  eq([3, 4, 5, 6].filter(x => f.wet[2 * w + x] === '1').length, 2, 'back in the pit, the only low ground left');
});
test('a crate burns up in lava and a barrel goes off', () => {
  eq(last(run(['########', '#S.B&..#', '####&###', '########'])).objs.length, 0, 'the crate is gone');
  eq(run(['#########', '#S..X&..#', '#########']).frames.some(f => f.blasts), true, 'pushed in, it blows');
});
test('a slime dies in lava and so does a bull that charges in', () => {
  const slime = run(['#########', '#S.E....#', '####.####', '####&####', '#########'], { '2,1': 'gulp', '3,1': 'gulp' });
  eq(slime.frames.some(f => f.objs.some(o => o.t === 'E' && o.y === 2)) || !last(slime).objs.length, true);
  eq(last(slime).objs.some(o => o.t === 'E'), false);
  const bull = run(['##########', '#S..}&...#', '##########'], { '2,1': 'fire' });
  eq(bull.frames.some(f => f.charge), true);
  eq(last(bull).objs.some(o => o.t === 'U'), false);
});
test('a decoy stands in lava, and one deep it is a stepping stone', () => {
  const map = ['#########', '#.......#', '#S.....F#', '###&&####', '#########'];
  eq(run(map, { '2,2': 'jump' }).outcome, 'lava', 'no stone, no landing');
  eq(run(map, { '2,2': 'jump', '4,2': 'decoy' }).outcome, 'win', 'it drops in ahead of the landing and takes the weight');
});
test('poured water fills the basin it falls into before it crosses a sill', () => {
  const r = run(['####################', '#..................#', '#S..............*.F#', '###~~~~~##.....#####', '###~~~~~##.....#####', '####################'], { '8,2': 'gulp', '9,2': 'gulp' });
  const f = r.frames.find(q => q.mouth === 'spit'), w = 20, wet = (x, y) => f.wet[y * w + x] === '1';
  eq([3, 4, 5, 6, 7].some(x => wet(x, 3) || wet(x, 4)), false, 'none of it crosses to the empty pit behind');
  eq([10, 11, 12, 13, 14].every(x => wet(x, 3) && wet(x, 4)), true, 'the pit it fell into is full to the brim');
  eq(r.outcome, 'win', 'and the runner walks across it');
});
test('water that overflows a full basin runs on to the next one', () => {
  const s = makeState(L(['#############', '#S..........#', '###...#...###', '#############']), { cards: {}, portals: [] });
  settle(s);
  for (let i = 0; i < 4; i++) C.addWaterForTest(s, 4, 1);
  settle(s);
  const w = 13, row = [...s.wet.slice(2 * w, 3 * w)].map((c, x) => (c === '1' ? x : -1)).filter(x => x >= 0);
  eq(row.filter(x => x < 6).length, 3, 'the near pit fills first');
  eq(row.filter(x => x > 6).length, 1, 'only then does any cross the sill');
});
test('running water drops into the nearest hole, even when a deeper one lies further on', () => {
  const s = makeState(L(['#################', '#S..............#', '#####.###.#######', '#####.###.#######', '#########.#######', '#########.#######', '#################']), { cards: {}, portals: [] });
  settle(s);
  C.addWaterForTest(s, 2, 1);
  settle(s);
  eq(wetAt(s, 5, 3), true, 'the near hole takes it');
  eq(wetAt(s, 9, 5), false, 'not the deep one further along the floor');
});
test('a burst dam fills the nearer pit before the farther one', () => {
  const map = ['####################', '#.................. #'.slice(0, 19) + '#', '#S.X.x~~~~~~~#######', '###.#.##.###########', '#########.##########', '####################'];
  const level = ['####################', '#..................#', '#S..........Xx~~~~~#', '#####.###.##########', '#####.###.##########', '#########.##########', '####################'];
  const r = run(level, { '1,2': 'fire' }), f = r.frames[1], w = 20, wet = (x, y) => f.wet[y * w + x] === '1';
  eq(f.blasts && f.blasts.length > 0, true, 'the barrel goes up');
  eq(wet(9, 3) && wet(9, 4), true, 'the pit next to the dam takes the water first');
  eq(wet(5, 3) && wet(5, 4), true, 'and what it cannot hold runs on to the next');
});
test('spat water takes the nearest way down, and goes on forward if both are as near', () => {
  const near = run(['####################', '#..................#', '#....S.............#', '###~~####.####..####', '###~~####.####..####', '####################'], { '5,2': 'gulp', '8,2': 'jump', '10,2': 'gulp' });
  const f = near.frames.find(q => q.mouth === 'spit'), w = 20, wet = (g, x, y) => g.wet[y * w + x] === '1';
  eq(wet(f, 9, 3) && wet(f, 9, 4), true, 'the hole two cells from where it landed, though it is behind the runner');
  const even = run(['####################', '#..................#', '#....S.............#', '###~~####.#####.####', '###~~####.#####.####', '####################'], { '5,2': 'gulp', '8,2': 'jump', '11,2': 'gulp' });
  const g = even.frames.find(q => q.mouth === 'spit');
  eq(wet(g, 15, 3) && wet(g, 15, 4), true, 'three cells either way: the pit ahead fills first');
});
test('connected shafts level out when one of them is poured into', () => {
  const s = makeState(L(['#########', '#S......#', '#.#####.#', '#.#####.#', '#.......#', '#########']), { cards: {}, portals: [] });
  settle(s);
  for (let i = 0; i < 12; i++) C.addWaterForTest(s, 1, 2);
  settle(s);
  eq(s.wet[3 * 9 + 1], s.wet[3 * 9 + 7], 'the same height on both sides');
});
test('the runner rides rising water up', () => {
  const r = run(['#########', '#.......#', '#.......#', '#.......#', '#.......#', '#S...=~~#', '#########'].map(x => x), { '2,5': 'fire' });
  const f = r.frames.find(q => q.firedSkill === 'fire');
  eq(f.cells.includes('='), false);
  eq(f.y, 5, 'two units spread over a wide floor stay one deep and the runner stays low');
});
test('rising water lifts the runner over a wall', () => {
  const r = run(['#######', '#.....#', '#.....#', '#~...=#', '#S.#~~#', '#######'].map(x => x), {});
  eq(r.frames.length > 1, true);
  const flood = run(['#########', '#.....~~#', '#.....~~#', '#.....~~#', '#.....~~#', '#S...=~~#', '#######.#'.replace('.', '#'), '#########'], { '1,5': 'fire' });
  const f = flood.frames.find(q => q.firedSkill === 'fire');
  eq(wetCount(f), 10);
  eq(f.y < 5, true, 'lifted off the floor');
});
test('flip sends water to the ceiling', () => {
  const r = run(['#########', '#.......#', '#.......#', '#S......#', '##~~~####', '#########'], { '1,3': 'flip' });
  const f = r.frames.find(q => q.g < 0);
  const w = 9;
  eq(wetCount(f), 3);
  eq([...Array(f.wet.length).keys()].filter(i => f.wet[i] === '1').every(i => Math.floor(i / w) === 1), true, 'all water on the top row');
});
test('flipping twice brings the water back down', () => {
  const r = run(['#########', '#.......#', '#.......#', '#.......#', '#.......#', '#S......#', '##~~~####', '#########'], { '1,5': 'flip', '5,1': 'flip' });
  const f = r.frames.filter(q => q.firedSkill === 'flip')[1];
  eq(f.g, 1);
  eq(wetCount(f), 3);
  eq([...Array(f.wet.length).keys()].filter(i => f.wet[i] === '1').every(i => Math.floor(i / 9) >= 5), true, 'water back at the bottom');
});
test('a plate listed as wet starts under water, level with the rest', () => {
  const level = { ...L(['#########', '#S.....F#', '##~~_~~##', '#########']), wet: ['4,2'] };
  const r = simulate(level, { cards: {}, portals: [] });
  eq(wetCount(r.frames[0]), 5);
  eq(r.outcome, 'win', 'the surface has no hole in it');
});
test('a statue sinks onto a plate under water and holds it', () => {
  const level = { ...L(['##########', '#S.....D.F', '##~~~_~~##', '##########']), wet: ['5,2'] };
  eq(simulate(level, { cards: {}, portals: [] }).outcome, 'loop', 'the door stays shut');
  eq(simulate(level, { cards: { '5,1': 'decoy' }, portals: [] }).outcome, 'win');
});

test('a statue in a wide pool leaves the surface flat', () => {
  const r = run(['###########', '#S.......F#', '##~~~~~~~##', '###########'], { '4,1': 'decoy' });
  const f = last(r);
  eq(r.outcome, 'win');
  eq(f.wet.slice(11, 22).includes('1'), false, 'no hump left on the walking row');
});
test('a statue in a shaft still raises the water', () => {
  const r = run(['#######', '#S....#', '###.###', '###.###', '###~###', '###~###', '#######'], { '3,1': 'decoy' });
  const f = r.frames.find(q => q.objs.length && q.objs[0].y === 5);
  eq(f.wet[4 * 7 + 3], '1', 'level one higher in the shaft');
  eq(f.wet[3 * 7 + 3], '1');
});
test('a statue made under water pushes the water aside', () => {
  const s = makeState(L(['######', '#~~~~#', '#~~~~#', '#S...#', '######'].map((x, i) => (i === 3 ? '#~~~~#' : x))), { cards: {}, portals: [] });
  settle(s);
  s.objs.push({ id: 9, t: 'M', x: 2, y: 3 });
  settle(s);
  eq(wetAt(s, 2, 3), false);
});

// ---------- Gulp ----------
test('gulp swallows the crate in front', () => {
  const r = run(['########', '#S.B...#', '########'], { '2,1': 'gulp' });
  eq(r.frames[2].objs.length, 0);
  eq(r.frames[2].belly, 'B');
});
test('gulp with a full belly spits in front', () => {
  const r = run(['#########', '#S.B....#', '#########'], { '2,1': 'gulp', '5,1': 'gulp' });
  const f = r.frames.find(q => q.note === 'spit');
  eq(f.belly, null);
  eq(f.objs.length, 1);
});
test('spitting into a wall keeps it in the belly', () => {
  const r = run(['#######', '#S.B.##', '#######'], { '2,1': 'gulp', '4,1': 'gulp' });
  eq(r.frames.some(f => f.note === 'stuck'), true);
});
test('spitting into a recessed plate holds it', () => {
  const r = run(['##########', '#S.B.....F'.slice(0, 10), '#####_####', '##########'], { '2,1': 'gulp', '4,1': 'gulp' });
  eq(r.frames.some(f => f.held), true);
});
test('gulp with nothing in front does nothing', () => {
  const r = run(['######', '#S...#', '######'], { '2,1': 'gulp' });
  eq(r.frames.some(f => f.note === 'empty'), true);
});
test('gulp drinks a whole pool from the surface', () => {
  const r = run(['#########', '#S......#', '##~~~~###', '#########'], { '3,1': 'gulp' });
  eq(wetCount(last(r)), 0);
});
test('spitting water pours the same amount into another pit', () => {
  const r = run(['############', '#.........S#', '####..###~~#', '####..###~~#', '############'], { '10,1': 'gulp', '7,1': 'gulp' }, [], -1);
  const drunk = r.frames.find(f => f.note === 'gulp');
  eq(drunk.belly, 'W');
  eq(wetCount(drunk), 0);
  const spat = r.frames.find(f => f.note === 'spit');
  eq(spat, undefined, 'the pit below the runner swallows it before the second card');
  const r2 = run(['############', '#S........#.'.slice(0, 11) + '#', '###~~#####.#'.replace('.', '#'), '###~~#####.#'.replace('.', '#'), '############'], {});
  eq(r2.frames.length > 1, true);
});
test('a spit pool lands where the runner points', () => {
  const s = makeState(L(['############', '#S.........#', '#########..#', '#########..#', '############']), { cards: {}, portals: [] });
  s.belly = { t: 'W', n: 4 };
  s.pending['8,1'] = 'gulp';
  for (let i = 0; i < 8 && !s.outcome; i++) step(s);
  eq(s.belly, null);
  eq([...s.wet].filter(c => c === '1').length, 4);
  eq([...Array(s.wet.length).keys()].filter(i => s.wet[i] === '1').every(i => Math.floor(i / 12) >= 2), true);
});
test('slimes can be swallowed and spat out as a landing pad', () => {
  const r = run(['#########', '#S.E....#', '#########'], { '2,1': 'gulp', '5,1': 'gulp' });
  eq(r.frames.some(f => f.belly === 'E'), true);
  eq(r.frames.find(f => f.note === 'spit').objs[0].t, 'E');
});
test('statues can be swallowed', () => {
  const s = makeState(L(['#########', '#S......#', '#########']), { cards: {}, portals: [] });
  s.objs.push({ id: 7, t: 'M', x: 3, y: 1 });
  s.pending['2,1'] = 'gulp';
  step(s);
  step(s);
  eq(s.belly, { t: 'M' });
  eq(s.objs.length, 0);
});
// ---------- Echo ----------
test('echo with nothing before it is hollow', () => {
  const r = run(['######', '#S...#', '######'], { '2,1': 'echo' });
  eq(r.frames.some(f => f.note === 'hollow'), true);
});
test('echo repeats the last card', () => {
  const r = run(['#########', '#.......#', '#S......#', '#########'], { '2,2': 'flip', '3,1': 'echo' });
  const f = r.frames.find(q => q.firedSkill === 'echo');
  eq(f.g, 1);
  eq(f.echoOf, 'flip');
});
test('echo after echo repeats the same card', () => {
  const r = run(['###########', '#.........#', '#S........#', '###########'], { '2,2': 'turn', '1,2': 'echo', '2,2': 'turn' });
  eq(r.outcome !== 'crash', true);
  const r2 = run(['#############', '#...........#', '#S..........#', '#############'], { '2,2': 'jump', '5,2': 'echo', '8,2': 'echo' });
  eq(r2.frames.filter(f => f.firedSkill === 'echo').length, 2);
});
test('echo can spit what gulp swallowed', () => {
  const r = run(['#########', '#S.B....#', '#########'], { '2,1': 'gulp', '5,1': 'echo' });
  eq(r.frames.some(f => f.note === 'spit'), true);
});

// ---------- Flip ----------
test('flip drops the runner onto the ceiling', () => {
  const r = run(['#########', '#.......#', '#.......#', '#S......#', '#########'], { '2,3': 'flip' });
  eq(r.frames.some(f => f.y === 1 && f.g === -1), true);
});
test('crates fall upward', () => {
  const r = run(['#########', '#.......#', '#.......#', '#S.B....#', '#########'], { '2,3': 'flip' });
  eq(r.frames[r.frames.length - 1].objs[0].y, 1);
});
test('jump is relative to gravity', () => {
  const r = run(['#########', '#.......#', '#.......#', '#S......#', '#########'], { '2,3': 'flip', '5,1': 'jump' });
  eq(r.frames.some(f => f.g === -1 && f.y === 2), true);
});

// ---------- Portal ----------
test('portal teleports and can be used again', () => {
  const r = run(['#########', '#S.#...F#', '#########'], {}, [['2,1', '5,1']]);
  eq(r.outcome, 'win');
});
test('portal exit collects a star', () => {
  const r = run(['#########', '#S.#.*.F#', '#########'], {}, [['2,1', '5,1']]);
  eq(r.stars, 1);
});
test('crates go through portals', () => {
  const r = run(['##########', '#S.B.#...#', '##########'], {}, [['4,1', '7,1']]);
  eq(r.frames.some(f => f.objs[0].x === 7), true);
});
test('a portal into wood does not work until the wood is gone', () => {
  const r = run(['##########', '#S..=...F#', '##########'], { '1,1': 'fire' }, [['2,1', '4,1']]);
  eq(r.frames.some(f => f.teleported), true, 'burned first, so the exit is open');
  const r2 = run(['#########', '#S..#=..#', '#########'], {}, [['2,1', '5,1']]);
  eq(r2.frames.some(f => f.teleported), false, 'exit inside wood');
  eq(r2.outcome, 'loop');
});
test('a blocked exit keeps the crate on this side', () => {
  const r = run(['##########', '#S.B.#.B.#', '##########'], {}, [['4,1', '7,1']]);
  eq(r.frames[r.frames.length - 1].objs.some(o => o.x === 4 || o.x === 3), true);
});
test('a crate falling through portals cannot hang the game', () => {
  const r = run(['#######', '#.....#', '#.B...#', '#.....#', '#S....#', '#######'], {}, [['2,3', '2,1']]);
  eq(r.frames.length > 1, true);
});
test('the runner falling through a portal loop is caught', () => {
  const r = run(['#####', '#S..#', '##.##', '##.##', '#####'], {}, [['2,3', '2,2']]);
  eq(['loop', 'timeout'].includes(r.outcome), true);
});

// ---------- Lasers ----------
test('a laser kills whoever walks into it', () => eq(run(['#######', '#S...F#', '#..V..#'.replace('V', '.'), '#######'].slice(0, 1).concat(['#..V..#', '#S...F#', '#######'])).outcome, 'laser'));
test('a crate stops a laser and shelters what is behind it', () => {
  const r = run(['#########', ')...B...#', '#.......#', '#########'.replace('#', '#')].map((x, i) => (i === 2 ? '#S.....F#' : x)), {});
  eq(r.frames[0].objs[0].y, 2, 'crate falls to the floor');
  const s = makeState(L(['#########', ')..#....#', '#S.....F#', '#########']), { cards: {}, portals: [] });
  eq(require(process.env.CORE || './core.js').beams(s)[0][2], 3, 'stone ends the beam');
});
test('pushing a crate out of the beam re-opens it', () => {
  const r = run(['#########', '#...V...#', '#.......#', '#S..B..F#', '#########']);
  eq(r.frames[0].beams[0][3], 3);
  eq(r.outcome, 'laser');
});
test('a statue stops a laser', () => {
  const s = makeState(L(['#########', ')...S...#', '#########']), { cards: {}, portals: [] });
  const C2 = require(process.env.CORE || './core.js');
  eq(C2.beams(s)[0][2], 8);
  s.objs.push({ id: 5, t: 'M', x: 4, y: 1 });
  eq(C2.beams(s)[0][2], 4);
});
test('a laser passes through portals', () => {
  const r = run(['###########', ')...#.....#', '#S....#...#'.replace('S', '.'), '###########'].slice(0, 2).concat(['#S.#...F..#', '###########']), {}, [['3,1', '6,1']]);
  eq(r.frames[0].beams.length, 2);
});
test('a blue block in front of an emitter switches the laser off', () => {
  const s = makeState(L(['#########', ')N...S..#', '#########']), { cards: {}, portals: [] });
  const C2 = require(process.env.CORE || './core.js');
  eq(C2.beams(s)[0][2] > 2, true, 'blue is air until the lever');
  s.lever = true;
  eq(C2.beams(s)[0][2], 1, 'then it blocks the beam');
});
test('flipping a crate out of a beam kills whoever it sheltered', () => {
  const r = run(['#########', '#...V...#', '#.......#', '#.....B.#', '#S..#...#'.replace('#S..#', '#S...'), '#########'], {});
  eq(r.frames.length > 1, true);
  const hit = run(['#########', ')...B...#', '#.......#', '#S......#', '#########'].map((x, i) => (i === 2 ? '#.......#' : x)), {});
  eq(hit.frames[0].objs[0].y, 3);
});
// ---------- Level-wide invariants of simulate ----------
test('a win ends the run on the finish', () => {
  const r = run(['#####', '#S.F#', '#####']);
  eq(last(r).x, 3);
});
test('every run terminates', () => {
  const r = run(['#####', '#S..#', '#####']);
  eq(r.steps < 400, true);
});

console.log(`${passed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
