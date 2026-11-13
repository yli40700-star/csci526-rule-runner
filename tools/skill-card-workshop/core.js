// CORE-START
/*
 * Side-on grid. Nothing moves unless the runner touches it: there is no clock and no timed machinery.
 * Water and crates can travel a long way, but they always finish inside the step that disturbed them.
 *
 * Map chars:
 *   . empty   # stone   = wood    % brittle floor   x cracked stone (only a blast breaks it)   ^ spikes   ~ water
 *   & lava (too thick to flow; water turns it to o, cooled rock, one cell of water to one cell of lava)
 *   B crate   X barrel  E slime   { } bull facing left and right   _ plate   D door (open while a plate is held)
 *   L lever   R red block (solid until the lever is thrown)   N blue block (solid once it is)
 *   ( ) A V laser emitters firing left, right, up and down
 *   * star    S start   F finish
 */
const SKILLS = {
  jump:   { name: 'Jump',   zh: '跳跃',   cost: 1, color: '#4c8dff', desc: '跳过面前一格，落回地面。能越过 1 格宽的坑，也能跳上 1 格高的台阶。只能从地面起跳。' },
  turn:   { name: 'Turn',   zh: '掉头',   cost: 1, color: '#2fbfa5', desc: '在这一格立刻掉头。撞墙时小人会自己掉头。' },
  gulp:   { name: 'Gulp',   zh: '吞吐',   cost: 1, color: '#f0a030', desc: '吞下正前方的箱子、炸药桶、替身或史莱姆；没东西可吞，就喝光挨着身体的一整片水，站岸边或水面上都行。嘴里有东西：吐到正前方一格，水会就近往低处流。' },
  echo:   { name: 'Echo',   zh: '回响',   cost: 1, color: '#b98cff', desc: '把上一张发动的卡再发动一次。贵的卡这样用更省气，但它要占一个卡槽。' },
  fire:   { name: 'Fire',   zh: '喷火',   cost: 2, color: '#ff6b3d', desc: '张嘴喷火，烧到碰见的第一样东西：木头烧光相连的一片，箱子和史莱姆烧没，炸药桶引爆，拉杆隔空扳动，烧蛮牛屁股它会往前冲。水能灭火。' },
  decoy:  { name: 'Decoy',  zh: '替身',   cost: 2, color: '#8a93a8', desc: '在脚下留一个石头替身，自己接着走。它能压住压力板、挡激光、挨蛮牛的撞。沉进水里照样压板，一格深的岩浆里能当垫脚石。' },
  flip:   { name: 'Flip',   zh: '反重力', cost: 3, color: '#e05fb0', desc: '重力上下颠倒，小人、箱子和水全部掉向另一边。' },
  portal: { name: 'Portal', zh: '传送门', cost: 3, color: '#31c7e8', desc: '放两个门，进一个就从另一个出来，可以反复用。箱子、火和激光也会穿过去。两门最远隔 6 格。' },
};
const SKILL_IDS = Object.keys(SKILLS);
const MAX_STEPS = 400;
const PORTAL_RANGE = 6;
// a is a step up and forward, b a step down and forward: a jump is one arc over one cell, a slime throws you over a bigger one.
const JUMP_ARC = ['a', 'b'];
const BOUNCE_ARC = ['a', 'a', 'b', 'b'];
const EMITTERS = { '(': [-1, 0], ')': [1, 0], A: [0, -1], V: [0, 1] };

const cellKey = (x, y) => x + ',' + y;
const parseKey = k => k.split(',').map(Number);
const inPortalRange = (a, b) => { const [ax, ay] = parseKey(a), [bx, by] = parseKey(b); return Math.max(Math.abs(ax - bx), Math.abs(ay - by)) <= PORTAL_RANGE; };

