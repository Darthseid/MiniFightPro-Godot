package com.example.wargamesimulatorpro

import android.content.Context
import android.widget.Toast
import kotlinx.coroutines.*

suspend fun combat(theContext: Context, activeSuperPlayer: SuperPlayer, inactiveSuperPlayer: SuperPlayer) = coroutineScope {
    Toast.makeText(theContext, "${activeSuperPlayer.thePlayer.playerName}'s Turn", Toast.LENGTH_SHORT).show()
    var currentPhase = "StartingPhase"
    distanceTextView.text = "STARTING PHASE"
    delay(2000)     // Perform command phase checks for all squads
    commandPhaseChecks(theContext, activeSuperPlayer)     // Get enemy abilities
    val dudeAbilities = activeSuperPlayer.thePlayer.playerAbilities
    val badguyAbilities = inactiveSuperPlayer.thePlayer.playerAbilities   // Loop through each squad in game1 (active player)
    val activeDudes = activeSuperPlayer.deployed
    val enemyDudes = inactiveSuperPlayer.deployed
    activeDudes.forEach { activeSquad ->
        activeSquad.forces.shellShock = false  // Reset shellshock at start of turn
        if (activeSquad.forces.composition.size < activeSquad.forces.startingModelSize / 2f)
            activeSquad.forces.shellShock = shellShockTest(theContext, activeSquad, inactiveSuperPlayer, dudeAbilities)    // Shellshock test if squad is below 50% strength
        if (activeSquad.forces.composition.any { it.health < it.startingHealth / 2f } && activeSquad.forces.startingModelSize == 1)
            activeSquad.forces.shellShock = shellShockTest(theContext, activeSquad, inactiveSuperPlayer, dudeAbilities)   // Additional shellshock test for single-model squads that are injured
        if (badguyAbilities.contains("Demonic Grief") && activeSquad.forces.shellShock)
            allocatePure(theContext, simpleRoll(3), activeSquad, activeSuperPlayer)    // Apply "Demonic Grief" ability if applicable
    }
    if (badguyAbilities.contains("Alien Terror"))
            activateAlienTerror(theContext, activeSuperPlayer,inactiveSuperPlayer)
    currentPhase = "MovementPhase"
    ContinueGame(theContext)
    playSound(theContext, R.raw.startmovement)
    distanceTextView.text = "Movement. Select Units for moving."
    delay(2000)
movementPhase(theContext, activeSuperPlayer, inactiveSuperPlayer)
    distanceTextView.text = "Shooting. Select Units for shooting and their targets."
    playSound(theContext, R.raw.startshooting)
    currentPhase = "ShootingPhase"
    delay(2000)
    var playerBuffs0 = false //This is to prevent armywide abilities from triggering more than once.
    activeDudes.forEach { activeSquad ->
        shootingPhaseChecks(theContext, activeSquad, inactiveSuperPlayer, activeSuperPlayer, playerBuffs0)
        playerBuffs0 = true
                        }
shootingPhaseSelection(theContext, activeSuperPlayer, inactiveSuperPlayer)
    activeDudes.forEach { activeSquad ->
        removeDeadModels(activeSquad, theContext) //This is purely for Resurrection checks.
        activeSquad.forces.squadAbilities = cleanupTemporaryAbilities(activeSquad.forces)
    }
    enemyDudes.forEach { inActiveSquad ->
        removeDeadModels(inActiveSquad, theContext) //This is also for resurrection and fight after death.
    }
    checkVictory(theContext)
    distanceTextView.text = "Charge. Select Units for charging and their targets."
    playSound(theContext, R.raw.charge)
    currentPhase = "ChargePhase"
    delay(2000)
chargePhaseSelection(theContext, activeSuperPlayer, inactiveSuperPlayer)
    checkVictory(theContext)
            playSound(theContext, R.raw.startfight)
    distanceTextView.text = "Fight. Alternate selecting fight units."
            currentPhase = "FightPhase"
    fightPhaseSelection(theContext, activeSuperPlayer, inactiveSuperPlayer)
        activeSuperPlayer.deployed.forEach { activeSquad ->
            var bilities = activeSquad.forces.squadAbilities
            bilities = cleanupTemporaryAbilities(activeSquad.forces)
            removeDeadModels(activeSquad, theContext)
        }
    enemyDudes.forEach { inActiveSquad ->
        removeDeadModels(inActiveSquad, theContext) //This is also for resurrection and fight after death.
    }
    playSound(theContext,R.raw.turnover)
}