package com.example.wargamesimulatorpro

import android.app.Activity
import android.app.AlertDialog
import android.content.Context
import android.content.Intent
import android.media.MediaPlayer
import android.view.*
import android.widget.TextView
import android.widget.Toast
import android.graphics.Color
import android.widget.Button
import com.example.wargamesimulatorpro.BattleActivity.Companion.battleJob
import com.example.wargamesimulatorpro.BattleActivity.Companion.fakeInchPx
import com.example.wargamesimulatorpro.MusicPlayer.stopMusic
import kotlinx.coroutines.CancellableContinuation
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import java.util.Locale
import kotlin.coroutines.resume
import kotlin.math.*


suspend fun activateAlienTerror(
    theContext: Context,
    super1: SuperPlayer,  // Active player
    inactivePlayer: SuperPlayer // Inactive player
) = suspendCancellableCoroutine { continuation ->
    createConfirmationDialog(theContext, "Would you like to activate Alien Terror?")
    { result ->
        if (result)
        {  // Loop through all squads in game1 and apply shellshock test
            super1.deployed.forEach { activeSquad ->
                activeSquad.forces.shellShock = shellShockTest(theContext, activeSquad, inactivePlayer, super1.thePlayer.playerAbilities)
            }
            inactivePlayer.thePlayer.playerAbilities.remove("Alien Terror")  // Remove Alien Terror from inactive player after use
            playSound(theContext, R.raw.screech) // Play screech sound effect
        }
        continuation.resume(Unit)   // Resume coroutine execution
    }
}

suspend fun moveStuff(
    activeGameUnit: GameUnit,
    enemyUnits: List<GameUnit>,
    moveDistance: Float
) = suspendCancellableCoroutine { continuation ->
    val screenWidth = rootLayout.width.toFloat()
    val screenHeight = rootLayout.height.toFloat()
    removeAllListeners(activeGameUnit) // 🔹 Remove listeners before movement starts
    activeGameUnit.physicalTroops.forEach { gameModel ->
        val circle = gameModel.view
        gameModel.healthText.setTextColor(Color.MAGENTA)
        circle.setOnTouchListener(object : View.OnTouchListener
        {
            var startX = 0f
            var startY = 0f
            var dX = 0f
            var dY = 0f
            override fun onTouch(view: View, event: MotionEvent): Boolean
            {
                view.performClick()
                when (event.action)
                {
                    MotionEvent.ACTION_DOWN ->
                        {
                        dX = event.rawX - view.x
                        dY = event.rawY - view.y
                        startX = view.x
                        startY = view.y
                    }
                    MotionEvent.ACTION_MOVE ->
                        {
                        val newX = event.rawX - dX
                        val newY = event.rawY - dY
                        val deltaX = newX - view.x
                        val deltaY = newY - view.y
                        val activeBoundingBox = calculateBoundingBox(activeGameUnit, false).apply {
                            offset(deltaX, deltaY)
                        }
                        val doesAvoidCollision = enemyUnits.none { enemyUnit ->
                            checkCollisionSAT(activeBoundingBox, calculateBoundingBox(enemyUnit, false))
                        } // Check collision with all enemy units
                        val isWithinScreenBounds = activeGameUnit.physicalTroops.all {
                            val futureX = it.view.x + deltaX
                            val futureY = it.view.y + deltaY
                            futureX >= 0 && futureX + it.view.width <= screenWidth &&
                                    futureY >= 0 && futureY + it.view.height <= screenHeight
                        }
                        val totalMoveDistance =
                            sqrt((newX - startX).pow(2) + (newY - startY).pow(2)) / fakeInchPx
                        if (isWithinScreenBounds && doesAvoidCollision && totalMoveDistance <= moveDistance)
                        {
                            moveGameUnit(activeGameUnit, deltaX, deltaY)
                            distanceTextView.text = String.format(Locale.US, "Distance Moved: %.2f", totalMoveDistance)
                        }
                    }
                    MotionEvent.ACTION_UP ->
                        {
                        activeGameUnit.physicalTroops.forEach { it.view.setOnTouchListener(null) }
                        activeGameUnit.physicalTroops.forEach { model ->
                            updateHealthText(model)
                        }
                        moveRotate(activeGameUnit, startX, startY, circle.x, circle.y)
                        continuation.resume(Unit)
                    }
                }
                return true
            }
        })
    }
}


