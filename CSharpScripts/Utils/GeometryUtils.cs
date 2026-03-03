using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class GeometryUtils
{
    public static float Distance(Vector2 a, Vector2 b)
    {
        return a.DistanceTo(b);
    }

    public static float DistanceInches(Vector2 a, Vector2 b, float pxPerInch)
    {
        return a.DistanceTo(b) / Mathf.Max(0.001f, pxPerInch);
    }

    public static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius, float epsilon = 0.1f)
    {
        return BoardGeometry.SegmentIntersectsCircle(start, end, center, radius, epsilon);
    }

    public static bool HasLineOfSight(Vector2 from, Vector2 to, IEnumerable<TerrainFeature> terrain, float pxPerInch)
    {
        if (terrain == null)
        {
            return true;
        }

        foreach (var feature in terrain.Where(t => t.IsPlaced && t.BlocksLineOfSight))
        {
            var radiusPx = feature.Radius * pxPerInch;
            if (SegmentIntersectsCircle(from, to, feature.Position, radiusPx, 4f))
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasMajorityLineOfSight(IEnumerable<Vector2> attackerPoints, Vector2 targetCenter, IEnumerable<TerrainFeature> terrain, float pxPerInch)
    {
        var points = attackerPoints?.ToList() ?? new List<Vector2>();
        if (points.Count == 0)
        {
            return false;
        }

        var canSee = points.Count(point => HasLineOfSight(point, targetCenter, terrain, pxPerInch));
        return canSee > points.Count / 2;
    }

    public static bool SegmentsBlockedByTerrain(IEnumerable<(Vector2 start, Vector2 end)> segments, IEnumerable<TerrainFeature> terrain, float pxPerInch)
    {
        if (segments == null || terrain == null)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            foreach (var feature in terrain.Where(t => t.IsPlaced && t.BlocksMovement))
            {
                var radiusPx = feature.Radius * pxPerInch;
                if (SegmentIntersectsCircle(segment.start, segment.end, feature.Position, radiusPx))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
