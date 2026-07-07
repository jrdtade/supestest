package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import java.util.Random
import kotlin.math.abs
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

val RNG = Random()

fun clampf(v: Float, lo: Float, hi: Float): Float = max(lo, min(hi, v))
fun lerpf(a: Float, b: Float, t: Float): Float = a + (b - a) * clampf(t, 0f, 1f)
fun rndf(a: Float, b: Float): Float = a + RNG.nextFloat() * (b - a)

fun distf(x1: Float, y1: Float, x2: Float, y2: Float): Float {
    val dx = x2 - x1
    val dy = y2 - y1
    return sqrt(dx * dx + dy * dy)
}

fun angleTo(x1: Float, y1: Float, x2: Float, y2: Float): Float = atan2(y2 - y1, x2 - x1)

/** Distance from point (px,py) to segment (x1,y1)-(x2,y2). Used for beam hit tests. */
fun pointSegDist(px: Float, py: Float, x1: Float, y1: Float, x2: Float, y2: Float): Float {
    val dx = x2 - x1
    val dy = y2 - y1
    val len2 = dx * dx + dy * dy
    if (len2 < 0.0001f) return distf(px, py, x1, y1)
    var t = ((px - x1) * dx + (py - y1) * dy) / len2
    t = clampf(t, 0f, 1f)
    return distf(px, py, x1 + t * dx, y1 + t * dy)
}

/**
 * Camera that follows a target through the world with smoothing, clamped to
 * world bounds, with support for screen shake.
 */
class Camera {
    var x = 0f
    var y = 0f
    var zoom = 1f
    var shake = 0f
    private var shakeX = 0f
    private var shakeY = 0f

    fun follow(tx: Float, ty: Float, dt: Float, worldW: Float, worldH: Float, screenW: Float, screenH: Float) {
        val s = clampf(dt * 4.5f, 0f, 1f)
        x += (tx - x) * s
        y += (ty - y) * s
        val halfW = screenW / (2f * zoom)
        val halfH = screenH / (2f * zoom)
        x = clampf(x, halfW, max(halfW, worldW - halfW))
        y = clampf(y, halfH, max(halfH, worldH - halfH))
        if (shake > 0f) {
            shake = max(0f, shake - dt * 18f)
            shakeX = rndf(-shake, shake)
            shakeY = rndf(-shake, shake)
        } else {
            shakeX = 0f; shakeY = 0f
        }
    }

    fun addShake(amount: Float) {
        shake = min(40f, shake + amount)
    }

    fun toScreenX(wx: Float, screenW: Float): Float = (wx - x + shakeX) * zoom + screenW / 2f
    fun toScreenY(wy: Float, screenH: Float): Float = (wy - y + shakeY) * zoom + screenH / 2f
    fun apply(c: Canvas, screenW: Float, screenH: Float) {
        c.translate(screenW / 2f, screenH / 2f)
        c.scale(zoom, zoom)
        c.translate(-x + shakeX, -y + shakeY)
    }
}

// ---------------------------------------------------------------------------
// Effects: lightweight particles and floating text for combat feedback.
// ---------------------------------------------------------------------------

abstract class Effect {
    var dead = false
    abstract fun update(dt: Float)
    abstract fun draw(c: Canvas, paint: Paint)
}

class Particle(
    var x: Float, var y: Float,
    var vx: Float, var vy: Float,
    var life: Float,
    var size: Float,
    var color: Int,
    var gravity: Float = 0f,
    var drag: Float = 0f
) : Effect() {
    private val maxLife = life

    override fun update(dt: Float) {
        x += vx * dt
        y += vy * dt
        vy += gravity * dt
        if (drag > 0f) {
            vx -= vx * drag * dt
            vy -= vy * drag * dt
        }
        life -= dt
        if (life <= 0f) dead = true
    }

    override fun draw(c: Canvas, paint: Paint) {
        val a = clampf(life / maxLife, 0f, 1f)
        paint.style = Paint.Style.FILL
        paint.color = color
        paint.alpha = (a * 255f).toInt()
        c.drawCircle(x, y, size * (0.4f + 0.6f * a), paint)
        paint.alpha = 255
    }
}

class Ring(
    var x: Float, var y: Float,
    var startR: Float, var endR: Float,
    var life: Float,
    var color: Int,
    var strokeW: Float = 6f
) : Effect() {
    private val maxLife = life

    override fun update(dt: Float) {
        life -= dt
        if (life <= 0f) dead = true
    }

    override fun draw(c: Canvas, paint: Paint) {
        val t = 1f - clampf(life / maxLife, 0f, 1f)
        paint.style = Paint.Style.STROKE
        paint.strokeWidth = strokeW * (1f - t * 0.7f)
        paint.color = color
        paint.alpha = ((1f - t) * 220f).toInt()
        c.drawCircle(x, y, lerpf(startR, endR, t), paint)
        paint.alpha = 255
        paint.style = Paint.Style.FILL
    }
}

class FloatText(
    var x: Float, var y: Float,
    private val text: String,
    private val color: Int,
    private val textSize: Float = 34f
) : Effect() {
    private var life = 1.1f

    override fun update(dt: Float) {
        y -= 60f * dt
        life -= dt
        if (life <= 0f) dead = true
    }

    override fun draw(c: Canvas, paint: Paint) {
        paint.style = Paint.Style.FILL
        paint.color = color
        paint.textSize = textSize
        paint.textAlign = Paint.Align.CENTER
        paint.alpha = (clampf(life, 0f, 1f) * 255f).toInt()
        c.drawText(text, x, y, paint)
        paint.alpha = 255
        paint.textAlign = Paint.Align.LEFT
    }
}

// Common color constants used across the game.
object Pal {
    val SUPER_BLUE = Color.rgb(30, 80, 200)
    val SUPER_RED = Color.rgb(210, 35, 42)
    val CAPE_RED = Color.rgb(180, 25, 32)
    val KRYPTO_GREEN = Color.rgb(80, 255, 110)
    val KRYPTO_DARK = Color.rgb(20, 120, 45)
    val HEAT_ORANGE = Color.rgb(255, 120, 30)
    val HEAT_CORE = Color.rgb(255, 230, 180)
    val ICE_BLUE = Color.rgb(160, 225, 255)
    val HUD_YELLOW = Color.rgb(255, 200, 40)
    val HUD_TEXT = Color.rgb(235, 240, 255)
    val DANGER_RED = Color.rgb(255, 70, 60)
    val SHIELD_CYAN = Color.rgb(90, 220, 255)
}
