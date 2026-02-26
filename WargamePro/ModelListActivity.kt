package com.example.wargamesimulatorpro

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.widget.*
import androidx.activity.ComponentActivity
import com.example.wargamesimulatorpro.GameData.MODELS_FILE_NAME
import com.example.wargamesimulatorpro.GameData.models
import com.example.wargamesimulatorpro.GameData.saveToFile
import com.example.wargamesimulatorpro.MusicPlayer.pauseMusic
import com.example.wargamesimulatorpro.MusicPlayer.playMusic

class ModelListActivity : ComponentActivity()
{
    private lateinit var modelListView: ListView
    private lateinit var createModelButton: Button
    private lateinit var modelsAdapter: ArrayAdapter<String>
    override fun onCreate(savedInstanceState: Bundle?)
    {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_model_list)
        modelListView = findViewById(R.id.modelListView)
        createModelButton = findViewById(R.id.createModelButton)
        GameData.loadModelsFromFile(this)
        updateModelList()
        createModelButton.setOnClickListener {
            if (models.size > 3000)
                Toast.makeText(this, "You have too many models.", Toast.LENGTH_SHORT).show()
            else
            {
                playSound(this, R.raw.select)
                val intent = Intent(this, CreateModelActivity::class.java)
                startActivity(intent)
            }
        }
        modelListView.setOnItemClickListener { _, _, position, _ ->
            val selectedModel = models[position]
            val intent = Intent(this, CreateModelActivity::class.java)
            intent.putExtra("modelName", selectedModel.name)
            intent.putExtra("modelHealth", selectedModel.health)
            intent.putExtra("modelBracketed", selectedModel.bracketed)
            intent.putParcelableArrayListExtra("modelTools", ArrayList(selectedModel.tools))
            startActivity(intent)
        }

        modelListView.setOnItemLongClickListener { _, _, position, _ ->
            val selectedModel = models[position]
            AlertDialog.Builder(this)
                .setTitle("Delete Model")
                .setMessage("Are you sure you want to delete '${selectedModel.name}'?")
                .setPositiveButton("Yes") { _, _ ->
                    models.remove(selectedModel)
                    saveToFile(this, MODELS_FILE_NAME, models)
                    updateModelList()
                }
                .setNegativeButton("No", null)
                .show()
            true
        }
    }

    override fun onResume()
    {
        super.onResume()
        updateModelList() // Refresh list when returning to this activity
        playMusic(this, R.raw.marching)
    }

    private fun updateModelList()
    {
        val modelNames = models.map { it.name }
        modelsAdapter = ArrayAdapter(this, android.R.layout.simple_list_item_1, modelNames)
        modelListView.adapter = modelsAdapter
    }
    override fun onPause()
    {
        super.onPause()
        pauseMusic()
    }
}
