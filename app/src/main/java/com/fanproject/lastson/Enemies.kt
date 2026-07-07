package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import kotlin.math.cos
import kotlin.math.sin

enum class DamageType { PUNCH, HEAT, EXPLOSION }

/** Hostile projectile. All projectiles in the game target Superman. */
class Projectile(
    var x: Float, var y: Float,
    var vx: Float, var vy: Float,
    var dmg: Float,
    var radius: Float,
    var color: Int,
    var kryptonite: Boolean = false,
    var homing: Boolean = false,
    var explosionRadius: Float = 0f,
    var life: Float = 4f
) {
    var dead = false

    fun update(g: Game, dt: Float) {
        if (homing) {
            val s = g.superman
            val a = angleTo(x, y, s.x, s.y)
            val speed = distf(0f, 0f, vx, vy)
            val cur = kotlin.math.atan2(vy, vx)
            var diff = a - cur
            while (diff > Math.PI) diff -= (2 * Math.PI).toFloat()
            while (diff < -Math.PI) diff += (2 * Math.PI).toFloat()
            val turn = clampf(diff, -2.2f * dt, 2.2f * dt)
            vx = cos(cur + turn) * speed
            vy = sin(cur + turn) * speed
        }
        x += vx * dt
        y += vy * dt
        life -= dt
        if (life <= 0f) { detonate(g); return }
        if (y > g.world.groundY) { detonate(g); return }

        val s = g.superman
        if (!s.dead && distf(x, y, s.x, s.y) < radius + s.radius) {
            s.hurt(dmg, kryptonite)
            if (kryptonite) {
                g.addEffect(FloatText(s.x, s.y - 70f, "KRYPTONITE HIT", Pal.KRYPTO_GREEN))
            }
            detonate(g)
        }
    }

    private fun detonate(g: Game) {
        if (dead) return
        dead = true
        if (explosionRadius > 0f) {
            g.explosionAt(x, y, explosionRadius, dmg * 0.7f, kryptonite)
        } else {
            g.addEffect(Ring(x, y, 4f, radius * 3f, 0.2f, color))
        }
    }

    fun draw(c: Canvas, paint: Paint) {
        paint.style = Paint.Style.FILL
        paint.color = color
        c.drawCircle(x, y, radius, paint)
        paint.color = Color.argb(90, Color.red(color), Color.green(color), Color.blue(color))
        c.drawCircle(x - vx * 0.02f, y - vy * 0.02f, radius * 1.7f, paint)
    }
}

abstract class Enemy(var x: Float, var y: Float) {
    var vx = 0f
    var vy = 0f
    abstract val radius: Float
    abstract val scoreValue: Int
    var maxHp = 50f
    var hp = 50f
    var dead = false
    var frozen = 0f            // seconds remaining frozen solid
    var freezeMeter = 0f       // fills to 100 -> frozen
    var hitFlash = 0f
    protected var t = rndf(0f, 6f)
    protected var fireCd = rndf(0.5f, 2f)

    /** Whether Superman's attacks can currently affect this enemy. */
    open fun targetable(g: Game): Boolean = true

    open fun applyFreeze(g: Game, amount: Float) {
        if (frozen > 0f) return
        freezeMeter += amount
        if (freezeMeter >= 100f) {
            freezeMeter = 0f
            frozen = 3.5f
            vx = 0f; vy = 0f
            g.addEffect(Ring(x, y, radius, radius * 1.6f, 0.4f, Pal.ICE_BLUE))
            g.addEffect(FloatText(x, y - radius - 20f, "FROZEN", Pal.ICE_BLUE))
        }
    }

    open fun onHit(g: Game, dmg: Float, type: DamageType) {
        if (dead) return
        hp -= dmg
        hitFlash = 0.1f
        if (hp <= 0f) die(g)
    }

    open fun die(g: Game) {
        if (dead) return
        dead = true
        g.onEnemyKilled(this)
        for (i in 0 until 14) {
            g.addEffect(Particle(x, y, rndf(-320f, 320f), rndf(-360f, 120f),
                rndf(0.4f, 0.9f), rndf(4f, 10f), explosionColor(), gravity = 500f))
        }
        g.addEffect(Ring(x, y, 10f, radius * 2.6f, 0.35f, explosionColor()))
    }

    protected open fun explosionColor(): Int = Pal.HEAT_ORANGE

