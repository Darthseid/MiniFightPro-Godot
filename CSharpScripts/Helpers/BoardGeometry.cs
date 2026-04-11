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
    public static float ClosestDistanceInches(
     IReadOnlyList<BattleModelActor> teamA,
     IReadOnlyList<BattleModelActor> teamB)
    {
        if (teamA == null || teamB == null || teamA.Count == 0 || teamB.Count == 0)
            return 0f;

        var closestEdgePx =
            (from a in teamA
             where a != null
             from b in teamB
             where b != null
             let centerPx = a.GlobalPosition.DistanceTo(b.GlobalPosition)
             let averageRadius = (a.BaseSizePx + b.BaseSizePx) / 4f // The average radius is used to approximate the edge-to-edge distance between the actors, since they are typically represented as circles in terms of collision and spacing. By subtracting the average radius from the center-to-center distance, we get a better estimate of how close the actors actually are to each other, rather than just how close their centers are.
             select centerPx - averageRadius)
            .Min();

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
            return;

        float angleDeg = Mathf.RadToDeg(Mathf.Atan2(delta.Y, delta.X));
        const float boundary = 90f;
        var sprite = actor.GetNode<Sprite2D>("MiniSprite");
        var crossedBoundary = angleDeg > boundary || angleDeg < -boundary; // If the angle is more than 90 degrees away from the default facing direction, flip the sprite and adjust the angle to keep it within a reasonable range.

        if (crossedBoundary)
        {
            sprite.Scale = new Vector2(-Mathf.Abs(sprite.Scale.X), sprite.Scale.Y);
            angleDeg = angleDeg > 0 ? angleDeg - 180f : angleDeg + 180f;
        }
        else
            sprite.Scale = new Vector2(Mathf.Abs(sprite.Scale.X), sprite.Scale.Y); 

        var rotationDuration = crossedBoundary ? 0.7f : 0.5f;
        var tween = sprite.CreateTween(); // Tween the rotation to the new angle. Using a tween here to make the turning feel smooth and natural, rather than an instant snap.
        tween.SetTrans(Tween.TransitionType.Sine); 
        tween.SetEase(Tween.EaseType.Out); // A slightly longer duration for crossing the boundary to make it feel more natural, since it's a bigger turn.
        tween.TweenProperty(sprite, "rotation_degrees", angleDeg, rotationDuration);
    }

    public static void FaceGroupTowardsEnemies(List<BattleModelActor> attackers, List<BattleModelActor> enemies)
    {
        if (attackers == null || enemies == null || attackers.Count == 0 || enemies.Count == 0)
            return;
        var enemyCenter = GetActorsCenter(enemies);

        foreach (var attacker in attackers)
        {
            if (attacker == null)
                continue;
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

        var liveMovers = movers.Where(m => m != null && m.BoundModel != null && m.BoundModel.Health > 0).ToList();
        var liveEnemies = enemies.Where(e => e != null && e.BoundModel != null && e.BoundModel.Health > 0).ToList();
        if (liveMovers.Count == 0 || liveEnemies.Count == 0)
            return false;

        var proposed = new Dictionary<BattleModelActor, Vector2>(liveMovers.Count);
        var rng = new RandomNumberGenerator();
        rng.Randomize(); //No seed needed.

        const float friendlyMinSeparationInches = 0.3f;
        var minFriendlyEdgeDistancePx = friendlyMinSeparationInches * GameGlobals.Instance.FakeInchPx;

        foreach (var mover in liveMovers)
        {
            var closestEnemy = liveEnemies.MinBy(e => e.GlobalPosition.DistanceTo(mover.GlobalPosition));
            if (closestEnemy == null)
                continue;

            var enemyPos = closestEnemy.GlobalPosition;
            var moverPos = mover.GlobalPosition;
            var front = (moverPos - enemyPos);
            if (front.LengthSquared() < 0.0001f)
                front = Vector2.Right;
            else
                front = front.Normalized(); // The "front" vector points from the mover to the enemy. Absolute direction needed.

            const float spread = 0.4f;    // Precompute constants that don't change per attempt
            float minR = (closestEnemy.BaseSizePx + mover.BaseSizePx) / 4f;
            float maxR = minR + GameGlobals.Instance.FakeInchPx - (friendlyMinSeparationInches * GameGlobals.Instance.FakeInchPx);

            Vector2? foundTarget = null;
            const int maxPlacementAttempts = 30;
            var localMinFriendlyEdgeDistancePx = minFriendlyEdgeDistancePx;             // Local copy of friendly spacing (pixels). This will be reduced by 0.01" each attempt (converted to pixels),
            var perAttemptReductionPx = 0.01f * GameGlobals.Instance.FakeInchPx;

            for (var attempt = 0; attempt < maxPlacementAttempts; attempt++) // The purpose is to surround the enemy target during a charge with the charging squad.
            {
                float roll = rng.Randf();
                float angleOffset;
                if (roll < 0.50f)
                    angleOffset = rng.RandfRange(-Mathf.Pi / 3f, Mathf.Pi / 3f); // This is -60 to +60 degrees on a unit circle.
                else if (roll < 0.80f)
                {
                    float sideBase = Mathf.Pi / 2f + rng.RandfRange(-spread, spread);
                    angleOffset = (rng.Randf() < 0.5f) ? sideBase : -sideBase;
                }
                else
                    angleOffset = Mathf.Pi + rng.RandfRange(-Mathf.Pi / 3f, Mathf.Pi / 3f); // More likely to try directly in front, then the sides, then the rear, but with some randomness to avoid predictability.

                var dir = front.Rotated(angleOffset);
                float t = rng.Randf();
                float r = Mathf.Sqrt(t * (maxR * maxR - minR * minR) + (minR * minR)); // Random point in the annulus between minR and maxR, with a distribution that favors points closer to minR to encourage tighter formations.
                var candidate = enemyPos + dir * r;
                var hasFriendlyConflict = proposed.Any(pair =>
                {
                    var edgeDistance = candidate.DistanceTo(pair.Value) - ((mover.BaseSizePx + pair.Key.BaseSizePx) * 0.25f);
                    return edgeDistance < localMinFriendlyEdgeDistancePx;
                });

                if (!hasFriendlyConflict)
                {
                    foundTarget = candidate;
                    break;
                }
                localMinFriendlyEdgeDistancePx = Mathf.Max(0f, localMinFriendlyEdgeDistancePx - perAttemptReductionPx);   // Reduce the required spacing slightly for the next attempt (inches -> pixels).
            }

            if (proposed.Count == 0)
                return false;
            const float chargeMoveDuration = 0.4f;
            foreach (var pair in proposed)
            {
                var placedMover = pair.Key;
                var target = pair.Value;
                var delta = target - placedMover.GlobalPosition;
                var tween = placedMover.CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine);
                tween.SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(placedMover, "global_position", target, chargeMoveDuration); // Animate the mover to the new position over a short duration to make the movement feel dynamic and impactful, rather than an instant teleport.
                FaceDelta(placedMover, delta);
            }
            await battleField.ToSignal(
                battleField.GetTree().CreateTimer(chargeMoveDuration),
                Timer.SignalName.Timeout
            );
            return true;
        }
        return false; // If we exhaust all movers without finding a valid placement, return false to indicate the move failed.
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
            return false;

        var liveMovers = movers.Where(m => m != null && m.BoundModel != null && m.BoundModel.Health > 0).ToList();
        var liveTargets = targetActors.Where(m => m != null && m.BoundModel != null && m.BoundModel.Health > 0).ToList();
        if (liveMovers.Count == 0 || liveTargets.Count == 0 || inches <= 0f)
            return false;

        var moverCenter = GetActorsCenter(liveMovers);
        var targetCenter = GetActorsCenter(liveTargets);
        var direction = (targetCenter - moverCenter).Normalized();
        if (moveAway)
            direction = -direction;

        if (direction.LengthSquared() < 0.0001f) // If the movers are exactly on top of the targets, just pick an arbitrary direction to move in (to avoid division by zero and to still achieve some movement).
            direction = Vector2.Right;

        var delta = direction * inches * GameGlobals.Instance.FakeInchPx;
        var viewport = field.GetViewportRect().Size;
        const float enemyBufferInches = 1.05f;

        var proposed = new Dictionary<BattleModelActor, Vector2>(liveMovers.Count);
        foreach (var mover in liveMovers) // First calculate the proposed new positions for all movers based on the desired movement direction and distance, while ensuring they stay within the bounds of the battlefield.
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
                    return false;
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
            return result;

        foreach (var squad in candidateSquads)
        {
            if (squad == null || EqualityComparer<TSquad>.Default.Equals(squad, targetSquad)) // Skip null squads and the target squad itself (if it's in the candidate list).
                continue;

            var actors = actorSelector(squad);
            if (actors == null || actors.Count == 0)
                continue;

            var distance = ClosestDistanceInches(targetActors, actors);
            if (distance <= radiusInches)
                result.Add(squad);
        }
        return result;
    }


    private static bool TryPlacePassengerOnRings(
        Vector2 center,
        BattleModelActor passenger,
        float maxRadiusPx,
        Vector2 viewport,
        List<BattleModelActor> enemyList,
        Dictionary<BattleModelActor, Vector2> placements,
        bool avoidFightRange)     // Helper that tries to place a single passenger around a center using candidate rings.
    {
        if (passenger == null)
            return false;

        var candidateRings = new[] { 0.35f, 0.55f, 0.75f, 1f }; // These values can be adjusted in a parameter, but for now they're just hardcoded.
        int ringCount = 30; // Every 12 degrees, a point on the ring is checked.
        for (var ringIndex = 0; ringIndex < candidateRings.Length; ringIndex++) //Note that closer rings are prioritized first.
        {
            var ringRadius = maxRadiusPx * candidateRings[ringIndex];
            for (var i = 0; i < ringCount; i++)
            {
                var angle = Mathf.Tau * (i / (float)ringCount); // Approximately 0.21 radians per iteration.
                var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                var radius = passenger.BaseSizePx * 0.5f;
                candidate.X = Mathf.Clamp(candidate.X, radius, viewport.X - radius);
                candidate.Y = Mathf.Clamp(candidate.Y, radius, viewport.Y - radius);

                var overlapsPassenger = placements.Any(existing => existing.Value.DistanceTo(candidate) < (existing.Key.BaseSizePx + passenger.BaseSizePx) * 0.5f);
                if (overlapsPassenger)
                    continue;

                var inFightRange = enemyList.Any(enemy => candidate.DistanceTo(enemy.GlobalPosition) < (((passenger.BaseSizePx + enemy.BaseSizePx) * 0.25f) + GameGlobals.Instance.FakeInchPx));
                if (avoidFightRange && inFightRange)
                    continue;

                placements[passenger] = candidate;
                return true;
            }
        }

        return false;
    }

    public static Dictionary<BattleModelActor, Vector2> FindValidDisembarkPositionsWithinRadius(
        IReadOnlyList<BattleModelActor> transportActors,
        IReadOnlyList<BattleModelActor> passengerActors,
        float radiusInches,
        bool mustAvoidFightRange,
        IReadOnlyList<BattleModelActor> enemyActors)
    {
        var placements = new Dictionary<BattleModelActor, Vector2>();
        if (transportActors == null || passengerActors == null || transportActors.Count == 0 || passengerActors.Count == 0)
            return placements;

        var transportCenter = GetActorsCenter(transportActors.Where(a => a != null).ToList());
        var viewport = transportActors[0].GetViewportRect().Size;
        var maxRadiusPx = Mathf.Max(0f, radiusInches) * GameGlobals.Instance.FakeInchPx;
        var enemyList = enemyActors?.Where(a => a != null && a.BoundModel != null && a.BoundModel.Health > 0).ToList() ?? new List<BattleModelActor>();

        foreach (var passenger in passengerActors.Where(a => a != null))
        {
            var placed = TryPlacePassengerOnRings(transportCenter, passenger, maxRadiusPx, viewport, enemyList, placements, mustAvoidFightRange);
            if (!placed)
            {
                placements.Clear();
                return placements;
            }
        }

        return placements;
    }

    public static bool PlacePassengerSquadAroundPoint(
        Vector2 centerPoint,
        IReadOnlyList<BattleModelActor> passengerActors,
        float radiusInches,
        IReadOnlyList<BattleModelActor> enemyActors,
        bool avoidFightRange)
    {
        if (passengerActors == null || passengerActors.Count == 0)
            return false;

        var viewport = passengerActors[0].GetViewportRect().Size;
        var maxRadiusPx = Mathf.Max(0f, radiusInches) * GameGlobals.Instance.FakeInchPx;
        var enemyList = enemyActors?.Where(a => a != null && a.BoundModel != null && a.BoundModel.Health > 0).ToList() ?? new List<BattleModelActor>();
        var placements = new Dictionary<BattleModelActor, Vector2>();

        foreach (var passenger in passengerActors.Where(a => a != null))
        {
            var placed = TryPlacePassengerOnRings(centerPoint, passenger, maxRadiusPx, viewport, enemyList, placements, avoidFightRange);
            if (!placed)
                return false;
        }

        foreach (var pair in placements)
            pair.Key.GlobalPosition = pair.Value;

        return true;
    }


    public static bool PlacePassengerSquadAroundTransport(
    IReadOnlyList<BattleModelActor> transportActors,
    IReadOnlyList<BattleModelActor> passengerActors,
    float radiusInches,
    IReadOnlyList<BattleModelActor> enemyActors,
    bool avoidFightRange)
    {
        var placements = FindValidDisembarkPositionsWithinRadius(
            transportActors,
            passengerActors,
            radiusInches,
            avoidFightRange,
            enemyActors);

        if (placements.Count == 0)
            return false;

        foreach (var pair in placements)
            pair.Key.GlobalPosition = pair.Value;

        return true;
    }

    public static Vector2 GetActorsCenter(List<BattleModelActor> actors)
    {
        var sum = Vector2.Zero;
        foreach (var actor in actors)
            sum += actor.GlobalPosition;
        return sum / actors.Count; // Average position of the actors, which serves as a central point for various calculations like facing direction and movement.
    }

    public static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius, float epsilon = 0.1f) //This is for terrain collisions.
    {
        var ab = end - start;
        var abLenSq = ab.LengthSquared();
        if (abLenSq <= 0.0001f)
            return start.DistanceTo(center) <= radius; // If the segment is effectively a point, just check if that point is within the circle.

        var t = Mathf.Clamp((center - start).Dot(ab) / abLenSq, 0f, 1f);
        var closest = start + (ab * t);
        var nearEndpoint = closest.DistanceTo(start) <= epsilon || closest.DistanceTo(end) <= epsilon;
        if (nearEndpoint)
            return false;

        return closest.DistanceTo(center) <= radius;
    }

}
