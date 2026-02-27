using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleField : Node2D
{
    [Export] public PackedScene ModelActorScene = GD.Load<PackedScene>("res://Scenes/BattleModelActor.tscn");

    public event Action? DragUpdated;
    public event Action? DragEnded;

    private Node2D _unitsTeamA;
    private Node2D _unitsTeamB;
    private readonly List<BattleModelActor> _teamAActors = new();
    private readonly List<BattleModelActor> _teamBActors = new();
    private readonly List<BattleModelActor> _draggingActors = new();
    private readonly List<BattleModelActor> _draggingEnemies = new();
    private readonly Dictionary<BattleModelActor, Vector2> _dragStartPositions = new();
    private readonly Dictionary<BattleModelActor, Vector2> _lastValidPositions = new();
    private float _maxMoveInches = -1f;
    private float _enemyBufferInches = 1.05f;
    private Vector2 _dragStartPointer;
    private bool _isDragging;
    private AudioManager? _audioManager;
    private Sprite2D? _battleBackground;
    private Line2D? _measureLine;
    private Label? _measureLabel;
    private bool _isMeasuringMode;
    private bool _measureActive;
    private Vector2 _measureStartWorld;
    private Line2D? _moveLine;
    private Label? _moveLabel;
    private bool _moveRulerActive;
    private Vector2 _moveOrigin;

    public override void _Ready()
    {
        _unitsTeamA = GetNodeOrNull<Node2D>("UnitsTeamA");
        _unitsTeamB = GetNodeOrNull<Node2D>("UnitsTeamB");
        _audioManager = GetNodeOrNull<AudioManager>("AudioManager");
        _battleBackground = GetNodeOrNull<Sprite2D>("BattleBackground");
        _measureLine = GetNodeOrNull<Line2D>("MeasureLine");
        _measureLabel = GetNodeOrNull<Label>("MeasureLabel");
        _moveLine = GetNodeOrNull<Line2D>("MoveLine");
        _moveLabel = GetNodeOrNull<Label>("MoveLabel");
        ResetMeasureVisuals();
        ResetMoveRulerVisuals();
        FitBackgroundToViewport();

        var sfxRoot = GetNodeOrNull<Node>("SfxPlayers");
        if (_audioManager != null && sfxRoot != null)
        {
            foreach (var child in sfxRoot.GetChildren())
            {
                if (child is AudioStreamPlayer player)
                {
                    _audioManager.Register(player.Name, player);
                }
            }
        }

        SetProcessInput(true);
    }


    private void FitBackgroundToViewport()
    {
        if (_battleBackground?.Texture == null)
        {
            return;
        }

        var viewportSize = GetViewportRect().Size;
        var textureSize = _battleBackground.Texture.GetSize();
        if (textureSize.X <= 0 || textureSize.Y <= 0)
        {
            return;
        }

        _battleBackground.Position = viewportSize * 0.5f;
        var scale = Mathf.Max(viewportSize.X / textureSize.X, viewportSize.Y / textureSize.Y);
        _battleBackground.Scale = new Vector2(scale, scale);
    }


    private static bool IsActorUsable(BattleModelActor? actor)
    {
        return actor != null && GodotObject.IsInstanceValid(actor) && actor.IsInsideTree() && actor.BoundModel != null;
    }

    private static void WarnInvalidActor(string context)
    {
        GD.PushWarning($"[BattleField] Skipping invalid BattleModelActor during {context}. Stale actor reference detected.");
    }

    private static void PruneInvalidActors(List<BattleModelActor> actors, string context)
    {
        for (var i = actors.Count - 1; i >= 0; i--)
        {
            if (IsActorUsable(actors[i]))
            {
                continue;
            }

            WarnInvalidActor(context);
            actors.RemoveAt(i);
        }
    }

    private static void PruneInvalidActorPositions(Dictionary<BattleModelActor, Vector2> positions, string context)
    {
        var invalid = positions.Keys.Where(actor => !IsActorUsable(actor)).ToList();
        foreach (var actor in invalid)
        {
            WarnInvalidActor(context);
            positions.Remove(actor);
        }
    }

    private void PruneTrackingState(string context)
    {
        PruneInvalidActors(_teamAActors, context);
        PruneInvalidActors(_teamBActors, context);
        PruneInvalidActors(_draggingActors, context);
        PruneInvalidActors(_draggingEnemies, context);
        PruneInvalidActorPositions(_dragStartPositions, context);
        PruneInvalidActorPositions(_lastValidPositions, context);

        if (_isDragging && _draggingActors.Count == 0)
        {
            EndDragSquad();
        }
    }

    public void UnregisterActor(BattleModelActor? actor)
    {
        if (actor == null)
        {
            return;
        }

        _teamAActors.Remove(actor);
        _teamBActors.Remove(actor);
        _draggingActors.Remove(actor);
        _draggingEnemies.Remove(actor);
        _dragStartPositions.Remove(actor);
        _lastValidPositions.Remove(actor);

        if (_isDragging && _draggingActors.Count == 0)
        {
            EndDragSquad();
        }
    }

    public void MoveSquadBy(IReadOnlyList<BattleModelActor> actors, Vector2 delta)
    {
        PruneTrackingState("MoveSquadBy");
        if (actors == null || actors.Count == 0)
            return;

        var proposed = new Dictionary<BattleModelActor, Vector2>();

        foreach (var actor in actors)
        {
            if (!IsActorUsable(actor))
            {
                WarnInvalidActor("MoveSquadBy");
                continue;
            }

            proposed[actor] = actor.GlobalPosition + delta;
        }

        if (proposed.Count == 0)
        {
            return;
        }

        var firstActor = proposed.Keys.FirstOrDefault();
        if (firstActor == null)
        {
            return;
        }

        var enemies = GetEnemiesForTeam(firstActor.TeamId);
        if (!ArePositionsValid(proposed, enemies))
            return;

        foreach (var pair in proposed)
        {
            pair.Key.GlobalPosition = pair.Value;
            BoardGeometry.FaceDelta(pair.Key, delta);
        }

        DragUpdated?.Invoke(); // reuse your existing HUD update system
    }


    public void ClearExistingUnits()
    {
        foreach (var actor in _teamAActors)
        {
            actor.QueueFree();
        }
        foreach (var actor in _teamBActors)
        {
            actor.QueueFree();
        }

        _teamAActors.Clear();
        _teamBActors.Clear();
        _draggingActors.Clear();
        _draggingEnemies.Clear();
        _dragStartPositions.Clear();
        _lastValidPositions.Clear();
        _isDragging = false;
        _moveRulerActive = false;
        ResetMoveRulerVisuals();
    }

    public IReadOnlyList<BattleModelActor> SpawnSquad(Squad theSquad, bool isTeamA, Texture2D teamTexture)
    {
        var container = isTeamA ? _unitsTeamA : _unitsTeamB;
        if (container == null || ModelActorScene == null || theSquad == null || theSquad.Composition == null)
            return Array.Empty<BattleModelActor>();
        Vector2 viewportSize = GetViewportRect().Size;


        int count = theSquad.Composition.Count;
        if (count == 0)
            return Array.Empty<BattleModelActor>();

        // --- Columns = ceil(sqrt(count)) ---
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        if (columns < 1) columns = 1;

        int rows = Mathf.CeilToInt(count / (float)columns);

        // --- Base size based on squad types (ported from Kotlin) ---
        float baseMultiplier = GetBaseSizeMultiplier(theSquad.SquadType); // e.g. 4.0f, 4.3f, etc.
        float baseSize = baseMultiplier * GameGlobals.Instance.FakeInchPx * 1.5f; //WSFree has larger base sizes due to only one squad per team.

        if (theSquad.SquadType != null && theSquad.SquadType.Contains("Titanic"))
            baseSize *= 2f;

        // --- Spacing based on base size (ported from Kotlin margins) ---
        float spacingX = baseSize + (GameGlobals.Instance.FakeInchPx); // keep the “+10px” feel proportional
        float spacingY = baseSize + (GameGlobals.Instance.FakeInchPx * 1.5f ); // keep the “+20px” feel proportional

        float padding = 4f * GameGlobals.Instance.FakeInchPx; // <-- 4 fake inches from screen edges as requested

        // Center formation width/height calculations
        float formationWidth = (columns - 1) * spacingX;
        float formationHeight = (rows - 1) * spacingY;

        // Compute start positions:
        // - Team A: spawn 4 fake inches from top-left
        // - Team B: spawn 4 fake inches from bottom-right
        float startX = isTeamA
            ? padding
            : viewportSize.X - formationWidth - padding;
        float startY = isTeamA
            ? padding
            : viewportSize.Y - formationHeight - padding;

        // Clamp to ensure formation stays inside viewport (in case formation is large)
        float maxStartX = Mathf.Max(padding, viewportSize.X - formationWidth - padding);
        float maxStartY = Mathf.Max(padding, viewportSize.Y - formationHeight - padding);
        startX = Mathf.Clamp(startX, padding, maxStartX);
        startY = Mathf.Clamp(startY, padding, maxStartY);

        var actors = new List<BattleModelActor>(count);

        for (int i = 0; i < count; i++)
        {
            var actor = ModelActorScene.Instantiate<BattleModelActor>();
            if (actor == null) continue;

            int row = i / columns;
            int col = i % columns;

            container.AddChild(actor);

            actor.Position = new Vector2(startX + col * spacingX, startY + row * spacingY);

            // Bind logical model + team (you already do this)
            actor.Bind(theSquad.Composition[i], isTeamA ? 1 : 2);

            // Choose texture later by team + squadType (or pass a resolved texture)
            actor.SetTexture(teamTexture);

            // Let the actor size itself (sprite scale / collision / hp label offset)
            actor.SetBaseSize(baseSize);

            actors.Add(actor);
        }

        if (isTeamA) _teamAActors.AddRange(actors);
        else _teamBActors.AddRange(actors);

        return actors;
    }

    public BattleModelActor? SpawnModelActor(Model model, int teamId, Texture2D teamTexture, Vector2 position, float baseSize)
    {
        if (model == null || ModelActorScene == null)
        {
            return null;
        }

        var container = teamId == 1 ? _unitsTeamA : _unitsTeamB;
        if (container == null)
        {
            return null;
        }

        var actor = ModelActorScene.Instantiate<BattleModelActor>();
        if (actor == null)
        {
            return null;
        }

        container.AddChild(actor);
        actor.Position = position;
        actor.Bind(model, teamId);
        actor.SetTexture(teamTexture);
        actor.SetBaseSize(baseSize);

        if (teamId == 1)
        {
            _teamAActors.Add(actor);
        }
        else
        {
            _teamBActors.Add(actor);
        }

        return actor;
    }

    private Vector2 GetSquadCenter(IReadOnlyList<BattleModelActor> actors)
    {
        if (actors == null || actors.Count == 0)
        {
            return Vector2.Zero;
        }

        var sum = Vector2.Zero;
        var count = 0;
        foreach (var actor in actors)
        {
            if (!IsActorUsable(actor))
            {
                continue;
            }

            sum += actor.GlobalPosition;
            count++;
        }

        if (count <= 0)
        {
            return Vector2.Zero;
        }

        return sum / count;
    }

    private void ResetMoveRulerVisuals()
    {
        if (_moveLine != null)
        {
            _moveLine.Visible = false;
            _moveLine.ClearPoints();
        }

        if (_moveLabel != null)
        {
            _moveLabel.Visible = false;
            _moveLabel.Text = string.Empty;
            _moveLabel.Rotation = 0f;
        }
    }

    private void UpdateMoveRuler(Vector2 newCenterWorld)
    {
        if (_moveLine != null)
        {
            var originLocal = ToLocal(_moveOrigin);
            var centerLocal = ToLocal(newCenterWorld);
            _moveLine.Visible = true;
            _moveLine.ClearPoints();
            _moveLine.AddPoint(originLocal);
            _moveLine.AddPoint(centerLocal);
        }

        if (_moveLabel != null)
        {
            var fakeInchPx = GameGlobals.Instance?.FakeInchPx ?? 1f;
            var distInches = _moveOrigin.DistanceTo(newCenterWorld) / Mathf.Max(0.001f, fakeInchPx);
            _moveLabel.Text = $"{distInches:0.0}\"";

            var originLocal = ToLocal(_moveOrigin);
            var centerLocal = ToLocal(newCenterWorld);
            var mid = (originLocal + centerLocal) * 0.5f;
            var dir = centerLocal - originLocal;
            var angle = dir.Angle();
            var normal = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            _moveLabel.Position = mid + normal * 12f;
            _moveLabel.Rotation = angle;
            _moveLabel.Visible = true;
        }
    }

    public void BeginDragSquad(IReadOnlyList<BattleModelActor> actors, Vector2 pointerPosGlobal, float maxMoveInches = -1f, float enemyBufferInches = 1.05f)
    {
        PruneTrackingState("BeginDragSquad");
        if (actors == null || actors.Count == 0)
        {
            return;
        }

        var validActors = actors.Where(IsActorUsable).ToList();
        if (validActors.Count == 0)
        {
            WarnInvalidActor("BeginDragSquad");
            return;
        }

        _draggingActors.Clear();
        _draggingActors.AddRange(validActors);
        _draggingEnemies.Clear();
        _draggingEnemies.AddRange(GetEnemiesForTeam(validActors[0].TeamId).Where(IsActorUsable));

        _dragStartPositions.Clear();
        _lastValidPositions.Clear();
        foreach (var actor in _draggingActors)
        {
            if (!IsActorUsable(actor))
            {
                WarnInvalidActor("BeginDragSquad");
                continue;
            }

            _dragStartPositions[actor] = actor.GlobalPosition;
            _lastValidPositions[actor] = actor.GlobalPosition;
        }

        if (_dragStartPositions.Count == 0)
        {
            _draggingActors.Clear();
            _draggingEnemies.Clear();
            return;
        }

        _dragStartPointer = pointerPosGlobal;
        _maxMoveInches = maxMoveInches;
        _enemyBufferInches = enemyBufferInches;

        SetMeasuringMode(false);
        _moveOrigin = GetSquadCenter(_draggingActors);
        _moveRulerActive = true;
        UpdateMoveRuler(_moveOrigin);

        _isDragging = true;
    }

    public void UpdateDragSquad(Vector2 pointerPosGlobal)
    {
        PruneTrackingState("UpdateDragSquad");
        if (!_isDragging || _draggingActors.Count == 0)
        {
            return;
        }

        var rawDelta = pointerPosGlobal - _dragStartPointer;
        var delta = rawDelta;

        if (_maxMoveInches > 0f)
        {
            var maxDeltaPx = _maxMoveInches * GameGlobals.Instance.FakeInchPx;
            if (rawDelta.Length() > maxDeltaPx)
            {
                delta = rawDelta.Normalized() * maxDeltaPx;
            }
        }

        var proposedPositions = new Dictionary<BattleModelActor, Vector2>(_draggingActors.Count);

        foreach (var actor in _draggingActors)
        {
            if (!IsActorUsable(actor))
            {
                WarnInvalidActor("UpdateDragSquad");
                continue;
            }

            if (!_dragStartPositions.TryGetValue(actor, out var startPos))
            {
                startPos = actor.GlobalPosition;
            }

            proposedPositions[actor] = startPos + delta;
        }

        if (proposedPositions.Count == 0)
        {
            EndDragSquad();
            return;
        }

        if (ArePositionsValid(proposedPositions, _draggingEnemies))
        {
            foreach (var pair in proposedPositions)
            {
                pair.Key.GlobalPosition = pair.Value;
                _lastValidPositions[pair.Key] = pair.Value;
            }
        }
        else
        {
            foreach (var actor in _draggingActors)
            {
                if (!IsActorUsable(actor))
                {
                    continue;
                }

                if (_lastValidPositions.TryGetValue(actor, out var lastValid))
                {
                    actor.GlobalPosition = lastValid;
                }
            }
        }

        if (_moveRulerActive)
        {
            var newCenter = GetSquadCenter(_draggingActors);
            UpdateMoveRuler(newCenter);
        }

        DragUpdated?.Invoke();
    }

    public void EndDragSquad()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragStartPositions.Clear();
        _lastValidPositions.Clear();
        _draggingActors.Clear();
        _draggingEnemies.Clear();
        _maxMoveInches = -1f;
        _enemyBufferInches = 1.05f;
        _moveRulerActive = false;
        ResetMoveRulerVisuals();
        DragEnded?.Invoke();
    }


    public void SetMeasuringMode(bool enabled)
    {
        _isMeasuringMode = enabled;
        if (!enabled)
        {
            _measureActive = false;
            ResetMeasureVisuals();
        }
    }

    private void ResetMeasureVisuals()
    {
        if (_measureLine != null)
        {
            _measureLine.Visible = false;
            _measureLine.ClearPoints();
        }

        if (_measureLabel != null)
        {
            _measureLabel.Visible = false;
            _measureLabel.Text = string.Empty;
        }
    }

    private void StartMeasure(Vector2 pointerWorld)
    {
        _measureStartWorld = pointerWorld;
        _measureActive = true;

        if (_measureLine != null)
        {
            _measureLine.Visible = true;
            _measureLine.ClearPoints();
            var startLocal = ToLocal(pointerWorld);
            _measureLine.AddPoint(startLocal);
            _measureLine.AddPoint(startLocal);
        }

        if (_measureLabel != null)
        {
            _measureLabel.Visible = true;
            _measureLabel.Text = "0.0\"";
            _measureLabel.Position = ToLocal(pointerWorld);
        }
    }

    private void UpdateMeasure(Vector2 pointerWorld)
    {
        if (!_measureActive)
        {
            return;
        }

        var startLocal = ToLocal(_measureStartWorld);
        var currentLocal = ToLocal(pointerWorld);

        if (_measureLine != null)
        {
            _measureLine.Visible = true;
            _measureLine.ClearPoints();
            _measureLine.AddPoint(startLocal);
            _measureLine.AddPoint(currentLocal);
        }

        var fakeInchPx = GameGlobals.Instance?.FakeInchPx ?? 1f;
        var inches = _measureStartWorld.DistanceTo(pointerWorld) / Mathf.Max(0.001f, fakeInchPx);

        if (_measureLabel != null)
        {
            var mid = (startLocal + currentLocal) * 0.5f;
            var delta = currentLocal - startLocal;
            var dir = delta.LengthSquared() > 0.001f ? delta.Normalized() : Vector2.Right;
            var normal = new Vector2(-dir.Y, dir.X);
            var offset = normal * 14f;

            _measureLabel.Visible = true;
            _measureLabel.Text = $"{inches:0.0}\"";
            _measureLabel.Position = mid + offset;
        }
    }

    private void EndMeasure()
    {
        _measureActive = false;
        ResetMeasureVisuals();
    }


    private bool IsPointerOverUi()
    {
        return GetViewport()?.GuiGetHoveredControl() != null;
    }

    public override void _Input(InputEvent @event)
    {
        PruneTrackingState("_Input");

        if (_isMeasuringMode && !_isDragging)
        {
            switch (@event)
            {
                case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && button.Pressed:
                    if (IsPointerOverUi())
                    {
                        if (_measureActive)
                        {
                            EndMeasure();
                        }
                        return;
                    }

                    StartMeasure(GetPointerGlobal(button.Position));
                    GetViewport().SetInputAsHandled();
                    return;

                case InputEventScreenTouch touch when touch.Pressed:
                    if (IsPointerOverUi())
                    {
                        if (_measureActive)
                        {
                            EndMeasure();
                        }
                        return;
                    }

                    StartMeasure(GetPointerGlobal(touch.Position));
                    GetViewport().SetInputAsHandled();
                    return;

                case InputEventMouseMotion motion when _measureActive:
                    if (IsPointerOverUi())
                    {
                        EndMeasure();
                        return;
                    }

                    UpdateMeasure(GetPointerGlobal(motion.Position));
                    GetViewport().SetInputAsHandled();
                    return;

                case InputEventScreenDrag drag when _measureActive:
                    if (IsPointerOverUi())
                    {
                        EndMeasure();
                        return;
                    }

                    UpdateMeasure(GetPointerGlobal(drag.Position));
                    GetViewport().SetInputAsHandled();
                    return;

                case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && !button.Pressed && _measureActive:
                    EndMeasure();
                    GetViewport().SetInputAsHandled();
                    return;

                case InputEventScreenTouch touch when !touch.Pressed && _measureActive:
                    EndMeasure();
                    GetViewport().SetInputAsHandled();
                    return;
            }
        }

        if (!_isDragging)
            return;

        switch (@event)
        {
            case InputEventMouseMotion motion:
                UpdateDragSquad(GetPointerGlobal(motion.Position));
                GetViewport().SetInputAsHandled();
                break;

            case InputEventScreenDrag drag:
                UpdateDragSquad(GetPointerGlobal(drag.Position));
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && !button.Pressed:
                EndDragSquad();
                GetViewport().SetInputAsHandled();
                break;

            case InputEventScreenTouch touch when !touch.Pressed:
                EndDragSquad();
                GetViewport().SetInputAsHandled();
                break;
        }
    }


    private static float GetBaseSizeMultiplier(IReadOnlyList<string>? SquadType)
    {
        if (SquadType == null) return 4.15f;

        if (SquadType.Contains("Infantry")) return 4.0f;
        if (SquadType.Contains("Mounted")) return 4.3f;
        if (SquadType.Contains("Character")) return 4.6f;
        if (SquadType.Contains("Vehicle")) return 4.9f;
        if (SquadType.Contains("Monster")) return 5.2f;
        if (SquadType.Contains("Fortification")) return 5.5f;

        return 4.15f;
    }

    private IReadOnlyList<BattleModelActor> GetEnemiesForTeam(int teamId)
    {
        return teamId == 1 ? _teamBActors : _teamAActors;
    }

    private bool IsMoveValid(IReadOnlyDictionary<BattleModelActor, Vector2> proposedPositions)
    {
        return ArePositionsValid(proposedPositions, _draggingEnemies);
    }

    public bool ArePositionsValid(IReadOnlyDictionary<BattleModelActor, Vector2> proposedPositions, IReadOnlyList<BattleModelActor> enemies)
    {
        PruneTrackingState("ArePositionsValid");
        if (proposedPositions.Count == 0)
        {
            return true;
        }

        var viewportRect = GetViewportRect();
        var inv2 = GetViewport().GetCanvasTransform().AffineInverse();
        var canvasRect = inv2 * viewportRect;
        var enemyList = (enemies ?? Array.Empty<BattleModelActor>()).Where(IsActorUsable).ToList();

        foreach (var pair in proposedPositions)
        {
            var actor = pair.Key;
            if (!IsActorUsable(actor))
            {
                WarnInvalidActor("ArePositionsValid");
                continue;
            }

            var newPos = pair.Value;
            var paddedRect = canvasRect.Grow(-(actor.BaseSizePx / 2f));

            if (!paddedRect.HasPoint(newPos))
            {
                return false;
            }

            foreach (var enemy in enemyList)
            {
                if (!IsActorUsable(enemy))
                {
                    WarnInvalidActor("ArePositionsValid");
                    continue;
                }

                var enemyBufferPx = _enemyBufferInches * GameGlobals.Instance.FakeInchPx;
                var minCenterDistance = MathF.Sqrt((actor.BaseSizePx / 2f) + (enemy.BaseSizePx / 2f));
                minCenterDistance = Mathf.Clamp(minCenterDistance, enemyBufferPx, enemyBufferPx + GameGlobals.Instance.FakeInchPx); // sanity clamp to prevent extreme cases
                // Compute distance from actor center to enemy's closest edge (treating enemy as a circle)
                float centerDistance = newPos.DistanceTo(enemy.GlobalPosition);
                float distanceToEnemyEdge = MathF.Max(0f, centerDistance - (enemy.BaseSizePx / 2f));
                if (distanceToEnemyEdge < minCenterDistance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private Vector2 GetPointerGlobal(Vector2 viewportPosition)
    {
        var inv = GetViewport().GetCanvasTransform().AffineInverse();
        return inv * viewportPosition;
    }
}