/** Fresh world for a level. Cells and water live in strings so that cloning a state stays cheap. */
function makeWorld(level) {
  const w = level.map[0].length, h = level.map.length;
  let cells = '', wet = '', sx = 0, sy = 0, nextId = 0;
  const objs = [], plates = [], emitters = [];
  level.map.forEach((row, y) => {
    for (let x = 0; x < w; x++) {
      const c = row[x];
      wet += c === '~' ? '1' : '0';
      if (c === 'S') { sx = x; sy = y; cells += '.'; }
      else if (c === 'B' || c === 'E' || c === 'X') { objs.push({ id: nextId++, t: c, x, y }); cells += '.'; }
      else if (c === '{' || c === '}') { objs.push({ id: nextId++, t: 'U', x, y, dir: c === '{' ? -1 : 1, dazed: false }); cells += '.'; }
      else if (c === '~') cells += '.';
      else {
        cells += c;
        if (c === '_') plates.push(y * w + x);
        if (EMITTERS[c]) emitters.push({ x, y, dx: EMITTERS[c][0], dy: EMITTERS[c][1] });
      }
    }
  });
  // The map holds one char per cell, so a star or a plate under water is listed in level.wet.
  (level.wet || []).forEach(k => { const [x, y] = parseKey(k), i = y * w + x; wet = wet.slice(0, i) + '1' + wet.slice(i + 1); });
  return { w, h, cells, wet, objs, nextId, plates, emitters, startX: sx, startY: sy, startDir: level.dir };
}

/** plan = { cards: { 'x,y': skill }, portals: [['x,y', 'x,y'], ...] } */
function makeState(level, plan) {
  const s = makeWorld(level);
  Object.assign(s, { x: s.startX, y: s.startY, dir: s.startDir, g: 1, queue: [], lever: false, belly: null, last: null, wetDirty: true, displaced: false, arc: 0, blasts: null, charge: null, stars: 0, t: 0, outcome: null, pending: {}, links: {}, firedAt: null, firedSkill: null, echoOf: null, note: null, bolt: null });
  Object.assign(s.pending, plan.cards);
  (plan.portals || []).forEach(([a, b]) => { s.links[a] = b; s.links[b] = a; });
  return s;
}

const cloneState = s => Object.assign({}, s, { objs: s.objs.map(o => Object.assign({}, o)), queue: s.queue.slice(), pending: Object.assign({}, s.pending), links: Object.assign({}, s.links), belly: s.belly && Object.assign({}, s.belly) });
const inMap = (s, x, y) => x >= 0 && y >= 0 && x < s.w && y < s.h;
const cellOf = (s, x, y) => s.cells[y * s.w + x];
const setCell = (s, x, y, c) => { const i = y * s.w + x; s.cells = s.cells.slice(0, i) + c + s.cells.slice(i + 1); };
const wetAt = (s, x, y) => inMap(s, x, y) && s.wet[y * s.w + x] === '1';
const setWet = (s, i, v) => { s.wet = s.wet.slice(0, i) + v + s.wet.slice(i + 1); };
const objAt = (s, x, y) => s.objs.find(o => o.x === x && o.y === y);
const removeObj = (s, o) => { s.objs.splice(s.objs.indexOf(o), 1); };
/** Heavy things sink and push water aside; everything else floats. */
const isHeavy = o => o.t === 'M' || o.t === 'U';

function plateHeld(s) {
  for (const i of s.plates) {
    const x = i % s.w, y = (i - x) / s.w;
    if (s.x === x && s.y === y) return true;
    const o = objAt(s, x, y);
    if (o && o.t !== 'E') return true;
  }
  return false;
}

function isSolid(s, x, y) {
  if (x < 0 || x >= s.w) return true;
  if (y < 0 || y >= s.h) return false;
  const c = cellOf(s, x, y);
  if (c === '#' || c === '=' || c === '%' || c === 'x' || c === 'o' || EMITTERS[c]) return true;
  if (c === 'D') return !plateHeld(s);
  if (c === 'R') return !s.lever;
  if (c === 'N') return s.lever;
  const o = objAt(s, x, y);
  return !!o && o.t !== 'E';
}

const supported = s => isSolid(s, s.x, s.y + s.g) || wetAt(s, s.x, s.y + s.g) || wetAt(s, s.x, s.y);
/** Loose objects never rest on the finish: a crate there would seal the level for good. */
const objectBlocked = (s, x, y) => !inMap(s, x, y) || isSolid(s, x, y) || !!objAt(s, x, y) || cellOf(s, x, y) === 'F';

// ---------- Water: a layer of its own, so it can stand over spikes, plates and stars ----------

