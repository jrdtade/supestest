package com.fanproject.lastson

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Canvas
import android.view.MotionEvent
import android.view.SurfaceHolder
import android.view.SurfaceView

class GameView(context: Context) : SurfaceView(context), SurfaceHolder.Callback {

    private val game = Game(context)
    private var thread: GameThread? = null

    init {
        holder.addCallback(this)
        isFocusable = true
    }

    override fun surfaceCreated(holder: SurfaceHolder) {
        game.screenW = width.toFloat()
        game.screenH = height.toFloat()
        thread = GameThread(holder, game).also {
            it.running = true
            it.start()
        }
    }

    override fun surfaceChanged(holder: SurfaceHolder, format: Int, width: Int, height: Int) {
        game.screenW = width.toFloat()
        game.screenH = height.toFloat()
    }

    override fun surfaceDestroyed(holder: SurfaceHolder) {
        thread?.let {
            it.running = false
            try {
                it.join(1000)
            } catch (_: InterruptedException) {
            }
        }
        thread = null
    }

    @SuppressLint("ClickableViewAccessibility")
    override fun onTouchEvent(event: MotionEvent): Boolean {
        game.onTouch(event)
        return true
    }

    private class GameThread(
        private val holder: SurfaceHolder,
        private val game: Game
    ) : Thread("GameLoop") {
        @Volatile var running = false

        override fun run() {
            var last = System.nanoTime()
            while (running) {
                val now = System.nanoTime()
                // Clamp dt so a hitch never turns into a physics explosion.
                val dt = clampf((now - last) / 1_000_000_000f, 0f, 1f / 30f)
                last = now

                game.update(dt)

                var canvas: Canvas? = null
                try {
                    canvas = holder.lockCanvas()
                    if (canvas != null) {
                        synchronized(holder) {
                            game.draw(canvas)
                        }
                    }
                } finally {
                    if (canvas != null) {
                        try {
                            holder.unlockCanvasAndPost(canvas)
                        } catch (_: IllegalStateException) {
                        }
                    }
                }

                // Cap around ~60fps to avoid spinning the CPU flat out.
                val frameNs = System.nanoTime() - now
                val sleepMs = (16_666_666L - frameNs) / 1_000_000L
                if (sleepMs > 0) {
                    try {
                        sleep(sleepMs)
                    } catch (_: InterruptedException) {
                    }
                }
            }
        }
    }
}
