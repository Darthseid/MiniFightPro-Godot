![Logo](minifightappicon.png)

MINIATURE FIGHT RULES AND HELP

# Squad Creation

Squad Creation is the customization place for Miniature Fight. This is where you create and customize Squads, Miniatures, Weapons, and their abilities.

Miniature Fight uses six-sided dice conventions in rules text:
- **D6** means roll one six-sided die.
- **D3** means roll one three-result die (1-3).

Each Squad has a name, Type, **🏃**, **🪨**, **❤️**, **🏳️**, **🛡️**, **➗**, and **🔮**.

- **🏃 Move**: how far the Squad normally moves (inches).
- **🪨 Hardness**: toughness used against Injury rolls.
- **🏳️ Bravery**: used for shellshock and bravery-based tests.
- **🛡️ Defense**: armor save value.
- **🔮 Dodge**: dodge save value.
- **➗ Damage Resistance**: resistance roll used against allocated damage.

Each Miniature has **❤️ Health**, a LowHealth threshold (usually causes a hit penalty when reached), and weapons.

**Squad Types** include Infantry, Character, Mounted, Monster, Building, Vehicle, Aircraft, Fortification, Fly, and Transport.

Each Weapon has a Name, **🏹 Range**, **🤺 Attacks**, **🔫 Hit Skill**, **💪 Strength**, **🕳️ Armor Penetration**, and **🩸 Damage**.

- **🏹 Range** under 1" is treated as melee.
- **🤺 Attacks** can be an integer or a D3/D6 expression.
- **🩸 Damage** can be an integer or a D3/D6 expression.

## Dice Conventions

For attack counts, damage, and variable modifiers, accepted expressions are:
- Integer constants (`1`, `2`, `7`)
- `D6`, `D3`
- Multiples and optional constants, such as `2D6+3`, `3D3`, `D6+2`, `2D3-1`

Use only **D6** and **D3** as base die types in rules content. Do not use other die families (for example D8), fractions, or mixed custom notation.

# Start Game

Press Start Game, choose one Squad for each team, then press Play.

The Active Player is the player whose turn is currently being resolved.

## Continue Arrow and Phase Progression

The **Continue →** button advances phase flow whenever the game is waiting for player confirmation. If Continue is not pressed, the game waits.

A turn progresses through:
- Terrain Setup (if enabled)
- Squad Deployment
- Starting
- Movement
- Shooting
- Engagement (charge)
- Melee
- End Turn

## Ruler Tool

Use the built-in ruler/measure tool to check distances between squads, models, and terrain during a match. This is intended for movement distances, shooting range checks, engagement/fight-range checks, and aura ranges.

# Core Battle Rules

## Fight Range and 1" Movement Rule

- **Fight Range is 1 inch.**
- During normal movement, models cannot end within 1" of enemy models.
- Moving out of Fight Range is a **Retreat**.
- Entering within 1" of enemy models is only done through valid charging/engagement movement.

## Movement: Standard, Rush, Retreat, Aircraft, Teleport

- **Standard Move:** up to the squad's movement allowance.
- **Rush (Advance):** adds **D6** movement. A squad that rushed cannot normally shoot or charge later that turn.
- **Charge After Rush:** bypasses the normal no-charge-after-rush restriction.
- **Retreat (Fall Back):** used when leaving Fight Range; squads that retreat cannot normally shoot or charge that turn.
- **Retreat + Shell-shocked:** can trigger mini losses (rout check).

### Aircraft movement

- Aircraft cannot declare charges.
- Aircraft movement is treated as a special move profile and must satisfy the minimum move rule when enforced: **at least 20"**.
- Aircraft are valid charge targets only for attackers with **Fly**.
- Aircraft movement is not blocked by terrain in the same way non-Fly charges/moves are handled.

### Teleport movement

- Squads with **Teleport** can make teleport-style movement.
- Teleport placement can ignore normal movement cap in teleport cases.
- Teleport must end **more than 9"** away from enemy squads.

## Shooting Restrictions