    fun update(g: Game, dt: Float) {
        if (dead) return
        t += dt
        if (hitFlash > 0f) hitFlash -= dt
        if (freezeMeter > 0f && frozen <= 0f) freezeMeter = clampf(freezeMeter - 12f * dt, 0f, 100f)
        if (frozen > 0f) {
            frozen -= dt
            // Frozen enemies drop out of the sky.
            vy += 600f * dt
            y += vy * dt
            if (y > g.world.groundY - radius) { y = g.world.groundY - radius; vy = 0f }
            return
        }
        ai(g, dt)
        x += vx * dt
        y += vy * dt
        x = clampf(x, radius, g.world.width - radius)
        y = clampf(y, radius, g.world.groundY - radius * 0.6f)
    }

    protected abstract fun ai(g: Game, dt: Float)

    fun drawBase(c: Canvas, paint: Paint, g: Game) {
        if (dead) return
        drawBody(c, paint, g)
        if (frozen > 0f) {
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(120, 160, 225, 255)
            c.drawCircle(x, y, radius * 1.15f, paint)
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 4f
            paint.color = Pal.ICE_BLUE
            c.drawCircle(x, y, radius * 1.15f, paint)
            paint.style = Paint.Style.FILL
        }
        if (hitFlash > 0f) {
            paint.color = Color.argb(130, 255, 255, 255)
            c.drawCircle(x, y, radius, paint)
        }
        // Health pip
        if (hp < maxHp) {
            paint.color = Color.argb(180, 0, 0, 0)
            c.drawRect(x - 34f, y - radius - 22f, x + 34f, y - radius - 14f, paint)
            paint.color = Pal.DANGER_RED
            c.drawRect(x - 34f, y - radius - 22f, x - 34f + 68f * (hp / maxHp), y - radius - 14f, paint)
        }
    }

    protected abstract fun drawBody(c: Canvas, paint: Paint, g: Game)

    /** Hover toward a preferred distance from Superman at a given speed. */
    protected fun hoverAt(g: Game, preferred: Float, speed: Float, dt: Float, heightBias: Float = -120f) {
        val s = g.superman
        val d = distf(x, y, s.x, s.y)
        val a = angleTo(x, y, s.x, s.y)
        val want = if (d > preferred + 60f) 1f else if (d < preferred - 60f) -1f else 0f
        vx += cos(a) * want * speed * 2f * dt
        vy += sin(a) * want * speed * 2f * dt
        vy += (s.y + heightBias + sin(t * 2f) * 40f - y) * 0.6f * dt
        vx -= vx * 1.6f * dt
        vy -= vy * 1.6f * dt
        val sp = distf(0f, 0f, vx, vy)
        if (sp > speed) { vx = vx / sp * speed; vy = vy / sp * speed }
    }

    protected fun shootAt(g: Game, speed: Float, dmg: Float, radius: Float, color: Int,
                          kryptonite: Boolean = false, spread: Float = 0.06f) {
        val s = g.superman
        val a = angleTo(x, y, s.x, s.y) + rndf(-spread, spread)
        g.projectiles.add(Projectile(x, y, cos(a) * speed, sin(a) * speed, dmg, radius, color, kryptonite))
    }
}

// ---------------------------------------------------------------------------

/** Intergang jetpack trooper with a high-tech blaster. Bread-and-butter enemy. */
class Goon(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 38f
    override val scoreValue = 100
    init { maxHp = 70f; hp = maxHp }

    override fun ai(g: Game, dt: Float) {
        hoverAt(g, 520f, 260f, dt)
        fireCd -= dt
        if (fireCd <= 0f && distf(x, y, g.superman.x, g.superman.y) < 950f) {
            fireCd = rndf(1.0f, 1.8f)
            shootAt(g, 700f, 7f, 9f, Pal.HUD_YELLOW)
        }
    }

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        paint.style = Paint.Style.FILL
        // Jetpack flame
        paint.color = Pal.HEAT_ORANGE
        c.drawCircle(x, y + 34f, 9f + sin(t * 20f) * 3f, paint)
        // Body
        paint.color = Color.rgb(90, 95, 105)
        c.drawRoundRect(x - 20f, y - 30f, x + 20f, y + 26f, 10f, 10f, paint)
        // Head w/ visor
        paint.color = Color.rgb(60, 62, 70)
        c.drawCircle(x, y - 40f, 13f, paint)
        paint.color = Pal.DANGER_RED
        c.drawRect(x - 10f, y - 44f, x + 10f, y - 38f, paint)
        // Gun
        paint.color = Color.rgb(40, 42, 48)
        val f = if (g.superman.x > x) 1f else -1f
        c.drawRect(x + f * 14f, y - 14f, x + f * 52f, y - 4f, paint)
    }
}

