package com.fanproject.lastson

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.view.MotionEvent
import kotlin.math.sin

enum class GState { TITLE, MISSION_SELECT, BRIEFING, PLAYING, PAUSED, MISSION_COMPLETE, MISSION_FAILED, VICTORY }

class Game(private val context: Context) {
    var state = GState.TITLE
    var screenW = 1920f
    var screenH = 1080f

    var world = World(7000f, 2200f)
    val superman = Superman(this)
    val enemies = ArrayList<Enemy>()
    val projectiles = ArrayList<Projectile>()
    val civilians = ArrayList<Civilian>()
    private val effects = ArrayList<Effect>()
    val camera = Camera()
    val hud = Hud(this)
    var mission: Mission? = null
    var plane: Plane? = null

    // Per-mission stats (read by mission scripts)
    var score = 0
    var rescues = 0
    var civiliansLost = 0
    var kills = 0
    var failReason = ""

    private val prefs = context.getSharedPreferences("lastson", Context.MODE_PRIVATE)
    var unlocked = prefs.getInt("unlocked", 1)
        private set

    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private var uiT = 0f
    private var stateDelay = 0f   // brief input lockout on state changes to avoid tap-through

    // -----------------------------------------------------------------------
    // Mission lifecycle
    // -----------------------------------------------------------------------

    private fun missionFor(i: Int): Mission = when (i) {
        1 -> Mission1(this)
        2 -> Mission2(this)
        3 -> Mission3(this)
        4 -> Mission4(this)
        5 -> Mission5(this)
        else -> Mission1(this)
    }

    private fun worldWidthFor(i: Int): Float = if (i == 4) 26000f else 7000f

    fun loadMission(i: Int) {
        val m = missionFor(i)
        world = World(worldWidthFor(i), 2200f)
        enemies.clear()
        projectiles.clear()
        civilians.clear()
        effects.clear()
        plane = null
        rescues = 0
        civiliansLost = 0
        kills = 0
        score = if (i == 1) 0 else score
        failReason = ""
        camera.zoom = 1f
        mission = m
        switchState(GState.BRIEFING)
    }

    private fun launchMission() {
        mission?.start()
        camera.x = superman.x
        camera.y = superman.y
        switchState(GState.PLAYING)
    }

    private fun switchState(s: GState) {
        state = s
        stateDelay = 0.35f
    }

    // -----------------------------------------------------------------------
    // Callbacks from entities
    // -----------------------------------------------------------------------

    fun addEffect(e: Effect) { if (effects.size < 600) effects.add(e) }
    fun hint(msg: String) = hud.showHint(msg)

    fun onEnemyKilled(e: Enemy) {
        kills++
        score += e.scoreValue
        addEffect(FloatText(e.x, e.y - 40f, "+${e.scoreValue}", Pal.HUD_YELLOW))
    }

    fun onCivilianRescued(c: Civilian) {
        rescues++
        score += 150
        addEffect(FloatText(c.x, c.y - 60f, "RESCUED +150", Color.rgb(90, 255, 140)))
    }

    fun onCivilianLost(c: Civilian) {
        civiliansLost++
        addEffect(FloatText(c.x, c.y - 60f, "CIVILIAN LOST", Pal.DANGER_RED))
        camera.addShake(10f)
    }

    fun onFireExtinguished() {
        score += 100
    }

    fun onRubbleCleared(r: Rubble) {
        score += 50
    }

    fun onSupermanDown() {
        failReason = "Superman is down. The city has no one else."
        addEffect(Ring(superman.x, superman.y, 20f, 300f, 1f, Pal.KRYPTO_GREEN))
    }

