package com.example.wargamesimulatorpro

import android.os.*

data class Squad(
    val name: String,
    var movement: Float,
    val hardness: Int,
    var defense: Int,
    var dodge: Int,
    var damageResistance: Int,
    var bravery: Int,
    val squadType: List<String>, // Changed from a single string to a list of strings
    var shellShock: Boolean = false, // No unit starts out shellshocked
    val composition: MutableList<Model>,
    val startingModelSize: Int,
    var squadAbilities: MutableList<SquadAbility>
) : Parcelable
{
    constructor(parcel: Parcel) : this(
        name = parcel.readString() ?: "",
        movement = parcel.readFloat(),
        hardness = parcel.readInt(),
        defense = parcel.readInt(),
        dodge = parcel.readInt(),
        damageResistance = parcel.readInt(),
        bravery = parcel.readInt(),
        squadType = mutableListOf<String>().apply {
            parcel.readStringList(this) // Read squadType as a list of strings
        },
        shellShock = parcel.readByte() != 0.toByte(),
        composition = mutableListOf<Model>().apply {
            parcel.readTypedList(this, Model.CREATOR)
        },
        startingModelSize = parcel.readInt(),
        squadAbilities = mutableListOf<SquadAbility>().apply {
            parcel.readTypedList(this, SquadAbility.CREATOR)
        }
    )

    override fun writeToParcel(parcel: Parcel, flags: Int)
    {
        parcel.writeString(name)
        parcel.writeFloat(movement)
        parcel.writeInt(hardness)
        parcel.writeInt(defense)
        parcel.writeInt(dodge)
        parcel.writeInt(damageResistance)
        parcel.writeInt(bravery)
        parcel.writeStringList(squadType) // Write squadType as a list of strings
        parcel.writeByte(if (shellShock) 1 else 0)
        parcel.writeTypedList(composition)
        parcel.writeInt(startingModelSize)
        parcel.writeTypedList(squadAbilities)
    }

    override fun describeContents(): Int = 0

    companion object CREATOR : Parcelable.Creator<Squad> {
        override fun createFromParcel(parcel: Parcel): Squad = Squad(parcel)
        override fun newArray(size: Int): Array<Squad?> = arrayOfNulls(size)
    }

    fun deepCopy(): Squad
    {
        return Squad(
            name = this.name,
            movement = this.movement,
            hardness = this.hardness,
            defense = this.defense,
            dodge = this.dodge,
            damageResistance = this.damageResistance,
            bravery = this.bravery,
            squadType = this.squadType.toList(), // Create a new list to avoid mutability issues
            shellShock = this.shellShock,
            composition = this.composition.map { it.deepCopy() }.toMutableList(),
            startingModelSize = composition.size,
            squadAbilities = this.squadAbilities.map { it.copy() }.toMutableList()
        )
    }
}

