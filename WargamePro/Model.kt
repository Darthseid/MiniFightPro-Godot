package com.example.wargamesimulatorpro

import android.os.Parcel
import android.os.Parcelable

data class Model(
    val name: String,
    val startingHealth: Int, // Use camelCase for variable names
    var health: Int,
    val bracketed: Int,
    val tools: MutableList<Weapon>
) : Parcelable {

    constructor(
        name: String,
        startingHealth: Int, // Initialize health with startingHealth
        bracketed: Int,
        tools: MutableList<Weapon>
    ) : this(name, startingHealth, startingHealth, bracketed, tools)

    constructor(parcel: Parcel) : this(
        name = parcel.readString() ?: "",
        startingHealth = parcel.readInt(),
        health = parcel.readInt(),
        bracketed = parcel.readInt(),
        tools = mutableListOf<Weapon>().apply {
            parcel.readTypedList(this, Weapon.CREATOR)
        }
    )

    override fun writeToParcel(parcel: Parcel, flags: Int) {
        parcel.writeString(name)
        parcel.writeInt(startingHealth)
        parcel.writeInt(health)
        parcel.writeInt(bracketed)
        parcel.writeTypedList(tools)
    }

    override fun describeContents(): Int = 0

    companion object CREATOR : Parcelable.Creator<Model> {
        override fun createFromParcel(parcel: Parcel): Model = Model(parcel)
        override fun newArray(size: Int): Array<Model?> = arrayOfNulls(size)
    }

    fun deepCopy(): Model
    {
        return Model(
            name,
            startingHealth,
            health, // Keep current health in the copy
            bracketed,
            tools.map { it.copy() }.toMutableList()
        )
    }
}
