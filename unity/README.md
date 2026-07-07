# LAST SON — PC flight prototype (Unity)

The desktop evolution of the Android experiment: a 3D Superman flight
prototype for PC. Goal of this first milestone: **make flying over
Metropolis feel great**, with a character model that holds up on screen.
Combat, rescues, and kryptonite port over from the 2D design once flight
feels right.

Everything is generated from code at runtime — there are **no imported
assets, prefabs, or scene wiring**. Open the project, open any empty
scene, press **Play**.

## The Superman model (procedural, New 52 styling)

Built entirely in `SupermanModel.cs` from lofted superellipse cross-sections
and sculpted spheres:

- Armored **blue suit** with a generated kryptonian micro-weave texture and
  darker chevron panel piping (no trunks)
- **Chest emblem**: diamond shield with an angular S glyph, rasterized as a
  curved decal conforming to the chest; the glyph repeats tone-on-tone
  across the **cape's** upper back
- **Cape**: real-time verlet cloth pinned at the shoulder blades — it
  streams and snaps with airspeed, flutters in hover, and collides with
  the torso
- **Red boots** with a lipped top, and a **red belt with gold buckle and
  studs**
- Head with a **squared jaw, strong chin, full head of dark hair**
  (widow's-peak hairline, sideburns), blue eyes, brows, nose
- Fully articulated joint hierarchy posed procedurally: heroic grounded
  stance, hover (knees soft, arms out), classic **one-fist-forward cruise**,
  and a two-fists **boost** pose, blended smoothly

Tweak the emblem glyph via the stroke list in `ProceduralTextures.SStrokes`.

## Flight

| Input | Action |
|---|---|
| Mouse | Look / steer (W follows your aim — dive and climb by looking) |
| W A S D | Fly |
| Space / Ctrl | Ascend / descend |
| **Hold Shift** | Super-speed boost — FOV stretch, camera pull-back, fist/boot speed trails |
| Space (near ground) | Take off / land |
| H | Toggle help · Esc releases the mouse |

The body pitches into the velocity vector and banks through turns; landing
happens automatically when you descend gently at low speed. Buildings and
ground are solid (CharacterController collision) — clipping a tower at
speed bleeds velocity and kicks the camera.

## The city

`CityGenerator.cs` lays out a ~2 km² street grid of textured tower blocks
(procedural window textures, lit at random), rooftop clutter and antennas,
downtown height falloff, and a gold-globed landmark tower at the center
for navigation. Late-afternoon sun, procedural skybox, distance fog.

## Opening the project

1. Unity **2021.3 LTS or newer** (2022.3 LTS recommended; the project has
   no package dependencies beyond built-in modules).
2. Open the `unity/` folder as a project. Let it import.
3. Open any scene (File → New Scene is fine) and press **Play** —
   `Bootstrap.cs` constructs the world at startup.
4. Uses the classic Input Manager (project default), so no Input System
   package is needed.

All C# sources are compile-verified against the UnityEngine 2021.3
assemblies.

## Code map

```
Assets/Scripts/
├── Bootstrap.cs           # builds the whole scene on Play (lighting, city, hero, camera, HUD)
├── MeshBuilder.cs         # loft/sphere/box procedural mesh toolkit
├── ProceduralTextures.cs  # suit weave, emblem rasterizer, cape, buildings, ground, palette
├── SupermanModel.cs       # the New 52 model + articulated rig
├── SupermanAnimator.cs    # procedural poses (ground/hover/cruise/boost)
├── CapeSimulation.cs      # verlet cloth cape
├── FlightController.cs    # flight physics, states, collisions, speed trails
├── CameraRig.cs           # orbit camera, speed FOV, occlusion pull-in
├── CityGenerator.cs       # procedural Metropolis + landmark tower
└── FlightHUD.cs           # speed/altitude/help overlay
```

*Personal, non-commercial fan project. Superman is a trademark of DC Comics.*
