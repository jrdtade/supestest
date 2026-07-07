package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.sin

class Superman(private val g: Game) {
    var x = 400f
    var y = 400f
    var vx = 0f
    var vy = 0f
    val radius = 46f

    var maxHp = 200f
    var hp = maxHp
    var maxPower = 100f     // stored solar energy: fuels heat vision, freeze breath, X-ray
    var power = maxPower

    var facing = 1          // 1 right, -1 left
    var flying = false
    private var sinceHurt = 99f
    var dead = false

    // Kryptonite state
    var exposed = false          // currently inside a kryptonite field
    var weakness = 0f            // lingering weakness timer after leaving a field
    val weakened: Boolean get() = exposed || weakness > 0f

    // Ability state
    var heatOn = false           // held
    var freezeOn = false         // held
    var xrayOn = false           // toggled
    var punchTimer = 0f
    @Volatile var punchQueued = false   // set from the UI thread, consumed on the game thread
    private var punchCooldown = 0f
    var heatBeamEndX = 0f
    var heatBeamEndY = 0f
    var heatBeamActive = false

    // Rescue state
    var carrying: Civilian? = null

    // Plane mission state
    var bracingPlane = false

    private var animT = 0f

    val damageMultiplier: Float get() = if (weakened) 0.25f else 1f

    fun reset(px: Float, py: Float) {
        x = px; y = py; vx = 0f; vy = 0f
        hp = maxHp; power = maxPower
        dead = false; exposed = false; weakness = 0f
        heatOn = false; freezeOn = false; xrayOn = false
        carrying = null; bracingPlane = false
        punchTimer = 0f; punchCooldown = 0f; punchQueued = false
        sinceHurt = 99f
    }

    fun update(dt: Float) {
        if (dead) return
        animT += dt
        sinceHurt += dt
        if (punchTimer > 0f) punchTimer -= dt
        if (punchCooldown > 0f) punchCooldown -= dt

        // --- Kryptonite exposure ---
        val wasExposed = exposed
        exposed = g.world.inKryptonite(x, y)
        if (exposed) {
            weakness = 2.0f
            hp -= 8f * dt                       // health ebbs away near kryptonite
            if (RNG.nextFloat() < dt * 8f) {
                g.addEffect(Particle(x + rndf(-30f, 30f), y + rndf(-40f, 30f),
                    rndf(-20f, 20f), rndf(-60f, -20f), 0.7f, 6f, Pal.KRYPTO_GREEN))
            }
            // Kryptonite suppresses the solar-fueled powers outright.
            heatOn = false
            freezeOn = false
            if (xrayOn) xrayOn = false
            if (!wasExposed) g.hint("KRYPTONITE! Powers suppressed - get clear!")
            if (hp <= 0f) die()
        } else if (weakness > 0f) {
            weakness -= dt
        }

        // --- Movement (flight) ---
        val joy = g.hud.joystick
        val speedCap = if (weakened) 420f else 900f
        val accel = if (weakened) 1500f else 3200f
        if (joy.active && joy.magnitude > 0.12f) {
            flying = true
            vx += joy.dirX * accel * joy.magnitude * dt
            vy += joy.dirY * accel * joy.magnitude * dt
            if (abs(joy.dirX) > 0.25f) facing = if (joy.dirX > 0f) 1 else -1
        }
        // Drag
        vx -= vx * 3.2f * dt
        vy -= vy * 3.2f * dt
        val sp = distf(0f, 0f, vx, vy)
        if (sp > speedCap) {
            vx = vx / sp * speedCap
            vy = vy / sp * speedCap
        }
        if (!bracingPlane) {
            x += vx * dt
            y += vy * dt
        }
        x = clampf(x, radius, g.world.width - radius)
        y = clampf(y, radius, g.world.groundY - 20f)
        if (y >= g.world.groundY - 22f && abs(vy) < 30f) flying = false

        // Speed trail
        if (sp > 500f && RNG.nextFloat() < dt * 30f) {
            g.addEffect(Particle(x - facing * 40f, y + rndf(-20f, 20f),
                -vx * 0.2f, -vy * 0.2f, 0.35f, 8f, Color.argb(150, 200, 220, 255)))
        }

        // --- Power meter ---
        var draining = false
        if (heatOn && power > 0f) { power -= 20f * dt; draining = true }
        if (freezeOn && power > 0f) { power -= 14f * dt; draining = true }
        if (xrayOn) {
            power -= 7f * dt
            draining = true
            if (power <= 0f) { xrayOn = false; g.hint("Solar reserves too low for X-ray vision") }
        }
        if (power <= 0f) {
            power = 0f
            heatOn = false
            freezeOn = false
        }
        if (!draining && !exposed) power = clampf(power + 12f * dt, 0f, maxPower)

        // --- Health regen (Kryptonian cells recharge in sunlight, not near kryptonite) ---
        if (sinceHurt > 5f && !weakened && hp > 0f) hp = clampf(hp + 7f * dt, 0f, maxHp)

        // --- Active abilities ---
        if (punchQueued) {
            punchQueued = false
            punch()
        }
        heatBeamActive = false
        if (heatOn && power > 0f) updateHeatVision(dt)
        if (freezeOn && power > 0f) updateFreezeBreath(dt)

        // --- Carried civilian ---
        carrying?.let { civ ->
            civ.x = x
            civ.y = y + 52f
            civ.vx = vx; civ.vy = vy
            for (s in g.world.safeZones) {
                if (distf(x, y, s.x, s.y) < s.radius) {
                    dropCivilian(true)
                    return@let
                }
            }
        }
    }

