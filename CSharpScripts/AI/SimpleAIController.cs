using System.Collections.Generic;
using System.Linq;

public static class SimpleAIController
{
    public static float ComputeTeamMeleeThreatScore(List<Squad> squads)
    {
        if (squads == null)
        {
            return 0f;
        }

        float total = 0f;
        foreach (var squad in squads)
        {
            if (squad?.Composition == null)
            {
                continue;
            }

            foreach (var model in squad.Composition)
            {
                if (model == null || model.Health <= 0 || model.Tools == null)
                {
                    continue;
                }

                foreach (var weapon in model.Tools)
                {
                    if (weapon == null || !weapon.IsMelee)
                    {
                        continue;
                    }

                    var attacks = Dice.ParseExpression(weapon.Attacks);
                    var damage = Dice.ParseExpression(weapon.Damage);
                    total += attacks * weapon.Strength * damage;
                }
            }
        }

        return total;
    }

    public static bool IsAggressive(float aiThreat, float enemyThreat)
    {
        return aiThreat >= enemyThreat;
    }

    public static Squad? PickClosestEnemySquad(Battle battle, Squad actingSquad, int enemyTeamId, IEnumerable<Squad> candidates)
    {
        if (battle == null || actingSquad == null || candidates == null)
        {
            return null;
        }

        var attackerActors = battle.GetActorsForSquad(actingSquad);
        return candidates
            .Where(squad => squad != null)
            .OrderBy(squad => BoardGeometry.ClosestDistanceInches(attackerActors, battle.GetActorsForSquad(squad)))
            .FirstOrDefault();
    }

    public static List<BattleModelActor> GetActorsForSquad(Battle battle, Squad squad)
    {
        return battle?.GetActorsForSquad(squad) ?? new List<BattleModelActor>();
    }
}
