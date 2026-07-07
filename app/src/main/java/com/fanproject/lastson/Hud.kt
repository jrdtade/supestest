package com.fanproject.lastson

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.view.MotionEvent
import kotlin.math.min
import kotlin.math.sqrt

class Joystick {
    var baseX = 0f
    var baseY = 0f
    var radius = 130f
    var knobX = 0f
    var knobY = 0f
    var active = false
    var pointerId = -1
    var dirX = 0f
    var dirY = 0f
    var magnitude = 0f

    fun press(px: Float, py: Float, id: Int) {
        active = true
        pointerId = id
        baseX = px; baseY = py
        knobX = px; knobY = py
        dirX = 0f; dirY = 0f; magnitude = 0f
    }

    fun move(px: Float, py: Float) {
        var dx = px - baseX
        var dy = py - baseY
        val d = sqrt(dx * dx + dy * dy)
        if (d > radius) {
            dx = dx / d * radius
            dy = dy / d * radius
        }
        knobX = baseX + dx
        knobY = baseY + dy
        magnitude = min(1f, d / radius)
        if (d > 1f) {
            dirX = dx / min(d, radius)
            dirY = dy / min(d, radius)
            val n = sqrt(dirX * dirX + dirY * dirY)
            if (n > 0f) { dirX /= n; dirY /= n }
        }
    }

    fun release() {
        active = false
        pointerId = -1
        dirX = 0f; dirY = 0f; magnitude = 0f
    }
}

class AbilityButton(val label: String, val color: Int, val toggle: Boolean = false) {
    var x = 0f
    var y = 0f
    var r = 86f
    var pressed = false
    var pointerId = -1
    var enabled = true
    var toggledOn = false

    fun hit(px: Float, py: Float): Boolean = distf(px, py, x, y) < r * 1.15f
}

class Hud(private val g: Game) {
    val joystick = Joystick()
    val punchBtn = AbilityButton("PUNCH", Color.rgb(230, 230, 240))
    val heatBtn = AbilityButton("HEAT", Pal.HEAT_ORANGE)
    val freezeBtn = AbilityButton("FREEZE", Pal.ICE_BLUE)
    val xrayBtn = AbilityButton("X-RAY", Color.rgb(150, 200, 255), toggle = true)
    private val buttons = listOf(punchBtn, heatBtn, freezeBtn, xrayBtn)

    private var hintText = ""
    private var hintTimer = 0f
    private var laidOut = false
    var pauseTapped = false

    fun showHint(msg: String) {
        hintText = msg
        hintTimer = 3.5f
    }

    private fun layout(w: Float, h: Float) {
        val sc = h / 1080f
        joystick.radius = 130f * sc
        val br = 82f * sc
        for (b in buttons) b.r = br
        punchBtn.x = w - 150f * sc;  punchBtn.y = h - 190f * sc
        heatBtn.x = w - 390f * sc;   heatBtn.y = h - 150f * sc
        freezeBtn.x = w - 590f * sc; freezeBtn.y = h - 280f * sc
        xrayBtn.x = w - 260f * sc;   xrayBtn.y = h - 430f * sc
        laidOut = true
    }

