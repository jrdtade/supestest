package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint

abstract class Mission(protected val g: Game) {
    abstract val id: Int
    abstract val title: String
    abstract val briefing: List<String>
    var complete = false
    var failed = false
    var failReason = ""

    abstract fun start()
    abstract fun update(dt: Float)
    abstract fun objectiveText(): String
    open fun drawExtras(c: Canvas, paint: Paint) {}

    protected fun fail(reason: String) {
        if (complete || failed) return
        failed = true
        failReason = reason
    }

    protected fun succeed() {
        if (complete || failed) return
        complete = true
    }
}

// ---------------------------------------------------------------------------
// MISSION 1 - Tutorial: flight, freeze breath, strength, rescues.
// ---------------------------------------------------------------------------
class Mission1(g: Game) : Mission(g) {
    override val id = 1
    override val title = "BAPTISM BY FIRE"
    override val briefing = listOf(
        "A gas main explosion has set Hobbs Bay burning.",
        "- Drag the LEFT stick to FLY",
        "- Hold FREEZE near flames to put them out",
        "- PUNCH through rubble to free the trapped",
        "- Fly into civilians to carry them; drop them at the SAFE ZONE"
    )
    private var fallerSpawned = false
    private var goonsSpawned = false
    private var timer = 0f

    override fun start() {
        val w = g.world
        w.generateCity(11L)
        w.skyTop = Color.rgb(35, 45, 90); w.skyBottom = Color.rgb(230, 130, 80)
        w.safeZones.add(SafeZone(600f, w.groundY - 10f))
        w.fires.add(FireZone(2100f, w.groundY - 60f, 120f))
        w.fires.add(FireZone(3300f, w.groundY - 700f, 110f))
        w.fires.add(FireZone(4400f, w.groundY - 60f, 130f))

        val r = Rubble(2800f, w.groundY)
        w.rubblePiles.add(r)
        g.civilians.add(Civilian(2800f, w.groundY - 26f, CivState.TRAPPED).also { it.rubble = r })
        g.civilians.add(Civilian(2050f, w.groundY - 26f, CivState.PANIC))
        g.civilians.add(Civilian(4460f, w.groundY - 26f, CivState.PANIC))
        g.superman.reset(600f, w.groundY - 400f)
        g.hint("Fires are spreading - freeze them before civilians burn!")
    }

    override fun update(dt: Float) {
        timer += dt
        if (!fallerSpawned && (timer > 25f || g.rescues >= 1)) {
            fallerSpawned = true
            g.civilians.add(Civilian(3350f, g.world.groundY - 900f, CivState.FALLING))
            g.hint("Someone's falling from the tower - CATCH THEM!")
        }
        val firesLeft = g.world.fires.count { !it.out }
        if (!goonsSpawned && firesLeft == 0 && g.rescues >= 4) {
            goonsSpawned = true
            for (i in 0 until 3) g.enemies.add(Goon(g.superman.x + 700f + i * 220f, g.world.groundY - 500f))
            g.hint("Intergang looters! PUNCH or hold HEAT vision to take them down")
        }
        if (g.civiliansLost > 0) fail("A civilian was lost.")
        if (goonsSpawned && g.enemies.none { !it.dead }) succeed()
    }

    override fun objectiveText(): String {
        val firesLeft = g.world.fires.count { !it.out }
        if (firesLeft > 0) return "Extinguish fires: ${g.world.fires.size - firesLeft}/${g.world.fires.size}   Rescued: ${g.rescues}/4"
        if (g.rescues < 4) return "Rescue civilians: ${g.rescues}/4"
        if (!goonsSpawned) return "Rescue civilians: ${g.rescues}/4"
        return "Defeat the looters: ${g.enemies.count { !it.dead }} left"
    }
}

// ---------------------------------------------------------------------------
// MISSION 2 - Intergang assault waves + kryptonite weapons + falling civilians.
// ---------------------------------------------------------------------------
class Mission2(g: Game) : Mission(g) {
    override val id = 2
    override val title = "INTERGANG RISING"
    override val briefing = listOf(
        "Intergang hits downtown with alien tech - and kryptonite rounds.",
        "- GREEN auras and bolts are KRYPTONITE: they drain your health",
        "- Your powers fail inside kryptonite fields - strike from outside",
        "- Catch falling civilians during the fight"
    )
    private var wave = 0
    private var fallTimer = 14f
    private var fallers = 0

    override fun start() {
        val w = g.world
        w.generateCity(22L)
        w.skyTop = Color.rgb(25, 30, 70); w.skyBottom = Color.rgb(140, 90, 120)
        w.safeZones.add(SafeZone(500f, w.groundY - 10f))
        g.superman.reset(700f, w.groundY - 500f)
        spawnWave()
    }