data class SquadAbility(
    val internal: String,
    val Name: String,
    val Modifier: Int,
    val isTemporary: Boolean
) : Parcelable {

    constructor(parcel: Parcel) : this(
        internal = parcel.readString() ?: "",
        Name = parcel.readString() ?: "",
        Modifier = parcel.readInt(),
        isTemporary = parcel.readByte() != 0.toByte()
    )

    override fun writeToParcel(parcel: Parcel, flags: Int)
    {
        parcel.writeString(internal)  // Write internal property
        parcel.writeString(Name)
        parcel.writeInt(Modifier)
        parcel.writeByte(if (isTemporary) 1 else 0)
    }

    override fun describeContents(): Int = 0

    companion object CREATOR : Parcelable.Creator<SquadAbility> {
        override fun createFromParcel(parcel: Parcel): SquadAbility = SquadAbility(parcel)
        override fun newArray(size: Int): Array<SquadAbility?> = arrayOfNulls(size)
    }
}
@Suppress("unused")
object SquadAbilities
{
    val minusHitRanged = SquadAbility("-1 Shoot", "-1 to Hit Ranged", 1, false)
    val minusHitRangedTemp = SquadAbility("Temp -1 Shoot", "Temp minus Hit Ranged", 1, true)
    val minusHitBrawl = SquadAbility("-1 Fight", "-1 to Hit Melee", 0, false)
    val tempMinusHitBrawl = SquadAbility("-1 Fight", "Temp minus Hit Brawl", 0, true)
    val deathExplode1 = SquadAbility("Explodes", "Explode on Death 1", 1, false)
    val deathExplode2 = SquadAbility("Explodes", "Explode on Death 2", 2, false)
    val deathExplode3 = SquadAbility("Explodes", "Explode on Death 3", 3, false)
    val minusHit = SquadAbility("-1 All", "-1 to Hit", 0, false)
    val minusHitTemp = SquadAbility("-1 All", "Temp minus Hit", 0, true)
    val closeUptoShoot = SquadAbility("12 inch or bust", "Camouflaged", 0, false)
    val closeUptoShootTemp = SquadAbility("12 inch or bust", "Temp close Up to Shoot", 0, true)
    val firstStrike = SquadAbility("Hit First", "First Strike", 0, false)
    val firstStrikeTemp = SquadAbility("Hit First", "Temp First Strike", 0, true)
    val shootRetreat = SquadAbility("FleeShoot", "Shoot After Retreat", 0, false)
    val tempShootRetreat = SquadAbility("FleeShoot", "Shoot After Retreat", 0, true)
    val chargeAfterRush = SquadAbility("DashBash", "Charge After Rush", 0, false)
    val tempChargeAfterRush = SquadAbility("DashBash", "Temp Charge After Rush", 0, true)
    val fightAfterMeleeDeath = SquadAbility("Hit@End", "Fight After Melee Death", 2, false)
    val tempFightAfterMeleeDeath = SquadAbility("Hit@End", "Temp Fight After Melee Death", 2, true)
    val selfRessurection = SquadAbility("2nd Life", "Ressurect", 1, false)
    val selfRessurectionTemp = SquadAbility("2nd Life", "Temp Ressurect", 1, true)
    val psiDefense = SquadAbility("BrainBlock", "Psionic Defense", 0, false)
    val psiDefenseTemp = SquadAbility("BrainBlock", "Temp Psionic Defense", 0, true)
    val pureDefense = SquadAbility("Special Def", "Pure Damage defense", 2, false)
    val pureDefenseTemp = SquadAbility("Special Def", "Temp pure Damage defense", 2, true)
    val teleport = SquadAbility("Tele", "Teleport", 2, false)
    val teleportTemp = SquadAbility("Tele", "Temp Teleport", 2, true)
    val shootShellshock = SquadAbility("ScareFire", "Shoot shellShock", 2, false)
    val shootShellshockTemp = SquadAbility("ScareFire", "Temp Shoot shellShock", 2, true)
    val officerOrder = SquadAbility("OO", "Officer Order", 0, false)
    val reanimator = SquadAbility("Zombie", "Regeneration", 0, false)
    val reanimatorTemp = SquadAbility("Zombie", "Temp Reanimator", 0, true)
    val advBoost1 = SquadAbility("+Rush", "+1 Rush Boost", 1, false)
    val advBoost1Temp = SquadAbility("+Rush", "Temp Rush Boost 1", 0, true)
    val advBoost6 = SquadAbility("Super Advance", "Turbo Rush", 6, false)
    val advBoost6Temp = SquadAbility("Super Advance", "Turbo Rush", 6, true)
    val stampede = SquadAbility("Crush", "Stampede", 0, false)
    val stampedeTemp = SquadAbility("Crush", "Stampede", 0, true)
    val plusOneToCharge = SquadAbility("+Charge", "+1 Charge Bonus", 1, false)
    val plusOneToChargeTemp = SquadAbility("+Charge", "Temp Charge Bonus 1", 1, true)
    val plus2ToCharge = SquadAbility("+Charge", "+2 Charge Bonus", 2, false)
    val plus3ToCharge = SquadAbility("+Charge", "+3 Charge Bonus", 3, false)
    val satanic = SquadAbility("Satan", "Satanic", 1, false)
    val satanicTemp = SquadAbility("Temp Satan", "Temp Satanic", 1, true)
    val reRollBravery = SquadAbility("TryAgain", "Reroll Bravery", 1, false)
    val reRollBraveryTemp = SquadAbility("Temp TryAgain", "Temp reroll Bravery", 1, true)
    val infect = SquadAbility("AIDS", "Infect", 1, false)
    val infectTemp = SquadAbility("Temp AIDS", "Temp Infect", 1, true)
    val resistFirstDamage = SquadAbility("stopfirsthit", "Resist First Damage", 1, false)
    val resistFirstDamageTemp = SquadAbility("Temp stopfirsthit", "Temp Resist First Damage", 1, true)
    val weakenStrongAttack = SquadAbility("4s Please", "Weaken Strong Attack", 1, false)
    val weakenStrongAttackTemp = SquadAbility("Temp 4s Please", "Temp Weaken Strong Attack", 1, true)
    val reduceDamageBy1 = SquadAbility("Less1", "-1 Damage", 1, false)
    val reduceDamageBy1Temp = SquadAbility("Temp Less1", "Temp -1 Damage", 1, true)
    val reduceDamageByHalf = SquadAbility("Less2", "÷2 Damage", 1, false)
    val reduceDamageByHalfTemp = SquadAbility("Temp Less2", "Temp ÷2 Damage", 1, true)
    val reduceDamageto1 = SquadAbility("Less3", "Only 1 Damage", 1, false)
    val reduceDamageto1Temp = SquadAbility("Temp Less3", "Temp Only 1 Damage", 1, true)
    val moveAfterShooting = SquadAbility("GunRun", "Move After Shooting", 1, false)
    val moveAfterShootingTemp = SquadAbility("Temp GunRun", "Temp Move After Shooting", 1, true)
    val moveBack = SquadAbility("Run Away", "Move after enemy.", 1, false)
    val moveBackTemp = SquadAbility("Temp Run Away", "Move after enemy.", 1, true)
    val squadrerollHits = SquadAbility("Pow1", "Hits are rerolled", 0, false)
    val squadrerollHitsTemp = SquadAbility("Temp Pow1", "Temp All hits are rerolled", 0, true)
    val squadrerollHitOnes = SquadAbility("Pow2", "Hit rolls 1 rerolled.", 0, false)
    val squadrerollHitOnesTemp = SquadAbility("Temp Pow2", "Temp Hit rolls of 1 are rerolled.", 0, true)
    val squadrerollInjuryOnes = SquadAbility("Pow3", "Injury rolls 1 rerolled", 0, false)
    val squadrerollInjuryOnesTemp = SquadAbility("Temp Pow3", "Temp Injury rolls of 1 are rerolled", 0, true)
    val squadplusOneInjuries = SquadAbility("Pow4", "+1 Injury Rolls", 0, false)
    val squadplusOneInjuriesTemp = SquadAbility("Temp Pow4", "Temp +1 to Injury Rolls", 0, true)
    val squadrerollInjuries = SquadAbility("Pow5", "Injuries rerolled", 0, false)
    val squadrerollInjuriesTemp = SquadAbility("Temp Pow5", "Temp All Injury rolls rerolled", 0, true)
    val stopRerolls = SquadAbility("Pow0", "No rerolls", 0, false)
    val stopRerollsTemp = SquadAbility("Temp Pow0", "Temp No rerolls against this Squad", 0, true)
    val noModifiers = SquadAbility("Pow-1", "Ignore negative Modifiers", 0, false)
    val noModifiersTemp = SquadAbility("Temp Pow-1", "Temp Ignore negative Modifiers", 0, true)
}