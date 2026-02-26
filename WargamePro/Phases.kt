package com.example.wargamesimulatorpro

import android.content.Context
import android.util.Log
import android.widget.Toast
import com.example.wargamesimulatorpro.BattleActivity.Companion.fakeInchPx
import com.example.wargamesimulatorpro.BattleActivity.Companion.round
import com.example.wargamesimulatorpro.SquadAbilities.firstStrikeTemp
import com.example.wargamesimulatorpro.SquadAbilities.infect
import com.example.wargamesimulatorpro.SquadAbilities.resistFirstDamage
import com.example.wargamesimulatorpro.SquadAbilities.selfRessurection
import com.example.wargamesimulatorpro.WeaponAbilities.blast
import com.example.wargamesimulatorpro.WeaponAbilities.devastatingInjuries
import com.example.wargamesimulatorpro.WeaponAbilities.oneHitReroll
import com.example.wargamesimulatorpro.WeaponAbilities.perilous
import com.example.wargamesimulatorpro.WeaponAbilities.pike
import com.example.wargamesimulatorpro.WeaponAbilities.oneShot
import com.example.wargamesimulatorpro.WeaponAbilities.oneWoundReroll
import com.example.wargamesimulatorpro.WeaponAbilities.pistol
import kotlinx.coroutines.*
import java.util.Locale
import kotlin.coroutines.resume
import kotlin.math.*

suspend fun movement(theContext: Context, movementAttribute: Float, activeGameUnit: GameUnit, enemyGameUnits: MutableList<GameUnit>): MoveVars = coroutineScope {
    var movementAttr = movementAttribute
    val moveVariables = MoveVars(move = false, advance = false, retreat = false)
    val fightRange = checkFightRange(activeGameUnit, enemyGameUnits, 1f)
    var rushResult = false
    val movinForce = activeGameUnit.forces
    val forceName = movinForce.name
    if(movinForce.squadType.contains("Fortification") || movementAttribute <= 0f) //Forts and slowpokes can't move.
        return@coroutineScope moveVariables
    if(movinForce.squadType.contains("Aircraft"))
    {
        Toast.makeText(theContext, "Moving Aircraft", Toast.LENGTH_SHORT).show()
        moveVariables.move = true
        aircraftMoveStuff(theContext, activeGameUnit, enemyGameUnits)
        return@coroutineScope moveVariables
    }
    var rushDistance = simpleRoll(6)
    Log.d("WSF","Rush Distance is $rushDistance")
    rushDistance += movinForce.squadAbilities.find { it.internal == "+Rush" }?.Modifier ?: 0
    Log.d("WSF","Rush Distance is $rushDistance")
    rushDistance = movinForce.squadAbilities.find { it.internal == "Super Advance" }?.Modifier ?: rushDistance
    Log.d("WSF","Rush Distance is $rushDistance")
    if (!fightRange)
    {
         rushResult = suspendCancellableCoroutine { continuation ->
            createConfirmationDialog(
                theContext,
                "Do you want to Rush $rushDistance with $forceName?"
            ) { result ->
                continuation.resume(result)
            }
        }
    }
    if (rushResult) // Suspend until user confirms Rush
    {
        movementAttr += rushDistance
        moveVariables.advance = true
    }
        if (fightRange) moveVariables.retreat = true
        moveStuff(activeGameUnit, enemyGameUnits, movementAttr)
        moveVariables.move = true
        if(movinForce.squadType.contains("Mounted"))
            playSound(theContext,R.raw.motorcycle)
        else
            playSound(theContext, R.raw.moved)
    return@coroutineScope moveVariables // Return the final state
}

