package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import java.util.Locale

class CreateWeaponActivity : ComponentActivity()
{
    private val selectedAbilities = mutableListOf<WeaponAbility>()
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_create_weapon)
        val weaponNameInput: EditText = findViewById(R.id.weaponNameInput)
        val weaponRangeInput: EditText = findViewById(R.id.weaponRangeInput)
        val weaponAttacksInput: EditText = findViewById(R.id.weaponAttacksInput)
        val weaponHitSkillInput: EditText = findViewById(R.id.weaponHitSkillInput)
        val weaponStrengthInput: EditText = findViewById(R.id.weaponStrengthInput)
        val weaponArmorPenInput: EditText = findViewById(R.id.weaponArmorPenInput)
        val weaponDamageInput: EditText = findViewById(R.id.weaponDamageInput)
        val selectAbilitiesButton: Button = findViewById(R.id.selectAbilitiesButton)
        val saveButton: Button = findViewById(R.id.saveButton)
        val discardButton: Button = findViewById(R.id.discardButton)
        val extras = intent.extras
        if (extras != null)
        {
            val locale = Locale.getDefault() // You can use Locale.US if you want US-specific formatting
            weaponNameInput.setText(extras.getString("weaponName"))
            weaponRangeInput.setText(String.format(locale, "%.2f", extras.getFloat("weaponRange")))
            weaponAttacksInput.setText(extras.getString("weaponAttacks"))
            weaponHitSkillInput.setText(String.format(locale, "%d", extras.getInt("weaponHitSkill")))
            weaponStrengthInput.setText(String.format(locale, "%d", extras.getInt("weaponStrength")))
            weaponArmorPenInput.setText(String.format(locale, "%d", extras.getInt("weaponArmorPen")))
            weaponDamageInput.setText(extras.getString("weaponDamage"))
            val abilities = extras.getParcelableArrayList<WeaponAbility>("weaponAbilities")
            if (abilities != null)
                selectedAbilities.addAll(abilities)
        }
        selectAbilitiesButton.setOnClickListener {  showAbilitiesDialog() } // Save button logic
        saveButton.setOnClickListener {
            val weaponName = weaponNameInput.text.toString()
            val weaponRange = weaponRangeInput.text.toString().toFloatOrNull() ?: 0f
            val weaponAttacks = weaponAttacksInput.text.toString()
            val weaponHitSkill = weaponHitSkillInput.text.toString().toLongOrNull() ?: 9  //Higher hit skills are worse.
            val weaponStrength = weaponStrengthInput.text.toString().toLongOrNull() ?: 0
            val weaponArmorPen = weaponArmorPenInput.text.toString().toLongOrNull() ?: 0
            val weaponDamage = weaponDamageInput.text.toString()
            if (weaponName.isBlank())
            {
                Toast.makeText(this, "Weapon name cannot be empty.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            try
            {  damageParser(weaponAttacks) }
            catch (e: Exception)
             {
                    Toast.makeText(this, "Weapon Attacks must be an integer or in D3/D6 format.", Toast.LENGTH_SHORT).show()
                    return@setOnClickListener
            }
            try { damageParser(weaponDamage) }
            catch (e: Exception)
            {
                Toast.makeText(this, "Weapon Damage must be an integer or in D3/D6 format.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            if (weaponHitSkill !in 0..9 ||
                weaponStrength !in 0..99 ||
                weaponArmorPen !in -9..9
                || weaponRange !in 0f..999f
            ) {
                Toast.makeText(this, "Values are out of bounds. Please enter valid numbers.", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }
            val weapon = Weapon(
                weaponName = weaponName,
                range = weaponRange,
                attacks = weaponAttacks,
                hitSkill = weaponHitSkill.toInt(),
                strength = weaponStrength.toInt(),
                armorPenetration = weaponArmorPen.toInt(),
                damage = weaponDamage,
                special = selectedAbilities.toMutableList()
            )
            GameData.addWeapon(this, weapon)
            finish()
        }
        discardButton.setOnClickListener {
            finish() // Close activity without saving
        }
    }
    private fun showAbilitiesDialog()
    {
        val abilities = WeaponAbilities::class.java.declaredFields.mapNotNull {
            it.isAccessible = true
            val ability = it.get(WeaponAbilities) as? WeaponAbility
            ability?.takeIf { !it.isTemporary } // Exclude temporary abilities
        }
        val sortedAbilities = abilities.sortedBy { it.Name }
        val abilityNames = sortedAbilities.map { it.Name }.toTypedArray()
        val checkedItems = BooleanArray(abilities.size) { index ->
            selectedAbilities.contains(abilities[index])
        }
        AlertDialog.Builder(this)
            .setTitle("Select Weapon Abilities")
            .setMultiChoiceItems(abilityNames, checkedItems) { _, index, isChecked ->
                val ability = abilities[index]
                if (isChecked)
                    selectedAbilities.add(ability)
                else
                    selectedAbilities.remove(ability)
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
