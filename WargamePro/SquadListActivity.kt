package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic

class SquadListActivity : ComponentActivity()
{
    private lateinit var unitListView: ListView
    private lateinit var createUnitButton: Button
    private lateinit var unitsAdapter: ArrayAdapter<String>
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_unit_list)
        unitListView = findViewById(R.id.unitListView)
        createUnitButton = findViewById(R.id.createSquadButton)
        GameData.loadSquadsFromFile(this)
        updateUnitList()
        createUnitButton.setOnClickListener {
            if (GameData.squads.size > 3000)
            {
                Toast.makeText(this, "You have too many squads.", Toast.LENGTH_SHORT).show()
            }
            else {
                playSound(this, R.raw.select)
                val intent = Intent(this, CreateSquadActivity::class.java)
                startActivity(intent)
            }
        }
        unitListView.setOnItemClickListener { _, _, position, _ ->
            val selectedUnit = GameData.squads[position]
            val intent = Intent(this, CreateSquadActivity::class.java).apply {
                putExtra("unitName", selectedUnit.name)
                putStringArrayListExtra("squadType", ArrayList(selectedUnit.squadType))
                putExtra("movement", selectedUnit.movement)
                putExtra("hardness", selectedUnit.hardness)
                putExtra("defense", selectedUnit.defense)
                putExtra("dodge", selectedUnit.dodge)
                putExtra("damageResistance", selectedUnit.damageResistance)
                putExtra("bravery", selectedUnit.bravery)
                putExtra("selectedModels", ArrayList(selectedUnit.composition))
                putParcelableArrayListExtra("unitAbilities", ArrayList(selectedUnit.squadAbilities))
            }
            startActivity(intent)
        }
        unitListView.setOnItemLongClickListener { _, _, position, _ ->
            val selectedUnit = GameData.squads[position]
            AlertDialog.Builder(this)
                .setTitle("Delete Unit")
                .setMessage("Are you sure you want to delete '${selectedUnit.name}'?")
                .setPositiveButton("Yes") { _, _ ->
                    GameData.squads.remove(selectedUnit)
                    GameData.saveToFile(this, GameData.SQUADS_FILE_NAME, GameData.squads)
                    updateUnitList()
                }
                .setNegativeButton("No", null)
                .show()
            true
        }
    }
    override fun onResume()
    {
        super.onResume()
        updateUnitList() // Refresh list when returning to this activity
        playMusic(this, R.raw.marching)
    }

    override fun onPause()
    {
        super.onPause()
        pauseMusic()
    }
    private fun updateUnitList()
    {
        val unitNames = GameData.squads.map { it.name }
        unitsAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, unitNames)
        unitListView.adapter = unitsAdapter
    }
}
