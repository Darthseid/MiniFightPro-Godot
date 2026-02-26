package com.example.wargamesimulatorpro

import android.content.Context
import android.media.MediaPlayer
fun playSound(context: Context, soundResId: Int)
{
    val mediaPlayer = MediaPlayer.create(context, soundResId)
    mediaPlayer.setOnCompletionListener { it.release() } // Release resources after playback
    mediaPlayer.start()
}
object MusicPlayer
{
    private var mediaPlayer: MediaPlayer? = null
    fun playMusic(context: Context, resId: Int)
    {
        if (mediaPlayer == null)
        {
            mediaPlayer = MediaPlayer.create(context, resId).apply {
                isLooping = true
                setVolume(0.7f, 0.7f)
                start()
            }
        } else if (!mediaPlayer!!.isPlaying)
        {
            mediaPlayer!!.start()
        }
    }
    fun pauseMusic()
    {
        mediaPlayer?.pause()
    }
    fun stopMusic()
    {
        mediaPlayer?.stop()
        mediaPlayer?.release()
        mediaPlayer = null
    }
}