    fun explosionAt(x: Float, y: Float, radius: Float, dmg: Float, kryptonite: Boolean) {
        for (i in 0 until 22) {
            addEffect(Particle(x, y, rndf(-500f, 500f), rndf(-500f, 300f),
                rndf(0.4f, 1.0f), rndf(6f, 16f),
                if (kryptonite) Pal.KRYPTO_GREEN else Pal.HEAT_ORANGE, gravity = 400f))
        }
        addEffect(Ring(x, y, 20f, radius, 0.5f, if (kryptonite) Pal.KRYPTO_GREEN else Pal.HEAT_ORANGE))
        camera.addShake(12f)
        if (dmg > 0f && !superman.dead) {
            val d = distf(x, y, superman.x, superman.y)
            if (d < radius) superman.hurt(dmg * (1f - d / radius), kryptonite)
        }
        if (kryptonite && dmg > 0f) {
            world.kryptoZones.add(KryptoZone(x, y, radius * 0.8f, 6f))
        }
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    fun update(dt: Float) {
        uiT += dt
        if (stateDelay > 0f) stateDelay -= dt
        when (state) {
            GState.PLAYING -> updatePlaying(dt)
            else -> {
                // Keep ambient effects alive on overlay screens.
                effects.forEach { it.update(dt) }
                effects.removeAll { it.dead }
            }
        }
    }

    private fun updatePlaying(dt: Float) {
        world.update(dt)
        superman.update(dt)
        hud.update(dt)

        for (e in enemies) e.update(this, dt)
        enemies.removeAll { it.dead }

        for (p in projectiles) p.update(this, dt)
        projectiles.removeAll { it.dead }

        for (c in civilians) c.update(this, dt)

        plane?.update(this, dt)

        for (fx in effects) fx.update(dt)
        effects.removeAll { it.dead }

        // Camera: in the plane mission, frame both Superman and the plane.
        val p = plane
        if (p != null && !p.landed && !p.crashed) {
            val mx = (superman.x + p.x) / 2f
            val my = (superman.y + p.y) / 2f
            camera.follow(mx, my, dt, world.width, world.height, screenW, screenH)
        } else {
            camera.follow(superman.x + superman.facing * 120f, superman.y, dt,
                world.width, world.height, screenW, screenH)
        }

        val m = mission ?: return
        m.update(dt)
        if (m.complete) {
            score += 1000
            if (m.id >= unlocked && m.id < 5) {
                unlocked = m.id + 1
                prefs.edit().putInt("unlocked", unlocked).apply()
            }
            switchState(if (m.id == 5) GState.VICTORY else GState.MISSION_COMPLETE)
        } else if (m.failed || superman.dead) {
            if (failReason.isEmpty()) failReason = m.failReason
            switchState(GState.MISSION_FAILED)
        }

        if (hud.pauseTapped) {
            hud.pauseTapped = false
            switchState(GState.PAUSED)
        }
    }

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    fun onTouch(e: MotionEvent) {
        when (state) {
            GState.PLAYING -> hud.onTouch(e, screenW, screenH)
            else -> {
                if (e.actionMasked == MotionEvent.ACTION_DOWN && stateDelay <= 0f) {
                    handleMenuTap(e.x, e.y)
                }
            }
        }
    }

    private fun handleMenuTap(x: Float, y: Float) {
        when (state) {
            GState.TITLE -> switchState(GState.MISSION_SELECT)
            GState.MISSION_SELECT -> {
                for (i in 1..5) {
                    val r = missionRowRect(i)
                    if (y > r[1] && y < r[3] && x > r[0] && x < r[2]) {
                        if (i <= unlocked) loadMission(i)
                        return
                    }
                }
            }
            GState.BRIEFING -> launchMission()
            GState.PAUSED -> {
                when (menuButtonAt(x, y, 3)) {
                    0 -> switchState(GState.PLAYING)
                    1 -> mission?.let { m -> loadMission(m.id); launchMission() }
                    2 -> switchState(GState.MISSION_SELECT)
                }
            }
            GState.MISSION_COMPLETE -> {
                when (menuButtonAt(x, y, 2)) {
                    0 -> mission?.let { m -> if (m.id < 5) loadMission(m.id + 1) }
                    1 -> switchState(GState.MISSION_SELECT)
                    else -> { }
                }
            }
            GState.MISSION_FAILED -> {
                when (menuButtonAt(x, y, 2)) {
                    0 -> mission?.let { m -> loadMission(m.id); launchMission() }
                    1 -> switchState(GState.MISSION_SELECT)
                    else -> { }
                }
            }
            GState.VICTORY -> switchState(GState.MISSION_SELECT)
            GState.PLAYING -> { }
        }
    }

    /** Layout rect [left, top, right, bottom] for a mission-select row. */
    private fun missionRowRect(i: Int): FloatArray {
        val w = screenW; val h = screenH
        val rowH = h * 0.115f
        val top = h * 0.24f + (i - 1) * (rowH + h * 0.018f)
        return floatArrayOf(w * 0.18f, top, w * 0.82f, top + rowH)
    }

    /** Vertical stack of overlay buttons; returns index tapped or -1. */
    private fun menuButtonAt(x: Float, y: Float, count: Int): Int {
        val w = screenW; val h = screenH
        val bw = w * 0.26f; val bh = h * 0.1f
        for (i in 0 until count) {
            val top = h * 0.44f + i * (bh + h * 0.03f)
            if (x > w / 2f - bw / 2f && x < w / 2f + bw / 2f && y > top && y < top + bh) return i
        }
        return -1
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    fun draw(c: Canvas) {
        when (state) {
            GState.TITLE -> drawTitle(c)
            GState.MISSION_SELECT -> drawMissionSelect(c)
            GState.BRIEFING -> drawBriefing(c)
            GState.PLAYING, GState.PAUSED, GState.MISSION_COMPLETE,
            GState.MISSION_FAILED, GState.VICTORY -> {
                drawGameplay(c)
                when (state) {
                    GState.PAUSED -> drawPauseOverlay(c)
                    GState.MISSION_COMPLETE -> drawResultOverlay(c, true)
                    GState.MISSION_FAILED -> drawResultOverlay(c, false)
                    GState.VICTORY -> drawVictoryOverlay(c)
                    else -> { }
                }
            }
        }
    }

    private fun drawGameplay(c: Canvas) {
        world.drawBackground(c, camera, screenW, screenH, paint)
        c.save()
        camera.apply(c, screenW, screenH)
        world.drawWorld(c, paint, superman.xrayOn)
        mission?.drawExtras(c, paint)
        plane?.draw(c, paint)
        for (civ in civilians) civ.draw(c, paint)
        for (e in enemies) e.drawBase(c, paint, this)
        superman.draw(c, paint)
        for (p in projectiles) p.draw(c, paint)
        for (fx in effects) fx.draw(c, paint)
        c.restore()
        if (state == GState.PLAYING) hud.draw(c, paint, screenW, screenH)
    }

    private fun drawShieldCrest(c: Canvas, cx: Float, cy: Float, s: Float) {
        paint.style = Paint.Style.FILL
        paint.color = Pal.SUPER_RED
        val p = Path()
        p.moveTo(cx, cy - s)
        p.lineTo(cx + s * 1.15f, cy - s * 0.3f)
        p.lineTo(cx, cy + s * 1.4f)
        p.lineTo(cx - s * 1.15f, cy - s * 0.3f)
        p.close()
        c.drawPath(p, paint)
        paint.color = Pal.HUD_YELLOW
        val p2 = Path()
        p2.moveTo(cx, cy - s * 0.75f)
        p2.lineTo(cx + s * 0.85f, cy - s * 0.22f)
        p2.lineTo(cx, cy + s * 1.05f)
        p2.lineTo(cx - s * 0.85f, cy - s * 0.22f)
        p2.close()
        c.drawPath(p2, paint)
        paint.color = Pal.SUPER_RED
        val bolt = Path()
        bolt.moveTo(cx + s * 0.18f, cy - s * 0.6f)
        bolt.lineTo(cx - s * 0.3f, cy + s * 0.1f)
        bolt.lineTo(cx + s * 0.02f, cy + s * 0.1f)
        bolt.lineTo(cx - s * 0.16f, cy + s * 0.75f)
        bolt.lineTo(cx + s * 0.38f, cy - s * 0.05f)
        bolt.lineTo(cx + s * 0.05f, cy - s * 0.05f)
        bolt.close()
        c.drawPath(bolt, paint)
    }

    private fun drawTitle(c: Canvas) {
        val w = screenW; val h = screenH
        world.drawBackground(c, camera, w, h, paint)
        paint.style = Paint.Style.FILL
        drawShieldCrest(c, w / 2f, h * 0.30f, h * 0.11f)
        paint.color = Pal.HUD_TEXT
        paint.textAlign = Paint.Align.CENTER
        paint.textSize = h * 0.085f
        paint.isFakeBoldText = true
        c.drawText("LAST SON", w / 2f, h * 0.60f, paint)
        paint.textSize = h * 0.035f
        paint.isFakeBoldText = false
        paint.color = Pal.HUD_YELLOW
        c.drawText("A FAN-MADE SUPERMAN ACTION EXPERIMENT", w / 2f, h * 0.66f, paint)
        paint.color = Color.argb((150 + 105 * sin(uiT * 3f)).toInt().coerceIn(0, 255), 235, 240, 255)
        paint.textSize = h * 0.04f
        c.drawText("TAP TO BEGIN", w / 2f, h * 0.82f, paint)
        paint.textSize = h * 0.022f
        paint.color = Color.argb(140, 200, 205, 220)
        c.drawText("Personal, non-commercial project. Superman is a trademark of DC Comics.", w / 2f, h * 0.95f, paint)
        paint.textAlign = Paint.Align.LEFT
    }

    private val missionTitles = listOf("BAPTISM BY FIRE", "INTERGANG RISING",
        "GHOSTS IN THE MACHINE", "FLIGHT 236", "HEART OF KRYPTONITE")

    private fun drawMissionSelect(c: Canvas) {
        val w = screenW; val h = screenH
        world.drawBackground(c, camera, w, h, paint)
        paint.color = Pal.HUD_TEXT
        paint.textAlign = Paint.Align.CENTER
        paint.textSize = h * 0.06f
        paint.isFakeBoldText = true
        c.drawText("MISSIONS", w / 2f, h * 0.15f, paint)
        paint.isFakeBoldText = false
        for (i in 1..5) {
            val r = missionRowRect(i)
            val locked = i > unlocked
            paint.color = if (locked) Color.argb(110, 30, 35, 55) else Color.argb(190, 40, 55, 95)
            c.drawRoundRect(r[0], r[1], r[2], r[3], 18f, 18f, paint)
            paint.color = if (locked) Color.argb(120, 150, 155, 170) else Pal.HUD_TEXT
            paint.textSize = h * 0.038f
            c.drawText(
                if (locked) "MISSION $i - LOCKED" else "MISSION $i - ${missionTitles[i - 1]}",
                w / 2f, (r[1] + r[3]) / 2f + h * 0.013f, paint)
        }
        paint.textSize = h * 0.024f
        paint.color = Color.argb(150, 200, 205, 220)
        c.drawText("SCORE ${score}", w / 2f, h * 0.94f, paint)
        paint.textAlign = Paint.Align.LEFT
    }

    private fun drawBriefing(c: Canvas) {
        val w = screenW; val h = screenH
        world.drawBackground(c, camera, w, h, paint)
        val m = mission ?: return
        paint.textAlign = Paint.Align.CENTER
        paint.color = Pal.HUD_YELLOW
        paint.textSize = h * 0.03f
        c.drawText("MISSION ${m.id}", w / 2f, h * 0.16f, paint)
        paint.color = Pal.HUD_TEXT
        paint.textSize = h * 0.06f
        paint.isFakeBoldText = true
        c.drawText(m.title, w / 2f, h * 0.25f, paint)
        paint.isFakeBoldText = false
        paint.textSize = h * 0.032f
        var y = h * 0.36f
        for (line in m.briefing) {
            c.drawText(line, w / 2f, y, paint)
            y += h * 0.055f
        }
        paint.color = Color.argb((150 + 105 * sin(uiT * 3f)).toInt().coerceIn(0, 255), 255, 200, 40)
        paint.textSize = h * 0.038f
        c.drawText("TAP TO LAUNCH", w / 2f, h * 0.88f, paint)
        paint.textAlign = Paint.Align.LEFT
    }

    private fun drawOverlayButton(c: Canvas, index: Int, label: String) {
        val w = screenW; val h = screenH
        val bw = w * 0.26f; val bh = h * 0.1f
        val top = h * 0.44f + index * (bh + h * 0.03f)
        paint.color = Color.argb(210, 40, 55, 95)
        c.drawRoundRect(w / 2f - bw / 2f, top, w / 2f + bw / 2f, top + bh, 16f, 16f, paint)
        paint.color = Pal.HUD_TEXT
        paint.textSize = h * 0.036f
        paint.textAlign = Paint.Align.CENTER
        c.drawText(label, w / 2f, top + bh / 2f + h * 0.012f, paint)
        paint.textAlign = Paint.Align.LEFT
    }

    private fun dim(c: Canvas, alpha: Int = 150) {
        paint.color = Color.argb(alpha, 0, 0, 10)
        c.drawRect(0f, 0f, screenW, screenH, paint)
    }

    private fun drawPauseOverlay(c: Canvas) {
        dim(c)
        paint.color = Pal.HUD_TEXT
        paint.textAlign = Paint.Align.CENTER
        paint.textSize = screenH * 0.06f
        c.drawText("PAUSED", screenW / 2f, screenH * 0.3f, paint)
        paint.textAlign = Paint.Align.LEFT
        drawOverlayButton(c, 0, "RESUME")
        drawOverlayButton(c, 1, "RESTART MISSION")
        drawOverlayButton(c, 2, "QUIT TO MISSIONS")
    }

    private fun drawResultOverlay(c: Canvas, won: Boolean) {
        dim(c)
        paint.textAlign = Paint.Align.CENTER
        paint.textSize = screenH * 0.065f
        paint.isFakeBoldText = true
        if (won) {
            paint.color = Pal.HUD_YELLOW
            c.drawText("MISSION COMPLETE", screenW / 2f, screenH * 0.26f, paint)
        } else {
            paint.color = Pal.DANGER_RED
            c.drawText("MISSION FAILED", screenW / 2f, screenH * 0.26f, paint)
        }
        paint.isFakeBoldText = false
        paint.textSize = screenH * 0.032f
        paint.color = Pal.HUD_TEXT
        if (won) {
            c.drawText("Rescued: $rescues    Hostiles down: $kills    Score: $score",
                screenW / 2f, screenH * 0.35f, paint)
        } else {
            c.drawText(failReason.ifEmpty { "The mission was lost." }, screenW / 2f, screenH * 0.35f, paint)
        }
        paint.textAlign = Paint.Align.LEFT
        if (won) {
            drawOverlayButton(c, 0, if ((mission?.id ?: 5) < 5) "NEXT MISSION" else "CONTINUE")
            drawOverlayButton(c, 1, "MISSION SELECT")
        } else {
            drawOverlayButton(c, 0, "RETRY")
            drawOverlayButton(c, 1, "MISSION SELECT")
        }
    }

    private fun drawVictoryOverlay(c: Canvas) {
        dim(c, 190)
        drawShieldCrest(c, screenW / 2f, screenH * 0.24f, screenH * 0.08f)
        paint.textAlign = Paint.Align.CENTER
        paint.color = Pal.HUD_YELLOW
        paint.textSize = screenH * 0.07f
        paint.isFakeBoldText = true
        c.drawText("THE CITY IS SAFE", screenW / 2f, screenH * 0.48f, paint)
        paint.isFakeBoldText = false
        paint.color = Pal.HUD_TEXT
        paint.textSize = screenH * 0.032f
        c.drawText("Final score: $score", screenW / 2f, screenH * 0.56f, paint)
        c.drawText("So... turns out a Superman game CAN work.", screenW / 2f, screenH * 0.63f, paint)
        paint.color = Color.argb((150 + 105 * sin(uiT * 3f)).toInt().coerceIn(0, 255), 235, 240, 255)
        c.drawText("TAP TO RETURN", screenW / 2f, screenH * 0.8f, paint)
        paint.textAlign = Paint.Align.LEFT
    }
}
