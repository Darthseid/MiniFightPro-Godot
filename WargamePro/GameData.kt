package com.example.wargamesimulatorpro

import android.content.Context
import com.example.wargamesimulatorpro.GameData.MODELS_FILE_NAME
import com.example.wargamesimulatorpro.GameData.PLAYERS_FILE_NAME
import com.example.wargamesimulatorpro.GameData.SQUADS_FILE_NAME
import com.example.wargamesimulatorpro.GameData.WEAPONS_FILE_NAME
import com.example.wargamesimulatorpro.GameData.saveToFile
import com.example.wargamesimulatorpro.SquadAbilities.deathExplode2
import com.example.wargamesimulatorpro.SquadAbilities.deathExplode3
import com.example.wargamesimulatorpro.SquadAbilities.officerOrder
import com.example.wargamesimulatorpro.SquadAbilities.reanimator
import com.example.wargamesimulatorpro.SquadAbilities.squadplusOneInjuries
import com.example.wargamesimulatorpro.SquadAbilities.squadrerollHits
import com.example.wargamesimulatorpro.WeaponAbilities.antiInfantry2
import com.example.wargamesimulatorpro.WeaponAbilities.blast
import com.example.wargamesimulatorpro.WeaponAbilities.bonusHits1
import com.example.wargamesimulatorpro.WeaponAbilities.fusion1
import com.example.wargamesimulatorpro.WeaponAbilities.hardHits
import com.example.wargamesimulatorpro.WeaponAbilities.ignoresCover
import com.example.wargamesimulatorpro.WeaponAbilities.injuryReroll
import com.example.wargamesimulatorpro.WeaponAbilities.perilous
import com.example.wargamesimulatorpro.WeaponAbilities.pike
import com.example.wargamesimulatorpro.WeaponAbilities.pistol
import com.example.wargamesimulatorpro.WeaponAbilities.rapid1
import com.example.wargamesimulatorpro.WeaponAbilities.rapid2
import com.example.wargamesimulatorpro.WeaponAbilities.rapid3
import com.example.wargamesimulatorpro.WeaponAbilities.skirmish
import com.google.firebase.crashlytics.buildtools.reloc.com.google.common.reflect.TypeToken
import com.google.gson.Gson
import java.io.File

object GameData {
    const val WEAPONS_FILE_NAME = "weapons.json"
    const val MODELS_FILE_NAME = "models.json"
    const val SQUADS_FILE_NAME = "squads.json"
    const val PLAYERS_FILE_NAME = "players.json" // New file for players

    val weapons = mutableListOf<Weapon>()
    val models = mutableListOf<Model>()
    val squads = mutableListOf<Squad>()
    val players = mutableListOf<Player>()  // New list to store players

    // ------ WEAPON MANAGEMENT ------
    fun addWeapon(context: Context, weapon: Weapon)
    {
        val existingWeaponIndex = weapons.indexOfFirst { it.weaponName == weapon.weaponName }
        if (existingWeaponIndex != -1)
            weapons[existingWeaponIndex] = weapon
        else
            weapons.add(weapon)
        saveToFile(context, WEAPONS_FILE_NAME, weapons)
    }

    fun loadWeaponsFromFile(context: Context)
    {
        weapons.clear()
        weapons.addAll(loadFromFile(context, WEAPONS_FILE_NAME, object : TypeToken<MutableList<Weapon>>() {}))
    }

    // ------ MODEL MANAGEMENT ------
    fun addModel(context: Context, model: Model)
    {
        val existingModelIndex = models.indexOfFirst { it.name == model.name }
        if (existingModelIndex != -1)
            models[existingModelIndex] = model
        else
            models.add(model)
        saveToFile(context, MODELS_FILE_NAME, models)
    }

    fun loadModelsFromFile(context: Context)
    {
        models.clear()
        models.addAll(loadFromFile(context, MODELS_FILE_NAME, object : TypeToken<MutableList<Model>>() {}))
    }

    // ------ SQUAD MANAGEMENT ------
    fun addSquad(context: Context, theSquad: Squad)
    {
        val existingSquadIndex = squads.indexOfFirst { it.name == theSquad.name }
        if (existingSquadIndex != -1)
            squads[existingSquadIndex] = theSquad
        else
            squads.add(theSquad)
        saveToFile(context, SQUADS_FILE_NAME, squads)
    }

    fun loadSquadsFromFile(context: Context)
    {
        squads.clear()
        squads.addAll(loadFromFile(context, SQUADS_FILE_NAME, object : TypeToken<MutableList<Squad>>() {}))
    }