suspend fun aircraftMoveStuff(
    theContext: Context,
    activeGameUnit: GameUnit,
    enemyUnits: List<GameUnit>
) = suspendCancellableCoroutine { continuation ->
    val minMoveDistance = 20f // Minimum movement distance in pixels
    val screenWidth = rootLayout.width.toFloat()
    val screenHeight = rootLayout.height.toFloat()

    activeGameUnit.physicalTroops.forEach { gameModel ->
        val circle = gameModel.view
        gameModel.healthText.setTextColor(Color.MAGENTA)
        circle.setOnTouchListener(object : View.OnTouchListener {
            var startX = 0f
            var startY = 0f
            var dX = 0f
            var dY = 0f
            var totalMoveDistance = 0f

            override fun onTouch(view: View, event: MotionEvent): Boolean {
                view.performClick()  // Accessibility improvement
                when (event.action) {
                    MotionEvent.ACTION_DOWN -> {
                        dX = event.rawX - view.x
                        dY = event.rawY - view.y
                        startX = view.x
                        startY = view.y
                    }
                    MotionEvent.ACTION_MOVE -> {
                        val newX = event.rawX - dX
                        val newY = event.rawY - dY
                        val deltaX = newX - view.x
                        val deltaY = newY - view.y

                        val activeBoundingBox = calculateBoundingBox(activeGameUnit, false).apply {
                            offset(deltaX, deltaY)
                        }

                        // Check collision with all enemy units
                        val doesAvoidCollision = enemyUnits.none { enemyUnit ->
                            checkCollisionSAT(activeBoundingBox, calculateBoundingBox(enemyUnit, false))
                        }

                        val isWithinScreenBounds = activeGameUnit.physicalTroops.all {
                            val futureX = it.view.x + deltaX
                            val futureY = it.view.y + deltaY
                            futureX >= 0 && futureX + it.view.width <= screenWidth &&
                                    futureY >= 0 && futureY + it.view.height <= screenHeight
                        }

                        if (isWithinScreenBounds && doesAvoidCollision) {
                            totalMoveDistance = sqrt((newX - startX).pow(2) + (newY - startY).pow(2)) / fakeInchPx
                            moveGameUnit(activeGameUnit, deltaX, deltaY)
                            distanceTextView.text = String.format(Locale.US, "Distance Moved: %.2f", totalMoveDistance)
                        }
                    }
                    MotionEvent.ACTION_UP -> {
                        if (totalMoveDistance < minMoveDistance) {
                            moveGameUnit(activeGameUnit, startX - circle.x, startY - circle.y)
                            moveRotate(activeGameUnit, startX, startY, circle.x, circle.y)
                            Toast.makeText(
                                theContext,
                                "Aircraft must move at least 20 inches!",
                                Toast.LENGTH_SHORT
                            ).show()
                        } else {
                            playSound(theContext, R.raw.jetfly)
                            activeGameUnit.physicalTroops.forEach { model ->
                                updateHealthText(model)
                            }
                            activeGameUnit.physicalTroops.forEach { it.view.setOnTouchListener(null) }
                            continuation.resume(Unit)
                        }
                    }
                }
                return true
            }
        })
    }
}


