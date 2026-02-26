package com.example.wargamesimulatorpro

import android.Manifest
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.content.res.Configuration
import android.os.Bundle
import android.widget.Button
import androidx.activity.ComponentActivity
import androidx.annotation.RequiresPermission
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import com.google.firebase.analytics.FirebaseAnalytics


class MainActivity : ComponentActivity()
{
    private lateinit var firebaseAnalytics: FirebaseAnalytics
    private var activityStartTime: Long = 0
    @RequiresPermission(allOf = [Manifest.permission.INTERNET, Manifest.permission.ACCESS_NETWORK_STATE, Manifest.permission.WAKE_LOCK])
    override fun onCreate(savedInstanceState: Bundle?)
    {
        playMusic(this, R.raw.marching)
        val sharedPreferences: SharedPreferences = getSharedPreferences("AppPrefs", Context.MODE_PRIVATE)
        firebaseAnalytics = FirebaseAnalytics.getInstance(this)
       activityStartTime = System.currentTimeMillis()  // Record the start time when the activity is opened
        super.onCreate(savedInstanceState)
        if (resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE)
                setContentView(R.layout.activity_main_landscape)
        else
            setContentView(R.layout.activity_main_portrait)
        val isFirstRun = sharedPreferences.getBoolean("isFirstRun", true)
        if (isFirstRun)
        {
            populatePresetData(this)
            with(sharedPreferences.edit())
            {
                putBoolean("isFirstRun", false)
                apply()
            }
        }
        GameData.loadModelsFromFile(this)
        GameData.loadWeaponsFromFile(this)
        GameData.loadSquadsFromFile(this)
        GameData.loadPlayersFromFile(this)
        val switchButton: Button = findViewById(R.id.readRulesButton)// Initialize the "Read Rules" button
        switchButton.setOnClickListener {
            val intent = Intent(this, RulesActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
        findViewById<Button>(R.id.createWeaponButton).setOnClickListener {
            val intent = Intent(this, WeaponListActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
        findViewById<Button>(R.id.createMiniatureButton).setOnClickListener {
            val intent = Intent(this, ModelListActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
        findViewById<Button>(R.id.createSquadButton).setOnClickListener {
            val intent = Intent(this, SquadListActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
        findViewById<Button>(R.id.startGameButton).setOnClickListener {
            val intent = Intent(this, StartGameActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
        findViewById<Button>(R.id.createPlayerButton).setOnClickListener {
            val intent = Intent(this, PlayerListActivity::class.java)
            playSound(this,R.raw.select)
            startActivity(intent)
        }
    }
    override fun onPause()
    {
        super.onPause()
        pauseMusic() // Pause music whenever the app is paused (background or sleep)
    }
    override fun onResume()
    {
        super.onResume()
        playMusic(this, R.raw.marching)
    }
    override fun onDestroy()
    {
        super.onDestroy()    // Calculate the time spent in the activity
        val timeSpent = System.currentTimeMillis() - activityStartTime
        val bundle = Bundle().apply {
            putString(FirebaseAnalytics.Param.ITEM_NAME, this@MainActivity.javaClass.simpleName)
            putLong("time_spent", timeSpent)
        }
        firebaseAnalytics.logEvent("activity_time_spent", bundle)   // Log a custom event for time spent in the activity
    }
}
