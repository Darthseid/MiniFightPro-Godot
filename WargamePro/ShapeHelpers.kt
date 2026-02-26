package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.content.Context
import android.graphics.Color
import android.view.*
import android.widget.*
import kotlin.math.*
import androidx.core.content.res.ResourcesCompat
import com.example.wargamesimulatorpro.BattleActivity.Companion.fakeInchPx
import java.util.Locale
import kotlin.random.Random

fun calculateEdgeDistance(x1: Float, y1: Float, width1: Int, height1: Int, x2: Float, y2: Float, width2: Int, height2: Int): Float
{
    val right1 = x1 + width1
    val bottom1 = y1 + height1
    val right2 = x2 + width2
    val bottom2 = y2 + height2  // Calculate the right and bottom edges for both objects

    val dx = when // Determine the horizontal distance between the edges
    {
        right1 < x2 -> x2 - right1  // Object 1 is to the left of Object 2
        right2 < x1 -> x1 - right2  // Object 1 is to the right of Object 2
        else -> 0f  // Objects overlap horizontally
    }
    val dy = when     // Determine the vertical distance between the edges
    {
        bottom1 < y2 -> y2 - bottom1  // Object 1 is above Object 2
        bottom2 < y1 -> y1 - bottom2  // Object 1 is below Object 2
        else -> 0f  // Objects overlap vertically
    }
    return sqrt(dx.pow(2) + dy.pow(2))    // Calculate and return the Euclidean distance between the closest edges
}

fun calculateClosestDistanceBetweenUnits(unit1: GameUnit, unit2: GameUnit): Float
{
    var minDistance = Float.MAX_VALUE  // Initialize with a large value
    for (gameModel1 in unit1.physicalTroops)  // Iterate through all models in unit1
    {
        for (gameModel2 in unit2.physicalTroops)  // Compare each model in unit1 with all models in unit2
        {
            val distance = calculateEdgeDistance(
                gameModel1.view.x, gameModel1.view.y, gameModel1.view.width, gameModel1.view.height,
                gameModel2.view.x, gameModel2.view.y, gameModel2.view.width, gameModel2.view.height  // Calculate the edge-to-edge distance between models
            )
            if (distance < minDistance)  // Update the minimum distance if a closer model is found
                minDistance = distance
        }
    }
    return minDistance / fakeInchPx   // Convert pixel distance to in-game measurement and return
}

fun findClosestModel(targetUnit: GameUnit, activeUnit: GameUnit): View
{
    var closestCircle = targetUnit.physicalTroops[0].view  // Assume the first model is closest initially
    var minDistance = Float.MAX_VALUE  // Start with a high value
    for (target in targetUnit.physicalTroops)   // Iterate through all models in targetUnit
    {
        for (active in activeUnit.physicalTroops)  // Compare each model in targetUnit with all models in activeUnit
        {
            val distance = calculateEdgeDistance(
                active.view.x, active.view.y, active.view.width, active.view.height,
                target.view.x, target.view.y, target.view.width, target.view.height // Calculate the edge-to-edge distance
            )
            if (distance < minDistance)
            {
                minDistance = distance  // If this model is the closest so far, update the reference
                closestCircle = target.view
            }
        }
    }
    return closestCircle // Return the closest model's view
}

fun moveGameUnit(activeGameUnit: GameUnit, deltaX: Float, deltaY: Float)
{
    activeGameUnit.physicalTroops.forEach { gameModel ->
        gameModel.view.animate()
            .x(gameModel.view.x + deltaX)
            .y(gameModel.view.y + deltaY)
            .setDuration(0)
            .start()
        gameModel.healthText.let { healthBar ->
            healthBar.animate()
                .x(healthBar.x + deltaX)
                .y(healthBar.y + deltaY)
                .setDuration(0)
                .start()
        }
    }
}

fun moveGameUnitCharge(activeGameUnit: GameUnit, deltaX: Float, deltaY: Float)
{
    activeGameUnit.physicalTroops.forEach { gameModel ->
        gameModel.view.animate()
            .x(gameModel.view.x + deltaX)
            .y(gameModel.view.y + deltaY)
            .setDuration(400)
            .start()
        gameModel.healthText.let { healthBar ->
            healthBar.animate()
                .x(healthBar.x + deltaX)
                .y(healthBar.y + deltaY)
                .setDuration(400)
                .start()
        }
    }
}