function waterOpen(s, x, y) {
  if (!inMap(s, x, y)) return false;
  const c = cellOf(s, x, y);
  if (c === '#' || c === '=' || c === '%' || c === 'x' || c === 'o' || c === 'D' || c === 'F' || EMITTERS[c]) return false;
  if ((c === 'R' && !s.lever) || (c === 'N' && s.lever)) return false;
  const o = objAt(s, x, y);
  return !o || !isHeavy(o);
}

/**
 * Where a unit of water let go at a dry (x, y) comes to rest. It falls; if it lands in water it joins it; if it lands on a floor
 * it runs to the nearest way down, on either side, and falls again. With no way down left it stays. That is how a basin fills
 * from the bottom up, and why water only crosses a sill once the basin behind it is full.
 */
function runoff(s, x, y, facing = 0) {
  const drops = (cx, cy) => waterOpen(s, cx, cy + s.g) && !wetAt(s, cx, cy + s.g);
  for (let guard = s.w + s.h; guard > 0; guard--) {
    while (drops(x, y)) y += s.g;
    if (wetAt(s, x, y + s.g)) {   // It has landed in water, and is part of that water now: anywhere it can still spread to comes first.
      const room = waterGaps(s, x, y + s.g, (y + s.g) * s.g)[0];
      if (room) { [x, y] = room; continue; }
    }
    const sideways = dir => {
      for (let nx = x + dir; waterOpen(s, nx, y); nx += dir) if (drops(nx, y)) return nx - x;
      return 0;
    };
    const right = sideways(1), left = sideways(-1), gap = Math.abs(left) - Math.abs(right);
    const way = !left ? right : !right ? left : gap < 0 || (gap === 0 && facing < 0) ? left : right;   // The nearer way down. Level with both, what was spat goes on forward.
    if (!way) break;
    x += way;
  }
  return y * s.w + x;
}

/** The dry cells touching the body of water at (x, y), nearest first, leaving out any above the row limit. */
function waterGaps(s, x, y, limit = -Infinity) {
  const seen = new Set([y * s.w + x]), open = [[x, y]], gaps = [];
  for (let i = 0; i < open.length; i++) {
    const [cx, cy] = open[i];
    for (const [nx, ny] of [[cx, cy + s.g], [cx - 1, cy], [cx + 1, cy], [cx, cy - s.g]]) {
      const id = ny * s.w + nx;
      if (seen.has(id) || !waterOpen(s, nx, ny) || ny * s.g < limit) continue;
      seen.add(id);
      if (wetAt(s, nx, ny)) open.push([nx, ny]);
      else gaps.push([nx, ny]);
    }
  }
  return gaps;
}

/** Adds one unit of water at (x, y). Let go under water, it comes out at the lowest gap of that body of water. */
function addWater(s, x, y, facing = 0) {
  if (!waterOpen(s, x, y)) return false;
  if (wetAt(s, x, y)) {
    const gaps = waterGaps(s, x, y);
    if (!gaps.length) return false;
    [x, y] = gaps.reduce((a, b) => (b[1] * s.g > a[1] * s.g ? b : a));
  }
  setWet(s, runoff(s, x, y, facing), '1');
  return true;
}

/** Water always takes the shallowest unit that can get somewhere lower, until nothing can. Pressure lets it leave by any gap in its body of water, at its own level or below. */
function flowWater(s) {
  if (!s.wet.includes('1')) return;
  for (let i = 0; i < s.wet.length; i++) {
    if (s.wet[i] !== '1') continue;
    const x = i % s.w, y = (i - x) / s.w;
    if (waterOpen(s, x, y)) continue;
    setWet(s, i, '0');         // A block just closed over this water, so it is squeezed out above.
    addWater(s, x, y - s.g);
  }
  const order = [];
  for (let i = 0; i < s.wet.length; i++) order.push(i);
  const depth = i => Math.floor(i / s.w) * s.g;
  order.sort((a, b) => depth(a) - depth(b));
  const shift = () => {
    for (const i of order) {
      if (s.wet[i] !== '1') continue;
      const x = i % s.w, y = (i - x) / s.w;
      if (wetAt(s, x, y - s.g)) continue;   // Only the top of a column can run off.
      for (const [gx, gy] of waterGaps(s, x, y, depth(i))) {
        const to = runoff(s, gx, gy);
        if (depth(to) <= depth(i)) continue;
        setWet(s, i, '0');
        setWet(s, to, '1');
        return true;
      }
    }
    return false;
  };
  for (let moves = s.wet.length * 4; moves > 0 && shift(); moves--);
  if (s.displaced) spreadDisplaced(s);
  for (let i = 0; i < s.wet.length; i++) {
    if (s.wet[i] !== '1' || s.cells[i] !== '&') continue;
    s.cells = s.cells.slice(0, i) + 'o' + s.cells.slice(i + 1);   // Water and lava meet: both are gone, and there is rock to stand on.
    setWet(s, i, '0');
    s.wetDirty = true;
  }
}