/** Intergang heavy with a kryptonite gatling. Emits a personal kryptonite aura. */
class KryptoGunner(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 44f
    override val scoreValue = 250
    val auraRadius = 230f
    private val aura = KryptoZone(x, y, auraRadius)
    private var auraRegistered = false
    init { maxHp = 110f; hp = maxHp }

    override fun ai(g: Game, dt: Float) {
        if (!auraRegistered) { g.world.kryptoZones.add(aura); auraRegistered = true }
        aura.x = x; aura.y = y
        hoverAt(g, 620f, 200f, dt)
        fireCd -= dt
        if (fireCd <= 0f && distf(x, y, g.superman.x, g.superman.y) < 1000f) {
            fireCd = rndf(1.6f, 2.4f)
            shootAt(g, 620f, 13f, 11f, Pal.KRYPTO_GREEN, kryptonite = true)
        }
    }

    override fun die(g: Game) {
        aura.life = 0.01f    // aura dissipates with its generator
        super.die(g)
    }

    override fun explosionColor(): Int = Pal.KRYPTO_GREEN

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        paint.style = Paint.Style.FILL
        paint.color = Pal.HEAT_ORANGE
        c.drawCircle(x, y + 40f, 10f + sin(t * 18f) * 3f, paint)
        paint.color = Color.rgb(55, 70, 58)
        c.drawRoundRect(x - 26f, y - 36f, x + 26f, y + 32f, 10f, 10f, paint)
        paint.color = Color.rgb(38, 48, 40)
        c.drawCircle(x, y - 46f, 15f, paint)
        paint.color = Pal.KRYPTO_GREEN
        c.drawRect(x - 11f, y - 50f, x + 11f, y - 43f, paint)
        // Glowing kryptonite gatling
        val f = if (g.superman.x > x) 1f else -1f
        paint.color = Color.rgb(35, 60, 40)
        c.drawRect(x + f * 16f, y - 18f, x + f * 66f, y + 2f, paint)
        paint.color = Pal.KRYPTO_GREEN
        c.drawCircle(x + f * 66f, y - 8f, 7f + sin(t * 9f) * 2f, paint)
    }
}

/** Fast robotic attack drone. Swarms in numbers. */
class Drone(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 26f
    override val scoreValue = 60
    private val orbitDir = if (RNG.nextBoolean()) 1f else -1f
    init { maxHp = 40f; hp = maxHp }

    override fun ai(g: Game, dt: Float) {
        val s = g.superman
        val a = angleTo(x, y, s.x, s.y)
        // Orbit the player while closing distance.
        val orbit = a + orbitDir * 1.2f
        vx += (cos(a) * 180f + cos(orbit) * 260f) * dt * 2f
        vy += (sin(a) * 180f + sin(orbit) * 260f) * dt * 2f
        vx -= vx * 1.2f * dt
        vy -= vy * 1.2f * dt
        val sp = distf(0f, 0f, vx, vy)
        val cap = 430f
        if (sp > cap) { vx = vx / sp * cap; vy = vy / sp * cap }
        fireCd -= dt
        if (fireCd <= 0f && distf(x, y, s.x, s.y) < 620f) {
            fireCd = rndf(0.9f, 1.5f)
            shootAt(g, 820f, 4f, 6f, Pal.DANGER_RED, spread = 0.12f)
        }
    }

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        paint.style = Paint.Style.FILL
        paint.color = Color.rgb(70, 78, 92)
        c.drawCircle(x, y, 22f, paint)
        paint.style = Paint.Style.STROKE
        paint.strokeWidth = 5f
        paint.color = Color.rgb(120, 130, 150)
        c.drawCircle(x, y, 30f, paint)
        paint.style = Paint.Style.FILL
        paint.color = Pal.DANGER_RED
        c.drawCircle(x, y, 8f + sin(t * 14f) * 2f, paint)
    }
}

