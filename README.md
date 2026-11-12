# AR Golf

A project for the course **Virtual & Augmented Reality**.

> **Status: early stage.** The concept below is still being worked out and may change.

## Concept

An augmented reality phone app for mini golf and putting practice. The player swings a **real golf club** to hit a **virtual ball** that is placed in the real world through the phone's camera, on an AR golf course.

Rough idea of how a round would work:

1. The phone scans the floor and detects a flat surface.
2. A virtual hole, or a short mini golf course, is placed on that surface.
3. A virtual ball appears at the tee position.
4. The player swings a real putter at the virtual ball.
5. The app detects the club's hit, works out the direction and speed, and the ball rolls with realistic physics.

### Open questions

- **Club tracking:** how do we detect the real club and measure the hit? Options to explore:
  - an image marker or a colored tag on the club head, tracked by the camera
  - a second phone or motion sensor attached to the club
  - the phone itself mounted on the club, using its accelerometer and gyroscope
  - a **real ball** instead: the phone on a stand tracks the real ball over its first 60–80 cm (white ball on a dark mat, ~30 fps), measures launch speed and direction, and a virtual ball continues to the virtual hole while a cushion stops the real one
- **Camera position:** where is the phone while the player putts? On a tripod, held by a second person, or on the club?
- **Game mode:** a single putting practice hole, a full mini golf course, or both?
- **Feedback:** what to show after each shot (ball path, speed, score, shot history).

## Current state

The project currently holds a physics prototype built while learning Unity:

- **`Assets/Scenes/BallDropScene.unity`:** a structure built from primitives (stepped platform, domed building, slanted roof on pillars). Press **Play** to drop a bouncy ball onto the slanted roof. **Reset** hides the ball again. The camera follows the ball.

This scene tests the basics the golf game will need: rigidbody physics, bounce materials, colliders, a camera that follows the ball, and UI buttons.

### Putting model (no AR yet)

- **Roll model** (`Assets/Scripts/Putting/`): our own putting physics, not Unity's Rigidbody. The ball slows down by `a = -mu*g - g*sin(slope)`, where `mu` comes from the green speed (Stimpmeter, default 10 ft). It is holed if it rolls over the hole at 1.0 m/s or slower.
- **Launch contract:** every way of hitting the ball (swipe now, club or ball tracking later) produces a `LaunchResult`: speed (m/s), direction on the floor, timestamp. So the game can be built and tested before hit detection works.
- **`SwipeLauncher`** + **`VirtualBall`**: swipe across the floor with a finger or the mouse to putt; the ball rolls along the simulated path.
- **Unit tests** (`Assets/Tests/EditMode/`): check the model against reference values, e.g. a 2 m putt needs 1.48 m/s on a flat stimp-10 green. Run them in **Window → General → Test Runner → EditMode**.

## Tech stack

| | |
|---|---|
| Engine | Unity **6000.6.5f1** (Unity 6) |
| Render pipeline | Universal Render Pipeline (URP) |
| Input | Input System package |
| UI | uGUI |
| AR (planned) | AR Foundation with ARCore (Android) and ARKit (iOS) |

## Getting started

1. Install Unity **6000.6.5f1** through Unity Hub. Using exactly this version avoids project upgrades.
2. Clone the repository and open the project folder in Unity Hub (**Add → Add project from disk**).
3. Open `Assets/Scenes/BallDropScene.unity` and press Play.

The first time you open the project, Unity rebuilds the `Library/` folder, which can take a few minutes.

## Project structure

```
Assets/
  Editor/       Editor-only scripts (BallSceneBuilder: rebuilds the demo scene via Tools → Build Ball Drop Scene)
  Materials/    Materials for the scene objects
  Physics/      Physics materials (bouncy ball)
  Prefabs/      Reusable objects (Pillar)
  Scenes/       Unity scenes
  Scripts/      Runtime scripts (BallDropController, CameraFollow, SwipeLauncher, VirtualBall)
    Putting/    Plain C# putting model: LaunchResult, RollSettings, RollSimulator
  Settings/     URP render pipeline settings
  Tests/        EditMode unit tests for the putting model
  Textures/     Textures (ground checkerboard)
  ThirdParty/   Kenney 3D Minigolf Pack and Kenney Nature Pack (CC0, www.kenney.nl), materials converted to URP
Packages/       Package manifest
ProjectSettings/
```

Generated folders such as `Library/`, `Temp/`, `Logs/` and `UserSettings/` are excluded by `.gitignore` and should never be committed.

## Roadmap

- [x] Unity project set up with URP
- [x] Physics prototype: ball, colliders, bounce, follow camera, UI
- [x] Putting physics: own roll model, green speed, slope, holed/missed rule, unit tests
- [ ] Putt playable by swipe in a test scene (stand-in for hit detection)
- [ ] Decide how to measure the hit (club tracking or real-ball tracking)
- [ ] Add AR Foundation and detect the floor on a phone
- [ ] Place a virtual hole and ball on the detected floor
- [ ] Hit detection: real hit produces launch speed and direction
- [ ] Mini golf course with obstacles
- [ ] Score and shot feedback
- [ ] Validation: measure speed (±10%), direction (±1°) and stop distance (±5 cm) over 20 putts