    private fun spawnWave() {
        wave++
        val cx = g.superman.x
        val gy = g.world.groundY
        when (wave) {
            1 -> {
                for (i in 0 until 4) g.enemies.add(Goon(cx + 800f + i * 260f, gy - rndf(350f, 750f)))
            }
            2 -> {
                for (i in 0 until 3) g.enemies.add(Goon(cx + 700f + i * 300f, gy - rndf(350f, 750f)))
                g.enemies.add(KryptoGunner(cx + 1300f, gy - 500f))
                g.enemies.add(KryptoGunner(cx - 900f, gy - 600f))
                g.hint("Kryptonite gatlings inbound - keep your distance!")
            }
            3 -> {
                for (i in 0 until 5) g.enemies.add(Goon(cx + rndf(-1400f, 1400f), gy - rndf(300f, 800f)))
                for (i in 0 until 3) g.enemies.add(KryptoGunner(cx + rndf(-1600f, 1600f), gy - rndf(400f, 700f)))
            }
        }
    }

    override fun update(dt: Float) {
        if (wave >= 2 && fallers < 3) {
            fallTimer -= dt
            if (fallTimer <= 0f) {
                fallTimer = rndf(16f, 24f)
                fallers++
                g.civilians.add(Civilian(g.superman.x + rndf(-700f, 700f), g.world.groundY - 950f, CivState.FALLING))
                g.hint("Civilian falling!")
            }
        }
        if (g.civiliansLost >= 2) fail("Too many civilians were lost.")
        if (g.enemies.none { !it.dead }) {
            if (wave < 3) spawnWave() else succeed()
        }
    }

    override fun objectiveText(): String =
        "Wave $wave/3 - Hostiles left: ${g.enemies.count { !it.dead }}" +
            if (g.civiliansLost > 0) "   Lost: ${g.civiliansLost}/2" else ""
}

// ---------------------------------------------------------------------------
// MISSION 3 - X-ray hunt: cloaked saboteurs and a hidden kryptonite bomb.
// ---------------------------------------------------------------------------
class Mission3(g: Game) : Mission(g) {
    override val id = 3
    override val title = "GHOSTS IN THE MACHINE"
    override val briefing = listOf(
        "LexCorp drones swarm the docks while cloaked aliens plant a",
        "kryptonite bomb hidden somewhere in the cargo yard.",
        "- Toggle X-RAY to reveal cloaked enemies (they can't be hit unseen)",
        "- X-RAY the crates to find the bomb, cut it open with HEAT vision,",
        "  then FREEZE the core to disarm it - before the timer runs out"
    )
    var bombTimer = 170f
    private var brutesSpawned = false

    override fun start() {
        val w = g.world
        w.generateCity(33L, density = 0.7f)
        w.skyTop = Color.rgb(8, 12, 30); w.skyBottom = Color.rgb(50, 45, 90)
        g.superman.reset(600f, w.groundY - 400f)
        w.bombCrate = BombCrate(5200f, w.groundY)
        for (i in 0 until 6) g.enemies.add(Drone(1400f + i * 350f, w.groundY - rndf(300f, 800f)))
        g.enemies.add(Cloaker(2600f, w.groundY - 400f))
        g.enemies.add(Cloaker(3900f, w.groundY - 300f))
        g.enemies.add(Cloaker(5000f, w.groundY - 500f))
        g.hint("Something's out there... use X-RAY vision")
    }

    override fun update(dt: Float) {
        val crate = g.world.bombCrate ?: return
        if (!crate.disarmed) {
            bombTimer -= dt
            if (bombTimer <= 0f) {
                g.explosionAt(crate.x, crate.y - 50f, 900f, 120f, kryptonite = true)
                fail("The kryptonite bomb detonated.")
                return
            }
        }
        // Reveal the crate when X-rayed from nearby.
        g.world.distToCrateVisible =
            g.superman.xrayOn && distf(g.superman.x, g.superman.y, crate.x, crate.y) < 900f
        if (!crate.revealed && g.world.distToCrateVisible) {
            crate.revealed = true
            g.hint("That's it! Cut the casing open with HEAT VISION!")
        }
        if (!brutesSpawned && crate.revealed) {
            brutesSpawned = true
            g.enemies.add(CyborgBrute(crate.x - 700f, g.world.groundY - 200f))
            g.enemies.add(CyborgBrute(crate.x + 700f, g.world.groundY - 200f))
            g.hint("Shielded cyborgs guarding the bomb - FREEZE their shields!")
        }
        if (crate.disarmed && g.enemies.none { !it.dead }) succeed()
    }

    override fun objectiveText(): String {
        val crate = g.world.bombCrate ?: return ""
        val timeStr = "BOMB: ${bombTimer.toInt()}s"
        return when {
            !crate.revealed -> "$timeStr - Find the bomb (X-RAY the cargo yard, head EAST)"
            !crate.casingOpen -> "$timeStr - Cut the casing open with HEAT vision"
            !crate.disarmed -> "$timeStr - FREEZE the exposed core!"
            else -> "Bomb disarmed - clear remaining hostiles: ${g.enemies.count { !it.dead }}"
        }
    }
}

