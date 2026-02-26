package com.example.wargamesimulatorpro

import android.annotation.SuppressLint
import android.app.AlertDialog
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.example.wargamesimulatorpro.GameData.models
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import java.util.Locale

class CreateSquadActivity : ComponentActivity()
{
    private val selectedModels = mutableListOf<Model>()
    private val selectedAbilities = mutableListOf<SquadAbility>()
    private val selectedSquadTypes = mutableListOf<String>() // Track selected squad types
    @SuppressLint("ClickableViewAccessibility")
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_create_squad)
        val squadNameInput: EditText = findViewById(R.id.unitNameInput)
        val squadTypeButton: Button = findViewById(R.id.unitTypeButton)
        val saveButton: Button = findViewById(R.id.saveButton)
        val discardButton: Button = findViewById(R.id.discardButton)
        val selectAbilitiesButton: Button = findViewById(R.id.selectAbilitiesButton)
        val movementInput: EditText = findViewById(R.id.movementInput)
        val hardnessInput: EditText = findViewById(R.id.hardnessInput)
        val defenseInput: EditText = findViewById(R.id.DefenseInput)
        val dodgeInput: EditText = findViewById(R.id.DodgeInput)
        val damageResistanceInput: EditText = findViewById(R.id.damageResistanceInput)
        val braveryInput: EditText = findViewById(R.id.braveryInput)
        val squadTypes = listOf("Aircraft", "Titanic", "Fortification", "Mounted", "Monster", "Character", "Vehicle", "Infantry", "Fly",  "Psychic")
        intent?.let {
            val extras = intent.extras
            if (extras != null)    // Populate fields if editing an existing squad
            {
                val locale = Locale.getDefault()
                squadNameInput.setText(extras.getString("unitName"))
                val receivedSquadTypes = extras.getStringArrayList("squadType") ?: emptyList<String>()
                selectedSquadTypes.clear()
                selectedSquadTypes.addAll(receivedSquadTypes)
                movementInput.setText(String.format(locale, "%.1f", extras.getFloat("movement")))
                hardnessInput.setText(String.format(locale, "%d", extras.getInt("hardness")))
                defenseInput.setText(String.format(locale, "%d", extras.getInt("defense")))
                dodgeInput.setText(String.format(locale, "%d", extras.getInt("dodge")))
                damageResistanceInput.setText(String.format(locale, "%d", extras.getInt("damageResistance")))
                braveryInput.setText(String.format(locale, "%d", extras.getInt("bravery")))
                val receivedModels = extras.getSerializable("selectedModels") as? List<Model>
                if (receivedModels != null) {
                    selectedModels.clear()
                    selectedModels.addAll(receivedModels)
                }
                val receivedAbilities = extras.getParcelableArrayList<SquadAbility>("unitAbilities")
                if (receivedAbilities != null) {
                    selectedAbilities.clear()
                    selectedAbilities.addAll(receivedAbilities)
                }
            }
        }


        squadTypeButton.setOnClickListener {
            val checkedItems = BooleanArray(squadTypes.size) { index ->
                selectedSquadTypes.contains(squadTypes[index])
            }
            AlertDialog.Builder(this)   // Multi-choice dialog for squad types
                .setTitle("Select Squad Types")
                .setMultiChoiceItems(squadTypes.toTypedArray(), checkedItems) { _, index, isChecked ->
                    val squadType = squadTypes[index]
                    if (isChecked) {
                        selectedSquadTypes.add(squadType)
                    } else {
                        selectedSquadTypes.remove(squadType)
                    }
                }
                .setPositiveButton("Done", null)
                .create()
                .show()
        }

        val selectedModelsAdapter = SelectedModelsAdapter(selectedModels) { _ -> }
        selectedModelsAdapter.onModelRemoved = { model ->
            val position = selectedModels.indexOf(model)
            if (position != -1) {
                selectedModels.remove(model)
                selectedModelsAdapter.notifyItemRemoved(position)
            }
        }
        val availableModelsAdapter = AvailableModelsAdapter(models) { model ->
            selectedModels.add(model)
            selectedModelsAdapter.notifyItemInserted(selectedModels.size - 1)
        }

        findViewById<RecyclerView>(R.id.availableModelsRecyclerView).apply {
            layoutManager = LinearLayoutManager(this@CreateSquadActivity)
            adapter = availableModelsAdapter
        }

        findViewById<RecyclerView>(R.id.selectedModelsRecyclerView).apply {
            layoutManager = LinearLayoutManager(this@CreateSquadActivity)
            adapter = selectedModelsAdapter
        }
        selectAbilitiesButton.setOnClickListener { showAbilitiesDialog() }
        saveButton.setOnClickListener {
            val squadName = squadNameInput.text.toString()
            val movement = movementInput.text.toString().toFloatOrNull()
            val toughness = hardnessInput.text.toString().toIntOrNull()
            val armorSave = defenseInput.text.toString().toIntOrNull()
            val dodge = dodgeInput.text.toString().toIntOrNull()
            val feelNoPain = damageResistanceInput.text.toString().toIntOrNull()
            val leadership = braveryInput.text.toString().toIntOrNull()

            if (squadName.isBlank() || movement == null || toughness == null || armorSave == null ||
                dodge == null || feelNoPain == null || leadership == null || selectedModels.isEmpty() || selectedSquadTypes.isEmpty()
            ) {
                Toast.makeText(this, "Please fill in all fields properly & choose models.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            if(selectedModels.size > 200)
            {
                Toast.makeText(this, "This Squad is too big!", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            val squad = Squad(
                name = squadName,
                movement = movement,
                hardness = toughness,
                defense = armorSave,
                dodge = dodge,
                damageResistance = feelNoPain,
                bravery = leadership,
                squadType = selectedSquadTypes.toMutableList(), // Updated squadType
                startingModelSize = selectedModels.size,
                composition = selectedModels.toMutableList(),
                squadAbilities = selectedAbilities.toMutableList()
            )

            GameData.addSquad(this, squad)
            finish()
        }

        discardButton.setOnClickListener {
            finish()
        }
    }
    private fun showAbilitiesDialog()
    {
        val abilities = SquadAbilities::class.java.declaredFields.mapNotNull {
            it.isAccessible = true
            val ability = it.get(SquadAbilities) as? SquadAbility
            ability?.takeIf { !it.isTemporary } // Exclude temporary abilities
        }
        val sortedAbilities = abilities.sortedBy { it.Name }    // Sort abilities alphabetically by Name

        val abilityNames = sortedAbilities.map { it.Name }.toTypedArray()
        val checkedItems = BooleanArray(sortedAbilities.size) { index ->
            selectedAbilities.contains(sortedAbilities[index])
        }
        AlertDialog.Builder(this)
            .setTitle("Select Unit Abilities")
            .setMultiChoiceItems(abilityNames, checkedItems) { _, index, isChecked ->
                val ability = sortedAbilities[index]
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
