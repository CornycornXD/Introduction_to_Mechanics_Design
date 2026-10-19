# Week 01 – Strict Baseline Movement Lab

GDEV50024 Introduction to Mechanics Design. A small 2D platformer lab where students play a
reliable controller with strict rules, before later weeks introduce timing and spatial forgiveness.

## Launch

1. Open the project in **Unity 6000.0.54f1**.
2. Open the scene **`Assets/Week01_StrictBaseline/Scenes/Week01_StrictBaseline.unity`**.
3. Press **Play**, then click inside the Game view so it receives the keyboard.

Nothing needs to be assembled by hand. The scene is not in Build Settings; add it there only if
you want a standalone build.

## Controls (shown in the scene)

| Action  | Keys                              |
|---------|-----------------------------------|
| Move    | A / D, or Left / Right arrows     |
| Jump    | Space, W, or Up arrow             |
| Restart | R (returns to the start)          |

Input uses the New Input System asset `Input/Week01Controls.inputactions` (action map `Lab`).

## The route

1. **Flat ground**: open floor for walking, stopping, turning and jumping.
2. **Platform edge**: a step, a raised ledge, a 5-unit gap, then a second ledge. A missed gap
   drops to the floor; walk back under the first ledge to the step to try again.
3. **Landing area**: three low posts. Clearing one post lands the character shortly before the next.
4. **Wall and low ceiling**: a passage 1.8 units high, then open space up to a tall wall.

## The rules (for the discussion after play)

All in `Scripts/StrictPlatformerController.cs`:

- Holding Left/Right sets horizontal speed to *Move Speed*; releasing stops at once.
- Gravity pulls down every physics step, up to *Max Fall Speed*.
- A Jump press is judged **once, on the frame it arrives**. It jumps only if the character is
  standing on the ground at that moment (solid surface under the feet, not rising or falling).
  Otherwise the press is discarded. Holding Jump never jumps again.
- The jump decision is made immediately and only its result (an upward speed) is handed to physics,
  so no press is ever stored for later.

Deliberately absent: coyote time, jump buffering, variable jump height, apex assistance, corner
correction, ledge protection, automatic step-up, acceleration and air-control tuning.

Tunable in the Inspector on **Player → Strict Platformer Controller**: Move Speed (7), Jump Height (2.5),
Gravity (30), Max Fall Speed (20), Ground Check Depth (0.04), and *Log Jump Presses*, which prints
every press and whether it was accepted to the Console. With the default 50 Hz physics step the
actual peak is about 2.39 units. The level was laid out for these defaults.

## Files

| Path | Purpose |
|------|---------|
| `Scenes/Week01_StrictBaseline.unity` | The lab scene |
| `Scripts/StrictPlatformerController.cs` | Movement, gravity, strict jump, ground check |
| `Scripts/LabRestart.cs` | Restart key (and a fall-out safety net) |
| `Scripts/LabCameraFollow.cs` | Horizontal follow, clamped to the lab, no smoothing |
| `Input/Week01Controls.inputactions` | Move / Jump / Restart keyboard bindings |
| `Physics/PlayerFrictionless.physicsMaterial2D` | Zero friction, so walls never slow a fall |
| `Editor/Week01LabBuilder.cs` | Regenerates the scene (menu below). Not needed by students |
| `Tests/PlayMode/StrictBaselineTests.cs` | Automated play tests of every rule above |

## Tests

**Window → General → Test Runner → PlayMode → Week01.StrictBaseline.Tests → Run All.**
The tests load the scene, drive a virtual keyboard frame by frame, and check grounded jumps, presses
in the air, holding through landing, presses just after an edge and just before landing, walls,
the low ceiling, and restart.

## Regenerating the scene

**Tools → GDEV50024 → Week 01 → Rebuild Movement Lab Scene** rebuilds the scene from
`Editor/Week01LabBuilder.cs` (the layout numbers live there). It asks for confirmation first and
**overwrites manual edits** to the scene.