/** What a decoy pushes out is one unit: on a wide surface it spreads too thin to matter, in a walled shaft it lifts the level. */
function spreadDisplaced(s) {
  s.displaced = false;
  for (let i = 0; i < s.wet.length; i++) {
    if (s.wet[i] !== '1') continue;
    const x = i % s.w, y = (i - x) / s.w, under = objAt(s, x, y + s.g);
    if (!(wetAt(s, x, y + s.g) || (under && isHeavy(under))) || wetAt(s, x - 1, y) || wetAt(s, x + 1, y) || wetAt(s, x, y - s.g)) continue;
    if (waterOpen(s, x - 1, y) || waterOpen(s, x + 1, y)) setWet(s, i, '0');
  }
}

function drink(s, x, y) {
  const open = [[x, y]];
  let n = 0;
  while (open.length) {
    const [cx, cy] = open.pop();
    if (!wetAt(s, cx, cy)) continue;
    setWet(s, cy * s.w + cx, '0');
    n++;
    open.push([cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]);
  }
  return n;
}

// ---------- Loose things ----------

/** Notes where an object went this step and how, so that the view can replay it. The rules never read this. */
function track(s, o, kind) {
  if (s.paths) (s.paths[o.id] = s.paths[o.id] || []).push([o.x, o.y, kind]);
}

/** Moves an object into a cell. A portal there carries it through, unless the far side is blocked. */
function place(s, o, x, y, kind) {
  o.x = x;
  o.y = y;
  track(s, o, kind);
  const out = s.links[cellKey(x, y)];
  if (!out) return;
  const [ex, ey] = parseKey(out);
  if (!objectBlocked(s, ex, ey) && !(s.x === ex && s.y === ey)) { o.x = ex; o.y = ey; track(s, o, 'portal'); }
}

function settleObjects(s) {
  for (let guard = 0; guard < 400; guard++) {
    let moved = false;
    for (const o of s.objs.slice()) {
      if (cellOf(s, o.x, o.y) === '&' && o.t !== 'M') {   // Stone sinks in it. Nothing else survives it.
        removeObj(s, o);
        if (o.t === 'X') explode(s, o.x, o.y);
        moved = true;
        continue;
      }
      if (isHeavy(o) && wetAt(s, o.x, o.y)) {   // A decoy made under water pushes that water aside at once.
        setWet(s, o.y * s.w + o.x, '0');
        addWater(s, o.x, o.y - s.g);
        s.wetDirty = s.displaced = true;
        moved = true;
      }
      if (!isHeavy(o) && wetAt(s, o.x, o.y)) {   // Crates and slimes bob up to the surface.
        const uy = o.y - s.g;
        if (!objectBlocked(s, o.x, uy) && !(s.x === o.x && s.y === uy)) { o.y = uy; track(s, o, 'float'); moved = true; }
        continue;
      }
      const ny = o.y + s.g;
      if (ny < 0 || ny >= s.h) { removeObj(s, o); moved = true; continue; }
      const under = cellOf(s, o.x, ny);
      if (under === '%' && o.t !== 'E') { setCell(s, o.x, ny, '.'); s.wetDirty = true; moved = true; continue; }
      if (objectBlocked(s, o.x, ny) || (s.x === o.x && s.y === ny)) continue;
      if (wetAt(s, o.x, ny)) {
        if (!isHeavy(o)) continue;
        setWet(s, ny * s.w + o.x, '0');   // Stone sinks and lifts the water it displaces.
        const was = o.y;
        o.y = ny;
        track(s, o, 'sink');
        addWater(s, o.x, was);
        s.wetDirty = s.displaced = true;
        moved = true;
        continue;
      }
      place(s, o, o.x, ny, 'fall');
      moved = true;
    }
    if (!moved) return;
  }
}