- Non-melee weapons require valid range.
- A squad that rushed cannot shoot unless the weapon has **Skirmish** or another enabling rule.
- A squad that retreated cannot shoot unless it has **Shoot Retreat**.
- Non-Monster/Vehicle squads in Fight Range can only shoot with **Pistol** weapons.
- Monsters/Vehicles can shoot in Fight Range with a hit penalty.
- **Blast** weapons can never be fired while the attacker is in Fight Range.

## Terrain

Terrain is set before normal play:
- Players choose terrain count and place pieces during Terrain Setup.
- Terrain can be repositioned before it is locked.
- Press Continue to lock terrain and proceed.

Terrain rules:
- Terrain pieces have a fixed radius (4").
- Terrain blocks movement and charge paths for non-Fly movement checks.
- Terrain blocks line of sight.
- Squads within 3" of a terrain piece gain cover benefits.
- Once locked, terrain no longer moves.
- The default terrain visual is the ruins texture (`Ruins.png` in the terrain scene).

## Aircraft and Fortification Restrictions

- **Aircraft:** cannot charge; can only be charged by units with **Fly**.
- **Fortifications / 0 Move squads:** cannot charge, but can be charged by valid enemy squads.

## Shellshock Tests

A squad is **Understrength** if it has lost over half its starting models (or equivalent single-model health threshold logic).

Understrength squads test at start-of-turn:
- Roll **2D6**.
- Pass if total (after modifiers) is at least the squad's **🏳️ Bravery**.
- Fail = squad becomes or remains **Shell-shocked**.

Shell-shocked effects:
- Retreat can cause rout losses.
- Rush/Retreat success contexts are harder (bravery pressure and penalties apply through current rule hooks).
- Some abilities interact with this test (for example **Reroll Bravery**, **Demonic Grief**, **Alien Terror**, **Hive Mind**, **Grim**).
- **Epic Bravery** order removes Shell Shock from a friendly squad.

## Perilous Tests

After a model fires a weapon with **Perilous**:
- Roll **1D6** through the dice overlay.
- On a **1**, perilous backlash is applied.

Current perilous resolution:
- Infantry bearer: the firing model is destroyed.
- Non-Infantry bearer: takes 3 pure self-damage.

Perilous rolls are normal interactive dice events, so they support:
- **Command Reroll** (if legal and available)
- **Fate Six** replacement (if legal and available)

# Orders and Order Points

Orders are timed commands. Players spend **Order Points (OP)** to activate them.

- Each player gains Order Points over the battle flow (starting at 0 and gaining during turn progression).
- Each player can use only **one order per phase window**.
- Orders are limited by timing windows and target restrictions.
- A player can also spend **1 OP** for a **Command Reroll**.

## Command Reroll (Order Reroll)

- Spend **1 OP** to reroll one die in the current roll event.
- Only your own dice can be rerolled.
- Only one Command Reroll per player per phase.
- A die cannot be rerolled if it was already rerolled.
- A die cannot be rerolled if it was replaced by Fate Six.
- Only reroll-eligible dice can be selected.

## Default Orders

| Name | Cost / Window / Target / Effect |
|---|---|
| Epic Challenge | **1 OP**; Start of Melee; friendly Character; grants temporary Precision to melee weapons for that melee phase. |
| Tank Shock | **1 OP**; Engagement phase; friendly Vehicle; grants temporary Stampede until end of turn. |
| Go to Ground | **1 OP**; when targeted in opponent Shooting; friendly Infantry; gains temporary Cover Benefit and Six Plus Dodge for that phase. |
| Counter-Offensive | **2 OP**; Start of Melee; friendly fight-eligible squad; gains temporary First Strike for the turn. |
| Heroic Intervention | **2 OP**; opponent melee timing; eligible friendly squad within 6" can move into engagement against a valid enemy already tied up. |
| Mists of Deimos | **3 OP**; Start of opponent Shooting; remove friendly squad into reserve, return next shooting via teleport-style placement, cannot charge that turn after return. |
| Epic Bravery | **2 OP**; Start of Movement; friendly squad; removes Shell Shock. |
| Fire Overwatch | **1 OP**; Start of opponent Engagement; friendly shooter squad is armed to fire Overwatch when charged. |

# Weapon Abilities

| Name | Effect |
|---|---|
| Bonus Hits X | Critical hits generate additional hit rolls equal to X. |
| Precision | Allocates successful injuries to less common/priority defenders first. |
| Hard Hits | Critical hits auto-penetrate. |
| Pike | Charge-synergy melee profile (used as a temporary melee pressure effect). |
| Conversion | At ranges over 12", critical threshold improves (typically 4+). |
| Devastating Injuries | Critical injuries are especially lethal and bypass normal save flow. |
| Perilous | After firing, roll 1D6; on 1, attacker suffers perilous backlash. |
| Hefty | If the attacker did not move, gains +1 to hit. |
| Ignores Cover | Target does not gain cover/AP mitigation from cover for that attack. |
| Fusion X | Within half range, adds X damage. |
| One Shot | Weapon is expended after firing (attacks set to 0 for matching profile). |
| Pistol | Can be fired in Fight Range where non-pistol weapons are restricted. |
| Rapid Fire X | Within half range, gains X additional attacks. |
| Skirmish | Weapon can be fired after rushing. |
| Anti-Infantry X | Critical injury threshold improves against Infantry targets (X+). |
| Anti-Monster X | Critical injury threshold improves against Monster targets (X+). |
| Anti-Vehicle X | Critical injury threshold improves against Vehicle targets (X+). |
| Anti-Fly X | Critical injury threshold improves against Fly targets (X+). |
| Anti-Character X | Critical injury threshold improves against Character targets (X+). |
| Anti-Psychic X | Critical injury threshold improves against Psychic targets (X+). |
| Blast | Gains extra attacks based on target size; cannot fire in Fight Range. |
| Reroll Injuries | Reroll failed injury rolls. |
| Psychic | Marks attack as psychic (interacts with Psionic Defense). |
| One Hit Reroll | One failed hit die can be rerolled. |
| Reroll Hits | Reroll failed hit rolls. |
| Reroll Hit Ones | Reroll unmodified hit rolls of 1. |
| Reroll Injury Ones | Reroll unmodified injury rolls of 1. |
| One Injury Reroll | One failed injury die can be rerolled. |
| Plus One Injuries | +1 to injury rolls. |
| Multi-Profile | Weapon profile can be selected from grouped variants before resolving attack. |
| Indirect Fire | Can shoot without line of sight; if fired without LOS, applies -1 hit and target gets cover benefits. |

# Squad Abilities

| Name | Effect |
|---|---|
| -1 to Hit Ranged | Enemy ranged attacks against this squad take -1 to hit. |
| -1 to Hit Melee | Enemy melee attacks against this squad take -1 to hit. |
| Explode on Death X | On destruction, can deal pure explosion damage in area. |
| -1 to Hit (All) | Universal -1 to be hit. |
| Aircraft | Uses aircraft movement/charge restrictions. |
| Alien Terror | Enemy can be forced into shellshock-style pressure tests. |
| Demonic Grief | Bravery/shellshock aura interaction with regeneration/punishment hooks. |
| Close Up To Shoot (Camouflaged) | Enemy cannot target this squad with shooting beyond 12". |
| First Strike | Fights first in melee ordering. |
| Temp First Strike | Temporary first-strike version from orders/effects. |
| Shoot Retreat | Can shoot after retreating. |
| Charge After Rush | Can charge after rushing. |
| Fight After Melee Death | Can still fight on death in melee timing. |
| Self Resurrection | One-time return after squad/model death condition. |
| Psi Defense | Negates/mitigates incoming Psychic-tagged damage. |
| Pure Defense | Resistance against pure damage sources. |
| Teleport | Enables teleport-style movement; must end >9" from enemies. |
| Shoot Shellshock | Shooting this squad performs can force enemy shellshock tests. |
| Reanimator | Regeneration at command/start checks (heal/resurrect flow). |
| Adv Boost 1 | +1 rush boost. |
| Adv Boost 6 | +6 rush boost. |
| Stampede | On successful charge, inflicts extra pure impact damage. |
| Plus One To Charge | Adds +1 to charge result/check. |
| Satanic | Can choose temporary weapon buffs with bravery-risk backlash. |
| ReRoll Bravery | First failed shellshock/bravery-style test can be rerolled. |
| Infect | Infection-tagged debuff ability (profile-defined modifier effect). |
| Resist First Damage | First serious incoming wound can be ignored. |
| Weaken Strong Attack | If attacker strength exceeds your hardness, reduce injury pressure. |
| Reduce Damage By 1 | Incoming damage reduced by 1 (minimum 1). |
| Reduce Damage By Half | Incoming damage halved (rounded up/minimum behavior from code path). |
| Reduce Damage To 1 | Incoming damage becomes 1. |
| Move After Shooting | Gains post-shoot move behavior. |
| Move Back | Reactive move-after-enemy behavior hook. |
| Squad Reroll Hits | Squad-wide failed hit rerolls. |
| Squad Reroll Hit Ones | Squad-wide reroll hit rolls of 1. |
| Squad Reroll Injury Ones | Squad-wide reroll injury rolls of 1. |
| Squad Plus One Injuries | Squad-wide +1 injury modifier. |
| Squad Reroll Injuries | Squad-wide failed injury rerolls. |
| Stop Rerolls | Enemy rerolls against this squad are disabled. |
| No Modifiers | Negative hit/injury modifiers are ignored. |
| Martial Stance | Choose melee stance options (defensive or offensive temporary effects). |
| Sub Routine | Choose shooting subroutine (temporary Skirmish or Hefty style buff). |
| Firing Deck | Transport can fire embarked squad ranged weapons. |
| Cover Benefit | Improved cover/defensive benefit in applicable contexts. |
| Six Plus Dodge | Grants 6+ dodge style defensive roll. |
| Free Healthcare | Friendly squads within 6" receive non-stacking -1 incoming damage support aura. |

# Player Abilities

| Name | Effect |
|---|---|
| Hive Mind | Applies Hive Mind squad aura/interaction package across the player's force. |
| Alien Terror | Gives access to alien-terror pressure playstyle interactions. |
| Grim | Applies enemy bravery pressure aura (shellshock modifier interaction). |
| Warrior Bless | Enables start-of-round blessing roll package and temporary buffs. |
| Martial | Grants Martial Stances to squads. |
| Berserk | Grants Berserk package to squads at round start. |
| Grief | Grants Demonic Grief package to squads. |
| Subroutines | Grants Subroutine package to squads. |
| Officer Order | Grants officer-order style command options to squads. |
| Stranded Miracle | Enables Fate Six pool usage (replace one die with a 6, subject to replacement/reroll rules). |

# Simulator / Duel Comparison Scene

The Duel Comparison simulator is a standalone scene for repeated 1v1 squad simulations without playing a full match.

You can configure:
- Squad A and Squad B
- First attacker mode
- Range
- Trial count
- Optional deterministic seed

The simulator reports:
- Win counts and win percentage trends
- Draws
- Average rounds
- Average winner remaining HP
- Penetrating injury rate
- Average damage per trial
- Histogram slides for rounds, remaining HP, penetration rate, and damage

Use it to compare builds and estimate matchup performance quickly.

# Other Important Mechanics

- **Go to Ground** grants temporary Cover Benefit + Six Plus Dodge when targeted by shooting.
- **Fire Overwatch** arms a squad to shoot reactively on charge declaration.
- **Bravery** governs shellshock and bravery-dependent tests.
- **Hardness** determines injury thresholds versus weapon strength.
- **Cover Benefit** and terrain cover can stack into shooting-defense context checks as allowed.
- **Order windows** strictly control when an order can be activated.
- **One order per phase window** and **one command reroll per phase** are enforced.
