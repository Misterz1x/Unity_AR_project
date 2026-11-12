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
- **Camera position:** where is the phone while the player putts? On a tripod, held by a second person, or on the club?
- **Game mode:** a single putting practice hole, a full mini golf course, or both?
- **Feedback:** what to show after each shot (ball path, speed, score, shot history).

## Current state

The project currently holds a physics prototype built while learning Unity:

- **`Assets/Scenes/BallDropScene.unity`:** a structure built from primitives (stepped platform, domed building, slanted roof on pillars). Press **Play** to drop a bouncy ball onto the slanted roof. **Reset** hides the ball again. The camera follows the ball.

This scene tests the basics the golf game will need: rigidbody physics, bounce materials, colliders, a camera that follows the ball, and UI buttons.

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
  Scripts/      Runtime scripts (BallDropController, CameraFollow)
  Settings/     URP render pipeline settings
  Textures/     Textures (ground checkerboard)
Packages/       Package manifest
ProjectSettings/
```

Generated folders such as `Library/`, `Temp/`, `Logs/` and `UserSettings/` are excluded by `.gitignore` and should never be committed.

## Roadmap

- [x] Unity project set up with URP
- [x] Physics prototype: ball, colliders, bounce, follow camera, UI
- [ ] Decide how to track the club and measure the hit
- [ ] Add AR Foundation and detect the floor on a phone
- [ ] Place a virtual hole and ball on the detected floor
- [ ] Putting physics: ball rolling on the ground, hole detection
- [ ] Club hit detection and shot direction and speed
- [ ] Mini golf course with obstacles
- [ ] Score and shot feedback