fun removeModelFromUnit(unit: GameUnit, deadGuy: GameModel, context: Context)
{
    val playSoundChance = Random.nextInt(100) < 20 // 20% chance to play sound
    if (playSoundChance)
        playSound(context, R.raw.wilhelm_scream) // Ensure Wilhelm_Scream.ogg is in res/raw
    val deadView = deadGuy.view
    rootLayout.removeView(deadView)  // Remove the view and health bar from the layout
        deadGuy.healthText.let { rootLayout.removeView(it) }
        unit.physicalTroops.remove(deadGuy) // Remove the GameModel from the unit's squad
        deadView.animate() // Animate the removal of the view
            .alpha(0f)
            .setDuration(500)
            .withEndAction {
               deadView.visibility = View.GONE
            }
}

fun updateHealthText(hurtGuy: GameModel)
{
    val hurtModel = hurtGuy.model
    val currentHealth = hurtModel.health
    val maxHealth = hurtModel.startingHealth
    val bracket = hurtModel.bracketed
    val healthText = hurtGuy.healthText
    healthText.text = String.format(Locale.US,"%d", hurtModel.health)
    val percentage = currentHealth * 100f / maxHealth
    healthText.setTextColor(
            when {
                percentage >= 66f -> Color.GREEN
                percentage < 66 && currentHealth > bracket -> Color.YELLOW
                else -> Color.RED
            }
        )
}

fun updateHealthText(gameUnit: GameUnit)
{
    gameUnit.physicalTroops.forEach { gameModel ->
        updateHealthText(gameModel) // Calls the single-model version
    }
}

fun markGameUnitPurple(gameUnit: GameUnit)
{
    gameUnit.physicalTroops.forEach { gameModel ->
        gameModel.healthText.setTextColor(Color.MAGENTA) // Calls the single-model version
    }
}

fun createGameUnit(dataUnit: Squad, laidOut: ViewGroup, drawableResId: Int, theContext: Context, initialX: Float, initialY: Float): GameUnit
{
    val gameModels = mutableListOf<GameModel>() // Store GameModels
    val count = dataUnit.startingModelSize
    val rowCount = sqrt(count.toDouble()).toInt()
    val colCount = if (rowCount == 0) 0 else ceil(count.toDouble() / rowCount).toInt()
    var leftMargin = initialX.toInt()
    var topMargin = initialY.toInt()
    var baseSize = when
    {
        "Infantry" in dataUnit.squadType -> (4f*fakeInchPx).toInt()
        "Mounted" in dataUnit.squadType -> (4.3f*fakeInchPx).toInt()
        "Character" in dataUnit.squadType -> (4.6f*fakeInchPx).toInt()
        "Vehicle" in dataUnit.squadType -> (4.9f*fakeInchPx).toInt()
        "Monster" in dataUnit.squadType -> (5.2f*fakeInchPx).toInt()
        "Fortification" in dataUnit.squadType -> (5.5f*fakeInchPx).toInt()
        else -> (4.15f*fakeInchPx).toInt() // Default size
    }
    if (dataUnit.squadType.contains("Titanic"))
        baseSize *= 2
    for (i in 0 until count) // Create the visual representation of the model
    {
        val circle = View(theContext).apply {
            layoutParams = ViewGroup.MarginLayoutParams(baseSize, baseSize).apply {
                this.leftMargin = leftMargin
                this.topMargin = topMargin
            }
            background = ResourcesCompat.getDrawable(theContext.resources, drawableResId, null)
        }
        laidOut.addView(circle)
        val model = dataUnit.composition.getOrNull(i % dataUnit.composition.size) ?: continue // Get the logical model
        val healthTextView = TextView(theContext).apply {
            layoutParams = ViewGroup.MarginLayoutParams(baseSize, ViewGroup.LayoutParams.WRAP_CONTENT).apply {
                this.leftMargin = leftMargin
                this.topMargin = topMargin + baseSize + 5 // Position health text below the model
            }
            text = String.format(Locale.US,"%d", model.startingHealth)
            textSize = 11f
            textAlignment = TextView.TEXT_ALIGNMENT_CENTER
            setTextColor(Color.GREEN) // Default to green
        }
        laidOut.addView(healthTextView)
        val newGuy = GameModel(model, circle, healthTextView) // Add the GameModel to the list
        gameModels.add(newGuy)
        updateHealthText(newGuy) // Update health bar appearance initially
        leftMargin += baseSize + 10
        if ((i + 1) % colCount == 0)
        {
            leftMargin = initialX.toInt()
            topMargin += baseSize + 20
        }
    }
    val motion = MoveVars(false, false, false)
    return GameUnit(gameModels, dataUnit, motion)
}

