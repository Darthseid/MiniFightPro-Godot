package com.example.wargamesimulatorpro


import android.content.*
import android.view.ViewGroup
import android.widget.TextView
import com.example.wargamesimulatorpro.SquadAbilities.selfRessurection
import kotlin.random.Random

fun simpleRoll(maximum: Int): Int
{ return Random.nextInt(1, maximum + 1) }
fun reRollOnes(): Int {
    var result = Random.nextInt(1, 6)
    if (result == 1)
        result = Random.nextInt(1, 6)
    return result
}

fun reRollFailed(skill: Int): Int {
    var result = Random.nextInt(1, 6)
    if (result < skill)
        result = Random.nextInt(1, 6)
    return result
}
fun Roll2d6(): Int
{
    return simpleRoll(6) + simpleRoll(6)
}

fun removeDeadModels(
    gameUnit: GameUnit, // Updated to use GameUnit for integrated functionality
    theContext: Context
) {
    val initialCount = gameUnit.forces.composition.size // Check for self-resurrection ability
    val risenModel = if (initialCount > 0) gameUnit.forces.composition[0] else null
    if (risenModel != null)
    {
        if (gameUnit.forces.squadAbilities.any { it == selfRessurection } && risenModel.health <= 0)
        {
            if (simpleRoll(6) > 1)
            {
                playSound(theContext, R.raw.resurrection)
                risenModel.health = risenModel.startingHealth
                gameUnit.forces.squadAbilities.removeIf { it == selfRessurection }
            }
            gameUnit.forces.squadAbilities.removeIf { it == selfRessurection }
        }
    }
    val iterator = gameUnit.forces.composition.iterator()     // Iterate through dead models and remove corresponding circles
    while (iterator.hasNext())
    {
        val model = iterator.next()
        if (model.health <= 0)
        {
            val indexToRemove = gameUnit.forces.composition.indexOf(model)
            if (indexToRemove >= 0 && indexToRemove < gameUnit.physicalTroops.size)
            {
                val galToRemove = gameUnit.physicalTroops[indexToRemove]
                removeModelFromUnit(gameUnit, galToRemove, theContext)
            }
            iterator.remove() // Remove model from composition
            coherency(gameUnit)
        }
    }
}

fun explosionProcess(theContext: Context, explodingUnit: GameUnit, demiseCheck: Int)
{
    var explosionCount = 0 // Initial rolls for demise
    repeat(demiseCheck) {
        if (simpleRoll(6) == 1) // Each roll of 1 causes an explosion
            explosionCount++
    }
    while (explosionCount > 0)
    {
        val explodeDamage = explodingUnit.forces.squadAbilities.find { it.internal == "Explodes" }?.Modifier ?: 1
        playSound(theContext, R.raw.explodes) // Play explosion sound
        val affectedUnits1 = playerOne.deployed.filter { unit ->
            calculateClosestDistanceBetweenUnits(explodingUnit, unit) <= 6f // Count cascading explosions
        }
        val affectedUnits2 = playerTwo.deployed.filter { unit ->
            calculateClosestDistanceBetweenUnits(explodingUnit, unit) <= 6f // Count cascading explosions
        }
        affectedUnits1.forEach { unit ->
            val newExplosions = allocatePure(theContext, explosionCount * explodeDamage, unit, playerOne)         // Apply explosion damage to each affected unit
            repeat(newExplosions) {
                if (simpleRoll(6) == 1) explosionCount++
            }
        }
        affectedUnits2.forEach { unit ->
            val newExplosions = allocatePure(theContext, explosionCount * explodeDamage, unit, playerTwo)         // Apply explosion damage to each affected unit
            repeat(newExplosions) {
                if (simpleRoll(6) == 1) explosionCount++
            }
        }
        explosionCount = 0
    }
}


