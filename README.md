# Rule Runner

A puzzle platformer where you never press jump during a run: you program your runner with if-then rule cards,
and every rule fires every time its condition comes true, spending a limited pool of qi.

CSCI 526 team project. Unity **6000.6.0f1**, WebGL, deployed with GitHub Pages.

| Build | Link |
|---|---|
| Beta | https://yli40700-star.github.io/csci526-rule-runner/beta/ (Pages: `main` branch, `/docs` folder) |

## Project layout (one module per owner)

All game code is under `Assets/RuleRunner/`, one assembly per folder:

| Folder | What it does | Depends on |
|---|---|---|
| `Core/` | Pure C# simulator (no `MonoBehaviour`): map, conditions, actions, rules, `Simulator.Run`, `LevelAnalyzer` | nothing |
| `Levels/` | `Resources/levels.json` (all level data) + `LevelDatabase` loader | Core |
| `View/` | Grey-box shapes, `MapView`, `RunnerView` (step playback), `CameraFitter` | Core |
| `UI/` | UI toolkit (rounded sprites, theme), level select, game screen header / controls / run log, result popup | Core, Levels, View |
| `Cards/` | Skill cards, loadout tray (runner portrait, qi pool, card slots), condition / action picker, mini diagrams | Core, View, UI |
| `Analytics/` | `AnalyticsTracker`: one record per run, POST to Google Apps Script | Core |
| `App/` | `GameController`: wires everything; `Scenes/Main.unity` | all |
| `Editor/` | Level Checker window, WebGL build, scene / player settings setup | all |
| `Tests/EditMode/` | Simulator and level tests | Core, Levels |

`tools/analytics/` holds the Apps Script receiver and chart builder (see its README).

## Common tasks

- **Run tests**: Window > General > Test Runner > EditMode > Run All (all levels, rule edge cases, prototype combination counts).
- **Design a level**: edit `Assets/RuleRunner/Levels/Resources/levels.json`, then *Rule Runner > Level Checker* to see every winning
  rule combination, the best qi, fewest rules and how the previous level's solution does. Set `"draft": true` to hide a level.
- **Build WebGL**: *Rule Runner > Build WebGL Beta* (outputs `docs/beta/`), or from a terminal:
  ```
  Unity -batchmode -quit -projectPath . -executeMethod RuleRunner.EditorTools.BuildWebGL.BuildBeta
  ```
  Each milestone has its own folder (`docs/alpha`, `docs/beta`, `docs/gold`) so older links keep working.
- **Analytics**: put the Apps Script `/exec` URL into `Assets/RuleRunner/Analytics/Resources/analytics_config.json`.

## Rules of the simulation

Design doc section 3 is the source of truth; `Core/Simulator.cs` implements it step by step and the tests pin the
level data to it. Do not change the step order without re-running the tests and the Level Checker.

## External references

- Unity 6 (6000.6.0f1), uGUI 2.6.0, Unity Test Framework 1.8.0.
- [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (editor-only package, not included in builds): lets an AI assistant
  drive the Unity editor during development.
- Claude Code (Anthropic) was used as an AI coding assistant for the simulator, UI, tools and tests.
- Google Apps Script / Google Sheets for play-test analytics.
- No third-party art, audio or code assets; all visuals are procedural grey-box shapes.
