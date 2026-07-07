# LAST SON — PC prototype (Unity)

The desktop evolution of the Android experiment: a 3D Superman sandbox for
PC — flight, all four powers, ground locomotion, combo melee combat, and a
reactive test range full of NPCs and physics props.

Everything is generated from code at runtime — there are **no imported
assets, prefabs, or scene wiring**. Open the project, open any empty
scene, press **Play**. You spawn on foot at a street intersection with the
test range around you.

## Powers

- **Heat vision** (hold `Q`): twin eye-beams to the crosshair, up to 320 m.
  Damages robots, heats objects — cars and tankers ignite, burn, and then
  explode; ice melts faster under the beam.
- **Freeze breath** (hold `F`): a frost cone. Douses fires, and fully
  frozen targets get encased in an ice block — punch the ice to **shatter**
  it for 2.5× damage, or let it melt.
- **X-ray vision** (toggle `X`): the city fades to a translucent blue
  shell while robots (red), civilians (green), and props (gold) glow
  through the walls — custom shaders, distance fog drops away.
- **Super strength** (`E`): grab the nearest car / crate / tanker, carry it
  overhead (walking or flying), then `LMB` to hurl it — thrown props deal
  impact damage to whatever they land on. Punches also send physics props
  flying.

## Combat

`LMB` punch, `RMB` kick, chained into combos with input buffering:

| Chain | Attack |
|---|---|
| P | Jab |
| P·P | Cross |
| P·P·P | **Haymaker** (big knockback) |
| K | Front kick |
| K·K | **360° Spin kick** (hits everything around you) |
| P·P·K | **Rising kick** (launcher — sends targets skyward) |
| K·P | Backfist |

All attacks are procedural pose keyframes blended over the base animation,
with lunge steps, hit-sphere detection at the strike frame, physics
knockback, and camera impact kicks. Attacks aim where the camera aims, so
flying punches work too.

## Locomotion

On the ground Superman now **walks** (procedural leg/arm swing cycle) and
**super-sprints** with `Shift` (deep forward lean, pumping arms, speed
trails). `Space` hops into flight; hovering idle near the ground settles
back into a landing. Flight is unchanged — it's the part you liked.

## The test range

Spawned around the starting intersection, everything reactive to every power:

- **10 training robots** — wander, stagger and tumble from hits, cook and
  spark under heat vision, freeze solid, and collapse in smoke when destroyed
- **16 pedestrians** — stroll the sidewalks and scatter in panic from
  explosions and nearby violence (they're indestructible sandbox dummies —
  they tumble, get up, and run)
- **10 parked cars** — punt them, throw them, ignite them (they burn for a
  few seconds, then explode), or freeze them
- **6 crates** — light, satisfying to punt and hurl
- **2 fuel tankers** — the big fireworks; chain reactions welcome

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

## Controls

| Input | Action |
|---|---|
| Mouse | Look / steer (airborne: W follows your aim — dive and climb by looking) |
| W A S D | Walk / fly |
| Shift | Super-sprint (ground) / boost (air) |
| Space / Ctrl | Take off & ascend / descend |
| LMB / RMB | Punch / kick (combos — see above); LMB hurls a carried prop |
| Q (hold) | Heat vision |
| F (hold) | Freeze breath |
| X | X-ray vision |
| E | Grab / set down the nearest prop |
| H · Esc | Toggle help · release the mouse |

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
├── Bootstrap.cs           # builds the whole scene on Play + spawns the test range
├── MeshBuilder.cs         # loft/sphere/box procedural mesh toolkit
├── ProceduralTextures.cs  # suit weave, emblem rasterizer, cape, buildings, ground, palette
├── SupermanModel.cs       # the New 52 model + articulated rig (incl. eye-beam anchors)
├── SupermanAnimator.cs    # poses + walk/sprint cycles + combat pose blending
├── CapeSimulation.cs      # verlet cloth cape
├── FlightController.cs    # ground walk/sprint + flight physics, impulses, trails
├── CameraRig.cs           # orbit camera, speed FOV, occlusion pull-in
├── CityGenerator.cs       # procedural Metropolis + landmark tower
├── CombatSystem.cs        # punch/kick combos, procedural attack keyframes, hit detection
├── PowersController.cs    # heat vision, freeze breath, x-ray, grab & throw
├── Damageable.cs          # hp / heat->ignite / freeze->ice-block core + IceBlock
├── RobotNPC.cs            # training robots (wander, stagger, burn, freeze, die)
├── CivilianNPC.cs         # pedestrians (stroll, panic, tumble, recover)
├── ThrowableProp.cs       # cars/crates/tankers + PropFactory builders
├── FxLibrary.cs           # bursts, explosions (damage+physics), fire, ice shards
├── Registry.cs            # x-ray occluder/highlight lists, panic broadcast
└── FlightHUD.cs           # speed/state/powers/combo overlay
Assets/Shaders/
├── XRayGlow.shader        # through-wall highlight (ZTest Always, rim + pulse)
└── XRayBuilding.shader    # translucent building shell during x-ray
```

*Personal, non-commercial fan project. Superman is a trademark of DC Comics.*