/** Loose things and water come to rest within the same step; each can disturb the other. */
function settle(s) {
  for (let i = 0; i < 12; i++) {
    settleObjects(s);
    if (!s.wetDirty) return;
    s.wetDirty = false;
    flowWater(s);
  }
}

// ---------- Rays ----------

/** Follows a straight line from next to (x, y), through portals, until stop(x, y) is true. Returns the segments drawn. */
function trace(s, x, y, dx, dy, stop) {
  const segs = [];
  let ax = x, ay = y, cx = x + dx, cy = y + dy, hops = 0;
  for (let i = 0; i < 400; i++) {
    if (!inMap(s, cx, cy) || stop(cx, cy)) break;
    const out = hops < 6 ? s.links[cellKey(cx, cy)] : null;
    if (out) {
      segs.push([ax, ay, cx, cy]);
      [ax, ay] = parseKey(out);
      hops++;
      if (stop(ax, ay)) { cx = ax; cy = ay; break; }
      cx = ax + dx;
      cy = ay + dy;
      continue;
    }
    cx += dx;
    cy += dy;
  }
  segs.push([ax, ay, cx, cy]);
  return segs;
}

function beams(s, visit = () => {}) {
  const out = [];
  for (const e of s.emitters) out.push(...trace(s, e.x, e.y, e.dx, e.dy, (x, y) => { visit(x, y); return isSolid(s, x, y); }));
  return out;
}

function burnWood(s, x, y) {
  const open = [[x, y]];
  while (open.length) {
    const [cx, cy] = open.pop();
    if (!inMap(s, cx, cy) || cellOf(s, cx, cy) !== '=') continue;
    setCell(s, cx, cy, '.');
    open.push([cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]);
  }
  s.wetDirty = true;
}

function throwLever(s) {
  s.lever = !s.lever;
  s.wetDirty = true;
}

/** A blast reaches the eight cells around it. Barrels in reach go off too, and the runner dies if it is that close. */
function explode(s, x, y) {
  const open = [[x, y]];
  while (open.length) {
    const [cx, cy] = open.pop();
    (s.blasts = s.blasts || []).push([cx, cy]);
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const nx = cx + dx, ny = cy + dy;
      if (!inMap(s, nx, ny)) continue;
      const c = cellOf(s, nx, ny), o = objAt(s, nx, ny);
      if (c === 'x' || c === '%') { setCell(s, nx, ny, '.'); s.wetDirty = true; }
      if (c === '=') burnWood(s, nx, ny);
      if (o) {
        removeObj(s, o);
        if (o.t === 'X') open.push([nx, ny]);
      }
      if (s.x === nx && s.y === ny) s.outcome = 'blast';
    }
  }
}

/** What a bull is looking at: the first thing along its row, if that is the runner or a decoy. */
function bullSees(s, o) {
  for (let x = o.x + o.dir; x >= 0 && x < s.w; x += o.dir) {
    if (s.x === x && s.y === o.y) return true;
    const t = objAt(s, x, o.y);
    if (t && t.t === 'M') return true;
    if (isSolid(s, x, o.y)) return false;
  }
  return false;
}

/** A bull runs in one go until stone stops it or the floor ends, breaking what stands in the way. Afterwards it is dazed for good. */
function charge(s, o) {
  const from = o.x;
  o.dazed = true;
  for (let i = 0; i < s.w; i++) {
    const nx = o.x + o.dir;
    if (!inMap(s, nx, o.y)) break;
    if (s.x === nx && s.y === o.y) { o.x = nx; s.outcome = 'bull'; break; }
    const c = cellOf(s, nx, o.y), t = objAt(s, nx, o.y);
    if (t && t.t === 'X') { removeObj(s, t); explode(s, nx, o.y); break; }
    if (t) removeObj(s, t);
    if (c === '=' || c === '%') { setCell(s, nx, o.y, '.'); s.wetDirty = true; }
    else if (isSolid(s, nx, o.y) || c === 'F') break;
    o.x = nx;
    track(s, o, 'charge');
    if (c === '^' || c === '&') { removeObj(s, o); break; }
    const under = o.y + s.g;
    if (!inMap(s, o.x, under) || !(isSolid(s, o.x, under) || wetAt(s, o.x, under))) break;   // Off the edge: gravity takes over.
  }
  s.charge = { id: o.id, from, to: o.x, y: o.y };
}

