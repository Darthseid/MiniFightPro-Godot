using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class OrderManager
{
    private readonly Battle _battle;
    private readonly HashSet<OrderWindowType> _openWindows = new();
    private readonly Dictionary<int, HashSet<string>> _usedOrdersThisTurn = new();
    private readonly Dictionary<int, bool> _usedOrderThisPhase = new();
    private readonly Dictionary<int, Squad?> _overwatchArmedSquad = new();
    private readonly HashSet<Squad> _fightPhasePrecisionTargets = new();

    public OrderManager(Battle battle)
    {
        _battle = battle;
        _usedOrdersThisTurn[1] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _usedOrdersThisTurn[2] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _usedOrderThisPhase[1] = false;
        _usedOrderThisPhase[2] = false;
        _overwatchArmedSquad[1] = null;
        _overwatchArmedSquad[2] = null;
    }

    public void InitializeBattlePoints()
    {
        _battle.Player1OrderPoints = 0;
        _battle.Player2OrderPoints = 0;
        RefreshHud();
    }

    public void BeginTurn()
    {
        _battle.Player1OrderPoints += 1;
        _battle.Player2OrderPoints += 1;
        _usedOrdersThisTurn[1].Clear();
        _usedOrdersThisTurn[2].Clear();
        _usedOrderThisPhase[1] = false;
        _usedOrderThisPhase[2] = false;
        _overwatchArmedSquad[1] = null;
        _overwatchArmedSquad[2] = null;
        RefreshHud();
    }

    public void ResetPhaseUsage()
    {
        _usedOrderThisPhase[1] = false;
        _usedOrderThisPhase[2] = false;
    }

    public void OpenWindow(OrderWindowType windowType, int activeTeamId)
    {
        _openWindows.Add(windowType);
        RefreshHud();
    }

    public void CloseWindow(OrderWindowType windowType)
    {
        _openWindows.Remove(windowType);
        if (windowType == OrderWindowType.OpponentChargePhaseStart)
        {
            _overwatchArmedSquad[1] = null;
            _overwatchArmedSquad[2] = null;
        }

        RefreshHud();
    }

    public void CloseAllWindows()
    {
        _openWindows.Clear();
        RefreshHud();
    }

    public bool IsWindowOpen(OrderWindowType windowType)
    {
        return _openWindows.Contains(windowType);
    }

    public bool IsOrderUsable(int playerId, Order order, out string reason)
    {
        reason = string.Empty;
        if (order == null)
        {
            reason = "Missing order.";
            return false;
        }

        if (_battle.IsTeamAI(playerId))
        {
            reason = "AI does not use orders.";
            return false;
        }

        if (!_openWindows.Contains(order.WindowType))
        {
            reason = "Wrong timing window.";
            return false;
        }

        if (order.WindowType == OrderWindowType.ChargePhase && _battle.ActiveTeamId != playerId)
        {
            reason = "Only active player can use this now.";
            return false;
        }

        if (order.WindowType == OrderWindowType.OpponentChargePhaseStart && _battle.ActiveTeamId == playerId)
        {
            reason = "Only defending player can use this now.";
            return false;
        }

        if (_usedOrderThisPhase[playerId])
        {
            reason = "Already used an order this phase.";
            return false;
        }

        if (_usedOrdersThisTurn[playerId].Contains(order.OrderId))
        {
            reason = "This order already used this turn.";
            return false;
        }

        if (GetOrderPoints(playerId) < order.OrderCost)
        {
            reason = "Not enough Order Points.";
            return false;
        }

        var targets = GetValidTargets(playerId, order);
        if (order.RequiresTarget && targets.Count == 0)
        {
            reason = "No valid targets.";
            return false;
        }

        return true;
    }

    public IReadOnlyList<Squad> GetValidTargets(int playerId, Order order)
    {
        var targetTeamId = order.TargetSide switch
        {
            OrderTargetSide.Friendly => playerId,
            OrderTargetSide.Self => playerId,
            OrderTargetSide.Enemy => playerId == 1 ? 2 : 1,
            _ => playerId
        };

        var candidates = _battle.GetAliveSquadsForTeam(targetTeamId);
        return candidates.Where(s => IsValidTargetByType(playerId, order, s)).ToList();
    }

    private bool IsValidTargetByType(int playerId, Order order, Squad squad)
    {
        if (!order.MatchesTargetType(squad))
        {
            return false;
        }

        if (order.TargetType == OrderTargetType.Shooters)
        {
            var enemyTeamId = playerId == 1 ? 2 : 1;
            return _battle.SquadHasRangedWeaponThatCanShoot(squad, enemyTeamId);
        }

        return true;
    }

    public async Task<bool> TryActivateOrderAsync(int playerId, string orderId)
    {
        var player = _battle.GetPlayerByTeam(playerId);
        var order = player?.Orders?.FirstOrDefault(o => string.Equals(o.OrderId, orderId, StringComparison.OrdinalIgnoreCase));
        if (order == null)
        {
            return false;
        }

        if (!IsOrderUsable(playerId, order, out var reason))
        {
            _battle.Hud?.ShowToast(reason);
            return false;
        }

        Squad? target = null;
        if (order.RequiresTarget)
        {
            var targetTeamId = order.TargetSide == OrderTargetSide.Enemy ? (playerId == 1 ? 2 : 1) : playerId;
            var validTargets = GetValidTargets(playerId, order);
            target = await _battle.PromptForSquadTargetAsync($"{order.OrderName}: select target squad", targetTeamId, validTargets);
            if (target == null)
            {
                return false;
            }
        }

        ApplyOrderEffect(order, playerId, target);
        SetOrderPoints(playerId, Math.Max(0, GetOrderPoints(playerId) - order.OrderCost));
        _usedOrderThisPhase[playerId] = true;
        _usedOrdersThisTurn[playerId].Add(order.OrderId);
        AudioManager.Instance?.Play("stratagem");
        _battle.Hud?.ShowToast($"{_battle.GetSquadName(playerId)} used {order.OrderName}.");
        RefreshHud();
        return true;
    }

    private void ApplyOrderEffect(Order order, int playerId, Squad? target)
    {
        if (target == null)
        {
            return;
        }

        if (string.Equals(order.OrderId, "epic_challenge", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var weapon in (target.Composition ?? new List<Model>())
                         .SelectMany(model => model.Tools ?? new List<Weapon>())
                         .Where(weapon => weapon.IsMelee))
            {
                if (weapon.Special.All(ability => ability.Innate != WeaponAbilities.TempPrecision.Innate || ability.Name != WeaponAbilities.TempPrecision.Name))
                {
                    weapon.Special.Add(WeaponAbilities.TempPrecision);
                }
            }

            _fightPhasePrecisionTargets.Add(target);
            return;
        }

        if (string.Equals(order.OrderId, "tank_shock", StringComparison.OrdinalIgnoreCase))
        {
            if (target.SquadAbilities.All(ability => ability.Name != SquadAbilities.StampedeTemp.Name || ability.Innate != SquadAbilities.StampedeTemp.Innate))
            {
                target.SquadAbilities.Add(SquadAbilities.StampedeTemp);
            }

            return;
        }

        if (string.Equals(order.OrderId, "fire_overwatch", StringComparison.OrdinalIgnoreCase))
        {
            _overwatchArmedSquad[playerId] = target;
        }
    }

    public void EndFightPhaseCleanup()
    {
        foreach (var squad in _fightPhasePrecisionTargets)
        {
            foreach (var weapon in (squad.Composition ?? new List<Model>()).SelectMany(model => model.Tools ?? new List<Weapon>()))
            {
                weapon.Special.RemoveAll(ability => ability.IsTemporary && ability.Innate == WeaponAbilities.TempPrecision.Innate);
            }
        }

        _fightPhasePrecisionTargets.Clear();
    }

    public async Task TryResolveOverwatchOnChargeDeclaredAsync(int attackerTeamId, Squad chargingSquad, Squad declaredTarget)
    {
        var defenderTeamId = attackerTeamId == 1 ? 2 : 1;
        if (!_overwatchArmedSquad.TryGetValue(defenderTeamId, out var armedSquad) || !ReferenceEquals(armedSquad, declaredTarget))
        {
            return;
        }

        _overwatchArmedSquad[defenderTeamId] = null;
        _battle.Hud?.ShowToast($"{declaredTarget.Name} fires Overwatch!");
        await _battle.ResolveOverwatchAsync(defenderTeamId, declaredTarget, chargingSquad);
        RefreshHud();
    }

    private int GetOrderPoints(int playerId)
    {
        return playerId == 1 ? _battle.Player1OrderPoints : _battle.Player2OrderPoints;
    }

    private void SetOrderPoints(int playerId, int value)
    {
        if (playerId == 1)
        {
            _battle.Player1OrderPoints = value;
        }
        else
        {
            _battle.Player2OrderPoints = value;
        }
    }

    public void RefreshHud()
    {
        _battle.Hud?.ConfigureOrders(_battle.TeamAPlayer, _battle.TeamBPlayer, this, OnOrderPressed);
    }

    private void OnOrderPressed(int playerId, string orderId)
    {
        _ = TryActivateOrderAsync(playerId, orderId);
    }
}
