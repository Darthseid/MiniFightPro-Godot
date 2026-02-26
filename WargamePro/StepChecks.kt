package com.example.wargamesimulatorpro


import android.app.AlertDialog
import android.content.Context
import android.util.Log
import kotlinx.coroutines.*
import kotlin.coroutines.resume
import android.widget.*
import com.example.wargamesimulatorpro.SquadAbilities.chargeAfterRush
import com.example.wargamesimulatorpro.SquadAbilities.fightAfterMeleeDeath
import com.example.wargamesimulatorpro.SquadAbilities.moveAfterShooting
import com.example.wargamesimulatorpro.SquadAbilities.moveBack
import com.example.wargamesimulatorpro.SquadAbilities.reRollBravery
import com.example.wargamesimulatorpro.SquadAbilities.reanimator
import com.example.wargamesimulatorpro.SquadAbilities.satanic
import com.example.wargamesimulatorpro.SquadAbilities.tempMinusHitBrawl
import com.example.wargamesimulatorpro.WeaponAbilities.hardHits
import com.example.wargamesimulatorpro.WeaponAbilities.bonusHits1
import com.example.wargamesimulatorpro.WeaponAbilities.skirmishTemp
import com.example.wargamesimulatorpro.WeaponAbilities.heftyTemp
import com.example.wargamesimulatorpro.WeaponAbilities.hardHitsTemp
import com.example.wargamesimulatorpro.WeaponAbilities.bonusHits1Temp
import kotlinx.coroutines.suspendCancellableCoroutine

val usedOfficerOrder = SquadAbility("UOO", "Used Officer Order", 0, false)
fun cleanupTemporaryAbilities(unit: Squad): MutableList<SquadAbility>
{
    val cleanedSquadAbilities = unit.squadAbilities.filter { !it.isTemporary }
    unit.composition
        .flatMap { it.tools }
        .distinctBy { it.weaponName } // Ensure distinct weapon types
        .forEach { weapon ->
            weapon.special.removeIf {it.isTemporary}
        }
    return cleanedSquadAbilities.toMutableList()
}