/** [from, to, row] for each bull that can still charge: how far along its row it can see. */
function bullSights(s) {
  const out = [];
  for (const o of s.objs) {
    if (o.t !== 'U' || o.dazed) continue;
    let x = o.x + o.dir;
    while (x >= 0 && x < s.w && !isSolid(s, x, o.y)) x += o.dir;
    out.push([o.x, x - o.dir, o.y]);
  }
  return out;
}

function bullsLook(s) {
  for (const o of s.objs.slice()) {
    if (o.t === 'U' && !o.dazed && s.objs.includes(o) && bullSees(s, o)) { charge(s, o); settle(s); }
    if (s.outcome) return;
  }
}

function fireBolt(s) {
  s.bolt = trace(s, s.x, s.y, s.dir, 0, (x, y) => {
    if (wetAt(s, x, y)) return true;
    const o = objAt(s, x, y), c = cellOf(s, x, y);
    if (o && o.t === 'X') { removeObj(s, o); explode(s, x, y); return true; }
    if (o && o.t === 'U') { if (!o.dazed) charge(s, o); return true; }
    if (o && o.t !== 'M') { removeObj(s, o); return true; }
    if (c === '=') { burnWood(s, x, y); return true; }
    if (c === 'L') { throwLever(s); return true; }
    return isSolid(s, x, y);
  });
}

// ---------- Cards ----------

function gulp(s) {
  const ax = s.x + s.dir, ay = s.y;
  if (s.belly) {
    const blocked = objectBlocked(s, ax, ay);
    if (s.belly.t === 'W') {           // Water can always be let out: with a wall ahead it wells up at the runner's feet.
      for (let i = 0; i < s.belly.n; i++) addWater(s, blocked ? s.x : ax, ay, s.dir);
      s.wetDirty = true;
    } else if (blocked) { s.note = 'stuck'; return; }
    else {
      const o = { id: s.nextId++, t: s.belly.t, x: ax, y: ay };
      s.objs.push(o);
      track(s, o, 'spit');
    }
    s.belly = null;
    s.note = s.mouth = 'spit';
    return;
  }
  const o = objAt(s, ax, ay);
  if (o && o.t === 'U') { s.note = 'toobig'; return; }
  if (o) { removeObj(s, o); s.belly = { t: o.t }; s.note = s.mouth = 'gulp'; return; }
  // Water only has to touch the runner: ahead of it, behind it, under its feet, or level with the ground it stands on.
  const back = s.x - s.dir, below = ay + s.g;
  for (const [wx, wy] of [[ax, ay], [ax, below], [s.x, below], [s.x, ay], [back, below], [back, ay]]) {
    if (!wetAt(s, wx, wy)) continue;
    s.sip = [wx, wy];
    s.belly = { t: 'W', n: drink(s, wx, wy) };
    s.wetDirty = true;
    s.note = s.mouth = 'gulp';
    return;
  }
  s.note = 'empty';
}

function fireCard(s, skill) {
  s.firedAt = cellKey(s.x, s.y);
  s.firedSkill = skill;
  switch (skill) {
    case 'jump':
      if (supported(s)) s.queue = JUMP_ARC.slice();
      else s.note = 'fizzle';   // No pushing off thin air: a jump leaves the ground and comes back to it.
      break;
    case 'turn': s.dir = -s.dir; break;
    case 'gulp': gulp(s); break;
    case 'fire': fireBolt(s); break;
    case 'decoy': {
      const o = { id: s.nextId++, t: 'M', x: s.x, y: s.y, dir: s.dir };
      s.objs.push(o);
      track(s, o, 'born');
      break;
    }
    case 'flip': s.g = -s.g; s.wetDirty = true; break;
    case 'echo':
      if (!s.last) { s.note = 'hollow'; return; }
      s.echoOf = s.last;
      fireCard(s, s.last);
      s.firedSkill = 'echo';
      return;
  }
  s.last = skill;
}

