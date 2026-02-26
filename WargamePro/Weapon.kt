package com.example.wargamesimulatorpro

import android.os.*
data class Weapon(
    val weaponName: String,
    val range: Float,
    var attacks: String,
    var hitSkill: Int,
    var strength: Int, // Use camelCase for consistency
    val armorPenetration: Int,
    val damage: String,
    val special: MutableList<WeaponAbility>,
) : Parcelable {
    val isMelee: Boolean
        get() = range <= 1f // Computed property to check if the weapon is melee
    constructor(parcel: Parcel) : this(
        weaponName = parcel.readString() ?: "",
        range = parcel.readFloat(),
        attacks = parcel.readString() ?: "",
        hitSkill = parcel.readInt(),
        strength = parcel.readInt(),
        armorPenetration = parcel.readInt(),
        damage = parcel.readString() ?: "",
        special = mutableListOf<WeaponAbility>().apply {
            parcel.readTypedList(this, WeaponAbility.CREATOR)
        }
    )
    override fun writeToParcel(parcel: Parcel, flags: Int) {
        parcel.writeString(weaponName)
        parcel.writeFloat(range)
        parcel.writeString(attacks)
        parcel.writeInt(hitSkill)
        parcel.writeInt(strength)
        parcel.writeInt(armorPenetration)
        parcel.writeString(damage)
        parcel.writeTypedList(special)
    }

    override fun describeContents(): Int = 0

    companion object CREATOR : Parcelable.Creator<Weapon> {
        override fun createFromParcel(parcel: Parcel): Weapon = Weapon(parcel)
        override fun newArray(size: Int): Array<Weapon?> = arrayOfNulls(size)
    }
}
data class WeaponAbility(
    val internal: String,      // Internal identifier for the ability
    val Name: String,          // Display name of the ability
    val Modifier: Int,         // Numeric modifier associated with the ability
    val isTemporary: Boolean   // Whether the ability is temporary
) : Parcelable {
    constructor(parcel: Parcel) : this(
        internal = parcel.readString() ?: "",
        Name = parcel.readString() ?: "",
        Modifier = parcel.readInt(),
        isTemporary = parcel.readByte() != 0.toByte()
    )

    override fun writeToParcel(parcel: Parcel, flags: Int) {
        parcel.writeString(internal)  // Write internal property
        parcel.writeString(Name)      // Write Name property
        parcel.writeInt(Modifier)     // Write Modifier property
        parcel.writeByte(if (isTemporary) 1 else 0) // Write isTemporary property
    }

    override fun describeContents(): Int = 0

    companion object CREATOR : Parcelable.Creator<WeaponAbility> {
        override fun createFromParcel(parcel: Parcel): WeaponAbility = WeaponAbility(parcel)
        override fun newArray(size: Int): Array<WeaponAbility?> = arrayOfNulls(size)
    }
}