suspend fun commandPhaseChecks(
    theContext: Context,
    player: SuperPlayer // Now takes a full player instead of a single squad
) = suspendCancellableCoroutine { continuation ->
   val commandAbilities = player.thePlayer.playerAbilities
    player.deployed.forEach { gameUnit ->
        val activeSquad = gameUnit.forces
        if (activeSquad.squadAbilities.any { it == reanimator })
            squadRegeneration(gameUnit, theContext)  // Apply reanimation ability if the squad has it
    }
    if (commandAbilities.contains("Officer Order") && !commandAbilities.contains("Used Officer Order") )
    {
        playSound(theContext, R.raw.bugle)             // Ensure the squad hasn't already used an Officer Order this turn
        player.deployed.forEach { gameUnit ->
            val activeSquad = gameUnit.forces         // Loop through each squad in player's deployed units
            val squadName = activeSquad.name  // Check if the player has the Officer Order ability
            if (!activeSquad.shellShock)
            {
                val commandOptions = arrayOf(
                    "Melee Maneuver! (+1 \uD83D\uDDE1\uFE0F for melee weapons)",
                    "Precision Fire! (+1 \uD83D\uDD2B for ranged weapons)",
                    "Volley Fire! (+1 \uD83E\uDD3A for Rapid Fire weapons)",
                    "Duck & Cover! (+1 \uD83D\uDEE1\uFE0F, max 3+)",
                    "Remain Steadfast! (-1 \uD83C\uDFF3\uFE0F)",
                    "Roll Out! (+3.0 \uD83C\uDFC3 )"
                )
                AlertDialog.Builder(theContext)
                    .setTitle("Choose an order for $squadName")
                    .setSingleChoiceItems(commandOptions, -1) { dialog, which ->
                        when (which) {
                            0 -> {
                                activeSquad.composition
                                    .flatMap { it.tools }
                                    .distinctBy { it.weaponName }
                                    .forEach { weapon ->
                                        if (weapon.isMelee) weapon.hitSkill = (weapon.hitSkill - 1).coerceAtLeast(2)
                                    }
                                activeSquad.squadAbilities.add(SquadAbility("", "activeMeleeBoost", 0, false))
                            }
                            1 -> {
                                activeSquad.composition
                                    .flatMap { it.tools }
                                    .distinctBy { it.weaponName }
                                    .forEach { weapon ->
                                        if (!weapon.isMelee) weapon.hitSkill = (weapon.hitSkill - 1).coerceAtLeast(2)
                                    }
                                activeSquad.squadAbilities.add(SquadAbility("", "activeAimBoost", 0, false))
                            }
                            2 -> {
                                activeSquad.composition
                                    .flatMap { it.tools }
                                    .distinctBy { it.weaponName }
                                    .forEach { weapon ->
                                        if (weapon.special.any { it.Name == "Rapid Fire" }) {
                                            val currentAttacks = weapon.attacks.toInt()
                                            weapon.attacks = (currentAttacks + 1).toString()
                                        }
                                    }
                                activeSquad.squadAbilities.add(SquadAbility("", "activeShootBoost", 0, false))
                            }
                            3 -> {
                                activeSquad.defense = (activeSquad.defense - 1).coerceAtLeast(3)
                                activeSquad.squadAbilities.add(SquadAbility("", "activeHeadsDown", 0, false))
                            }
                            4 -> {
                                activeSquad.bravery--
                                activeSquad.squadAbilities.add(SquadAbility("", "activeDuty", 0, false))
                            }
                            5 -> {
                                activeSquad.movement += 3f
                                activeSquad.squadAbilities.add(SquadAbility("", "activeMoveFast", 0, false))
                            }
                        }
                        commandAbilities.add("Used Officer Order")
                        dialog.dismiss()
                        continuation.resume(Unit) // Resume coroutine after selection is complete
                    }
                    .setCancelable(false)
                    .create()
                    .show()
            }
        }
    } else
        continuation.resume(Unit)
}


fun movementPhaseChecks(activeDude: Squad): Float
{
    return 0f
}

