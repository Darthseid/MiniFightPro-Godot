package com.example.wargamesimulatorpro

import android.content.Context
import android.util.Log
import android.widget.Toast
import com.example.wargamesimulatorpro.BattleActivity.Companion.round
import com.example.wargamesimulatorpro.SquadAbilities.closeUptoShoot
import com.example.wargamesimulatorpro.SquadAbilities.firstStrikeTemp
import com.example.wargamesimulatorpro.SquadAbilities.minusHitRanged
import com.example.wargamesimulatorpro.SquadAbilities.noModifiers
import com.example.wargamesimulatorpro.SquadAbilities.reduceDamageBy1
import com.example.wargamesimulatorpro.SquadAbilities.reduceDamageByHalf
import com.example.wargamesimulatorpro.SquadAbilities.reduceDamageto1
import com.example.wargamesimulatorpro.SquadAbilities.squadplusOneInjuries
import com.example.wargamesimulatorpro.SquadAbilities.squadrerollHitOnes
import com.example.wargamesimulatorpro.SquadAbilities.squadrerollHits
import com.example.wargamesimulatorpro.SquadAbilities.squadrerollInjuryOnes
import com.example.wargamesimulatorpro.SquadAbilities.stopRerolls
import com.example.wargamesimulatorpro.SquadAbilities.weakenStrongAttack
import com.example.wargamesimulatorpro.WeaponAbilities.blast
import com.example.wargamesimulatorpro.WeaponAbilities.conversion
import com.example.wargamesimulatorpro.WeaponAbilities.hefty
import com.example.wargamesimulatorpro.WeaponAbilities.ignoresCover
import com.example.wargamesimulatorpro.WeaponAbilities.pistol
import com.example.wargamesimulatorpro.WeaponAbilities.plusOneInjuries
import com.example.wargamesimulatorpro.WeaponAbilities.rerollHitOnes
import com.example.wargamesimulatorpro.WeaponAbilities.rerollHits
import com.example.wargamesimulatorpro.WeaponAbilities.rerollInjuryOnes
import com.example.wargamesimulatorpro.WeaponAbilities.heftyTemp
import com.example.wargamesimulatorpro.WeaponAbilities.injuryReroll
import com.example.wargamesimulatorpro.SquadAbilities.teleport
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume


fun attackUnitWeapons(unity: Squad): MutableList<Weapon>
{
    val weaponList = unity.composition.flatMap { model -> model.tools.map { it.copy() } }.toMutableList() // Copy all weapons from the unit's composition
    val weaponCount = weaponList.groupingBy { it.weaponName }.eachCount() // Create a map to count the occurrences of each weapon by its name
    weaponList.retainAll { weapon -> (weaponCount[weapon.weaponName] ?: 0) > 1 } // Filter out weapons that only appear once
    return weaponList.distinctBy { it.weaponName }.toMutableList() // Remove duplicates by converting to a set and back to a list
}

fun checkValidShooting(shooter: GameUnit, firearm: Weapon, shooted: GameUnit ): Boolean
{
   val distance = calculateClosestDistanceBetweenUnits(shooter, shooted)
    var validShooting = distance <= (firearm.range)
    val shotAbilities = firearm.special
    val fightRange = distance <= 1f
    if (firearm.isMelee)
        return false //Prevent tanks from "shooting with their tracks" in fight range.
    if (shooter.movable.advance && shotAbilities.none { it.internal == "RunGun" })
        validShooting = false
    if (shooter.movable.retreat && shooter.forces.squadAbilities.none {it.internal == "FleeShoot"})
        validShooting = false
    if(shooter.forces.squadType.none { it == "Monster" || it == "Vehicle" })
    {
        if (fightRange && shotAbilities.none { it == pistol })
            validShooting = false
    }
    if(shooted.forces.squadAbilities.any { it == closeUptoShoot  } && distance > 12f)
        validShooting = false
    if (fightRange && shotAbilities.any {it == blast}) //Blast can never be used at point-blank range.
        validShooting = false
    return validShooting
}

