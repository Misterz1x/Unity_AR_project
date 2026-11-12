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
```
a  = -mu*g - g*sin(theta)          theta = slope along roll, + uphill
mu = vs^2 / (2*g*ds)               vs = 1.83 m/s (Stimpmeter release), ds = stimp in metres
flat stop distance = v^2 / (2*mu*g)
```
- Default stimp 10 ft -> mu ≈ 0.056. Tunable in `RollSettings`.
- Holed rule: ball centre passes over the hole at ≤ 1.0 m/s (≈ "would roll 3 ft past" at stimp 10).
- Hole Ø 108 mm, ball Ø 42.7 mm. Path is a straight line; side slope (break) is not modelled yet.

Reference values (flat, stimp 10), covered by `Assets/Tests/EditMode/RollSimulatorTests.cs`:

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
    Putting/        ARGolf.Putting asmdef: plain C#, no MonoBehaviours (LaunchResult, RollSettings, RollSimulator)
    *.cs            MonoBehaviours in Assembly-CSharp (SwipeLauncher, VirtualBall, BallDropController, CameraFollow)
  Tests/EditMode/   ARGolf.Putting.Tests asmdef (Unity Test Framework, NUnit)
  Editor/           editor-only tools
```
- Physics and estimation logic go in `Scripts/Putting/` as plain C# so it can be unit-tested in EditMode.
- Planned scripts (add as needed): `SessionFlow` (find floor → place hole → ready → rolling → result), `HolePlacer`, `PuttLine` (aim line), `HudController`/`ResultPanel`, `StatsStore` (JSON on phone), plus one launcher per hit-detection method.

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