suspend fun chargePhaseChecks (theContext: Context, game0: GameUnit, enemyGameUnits: MutableList<GameUnit>): Float
{
    val activeDude = game0.forces
    val dudeName = activeDude.name
    val primeAbility = activeDude.squadAbilities
    val fightRange = checkFightRange(game0, enemyGameUnits, 1f)
    if(primeAbility.any {it == moveAfterShooting} && !fightRange)
    {
        Toast.makeText(theContext, "$dudeName can move again but can't charge if you move.", Toast.LENGTH_SHORT).show()
       val moveCheck =  movement(theContext, activeDude.movement, game0, enemyGameUnits)
        if(moveCheck.move)
        return -12f //This proved they moved. If they didn't move, they can still charge.
    }
    if (primeAbility.any { it.internal == "+Charge" })
        return primeAbility.find { it.internal == "+Charge" }?.Modifier?.toFloat() ?: 1f
    return 1f
}
suspend fun shootingPhaseChecks(context: Context, game0: GameUnit, enemyPlayer: SuperPlayer, activePlayer: SuperPlayer, playerBuffs: Boolean)
{
    val activeDude = game0.forces
    val fightRange = checkFightRange(game0, enemyPlayer.deployed, 1f)
    val moveRange = checkFightRange(game0, enemyPlayer.deployed, 9f)
    if (game0.forces.squadAbilities.any { it == moveBack } && !fightRange && moveRange)
    {
        val dudeName = game0.forces.name  // Handle "moveBack" ability
        val moveDistance = simpleRoll(6).toFloat()
        Toast.makeText(context, "$dudeName can move $moveDistance inches", Toast.LENGTH_SHORT).show()
        withContext(Dispatchers.Main) {
            moveStuff(game0, enemyPlayer.deployed, moveDistance)
        }
    } // Handle "satanic" ability
    if (satanic in activeDude.squadAbilities.map { it })
    {
        suspendCancellableCoroutine { continuation ->
            playSound(context, R.raw.demonlaugh)
            val options = arrayOf("Bonus Hits 1", "Hard Hits", "Nothing")
            AlertDialog.Builder(context)
                .setTitle("Satanic Prayer. Choose an option at a potential cost!")
                .setSingleChoiceItems(options, -1) { dialog, which ->
                    when (which)
                    {
                        0 -> {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(bonusHits1Temp) }
                            if (Roll2d6() < activeDude.bravery) {
                                allocatePure(context, simpleRoll(3), game0, activePlayer)
                                playSound(context, R.raw.perilous)
                            }
                        }
                        1 -> {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(hardHitsTemp) }
                            if (Roll2d6() < activeDude.bravery) {
                                allocatePure(context, simpleRoll(3), game0, activePlayer)
                                playSound(context, R.raw.perilous)
                            }
                        }
                        else -> {}
                    }
                    dialog.dismiss()
                    continuation.resume(Unit)
                }
                .setCancelable(false)
                .create()
                .show()
        }
    }
    if (activePlayer.thePlayer.playerAbilities.contains("Subroutines") && !playerBuffs)
    {
        suspendCancellableCoroutine{ continuation ->
            playSound(context, R.raw.subroutine)
            val options = arrayOf("Skirmish Ability", "Hefty Ability")
            AlertDialog.Builder(context)
                .setTitle("Choose a Subroutine for your guns!")
                .setSingleChoiceItems(options, -1)
                { dialog, which ->
                    when (which)
                    {
                        0 ->
                            {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(skirmishTemp) }
                        }
                        1 ->
                            {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(heftyTemp) }
                        }
                    }
                    dialog.dismiss()
                    continuation.resume(Unit)
                }
                .setCancelable(false)
                .create()
                .show()
        }
    }
}
suspend fun fightPhaseChecks(context: Context, game0: GameUnit, activePlayer: SuperPlayer)
{
    val activeDude = game0.forces
    val name = activeDude.name
    if (activePlayer.thePlayer.playerAbilities.contains("Martial Stances"))
    {
        suspendCancellableCoroutine { continuation ->
            playSound(context, R.raw.stance)
            val options = arrayOf("-1 to Hit", "Bonus Hits 1", "Hard Hits")
            AlertDialog.Builder(context)
                .setTitle("Martial Stance for $name")
                .setSingleChoiceItems(options, -1) { dialog, which ->
                    when (which)
                    {
                        0 -> activeDude.squadAbilities.add(tempMinusHitBrawl)
                        1 ->
                            {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(bonusHits1Temp) }
                        }
                        2 ->
                            {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(hardHitsTemp) }
                        }
                    }
                    dialog.dismiss()
                    continuation.resume(Unit) // Resume coroutine after dialog action
                }
                .setCancelable(false)
                .create()
                .show()
        }
    }

    if (satanic in activeDude.squadAbilities.map { it })
    {
        suspendCancellableCoroutine { continuation ->
            playSound(context, R.raw.demonlaugh)
            val options = arrayOf("Bonus Hits 1", "Hard Hits", "Nothing")
            AlertDialog.Builder(context)
                .setTitle("Satanic Prayer for $name")
                .setSingleChoiceItems(options, -1) { dialog, which ->
                    when (which) {
                        0 -> {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(bonusHits1Temp) }
                            if (Roll2d6() < activeDude.bravery)
                            {
                                allocatePure(context, simpleRoll(3), game0, activePlayer)
                                playSound(context, R.raw.perilous)
                            }
                        }
                        1 -> {
                            activeDude.composition
                                .flatMap { it.tools }
                                .distinctBy { it.weaponName }
                                .forEach { weapon -> weapon.special.add(hardHitsTemp) }
                            if (Roll2d6() < activeDude.bravery)
                            {
                                allocatePure(context, simpleRoll(3), game0, activePlayer)
                                playSound(context, R.raw.perilous)
                            }
                        }
                        else -> { }
                    }
                    dialog.dismiss()
                    continuation.resume(Unit) // Resume coroutine after dialog action
                }
                .setCancelable(false)
                .create()
                .show()
        }
    }
}