suspend fun charge(
    theContext: Context,
    chargeBoost: Float,
    enemyUnit: GameUnit,
    activeUnit: GameUnit
): Boolean = coroutineScope {
    val chargeResult = suspendCancellableCoroutine{ continuation ->
        val name1 = activeUnit.forces.name
        val name2 = enemyUnit.forces.name
        val distance = String.format(Locale.US,"%.2f", calculateClosestDistanceBetweenUnits(activeUnit, enemyUnit))
        createConfirmationDialog(theContext, "Do you want to Charge $name2 with $name1? The distance is $distance ") { result ->
            continuation.resume(result)
        }
    }
    if (chargeResult)
    {
        val sum = Roll2d6() + chargeBoost
        val distance = calculateClosestDistanceBetweenUnits(activeUnit, enemyUnit)
        if (sum >= distance && distance <= 12f)
        {
            moveRotate(activeUnit, enemyUnit)
            val closestEnemyCircle = findClosestModel(enemyUnit, activeUnit)
            val activeCircle = findClosestModel(activeUnit, enemyUnit)  // Find the closest circles of both units
            val angle = atan2(closestEnemyCircle.y - activeCircle.y, closestEnemyCircle.x - activeCircle.x)
            val distanceToMove = calculateEdgeDistance(activeCircle.x, activeCircle.y, activeCircle.width, activeCircle.height, closestEnemyCircle.x, closestEnemyCircle.y, closestEnemyCircle.width, closestEnemyCircle.height) - fakeInchPx * 0.1f
            val newX = activeCircle.x + distanceToMove * cos(angle)
            val newY = activeCircle.y + distanceToMove * sin(angle)      // Move the active unit to the new position
            playSound(theContext, R.raw.goodcharge)
            moveGameUnitCharge(activeUnit, newX - activeCircle.x, newY - activeCircle.y) // Rotate the active unit to face the enemy
            true // Charge succeeded
        } else {
            playSound(theContext, R.raw.failedcharge)
            false // Charge failed
        }
    } else
        false // Charge was not initiated
}
fun hitSequence(attackRolls: Int, hitSkill: Int, hitModifier: Int, reRollCheck: Int, critThreshold: Int, abilityCheck: MutableList<WeaponAbility>): Pair<Int, Int> {
    var hits = 0
    val hardHits = 0
    if (hitSkill < 2) {
        hits = attackRolls
        return Pair(hits, hardHits)
    }
    val hitChange = hitModifier.coerceIn(-1, 1)
    var rollChecker: Int
    val bonusHits = abilityCheck.find { it.internal == "Bonus Hits" }?.Modifier ?: 0
    Log.d("WSF","Bonus Hits is $bonusHits")
    val hardHitsTest = abilityCheck.any { it == WeaponAbilities.hardHits }
    Log.d("WSF","Hard Hits is $hardHitsTest")
    var oneReroll = abilityCheck.any { it == oneHitReroll }
    Log.d("WSF","One HIt Reroll is $oneReroll")
    var lethalCounter = 0
    for (i in 0 until attackRolls)
    {
        var criticalHit = false
        rollChecker = reRollCheck
        if (oneReroll) {
            rollChecker = 2
            oneReroll = false
        }
        val diceRoll = when (rollChecker)
        {
            1 -> reRollOnes()
            2 -> reRollFailed(hitSkill)
            else -> simpleRoll(6)
        }
        if (diceRoll == critThreshold) criticalHit = true
        if (diceRoll == 1) continue
        if (diceRoll + hitChange >= hitSkill || criticalHit)
            hits++
        if (hardHitsTest && criticalHit)
        {
            hits--
            lethalCounter++
        }
        if (bonusHits > 0 && criticalHit)
        {
            hits += bonusHits
        }
    }
    return Pair(hits, lethalCounter)
}


fun woundSequence(hits: Int, strength: Int, hardness: Int, injuryModifier: Int, reRollCheck: Int, abilityCheck: MutableList<WeaponAbility>, antiLimit: Int): Pair<Int, Int>
{
    var injuries = 0
    var rollchecker: Int
    val devastatingInjuries = abilityCheck.any{ it == devastatingInjuries }
    Log.d("","Devastating Injuries is $devastatingInjuries")
    var devastatingCounter = 0
    val injuryChange = injuryModifier.coerceIn(-1, 1) // Limit the range of WoundModifier
    var criticalInjury = false
    var oneReroll = abilityCheck.any { it == oneWoundReroll }
    Log.d("WSF","oneWoundreroll is $oneReroll")
    for (i in 0..<hits)
    {
        val injuryThreshold = when
        {
            strength >= hardness * 2 -> 2
            strength > hardness && strength < hardness * 2 -> 3
            strength == hardness -> 4
            strength < hardness && hardness < strength * 2 -> 5
            else -> 6
        }
        rollchecker = reRollCheck
        if(oneReroll)
        {
            rollchecker = 2
            oneReroll = false
        }
        val diceRoll = when (rollchecker)
        {
            1 -> reRollOnes()
            2 -> reRollFailed(injuryThreshold)
            else -> simpleRoll(6)
        }
        if (diceRoll == 1) continue // Unmodified wound roll of 1 always fails
        if (diceRoll >= antiLimit) criticalInjury = true
        if (diceRoll + injuryChange >= injuryThreshold || criticalInjury) // Check for successful wound
            injuries++
        if (devastatingInjuries && criticalInjury)
        {
            devastatingCounter++
            injuries--
        }
    }
    return Pair(injuries, devastatingCounter)
}