// ---------- The runner ----------

function touch(s, x, y, how) {
  const c = cellOf(s, x, y);
  if (c === '&') { s.outcome = 'lava'; return; }
  if (c === '^' && !wetAt(s, x, y)) { s.outcome = 'spike'; return; }
  const o = objAt(s, x, y);
  if (o && o.t === 'E') {
    if (how !== 'fall') { s.outcome = 'slime'; return; }
    removeObj(s, o);
    s.queue = BOUNCE_ARC.slice();   // Higher than any jump: a slime is the only big jump in the game.
    s.note = 'stomp';
  }
  if (c === '*') { s.stars++; setCell(s, x, y, '.'); s.note = 'star'; }
  if (c === 'F') { s.outcome = 'win'; return; }
  if (c === 'L') { throwLever(s); s.note = 'lever'; }
}

function enter(s, x, y, how) {
  s.x = x;
  s.y = y;
  s.how = how;
  if (y < 0 || y >= s.h) { s.outcome = 'fell'; return; }
  touch(s, x, y, how);
  if (s.outcome) return;
  const out = s.links[cellKey(x, y)];
  if (!out) return;
  const [ex, ey] = parseKey(out);
  if (isSolid(s, ex, ey)) return;   // A blocked exit (wood, a crate, a closed door) leaves the portal dead for now.
  s.via = [x, y];
  s.x = ex;
  s.y = ey;
  s.queue = [];
  s.teleported = true;
  touch(s, ex, ey, 'portal');
}

function tryWalk(s) {
  const nx = s.x + s.dir;
  const o = nx >= 0 && nx < s.w ? objAt(s, nx, s.y) : null;
  if (o && (o.t === 'B' || o.t === 'X')) {
    const bx = nx + s.dir;
    if (objectBlocked(s, bx, s.y)) { s.dir = -s.dir; s.bumped = true; return; }
    place(s, o, bx, s.y, 'push');
    enter(s, nx, s.y, 'walk');
    return;
  }
  if (isSolid(s, nx, s.y)) { s.dir = -s.dir; s.bumped = true; return; }
  enter(s, nx, s.y, 'walk');
}

function moveOnce(s) {
  while (s.queue.length) {
    if (s.queue[0] === 'b' && supported(s)) { s.queue = []; break; }   // Came down early, on a ledge.
    const m = s.queue.shift(), nx = s.x + s.dir;
    if (m === 'a') {
      const ny = s.y - s.g;
      if (!inMap(s, s.x, ny) || isSolid(s, s.x, ny)) { s.queue = []; break; }   // No headroom.
      s.arc = s.queue[0] === 'a' ? 2 : 1;
      if (!isSolid(s, nx, ny)) enter(s, nx, ny, 'air');
      else enter(s, s.x, ny, 'air');   // A wall ahead: rise straight up against it.
      return;
    }
    if (isSolid(s, nx, s.y)) { s.queue = []; break; }
    s.arc = s.queue.length ? -2 : -1;
    if (!isSolid(s, nx, s.y + s.g)) enter(s, nx, s.y + s.g, 'fall');
    else enter(s, nx, s.y, 'fall');   // The ground ahead is higher than where the arc would end.
    return;
  }
  if (!supported(s)) { enter(s, s.x, s.y + s.g, 'fall'); return; }
  tryWalk(s);
}

/** Water lifts the runner to its surface; a ceiling stops the climb. */
function surface(s) {
  for (let i = 0; i < s.h && !s.outcome && wetAt(s, s.x, s.y); i++) {
    const ny = s.y - s.g;
    if (!inMap(s, s.x, ny) || isSolid(s, s.x, ny)) return;
    enter(s, s.x, ny, 'float');
  }
}

function laserTouch(s) {
  for (let i = 0; i < 8; i++) {
    const barrels = [];
    beams(s, (x, y) => {
      if (x === s.x && y === s.y) s.outcome = 'laser';
      const o = objAt(s, x, y);
      if (o && o.t === 'X') barrels.push(o);
    });
    if (!barrels.length) return;
    for (const o of barrels) if (s.objs.includes(o)) { removeObj(s, o); explode(s, o.x, o.y); }
    settle(s);
  }
}