// ---------------------------------------------------------------------------
// MISSION 4 - The plane save (Superman Returns homage).
// ---------------------------------------------------------------------------
class Mission4(g: Game) : Mission(g) {
    override val id = 4
    override val title = "FLIGHT 236"
    override val briefing = listOf(
        "Flight 236 out of Metropolis International is going down -",
        "starboard engine on fire, 240 souls aboard.",
        "- CHASE the plane down (full speed!)",
        "- FREEZE the burning engine before it tears the wing off",
        "- Then brace UNDER THE NOSE: your strength will bleed off its",
        "  speed. Slow it before the ground gets there."
    )
    private var graceT = 0f

    override fun start() {
        val w = g.world
        w.generateCity(44L, density = 0.5f)
        w.skyTop = Color.rgb(90, 140, 210); w.skyBottom = Color.rgb(210, 220, 235)
        g.superman.reset(400f, 800f)
        g.plane = Plane(1000f, 500f)
        g.camera.zoom = 0.8f
        g.hint("There! Flight 236 - go, go, GO!")
    }

    override fun update(dt: Float) {
        val p = g.plane ?: return
        graceT += dt
        if (p.crashed) { fail("Flight 236 was lost.") ; return }
        if (p.landed) {
            succeed()
            return
        }
        if (p.x > g.world.width - 400f) {
            fail("The plane went down beyond the city.")
        }
    }

    override fun objectiveText(): String {
        val p = g.plane ?: return ""
        val alt = ((g.world.groundY - 90f - p.y).coerceAtLeast(0f) / 10f).toInt()
        return when {
            p.landed -> "Flight 236 is down safe."
            p.engineOnFire -> "FREEZE the engine fire!   ALT ${alt}m  SPD ${p.vx.toInt()}  SINK ${p.vy.toInt()}"
            !p.braced -> "Brace under the NOSE!   ALT ${alt}m  SPD ${p.vx.toInt()}  SINK ${p.vy.toInt()}"
            else -> "HOLD ON! Slowing...   ALT ${alt}m  SPD ${p.vx.toInt()}  SINK ${p.vy.toInt()}"
        }
    }

    override fun drawExtras(c: Canvas, paint: Paint) {
        val p = g.plane ?: return
        if (!p.landed && !p.crashed) {
            // Marker arrow toward the plane's catch point when the engine is out.
            if (!p.engineOnFire && !p.braced) {
                paint.color = Pal.HUD_YELLOW
                c.drawCircle(p.noseX, p.noseY, 18f, paint)
                paint.style = Paint.Style.STROKE
                paint.strokeWidth = 4f
                c.drawCircle(p.noseX, p.noseY, 130f, paint)
                paint.style = Paint.Style.FILL
            }
        }
    }
}

// ---------------------------------------------------------------------------
// MISSION 5 - Boss: the K-13 Warmech.
// ---------------------------------------------------------------------------
class Mission5(g: Game) : Mission(g) {
    override val id = 5
    override val title = "HEART OF KRYPTONITE"
    override val briefing = listOf(
        "Intergang's masterstroke: the K-13 Warmech, an alien chassis",
        "built around a kryptonite reactor.",
        "- Its armor is nearly impervious. X-RAY it to see the reactor cycle",
        "- When the chest opens it floods the area with kryptonite -",
        "  hit the core with HEAT VISION from outside the field",
        "- Watch for missiles, beam sweeps, and kryptonite pulses"
    )
    private var addTimer = 18f
    private var boss: BossWarmech? = null

    override fun start() {
        val w = g.world
        w.generateCity(55L, density = 0.8f)
        w.skyTop = Color.rgb(30, 15, 45); w.skyBottom = Color.rgb(120, 60, 60)
        g.superman.reset(700f, w.groundY - 500f)
        boss = BossWarmech(2400f, w.groundY - 800f)
        g.enemies.add(boss!!)
        g.hint("That thing is powered by a kryptonite heart. End this.")
    }

    override fun update(dt: Float) {
        val b = boss ?: return
        addTimer -= dt
        if (addTimer <= 0f && !b.dead) {
            addTimer = rndf(16f, 24f)
            g.enemies.add(Drone(b.x - 400f, b.y))
            g.enemies.add(Drone(b.x + 400f, b.y))
        }
        if (b.dead) succeed()
    }

    override fun objectiveText(): String {
        val b = boss ?: return ""
        if (b.dead) return "K-13 destroyed."
        val pct = (b.hp / b.maxHp * 100f).toInt()
        return if (b.chestOpen) "REACTOR EXPOSED - HIT IT!   K-13: $pct%"
        else "K-13 armor: $pct% - X-RAY shows the reactor cycle"
    }
}