fun rout(theContext: Context, gameUnit: GameUnit)
{
    val unit = gameUnit.forces
    val modelsToRemove = mutableListOf<Pair<GameModel, Model>>() // List to collect models to remove
    for ((gameModel, model) in gameUnit.physicalTroops.zip(unit.composition))   // Iterate through physicalTroops and composition together
    {
        val roll = simpleRoll(6) // Roll a D6
        if (roll < 3) {
            modelsToRemove.add(Pair(gameModel, model)) // Collect models to remove
        }
    }
    for ((gameModel, model) in modelsToRemove)   // Remove models after iteration
    {
        removeModelFromUnit(gameUnit, gameModel, theContext) // Remove the GameModel
        playSound(theContext, R.raw.punch) // Play Punch.mp3
        unit.composition.remove(model) // Remove the Model from the unit composition
    }
}

fun squadRegeneration(gameUnit0: GameUnit, theContext: Context)
{
    val zombieUnit = gameUnit0.forces
    var remainingWounds = simpleRoll(3) // Roll D3 to determine reanimation wounds
    remainingWounds = healModels(gameUnit0, remainingWounds) // Case 1: Heal models with partial wounds
    val avgX = gameUnit0.physicalTroops.map { it.view.x }.average().toFloat()
    val avgY = gameUnit0.physicalTroops.map { it.view.y }.average().toFloat()     // Calculate average position of existing models for reanimation placement
    val parentLayout = gameUnit0.physicalTroops.firstOrNull()?.view?.parent as? ViewGroup
        ?: throw IllegalStateException("Parent layout not found.")
    val referenceModel = gameUnit0.physicalTroops.firstOrNull() // Find an existing model to copy rotation from
    while (remainingWounds > 0 && zombieUnit.composition.size < zombieUnit.startingModelSize)
    {
        val originalGameModel = referenceModel ?: break         // Create the new view for the model
        val newModel = originalGameModel.model.deepCopy().apply { health = 1 } // Case 2: Reanimate destroyed models
        val newView = originalGameModel.view.duplicate(theContext).apply {
            layoutParams = ViewGroup.MarginLayoutParams(150, 150).apply {
                this.leftMargin = (avgX + (-150..150).random()).toInt()
                this.topMargin = (avgY + (-150..150).random()).toInt()
            }
            rotation = referenceModel.view.rotation // Copy rotation
            scaleX = referenceModel.view.scaleX // Copy reflection if needed
        }
        parentLayout.addView(newView)
        val newHealthText = originalGameModel.healthText.duplicateTextView(theContext).apply {
            layoutParams = ViewGroup.MarginLayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT // Create the health text and position it below the model
            ).apply {
                this.leftMargin = newView.left
                this.topMargin = newView.top + newView.layoutParams.height + 10 // Offset below the model
            }
            textSize = 16f // Increase font size for readability
            textAlignment = TextView.TEXT_ALIGNMENT_CENTER
        }
        parentLayout.addView(newHealthText)
        val newGameModel = GameModel(
            model = newModel,
            view = newView, // Create and add the new GameModel
            healthText = newHealthText
        )
        zombieUnit.composition.add(newModel)
        gameUnit0.physicalTroops.add(newGameModel)
        remainingWounds--
        updateHealthText(newGameModel)
    }
    if (remainingWounds > 0)
        healModels(gameUnit0, remainingWounds) // Case 3: Heal any remaining wounds
    coherency(gameUnit0)
    playSound(theContext, R.raw.energy)
}

fun healModels(medicated: GameUnit, remainingWounds: Int): Int
{
    var woundsLeft = remainingWounds
    for (gameModel in medicated.forces.composition)
    {
        if (gameModel.health < gameModel.startingHealth && woundsLeft > 0)
        {
            val woundsToHeal = minOf(woundsLeft, gameModel.startingHealth - gameModel.health)
            gameModel.health += woundsToHeal
            woundsLeft -= woundsToHeal
        }
    }
    medicated.physicalTroops.forEach { updateHealthText(it) }
    return woundsLeft
}

fun randomPosition(input: Int): Float //For Initial positioning.
{
val conv = input.toFloat()
   return conv*Random.nextFloat() * 2f - 1f //This is the equivalent of a  random float between -input and input.
}