@Suppress("unused")
object WeaponAbilities
{
    val bonusHits1 = WeaponAbility("Bonus Hits", "Bonus Hits 1", 1, false)
    val bonusHits1Temp = WeaponAbility("Bonus Hits", "TBonus Hits", 1, true)
    val bonusHits2 = WeaponAbility("Bonus Hits", "Bonus Hits 2", 2, false)
    val bonusHits2Temp = WeaponAbility("Bonus Hits", "Temp Bonus Hits 2", 2, true)
    val bonusHits3 = WeaponAbility("Bonus Hits", "Bonus Hits 3", 3, false)
    val bonusHits3Temp = WeaponAbility("Bonus Hits", "Temp Bonus Hits 3", 3, true)
    val precision = WeaponAbility("rareFirst", "Precision", 0, false)
    val tempPrecision = WeaponAbility("rareFirst", "Temp Precision", 0, true)
    val hardHits = WeaponAbility("HH", "Hard Hits", 0, false)
    val hardHitsTemp = WeaponAbility("HH", "Temp Hard Hits", 0, true)
    val pike = WeaponAbility("HH", "Pike", 0, false)
    val pikeTemp = WeaponAbility("HH", "Temp Pike", 0, true)
    val conversion = WeaponAbility("Convert", "Conversion", 0, false)
    val conversionTemp = WeaponAbility("Convert", "Temp Conversion", 0, true)
    val devastatingInjuries = WeaponAbility("DI", "Devastating Injuries", 0, false)
    val devastatingInjuriesTemp = WeaponAbility("DI", "Temp Devastating Injuries", 0, true)
    val perilous = WeaponAbility("Self-Inflict", "Perilous", 0, false)
    val hefty = WeaponAbility("Hefty", "Hefty", 0, false)
    val heftyTemp = WeaponAbility("Hefty", "Temp Hefty", 0, true)
    val ignoresCover = WeaponAbility("noCover", "Ignores Cover", 0, false)
    val ignoresCoverTemp = WeaponAbility("Temp noCover", "Temp Ignores Cover", 0, true)
    val fusion1 = WeaponAbility("Fusion", "Fusion 1", 1, false)
    val fusion1Temp = WeaponAbility("Fusion", "Temp Fusion 1", 1, true)
    val fusion2 = WeaponAbility("Fusion", "Fusion 2", 2, false)
    val fusion2Temp = WeaponAbility("Fusion", "Temp Fusion 2", 2, true)
    val fusion3 = WeaponAbility("Fusion", "Fusion 3", 3, false)
    val fusion3Temp = WeaponAbility("Fusion", "Temp Fusion 3", 3, true)
    val oneShot = WeaponAbility("1 Shot", "One Shot", 0, false)
    val oneShotTemp = WeaponAbility("1 Shot", "Temp One Shot", 0, true)
    val pistol = WeaponAbility("Handgun", "Pistol", 0, false)
    val pistolTemp = WeaponAbility("Handgun", "Temp Pistol", 0, true)
    val rapid1 = WeaponAbility("Dakka", "Rapid Fire 1", 1, false)
    val rapid1Temp = WeaponAbility("Dakka", "Temp Rapid Fire 1", 1, true)
    val rapid2 = WeaponAbility("Dakka", "Rapid Fire 2", 2, false)
    val rapid2Temp = WeaponAbility("Dakka", "Temp Rapid Fire 2", 2, true)
    val rapid3 = WeaponAbility("Dakka", "Rapid Fire 3", 3, false)
    val rapid3Temp = WeaponAbility("Dakka", "Temp Rapid Fire 3", 3, true)
    val skirmish = WeaponAbility("RunGun", "Skirmish", 0, false)
    val skirmishTemp = WeaponAbility("RunGun", "Skirmish", 0, true)
    val antiInfantry2 = WeaponAbility("Mobkiller", "Anti-Infantry 2", 2, false)
    val antiInfantry3 = WeaponAbility("Mobkiller", "Anti-Infantry 3", 3, false)
    val antiInfantry4 = WeaponAbility("Mobkiller", "Anti-Infantry 4", 4, false)
    val antiMonster = WeaponAbility("KaijuKiller", "Anti-Monster 2", 2, false)
    val antiVehicle = WeaponAbility("TankKiller", "Anti-Vehicle 2", 2, false)
    val antiFly = WeaponAbility("Ack-Ack", "Anti-Fly 2", 2, false)
    val antiCharacter = WeaponAbility("Assassinate", "Anti-Character 2", 2, false)
    val antiPsi = WeaponAbility("WitchKiller", "Anti-Psychic 2", 2, false)
    val blast = WeaponAbility("Boom", "Blast", 0, false)
    val blastTemp = WeaponAbility("Temp Boom", "Temp Blast", 0, true)
    val injuryReroll = WeaponAbility("RRI", "Reroll Injuries", 0, false)
    val tempInjuryReroll = WeaponAbility("Temp RRI", "Temp Reroll Injuries", 0, true)
    val psychic = WeaponAbility("Psi", "Psychic", 0, false)
    val psychicTemp = WeaponAbility("Temp Psi", "Temp Psychic", 0, true)
    val oneHitReroll = WeaponAbility("!", "One Hit Reroll", 0, false)
    val oneHitRerollTemp = WeaponAbility("Temp !", "Temp One Hit Reroll", 0, true)
    val rerollHits = WeaponAbility("@", "All hits are rerolled", 0, false)
    val rerollHitsTemp = WeaponAbility("Temp @", "Temp All hits are rerolled", 0, true)
    val rerollHitOnes = WeaponAbility("#", "Hit rolls of 1 are rerolled.", 0, false)
    val rerollHitOnesTemp = WeaponAbility("Temp #", "Temp Hit rolls of 1 are rerolled.", 0, true)
    val rerollInjuryOnes = WeaponAbility("$", "Injury rolls of 1 are rerolled", 0, false)
    val rerollInjuryOnesTemp = WeaponAbility("Temp $", "Temp Injury rolls of 1 are rerolled", 0, true)
    val oneWoundReroll = WeaponAbility("%", "One Injury Reroll", 0, false)
    val oneWoundRerollTemp = WeaponAbility("Temp %", "Temp One Injury Reroll", 0, true)
    val plusOneInjuries = WeaponAbility("^", "+1 to Injury Rolls", 0, false)
    val plusOneInjuriesTemp = WeaponAbility("Temp ^", "Temp +1 to Injury Rolls", 0, true)
}