/** Heavily armored cyborg. Its energy shield must be frozen before it can be damaged. */
class CyborgBrute(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 62f
    override val scoreValue = 400
    var shieldUp = true
    private var shieldDownTimer = 0f
    private var chargeCd = rndf(2f, 4f)
    private var charging = 0f
    init { maxHp = 260f; hp = maxHp }

    override fun applyFreeze(g: Game, amount: Float) {
        if (shieldUp) {
            freezeMeter += amount
            if (freezeMeter >= 100f) {
                freezeMeter = 0f
                shieldUp = false
                shieldDownTimer = 5f
                g.addEffect(Ring(x, y, radius, radius * 2.2f, 0.5f, Pal.SHIELD_CYAN))
                g.addEffect(FloatText(x, y - radius - 24f, "SHIELD DOWN", Pal.SHIELD_CYAN))
            }
            return
        }
        super.applyFreeze(g, amount)
    }

    override fun onHit(g: Game, dmg: Float, type: DamageType) {
        if (shieldUp && type != DamageType.EXPLOSION) {
            // Shield soaks it. Teach the player: freeze first.
            g.addEffect(Ring(x, y, radius * 1.1f, radius * 1.5f, 0.2f, Pal.SHIELD_CYAN))
            if (RNG.nextFloat() < 0.25f) g.hint("Its shield resists attacks - FREEZE it first!")
            return
        }
        super.onHit(g, dmg, type)
    }

    override fun ai(g: Game, dt: Float) {
        if (!shieldUp) {
            shieldDownTimer -= dt
            if (shieldDownTimer <= 0f) shieldUp = true
        }
        val s = g.superman
        if (charging > 0f) {
            charging -= dt
            if (distf(x, y, s.x, s.y) < radius + s.radius + 20f) {
                s.hurt(16f)
                s.vx += (if (s.x > x) 1f else -1f) * 700f
                charging = 0f
            }
            return
        }
        hoverAt(g, 300f, 190f, dt, heightBias = 0f)
        chargeCd -= dt
        if (chargeCd <= 0f && distf(x, y, s.x, s.y) < 520f) {
            chargeCd = rndf(2.6f, 4.2f)
            charging = 0.7f
            val a = angleTo(x, y, s.x, s.y)
            vx = cos(a) * 760f
            vy = sin(a) * 760f
        }
    }

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        paint.style = Paint.Style.FILL
        paint.color = Color.rgb(80, 70, 90)
        c.drawRoundRect(x - 44f, y - 50f, x + 44f, y + 46f, 14f, 14f, paint)
        paint.color = Color.rgb(120, 108, 130)
        c.drawRoundRect(x - 44f, y - 50f, x + 44f, y - 20f, 14f, 14f, paint)
        paint.color = Pal.DANGER_RED
        c.drawCircle(x - 14f, y - 34f, 6f, paint)
        c.drawCircle(x + 14f, y - 34f, 6f, paint)
        // Fists
        paint.color = Color.rgb(60, 52, 68)
        c.drawCircle(x - 52f, y + 12f, 18f, paint)
        c.drawCircle(x + 52f, y + 12f, 18f, paint)
        if (shieldUp) {
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 5f
            paint.color = Color.argb(170, 90, 220, 255)
            c.drawCircle(x, y, radius * 1.25f + sin(t * 6f) * 4f, paint)
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(36, 90, 220, 255)
            c.drawCircle(x, y, radius * 1.25f, paint)
        }
    }
}

/** Cloaked alien stalker. Invisible and untouchable until revealed by X-ray vision. */
class Cloaker(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 40f
    override val scoreValue = 300
    private var attackFlash = 0f
    init { maxHp = 90f; hp = maxHp }

    fun revealed(g: Game): Boolean =
        (g.superman.xrayOn && distf(x, y, g.superman.x, g.superman.y) < 1100f) || attackFlash > 0f

    override fun targetable(g: Game): Boolean = revealed(g)

    override fun ai(g: Game, dt: Float) {
        if (attackFlash > 0f) attackFlash -= dt
        val s = g.superman
        hoverAt(g, 140f, 360f, dt, heightBias = 0f)
        fireCd -= dt
        if (fireCd <= 0f && distf(x, y, s.x, s.y) < radius + s.radius + 40f) {
            fireCd = rndf(1.2f, 1.9f)
            attackFlash = 0.8f
            s.hurt(13f)
            g.addEffect(Ring(s.x, s.y, 10f, 70f, 0.25f, Color.rgb(190, 90, 255)))
        }
    }

    override fun explosionColor(): Int = Color.rgb(190, 90, 255)

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        val vis = revealed(g)
        val alpha = if (vis) 255 else 18   // faint shimmer when cloaked
        paint.style = Paint.Style.FILL
        paint.color = Color.argb(alpha, 130, 60, 180)
        c.drawRoundRect(x - 22f, y - 40f, x + 22f, y + 30f, 16f, 16f, paint)
        paint.color = Color.argb(alpha, 190, 90, 255)
        c.drawCircle(x, y - 48f, 14f, paint)
        // Claws
        val f = if (g.superman.x > x) 1f else -1f
        paint.strokeWidth = 5f
        paint.style = Paint.Style.STROKE
        c.drawLine(x + f * 20f, y - 10f, x + f * 48f, y - 26f, paint)
        c.drawLine(x + f * 20f, y, x + f * 52f, y - 4f, paint)
        c.drawLine(x + f * 20f, y + 10f, x + f * 48f, y + 18f, paint)
        paint.style = Paint.Style.FILL
        if (vis && g.superman.xrayOn) {
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 3f
            paint.color = Color.argb(200, 150, 220, 255)
            c.drawCircle(x, y - 8f, radius + 14f, paint)
            paint.style = Paint.Style.FILL
        }
    }
}

