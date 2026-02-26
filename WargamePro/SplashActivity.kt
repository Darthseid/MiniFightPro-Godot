package com.example.wargamesimulatorpro

import android.content.Intent
import android.media.MediaPlayer
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import com.example.wargamesimulatorpro.R

class SplashActivity : AppCompatActivity() {

    private var mediaPlayer: MediaPlayer? = null

    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_splash)
        mediaPlayer = MediaPlayer.create(this, R.raw.lightning).apply {
            setOnCompletionListener {
                navigateToMainActivity() // Navigate when sound finishes
                releaseMediaPlayer() // Release MediaPlayer resources
            }
            start() // Start playback
        }
    }
    private fun navigateToMainActivity()
    {
        val intent = Intent(this, MainActivity::class.java)
        startActivity(intent)
        finish() // Ensure SplashActivity is removed from the back stack
    }
    private fun releaseMediaPlayer()
    {
        mediaPlayer?.release()
        mediaPlayer = null
    }
    override fun onDestroy()
    {
        super.onDestroy()
        releaseMediaPlayer() // Clean up resources if activity is destroyed
    }
}