fun createConfirmationDialog(context: Context, alertText: String, onResult: (Boolean) -> Unit)
{
    AlertDialog.Builder(context)
        .setMessage(alertText)
        .setCancelable(false) // Prevent dialog from being dismissed without interaction
        .setPositiveButton("Yes") { _, _ ->
            onResult(true) // Return true if "Yes" is pressed
        }
        .setNegativeButton("No") { _, _ ->
            onResult(false) // Return false if "No" is pressed
        }
        .create()
        .show()
}

fun checkCollisionSAT(boxA: BoundingBox, boxB: BoundingBox): Boolean
{
    val axes = listOf(Pair(1f, 0f), Pair(0f, 1f)) // Check collision along X & Y axes
    for (axis in axes)
    {
        val projectionA = projectBoxOntoAxis(boxA, axis) // Project Box A onto the current axis
        val projectionB = projectBoxOntoAxis(boxB, axis) // Project Box B onto the current axis
        if (projectionA.second < projectionB.first || projectionB.second < projectionA.first)
            return false   // If there is no overlap between the projections, the boxes do not collide
    }
    return true     // If projections overlap on all tested axes, the boxes are colliding
}

fun projectBoxOntoAxis(box: BoundingBox, axis: Pair<Float, Float>): Pair<Float, Float>
{  // Compute projections of the bounding box corners onto the given axis
    val corners = listOf(
        Pair(box.left, box.top),      // Top-left corner
        Pair(box.right, box.top),     // Top-right corner
        Pair(box.left, box.bottom),   // Bottom-left corner
        Pair(box.right, box.bottom)   // Bottom-right corner
    )
    val projections = corners.map { corner ->
        corner.first * axis.first + corner.second * axis.second
    }  // Compute dot products to determine the projections on the axis
    return Pair(projections.minOrNull()!!, projections.maxOrNull()!!)     // Return the min and max projections (range of the bounding box on the axis)
}

fun calculateBoundingBox(unit: GameUnit, isTeleport: Boolean): BoundingBox
{  // Adjust bounding box size depending on whether the unit is teleporting
    val inchDivisor = if (isTeleport) 2f / 6f else 2f  // Teleporting increases the bounding box size
    val views = unit.physicalTroops.map { it.view }  // Get all model views belonging to the unit

    val left = views.minOf { it.x } - fakeInchPx / inchDivisor
    val top = views.minOf { it.y } - fakeInchPx / inchDivisor   // Compute the bounding box by finding the min and max positions of all unit models
    val right = views.maxOf { it.x + it.width } + fakeInchPx / inchDivisor
    val bottom = views.maxOf { it.y + it.height } + fakeInchPx / inchDivisor

    return BoundingBox(left, top, right, bottom)  // Return the computed bounding box
}


fun View.duplicate(context: Context): View // Creates a duplicate of a View with the same layout parameters and background
{
    val newView = View(context) // Create a new View instance
    newView.layoutParams = this.layoutParams // Copy layout parameters
    newView.background = this.background // Copy background
    return newView // Return the duplicated View
}


