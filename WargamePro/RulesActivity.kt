package com.example.wargamesimulatorpro

import android.os.Bundle
import android.webkit.WebView
import androidx.activity.*
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic
import com.example.wargamesimulatorpro.R

class RulesActivity : ComponentActivity()
{
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContentView(R.layout.activity_rules)
        val webView: WebView = findViewById(R.id.rulesWebView)
        webView.loadUrl("file:///android_asset/MiniRules.htm") // Load HTML file
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