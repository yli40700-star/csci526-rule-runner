// An independent model of water, used only to check the rules: droplets on a fine grid, one to a place, so volume is exact. Gravity pulls
// them down and they run sideways off whatever they land on. Pressure levels out any connected body. It shares no code with core.js.
const N = 6;   // Fine cells per cell, each way.

function makeGrid(w, h, open) {
  const W = w * N, H = h * N, solid = new Uint8Array(W * H);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) solid[y * W + x] = open[Math.floor(y / N) * w + Math.floor(x / N)] ? 0 : 1;
  return { W, H, solid, occ: new Uint8Array(W * H), dir: new Int8Array(W * H), mark: new Uint32Array(W * H), stamp: 0 };
}

function fill(G, cx, cy) {
  for (let y = cy * N; y < (cy + 1) * N; y++) for (let x = cx * N; x < (cx + 1) * N; x++) {
    const i = y * G.W + x;
    if (!G.solid[i]) { G.occ[i] = 1; G.dir[i] = (x ^ y) & 1 ? 1 : -1; }
  }
}

function tick(G, g, rnd, hot) {
  const { W, H, solid, occ, dir, mark } = G, stamp = ++G.stamp;
  const free = (x, y) => x >= 0 && y >= 0 && x < W && y < H && !solid[y * W + x] && !occ[y * W + x];
  let moved = 0;
  for (let row = 0; row < H; row++) {
    const y = g > 0 ? H - 1 - row : row, forward = stamp & 1;
    for (let k = 0; k < W; k++) {
      const x = forward ? k : W - 1 - k, i = y * W + x;
      if (!occ[i] || mark[i] === stamp) continue;
      let cx = x, cy = y, d = dir[i];
      for (let f = 0; f < 4 && free(cx, cy + g); f++) cy += g;
      if (cy === y) {
        if (free(cx + d, cy) && free(cx + d, cy + g)) { cx += d; cy += g; }   // Never through the gap where two blocks touch at a corner.
        else if (free(cx - d, cy) && free(cx - d, cy + g)) { d = -d; cx += d; cy += g; }
        else {
          for (let r = 0; r < 6; r++) {
            if (!free(cx + d, cy)) { if (r === 0) d = -d; break; }
            cx += d;
            if (free(cx, cy + g)) break;
          }
        }
      }
      if (cx === x && cy === y) { dir[i] = d; continue; }
      moved++;
      occ[i] = 0;
      const n = cy * W + cx;
      if (hot && hot.take(cx, cy)) continue;
      occ[n] = 1;
      dir[n] = d;
      mark[n] = stamp;
    }
  }
  return moved;
}

/** Hydrostatics: in each connected body the highest droplet moves to the lowest gap, until the surface is level. */
function level(G, g, hot) {
  const { W, H, solid, occ } = G, seen = new Uint8Array(W * H);
  let moved = 0;
  for (let s = 0; s < W * H; s++) {
    if (!occ[s] || seen[s]) continue;
    const body = [s];
    seen[s] = 1;
    for (let q = 0; q < body.length; q++) {
      const c = body[q], x = c % W, nb = [x > 0 ? c - 1 : -1, x < W - 1 ? c + 1 : -1, c - W, c + W];
      for (const n of nb) if (n >= 0 && n < W * H && occ[n] && !seen[n]) { seen[n] = 1; body.push(n); }
    }
    if (body.length < 8) continue;
    for (let round = 0; round < 40; round++) {
      let top = -1, topDepth = Infinity, gap = -1, gapDepth = -Infinity;
      for (const c of body) {
        if (!occ[c]) continue;
        const y = Math.floor(c / W), x = c % W, depth = y * g;
        if (depth < topDepth) { topDepth = depth; top = c; }
        for (const n of [x > 0 ? c - 1 : -1, x < W - 1 ? c + 1 : -1, c - W, c + W]) {
          if (n < 0 || n >= W * H || solid[n] || occ[n]) continue;
          const d = Math.floor(n / W) * g;
          if (d > gapDepth) { gapDepth = d; gap = n; }
        }
      }
      if (gap < 0 || gapDepth - topDepth < 2) break;
      occ[top] = 0;
      moved++;
      if (hot && hot.take(gap % W, Math.floor(gap / W))) continue;
      occ[gap] = 1;
      G.dir[gap] = gap & 1 ? 1 : -1;
      body.push(gap);
    }
  }
  return moved;
}