fun obtainModifiers(firearm: Weapon, guy1: GameUnit, guy2: GameUnit, coverType: Boolean, isFight: Boolean, Player1: Player, Player2: Player): DiceModifiers
{
    var hitMod = 0
    val distance = calculateClosestDistanceBetweenUnits(guy1, guy2)
    var woundMod = 0
    var hitReroll = 0
    var woundReroll = 0
    var antiThreshold = 6 // Critical Wound upper limit
    var critThreshold = 6
    val antiAbilities = listOf(
        "MobKiller" to "Infantry",
        "TankKiller" to "Vehicle",
        "KaijuKiller" to "Monster",
        "Assassinate" to "Character",
        "Ack-Ack" to "Fly",
        "WitchKiller" to "Psychic"
    )
    val shooter = guy1.forces
    val shooted = guy2.forces
    val squadType = shooter.squadType
    for ((ability, antiType) in antiAbilities)
    {
        if (firearm.special.any { it.internal == ability } && shooted.squadType.contains(antiType))
            antiThreshold = firearm.special.find { it.internal == ability }?.Modifier ?: antiThreshold
        Log.d("WSF","Handled $ability for $antiType")
    }
    if (distance <= 1f && squadType.any { it == "Monster" || it == "Vehicle" } && !firearm.isMelee)
    {
        Log.d("WSF","Big Model Penalty")
        hitMod -= 1
    }
    if (distance > 12f && firearm.special.any { it == conversion })
    {
        Log.d("WSF","Long Range Boost")
        critThreshold = critThreshold.coerceAtMost(4)
    }
    var armorMod = firearm.armorPenetration
    if (coverType && firearm.special.none { it == ignoresCover })
        armorMod += 1
    armorMod = armorMod.coerceAtMost(1) // Cap at +1 save modifier
    val unsteady = guy1.movable.move
    if (!unsteady && firearm.special.any { it == hefty || it == heftyTemp })
    {
        Log.d("WSF","Opposite of Wimpy")
        hitMod += 1
    }
    if (!isFight && shooted.squadAbilities.any { it == minusHitRanged })
    {
        Log.d("WSF","Miss ranged Proc")
        hitMod -= 1
    }
    if (isFight && shooted.squadAbilities.any { it.internal == "-1 Fight"}) {
        hitMod -= 1
        Log.d("WSF","-1 Melee Proc")
    }
    if (shooted.squadAbilities.any { it.internal == "-1 All" })
    {
        Log.d("WSF","Miss everything proc")
        hitMod -= 1
    }
    if (shooted.shellShock && Player1.playerAbilities.contains("Grim") && round > 2)
    {
        Log.d("WSF","Grim Buff Proc")
        woundMod += 1
    }
    if (shooter.shellShock && Player2.playerAbilities.contains("Grim") && round > 2)
    {
        Log.d("WSF","Grim Debuff Proc")
        hitMod -= 1
    }
    if (firearm.special.any { it == injuryReroll })
    {
        Log.d("WSF","Injury reroll proc")
        woundReroll = 2
    }
    if (firearm.special.any { it == rerollHits } || shooter.squadAbilities.any {it == squadrerollHits})
    {
        Log.d("WSF","Reroll hits Proc")
        hitReroll = 2
    }
    if (firearm.special.any { it == rerollHitOnes } || shooter.squadAbilities.any {it == squadrerollHitOnes})
    {
        Log.d("WSF","reroll Hit Ones Proc")
        hitReroll = 1
    }
    if (firearm.special.any { it == rerollInjuryOnes } || shooter.squadAbilities.any {it == squadrerollInjuryOnes})
    {
        Log.d("WSF","Reroll Injury Ones Proc")
        hitReroll = 1
    }
    if (firearm.special.any { it == plusOneInjuries } || shooter.squadAbilities.any {it == squadplusOneInjuries})
    {
        Log.d("WSF","+1 Injury Proc")
        woundMod += 1
    }
    if (shooted.squadAbilities.any {it == stopRerolls})
    {
        hitReroll = 0
        woundReroll = 0
        Log.d("WSF","Stop Rerolls Proc")
    }
    if (firearm.strength > shooted.hardness && shooted.squadAbilities.any { it == weakenStrongAttack })
    {
        Log.d("WSF","weaken Strong Attack Proc")
        woundMod -= 1
    }
    if (shooter.squadAbilities.any {it == noModifiers})
    {
        hitMod = hitMod.coerceAtLeast(0)
        woundMod = woundMod.coerceAtLeast(0)
        Log.d("WSF","No Mods Proc")
    }
    return DiceModifiers(hitMod, woundMod, hitReroll, woundReroll, armorMod, critThreshold, antiThreshold)
}

