package com.example.wargamesimulatorpro

import android.os.*
import kotlinx.parcelize.Parcelize

@Parcelize
data class Player(
    val theirSquads: MutableList<Squad>,
    val orderPoints: Int,
    val orders: MutableList<Order>,
    val isAI: Boolean,
    val playerName: String,
    val playerAbilities: MutableList<String>,

) : Parcelable

data class SuperPlayer(
    var thePlayer: Player,
    val deployed: MutableList<GameUnit>,
)

object PlayerAbilities
{
    val hiveMind = "Hive Mind"
    val alienTerror = "Alien Terror"
    val grim = "Grim"
    val warriorBless = "Warrior Bless"
    val martial = "Martial Stances"
    val berserk = "Berserk"
    val grief = "Demonic Grief"
    val subroutines = "Subroutines"
}