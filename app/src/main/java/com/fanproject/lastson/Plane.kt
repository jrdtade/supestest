package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.sin
// (sin used for pitch-derived positions via kotlin.math)

/**
 * The stricken airliner for the "Flight 236" mission - a recreation of the
 * Superman Returns save. It streaks across the sky losing altitude; the
 * starboard engine is on fire (freeze breath puts it out), and the only way
 * to land it is to brace under the nose and bleed off its speed before the
 * ground arrives.
 */
class Plane(startX: Float, startY: Float) {
    var x = startX
    var y = startY
    var vx = 520f
    var vy = 30f
    var fireHp = 120f
    val engineOnFire: Boolean get() = fireHp > 0f
    var landed = false
    var crashed = false
    var braced = false            // Superman is under the nose pushing
    private var t = 0f
    private var turbulenceT = 0f

    val len = 560f                // fuselage length
    val pitch: Float get() = atan2(vy, vx)

    // World position of the burning engine (under the near wing).
    val engineX: Float get() = x - cos(pitch) * 40f
    val engineY: Float get() = y + 70f - sin(pitch) * 40f

    // Where Superman must be to brace: just under the nose cone.
    val noseX: Float get() = x + cos(pitch) * (len * 0.5f)
    val noseY: Float get() = y + sin(pitch) * (len * 0.5f) + 40f

    fun update(g: Game, dt: Float) {
        if (landed || crashed) return
        t += dt

        val s = g.superman
        // Bracing: Superman under the nose within the catch zone. Impossible while
        // the engine still burns (the airframe shakes too violently) or while
        // kryptonite-weakened.
        val inZone = !s.dead && distf(s.x, s.y, noseX, noseY) < 130f
        if (inZone && engineOnFire && !braced && RNG.nextFloat() < dt * 1.5f) {
            g.hint("Too much turbulence - kill that engine fire first!")
        }
        braced = inZone && !s.weakened && !engineOnFire
        s.bracingPlane = braced

        if (braced) {
            // Superman's strength bleeds off speed and descent.
            vx -= 210f * dt
            vy -= 320f * dt
            if (vx < 70f) vx = 70f
            if (vy < -20f) vy = -20f
            // He gets dragged along with the plane while pushing.
            s.x = noseX
            s.y = noseY
            s.facing = 1
            // Turbulence tries to shake him off (fails if kryptonite-weakened already handled).
            turbulenceT -= dt
            if (turbulenceT <= 0f) {
                turbulenceT = rndf(1.2f, 2.2f)
                g.camera.addShake(10f)
            }
        } else {
            // Unsupported: gravity wins, faster while the engine burns.
            vy += (if (engineOnFire) 14f else 8f) * dt
            vx += 6f * dt
        }

        x += vx * dt
        y += vy * dt

        // Engine fire particles
        if (engineOnFire && RNG.nextFloat() < dt * 50f) {
            g.addEffect(Particle(engineX, engineY, rndf(-260f, -80f) - vx * 0.4f, rndf(-120f, 40f),
                rndf(0.4f, 0.8f), rndf(8f, 20f), if (RNG.nextBoolean()) Pal.HEAT_ORANGE else Color.rgb(90, 90, 95)))
        }

        // Ground contact
        if (y + 90f >= g.world.groundY) {
            y = g.world.groundY - 90f
            if (vy <= 260f && vx <= 340f) {
                landed = true
                vy = 0f
                g.camera.addShake(14f)
                g.addEffect(Ring(x, y + 80f, 40f, 500f, 0.8f, Color.rgb(200, 200, 210)))
            } else {
                crashed = true
                g.explosionAt(x, y, 500f, 0f, false)
                g.camera.addShake(40f)
            }
            s.bracingPlane = false
        }
    }

    fun draw(c: Canvas, paint: Paint) {
        c.save()
        c.translate(x, y)
        c.rotate(Math.toDegrees(pitch.toDouble()).toFloat())

        paint.style = Paint.Style.FILL
        // Fuselage
        paint.color = if (crashed) Color.rgb(80, 70, 70) else Color.rgb(225, 228, 235)
        c.drawRoundRect(-len / 2f, -50f, len / 2f, 50f, 60f, 60f, paint)
        // Nose cone tint
        paint.color = Color.rgb(60, 70, 90)
        c.drawArc(len / 2f - 90f, -50f, len / 2f + 10f, 50f, -70f, 140f, true, paint)
        // Tail fin
        paint.color = Color.rgb(200, 60, 60)
        val tail = Path()
        tail.moveTo(-len / 2f + 30f, -40f)
        tail.lineTo(-len / 2f - 10f, -150f)
        tail.lineTo(-len / 2f + 90f, -40f)
        tail.close()
        c.drawPath(tail, paint)
        // Wing
        paint.color = Color.rgb(190, 195, 205)
        val wing = Path()
        wing.moveTo(-40f, 10f)
        wing.lineTo(-190f, 120f)
        wing.lineTo(-60f, 120f)
        wing.lineTo(60f, 14f)
        wing.close()
        c.drawPath(wing, paint)
        // Engine pod
        paint.color = if (engineOnFire) Color.rgb(120, 60, 40) else Color.rgb(120, 125, 135)
        c.drawRoundRect(-90f, 58f, -10f, 96f, 18f, 18f, paint)
        // Windows
        paint.color = Color.rgb(90, 130, 180)
        var wx = -len / 2f + 90f
        while (wx < len / 2f - 110f) {
            c.drawCircle(wx, -14f, 8f, paint)
            wx += 42f
        }
        c.restore()
    }
}