fun TextView.duplicateTextView(context: Context): TextView // Creates a duplicate of a TextView while maintaining its style and properties
{
    return TextView(context).apply {
        layoutParams = this@duplicateTextView.layoutParams // Copy layout parameters
        text = this@duplicateTextView.text // Copy text content

        val fontScale = context.resources.configuration.fontScale
        textSize = this@duplicateTextView.textSize / fontScale         // Adjust text size based on device font scale to maintain consistency
        setTextColor(this@duplicateTextView.currentTextColor) // Copy text color
        gravity = this@duplicateTextView.gravity // Copy gravity settings


        setPadding(
            this@duplicateTextView.paddingLeft,
            this@duplicateTextView.paddingTop,
            this@duplicateTextView.paddingRight,         // Copy padding values from the original TextView
            this@duplicateTextView.paddingBottom
        )

        typeface = this@duplicateTextView.typeface // Copy typeface/font style
        background = this@duplicateTextView.background // Copy background
    }
}

fun moveRotate(gameUnit: GameUnit, oldX: Float, oldY: Float, newX: Float, newY: Float) //This is for rotating towards the point moved. Used during Movement Phase.
{
    val angle = atan2(newY - oldY, newX - oldX) // Convert angle to degrees for easier manipulation
    val angleInDegrees = Math.toDegrees(angle.toDouble()).toFloat()   // Calculate the angle of movement in radians
    gameUnit.physicalTroops.forEach { gameModel ->
        val view = gameModel.view
        val boundary = 90f // Half of 180°.
        if (angleInDegrees > boundary || angleInDegrees < -boundary)  // Reflect the view across the y-axis with animation
        {
            view.animate()
                .scaleX(-1f) // Reflect horizontally
                .rotation(if (angleInDegrees > 0) (2f*boundary) + angleInDegrees else (-2f*boundary) + angleInDegrees)
                .setDuration(700) // Smooth animation duration
                .start()
        } else
        {
            view.animate() // Rotate normally without reflection
                .scaleX(1f) // Ensure no reflection
                .rotation(angleInDegrees)
                .setDuration(500) // Smooth animation duration
                .start()
        }
    }
}

fun moveRotate(activeUnit: GameUnit, enemyUnit: GameUnit) //This is for rotating towards enemy. Used during Shoot, Charge, and Fight Phase.
{
    val activeCenterX = activeUnit.physicalTroops.map { it.view.x }.average().toFloat()
    val activeCenterY = activeUnit.physicalTroops.map { it.view.y }.average().toFloat()
    val enemyCenterX = enemyUnit.physicalTroops.map { it.view.x }.average().toFloat()     // Calculate the center of the active unit
    val enemyCenterY = enemyUnit.physicalTroops.map { it.view.y }.average().toFloat()     // Calculate the center of the enemy unit
    val angle = atan2(
        enemyCenterY - activeCenterY,
        enemyCenterX - activeCenterX // Calculate the angle between the two centers
    )
    val angleInDegrees = Math.toDegrees(angle.toDouble()).toFloat()  // Rotate each model in the active unit
    activeUnit.physicalTroops.forEach { gameModel ->
        val view = gameModel.view
        val boundary = 90f  // Reflect the view across the y-axis with animation
        if (angleInDegrees > boundary || angleInDegrees < -boundary)
        {
            view.animate()
                .scaleX(-1f) // Reflect horizontally
                .rotation(if (angleInDegrees > 0) (2f * boundary) + angleInDegrees else (-2f * boundary) + angleInDegrees)
                .setDuration(700) // Smooth animation duration
                .start()
        } else
        {
            view.animate()  // Rotate normally without reflection
                .scaleX(1f) // Ensure no reflection
                .rotation(angleInDegrees)
                .setDuration(500) // Smooth animation duration
                .start()
        }
    }
}


