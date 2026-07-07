package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import kotlin.math.sin

enum class CivState { PANIC, TRAPPED, FALLING, CARRIED, SAFE, DEAD }

class Civilian(var x: Float, var y: Float, var state: CivState) {
    var vx = 0f
    var vy = 0f
    var rubble: Rubble? = null     // set while TRAPPED
    private var burn = 0f          // 0..1 while standing inside flames
    private var t = rndf(0f, 6f)
    private val shirt = Color.rgb(120 + RNG.nextInt(120), 90 + RNG.nextInt(120), 80 + RNG.nextInt(140))
    val radius = 26f

    val needsRescue: Boolean
        get() = state == CivState.PANIC || state == CivState.FALLING || state == CivState.TRAPPED

    fun update(g: Game, dt: Float) {
        t += dt
        when (state) {
            CivState.FALLING -> {
                vy += 900f * dt
                if (vy > 800f) vy = 800f   // flailing terminal velocity: a catch window
                y += vy * dt
                x += vx * dt
                // Superman catches by flying into them.
                val s = g.superman
                if (!s.dead && s.carrying == null && distf(x, y, s.x, s.y) < radius + s.radius + 14f) {
                    if (s.tryPickupCivilian(this)) {
                        g.addEffect(FloatText(x, y - 40f, "CAUGHT!", Pal.HUD_YELLOW))
                        vy = 0f
                    }
                    return
                }
                if (y >= g.world.groundY - radius) {
                    if (vy > 700f) {
                        state = CivState.DEAD
                        y = g.world.groundY - radius
                        g.onCivilianLost(this)
                    } else {
                        // Survivable landing; they panic on the ground instead.
                        y = g.world.groundY - radius
                        vy = 0f
                        state = CivState.PANIC
                    }
                }
            }
            CivState.PANIC -> {
                y = g.world.groundY - radius
                // Standing in flames slowly overwhelms them; ~10s of exposure is fatal,
                // and the burn recovers once the fire is out or they're carried clear.
                var inFire = false
                for (f in g.world.fires) {
                    if (!f.out && distf(x, y, f.x, f.y) < f.radius * 0.8f) { inFire = true; break }
                }
                if (inFire) {
                    burn += dt / 10f
                    if (burn >= 1f) {
                        state = CivState.DEAD
                        g.onCivilianLost(this)
                        return
                    }
                } else {
                    burn = (burn - dt / 4f).coerceAtLeast(0f)
                }
                val s = g.superman
                if (!s.dead && s.carrying == null && distf(x, y, s.x, s.y) < radius + s.radius + 12f) {
                    s.tryPickupCivilian(this)
                }
            }
            CivState.TRAPPED -> {
                val r = rubble
                if (r != null && r.destroyed) {
                    state = CivState.PANIC
                    g.addEffect(FloatText(x, y - 50f, "FREED!", Pal.HUD_YELLOW))
                }
            }
            CivState.CARRIED, CivState.SAFE, CivState.DEAD -> { /* handled elsewhere */ }
        }
    }

    fun draw(c: Canvas, paint: Paint) {
        if (state == CivState.DEAD) {
            paint.color = Color.argb(140, 90, 90, 100)
            c.drawCircle(x, y + 10f, 16f, paint)
            return
        }
        val wave = if (needsRescue) sin(t * 10f) * 8f else 0f
        paint.style = Paint.Style.FILL
        // Body
        paint.color = shirt
        c.drawRoundRect(x - 10f, y - 16f, x + 10f, y + 14f, 6f, 6f, paint)
        // Head
        paint.color = Color.rgb(230, 185, 150)
        c.drawCircle(x, y - 26f, 9f, paint)
        // Waving arms when in danger
        paint.strokeWidth = 5f
        paint.style = Paint.Style.STROKE
        paint.color = shirt
        c.drawLine(x - 8f, y - 12f, x - 18f, y - 26f - wave, paint)
        c.drawLine(x + 8f, y - 12f, x + 18f, y - 26f + wave, paint)
        paint.style = Paint.Style.FILL
        // Rescue marker
        if (needsRescue) {
            paint.color = Pal.HUD_YELLOW
            c.drawCircle(x, y - 56f + sin(t * 5f) * 4f, 6f, paint)
        }
        if (state == CivState.SAFE) {
            paint.color = Color.rgb(90, 255, 140)
            c.drawCircle(x, y - 48f, 5f, paint)
        }
    }
}
