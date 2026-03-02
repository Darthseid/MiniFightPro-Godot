using Godot;
using System.Collections.Generic;
using System.Linq;


public static class ShapeHelpers
{

    public readonly struct SelectionResult
    {
        public SelectionResult(int selectedTeamId, List<BattleModelActor> selectedActors)
        {
            SelectedTeamId = selectedTeamId;
            SelectedActors = selectedActors;
        }

        public int SelectedTeamId { get; }
        public List<BattleModelActor> SelectedActors { get; }
    }

    public static SelectionResult OnActorSelected(
        BattleModelActor actor,
        Vector2 pointerGlobal,
        BattlePhase currentPhase,
        bool awaitingMovement,
        int activeTeamId,
        int selectedTeamId,
        List<BattleModelActor> selectedActors,
        List<BattleModelActor> activeSquadActors,
        List<BattleModelActor> teamAActors,
        List<BattleModelActor> teamBActors,
        BattleField battleField,
        float maxMoveInches,
        float enemyBufferInches
    )
    {
        if (currentPhase != BattlePhase.Movement || !awaitingMovement)
        {
            return new SelectionResult(selectedTeamId, selectedActors);
        }

        if (actor.TeamId != activeTeamId)
        {
            return new SelectionResult(selectedTeamId, selectedActors);
        }

        var selection = (activeSquadActors ?? new List<BattleModelActor>()).Where(a => a != null && a.TeamId == actor.TeamId).ToList();
        if (selection.Count == 0)
        {
            return new SelectionResult(selectedTeamId, selectedActors);
        }

        if (selectedTeamId != actor.TeamId && selectedActors.Count > 0)
        {
            SetSelectableVisuals(selectedActors, false);
        }

        selectedTeamId = actor.TeamId;
        selectedActors = selection;

        if (currentPhase == BattlePhase.Movement)
        {
            battleField?.BeginDragSquad(selectedActors, pointerGlobal, maxMoveInches, enemyBufferInches);
        }

        return new SelectionResult(selectedTeamId, selectedActors);
    }

    public static void OnDragUpdated(BattleHud battleHud, IReadOnlyList<BattleModelActor> teamA, IReadOnlyList<BattleModelActor> teamB)
    {
    }

    public static void SetSelectableVisuals(IEnumerable<BattleModelActor> actors, bool selected)
    {
        foreach (var actor in actors)
        {
            actor.SetSelectableVisual(selected);
        }
    }

    public static bool CanCharge(Squad activeSquad, MoveVars moveVars, float distanceInches)
    {
        if (distanceInches <= 1f)
        {
            return false;
        }

        if (distanceInches > 12f)
        {
            return false;
        }

        if (moveVars.Retreat)
        {
            return false;
        }

        if (moveVars.Rush && activeSquad.SquadAbilities.All(ability => ability.Innate != "DashBash"))
        {
            return false;
        }

        if (activeSquad.SquadType.Contains("Fortification") || activeSquad.Movement <= 0.01f)
        {
            return false;
        }

        if (activeSquad.SquadType.Contains("Aircraft"))
        {
            return false;
        }

        return true;
    }

    public static bool CanDeclareChargeTarget(Squad attacker, Squad target)
    {
        if (attacker == null || target == null)
        {
            return false;
        }

        if (target.SquadType.Contains("Aircraft") && !attacker.SquadType.Contains("Fly"))
        {
            return false;
        }

        return true;
    }

    public static bool CheckFightRange(float radius)
    {
        return false;
    }
}
