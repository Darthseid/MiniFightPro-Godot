package com.example.wargamesimulatorpro

data class MoveVars(
    var move: Boolean,
    var advance: Boolean,
    var retreat: Boolean,
)

data class DiceModifiers(
    var hitMod: Int,
    var woundMod: Int,
    var hitReroll: Int,
    var woundReroll: Int,
    var defenseMod: Int,
    var critThreshold: Int,
    var antiThreshold: Int
)