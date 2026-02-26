package com.example.wargamesimulatorpro

import android.os.*
import kotlinx.parcelize.Parcelize

@Parcelize
data class Order(
    var orderCost: Int,
    val orderName: String,
    val availablePhase: String,
    val targetsEnemy: Boolean,
    val description: String
) : Parcelable