fun endOfRoundChecks (activeDude: Squad)
{
    activeDude.squadAbilities.remove(usedOfficerOrder)
    if (activeDude.squadAbilities.any { it.Name == "activeAimBoost" })
    {
        activeDude.composition
            .flatMap { it.tools }
            .filter { !it.isMelee } // Ranged weapons only
            .distinctBy { it.weaponName } // Ensure distinct weapon types
            .forEach { weapon ->
                weapon.hitSkill += 1 // Undoing Take Aim
            }
        activeDude.squadAbilities.removeIf { it.Name == "activeAimBoost" }
    }

    if (activeDude.squadAbilities.any { it.Name == "activeFightBoost" })
    {
        activeDude.composition
            .flatMap { it.tools }
            .filter { it.isMelee } // Melee weapons only
            .distinctBy { it.weaponName } // Ensure distinct weapon types
            .forEach { weapon ->
                weapon.hitSkill += 1 // Undoing Fix Bayonet
            }
        activeDude.squadAbilities.removeIf { it.Name == "activeFightBoost" }
    }
    if (activeDude.squadAbilities.any { it.Name == "activeShootBoost" })
    {
        activeDude.composition
            .flatMap { it.tools }
            .filter { it.special.any { ability -> ability.Name == "Rapid Fire" } }
            .distinctBy { it.weaponName } // Ensure distinct weapon types
            .forEach { weapon ->
                weapon.attacks = (damageParser(weapon.attacks) - 1).toString()
            }
        activeDude.squadAbilities.removeIf { it.Name == "activeShootBoost" }
        Log.d("WSF","Shoot Boost Removed")
    }
    if (activeDude.squadAbilities.any { it.Name == "activeHeadsDown" })
    {
        activeDude.defense++
        activeDude.squadAbilities.removeIf { it.Name == "activeHeadsDown" }
    }
    if (activeDude.squadAbilities.any { it.Name == "activeDuty" })
    {
        activeDude.bravery++
        activeDude.squadAbilities.removeIf { it.Name == "activeDuty" }
    }
    if (activeDude.squadAbilities.any { it.Name == "activeMoveFast" })
    {
        activeDude.movement -= 3f
        activeDude.squadAbilities.removeIf { it.Name == "activeMoveFast" }
    }
    if (activeDude.squadAbilities.any { it.Name == "improvedOrk \uD83D\uDD2E" })
    {
        activeDude.dodge = 7
        activeDude.squadAbilities.removeIf { it.Name == "improvedOrk \uD83D\uDD2E" }
    }
    if (activeDude.squadAbilities.any { it.Name == "buffStrengthAndAttack" })
    {
        activeDude.composition
            .flatMap { it.tools }
            .filter { it.isMelee } // Melee weapons only
            .distinctBy { it.weaponName } // Ensure distinct weapon types
            .forEach { weapon ->
                weapon.strength -= 1
                weapon.attacks = (damageParser(weapon.attacks) - 1).toString()
            }
        activeDude.squadAbilities.removeIf { it.Name == "buffStrengthAndAttack" }
        Log.d("WSF","berserking removed")
    }
    if (activeDude.squadAbilities.any { it.Name == "BlessingsActivated" })
    {
        if (activeDude.squadAbilities.any { it.Name == "➗ Boost1" })
            activeDude.damageResistance++
        if (activeDude.squadAbilities.any { it.Name == "Move2" })
            activeDude.movement -= 2f
        activeDude.composition
            .flatMap { it.tools }
            .filter { it.isMelee } // Melee weapons only
            .distinctBy { it.weaponName } // Ensure distinct weapon types
            .forEach { weapon ->
                weapon.special.remove(bonusHits1)
                weapon.special.remove(hardHits)
            }
    }
    if (activeDude.squadAbilities.any { it.Name == "BlessingsActivated" } )
    {
        activeDude.squadAbilities.remove(chargeAfterRush)
        activeDude.squadAbilities.remove(fightAfterMeleeDeath)
        activeDude.squadAbilities.removeIf { it.Name == "BlessingsActivated" }
        activeDude.squadAbilities.removeIf { it.Name == "Move2" }
        activeDude.squadAbilities.removeIf { it.Name == "➗ Boost1" }
        Log.d("WSF","Blessings Removed")
    }
}

