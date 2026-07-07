package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.LinearGradient
import android.graphics.Paint
import android.graphics.Shader
import kotlin.math.sin

class Building(val x: Float, val w: Float, val h: Float, val tint: Int)

/** A blaze on a rooftop or street. Extinguished with freeze breath. */
class FireZone(var x: Float, var y: Float, var radius: Float) {
    var intensity = 1f   // 0 = out
    val out: Boolean get() = intensity <= 0f
    var flicker = rndf(0f, 6.28f)
}

/** Collapsed debris pinning a civilian. Only super-strength (punches) clears it. */
class Rubble(var x: Float, var y: Float) {
    var hp = 120f
    val destroyed: Boolean get() = hp <= 0f
}

/**
 * A kryptonite radiation field. While Superman is inside one: health drains,
 * outgoing damage is quartered, and heat vision / freeze breath / X-ray are
 * unavailable.
 */
class KryptoZone(var x: Float, var y: Float, var radius: Float, var life: Float = -1f) {
    val expired: Boolean get() = life == 0f

    fun update(dt: Float) {
        if (life > 0f) {
            life -= dt
            if (life <= 0f) life = 0f
        }
    }

    fun contains(px: Float, py: Float): Boolean = !expired && distf(px, py, x, y) < radius
}

/**
 * The armored kryptonite bomb from Mission 3. Sealed inside a lead-lined
 * crate: X-ray reveals it, heat vision cuts the casing open, freeze breath
 * stabilizes the exposed core.
 */
class BombCrate(var x: Float, var y: Float) {
    var revealed = false          // Superman has seen it with X-ray
    var casingHp = 160f           // heat vision only
    val casingOpen: Boolean get() = casingHp <= 0f
    var coreFreeze = 0f           // freeze breath fills this to 100 to disarm
    var disarmed = false
    var auraAdded = false
}

class SafeZone(val x: Float, val y: Float, val radius: Float = 170f)

class World(val width: Float, val height: Float) {
    val groundY = height - 140f
    val buildings = ArrayList<Building>()
    val farSkyline = ArrayList<Building>()
    val fires = ArrayList<FireZone>()
    val rubblePiles = ArrayList<Rubble>()
    val kryptoZones = ArrayList<KryptoZone>()
    val safeZones = ArrayList<SafeZone>()
    var bombCrate: BombCrate? = null

    // Sky colors, set per mission for mood.
    var skyTop = Color.rgb(20, 35, 80)
    var skyBottom = Color.rgb(120, 90, 130)
    private var skyShader: Shader? = null
    private var t = 0f

    fun generateCity(seed: Long, density: Float = 1f) {
        val rng = java.util.Random(seed)
        buildings.clear()
        farSkyline.clear()
        var x = 100f
        while (x < width - 300f) {
            val w = 180f + rng.nextFloat() * 260f
            val h = (250f + rng.nextFloat() * 700f) * density
            val tint = Color.rgb(
                30 + rng.nextInt(30),
                35 + rng.nextInt(30),
                55 + rng.nextInt(40)
            )
            buildings.add(Building(x, w, h, tint))
            x += w + 90f + rng.nextFloat() * 220f
        }
        x = 0f
        while (x < width) {
            val w = 260f + rng.nextFloat() * 380f
            val h = 500f + rng.nextFloat() * 900f
            farSkyline.add(Building(x, w, h, Color.rgb(18, 24, 46)))
            x += w + 40f
        }
    }

    fun update(dt: Float) {
        t += dt
        kryptoZones.removeAll { it.expired }
        for (z in kryptoZones) z.update(dt)
    }

    fun inKryptonite(px: Float, py: Float): Boolean {
        for (z in kryptoZones) if (z.contains(px, py)) return true
        return false
    }

    fun drawBackground(c: Canvas, cam: Camera, screenW: Float, screenH: Float, paint: Paint) {
        if (skyShader == null) {
            skyShader = LinearGradient(0f, 0f, 0f, screenH, skyTop, skyBottom, Shader.TileMode.CLAMP)
        }
        paint.style = Paint.Style.FILL
        paint.shader = skyShader
        c.drawRect(0f, 0f, screenW, screenH, paint)
        paint.shader = null

        // Far skyline with light parallax.
        val par = 0.35f
        for (b in farSkyline) {
            val sx = (b.x - cam.x * par) * cam.zoom + screenW / 2f
            val sw = b.w * cam.zoom
            if (sx + sw < 0f || sx > screenW) continue
            val groundS = cam.toScreenY(groundY, screenH)
            val topS = groundS - b.h * cam.zoom * 0.8f
            paint.color = b.tint
            c.drawRect(sx, topS, sx + sw, groundS, paint)
        }
    }