/** One step: a card on this cell fires and whatever it shook loose lands, then the runner moves one cell, then loose things settle. */
function step(s) {
  s.t++;
  s.firedAt = s.firedSkill = s.echoOf = s.note = s.bolt = s.blasts = s.charge = null;
  s.teleported = s.bumped = s.landed = false;
  s.how = s.via = s.sip = s.mouth = null;
  if (s.record) s.paths = {};
  s.prevArc = s.arc;
  s.arc = 0;
  const fromX = s.x, fromY = s.y, floorY = s.y + s.g, wasAir = !supported(s);
  const k = cellKey(s.x, s.y);
  const skill = s.pending[k];
  if (skill) {
    delete s.pending[k];
    fireCard(s, skill);
    settle(s);
    if (s.outcome) return;
  }
  surface(s);   // Water that has just closed over the runner carries it straight up: it does not get a step forward as well.
  if (s.outcome) return;
  if (s.how === 'float') s.queue = [];
  else moveOnce(s);
  if (s.outcome) return;
  if ((s.x !== fromX || s.y !== fromY) && inMap(s, fromX, floorY) && cellOf(s, fromX, floorY) === '%') {
    setCell(s, fromX, floorY, '.');   // Brittle floor holds the runner once and gives way behind it.
    s.wetDirty = true;
  }
  settle(s);
  surface(s);
  if (s.outcome) return;
  settle(s);
  s.landed = (wasAir || s.arc !== 0) && supported(s);
  bullsLook(s);
  if (s.outcome) return;
  laserTouch(s);
}

const worldKey = s => [s.x, s.y, s.dir, s.g, s.queue.join(''), s.lever ? 1 : 0, s.belly ? s.belly.t + s.belly.n : '', s.last || '', s.cells, s.wet, s.objs.map(o => o.t + o.x + ',' + o.y + (o.dazed ? 'z' : '')).join(';'), Object.keys(s.pending).length].join('|');

const snapshot = s => ({
  x: s.x, y: s.y, dir: s.dir, g: s.g, cells: s.cells, wet: s.wet, lever: s.lever, belly: s.belly && s.belly.t, stars: s.stars, held: plateHeld(s),
  objs: s.objs.map(o => ({ id: o.id, t: o.t, x: o.x, y: o.y, dir: o.dir, dazed: o.dazed })), firedAt: s.firedAt, firedSkill: s.firedSkill, echoOf: s.echoOf, note: s.note, bolt: s.bolt,
  blasts: s.blasts, charge: s.charge, arc: s.arc, landed: !!s.landed, how: s.how, via: s.via, sip: s.sip, mouth: s.mouth, paths: s.paths,
  beams: s.emitters.length ? beams(s) : [], sights: bullSights(s), teleported: !!s.teleported, bumped: !!s.bumped,
});

/** Plays a whole run and records one frame per step for the view. */
function simulate(level, plan) {
  const s = makeState(level, plan);
  settle(s);
  s.record = true;
  const frames = [{ ...snapshot(s), first: true }];
  const seen = new Set([worldKey(s)]);
  let outcome = 'timeout';
  for (let i = 0; i < MAX_STEPS; i++) {
    step(s);
    frames.push(snapshot(s));
    if (s.outcome) { outcome = s.outcome; break; }
    const key = worldKey(s);
    if (seen.has(key)) { outcome = 'loop'; break; }
    seen.add(key);
  }
  return { outcome, stars: s.stars, frames, steps: frames.length - 1 };
}

/** Cards can sit in any cell the runner might ever pass through: mid-air, under a crate, or in wood that will burn. */
function canPlaceAt(level, world, x, y) {
  if (!inMap(world, x, y)) return false;
  return '._L*DRN=%x'.includes(cellOf(world, x, y));
}
// CORE-END
if (typeof module !== 'undefined') module.exports = { SKILLS, SKILL_IDS, PORTAL_RANGE, EMITTERS, cellKey, parseKey, inPortalRange, makeWorld, makeState, cloneState, step, settle, simulate, canPlaceAt, cellOf, isSolid, worldKey, objAt, wetAt, supported, beams, waterOpen, addWaterForTest: addWater };