suspend fun generateBlessings(theContext: Context, activeDude: Squad) = suspendCancellableCoroutine { continuation ->
    val rolls = MutableList(8) { simpleRoll(6) }
    var blessLimit = 0
    playSound(theContext, R.raw.battle_cry)
    val validBlessings = mutableListOf<Pair<String, () -> Unit>>()
    fun removeOccurrences(value: Int, count: Int)
    {
        repeat(count) { rolls.remove(value) }
    }
    if (rolls.count { it == 6 } >= 2)      // Determine valid blessings
    {
        validBlessings.add("Charge After Rushing (Double 6)" to
                {
            activeDude.squadAbilities.add(chargeAfterRush)
            removeOccurrences(6, 2)
        })
    }
    if (rolls.count { it >= 4 } >= 3) {
        validBlessings.add("Charge After Rushing (Triple 4)" to
                {
            activeDude.squadAbilities.add(chargeAfterRush)
            removeOccurrences(4, 3)
        })
    }
    if (rolls.count { it >= 5 } >= 2) {
        validBlessings.add("Melee Hard Hits (Double 5)" to
                {
            activeDude.composition.flatMap { it.tools }
                .filter { it.isMelee }
                .distinct()
                .forEach { weapon -> weapon.special.add(hardHits) }
            removeOccurrences(5, 2)
        })
    }
    if (rolls.count { it >= 4 } >= 2) {
        validBlessings.add("Fight After Melee Death (Double 4)" to
                {
            activeDude.squadAbilities.add(fightAfterMeleeDeath)
            removeOccurrences(4, 2)
        })
    }
    if (rolls.groupBy { it }.values.any { it.size >= 3 })
    {
        validBlessings.add("Melee Hard Hits (Triple Any)" to {
            activeDude.composition.flatMap { it.tools }
                .filter { it.isMelee }
                .distinct()
                .forEach { weapon -> weapon.special.add(hardHits) }
            rolls.groupBy { it }.values.find { it.size >= 3 }?.let { removeOccurrences(it.first(), 3) }
        })
    }
    if (rolls.count { it >= 3 } >= 2) {
        validBlessings.add("Bonus Hits (Double 3)" to {
            activeDude.composition.flatMap { it.tools }
                .filter { it.isMelee }
                .distinct()
                .forEach { weapon -> weapon.special.add(bonusHits1) }
            removeOccurrences(3, 2)
        })
    }
    if (rolls.groupBy { it }.values.any { it.size >= 2 }) {
        validBlessings.add("Damage Resistance (Double Any)" to {
            activeDude.damageResistance -= 1
            activeDude.squadAbilities.add(SquadAbility("", "➗ Boost1", 0, false))
            rolls.groupBy { it }.values.find { it.size >= 2 }?.let { removeOccurrences(it.first(), 2) }
        })
    }
    if (rolls.groupBy { it }.values.any { it.size >= 2 })
    {
        validBlessings.add("Movement Boost (+2 Movement)" to
                {
            activeDude.movement += 2f
            activeDude.squadAbilities.add(SquadAbility("", "Move2", 0, false))
            rolls.groupBy { it }.values.find { it.size >= 2 }?.let { removeOccurrences(it.first(), 2) }
        })
    }
    if (validBlessings.isNotEmpty())
    {
        val unholyName = activeDude.name // Display blessings selection dialog
        val blessingNames = validBlessings.map { it.first }
        val selectedBlessings = mutableListOf<Int>()
        AlertDialog.Builder(theContext)
            .setTitle("Select up to 2 Blessings for $unholyName")
            .setMultiChoiceItems(blessingNames.toTypedArray(), null) { _, which, isChecked ->
                if (isChecked)
                {
                    if (selectedBlessings.size < 2)
                        selectedBlessings.add(which)
                     else
                        Toast.makeText(theContext, "You can only select up to 2 blessings!", Toast.LENGTH_SHORT).show()
                } else
                    selectedBlessings.remove(which)
            }
            .setPositiveButton("Activate") { _, _ ->
                selectedBlessings.forEach { index ->
                    validBlessings[index].second()
                    blessLimit++
                }
                continuation.resume(Unit) // Resume coroutine after blessings are selected
            }
            .setNegativeButton("Cancel") { _, _ ->
                continuation.resume(Unit) // Resume coroutine even if the dialog is cancelled
            }
            .setCancelable(false)
            .create()
            .show()
    } else
    {
        Toast.makeText(theContext, "No valid blessings available!", Toast.LENGTH_SHORT).show()
        continuation.resume(Unit) // Resume coroutine when no blessings are available
    }
}

