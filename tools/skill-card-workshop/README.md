# Skill-card workshop (prototype)

A playable draft of a new mechanic, made for the team's new-mechanic discussion. It is not part of the Unity game: nothing under `Assets/` uses it.

**Play:** open `docs/proto/skill-card-workshop.html` in a browser. It is one self-contained file and needs no server. The page is in Chinese.

## The idea

- Each level has a few skill slots and a budget of qi. The player fills the slots from a library of eight skills, then drags skill cards onto map cells.
- The runner walks by itself and turns round at walls. A card fires the first time the runner reaches its cell.
- Nothing runs on a timer. The world only changes when the runner touches it.
- Skills: Jump, Turn, Gulp, Echo (1 qi each), Fire, Decoy (2), Flip, Portal (3).
- Machines: wood, brittle floor, cracked stone, spikes, water, lava, crates, barrels, slime, a bull, plates and doors, a lever with red and blue blocks, and lasers.
- 22 levels. Levels 14 to 20 and 22 are the hard ones. Levels 1 to 13 and 21 show a short card the first time they open.
- Every level has a 最优解 (best solution) button that loads a three-star plan found by the solver.

## Files

| File | What it is |
| --- | --- |
| `core.js` | The rules: a deterministic step simulation, no drawing |
| `levels.js` | The 22 maps, slot counts, qi budgets and intro cards |
| `page.html` | The page template: UI, drawing, animation and the water effect |
| `build.py` | Puts the four parts together into `docs/proto/skill-card-workshop.html` |
| `survey.json` | The solver's results, used for each level's best plan and route count |
| `solver.js`, `survey.js` | Search every loadout and card placement for each level |
| `rules.test.js` | Rule tests, one small map per rule |
| `fuzz.js` | Random plans on every level, checked against the world's invariants |
| `refwater.js`, `audit-water.js` | An independent water model, and a check of the water rule against it |

## Change a level

1. Edit `levels.js`.
2. From this folder, run `node survey.js <level id>`. It re-solves that level and updates `survey.json`. Level 20 takes a few minutes.
3. Run `python3 build.py` to rebuild the page.

## Checks

Run these from this folder. They need Node only, not Unity.

- `node rules.test.js`: 141 rule tests.
- `node fuzz.js 2000`: 2,000 random plans per level. It prints any broken invariant.
- `node audit-water.js 200`: compares the water rule with the independent model. Add `flip` to include gravity flips.

## Known limits

- The solver does not finish 3 of level 20's loadouts. It also does not try every portal that only a crate, fire or a laser passes through. The best qi shown is therefore the best found, not a proof.
- The page has only been tested in Chrome.