    private fun updateHeatVision(dt: Float) {
        heatBeamActive = true
        // Auto-aim at the closest damageable enemy; otherwise fire straight ahead.
        var target: Enemy? = null
        var best = 1500f
        for (e in g.enemies) {
            if (e.dead || !e.targetable(g)) continue
            val d = distf(x, y, e.x, e.y)
            if (d < best) { best = d; target = e }
        }
        val eyeY = y - 26f
        var dirX = facing.toFloat()
        var dirY = 0f
        if (target != null) {
            val a = angleTo(x, eyeY, target.x, target.y)
            dirX = cos(a); dirY = sin(a)
            facing = if (dirX >= 0f) 1 else -1
        }
        var endX = x + dirX * 1500f
        var endY = eyeY + dirY * 1500f
        val dps = 55f * damageMultiplier

        if (target != null && pointSegDist(target.x, target.y, x, eyeY, endX, endY) < target.radius + 14f) {
            endX = target.x; endY = target.y
            target.onHit(g, dps * dt, DamageType.HEAT)
            if (RNG.nextFloat() < dt * 25f) {
                g.addEffect(Particle(endX + rndf(-12f, 12f), endY + rndf(-12f, 12f),
                    rndf(-120f, 120f), rndf(-160f, 20f), 0.4f, 6f, Pal.HEAT_ORANGE))
            }
        }

        // Heat vision also cuts open the bomb crate casing in Mission 3.
        g.world.bombCrate?.let { cr ->
            if (!cr.casingOpen && cr.revealed &&
                pointSegDist(cr.x, cr.y - 55f, x, eyeY, endX, endY) < 80f) {
                cr.casingHp -= 60f * dt
                endX = cr.x; endY = cr.y - 55f
                if (RNG.nextFloat() < dt * 25f) {
                    g.addEffect(Particle(endX + rndf(-15f, 15f), endY,
                        rndf(-150f, 150f), rndf(-200f, 0f), 0.5f, 7f, Pal.HEAT_CORE))
                }
                if (cr.casingOpen) g.hint("Casing breached! FREEZE the kryptonite core!")
            }
        }

        heatBeamEndX = endX
        heatBeamEndY = endY
    }

