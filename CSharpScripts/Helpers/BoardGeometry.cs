using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class BoardGeometry
{
    public static float DistanceInches(BattleModelActor actorA, BattleModelActor actorB)
    {
        if (actorA == null || actorB == null)
            return 0f;
        float centerPx = actorA.GlobalPosition.DistanceTo(actorB.GlobalPosition);
        float averageRadius = (actorA.BaseSizePx + actorB.BaseSizePx) / 4f;
        float edgePx = centerPx - averageRadius;
        return edgePx / GameGlobals.Instance.FakeInchPx;
    }
    public static float ClosestDistanceInches(IReadOnlyList<BattleModelActor> teamA, IReadOnlyList<BattleModelActor> teamB)
    {
        if (teamA == null || teamB == null || teamA.Count == 0 || teamB.Count == 0)
            return 0f;
        float closestEdgePx = float.MaxValue;
        foreach (var a in teamA)
        {
            if (a == null) continue;
            foreach (var b in teamB)
            {
                if (b == null) continue;
                float centerPx = a.GlobalPosition.DistanceTo(b.GlobalPosition);
                float averageRadius = (a.BaseSizePx + b.BaseSizePx) / 4f;
                float edgePx = centerPx - averageRadius;
                if (edgePx < closestEdgePx)
                    closestEdgePx = edgePx;
            }
        }
        return closestEdgePx / GameGlobals.Instance.FakeInchPx;
    }

    public static BattleModelActor GetClosestEnemy(BattleModelActor attacker, IReadOnlyList<BattleModelActor> enemies)
    {
        if (attacker == null || enemies == null || enemies.Count == 0)
        {
            return null;
        }
        return enemies
            .Where(enemy => enemy != null && enemy.BoundModel != null && enemy.BoundModel.Health > 0)
            .MinBy(enemy =>
            {
                float centerPx = enemy.GlobalPosition.DistanceTo(attacker.GlobalPosition);
                float averageRadius = (enemy.BaseSizePx + attacker.BaseSizePx) / 4f;
                return centerPx - averageRadius;
            });
    }

    public static void FaceDelta(BattleModelActor actor, Vector2 delta)
    {
        if (actor == null || delta.LengthSquared() < 0.0001f)
        {
            return;
        }

        float angleDeg = Mathf.RadToDeg(Mathf.Atan2(delta.Y, delta.X));
        const float boundary = 90f;
        var sprite = actor.GetNode<Sprite2D>("MiniSprite");
        var crossedBoundary = angleDeg > boundary || angleDeg < -boundary;

        if (crossedBoundary)
        {
            sprite.Scale = new Vector2(-Mathf.Abs(sprite.Scale.X), sprite.Scale.Y);
            angleDeg = angleDeg > 0 ? angleDeg - 180f : angleDeg + 180f;
        }
        else
        {
            sprite.Scale = new Vector2(Mathf.Abs(sprite.Scale.X), sprite.Scale.Y);
        }

        var rotationDuration = crossedBoundary ? 0.7f : 0.5f;
        var tween = sprite.CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation_degrees", angleDeg, rotationDuration);
    }

    public static void FaceGroupTowardsEnemies(List<BattleModelActor> attackers, List<BattleModelActor> enemies)
    {
        if (attackers == null || enemies == null || attackers.Count == 0 || enemies.Count == 0)
        {
            return;
        }
        var enemyCenter = GetActorsCenter(enemies);

        foreach (var attacker in attackers)
        {
            if (attacker == null)
            {
                continue;
            }
            var delta = enemyCenter - attacker.GlobalPosition;
            FaceDelta(attacker, delta);
        }
    }
    public static async Task<bool> TryMoveIntoEngagement(
    IReadOnlyList<BattleModelActor> movers,
    IReadOnlyList<BattleModelActor> enemies,
    BattleField battleField
)
    {
        if (battleField == null || movers == null || movers.Count == 0 || enemies == null || enemies.Count == 0)
            return false;
        var proposed = new Dictionary<BattleModelActor, Vector2>(movers.Count);     
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        foreach (var mover in movers)
        {
            if (mover == null)
                continue;

            var closestEnemy = enemies
                .Where(e => e != null && e.BoundModel != null && e.BoundModel.Health > 0)
                .OrderBy(e => e.GlobalPosition.DistanceTo(mover.GlobalPosition))
                .FirstOrDefault();
            if (closestEnemy == null)
                continue;
            var enemyPos = closestEnemy.GlobalPosition;
            var moverPos = mover.GlobalPosition;

            
            var front = (moverPos - enemyPos); // "Front" is defined by the approach direction: from enemy -> mover
            if (front.LengthSquared() < 0.0001f)
            {
                // If overlapping positions, pick any stable direction
                front = Vector2.Right;
            }
            else
            {
                front = front.Normalized();
            }   // Pick an angle offset relative to "front" to allow front/side/back placements.  
            float roll = rng.Randf();
            float angleOffset; // Adjust weights + cone widths to taste.
            if (roll < 0.50f)
            {     
                angleOffset = rng.RandfRange(-Mathf.Pi / 3f, Mathf.Pi / 3f);  // 50%: front cone (±60°)
            }
            else if (roll < 0.80f)
            {
               
                float spread = 0.40f; // radians (~23°)
                float sideBase = Mathf.Pi / 2f + rng.RandfRange(-spread, spread);  // 30%: sides (around ±90° with a small spread)
                angleOffset = (rng.Randf() < 0.5f) ? sideBase : -sideBase;
            }
            else
            {              
                angleOffset = Mathf.Pi + rng.RandfRange(-Mathf.Pi / 3f, Mathf.Pi / 3f);  // 20%: back cone (180° ±60°)
            }
            var dir = front.Rotated(angleOffset);
            float minR = (closestEnemy.BaseSizePx + mover.BaseSizePx) / 4f;
            float maxR = minR + GameGlobals.Instance.FakeInchPx - (1 / 5f * GameGlobals.Instance.FakeInchPx);
            float t = rng.Randf();     // Uniform area sample in annulus [minR, maxR]
            float r = Mathf.Sqrt(t * (maxR * maxR - minR * minR) + (minR * minR));
            var target = enemyPos + dir * r;
            proposed[mover] = target;
        }
        if (proposed.Count == 0)
            return false;
        const float chargeMoveDuration = 0.4f;
        foreach (var pair in proposed)
        {
            var mover = pair.Key;
            var target = pair.Value;
            var delta = target - mover.GlobalPosition;
            var tween = mover.CreateTween(); //Tween means an animation that interpolates properties over time. In this case, we're creating a tween to animate the movement of the "mover" actor from its current position to the "target" position.
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(mover, "global_position", target, chargeMoveDuration);
            FaceDelta(mover, delta);
        }

        await battleField.ToSignal(
            battleField.GetTree().CreateTimer(chargeMoveDuration),
            Timer.SignalName.Timeout
        );

        return true;
    }

    public static async Task<bool> TryMoveSquadTowardTarget(
        List<BattleModelActor> movers,
        List<BattleModelActor> targetActors,
        BattleField field,
        float inches)
    {
        return await TryMoveSquadRelativeToTarget(movers, targetActors, field, inches, moveAway: false);
    }

    public static async Task<bool> TryMoveSquadAwayFromTarget(
        List<BattleModelActor> movers,
        List<BattleModelActor> threatActors,
        BattleField field,
        float inches)
    {
        return await TryMoveSquadRelativeToTarget(movers, threatActors, field, inches, moveAway: true);
    }

    private static async Task<bool> TryMoveSquadRelativeToTarget(
        IReadOnlyList<BattleModelActor> movers,
        IReadOnlyList<BattleModelActor> targetActors,
        BattleField field,
        float inches,
        bool moveAway)
    {
        if (field == null || movers == null || movers.Count == 0 || targetActors == null || targetActors.Count == 0)
        {
            return false;
        }

        var liveMovers = movers.Where(m => m != null && m.BoundModel != null && m.BoundModel.Health > 0).ToList();
        var liveTargets = targetActors.Where(m => m != null && m.BoundModel != null && m.BoundModel.Health > 0).ToList();
        if (liveMovers.Count == 0 || liveTargets.Count == 0 || inches <= 0f)
        {
            return false;
        }

        var moverCenter = GetActorsCenter(liveMovers);
        var targetCenter = GetActorsCenter(liveTargets);
        var direction = (targetCenter - moverCenter).Normalized();
        if (moveAway)
        {
            direction = -direction;
        }

        if (direction.LengthSquared() < 0.0001f)
        {
            direction = Vector2.Right;
        }

        var delta = direction * inches * GameGlobals.Instance.FakeInchPx;
        var viewport = field.GetViewportRect().Size;
        const float enemyBufferInches = 1.05f;

        var proposed = new Dictionary<BattleModelActor, Vector2>(liveMovers.Count);
        foreach (var mover in liveMovers)
        {
            var candidate = mover.GlobalPosition + delta;
            var r = mover.BaseSizePx * 0.5f;
            candidate.X = Mathf.Clamp(candidate.X, r, viewport.X - r);
            candidate.Y = Mathf.Clamp(candidate.Y, r, viewport.Y - r);
            proposed[mover] = candidate;
        }

        foreach (var pair in proposed)
        {
            var mover = pair.Key;
            var targetPosition = pair.Value;
            foreach (var threat in liveTargets)
            {
                var minCenterDistance = ((mover.BaseSizePx + threat.BaseSizePx) * 0.25f) + (enemyBufferInches * GameGlobals.Instance.FakeInchPx);
                if (targetPosition.DistanceTo(threat.GlobalPosition) < minCenterDistance)
                {
                    return false;
                }
            }
        }

        const float moveDuration = 0.25f;
        foreach (var pair in proposed)
        {
            var mover = pair.Key;
            var targetPosition = pair.Value;
            var stepDelta = targetPosition - mover.GlobalPosition;
            var tween = mover.CreateTween();
            tween.SetTrans(Tween.TransitionType.Sine);
            tween.SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(mover, "global_position", targetPosition, moveDuration);
            FaceDelta(mover, stepDelta);
        }

        await field.ToSignal(field.GetTree().CreateTimer(moveDuration), Timer.SignalName.Timeout);
        return true;
    }

    public static List<TSquad> GetSquadsWithinRadius<TSquad>(
        TSquad targetSquad,
        IReadOnlyList<BattleModelActor> targetActors,
        IEnumerable<TSquad> candidateSquads,
        System.Func<TSquad, IReadOnlyList<BattleModelActor>> actorSelector,
        float radiusInches)
    {
        var result = new List<TSquad>();
        if (targetActors == null || targetActors.Count == 0 || candidateSquads == null || actorSelector == null)
        {
            return result;
        }

        foreach (var squad in candidateSquads)
        {
            if (squad == null || EqualityComparer<TSquad>.Default.Equals(squad, targetSquad))
            {
                continue;
            }

            var actors = actorSelector(squad);
            if (actors == null || actors.Count == 0)
            {
                continue;
            }

            var distance = ClosestDistanceInches(targetActors, actors);
            if (distance <= radiusInches)
            {
                result.Add(squad);
            }
        }

        return result;
    }

    public static Vector2 GetActorsCenter(List<BattleModelActor> actors)
    {
        var sum = Vector2.Zero;
        foreach (var actor in actors)
        {
            sum += actor.GlobalPosition;
        }
        return sum / actors.Count;
    }
}
