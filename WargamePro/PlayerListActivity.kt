package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic

class PlayerListActivity : ComponentActivity()
{
    private lateinit var playerListView: ListView
    private lateinit var createPlayerButton: Button
    private lateinit var playersAdapter: ArrayAdapter<String>
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_player_list)
        playerListView = findViewById(R.id.playerListView)
        createPlayerButton = findViewById(R.id.createPlayerButton)
        GameData.loadPlayersFromFile(this)
        updatePlayerList()  // Button to create a new player
        createPlayerButton.setOnClickListener {
            if (GameData.players.size > 1000)
            { // Limit the number of players
                Toast.makeText(this, "You have too many players.", Toast.LENGTH_SHORT).show()
            } else
            {
                playSound(this, R.raw.select)
                val intent = Intent(this, CreatePlayerActivity::class.java)
                startActivity(intent)
            }
        }
        playerListView.setOnItemClickListener { _, _, position, _ ->
            val selectedPlayer = GameData.players[position]
            val intent = Intent(this, CreatePlayerActivity::class.java).apply {
                putExtra("playerName", selectedPlayer.playerName)  // Edit player on tap
                putParcelableArrayListExtra("theirSquads", ArrayList(selectedPlayer.theirSquads))
                putParcelableArrayListExtra("orders", ArrayList(selectedPlayer.orders))
                putExtra("isAI", selectedPlayer.isAI)
                putStringArrayListExtra("playerAbilities", ArrayList(selectedPlayer.playerAbilities))
            }
            startActivity(intent)
        }
        playerListView.setOnItemLongClickListener { _, _, position, _ ->
            val selectedPlayer = GameData.players[position]
            AlertDialog.Builder(this)   // Long press to delete a player
                .setTitle("Delete Player")
                .setMessage("Are you sure you want to delete '${selectedPlayer.playerName}'?")
                .setPositiveButton("Yes") { _, _ ->
                    GameData.players.remove(selectedPlayer)
                    GameData.saveToFile(this, GameData.PLAYERS_FILE_NAME, GameData.players)
                    updatePlayerList()
                }
                .setNegativeButton("No", null)
                .show()
            true
        }
    }
    override fun onResume()
    {
        super.onResume()
        updatePlayerList() // Refresh list when returning to this activity
        playMusic(this, R.raw.marching)
    }
    override fun onPause()
    {
        super.onPause()
        pauseMusic()
    }
    private fun updatePlayerList()
    {
        val playerNames = GameData.players.map { it.playerName }
        playersAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, playerNames)
        playerListView.adapter = playersAdapter
    }
}