    private fun updateFreezeBreath(dt: Float) {
        val coneLen = 460f
        val mouthX = x + facing * 24f
        val mouthY = y - 18f

        fun inCone(px: Float, py: Float, pr: Float): Boolean {
            val dx = px - mouthX
            if (facing > 0 && dx < -pr) return false
            if (facing < 0 && dx > pr) return false
            val d = distf(mouthX, mouthY, px, py)
            if (d > coneLen + pr) return false
            val spread = 40f + d * 0.45f
            return abs(py - mouthY) < spread + pr
        }

        // Frost particles
        if (RNG.nextFloat() < dt * 60f) {
            g.addEffect(Particle(mouthX, mouthY,
                facing * rndf(300f, 620f), rndf(-90f, 90f), 0.5f, rndf(5f, 12f), Pal.ICE_BLUE, drag = 1.5f))
        }

        for (e in g.enemies) {
            if (e.dead || !e.targetable(g)) continue
            if (inCone(e.x, e.y, e.radius)) e.applyFreeze(g, 90f * dt)
        }
        for (f in g.world.fires) {
            if (!f.out && inCone(f.x, f.y, f.radius)) {
                f.intensity -= 0.55f * dt
                if (f.out) {
                    g.addEffect(Ring(f.x, f.y, 20f, f.radius * 1.4f, 0.5f, Pal.ICE_BLUE))
                    g.onFireExtinguished()
                }
            }
        }
        g.world.bombCrate?.let { cr ->
            if (cr.casingOpen && !cr.disarmed && inCone(cr.x, cr.y - 55f, 60f)) {
                cr.coreFreeze += 45f * dt
                if (cr.coreFreeze >= 100f) {
                    cr.disarmed = true
                    g.addEffect(Ring(cr.x, cr.y - 55f, 20f, 300f, 0.8f, Pal.ICE_BLUE))
                    g.hint("Kryptonite core stabilized. Bomb disarmed!")
                }
            }
        }
        g.plane?.let { p ->
            if (p.engineOnFire && inCone(p.engineX, p.engineY, 90f)) {
                p.fireHp -= 55f * dt
                if (!p.engineOnFire) {
                    g.addEffect(Ring(p.engineX, p.engineY, 30f, 260f, 0.7f, Pal.ICE_BLUE))
                    g.hint("Engine fire out! Get under the nose and slow the plane!")
                }
            }
        }
    }

    /** Super-strength melee strike. Runs on the game thread via punchQueued. */
    private fun punch() {
        if (dead || punchCooldown > 0f || carrying != null) return
        punchCooldown = 0.30f
        punchTimer = 0.18f
        val reach = 160f
        val px = x + facing * 80f
        var hitSomething = false
        val dmg = 42f * damageMultiplier

        for (e in g.enemies) {
            if (e.dead || !e.targetable(g)) continue
            if (distf(px, y, e.x, e.y) < reach + e.radius) {
                val actual = if (e.frozen > 0f) dmg * 3f else dmg
                e.onHit(g, actual, DamageType.PUNCH)
                e.vx += facing * 500f
                e.vy -= 150f
                hitSomething = true
                g.camera.addShake(6f)
                g.addEffect(Ring(e.x, e.y, 10f, 90f, 0.25f, Color.WHITE))
                if (e.frozen > 0f) g.addEffect(FloatText(e.x, e.y - 60f, "SHATTER!", Pal.ICE_BLUE))
            }
        }
        for (r in g.world.rubblePiles) {
            if (r.destroyed) continue
            if (distf(px, y, r.x, r.y) < reach + 60f) {
                r.hp -= 60f * damageMultiplier
                hitSomething = true
                g.camera.addShake(8f)
                for (i in 0 until 6) {
                    g.addEffect(Particle(r.x + rndf(-40f, 40f), r.y - 30f,
                        rndf(-220f, 220f), rndf(-340f, -60f), 0.7f, rndf(5f, 11f),
                        Color.rgb(120, 112, 100), gravity = 700f))
                }
                if (r.destroyed) {
                    g.addEffect(FloatText(r.x, r.y - 80f, "CLEARED!", Pal.HUD_YELLOW))
                    g.onRubbleCleared(r)
                }
            }
        }
        if (!hitSomething) {
            // Whiff feedback
            g.addEffect(Ring(px, y, 6f, 50f, 0.18f, Color.argb(140, 255, 255, 255)))
        }
    }

    fun tryPickupCivilian(civ: Civilian): Boolean {
        if (carrying != null || dead) return false
        carrying = civ
        civ.state = CivState.CARRIED
        g.hint("Carry them to the SAFE ZONE")
        return true
    }

    fun dropCivilian(safe: Boolean) {
        val civ = carrying ?: return
        carrying = null
        if (safe) {
            civ.state = CivState.SAFE
            civ.y = g.world.groundY - 26f
            civ.vx = 0f; civ.vy = 0f
            g.onCivilianRescued(civ)
        } else {
            civ.state = CivState.FALLING
        }
    }

