using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class CombatManager
{
    private readonly Func<List<BattleModelActor>> _getActiveActors;
    private readonly Func<List<BattleModelActor>> _getInactiveActors;
    private readonly Func<Squad> _getTeamASquad;
    private readonly Func<Squad> _getTeamBSquad;
    private readonly Func<MoveVars> _getTeamAMove;
    private readonly Func<MoveVars> _getTeamBMove;
    private readonly Func<Squad, float, bool, List<Squad>> _getSquadsWithinRadius;
    private readonly Func<Squad, List<BattleModelActor>> _getActorsForSquad;
    private readonly Action _postDamageCleanupAndVictoryCheck;
    private readonly Func<BattleModelActor, Squad, Squad, bool> _canActorSeeTargetSquad;
    private readonly BattleHud _battleHud;
    private readonly BattleField _battleField;

    public CombatManager(
        BattleHud battleHud,
        BattleField battleField,
        Func<List<BattleModelActor>> getActiveActors,
        Func<List<BattleModelActor>> getInactiveActors,
        Func<Squad> getTeamASquad,
        Func<Squad> getTeamBSquad,
        Func<MoveVars> getTeamAMove,
        Func<MoveVars> getTeamBMove,
        Func<Squad, float, bool, List<Squad>> getSquadsWithinRadius,
        Func<Squad, List<BattleModelActor>> getActorsForSquad,
        Action postDamageCleanupAndVictoryCheck,
        Func<BattleModelActor, Squad, Squad, bool> canActorSeeTargetSquad)
    {
        _battleHud = battleHud;
        _battleField = battleField;
        _getActiveActors = getActiveActors;
        _getInactiveActors = getInactiveActors;
        _getTeamASquad = getTeamASquad;
        _getTeamBSquad = getTeamBSquad;
        _getTeamAMove = getTeamAMove;
        _getTeamBMove = getTeamBMove;
        _getSquadsWithinRadius = getSquadsWithinRadius;
        _getActorsForSquad = getActorsForSquad;
        _postDamageCleanupAndVictoryCheck = postDamageCleanupAndVictoryCheck;
        _canActorSeeTargetSquad = canActorSeeTargetSquad;
    }

    public async Task ResolveShootingPhaseAsync(string selectedWeaponFingerprint = null, bool hasLineOfSight = true)
    {
        var attackers = _getActiveActors().ToList();
        var defenders = _getInactiveActors();
        if (attackers.Count == 0 || defenders.Count == 0)
            return;

        var attacker = attackers.FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker == null)
            return;

        var target = BoardGeometry.GetClosestEnemy(attacker, defenders);
        if (target == null)
            return;

        var attackerSquad = attacker.TeamId == 1 ? _getTeamASquad() : _getTeamBSquad();
        var defenderSquad = attacker.TeamId == 1 ? _getTeamBSquad() : _getTeamASquad();
        BoardGeometry.FaceGroupTowardsEnemies(attackers, defenders);
        await CombatEngine.ResolveBatchedAttack(
            attacker,
            target,
            false,
            _getTeamASquad(),
            _getTeamBSquad(),
            attackers,
            defenders,
            _getTeamAMove(),
            _getTeamBMove(),
            _battleHud,
            _battleField,
            _postDamageCleanupAndVictoryCheck,
            HandleExplosionProcess,
            selectedWeaponFingerprint,
            false,
            hasLineOfSight,
            actor => _canActorSeeTargetSquad(actor, attackerSquad, defenderSquad));
    }

    public async Task ResolveFightPhaseAsync(string selectedWeaponFingerprint = null)
    {
        var attackers = _getActiveActors().ToList();
        var defenders = _getInactiveActors();
        if (attackers.Count == 0 || defenders.Count == 0)
            return;

        var attacker = attackers.FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker == null)
            return;

        var target = BoardGeometry.GetClosestEnemy(attacker, defenders);
        if (target == null)
            return;

        BoardGeometry.FaceGroupTowardsEnemies(attackers, defenders);
        await CombatEngine.ResolveBatchedAttack(attacker, target, true, _getTeamASquad(), _getTeamBSquad(), attackers, defenders, _getTeamAMove(), _getTeamBMove(), _battleHud, _battleField, _postDamageCleanupAndVictoryCheck, HandleExplosionProcess, selectedWeaponFingerprint);
    }

    public async Task ResolveReactiveFireAsync(int defenderTeamId, Squad defenderSquad, Squad chargingSquad, Action<int> setActiveTeamId, Action<int, Squad> setActiveSquadForTeam)
    {
        setActiveSquadForTeam(defenderTeamId, defenderSquad);
        setActiveSquadForTeam(defenderTeamId == 1 ? 2 : 1, chargingSquad);
        setActiveTeamId(defenderTeamId);

        var attacker = _getActorsForSquad(defenderSquad).FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        var target = _getActorsForSquad(chargingSquad).FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker != null && target != null)
        {
            var attackerActors = _getActorsForSquad(defenderSquad);
            var targetActors = _getActorsForSquad(chargingSquad);
            var hasLos = attackerActors.Any(actor => _canActorSeeTargetSquad(actor, defenderSquad, chargingSquad));
            await CombatEngine.ResolveBatchedAttack(
                attacker,
                target,
                false,
                _getTeamASquad(),
                _getTeamBSquad(),
                attackerActors,
                targetActors,
                _getTeamAMove(),
                _getTeamBMove(),
                _battleHud,
                _battleField,
                _postDamageCleanupAndVictoryCheck,
                HandleExplosionProcess,
                null,
                true,
                hasLos,
                actor => _canActorSeeTargetSquad(actor, defenderSquad, chargingSquad));
        }
    }

    public void HandleExplosionProcess(Squad explodedSquad, Squad enemySquad, int demiseCheck)
    {
        if (explodedSquad == null || enemySquad == null || demiseCheck <= 0 || explodedSquad.SquadAbilities.All(ability => ability.Innate != "Explodes"))
            return;

        var manyExplosions = 0;
        for (int i = 0; i < demiseCheck; i++)
        {
            if (DiceHelpers.SimpleRoll(6) == 1)
                manyExplosions++;
        }

        if (manyExplosions <= 0)
            return;

        const int safetyLimit = 10;
        var processedExplosions = 0;
        while (manyExplosions > 0 && processedExplosions < safetyLimit)
        {
            var explodeDamage = explodedSquad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "Explodes")?.ResolveModifier() ?? 1;
            AudioManager.Instance?.Play("explodes");
            var blastDamage = explodeDamage * manyExplosions;

            foreach (var nearbySquad in _getSquadsWithinRadius(explodedSquad, 6f, true))
            {
                CombatRolls.AllocatePure(blastDamage, nearbySquad);
                foreach (var enemyActor in _getActorsForSquad(nearbySquad))
                    enemyActor.RefreshHp();
            }

            var newExplosions = CombatRolls.AllocatePure(blastDamage, explodedSquad);
            foreach (var explodedActor in _getActorsForSquad(explodedSquad))
                explodedActor.RefreshHp();

            manyExplosions = 0;
            for (int i = 0; i < newExplosions; i++)
            {
                if (DiceHelpers.SimpleRoll(6) == 1)
                    manyExplosions++;
            }
            processedExplosions++;
        }
        _postDamageCleanupAndVictoryCheck();
    }

}
