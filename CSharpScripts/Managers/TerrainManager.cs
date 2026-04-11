using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TerrainManager
{
    private readonly List<TerrainPiece> _terrainPieces = new();
    private int _terrainCount;
    private int _terrainUnplacedCount;
    private bool _terrainLocked;

    public const float TerrainRadiusInches = 4f;
    public List<TerrainFeature> ActiveTerrain { get; } = new();
    public bool IsLocked => _terrainLocked;
    public int UnplacedCount => _terrainUnplacedCount;

    public void Initialize(int terrainCount)
        { _terrainCount = Math.Max(0, terrainCount); }

    public void BuildTerrainFeatures()
    {
        ActiveTerrain.Clear();
        _terrainUnplacedCount = _terrainCount;
        for (int i = 0; i < _terrainCount; i++)
            ActiveTerrain.Add(new TerrainFeature { Radius = TerrainRadiusInches, IsPlaced = false });
    }

    public void SpawnTerrainPieces(Node parentBattleField, PackedScene terrainPieceScene)
    {
        foreach (var piece in _terrainPieces)
            piece?.QueueFree();

        _terrainPieces.Clear();
        if (parentBattleField == null || terrainPieceScene == null)
            return;

        foreach (var terrain in ActiveTerrain)
        {
            var piece = terrainPieceScene.Instantiate<TerrainPiece>();
            piece.Bind(terrain);
            piece.Visible = false;
            piece.SetLocked(false);
            parentBattleField.AddChild(piece);
            _terrainPieces.Add(piece);
        }

        _terrainLocked = false;
    }

    public bool PlaceNextTerrainAt(Vector2 globalPos, Func<Vector2, Vector2> clampOrTransformIfNeeded = null)
    {
        if (_terrainLocked || _terrainUnplacedCount <= 0)
            return false;

        var next = ActiveTerrain.FirstOrDefault(t => !t.IsPlaced);
        if (next == null)
        {
            _terrainUnplacedCount = 0;
            return false;
        }

        var finalPos = clampOrTransformIfNeeded != null ? clampOrTransformIfNeeded(globalPos) : globalPos;
        next.Position = finalPos;
        next.IsPlaced = true;
        _terrainUnplacedCount = Math.Max(0, _terrainUnplacedCount - 1);

        var piece = _terrainPieces.FirstOrDefault(p => p.Data == next);
        if (piece != null)
        {
            piece.Visible = true;
            piece.GlobalPosition = finalPos;
            piece.Bind(next);
        }

        return true;
    }

    public void LockTerrain()
    {
        _terrainLocked = true;
        _terrainUnplacedCount = 0;
        foreach (var piece in _terrainPieces)
        {
            piece?.SetLocked(true);
            if (piece != null)
                piece.Visible = true;
        }
    }

    public bool IsTerrainBlockingMovement(IEnumerable<(Vector2 start, Vector2 end)> segments, float pxPerInch)
    {
        foreach (var segment in segments)
        {
            foreach (var terrain in ActiveTerrain.Where(t => t.IsPlaced && t.BlocksMovement))
            {
                var radiusPx = terrain.Radius * pxPerInch;
                if (BoardGeometry.SegmentIntersectsCircle(segment.start, segment.end, terrain.Position, radiusPx))
                    return true;
            }
        }

        return false;
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to, float pxPerInch)
    {
        foreach (var terrain in ActiveTerrain.Where(t => t.IsPlaced && t.BlocksLineOfSight))
        {
            var radiusPx = terrain.Radius * pxPerInch;
            if (BoardGeometry.SegmentIntersectsCircle(from, to, terrain.Position, radiusPx, 4f))
                return false;
        }

        return true;
    }

    public bool HasMajorityLineOfSight(IEnumerable<Vector2> attackerPoints, Vector2 targetCenter, float pxPerInch) //Consider recoding this so that the models that can see the target are marked and then fire.
    {
        var points = attackerPoints.ToList();
        if (points.Count == 0)
            return false;

        var canSee = points.Count(point => HasLineOfSight(point, targetCenter, pxPerInch));
        return canSee > points.Count / 2;
    }

    public bool IsSquadInTerrainCover(IEnumerable<Vector2> squadPoints, float pxPerInch)
    {
        var points = squadPoints.ToList();
        if (points.Count == 0)
            return false;

        foreach (var terrain in ActiveTerrain.Where(t => t.IsPlaced && t.ProvidesCover))
        {
            var threshold = (terrain.Radius + 3f) * pxPerInch;
            if (points.Any(point => point.DistanceTo(terrain.Position) <= threshold))
                return true;
        }

        return false;
    }

    public void ApplyTerrainCoverAtCommandPhaseStart(IEnumerable<Squad> squads, Func<Squad, IEnumerable<Vector2>> squadPoints, Action<string> log)
    {
        foreach (var squad in squads)
        {
            squad.SquadAbilities.RemoveAll(ability => ability.IsTemporary && ability.Innate == SquadAbilities.CoverBenefitTemp.Innate);
            if (IsSquadInTerrainCover(squadPoints(squad), Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f)))
            {
                squad.SquadAbilities.Add(SquadAbilities.CoverBenefitTemp);
                log($"[Terrain] Cover applied to {squad.Name}");
            }
        }
    }
}
