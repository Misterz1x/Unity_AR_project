# CLAUDE.md — AR Golf

Context for Claude Code in this repo. Student project for the course Virtual & Augmented Reality. The README has the concept and open questions.

## What we are building
AR phone app for putting practice (mini golf later). Phone finds the floor, a virtual hole is placed, the player hits, and a **virtual ball** rolls to the hole with our own roll model.

How the hit is measured is **still open** (see README, "Open questions"):
- real club tracked (marker/tag, motion sensor, or phone on club) hitting a virtual ball, or
- real ball tracked by the camera over its first 60–80 cm, then handed over to the virtual ball.

Until that is decided, every input method must produce the same `LaunchResult`, and `SwipeLauncher` stands in for it.

## Fixed decisions (ask before changing)
- **Engine:** Unity `6000.6.5f1`, URP. Both partners use exactly this version.
- **Input:** Input System package only (`activeInputHandler: 1`). No legacy `Input.*`.
- **AR (planned):** AR Foundation + ARCore (Android) and ARKit (iOS).
- **Putt physics:** our own roll model in `RollSimulator`. **No Rigidbody for the putt.** (Rigidbody is fine for other things, e.g. the BallDrop prototype or future obstacles.)
- **Launch contract:** `LaunchResult` = speed (m/s), unit direction on the floor (x = world X, y = world Z), timestamp. Keep it stable.

## Roll model
2D on the floor plane, fixed time step (default 1/240 s), green shape from `IGreenSurface` (height + slope):
```
a  = -mu*g*v_hat - (5/7)*g*grad(h)   grad(h) = slope (dh/dx, dh/dz); 5/7 = rolling solid ball
mu = vs^2 / (2*g*ds)                 vs = 1.83 m/s (Stimpmeter release), ds = stimp in metres
flat stop distance = v^2 / (2*mu*g)
at rest: stays while (5/7)*|grad(h)| <= mu, else rolls downhill
```
- Default stimp 10 ft -> mu ≈ 0.056; a ball at rest holds on slopes up to ~7.8 %. Tunable in `RollSettings`.
- Small-slope approximation; fine for greens (< ~8 %). Slopes in UI and data: percent.
- Holed rule: ball centre enters the hole circle at ≤ 1.0 m/s (≈ "would roll 3 ft past" at stimp 10). Faster = lip-out, ball rolls on.
- Hole Ø 108 mm, ball Ø 42.7 mm.
- Sanity check (stimp 10, 2 % side slope): 3 m putt aims ~27 cm uphill, 6 m ~63 cm.

Reference values (flat, stimp 10), covered by `Assets/Tests/EditMode/RollSimulatorTests.cs` (slopes: `SlopeRollTests.cs`):

| Putt | Launch speed | Travel per frame @30 fps |
|---|---|---|
| 1 m | 1.05 m/s | 3.5 cm |
| 2 m | 1.48 m/s | 4.9 cm |
| 3 m | 1.82 m/s | 6.1 cm |
| 5 m | 2.34 m/s | 7.8 cm |

## Code layout
```
Assets/
  Scripts/
    Putting/        ARGolf.Putting asmdef: plain C#, no MonoBehaviours (LaunchResult, RollSettings, RollSimulator,
                    GreenSurface, HeightField, GreenDefinition, GreenModel, FamousGreens, PuttSolver)
    *.cs            MonoBehaviours in Assembly-CSharp (VirtualBall, GreenView, GreenPracticeController, launchers, cameras)
  Tests/EditMode/   ARGolf.Putting.Tests asmdef (Unity Test Framework, NUnit)
  Editor/           editor-only tools
```
- Physics and estimation logic go in `Scripts/Putting/` as plain C# so it can be unit-tested in EditMode.
- Planned scripts (add as needed): `SessionFlow` (find floor → place hole → ready → rolling → result), `HolePlacer`, `PuttLine` (aim line), `HudController`/`ResultPanel`, `StatsStore` (JSON on phone), plus one launcher per hit-detection method.

## Greens
- A green is a `GreenDefinition` (green-local metres, x = right, y = away from the player): outline (oval + wobbles),
  stimp, fringe, height features (tilt %, tiers, mounds/hollows, ridges/swales, crowns), pins, putts.
- `GreenModel.Build` bakes it into a `HeightField` (10 cm grid, lowest point on the green = 0); `PlacedGreen` puts it
  in the world (origin + yaw). Friction: green 1, fringe (default stimp 5) and rough (stimp 1.5) scaled by
  stimp ratio, blended over 10 cm at each edge. The ball sits 3 mm (fringe) / 12 mm (rough) into the grass.
- Built-in greens in `FamousGreens` ("inspired by", not surveys; no course logos). `GreenTests` checks every green:
  pins ≥ 1 m inside the edge on slopes < 75 % of the holding slope, balls on the green and at rest, putt names state
  the real distance (±10 %), every putt holeable by `PuttSolver`. Add a green there and run the tests.
- `PuttSolver.Solve` = ideal putt (drops at the speed that would roll ~40 cm past), used for the ideal line.
- Scene: Tools > AR Golf > Build Famous Greens Scene -> `Assets/Scenes/FamousGreens.unity` (owner: Person A).
  `GreenView` draws the green at runtime (`visualHeightScale` 0 = flat for AR, slope colours, contours).

## Phases (each has a check; don't start the next in a track before it passes)
| # | Phase | Done when |
|---|---|---|
| 0 | AR setup | Phone shows camera view with floor detected; both partners can build |
| 1 | Virtual putting | Full putt playable by swipe, holed/missed judged correctly (tests green) |
| 2 | Hit detection | Real hit launches the virtual ball (method from README open questions) |
| 3 | UI and stats | A new user finishes a session without help |
| 4 | Validation and demo | Error figures over 20 putts; demo video |

Validation targets (20 putts): speed ±10%, direction ±1° (1° ≈ 5.2 cm sideways over 3 m ≈ hole radius), stop distance ±5 cm.

## Repo rules
- **One owner per scene and prefab.** Scenes merge badly; don't edit a `.unity`/`.prefab` the current user doesn't own.
- Commit `.meta` files with their assets. Never commit `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`.
- Small commits, pull daily, push before stopping for the day.

## How Claude should work here
- Edit C# and text files only. Anything done in the Unity Editor (scene setup, components, Inspector values, packages, build) -> give short numbered Editor steps instead of hand-editing scene/prefab YAML.
- Units: metres, seconds, m/s; degrees in UI. Tunable numbers in settings classes, not magic numbers.
- Minimal edits. After each change, list what changed per file.
- Be concise; rules and code over prose. Ask before changing anything under "Fixed decisions".