function lowestGap(G, g, from) {
  const { W, H, solid, occ } = G, seen = new Set([from]), body = [from];
  let gap = -1, gapDepth = -Infinity;
  for (let q = 0; q < body.length; q++) {
    const c = body[q], x = c % W;
    for (const n of [x > 0 ? c - 1 : -1, x < W - 1 ? c + 1 : -1, c - W, c + W]) {
      if (n < 0 || n >= W * H || solid[n] || seen.has(n)) continue;
      seen.add(n);
      if (occ[n]) body.push(n);
      else if (Math.floor(n / W) * g > gapDepth) { gapDepth = Math.floor(n / W) * g; gap = n; }
    }
  }
  return gap;
}

/** Lets water that was in prev move as gravity takes it inside the geometry of f, and returns how many units end in each cell. */
function settleRef(job, seed = 1) {
  const { w, h, g, start, open, pour, burn } = job;
  const G = makeGrid(w, h, open);
  let s = seed; const rnd = () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
  for (let i = 0; i < w * h; i++) if (start[i]) fill(G, i % w, Math.floor(i / w));
  const hot = burn && burn.size ? {
    left: new Map([...burn].map(i => [i, N * N])),
    take(x, y) {
      const i = Math.floor(y / N) * w + Math.floor(x / N), n = this.left.get(i);
      if (!n) return false;
      if (n === 1) {
        this.left.delete(i);
        for (let fy = Math.floor(i / w) * N; fy < (Math.floor(i / w) + 1) * N; fy++) for (let fx = (i % w) * N; fx < ((i % w) + 1) * N; fx++) { G.solid[fy * G.W + fx] = 1; G.occ[fy * G.W + fx] = 0; }
      } else this.left.set(i, n - 1);
      return true;
    },
  } : null;
  let waiting = pour ? pour.n * N * N : 0, still = 0;
  for (let t = 0; t < 40000; t++) {
    if (waiting > 0) {
      const x0 = pour.x * N + Math.floor(N / 2) - 2, y0 = pour.y * N + Math.floor(N / 2);
      for (let k = 0; k < 8 && waiting > 0; k++) {
        const x = x0 + (k % 5);
        let y = y0;
        while (y >= 0 && y < G.H && G.occ[y * G.W + x]) y -= g;   // Spat out under water, it joins the surface.
        const i = y * G.W + x;
        if (y >= 0 && y < G.H && !G.solid[i] && !G.occ[i]) { G.occ[i] = 1; G.dir[i] = pour.dir; waiting--; }
        else {   // Roofed in: it comes out of the lowest gap of the water it was let go in.
          const gap = lowestGap(G, g, y0 * G.W + x);
          if (gap >= 0) { G.occ[gap] = 1; G.dir[gap] = pour.dir || 1; waiting--; }
        }
      }
    }
    let moved = tick(G, g, rnd, hot);
    if (t % 8 === 0) moved += level(G, g, hot);
    still = moved === 0 && waiting === 0 ? still + 1 : 0;
    job.lastTick = t;
    if (job.trace && t % job.trace === 0) { const m = []; for (let ry = 0; ry < h; ry++) { let c = 0; for (let k = ry * N * G.W; k < (ry + 1) * N * G.W; k++) c += G.occ[k]; if (c) m.push(ry + ':' + (c / (N * N)).toFixed(1)); } console.log(t, moved, m.join(' ')); }
    if (still > 6) break;
  }
  const mass = new Float32Array(w * h);
  for (let y = 0; y < G.H; y++) for (let x = 0; x < G.W; x++) if (G.occ[y * G.W + x]) mass[Math.floor(y / N) * w + Math.floor(x / N)] += 1 / (N * N);
  return { mass, leftover: waiting / (N * N), G, ticks: job.lastTick };
}

/** The largest amount of water, in cells, on the wrong side of any column or row, between the two results. */
function spread(w, h, mass, wet) {
  let dx = 0, dy = 0;
  for (let x = 0, a = 0, b = 0; x < w; x++) { for (let y = 0; y < h; y++) { a += mass[y * w + x]; b += wet[y * w + x] === '1' ? 1 : 0; } dx = Math.max(dx, Math.abs(a - b)); }
  for (let y = 0, a = 0, b = 0; y < h; y++) { for (let x = 0; x < w; x++) { a += mass[y * w + x]; b += wet[y * w + x] === '1' ? 1 : 0; } dy = Math.max(dy, Math.abs(a - b)); }
  return { dx, dy };
}

/** Water on the wrong stretch of floor or ceiling: both results are summed along each unbroken run of open cells in a row. A thin film counts for nothing. */
function misplaced(w, h, open, mass, wet) {
  let out = 0;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (!open[y * w + x]) continue;
      let a = 0, b = 0;
      for (; x < w && open[y * w + x]; x++) { a += mass[y * w + x] >= 0.25 ? mass[y * w + x] : 0; b += wet[y * w + x] === '1' ? 1 : 0; }
      out += Math.abs(a - b);
    }
  }
  return out / 2;
}

module.exports = { settleRef, spread, misplaced };