fun coherency(gameUnit: GameUnit)
{
    val models = gameUnit.physicalTroops
    if (models.size < 2) return // Coherency isn't needed for 0 or 1 model
    val unitCenterX = models.map { it.view.x }.average().toFloat()
    val unitCenterY = models.map { it.view.y }.average().toFloat()
    val maxDistanceFromCenter = fakeInchPx * 0.22f // Calculate the maximum distance allowed from the center
    models.forEach { modelA ->
        val isWithinCoherency = models.any { modelB ->
            val isWithinOneInch = calculateDistanceCoherency(modelA, modelB) <= fakeInchPx
            modelA != modelB && isWithinOneInch // Check if modelA is within one fake inch of any other model
        }
        val isWithinMaxDistance = if (models.size < 6) true else calculateDistanceFromCenter(modelA, unitCenterX, unitCenterY) <= maxDistanceFromCenter
        if (!isWithinCoherency || !isWithinMaxDistance)
        {
            val nearestModel = models // Find the nearest model
                .filter { it != modelA }
                .minByOrNull { modelB -> calculateDistanceCoherency(modelA, modelB) }
            nearestModel?.let { modelB ->
                val distanceToNearest = calculateDistanceCoherency(modelA, modelB)
                val distanceToCenter = calculateDistanceFromCenter(modelA, unitCenterX, unitCenterY)
                val moveTargetX: Float
                val moveTargetY: Float
                if (!isWithinCoherency && distanceToNearest > fakeInchPx)
                {  // Move closer to the nearest model to maintain coherency
                    val angleToNearest = atan2(modelB.view.y - modelA.view.y, modelB.view.x - modelA.view.x)
                    val moveDistance = distanceToNearest - fakeInchPx
                    moveTargetX = modelA.view.x + moveDistance * cos(angleToNearest)
                    moveTargetY = modelA.view.y + moveDistance * sin(angleToNearest)
                } else if (!isWithinMaxDistance && distanceToCenter > maxDistanceFromCenter)
                {
                    val angleToCenter = atan2(unitCenterY - modelA.view.y, unitCenterX - modelA.view.x)
                    val moveDistance = distanceToCenter - maxDistanceFromCenter // Move closer to the unit center to stay within bounds
                    moveTargetX = modelA.view.x + moveDistance * cos(angleToCenter)
                    moveTargetY = modelA.view.y + moveDistance * sin(angleToCenter)
                } else
                    return@let
                modelA.view.animate()
                    .x(moveTargetX)
                    .y(moveTargetY)       // Animate movement of the model and its health text
                    .setDuration(600) // Smooth animation
                    .start()
                modelA.healthText.animate()
                    .x(moveTargetX) // Animate healthText to follow the model
                    .y(moveTargetY - 20) // Offset above the model
                    .setDuration(600)
                    .start()
            }
        }
    }
}

private fun calculateDistanceFromCenter(model: GameModel, centerX: Float, centerY: Float): Float
{
    return sqrt((model.view.x - centerX).pow(2) + (model.view.y - centerY).pow(2))
}
private fun calculateDistanceCoherency(modelA: GameModel, modelB: GameModel): Float
{ // Helper function to calculate distance between two models
    return sqrt((modelA.view.x - modelB.view.x).pow(2) + (modelA.view.y - modelB.view.y).pow(2))
}

fun checkFightRange(activeUnit: GameUnit, enemyUnits: List<GameUnit>, radius: Float): Boolean
{
    for (enemyUnit in enemyUnits)   // If SAT doesn't detect a collision, check closest model-to-model distance
    {
        if (calculateClosestDistanceBetweenUnits(activeUnit, enemyUnit) <= radius)
            return true // An enemy is within 1 fake inch
    }
    return false // No enemy units in range
}

fun canCharge(activeUnit: GameUnit, enemyUnits: List<GameUnit>): Boolean
{
   val examined = activeUnit.movable
   val examined2 = activeUnit.forces
    if(checkFightRange(activeUnit, enemyUnits, 1f))
        return false
    if (examined.retreat || examined.advance)
        return false
    if (examined2.squadType.contains("Fortification") || examined2.movement <= 0f)
        return false
    if (examined2.squadType.contains("Aircraft"))
        return false
    return true // No enemy units in range
}

fun removeAllListeners(gameUnit: GameUnit)  // Helper function to remove touch listeners
{
    gameUnit.physicalTroops.forEach { model ->
        model.view.setOnTouchListener(null) // Remove listener
        model.view.setOnClickListener(null) // Touch and Click listeners are different.
    }
}

fun removeAllListeners(aPlayer: SuperPlayer)  // Helper function to remove touch listeners
{
   val entireSide = aPlayer.deployed
    entireSide.forEach { aUnit ->
        removeAllListeners(aUnit) //This uses the previous removal function.
        }
}