    /** Handle a touch event during gameplay. */
    fun onTouch(e: MotionEvent, w: Float, h: Float) {
        if (!laidOut) layout(w, h)
        val s = g.superman
        when (e.actionMasked) {
            MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> {
                val i = e.actionIndex
                val px = e.getX(i)
                val py = e.getY(i)
                val id = e.getPointerId(i)
                // Pause button (top-right corner)
                val sc = h / 1080f
                if (px > w - 120f * sc && py < 120f * sc) {
                    pauseTapped = true
                    return
                }
                var onButton = false
                for (b in buttons) {
                    if (b.hit(px, py)) {
                        onButton = true
                        b.pressed = true
                        b.pointerId = id
                        when (b) {
                            punchBtn -> s.punchQueued = true
                            heatBtn -> if (!s.weakened && s.power > 1f) s.heatOn = true
                                       else if (s.weakened) g.hint("Kryptonite is suppressing your heat vision")
                            freezeBtn -> if (!s.weakened && s.power > 1f) s.freezeOn = true
                                         else if (s.weakened) g.hint("Kryptonite is suppressing your freeze breath")
                            xrayBtn -> {
                                if (s.xrayOn) s.xrayOn = false
                                else if (!s.weakened && s.power > 5f) s.xrayOn = true
                                else if (s.weakened) g.hint("Kryptonite is suppressing your X-ray vision")
                            }
                        }
                    }
                }
                if (!onButton && px < w * 0.45f && !joystick.active) {
                    joystick.press(px, py, id)
                }
            }
            MotionEvent.ACTION_MOVE -> {
                for (i in 0 until e.pointerCount) {
                    val id = e.getPointerId(i)
                    if (joystick.active && id == joystick.pointerId) {
                        joystick.move(e.getX(i), e.getY(i))
                    }
                }
            }
            MotionEvent.ACTION_POINTER_UP, MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                val cancelAll = e.actionMasked == MotionEvent.ACTION_UP || e.actionMasked == MotionEvent.ACTION_CANCEL
                val id = e.getPointerId(e.actionIndex)
                if (joystick.active && (cancelAll || id == joystick.pointerId)) joystick.release()
                for (b in buttons) {
                    if (b.pressed && (cancelAll || id == b.pointerId)) {
                        b.pressed = false
                        b.pointerId = -1
                        when (b) {
                            heatBtn -> s.heatOn = false
                            freezeBtn -> s.freezeOn = false
                            else -> { }
                        }
                    }
                }
            }
        }
    }

    fun update(dt: Float) {
        if (hintTimer > 0f) hintTimer -= dt
        val s = g.superman
        // Keep held states honest (power may have run out mid-hold).
        if (!heatBtn.pressed) s.heatOn = false
        if (!freezeBtn.pressed) s.freezeOn = false
        xrayBtn.toggledOn = s.xrayOn
        heatBtn.enabled = !s.weakened && s.power > 1f
        freezeBtn.enabled = !s.weakened && s.power > 1f
        xrayBtn.enabled = !s.weakened
    }

    fun draw(c: Canvas, paint: Paint, w: Float, h: Float) {
        if (!laidOut) layout(w, h)
        val sc = h / 1080f
        val s = g.superman

        // --- X-ray overlay tint ---
        if (s.xrayOn) {
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(60, 90, 150, 255)
            c.drawRect(0f, 0f, w, h, paint)
        }
        // --- Kryptonite vignette ---
        if (s.weakened) {
            paint.color = Color.argb(if (s.exposed) 80 else 40, 60, 255, 100)
            c.drawRect(0f, 0f, w, h * 0.12f, paint)
            c.drawRect(0f, h * 0.88f, w, h, paint)
            c.drawRect(0f, 0f, w * 0.06f, h, paint)
            c.drawRect(w * 0.94f, 0f, w, h, paint)
            if (s.exposed) {
                paint.color = Pal.KRYPTO_GREEN
                paint.textSize = 40f * sc
                paint.textAlign = Paint.Align.CENTER
                c.drawText("KRYPTONITE EXPOSURE", w / 2f, h * 0.16f, paint)
                paint.textAlign = Paint.Align.LEFT
            }
        }

        // --- Bars ---
        val barW = 420f * sc
        val barH = 26f * sc
        val bx = 40f * sc
        var by = 40f * sc
        paint.color = Color.argb(170, 0, 0, 0)
        c.drawRoundRect(bx - 6f, by - 6f, bx + barW + 6f, by + barH + 6f, 8f, 8f, paint)
        paint.color = if (s.weakened) Pal.KRYPTO_DARK else Color.rgb(50, 120, 255)
        c.drawRoundRect(bx, by, bx + barW * clampf(s.hp / s.maxHp, 0f, 1f), by + barH, 6f, 6f, paint)
        paint.color = Pal.HUD_TEXT
        paint.textSize = 22f * sc
        c.drawText("VITALS", bx + 8f, by + barH - 6f, paint)

        by += barH + 18f * sc
        paint.color = Color.argb(170, 0, 0, 0)
        c.drawRoundRect(bx - 6f, by - 6f, bx + barW + 6f, by + barH + 6f, 8f, 8f, paint)
        paint.color = Pal.HUD_YELLOW
        c.drawRoundRect(bx, by, bx + barW * clampf(s.power / s.maxPower, 0f, 1f), by + barH, 6f, 6f, paint)
        paint.color = Color.rgb(40, 30, 0)
        c.drawText("SOLAR", bx + 8f, by + barH - 6f, paint)

        // --- Objective ---
        g.mission?.let { m ->
            paint.color = Color.argb(150, 0, 0, 0)
            paint.textSize = 30f * sc
            val txt = m.objectiveText()
            val tw = paint.measureText(txt)
            c.drawRoundRect(w / 2f - tw / 2f - 18f, 18f * sc, w / 2f + tw / 2f + 18f, 66f * sc, 10f, 10f, paint)
            paint.color = Pal.HUD_TEXT
            paint.textAlign = Paint.Align.CENTER
            c.drawText(txt, w / 2f, 52f * sc, paint)
            paint.textAlign = Paint.Align.LEFT
        }

        // --- Score ---
        paint.color = Pal.HUD_TEXT
        paint.textSize = 26f * sc
        paint.textAlign = Paint.Align.RIGHT
        c.drawText("SCORE ${g.score}", w - 150f * sc, 52f * sc, paint)
        paint.textAlign = Paint.Align.LEFT

        // --- Pause button ---
        paint.color = Color.argb(140, 255, 255, 255)
        c.drawRect(w - 96f * sc, 30f * sc, w - 76f * sc, 86f * sc, paint)
        c.drawRect(w - 62f * sc, 30f * sc, w - 42f * sc, 86f * sc, paint)

        // --- Hint banner ---
        if (hintTimer > 0f && hintText.isNotEmpty()) {
            paint.textSize = 34f * sc
            val alpha = (clampf(hintTimer, 0f, 1f) * 235f).toInt()
            paint.color = Color.argb((alpha * 0.7f).toInt(), 0, 0, 0)
            val tw = paint.measureText(hintText)
            val hy = h * 0.72f
            c.drawRoundRect(w / 2f - tw / 2f - 20f, hy - 44f * sc, w / 2f + tw / 2f + 20f, hy + 16f * sc, 12f, 12f, paint)
            paint.color = Color.argb(alpha, 255, 230, 140)
            paint.textAlign = Paint.Align.CENTER
            c.drawText(hintText, w / 2f, hy, paint)
            paint.textAlign = Paint.Align.LEFT
        }

        // --- Joystick ---
        if (joystick.active) {
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 4f
            paint.color = Color.argb(120, 255, 255, 255)
            c.drawCircle(joystick.baseX, joystick.baseY, joystick.radius, paint)
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(140, 255, 255, 255)
            c.drawCircle(joystick.knobX, joystick.knobY, 46f * sc, paint)
        } else {
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 3f
            paint.color = Color.argb(50, 255, 255, 255)
            c.drawCircle(200f * sc, h - 240f * sc, joystick.radius, paint)
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(60, 255, 255, 255)
            paint.textSize = 24f * sc
            paint.textAlign = Paint.Align.CENTER
            c.drawText("FLY", 200f * sc, h - 232f * sc, paint)
            paint.textAlign = Paint.Align.LEFT
        }

        // --- Ability buttons ---
        for (b in buttons) {
            val base = if (b.enabled) 165 else 55
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(if (b.pressed || b.toggledOn) 235 else base,
                Color.red(b.color), Color.green(b.color), Color.blue(b.color))
            c.drawCircle(b.x, b.y, b.r, paint)
            paint.style = Paint.Style.STROKE
            paint.strokeWidth = 4f
            paint.color = Color.argb(200, 255, 255, 255)
            c.drawCircle(b.x, b.y, b.r, paint)
            paint.style = Paint.Style.FILL
            paint.color = if (b.pressed || b.toggledOn) Color.BLACK else Color.argb(230, 255, 255, 255)
            paint.textSize = 26f * sc
            paint.textAlign = Paint.Align.CENTER
            c.drawText(b.label, b.x, b.y + 9f * sc, paint)
            paint.textAlign = Paint.Align.LEFT
            // Kryptonite lockout crosses
            if (!b.enabled && b !== punchBtn) {
                paint.strokeWidth = 6f
                paint.style = Paint.Style.STROKE
                paint.color = Color.argb(200, 80, 255, 110)
                c.drawLine(b.x - b.r * 0.6f, b.y - b.r * 0.6f, b.x + b.r * 0.6f, b.y + b.r * 0.6f, paint)
                paint.style = Paint.Style.FILL
            }
        }
    }
}
