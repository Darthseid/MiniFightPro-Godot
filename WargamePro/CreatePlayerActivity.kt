package com.example.wargamesimulatorpro

import android.annotation.SuppressLint
import android.app.AlertDialog
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.example.wargamesimulatorpro.GameData.squads
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import kotlin.reflect.KProperty1

class CreatePlayerActivity : ComponentActivity()
{
    private val selectedSquads = mutableListOf<Squad>()
    private val selectedAbilities = mutableListOf<String>()
    @SuppressLint("ClickableViewAccessibility")
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_create_player)
        val playerNameInput: EditText = findViewById(R.id.playerNameInput)
        val isAICheckBox: CheckBox = findViewById(R.id.isAICheckBox)
        val saveButton: Button = findViewById(R.id.saveButton)
        val discardButton: Button = findViewById(R.id.discardButton)        // Populate fields if editing an existing player
        val selectAbilitiesButton: Button = findViewById(R.id.selectAbilitiesButton)
        intent?.let {
            val extras = intent.extras
            if (extras != null)
            {
                playerNameInput.setText(extras.getString("playerName"))
                isAICheckBox.isChecked = extras.getBoolean("isAI")
                val receivedSquads = extras.getParcelableArrayList<Squad>("theirSquads")
                if (receivedSquads != null)
                {
                    selectedSquads.clear()
                    selectedSquads.addAll(receivedSquads)
                }
                val receivedAbilities = extras.getStringArrayList("playerAbilities")
                if (receivedAbilities != null)
                {
                    selectedAbilities.clear()
                    selectedAbilities.addAll(receivedAbilities)
                }
            }
        } // RecyclerView for Selected Squads
        val selectedSquadsAdapter = SelectedSquadsAdapter(selectedSquads) { _ -> }
        selectedSquadsAdapter.onSquadRemoved = { squad ->
            val position = selectedSquads.indexOf(squad)
            if (position != -1)
            {
                selectedSquads.remove(squad)
                selectedSquadsAdapter.notifyItemRemoved(position)
            }
        }
        val availableSquadsAdapter = AvailableSquadsAdapter(squads)
        { squad ->
            selectedSquads.add(squad)
            selectedSquadsAdapter.notifyItemInserted(selectedSquads.size - 1)
        }
        findViewById<RecyclerView>(R.id.availableSquadsRecyclerView).apply {
            layoutManager = LinearLayoutManager(this@CreatePlayerActivity)
            adapter = availableSquadsAdapter
        }
        findViewById<RecyclerView>(R.id.selectedSquadsRecyclerView).apply {
            layoutManager = LinearLayoutManager(this@CreatePlayerActivity)
            adapter = selectedSquadsAdapter
        }
        selectAbilitiesButton.setOnClickListener { showAbilitiesDialog() }
        saveButton.setOnClickListener {
            val playerName = playerNameInput.text.toString()
            val isAI = isAICheckBox.isChecked

            if (playerName.isBlank() || selectedSquads.isEmpty()) {
                Toast.makeText(this, "Please enter a player name and select squads.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            val orderPoints = 3 + selectedSquads.size  // Order points are 3 + squad count
            val player = Player(
                playerName = playerName,
                orderPoints = orderPoints,  // Now dynamically calculated
                isAI = isAI,
                theirSquads = selectedSquads.toMutableList(),
                orders = mutableListOf(),
                playerAbilities = selectedAbilities.toMutableList()
            )
            GameData.addPlayer(this, player)
            finish()
        }
        discardButton.setOnClickListener {
            finish()
        }
    }
    private fun showAbilitiesDialog()
    {
        val abilities = PlayerAbilities::class.members
            .filterIsInstance<KProperty1<PlayerAbilities, String>>()
            .map { it.get(PlayerAbilities) }
            .sorted()
        val checkedItems = BooleanArray(abilities.size) { index ->
            selectedAbilities.contains(abilities[index])
        }
        AlertDialog.Builder(this)
            .setTitle("Select Player Abilities")
            .setMultiChoiceItems(abilities.toTypedArray(), checkedItems) { _, index, isChecked ->
                val ability = abilities[index]
                if (isChecked) {
                    selectedAbilities.add(ability)
                } else {
                    selectedAbilities.remove(ability)
                }
            }
            .setPositiveButton("Done", null)
            .create()
            .show()
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