fun saveSequence(injuries: Int, defense: Int, armorPenetration: Int, dodge: Int): Int
{
    val reducedSave = defense - armorPenetration
    val finalSave = if (dodge < reducedSave) dodge else reducedSave
    Log.d("WSF","Dodge is $dodge")
    var unsavedInjuries = 0
    repeat(injuries) {
        val diceRoll = simpleRoll(6)
        if (diceRoll + armorPenetration < finalSave) unsavedInjuries++
    }
    return unsavedInjuries
}
fun allocateDamage(theContext: Context, weaponDamage: String, seriousInjuries: Int, unit: GameUnit, specialty: MutableList<WeaponAbility>, half: Boolean, damagedPlayer:SuperPlayer ): Int
{
    if (seriousInjuries <= 0) return 0 // Early exit if no unsaved wounds
    val damagedUnit = unit.forces
    val damagedGroup = damagedUnit.composition
    val damagedAbilities = damagedUnit.squadAbilities
    var remainingWounds = seriousInjuries
    var modelsKilled = 0
    var resist = damagedUnit.damageResistance
    var damageDealt = 0
    var mantle = false // Ability to set the first unsaved wound damage to 0.
    if (damagedAbilities.any { it.internal == "BrainBlock" } && specialty.any { it.internal == "Psi" }) //Psionic Defense
        resist = unit.forces.squadAbilities.find { it.internal == "BrainBlock" }?.Modifier ?: unit.forces.damageResistance
    Log.d("","resist is $resist")
    if (specialty.any { it.internal == "rareFirst" })
    {
        val nameFrequency = damagedGroup.groupingBy { it.name }.eachCount()
        damagedGroup.sortBy { nameFrequency[it.name] ?: 32767 } //Rarer models are damaged first.
        Log.d("WSF","Precision is active.")
    } else
        damagedGroup.sortBy { it.health } // Default sort
    val iterator = damagedGroup.iterator()
    while (iterator.hasNext() && remainingWounds > 0)
    {
        val model = iterator.next()
        while (remainingWounds > 0 && model.health > 0)
        {
            var woundLoss = damageParser(weaponDamage)
            woundLoss = damageMods(woundLoss, damagedAbilities, specialty, half)
            var reducedDamage = woundLoss
            repeat(woundLoss) {
                val fnpCheck = simpleRoll(6)
                if (fnpCheck >= resist) reducedDamage--
            }
            if (!mantle && damagedAbilities.any { it == resistFirstDamage })
            {
                mantle = true
                reducedDamage = 0
                Log.d("WSF","Mantle Finished")
            }
            model.health -= reducedDamage
            damageDealt += reducedDamage
            remainingWounds--
            val corpse = unit.physicalTroops.first()
            if (model.health <= 0 && unit.forces.squadAbilities.none { it == selfRessurection } && unit.forces.squadAbilities.none { it.internal == "Hit@End" })
            {
                modelsKilled++
                    removeModelFromUnit(unit, corpse, theContext)
                iterator.remove()
                break
            } else
            {
                playSound(theContext, R.raw.swordinjury)
                updateHealthText(corpse)
            }
        }
    }
    coherency(unit)
    if(unit.forces.composition.isEmpty())
        damagedPlayer.deployed.remove(unit)
    return modelsKilled
}
fun allocatePure(theContext: Context, pureDamage: Int, gameUnitPure: GameUnit, damagedPlayer: SuperPlayer): Int
{
    if (pureDamage <= 0) return 0 // Early exit if no pure Damage.
    var remainingWounds = pureDamage
    var modelsKilled = 0
    var damageDealt = 0
    val unit = gameUnitPure.forces
    unit.composition.sortBy { it.health }     // Sort models by health (damaged models first)
    var resist = unit.damageResistance // Adjust Feel No Pain (FNP) if the unit has Mortal Defense ability
    if (unit.squadAbilities.any { it.internal == "Special Def" }) //Pure Defense
    {
        resist = unit.squadAbilities.find { it.internal == "Special Def" }?.Modifier ?: unit.damageResistance
    }
    Log.d("WSF","resist is $resist")
    val iterator = unit.composition.iterator()    // Iterator to traverse models
    while (iterator.hasNext() && remainingWounds > 0)
    {
        val model = iterator.next()
        while (remainingWounds > 0 && model.health > 0)
        {
            val resistCheck = simpleRoll(6)
            val woundNegated = resistCheck >= resist
            if (!woundNegated)
            {
                model.health-- // Apply one mortal wound
                remainingWounds--
                damageDealt++
               playSound(theContext, R.raw.swordinjury)
            } else
                remainingWounds--
            if (model.health <= 0 && unit.squadAbilities.none { it == selfRessurection })
            {
                modelsKilled++
                val corpse = gameUnitPure.physicalTroops.first()
                removeModelFromUnit(gameUnitPure, corpse, theContext)
                iterator.remove() // Remove the model from the unit
                break // Move to the next model
            }
        }
    }
    Toast.makeText(theContext, "$damageDealt Pure Damage Dealt!", Toast.LENGTH_SHORT,).show()
    coherency(gameUnitPure)
    if(gameUnitPure.forces.composition.isEmpty())
        damagedPlayer.deployed.remove(gameUnitPure)
    return modelsKilled
}