suspend fun checkVictory(
    theContext: Context,
) = suspendCancellableCoroutine { continuation ->
    if (playerOne.deployed.isEmpty() || playerTwo.deployed.isEmpty())
    {
        val winner = if (playerOne.deployed.isEmpty()) playerTwo.thePlayer.playerName else playerOne.thePlayer.playerName // Determine winner
        stopMusic()
        val dialog = AlertDialog.Builder(theContext).create().apply {
            val messageView = TextView(theContext).apply {
                text = theContext.getString(R.string.game_over_message, winner) // Use placeholder
                textSize = 32f // Check if a squad is empty
                setPadding(50, 50, 50, 50)
                textAlignment = TextView.TEXT_ALIGNMENT_CENTER
            }
            setView(messageView)
            setCancelable(true) // Allow dialog to be dismissed or cancelled
        }
        val mediaPlayer = MediaPlayer.create(theContext, R.raw.victory).apply {
            setOnCompletionListener {
                if (dialog.isShowing) dialog.dismiss() // Close the dialog
                battleJob?.cancel()  // Cancel the battle coroutine
                battleJob = null // Nullify the reference to avoid memory leaks
                val intent = Intent(theContext, MainActivity::class.java)
                theContext.startActivity(intent) // Navigate back to MainActivity
            }
        }
        dialog.setOnDismissListener {
            if (mediaPlayer.isPlaying) mediaPlayer.stop()
            mediaPlayer.release()         // Handle dialog dismiss or cancel
            resumeContinuationSafe(continuation)
            navigateToMain(theContext)
        }
        dialog.setOnCancelListener {
            if (mediaPlayer.isPlaying) mediaPlayer.stop()
            mediaPlayer.release()
            resumeContinuationSafe(continuation)
            navigateToMain(theContext)
        }
        dialog.show()
        mediaPlayer.start()         // Show the dialog and start the victory sound
    } else
        continuation.resume(Unit) // No victory detected
}

private fun resumeContinuationSafe(continuation: CancellableContinuation<Unit>)
{
    if (continuation.isActive)
        continuation.resume(Unit)
}

private fun navigateToMain(context: Context)
{
    val intent = Intent(context, MainActivity::class.java).apply {
        flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_NEW_TASK
    }
    context.startActivity(intent)
    if (context is Activity) context.finish() // Finish current activity
}

suspend fun teleport(theContext: Context, activeGameUnit: GameUnit, enemyUnits: List<GameUnit>) = suspendCancellableCoroutine { continuation ->
    val screenWidth = rootLayout.width.toFloat()
    val screenHeight = rootLayout.height.toFloat()

    activeGameUnit.physicalTroops.forEach { gameModel ->
        gameModel.healthText.setTextColor(Color.MAGENTA)
        val circle = gameModel.view
        circle.setOnTouchListener(object : View.OnTouchListener {
            var dX = 0f
            var dY = 0f

            override fun onTouch(view: View, event: MotionEvent): Boolean {
                view.performClick()  // Accessibility improvement
                when (event.action) {
                    MotionEvent.ACTION_DOWN -> {
                        dX = event.rawX - view.x
                        dY = event.rawY - view.y
                    }
                    MotionEvent.ACTION_MOVE -> {
                        val newX = event.rawX - dX
                        val newY = event.rawY - dY
                        val deltaX = newX - view.x
                        val deltaY = newY - view.y

                        val activeBoundingBox = calculateBoundingBox(activeGameUnit, true).apply {
                            offset(deltaX, deltaY)
                        }

                        // Check collision with all enemy units
                        val doesAvoidForbiddenBoundary = enemyUnits.none { enemyUnit ->
                            checkCollisionSAT(activeBoundingBox, calculateBoundingBox(enemyUnit, true))
                        }

                        val isWithinScreenBounds = activeGameUnit.physicalTroops.all {
                            val futureX = it.view.x + deltaX
                            val futureY = it.view.y + deltaY
                            futureX >= 0 && futureX + it.view.width <= screenWidth &&
                                    futureY >= 0 && futureY + it.view.height <= screenHeight
                        }

                        if (isWithinScreenBounds && doesAvoidForbiddenBoundary) {
                            moveGameUnit(activeGameUnit, deltaX, deltaY)
                        }
                    }
                    MotionEvent.ACTION_UP -> {
                        activeGameUnit.physicalTroops.forEach { model ->
                            updateHealthText(model)
                        }
                        activeGameUnit.physicalTroops.forEach { it.view.setOnTouchListener(null) }
                        continuation.resume(Unit)
                        playSound(theContext, R.raw.moved)
                    }
                }
                return true
            }
        })
    }
}

suspend fun ContinueGame(context: Context) = coroutineScope {
    val nextPhaseButton: Button = (context as Activity).findViewById(R.id.next_phase_button)
    nextPhaseButton.isEnabled = true // Enable the button
    val timeoutJob = launch {
        delay(30000) // Auto-continue after 30 seconds
        return@launch
    }
    nextPhaseButton.setOnClickListener {
        nextPhaseButton.isEnabled = false // Prevent multiple presses
        timeoutJob.cancel() // Cancel the auto-continue
        return@setOnClickListener
    }
}