fun damageParser(input: String): Int
{
    try
    { return input.toInt() }
    catch (e: NumberFormatException)
    { /* Ignore the exception and continue with regex parsing */ }
    val pattern = Regex("(\\d*)[Dd]?(\\d+)([+-](\\d+))?")
    val matchResult = pattern.matchEntire(input)
    if (matchResult != null) {
        val multiplier = matchResult.groupValues[1].toIntOrNull() ?: 1
        val sides = matchResult.groupValues[2].toInt()
        var result = 0
        repeat(multiplier) {  result += simpleRoll(sides) }
        val modifier = matchResult.groupValues[3].toIntOrNull()
        if (modifier != null)
            result += modifier
        return result
    } else
        throw IllegalArgumentException("Invalid input: $input")
}

fun damageMods (damage: Int, targetSquadAbilities: MutableList<SquadAbility>, specials:MutableList<WeaponAbility>, half: Boolean): Int
{
    var newDamage = damage
    val fusionModifier = specials.find { it.internal == "Fusion" }?.Modifier ?: 0
    if (half && fusionModifier > 0)
    {
        Log.d("WSF","Fusion Proc")
        newDamage += fusionModifier
    }
    if (targetSquadAbilities.any { it == reduceDamageBy1 })
        newDamage = (newDamage - 1).coerceAtLeast(1)
    if (targetSquadAbilities.any { it == reduceDamageByHalf })
        newDamage = (newDamage + 1) / 2
    if (targetSquadAbilities.any { it == reduceDamageto1 })
        newDamage = 1
    Log.d("WSF","damage was $damage, now its $newDamage")
    return newDamage
}

suspend fun movementPhase(theContext: Context, game1: SuperPlayer, game2: SuperPlayer) = coroutineScope {
    val selectedUnits = mutableListOf<GameUnit>() // List of units the player chooses to move
    suspendCancellableCoroutine { continuation ->
        game1.deployed.forEach { activeSquad ->
            activeSquad.physicalTroops.forEach { model ->
                model.view.setOnClickListener {
                    if (!selectedUnits.contains(activeSquad))
                    {     // Step 1: Set up touch listeners for unit selection
                        selectedUnits.add(activeSquad) // Add unit to selection
                       markGameUnitPurple(activeSquad)
                    } else
                    {
                        selectedUnits.remove(activeSquad) // Deselect unit
                        updateHealthText(model)
                    }
                }
            }
        }  // Wait for user confirmation before proceeding
        launch { ContinueGame(theContext) }.invokeOnCompletion {
            removeAllListeners(game1) // Remove all listeners before movement starts
            game1.deployed.forEach { selectedSquad ->
                updateHealthText(selectedSquad)
            }
            continuation.resume(Unit)
        }
    }
    for (unit in selectedUnits)     // Step 2: Move the selected units
    {
        processUnitMovement(theContext, unit, game2)
    }
}

