package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic

class WeaponListActivity : ComponentActivity()
{
    private lateinit var weaponListView: ListView
    private lateinit var createWeaponButton: Button
    private lateinit var weaponsAdapter: ArrayAdapter<String>
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_weapon_list)
        weaponListView = findViewById(R.id.weaponListView)
        createWeaponButton = findViewById(R.id.createWeaponButton)
        GameData.loadWeaponsFromFile(this)
        updateWeaponList()
        createWeaponButton.setOnClickListener {
            if (GameData.weapons.size > 3000)
            {
                Toast.makeText(this, "You have too many weapons.", Toast.LENGTH_SHORT).show()
            } else {
                playSound(this, R.raw.select)
                val intent = Intent(this, CreateWeaponActivity::class.java)
                startActivity(intent)
            }
        } // Weapon List item click logic (Edit weapon)


        weaponListView.setOnItemClickListener { _, _, position, _ ->
            val selectedWeapon = GameData.weapons[position]
            val intent = Intent(this, CreateWeaponActivity::class.java)
            intent.putExtra("weaponName", selectedWeapon.weaponName)
            intent.putExtra("weaponRange", selectedWeapon.range)
            intent.putExtra("weaponAttacks", selectedWeapon.attacks)
            intent.putExtra("weaponHitSkill", selectedWeapon.hitSkill)
            intent.putExtra("weaponStrength", selectedWeapon.strength)
            intent.putExtra("weaponArmorPen", selectedWeapon.armorPenetration)
            intent.putExtra("weaponDamage", selectedWeapon.damage)
            intent.putExtra("weaponAbilities", ArrayList(selectedWeapon.special))
            startActivity(intent)
        }

        // Weapon List long click logic (Delete weapon)
        weaponListView.setOnItemLongClickListener { _, _, position, _ ->
            val selectedWeapon = GameData.weapons[position]
            AlertDialog.Builder(this)
                .setTitle("Delete Weapon")
                .setMessage("Are you sure you want to delete '${selectedWeapon.weaponName}'?")
                .setPositiveButton("Yes") { _, _ ->
                    GameData.weapons.remove(selectedWeapon)
                    GameData.saveToFile(this, GameData.WEAPONS_FILE_NAME, GameData.weapons)
                    updateWeaponList()
                }
                .setNegativeButton("No", null)
                .show()
            true
        }
    }
    override fun onResume()
    {
        super.onResume()
        updateWeaponList() // Refresh list when returning to this activity
        playMusic(this, R.raw.marching)
    }
    override fun onPause()
    {
        super.onPause()
        pauseMusic()
    }
    private fun updateWeaponList() {
        val weaponNames = GameData.weapons.map { it.weaponName }
        weaponsAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, weaponNames)
        weaponListView.adapter = weaponsAdapter
    }
}
