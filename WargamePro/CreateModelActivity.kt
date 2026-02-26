package com.example.wargamesimulatorpro

import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic

class CreateModelActivity : ComponentActivity()
{

    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_create_model)
        val modelNameInput: EditText = findViewById(R.id.modelNameInput)
        val healthInput: EditText = findViewById(R.id.modelHealthInput)
        val bracketedInput: EditText = findViewById(R.id.modelBracketedInput)
        val weaponListView: ListView = findViewById(R.id.weaponListView)
        val saveButton: Button = findViewById(R.id.saveButton)
        val discardButton: Button = findViewById(R.id.discardButton)
        val weapons = GameData.weapons
        val selectedWeapons = mutableListOf<Weapon>()
        val weaponNames = weapons.map { it.weaponName }
        val adapter = ArrayAdapter(this, android.R.layout.simple_list_item_multiple_choice, weaponNames)
        weaponListView.adapter = adapter
        weaponListView.choiceMode = ListView.CHOICE_MODE_MULTIPLE
        val extras = intent.extras
        if (extras != null) {
            modelNameInput.setText(extras.getString("modelName"))
            healthInput.setText(extras.getInt("modelHealth").toString())
            bracketedInput.setText(extras.getInt("modelBracketed").toString())

            val existingTools = extras.getParcelableArrayList<Weapon>("modelTools") ?: mutableListOf()
            GameData.weapons.forEachIndexed { index, weapon ->
                if (existingTools.contains(weapon)) {
                    weaponListView.setItemChecked(index, true)
                    selectedWeapons.add(weapon)
                }
            }
        }
        weaponListView.setOnItemClickListener { _, _, position, _ ->
            val weapon = weapons[position]
            if (selectedWeapons.contains(weapon))
                selectedWeapons.remove(weapon) // Deselect weapon
            else
                selectedWeapons.add(weapon) // Select weapon
        }
        saveButton.setOnClickListener {
            val modelName = modelNameInput.text.toString()
            val health = healthInput.text.toString().toIntOrNull() ?: 0
            val bracketed = bracketedInput.text.toString().toIntOrNull() ?: 0
            if (modelName.isBlank() || health <= 0) {
                Toast.makeText(this, "Please enter valid model details.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            val model = Model(
                name = modelName,
                startingHealth = health,
                health = health,
                bracketed = bracketed,
                tools = selectedWeapons
            )
            GameData.addModel(this, model)
            finish()
        }
        discardButton.setOnClickListener {
            finish() // Close activity without saving
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