suspend fun shootingPhase(theContext: Context, game1: GameUnit, game2: GameUnit, shootPlayer: SuperPlayer, shotPlayer: SuperPlayer)
{
    var InCover = false
    val shooter = game1.forces
    val shooted = game2.forces
    val distance = calculateClosestDistanceBetweenUnits(game1, game2)
    val activeGuns = attackUnitWeapons(shooter) // List of all weapons in the unit
    val shootyModels = shooter.composition
    val weaponShotMap = activeGuns.associateBy({ it.weaponName }, { 0 }).toMutableMap() // Initialize with 0 shots for each weapon in ActiveGuns
    var demiseCheck: Int
    moveRotate(game1, game2)
    for (shootingModel in shootyModels)
    {
        var lowHealth = 0
        val shootList = shootingModel.tools
        if (shootingModel.health <= shootingModel.bracketed)
            lowHealth -= 1
        for (shoot in shootList)
        {
            checkVictory(theContext)
            if (shootingModel.health == 0)
                continue //This prevents iterator errors.
            val shotAbilities = shoot.special
            val halfRange = distance <= (shoot.range / 2f)
            var validShooting = checkValidShooting(game1, shoot, game2)
            if (shotAbilities.any { it == pistol } && distance > 1f)  // Check if the current weapon has the Pistol ability
            {
                if (shootList.any { otherWeapon ->
                        otherWeapon != shoot &&
                                otherWeapon.range > 1f &&
                                otherWeapon.special.none { it == pistol }
                    }
                )
                    validShooting = false
                Log.d("WSF", "Pistol Check")
            }
            if (validShooting) {
                var shots = damageParser(shoot.attacks)
                if (shots < 1) continue //Skip one-Shot weapons or invalid weapons.
                val rapidFireModifier =
                    shotAbilities.find { it.Name == "Rapid Fire" }?.Modifier ?: 0
                Log.d("WSF", "rapid Fire is  $rapidFireModifier")
                if (halfRange && rapidFireModifier > 0)
                    shots += rapidFireModifier
                val blastModifier = shotAbilities.any { it == blast }
                if (blastModifier) {
                    val blastHits =
                        shooted.composition.size / 5 //An additional attack for every 5 models rounded down.
                    shots += blastHits
                    Log.d("WSF", "blast triggered")
                }
                if (shoot.weaponName in weaponShotMap && shotAbilities.none { it == perilous }) //Hazardous weapons are the exception to batch shooting to make sure the right model dies.
                {
                    weaponShotMap[shoot.weaponName] =
                        weaponShotMap[shoot.weaponName]!! + shots // Add shots to the cumulative total
                    continue
                }
                weaponShotMap[shoot.weaponName] =
                    shots // First time processing this weapon, initialize cumulative shots
                val modifyMe = obtainModifiers(shoot, game1, game2, InCover, false, shootPlayer.thePlayer, shotPlayer.thePlayer)
                val hitMod = modifyMe.hitMod + lowHealth
                val woundMod = modifyMe.woundMod
                val hitReroll = modifyMe.hitReroll
                val woundReroll = modifyMe.woundReroll
                val armorMod = modifyMe.defenseMod
                val antiThreshold = modifyMe.antiThreshold
                val critThreshold = modifyMe.critThreshold
                var targetToughness = shooted.hardness
                val contagionRange =
                    if (round < 3) round * 3f else 9f //The current round is a global variable.
                if (shooter.squadAbilities.any { it == infect } && distance <= contagionRange) {
                    Log.d("WSF", "Infect!")
                    targetToughness--
                }
                val hits = hitSequence(
                    shots,
                    shoot.hitSkill,
                    hitMod,
                    hitReroll,
                    critThreshold,
                    shoot.special
                )
                val injuries = woundSequence(
                    hits.first,
                    shoot.strength,
                    targetToughness,
                    woundMod,
                    woundReroll,
                    shoot.special,
                    antiThreshold
                )
                val soundByte = hits.second + injuries.second
                val printHits = hits.first
                val defaultSound =
                    if (shotAbilities.any { it == blast }) R.raw.rocketlauncher else R.raw.rifleshot
                if (soundByte > 0)
                    repeat(soundByte) { playSound(theContext, R.raw.critical) }
                else
                    repeat(printHits) { playSound(theContext, defaultSound) }
                if (printHits == 0 && hits.second == 0) playSound(theContext, R.raw.rangedmiss)
                val printInjuries = injuries.first + hits.second
                val failedSaves =
                    saveSequence(printInjuries, shooted.defense, armorMod, shooted.dodge)
                val printPenetrations = failedSaves + injuries.second
                demiseCheck = allocateDamage(theContext, shoot.damage, printPenetrations, game2, shoot.special, halfRange, shotPlayer)
                Toast.makeText(theContext, "${shoot.weaponName} had $printHits Hits, $printInjuries Injuries, and dealt $printPenetrations Piercing Wounds. ", Toast.LENGTH_SHORT).show()
                delay(2000)
                if (shooted.squadAbilities.any { it.internal == "Explodes" }) //This handles explosions in the target unit. Although most units with this ability are single model units.
                    explosionProcess(theContext, game2,  demiseCheck)
                if (shotAbilities.any { it == perilous })
                {
                    val roll = simpleRoll(6) // Roll once to avoid multiple evaluations
                    if (roll == 1 && shooter.squadType.contains("Infantry"))
                    {
                        shootingModel.health = 0
                        playSound(theContext, R.raw.perilous)
                    } else if (roll == 1)
                    {
                        playSound(theContext, R.raw.perilous)
                        demiseCheck = allocatePure(theContext, 3, game1, shotPlayer)
                        if (shootingModel.health <= 0 || shooter.composition.isEmpty()) //This is to prevent errors from a dead model shooting.
                            return
                        if (shooter.squadAbilities.any { it.internal == "Explodes" }) //This handles explosions in the target unit. Although most units with this ability are single model units.
                            explosionProcess(theContext, game1, demiseCheck)
                    }
                }
                if (shotAbilities.any { it == oneShot }) //This is for once per battle weapons.
                    shoot.attacks = "0"
            }
        }
    }
    for (activeWeapon in activeGuns)
    {
        checkVictory(theContext)
        val cumulativeShots = weaponShotMap[activeWeapon.weaponName] ?: 0
        if (cumulativeShots > 0) {
            val shotAbilities = activeWeapon.special
            val halfRange = distance <= (activeWeapon.range / 2f)
            val validShooting = checkValidShooting(game1, activeWeapon, game2)
            val modifyMe = obtainModifiers(activeWeapon, game1, game2, InCover, false, shootPlayer.thePlayer, shotPlayer.thePlayer)
            val hitMod = modifyMe.hitMod
            val woundMod = modifyMe.woundMod
            val hitReroll = modifyMe.hitReroll
            val woundReroll = modifyMe.woundReroll
            val armorMod = modifyMe.defenseMod
            val antiThreshold = modifyMe.antiThreshold
            val critThreshold = modifyMe.critThreshold
            if (validShooting) {
                var targetToughness = shooted.hardness
                val contagionRange =
                    if (round < 3) round * 3f else 9f //The current round is a global variable.
                if (shooter.squadAbilities.any { it == infect } && distance <= contagionRange) {
                    Log.d("WSF", "Infect!")
                    targetToughness--
                }
                val hits = hitSequence(
                    cumulativeShots,
                    activeWeapon.hitSkill,
                    hitMod,
                    hitReroll,
                    critThreshold,
                    shotAbilities
                )
                val defaultSound =
                    if (shotAbilities.any { it == blast }) R.raw.rocketlauncher else R.raw.rifleshot
                val injuries = woundSequence(
                    hits.first,
                    activeWeapon.strength,
                    targetToughness,
                    woundMod,
                    woundReroll,
                    shotAbilities,
                    antiThreshold
                )
                val soundByte = hits.second + injuries.second
                val printHits = hits.first
                if (soundByte > 0)
                    repeat(soundByte) { playSound(theContext, R.raw.critical) }
                else
                    repeat(printHits) { playSound(theContext, defaultSound) }
                if (printHits == 0 && hits.second == 0) playSound(theContext, R.raw.rangedmiss)
                val printInjuries = injuries.first + hits.second
                val failedSaves = saveSequence(
                    injuries.first + hits.second,
                    shooted.defense,
                    armorMod,
                    shooted.dodge
                )
                val printPenetrations = failedSaves + injuries.second
                demiseCheck = allocateDamage(theContext, activeWeapon.damage, printPenetrations, game2, shotAbilities, halfRange, shotPlayer)
                Toast.makeText(
                    theContext,
                    "${activeWeapon.weaponName} had $printHits Hits, $printInjuries Injuries, and dealt $printPenetrations Piercing Wounds. ",
                    Toast.LENGTH_SHORT
                ).show()
                delay(3000)
                if (shooted.squadAbilities.any { it.internal == "Explodes" }) //This handles explosions in the target unit. Although most units with this ability are single model units.
                    explosionProcess(theContext, game2,  demiseCheck)
            }
        }
    }
}

