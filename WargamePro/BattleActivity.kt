package com.example.wargamesimulatorpro

import android.annotation.SuppressLint
import android.content.Intent
import android.content.res.Configuration
import android.os.Build
import android.os.Bundle
import android.util.DisplayMetrics
import android.view.Gravity
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.OnBackPressedCallback
import androidx.annotation.RequiresApi
import androidx.window.layout.WindowMetricsCalculator
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import com.google.firebase.analytics.FirebaseAnalytics
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

lateinit var rootLayout: ViewGroup
lateinit var distanceTextView: TextView
lateinit var displayMetrics: DisplayMetrics
lateinit var playerOne: SuperPlayer
lateinit var playerTwo: SuperPlayer
lateinit var continueView: TextView
private lateinit var firebaseAnalytics: FirebaseAnalytics
private var battleStartTime: Long = 0

class BattleActivity : ComponentActivity()
{
    companion object
    {
        var turn: Int = 0
        var round: Int = 1
        var fakeInchPx = 0f
        var battleJob: Job? = null
    }
    @RequiresApi(Build.VERSION_CODES.TIRAMISU)
    @SuppressLint("ClickableViewAccessibility")
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true)
        {
            override fun handleOnBackPressed()
            {
                battleJob?.cancel()  // Handle back button
                battleJob = null
                startActivity(Intent(this@BattleActivity, MainActivity::class.java))
                finish()
            }
        })
        setContentView(R.layout.activity_battle)
        rootLayout = findViewById(R.id.root_layout)
        val windowMetrics = WindowMetricsCalculator.getOrCreate().computeCurrentWindowMetrics(this)
        val bounds = windowMetrics.bounds
        val screenWidth = bounds.width()    // Get screen size
        val screenHeight = bounds.height()

        displayMetrics = DisplayMetrics().apply {
            widthPixels = screenWidth
            heightPixels = screenHeight
            density = resources.displayMetrics.density
        }
        fakeInchPx = 7.1f * resources.displayMetrics.density
        playerOne = SuperPlayer(intent.getParcelableExtra("player1") ?: error("Player 1 not found"), mutableListOf())
        playerTwo = SuperPlayer(intent.getParcelableExtra("player2") ?: error("Player 2 not found"), mutableListOf())
        var increment = 0
        playerOne.thePlayer.theirSquads.forEach { squad ->
            val x = (3f+increment * randomPosition(increment)) * fakeInchPx
            val y = (3f+increment * randomPosition(increment)) * fakeInchPx
            val unitImage = getUnitOnePicture(squad)
            val gameUnit = createGameUnit(squad, rootLayout, unitImage, this, x, y)
            playerOne.deployed.add(gameUnit)
            increment++
        }
        increment = 0
        playerTwo.thePlayer.theirSquads.forEach { squad ->
            val x = (35f+increment * randomPosition(increment)) * fakeInchPx
            val y = (35f+increment * randomPosition(increment)) * fakeInchPx
            val unitImage = getUnitTwoPicture(squad)
            val gameUnit = createGameUnit(squad, rootLayout, unitImage, this, x, y)
            playerTwo.deployed.add(gameUnit)
            increment++
        }
        distanceTextView = findViewById(R.id.distance_text_view)
        continueView = findViewById(R.id.next_phase_button)
        firebaseAnalytics = FirebaseAnalytics.getInstance(this)
        battleStartTime = System.currentTimeMillis()
        firebaseAnalytics.logEvent("battle_started", null)
        battleJob = CoroutineScope(Dispatchers.Main).launch {
            try {
                playMatch(this@BattleActivity, playerOne, playerTwo)
            } catch (e: CancellationException)
            {
                e.printStackTrace()
            }
        }
    }
    private fun getUnitOnePicture(squad: Squad): Int
    {
        return when
        {
            "Aircraft" in squad.squadType -> R.drawable.combatjet
            "Titanic" in squad.squadType -> R.drawable.mecha
            "Fortification" in squad.squadType -> R.drawable.fort
            "Character" in squad.squadType -> R.drawable.vip
            "Mounted" in squad.squadType -> R.drawable.biker
            "Monster" in squad.squadType -> R.drawable.monsterbug
            "Vehicle" in squad.squadType -> R.drawable.tank
            "Infantry" in squad.squadType -> R.drawable.gunman
            else -> R.drawable.circle_shape_green
        }
    }
    private fun getUnitTwoPicture(squad: Squad): Int
    {
        return when
        {
            "Aircraft" in squad.squadType -> R.drawable.helicopter
            "Fortification" in squad.squadType -> R.drawable.fort2
            "Character" in squad.squadType -> R.drawable.vip2
            "Mounted" in squad.squadType -> R.drawable.dinorider
            "Monster" in squad.squadType -> R.drawable.monsterspike
            "Vehicle" in squad.squadType -> R.drawable.tank2
            "Infantry" in squad.squadType -> R.drawable.gunman2
            else -> R.drawable.circle_shape_blue
        }
    }
    override fun onConfigurationChanged(newConfig: Configuration)
    {
        super.onConfigurationChanged(newConfig)
        (distanceTextView.layoutParams as? FrameLayout.LayoutParams)?.apply {
            gravity = Gravity.TOP or Gravity.CENTER_HORIZONTAL
            topMargin = 16
            distanceTextView.layoutParams = this
        }
        val allGameUnits = playerOne.deployed + playerTwo.deployed
        allGameUnits.flatMap { it.physicalTroops }.forEach { gameModel ->
            gameModel.view.x = gameModel.view.x
            gameModel.view.y = gameModel.view.y
            gameModel.healthText.let { healthBar ->
                healthBar.x = healthBar.x
                healthBar.y = healthBar.y
            }
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
    override fun onDestroy()
    {
        super.onDestroy()
        val battleDuration = System.currentTimeMillis() - battleStartTime
        val bundle = Bundle().apply {
            putLong("battle_duration", battleDuration)
        }
        firebaseAnalytics.logEvent("battle_completed", bundle)
    }
}