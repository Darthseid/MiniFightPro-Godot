package com.example.wargamesimulatorpro

import android.view.View
import android.widget.TextView

data class GameUnit
    (
    val physicalTroops: MutableList<GameModel>,
    val forces: Squad,
    var movable: MoveVars
)

data class BoundingBox(var left: Float, var top: Float, var right: Float, var bottom: Float) // Data class for bounding box
{
    fun offset(dx: Float, dy: Float)
    {
        left += dx
        top += dy
        right += dx
        bottom += dy
    }
}

data class GameModel(
    val model: Model,      // The logical representation of the model
    val view: View,        // The visual representation of the model
    val healthText: TextView // Health bar associated with the model, nullable for models with 1 health
)