/**
 * K-13 "Warmech" - final boss. A kryptonite-powered war machine.
 * Its armor shrugs off nearly everything; the reactor behind its chest plate
 * is the weak point, exposed on a cycle that X-ray vision lets you anticipate.
 * While the chest is open it floods the area around it with kryptonite,
 * so heat vision from outside the field is the play.
 */
class BossWarmech(x: Float, y: Float) : Enemy(x, y) {
    override val radius = 130f
    override val scoreValue = 3000
    var chestOpen = false
    private var cycleT = 0f
    private val closedTime = 9f
    private val openTime = 5f
    private var attackCd = 2.5f
    private var beamTelegraph = 0f
    private var beamActive = 0f
    private var beamAngle = 0f
    private val chestAura = KryptoZone(x, y, 300f)
    private var auraIn = false
    init { maxHp = 1500f; hp = maxHp }

    val openIn: Float get() = if (chestOpen) 0f else closedTime - cycleT

    override fun applyFreeze(g: Game, amount: Float) {
        // Too massive to freeze solid; freeze breath just slows it briefly.
        vx *= 0.9f; vy *= 0.9f
    }

    override fun onHit(g: Game, dmg: Float, type: DamageType) {
        val actual = if (chestOpen) dmg * 1.6f else dmg * 0.08f
        if (!chestOpen && RNG.nextFloat() < 0.2f) {
            g.hint("Armor's too thick - X-RAY it to find a weakness!")
        }
        super.onHit(g, actual, type)
    }

    override fun die(g: Game) {
        chestAura.life = 0.01f
        for (i in 0 until 5) {
            g.addEffect(Ring(x + rndf(-100f, 100f), y + rndf(-100f, 100f), 20f, 320f,
                rndf(0.5f, 1.0f), if (i % 2 == 0) Pal.HEAT_ORANGE else Pal.KRYPTO_GREEN))
        }
        g.camera.addShake(30f)
        super.die(g)
    }

    override fun ai(g: Game, dt: Float) {
        val s = g.superman

        // Chest cycle
        cycleT += dt
        if (!chestOpen && cycleT >= closedTime) {
            chestOpen = true
            cycleT = 0f
            chestAura.life = -1f
            if (!auraIn) { g.world.kryptoZones.add(chestAura); auraIn = true }
            g.addEffect(FloatText(x, y - radius - 40f, "REACTOR EXPOSED!", Pal.KRYPTO_GREEN))
        } else if (chestOpen && cycleT >= openTime) {
            chestOpen = false
            cycleT = 0f
            chestAura.life = 0.01f
            auraIn = false
        }
        chestAura.x = x; chestAura.y = y

        // Slow patrol above the player.
        vx += ((s.x - x) * 0.4f - vx) * dt
        vy += ((s.y - 340f - y) * 0.8f - vy) * dt
        vx = clampf(vx, -170f, 170f)
        vy = clampf(vy, -140f, 140f)

        // Attacks
        if (beamTelegraph > 0f) {
            beamTelegraph -= dt
            beamAngle = angleTo(x, y, s.x, s.y)
            if (beamTelegraph <= 0f) beamActive = 1.1f
            return
        }
        if (beamActive > 0f) {
            beamActive -= dt
            val ex = x + cos(beamAngle) * 2000f
            val ey = y + sin(beamAngle) * 2000f
            if (pointSegDist(s.x, s.y, x, y, ex, ey) < 60f) {
                s.hurt(26f * dt, kryptonite = true)  // ~26 dmg/s while in the beam
            }
            return
        }
        attackCd -= dt
        if (attackCd <= 0f) {
            attackCd = rndf(2.4f, 3.6f)
            when (RNG.nextInt(3)) {
                0 -> {   // Homing missile volley
                    for (i in 0 until 4) {
                        val a = -1.2f + i * 0.8f
                        g.projectiles.add(Projectile(x + cos(a) * 100f, y + sin(a) * 100f,
                            cos(a) * 420f, sin(a) * 420f, 18f, 13f, Pal.HEAT_ORANGE,
                            homing = true, explosionRadius = 130f, life = 5f))
                    }
                }
                1 -> beamTelegraph = 1.0f   // Kryptonite beam sweep
                2 -> {   // Kryptonite pulse at the player's position
                    g.world.kryptoZones.add(KryptoZone(s.x, s.y, 240f, 4.5f))
                    g.addEffect(Ring(s.x, s.y, 30f, 240f, 0.6f, Pal.KRYPTO_GREEN))
                    g.hint("Kryptonite pulse - move!")
                }
            }
        }
    }