private suspend fun processUnitMovement(theContext: Context, processed: GameUnit, game2: SuperPlayer)
{
    val activeSquad = processed.forces
    var activeMove = activeSquad.movement
    activeMove += movementPhaseChecks(activeSquad)

    val movable = movement(theContext, activeMove, processed, game2.deployed)
    val engagement = checkFightRange(processed, game2.deployed, 1f)

    if (activeSquad.squadAbilities.any { it == teleport } && !engagement)
    {
        Toast.makeText(theContext, "Teleport across the map!", Toast.LENGTH_SHORT).show()
        teleport(theContext, processed, game2.deployed)
    }

    if (activeSquad.shellShock && movable.retreat && !activeSquad.squadType.contains("Titanic"))
    {
        Toast.makeText(theContext, "${activeSquad.name} routed!", Toast.LENGTH_SHORT).show()
        rout(theContext, processed)
        delay(2000)
    }
}

suspend fun shootingPhaseSelection(theContext: Context, player1: SuperPlayer, player2: SuperPlayer) = coroutineScope {
    val activeUnits = player1.deployed.toMutableList()
    suspendCancellableCoroutine { continuation ->
        activeUnits.forEach { activeSquad ->
            val theName = activeSquad.forces.name
            activeSquad.physicalTroops.forEach { model ->
                model.view.setOnClickListener { view ->
                    view.isClickable = false                     // Prevent multiple clicks
                    launch {
                        val wantsToShoot = suspendCancellableCoroutine { shootContinuation ->
                            createConfirmationDialog(theContext, "Do you want to Shoot with $theName?") { result ->
                                shootContinuation.resume(result)
                            }
                        }
                        removeAllListeners(activeSquad) // Remove listener from this unit
                        if (wantsToShoot)
                        {
                            markGameUnitPurple(activeSquad)
                            val target = selectEnemyTarget(player2)
                            if (target != null)
                            {
                                shootingPhase(theContext, activeSquad, target, player1, player2)
                                if (activeSquad.forces.squadAbilities.any { it.internal == "ScareFire" })
                                    shellShockTest(theContext, target, player1, player2.thePlayer.playerAbilities)
                            }
                            updateHealthText(activeSquad)
                        }
                        activeUnits.remove(activeSquad) // Mark as finished
                        if (activeUnits.isEmpty())
                        {
                            removeAllListeners(player2) // Remove enemy touch listeners once all are done
                            continuation.resume(Unit)
                        }
                    }
                }
            }
        }
    }
}


private suspend fun selectEnemyTarget(game2: SuperPlayer): GameUnit? =
    suspendCancellableCoroutine { continuation ->
        val enemyUnits = game2.deployed.toMutableList() // Allows player to select an enemy unit by touch
        enemyUnits.forEach { enemyUnit ->
            enemyUnit.physicalTroops.forEach { model ->
                model.view.setOnClickListener { view ->
                    view.isClickable = false // Prevent multiple selections
                    removeAllListeners(game2) // Remove all enemy listeners after selection
                    continuation.resume(enemyUnit)
                }
            }
        }
    }

suspend fun chargePhaseSelection(theContext: Context, player1: SuperPlayer, player2: SuperPlayer) = coroutineScope {
    val activeUnits = player1.deployed.toMutableList() // Copy list to modify safely
    val enemyUnits = player2.deployed
    var validUnits = activeUnits.filter { unit -> player1.deployed.any { checkFightRange(unit, enemyUnits, 12f) } }
    validUnits = validUnits.filter { unit -> player1.deployed.any { canCharge(unit, enemyUnits) } } //Units that are too far away and cannot charge are ignored.
    suspendCancellableCoroutine { continuation ->
        validUnits.forEach { activeSquad ->
            activeSquad.physicalTroops.forEach { model ->
                model.view.setOnClickListener { view ->
                    view.isClickable = false // Prevent multiple triggers
                    launch {
                        val chargeBoost = chargePhaseChecks(theContext, activeSquad, enemyUnits)
                        val abilities = activeSquad.forces.squadAbilities
                        if (abilities.any { it.internal == "DashBash" })
                            activeSquad.movable.advance = false
                        if ( chargeBoost > 0f)
                        {
                            markGameUnitPurple(activeSquad)
                            val enemyTarget = selectEnemyTarget(player2)
                            if (enemyTarget != null)
                            {
                                val successfulCharge = charge(theContext, chargeBoost, enemyTarget, activeSquad)
                                if (successfulCharge)
                                {
                                    abilities.add(firstStrikeTemp)
                                    if (abilities.any { it.internal == "Crush" })
                                    {
                                        val roll = simpleRoll(6)
                                        val affectedUnits = enemyUnits.filter { unit ->
                                            calculateClosestDistanceBetweenUnits(activeSquad, unit) <= 1f //Crush all enemy units within fight range of the charged unit.
                                        }
                                        val damage = when (roll)
                                        {
                                            6 -> 3
                                            in 2..5 -> simpleRoll(3)
                                            else -> 0
                                        }
                                        affectedUnits.forEach { crushed ->
                                            allocatePure(theContext, damage, crushed, player2)
                                        }
                                    }
                                }
                            }
                            updateHealthText(activeSquad)
                        }
                        removeAllListeners(activeSquad) // Remove listener from unit
                        activeUnits.remove(activeSquad) // Mark as processed
                        if (activeUnits.isEmpty())
                        {
                            removeAllListeners(player2) // Remove enemy listeners after all attacks
                            continuation.resume(Unit)
                        }
                    }
                }
            }
        }
    }
}