    /** Draw world geometry. Called inside the camera transform. */
    fun drawWorld(c: Canvas, paint: Paint, xrayOn: Boolean) {
        // Ground.
        paint.style = Paint.Style.FILL
        paint.color = Color.rgb(28, 30, 38)
        c.drawRect(0f, groundY, width, height, paint)
        paint.color = Color.rgb(52, 54, 62)
        c.drawRect(0f, groundY, width, groundY + 14f, paint)

        // Buildings with window grids.
        for (b in buildings) {
            val top = groundY - b.h
            paint.color = if (xrayOn) Color.argb(70, 90, 140, 220) else b.tint
            c.drawRect(b.x, top, b.x + b.w, groundY, paint)
            paint.color = if (xrayOn) Color.argb(40, 150, 200, 255) else Color.argb(160, 255, 220, 130)
            var wy = top + 30f
            while (wy < groundY - 40f) {
                var wx = b.x + 22f
                while (wx < b.x + b.w - 30f) {
                    if (((wx + wy).toInt() / 37) % 3 != 0) {
                        c.drawRect(wx, wy, wx + 16f, wy + 22f, paint)
                    }
                    wx += 46f
                }
                wy += 58f
            }
        }

        // Safe zones: glowing beacons on the ground.
        for (s in safeZones) {
            paint.color = Color.argb(70, 90, 255, 140)
            c.drawCircle(s.x, s.y, s.radius, paint)
            paint.color = Color.rgb(90, 255, 140)
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 5f
            c.drawCircle(s.x, s.y, s.radius * (0.8f + 0.2f * sin(t * 3f)), paint)
            paint.style = Paint.Style.FILL
            paint.textSize = 34f
            paint.textAlign = Paint.Align.CENTER
            c.drawText("SAFE ZONE", s.x, s.y - s.radius - 14f, paint)
            paint.textAlign = Paint.Align.LEFT
        }

        // Rubble piles.
        for (r in rubblePiles) {
            if (r.destroyed) continue
            paint.color = Color.rgb(95, 88, 80)
            c.drawCircle(r.x - 30f, r.y - 18f, 34f, paint)
            c.drawCircle(r.x + 26f, r.y - 14f, 40f, paint)
            c.drawCircle(r.x - 2f, r.y - 42f, 30f, paint)
            paint.color = Color.rgb(70, 64, 58)
            c.drawCircle(r.x + 6f, r.y - 20f, 26f, paint)
        }

        // Fires.
        for (f in fires) {
            if (f.out) continue
            val i = f.intensity
            f.flicker += 0.25f
            paint.color = Color.argb((110 * i).toInt(), 255, 120, 20)
            c.drawCircle(f.x, f.y, f.radius * (1f + 0.08f * sin(f.flicker)), paint)
            paint.color = Color.argb((190 * i).toInt(), 255, 170, 40)
            c.drawCircle(f.x, f.y + f.radius * 0.15f, f.radius * 0.6f, paint)
            paint.color = Color.argb((220 * i).toInt(), 255, 235, 160)
            c.drawCircle(f.x, f.y + f.radius * 0.25f, f.radius * 0.3f, paint)
        }

        // Kryptonite fields.
        for (z in kryptoZones) {
            paint.color = Color.argb(46, 80, 255, 110)
            c.drawCircle(z.x, z.y, z.radius, paint)
            paint.color = Color.argb(120, 80, 255, 110)
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 4f
            c.drawCircle(z.x, z.y, z.radius * (0.9f + 0.1f * sin(t * 5f)), paint)
            paint.style = Paint.Style.FILL
        }

        // Bomb crate.
        bombCrate?.let { cr ->
            paint.color = Color.rgb(70, 75, 85)
            c.drawRect(cr.x - 70f, cr.y - 110f, cr.x + 70f, cr.y, paint)
            paint.color = Color.rgb(110, 115, 125)
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 6f
            c.drawRect(cr.x - 70f, cr.y - 110f, cr.x + 70f, cr.y, paint)
            c.drawLine(cr.x - 70f, cr.y - 55f, cr.x + 70f, cr.y - 55f, paint)
            paint.style = Paint.Style.FILL
            if (cr.casingOpen || (xrayOn && distToCrateVisible)) {
                // Exposed / x-rayed core.
                val pulse = 0.8f + 0.2f * sin(t * 6f)
                paint.color = Color.argb((200 * pulse).toInt(), 80, 255, 110)
                c.drawCircle(cr.x, cr.y - 55f, 34f * pulse, paint)
                if (cr.disarmed) {
                    paint.color = Pal.ICE_BLUE
                    c.drawCircle(cr.x, cr.y - 55f, 38f, paint)
                }
            }
            if (cr.casingOpen && !cr.disarmed) {
                paint.color = Color.argb(200, 80, 255, 110)
                paint.textSize = 30f
                paint.textAlign = Paint.Align.CENTER
                c.drawText("FREEZE THE CORE", cr.x, cr.y - 140f, paint)
                paint.textAlign = Paint.Align.LEFT
            }
        }
    }

    // Set by Game each frame so the crate knows whether X-ray reveal applies.
    var distToCrateVisible = false
}
