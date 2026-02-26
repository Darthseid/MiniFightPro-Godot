package com.example.wargamesimulatorpro

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import com.google.firebase.analytics.FirebaseAnalytics

class StartGameActivity : ComponentActivity()
{
    private lateinit var player1Spinner: Spinner
    private lateinit var player2Spinner: Spinner
    private lateinit var beginBattleButton: Button
    private lateinit var firebaseAnalytics: FirebaseAnalytics

    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        updateMini(this)
        updateSquad(this)
        updatePlayer(this)
        setContentView(R.layout.activity_start_game)
        player1Spinner = findViewById(R.id.unit1Spinner)
        player2Spinner = findViewById(R.id.unit2Spinner)
        beginBattleButton = findViewById(R.id.beginBattleButton)
        val players = GameData.players
        if (players.isEmpty())
        {
            Toast.makeText(this, "No players available. Please create players first.", Toast.LENGTH_LONG).show()
            finish()
            return
        }
        val playerNames = players.map { it.playerName }
        val adapter = ArrayAdapter(this, android.R.layout.simple_spinner_item, playerNames)
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
        player1Spinner.adapter = adapter
        player2Spinner.adapter = adapter
        beginBattleButton.setOnClickListener {
            val player1Position = player1Spinner.selectedItemPosition
            val player2Position = player2Spinner.selectedItemPosition
            playSound(this, R.raw.select)
            if (player1Position == -1 || player2Position == -1 || player1Position == player2Position) {
                Toast.makeText(this, "Please select two different players.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            val player1 = players[player1Position]
            val player2 = players[player2Position]

            val intent = Intent(this, BattleActivity::class.java).apply {
                putExtra("player1", player1)
                putExtra("player2", player2)
            }
            val bundle = Bundle().apply {
                putString("selected_player_1", player1.playerName)
                putString("selected_player_2", player2.playerName)
            }
            firebaseAnalytics = FirebaseAnalytics.getInstance(this)
            firebaseAnalytics.logEvent("player_selected", bundle)
            startActivity(intent)
        }
    }

    override fun onPause()
    {
        super.onPause()
        pauseMusic()
    }

    override fun onResume()
    {
        super.onResume()
        playMusic(this, R.raw.marching)
    }
}

fun updateMini(context: Context)
{
    val savedWeapons = GameData.weapons // Load the saved weapons
    val savedModels = GameData.models
    var isUpdated = false // Flag to track if any changes were made
    savedModels.forEach { model ->
        val updatedWeapons = model.tools.filter { savedWeapon ->
            savedWeapons.any { it.weaponName == savedWeapon.weaponName }   // Filter weapons that still exist in the saved weapons list
        }.map { savedWeapon ->
            savedWeapons.find { it.weaponName == savedWeapon.weaponName } ?: savedWeapon // Update weapon values to match the saved weapon
        }
        if (updatedWeapons.isNotEmpty() && updatedWeapons != model.tools)
        {
            model.tools.clear()
            model.tools.addAll(updatedWeapons)
            isUpdated = true // Mark as updated if changes were made
        }
    }
    if (isUpdated)
        GameData.saveToFile(context, GameData.MODELS_FILE_NAME, savedModels) // Save only if changes were made
}

fun updateSquad(context: Context)
{
    val savedModels = GameData.models // Load the saved squads and models
    val savedSquads = GameData.squads
    var isUpdated = false // Flag to track if any changes were made
    savedSquads.forEach { squad ->
        val updatedModels = squad.composition.filter { savedModel ->
            savedModels.any { it.name == savedModel.name } // Filter models that still exist in the saved models list
        }.map { savedModel ->
            savedModels.find { it.name == savedModel.name } ?: savedModel  // Update model values to match the saved model
        }
        if (updatedModels.isNotEmpty() && updatedModels != squad.composition)
        {
            squad.composition.clear()
            squad.composition.addAll(updatedModels)
            isUpdated = true // Mark as updated if changes were made
        }
    }
    if (isUpdated)
        GameData.saveToFile(context, GameData.SQUADS_FILE_NAME, savedSquads) // Save only if changes were made
}

fun updatePlayer(context: Context)
{ // Update squads: Keep only squads that exist in GameData.squads
    val savedSquads = GameData.squads  // Load current squads
    val savedPlayers = GameData.players  // Load current players
    var isUpdated = false  // Flag to track if changes were made
    savedPlayers.forEach { player ->
        val updatedSquads = player.theirSquads.filter { savedSquad ->
            savedSquads.any { it.name == savedSquad.name }
        }.map { savedSquad ->
            savedSquads.find { it.name == savedSquad.name } ?: savedSquad  // Update squad data
        }
        if (updatedSquads.isNotEmpty() && updatedSquads != player.theirSquads)
        {
            player.theirSquads.clear()
            player.theirSquads.addAll(updatedSquads)
            isUpdated = true
        }
    }
    if (isUpdated)
        GameData.saveToFile(context, GameData.PLAYERS_FILE_NAME, savedPlayers)  // Save only if changes were made
}