    fun hurt(dmg: Float, kryptonite: Boolean = false) {
        if (dead) return
        var d = dmg
        if (kryptonite) {
            d *= 1.6f
            weakness = 2.5f
        }
        // High-tech weapons hurt; conventional scratches barely register (handled by callers).
        hp -= d
        sinceHurt = 0f
        g.camera.addShake(clampf(d * 0.4f, 2f, 14f))
        if (hp <= 0f) die()
    }

    private fun die() {
        if (dead) return
        hp = 0f
        dead = true
        heatOn = false; freezeOn = false; xrayOn = false
        dropCivilian(false)
        g.onSupermanDown()
    }

    fun draw(c: Canvas, paint: Paint) {
        if (dead) return
        val f = facing.toFloat()
        val bob = if (flying) sin(animT * 6f) * 3f else 0f
        val by = y + bob

        // Cape
        paint.style = Paint.Style.FILL
        paint.color = Pal.CAPE_RED
        val cape = Path()
        val flow = sin(animT * 8f) * 10f + clampf(-vx * f * 0.04f, -14f, 26f)
        cape.moveTo(x - f * 6f, by - 34f)
        cape.quadTo(x - f * (60f + flow), by - 10f, x - f * (74f + flow * 1.4f), by + 34f)
        cape.quadTo(x - f * 40f, by + 26f, x - f * 12f, by + 24f)
        cape.close()
        c.drawPath(cape, paint)

        // Legs (red boots)
        paint.color = Pal.SUPER_RED
        if (flying) {
            c.drawRect(x - f * 30f, by + 14f, x + f * 2f, by + 26f, paint)
        } else {
            c.drawRect(x - 12f, by + 12f, x - 2f, by + 44f, paint)
            c.drawRect(x + 2f, by + 12f, x + 12f, by + 44f, paint)
        }

        // Torso
        paint.color = Pal.SUPER_BLUE
        c.drawRoundRect(x - 20f, by - 36f, x + 20f, by + 16f, 12f, 12f, paint)
        // Arm reaching forward when punching / flying
        val armLen = if (punchTimer > 0f) 46f else if (flying) 40f else 20f
        c.drawRoundRect(
            if (f > 0) x + 8f else x - 8f - armLen,
            by - 30f,
            if (f > 0) x + 8f + armLen else x - 8f,
            by - 16f, 8f, 8f, paint)

        // Chest crest (generic diamond, gold)
        paint.color = Pal.HUD_YELLOW
        val crest = Path()
        crest.moveTo(x, by - 32f)
        crest.lineTo(x + 10f, by - 24f)
        crest.lineTo(x, by - 12f)
        crest.lineTo(x - 10f, by - 24f)
        crest.close()
        c.drawPath(crest, paint)

        // Head
        paint.color = Color.rgb(235, 190, 160)
        c.drawCircle(x + f * 4f, by - 48f, 13f, paint)
        paint.color = Color.rgb(25, 20, 30)
        c.drawArc(x + f * 4f - 13f, by - 62f, x + f * 4f + 13f, by - 42f, 180f, 180f, true, paint)

        // Eyes glow while heat vision charges
        if (heatOn && power > 0f) {
            paint.color = Pal.HEAT_ORANGE
            c.drawCircle(x + f * 9f, by - 48f, 5f, paint)
        }

        // Kryptonite sickness tint
        if (weakened) {
            paint.color = Color.argb(70, 80, 255, 110)
            c.drawCircle(x, by - 12f, 60f, paint)
        }

        // Heat vision beam
        if (heatBeamActive) {
            paint.strokeCap = Paint.Cap.ROUND
            paint.style = Paint.Style.STROKE
            paint.color = Color.argb(120, 255, 80, 20)
            paint.strokeWidth = 14f
            c.drawLine(x, by - 48f, heatBeamEndX, heatBeamEndY, paint)
            paint.color = Pal.HEAT_ORANGE
            paint.strokeWidth = 7f
            c.drawLine(x, by - 48f, heatBeamEndX, heatBeamEndY, paint)
            paint.color = Pal.HEAT_CORE
            paint.strokeWidth = 3f
            c.drawLine(x, by - 48f, heatBeamEndX, heatBeamEndY, paint)
            paint.style = Paint.Style.FILL
        }
    }
}
