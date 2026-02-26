package com.example.wargamesimulatorpro

import android.content.Context
import android.widget.Toast
import kotlinx.coroutines.*
import java.util.Random

suspend fun playMatch(theContext: Context, dropDown1: SuperPlayer, dropDown2: SuperPlayer) = coroutineScope {
    while (isActive)
    {
        val rand = Random()
        val turnOrder = rand.nextBoolean()
        playSound(theContext, R.raw.startbattle)
        Toast.makeText(theContext, "Pre-game Deployment", Toast.LENGTH_SHORT).show()
        dropDown1.deployed.forEach { unit -> teleport(theContext, unit, dropDown2.deployed) }        // Iterate through all units of dropDown1 and teleport them
        dropDown2.deployed.forEach { unit -> teleport(theContext, unit, dropDown1.deployed) }    // Iterate through all units of dropDown2 and teleport them
        val firstPlayer: SuperPlayer = if (turnOrder) dropDown1 else dropDown2
        val secondPlayer: SuperPlayer = if (turnOrder) dropDown2 else dropDown1
        Toast.makeText(theContext, "${firstPlayer.thePlayer.playerName} acts first", Toast.LENGTH_SHORT).show()
        playSound(theContext, R.raw.roundbell)
        var gameOver = false
        while (!gameOver)
        {
            otherTurns(theContext, firstPlayer, secondPlayer)
            gameOver = (dropDown1.deployed.isEmpty() || dropDown2.deployed.isEmpty())
        }
    }
}

suspend fun otherTurns(theContext: Context, super1: SuperPlayer, super2: SuperPlayer) = coroutineScope {
    val p1 = super1.thePlayer
    val p2 = super2.thePlayer
    BattleActivity.turn++
    if (BattleActivity.turn > 2)
    {
        BattleActivity.turn -= 2
        BattleActivity.round++
        playSound(theContext, R.raw.roundbell)
    }
    Toast.makeText(theContext, "Round: ${BattleActivity.round} Turn: ${BattleActivity.turn}", Toast.LENGTH_SHORT).show()
    val activePlayer = if (BattleActivity.turn % 2 == 1) super1 else super2
    val inactivePlayer = if (super2 == activePlayer) super1 else super2
    if (p1.playerAbilities.contains("Warrior Bless") && BattleActivity.turn == 1)
       super1.deployed.forEach{ unit ->  generateBlessings(theContext, unit.forces)}
    if (p2.playerAbilities.contains("Warrior Bless") && BattleActivity.turn == 1)
        super2.deployed.forEach{ unit ->  generateBlessings(theContext, unit.forces)}
    if (p1.playerAbilities.contains("Berserk") && BattleActivity.turn == 1)
    {
        berserking(theContext, p1)
        p2.playerAbilities.remove("Berserk")
    }
    if (p2.playerAbilities.contains("Berserk") && BattleActivity.turn == 1)
    {
        berserking(theContext, p2)
        p2.playerAbilities.remove("Berserk")
    }
    delay(2000)
    combat(theContext, activePlayer, inactivePlayer)
        if (BattleActivity.turn == 2) //Two turns per round
        {
            super1.deployed.forEach{ unit ->  endOfRoundChecks(unit.forces)}
            super2.deployed.forEach{ unit ->  endOfRoundChecks(unit.forces)}
        }
}

