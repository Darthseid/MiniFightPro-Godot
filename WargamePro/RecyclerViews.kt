package com.example.wargamesimulatorpro

import android.view.*
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class AvailableModelsAdapter(
    private val models: List<Model>,
    private val onModelSelected: (Model) -> Unit
) : RecyclerView.Adapter<AvailableModelsAdapter.ModelViewHolder>()
{
    inner class ModelViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView)
    {
        val modelName: TextView = itemView.findViewById(R.id.modelNameTextView)
    }
    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ModelViewHolder
    {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_model, parent, false)
        return ModelViewHolder(view)
    }

    override fun onBindViewHolder(holder: ModelViewHolder, position: Int)
    {
        val model = models[position]
        holder.modelName.text = model.name
        holder.itemView.setOnClickListener { onModelSelected(model) }
    }
    override fun getItemCount(): Int = models.size
}

class SelectedModelsAdapter(
    private val models: MutableList<Model>,
    var onModelRemoved: (Model) -> Unit
) : RecyclerView.Adapter<SelectedModelsAdapter.ModelViewHolder>()
{
    inner class ModelViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView)
    {
        val modelName: TextView = itemView.findViewById(R.id.modelNameTextView)
    }
    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): ModelViewHolder
    {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_model, parent, false)
        return ModelViewHolder(view)
    }
    override fun onBindViewHolder(holder: ModelViewHolder, position: Int)
    {
        val model = models[position]
        holder.modelName.text = model.name
    }
    override fun getItemCount(): Int = models.size
}
class AvailableSquadsAdapter(
    private val squads: List<Squad>,
    private val onSquadSelected: (Squad) -> Unit,
) : RecyclerView.Adapter<AvailableSquadsAdapter.SquadViewHolder>()
{
    inner class SquadViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView)
    {
        val squadNameTextView: TextView = itemView.findViewById(R.id.squadNameTextView)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SquadViewHolder
    {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_available_squad, parent, false)
        return SquadViewHolder(view)
    }

    override fun onBindViewHolder(holder: SquadViewHolder, position: Int)
    {
        val squad = squads[position]
        holder.squadNameTextView.text = squad.name

        holder.itemView.setOnClickListener {
            onSquadSelected(squad) // Call the lambda when an item is clicked
        }
    }
    override fun getItemCount(): Int = squads.size
}

class SelectedSquadsAdapter(
    private val squads: MutableList<Squad>,
    var onSquadRemoved: (Squad) -> Unit
) : RecyclerView.Adapter<SelectedSquadsAdapter.SquadViewHolder>()
{
    inner class SquadViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView)
    {
        val squadNameTextView: TextView = itemView.findViewById(R.id.squadNameTextView)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SquadViewHolder
    {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_selected_squad, parent, false)
        return SquadViewHolder(view)
    }
    override fun onBindViewHolder(holder: SquadViewHolder, position: Int)
    {
        val squad = squads[position]
        holder.squadNameTextView.text = squad.name
    }
    override fun getItemCount(): Int = squads.size
}