suspend fun processFightPhase(theContext: Context, game1: GameUnit, game2: GameUnit, fighter: Squad, swingPlayer: SuperPlayer, hitPlayer: SuperPlayer)
{
    val target = game2.forces
    moveRotate(game1, game2)
    val groupedWeapons = fighter.composition.flatMap { model -> model.tools }  // Group weapons by name for batch processing
        .filter { it.isMelee } // Only melee weapons
        .groupBy { it.weaponName }  // Iterate through grouped weapons
    groupedWeapons.forEach { (_, weapons) ->
         checkVictory(theContext)
        val totalShots = weapons.sumOf { damageParser(it.attacks) } // Batch process total shots
        val fightWeapon = weapons.first() // Representative weapon for shared properties
        val swingAbilities = fightWeapon.special
        var pikeBonus = 0
        val chargeCheck = fighter.squadAbilities.any{it == firstStrikeTemp}
        if(chargeCheck && swingAbilities.any {it == pike})
        {
            Log.d("WSF","Pike Proc!")
            pikeBonus += 1
        }
        val modifyMe = obtainModifiers(fightWeapon,game1, game2,false,false, swingPlayer.thePlayer, hitPlayer.thePlayer)
        val hitMod = modifyMe.hitMod
        val woundMod = modifyMe.woundMod + pikeBonus
        val hitReroll = modifyMe.hitReroll
        val woundReroll = modifyMe.woundReroll
        val armorMod = modifyMe.defenseMod
        val antiThreshold = modifyMe.antiThreshold
        val critThreshold = modifyMe.critThreshold
        var targetToughness = target.hardness
        if (fighter.squadAbilities.any {it == infect}) //Fight Range is always less than 3 inches.
        {
            Log.d("WSF","Infect!")
            targetToughness--
        }
        val hits = hitSequence(totalShots, fightWeapon.hitSkill, hitMod, hitReroll, critThreshold, fightWeapon.special)
        if (hits.first == 0 && hits.second == 0) playSound(theContext, R.raw.meleemiss)
        val injuries = woundSequence(hits.first, fightWeapon.strength, targetToughness, woundMod, woundReroll, swingAbilities, antiThreshold)
        val soundByte = hits.second+injuries.second
        val printHits = hits.first
        if(soundByte > 0)
            repeat(soundByte)  {playSound(theContext, R.raw.critical) }
        else
            repeat(printHits)  {playSound(theContext, R.raw.meleehit) }
        val printInjuries = injuries.first+hits.second
        val failedSaves = saveSequence( printInjuries, target.defense, armorMod, target.dodge)
        val printPenetrations = failedSaves+injuries.second
        if(printPenetrations > 0)
            playSound(theContext, R.raw.chomp)
        val demiseCheck = allocateDamage(theContext, fightWeapon.damage, printPenetrations, game2, fightWeapon.special, false, hitPlayer)
        Toast.makeText(theContext, "${fightWeapon.weaponName} had $printHits Hits, $printInjuries Injuries, and dealt $printPenetrations Piercing Wounds. ", Toast.LENGTH_SHORT).show()
        delay(3000)
        if(target.squadAbilities.any {it.internal == "Explodes"})//This handles explosions in the target unit. Although most units with this ability are single model units.
            explosionProcess(theContext, game2, demiseCheck)
    }
}