    // ------ PLAYER MANAGEMENT ------
    fun addPlayer(context: Context, player: Player)
    {
        val existingPlayerIndex = players.indexOfFirst { it.playerName == player.playerName }
        if (existingPlayerIndex != -1)
            players[existingPlayerIndex] = player  // Update existing player
        else
            players.add(player)  // Add new player
        saveToFile(context, PLAYERS_FILE_NAME, players)
    }

    fun loadPlayersFromFile(context: Context)
    {
        players.clear()
        players.addAll(loadFromFile(context, PLAYERS_FILE_NAME, object : TypeToken<MutableList<Player>>() {}))
    }
    // ------ GENERIC SAVE/LOAD FUNCTIONS ------
    inline fun <reified T> saveToFile(context: Context, fileName: String, data: List<T>)
    {
        val file = File(context.filesDir, fileName)
        val json = Gson().toJson(data)
        file.writeText(json)
    }

    private fun <T> loadFromFile(context: Context, fileName: String, typeToken: TypeToken<MutableList<T>>): MutableList<T>
    {
        val file = File(context.filesDir, fileName)
        return if (file.exists())
        {
            val json = file.readText()
            Gson().fromJson(json, typeToken.type) ?: mutableListOf()
        } else
            mutableListOf()
    }
}

fun populatePresetData(context: Context)  // Preset data for weapons
{
    val presetWeapons = mutableListOf(
        Weapon("Laser Gun", 24f, "1", 4, 3, 0, "1", mutableListOf(rapid1)),
        Weapon("Frag Gun", 24f, "1", 3, 4, 0, "1", mutableListOf(rapid1)),
        Weapon("Bayonet", 1f, "1", 4, 3, 0, "1", mutableListOf()) ,
        Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", mutableListOf()),
        Weapon("Flamethrower", 12f, "D6", 1, 4, 0, "1", mutableListOf(ignoresCover)),
        Weapon("Battle Cannon", 48f, "D6+3", 4, 10, -1, "3", mutableListOf(blast)),
        Weapon("Multi-Fusion", 18f, "2", 4, 9, -4, "D6", mutableListOf(fusion1)),
        Weapon("Laser Cannon", 48f, "1", 4, 12, -3, "D6+1", mutableListOf()),
        Weapon("Armored Tracks", 1f, "6", 4, 7, 0, "1", mutableListOf()),
        Weapon("Disc Handgun", 12f, "1", 2, 4, -1, "1", mutableListOf(skirmish, pistol)),
        Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", mutableListOf(antiInfantry2)),
        Weapon("3s Plasma Pistol", 12f, "1", 3, 8, -3, "2", mutableListOf(perilous)),
        Weapon("4s Plasma Pistol", 12f, "1", 4, 8, -3, "2", mutableListOf(perilous)),
        Weapon("Gauss Killer", 48f, "1", 4, 14, -3, "6", mutableListOf(hardHits)),
        Weapon("Disc Handgun", 12f, "1", 2, 4, -1, "1", mutableListOf(skirmish, pistol)),
        Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", mutableListOf(antiInfantry2)),
        Weapon("Twin Kannon", 36f, "D3", 4, 12, -2, "D6", mutableListOf(blast, perilous, injuryReroll)),
        Weapon("Twin Shooter", 36f, "4", 4, 6, -1, "1", mutableListOf(rapid2, bonusHits1, injuryReroll)),
        Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", mutableListOf(blast)),
        Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", mutableListOf()),
        Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", mutableListOf()),
        Weapon("Smash Gun", 48f, "D3", 4, 9, -3, "4", mutableListOf(blast)),
        Weapon("Multi-Blaster", 100f, "30", 3, 9, -2, "3", mutableListOf(bonusHits1)),
        Weapon("Mecha Feet", 1f, "6", 4, 12, -2, "4", mutableListOf()),
        Weapon("Tornado Fragger", 18f, "3", 2, 4, -1, "2", mutableListOf(rapid3, injuryReroll)),
        Weapon("Bike Pike", 1f, "5", 2, 7, -2, "2", mutableListOf(pike)),
    )
    val planeWeapons = mutableListOf(
        Weapon("Smash Gun", 48f, "D3", 4, 9, -3, "4", mutableListOf(blast)),
        Weapon("Twin Kannon", 36f, "D3", 4, 12, -2, "D6", mutableListOf(blast, perilous, injuryReroll)),
        Weapon("Twin Shooter", 36f, "4", 4, 6, -1, "1", mutableListOf(rapid2, bonusHits1, injuryReroll))
    )
    saveToFile(context, WEAPONS_FILE_NAME, presetWeapons)
    val guardWeapons = mutableListOf(
        Weapon("Laser Gun", 24f, "1", 4, 3, 0, "1", mutableListOf(rapid1)),
        Weapon("Bayonet", 1f, "1", 4, 3, 0, "1", mutableListOf()) ,
    )
    val marineWeapons = mutableListOf(
        Weapon("Frag Gun", 24f, "1", 3, 4, 0, "1", mutableListOf(rapid1)), //Implement Hit Reroll, Wound Reroll, +1 to Hit, +1 to Wound weapon abilities.
        Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", mutableListOf())
    )
    val jetBikeWeapons = mutableListOf(
        Weapon("Tornado Fragger", 18f, "3", 2, 4, -1, "2", mutableListOf(rapid3, injuryReroll)),
        Weapon("Bike Pike", 1f, "5", 2, 7, -2, "2", mutableListOf(pike)),
    )
    val flameMarineWeapons = mutableListOf(
        Weapon("Flamethrower", 12f, "D6", 1, 4, 0, "1", mutableListOf(ignoresCover)),
        Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", mutableListOf())
    )
    val sergeantWeapons = mutableListOf(
        Weapon("3s Plasma Pistol", 12f, "1", 3, 8, -3, "2", mutableListOf(perilous)),
        Weapon("Power Gauntlet", 1f, "1", 3, 8, -2, "2", mutableListOf())
    )
    val guardSargeWeapons = mutableListOf(
        Weapon("4s Plasma Pistol", 12f, "1", 4, 8, -3, "2", mutableListOf(perilous)),
        Weapon("Power Spear", 1f, "2", 4, 4, -2, "1", mutableListOf())
    )
    val tankWeapons = mutableListOf(
        Weapon("Battle Cannon", 48f, "D6+3", 4, 10, -1, "3", mutableListOf(blast)),
        Weapon("Multi-Fusion", 18f, "2", 4, 9, -4, "D6", mutableListOf(fusion1)),
        Weapon("Laser Cannon", 48f, "1", 4, 12, -3, "D6+1", mutableListOf()),
        Weapon("Armored Tracks", 1f, "6", 4, 7, 0, "1", mutableListOf())
    )
    val clairvoyantWeapons = mutableListOf(
        Weapon("Disc Pistol", 12f, "1", 2, 4, -1, "1", mutableListOf(skirmish, pistol)),
        Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", mutableListOf(antiInfantry2))
    )
    val pylonWeapon = mutableListOf(
        Weapon("Gauss Killer", 48f, "1", 4, 14, -3, "6", mutableListOf(hardHits))
    )
    val hugeMechaWeapons = mutableListOf(
        Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", mutableListOf(blast)),
        Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", mutableListOf()),
        Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", mutableListOf()),
        Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", mutableListOf(blast)),
        Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", mutableListOf()),
        Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", mutableListOf()),
        Weapon("Multi-Blaster", 100f, "30", 3, 9, -2, "3", mutableListOf(bonusHits1)),
        Weapon("Mecha Feet", 1f, "6", 4, 12, -2, "4", mutableListOf())
    )
    val presetModels = mutableListOf(
        Model("Guard", 1, 0, guardWeapons),
        Model("Marine", 2, 0, marineWeapons),
        Model("Marine Sarge", 2, 0, sergeantWeapons),
        Model("Flame Marine", 2, 0, flameMarineWeapons),
        Model("Guard Sarge", 1, 1, guardSargeWeapons),
        Model("Medium Tank", 13, 4, tankWeapons),
        Model("Homemade Biplane", 12, 4, planeWeapons),
        Model("Clairvoyant", 3, 0, clairvoyantWeapons),
        Model("Zapper Pylon", 10, 0, pylonWeapon),
        Model("Huge Mecha", 100, 33, hugeMechaWeapons),
        Model("Floating Biker", 5, 0, jetBikeWeapons)
    )
    val mechaSquad = mutableListOf(Model("Huge Mecha", 100, 33, hugeMechaWeapons))
    val bikerSquad = mutableListOf<Model>()
    repeat(3) { bikerSquad.add(presetModels[10].deepCopy()) }
    val planeSquad = mutableListOf(Model("Homemade Biplane", 12, 4, planeWeapons))
    val pylonModel = mutableListOf(
        Model("Zapper Pylon", 10, 0, pylonWeapon)
    )
    val clairvoyantModels = mutableListOf(
        Model("Clairvoyant", 3, 4, clairvoyantWeapons)
    )
    saveToFile(context, MODELS_FILE_NAME, presetModels)
    val guardSquad = mutableListOf<Model>()
    repeat(18) { guardSquad.add(presetModels[0].deepCopy()) } // Add 18 Guards
    repeat(2) { guardSquad.add(presetModels[4].deepCopy()) } // Add 2 Guard Sergeants
    val marineSquad = mutableListOf<Model>()
    repeat(2) { marineSquad.add(presetModels[3].deepCopy()) } // Add 2 Flame Marines
    marineSquad.add(presetModels[2].deepCopy()) // Add 1 Marine Sergeant
    repeat(7) { marineSquad.add(presetModels[1].deepCopy()) } // Add 7 Marines
    val tankSquad = mutableListOf<Model>()
    repeat(3) { tankSquad.add(presetModels[5].deepCopy()) }  // Preset data for squads
    val Ast = Squad("Guard Squad", 6f, 3, 5, 7, 7, 7, listOf("Infantry"), false,  guardSquad, 20, mutableListOf(officerOrder))
    val Ade = Squad("Marine Squad", 6f, 4, 3, 7, 7, 6, listOf("Infantry"), false, marineSquad, 20, mutableListOf(squadrerollHits, squadplusOneInjuries))
    val tnk =  Squad("Battle Tanks", 10f, 11, 2, 13, 7, 7, listOf("Vehicle"), false, tankSquad, 3, mutableListOf(deathExplode2))
    val vert = Squad( "MagLev Bikes", 12f, 7, 2, 4, 7, 6, listOf("Mounted", "Fly"), false,  bikerSquad, 3, mutableListOf(deathExplode3))
    val dak =    Squad("Homemade Biplane", 99.9f, 9, 3, 4, 7, 7, listOf("Aircraft", "Fly", "Vehicle"), false,  planeSquad, 1, mutableListOf(deathExplode2))
    val pyl = Squad("Zapper Pylon", 0f, 8, 3, 7, 7, 7, listOf("Fortification", "Vehicle"), false,  pylonModel, 1, mutableListOf(deathExplode2, reanimator, SquadAbilities.teleport))
    val seer = Squad( "Clairvoyant", 7f, 3, 6, 4, 7, 6, listOf("Character", "Infantry", "Psychic"), true, clairvoyantModels, 1, mutableListOf())
    val presetSquads = listOf(
        Squad("Guard Squad", 6f, 3, 5, 7, 7, 7, listOf("Infantry"), false,  guardSquad, 20, mutableListOf(officerOrder)),
        Squad("Marine Squad", 6f, 4, 3, 7, 7, 6, listOf("Infantry"), false, marineSquad, 20, mutableListOf(squadrerollHits, squadplusOneInjuries)),
        Squad("Battle Tanks", 10f, 11, 2, 13, 7, 7, listOf("Vehicle"), false, tankSquad, 3, mutableListOf(deathExplode2)),
        Squad("Homemade Biplane", 99.9f, 9, 3, 4, 7, 7, listOf("Aircraft", "Fly", "Vehicle"), false,  planeSquad, 1, mutableListOf(deathExplode2)),
        Squad("Zapper Pylon", 0f, 8, 3, 7, 7, 7, listOf("Fortification", "Vehicle"), false,  pylonModel, 1, mutableListOf(deathExplode2, reanimator, SquadAbilities.teleport)) ,
        Squad( "Clairvoyant", 7f, 3, 6, 4, 7, 6, listOf("Character", "Infantry", "Psychic"), true, clairvoyantModels, 1, mutableListOf()),
        Squad( "Huge Mecha", 10f, 16, 2, 5, 7, 6, listOf("Titanic", "Vehicle"), false, mechaSquad, 1, mutableListOf(deathExplode2)),
        Squad( "MagLev Bikes", 12f, 7, 2, 4, 7, 6, listOf("Mounted", "Fly"), false,  bikerSquad, 3, mutableListOf(deathExplode3)),
    )
    saveToFile(context, SQUADS_FILE_NAME, presetSquads)
    val InfForces = mutableListOf(Ast, Ade)
    val vehForces = mutableListOf(tnk, vert)
    val xeno = mutableListOf(seer, dak, pyl)
    val presetPlayers = listOf(
        Player(InfForces, 5, mutableListOf(), false, "Human Dominion", mutableListOf()),
        Player(vehForces, 5, mutableListOf(), false, "Cyborg Alliance", mutableListOf("Subroutines")),
        Player(xeno, 6, mutableListOf(), false, "Saint Xelia's Armies", mutableListOf("Alien Terror"))
    )
    saveToFile(context, PLAYERS_FILE_NAME, presetPlayers)
}