suspend fun berserking(theContext: Context, play: Player) = suspendCancellableCoroutine { continuation ->
    playSound(theContext, R.raw.battle_cry)
    val commander = play.playerName
    createConfirmationDialog(theContext, "Do you want to activate Berserking this round for $commander?") { result ->
        if (result)
        {
            play.theirSquads.forEach { activeDude ->
                activeDude.squadAbilities.add(SquadAbility("", "buffStrengthAndAttack", 0, false))
                activeDude.squadAbilities.add(chargeAfterRush)
                if (activeDude.dodge > 5)
                {
                    activeDude.squadAbilities.add(SquadAbility("", "improvedOrkInvuln", 0, false))
                    activeDude.dodge = 5
                }
                activeDude.composition
                    .flatMap { it.tools }
                    .filter { it.isMelee }
                    .distinctBy { it.weaponName }
                    .forEach { weapon ->
                        weapon.strength += 1
                        weapon.attacks = (damageParser(weapon.attacks) + 1).toString()
                    }
            }
        }
        continuation.resume(Unit) // Resume the coroutine after completing the berserking logic
    }
}

fun shellShockTest (theContext: Context, game0: GameUnit, inactivePlayer: SuperPlayer, activeAbilities: MutableList<String>): Boolean
{
    val activeGuy = game0.forces
    val baseBravery = activeGuy.bravery
    val unitAbilities = activeGuy.squadAbilities
    var shellShockModifier = 0
    if (activeAbilities.contains("Demonic Grief"))
        shellShockModifier += 1
    if (inactivePlayer.thePlayer.playerAbilities.contains("Demonic Grief"))
        shellShockModifier -= 1
    if (activeAbilities.contains("Hive Mind"))
        shellShockModifier += simpleRoll(6)
    if (inactivePlayer.thePlayer.playerAbilities.contains("Demonic Grief") && checkFightRange(game0, inactivePlayer.deployed, 12f))
        shellShockModifier -= 1
    Log.d("WSF","ShellshockModifier is  $shellShockModifier")
    var SSTest = Roll2d6() + shellShockModifier < baseBravery
    if(SSTest && unitAbilities.any { it == reRollBravery } )
        SSTest = Roll2d6() + shellShockModifier < baseBravery
    if (SSTest)
        playSound(theContext, R.raw.failedbravery)
    if (activeAbilities.contains("Demonic Grief") && !SSTest)
        squadRegeneration(game0, theContext)
    Log.d("WSF","Did Shellshock fail? $SSTest")
    return SSTest
}