    override fun drawBody(c: Canvas, paint: Paint, g: Game) {
        paint.style = Paint.Style.FILL
        // Hull
        paint.color = Color.rgb(58, 62, 74)
        c.drawRoundRect(x - 110f, y - 100f, x + 110f, y + 100f, 26f, 26f, paint)
        // Shoulders
        paint.color = Color.rgb(78, 84, 98)
        c.drawRoundRect(x - 150f, y - 70f, x - 96f, y + 30f, 18f, 18f, paint)
        c.drawRoundRect(x + 96f, y - 70f, x + 150f, y + 30f, 18f, 18f, paint)
        // Head
        paint.color = Color.rgb(44, 46, 56)
        c.drawRoundRect(x - 34f, y - 140f, x + 34f, y - 92f, 12f, 12f, paint)
        paint.color = Pal.DANGER_RED
        c.drawRect(x - 24f, y - 126f, x + 24f, y - 114f, paint)
        // Chest plate / reactor
        if (chestOpen) {
            val pulse = 0.85f + 0.15f * sin(t * 10f)
            paint.color = Color.rgb(20, 40, 26)
            c.drawCircle(x, y, 62f, paint)
            paint.color = Color.argb((255 * pulse).toInt(), 80, 255, 110)
            c.drawCircle(x, y, 44f * pulse, paint)
        } else {
            paint.color = Color.rgb(92, 98, 112)
            c.drawRoundRect(x - 58f, y - 52f, x + 58f, y + 52f, 14f, 14f, paint)
            paint.color = Color.rgb(70, 74, 86)
            c.drawLine(x, y - 52f, x, y + 52f, paint.apply { strokeWidth = 6f; style = Paint.Style.STROKE })
            paint.style = Paint.Style.FILL
            // X-ray shows the reactor through the armor.
            if (g.superman.xrayOn) {
                paint.color = Color.argb(140, 80, 255, 110)
                c.drawCircle(x, y, 40f, paint)
                paint.color = Pal.KRYPTO_GREEN
                paint.textSize = 30f
                paint.textAlign = Paint.Align.CENTER
                c.drawText("OPENS IN ${openIn.toInt() + 1}s", x, y - radius - 46f, paint)
                paint.textAlign = Paint.Align.LEFT
            }
        }
        // Beam telegraph / beam
        if (beamTelegraph > 0f || beamActive > 0f) {
            val ex = x + cos(beamAngle) * 2000f
            val ey = y + sin(beamAngle) * 2000f
            paint.style = Paint.Style.STROKE
            if (beamActive > 0f) {
                paint.strokeWidth = 34f
                paint.color = Color.argb(110, 80, 255, 110)
                c.drawLine(x, y, ex, ey, paint)
                paint.strokeWidth = 14f
                paint.color = Pal.KRYPTO_GREEN
                c.drawLine(x, y, ex, ey, paint)
            } else {
                paint.strokeWidth = 4f
                paint.color = Color.argb(150, 255, 80, 80)
                c.drawLine(x, y, ex, ey, paint)
            }
            paint.style = Paint.Style.FILL
        }
    }
}