suspend fun fightPhaseSelection(theContext: Context, game1: SuperPlayer, game2: SuperPlayer) = coroutineScope {
    val allUnits = (game1.deployed + game2.deployed).toMutableList()
    val validUnits = allUnits.filter { unit -> game2.deployed.any { checkFightRange(unit, game2.deployed, 1f) } }
    val priorityOne = validUnits.filter { it.forces.squadAbilities.any { ability -> ability.internal == "Hit First" } }
    val priorityTwo = validUnits - priorityOne.toSet()
    for (priorityList0 in listOf(priorityOne, priorityTwo))
    {
        val priorityList = priorityList0.toMutableList()
        suspendCancellableCoroutine { continuation ->
            var isgame1Turn = true // Game2 selects first
            fun selectNextFighter()
            {
                if (priorityList.isEmpty())
                {
                    continuation.resume(Unit) // End phase if all have fought
                    return
                }
                val currentPlayer = if (isgame1Turn) game2 else game1
                val opponentPlayer = if (isgame1Turn) game1 else game2
                val currentUnits = priorityList.filter { it in currentPlayer.deployed }
                if (currentUnits.isEmpty())
                {
                    isgame1Turn = !isgame1Turn // Swap turn
                    selectNextFighter()
                    return
                }
                launch {
                    val selectedUnit = selectUnitForFight(currentUnits) // Suspend until player selects a unit
                    val enemyUnits = opponentPlayer.deployed.filter { checkFightRange(selectedUnit, opponentPlayer.deployed, 1f) }
                    fightPhaseChecks(theContext, selectedUnit, currentPlayer)
                    if (enemyUnits.isNotEmpty())
                    {
                        markGameUnitPurple(selectedUnit)
                        val targetUnit = selectUnitForFight(enemyUnits) // Suspend until player selects an enemy
                        processFightPhase(theContext, selectedUnit, targetUnit,  selectedUnit.forces, game1, game2)
                        updateHealthText(selectedUnit)
                        priorityList.remove(selectedUnit) // Remove unit after fighting
                        removeAllListeners(selectedUnit)
                        isgame1Turn = !isgame1Turn // Swap turn
                        selectNextFighter()
                    } else
                    {
                        removeAllListeners(selectedUnit)
                        priorityList.remove(selectedUnit)
                        selectNextFighter()
                    }
                }
            }
            selectNextFighter() // Start selection
        }
    }
}
private suspend fun selectUnitForFight(availableUnits: List<GameUnit>) =
    suspendCancellableCoroutine { continuation ->
        availableUnits.forEach { unit ->
            unit.physicalTroops.forEach { model ->
                model.view.setOnClickListener { view ->
                    view.isClickable = false // Prevent multiple triggers
                    removeAllListeners(unit) // Remove listeners immediately
                    continuation.resume(unit) // Return selected unit
                }
            }
        }
    }

