# LAST SON — a fan-made Superman action game for Android

A fast-paced, fully interactive 2D Superman action game for Android phones,
built as a **personal, non-commercial experiment** to answer one question:
*could a Superman game actually work?* (Superman and related elements are
trademarks of DC Comics. This is a fan project — do not distribute.)

The whole game is a single self-contained Kotlin app: a custom engine on
`SurfaceView`/`Canvas`, zero external assets, zero third-party dependencies.
Everything on screen is drawn procedurally.

## The powers (all usable, all *required*)

| Power | Control | Used for |
|---|---|---|
| **Flight** | Left virtual stick | Everything — free 360° movement, catching fallers, chasing the plane |
| **Super strength** | PUNCH button | Melee combat, shattering frozen enemies, clearing rubble off trapped civilians |
| **Heat vision** | Hold HEAT | Auto-aiming ranged beam; the only way to cut open the bomb casing and to hurt the boss core from outside its kryptonite field |
| **Freeze breath** | Hold FREEZE | Freezes enemies solid (punch for 3× damage), cracks cyborg energy shields, extinguishes fires and the plane's engine fire, stabilizes the kryptonite bomb core |
| **X-ray vision** | Toggle X-RAY | Reveals cloaked aliens (untouchable while unseen), finds the hidden bomb, shows the boss reactor cycle through its armor |

Heat vision, freeze breath, and X-ray all draw from a shared **solar power
meter** that recharges when idle, so you can't just hold a beam forever.

## Kryptonite

Green fields, auras, and projectiles are kryptonite, and it behaves like it
should:

- **Health ebbs away** continuously inside any kryptonite field
- **Damage dealt drops to 25%**
- **Heat vision, freeze breath, and X-ray are unavailable** (buttons lock out)
- Flight speed is halved; solar recharge and health regen stop
- Kryptonite hits leave a lingering weakness for a few seconds after exposure

## Enemies

Nobody here gets one-tapped — they're built to survive contact with Superman:

- **Intergang goons** — jetpack troopers with high-tech blasters
- **Kryptonite gunners** — heavies with kryptonite gatlings *and a personal kryptonite aura*: punching them is a trap, hit them from range
- **Robot drones** — fast orbiting swarmers
- **Cyborg brutes** — charging tanks behind energy shields that only freeze breath can crack
- **Cloaked alien stalkers** — invisible and invulnerable until X-ray reveals them
- **K-13 Warmech (boss)** — an alien chassis around a kryptonite reactor: armor shrugs off ~everything, the chest opens on a cycle (X-ray shows the countdown), and an open chest floods the area with kryptonite — so you have to snipe the core with heat vision from outside the field while dodging homing missiles, beam sweeps, and kryptonite pulses

## Civilian rescues

- Catch civilians falling from towers before they hit the street
- Punch rubble apart to free the trapped
- Carry people out of burning blocks to marked safe zones (fly into them to pick up; they drop automatically in the zone)
- Missions can fail if too many civilians are lost

## Missions

1. **Baptism by Fire** — tutorial: flight, freeze breath, strength, rescues
2. **Intergang Rising** — three assault waves with kryptonite weapons while civilians fall around you
3. **Ghosts in the Machine** — X-ray hunt: cloaked saboteurs and a hidden, timered kryptonite bomb (find with X-ray → cut open with heat vision → freeze the core)
4. **Flight 236** — the *Superman Returns* plane save: chase the diving jet, freeze the burning engine, brace under the nose, and bleed off its speed before the ground arrives
5. **Heart of Kryptonite** — the K-13 Warmech boss fight

Progress (unlocked missions) persists via `SharedPreferences`.

## Building & running

Requirements: Android Studio (or the Android SDK + Gradle 8.7+), JDK 17+.
Min SDK 24 (Android 7.0), target SDK 35. Landscape only.

```bash
# From Android Studio: open the project folder and Run.
# Or from the command line with the Android SDK installed:
./gradlew assembleDebug        # (generate the wrapper first with `gradle wrapper` if needed)
adb install app/build/outputs/apk/debug/app-debug.apk
```

There are no dependencies beyond the Android framework and the Kotlin
stdlib, so the first build only needs to fetch the Android Gradle Plugin.

> Note: all Kotlin sources are compile-verified against the Android 15
> framework. The APK build itself needs a local Android SDK.

## Code map

```
app/src/main/java/com/fanproject/lastson/
├── MainActivity.kt   # fullscreen immersive activity
├── GameView.kt       # SurfaceView + fixed-cap game loop thread
├── Game.kt           # state machine, mission lifecycle, menus, scoring
├── Core.kt           # math helpers, camera (follow/shake), particle effects
├── World.kt          # procedural city, fires, rubble, kryptonite zones, bomb crate
├── Superman.kt       # flight physics, all five powers, kryptonite debuffs
├── Enemies.kt        # projectiles + goon/gunner/drone/brute/cloaker + K-13 boss
├── Civilians.kt      # panic/trapped/falling/carried rescue state machine
├── Plane.kt          # Flight 236: physics, engine fire, nose-brace landing
├── Hud.kt            # multi-touch joystick, ability buttons, bars, hints
└── Missions.kt       # the five scripted missions
```

## So… can a Superman game work?

The design bet this prototype makes: the problem was never invulnerability,
it's that most Superman games treat the powers as interchangeable damage
buttons. Here each power is a *verb with exclusive jobs* (freeze is the only
firefighting tool, X-ray the only counter to cloaking, strength the only
answer to rubble), kryptonite is a spatial puzzle rather than a flat debuff,
and half the challenge is protecting people instead of protecting yourself.
Play mission 4 and judge for yourself.
