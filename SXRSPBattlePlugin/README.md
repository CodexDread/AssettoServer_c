# SP Battle Plugin

A Tokyo Xtreme Racer (TXR) inspired Spirit Point battle system for AssettoServer.

## Features

### Core Mechanics
- **SP (Spirit Points) System**: Each player has an SP bar that drains when behind
- **Distance-Based Drain**: The further behind you fall, the faster your SP drains
- **Collision Penalties**: Crashes cost you SP
- **Overtake Bonuses**: Pass your opponent to gain SP and reset the timer
- **Driver Level Bonus**: Higher DL = bigger SP pool (future integration)

### How to Battle
1. **Flash headlights 3 times** near an opponent to challenge
2. Opponent accepts with **hazard lights** or `/accept` command
3. Line up side-by-side
4. Countdown: 3... 2... 1... GO!
5. **Stay ahead or lose SP** - first to 0 loses

### Drain Zones (Default Config)
| Distance | Drain Rate | Status |
|----------|------------|--------|
| 0-10m | 0 SP/s | Drafting |
| 10-25m | 2 SP/s | Close |
| 25-50m | 5 SP/s | Moderate |
| 50-100m | 10 SP/s | Falling |
| 100-200m | 20 SP/s | Behind |
| 200m+ | 30 SP/s | Critical |

## Installation

1. Copy `SPBattlePlugin.dll` and `lua/` folder to your plugins directory
2. Add configuration to `extra_cfg.yml`:

```yaml
EnablePlugins:
  - SPBattlePlugin

---
!SPBattleConfiguration
TotalSP: 100
DriverLevelBonusSPPerLevel: 5
# ... see cfg/plugin_sp_battle_config.yml for all options
```

## Commands

| Command | Description |
|---------|-------------|
| `/battle <player>` | Challenge a specific player |
| `/accept` | Accept a pending challenge |
| `/mystats` | Show your battle statistics |
| `/sptop` | Show top 5 players |
| `/spinfo` | Show SP Battle info and your stats |

### Admin Commands
| Command | Description |
|---------|-------------|
| `/setdl <player> <level>` | Set player's Driver Level |

## HTTP API

| Endpoint | Description |
|----------|-------------|
| `GET /spbattle/leaderboard` | Top 10 players |
| `GET /spbattle/leaderboard/{steamId}` | Player ranking |
| `GET /spbattle/stats/{steamId}` | Detailed player stats |

## Configuration Reference

```yaml
# SP Pool
TotalSP: 100                          # Base SP for all players
DriverLevelBonusSPPerLevel: 5         # Extra SP per DL
MaxDriverLevel: 50                    # Cap for DL bonus

# Distance Drain
FollowDistanceThresholds: [10, 25, 50, 100, 200]
DrainRatesPerSecond: [0, 2, 5, 10, 20, 30]

# Penalties & Bonuses
CollisionSPPenalty: 10                # SP lost on opponent collision
WallCollisionSPPenalty: 5             # SP lost on wall collision
OvertakeSPBonus: 5                    # SP gained on overtake
LeadBonusPerSecond: 0.5               # SP/sec for leading

# Battle Rules
MinBattleSpeedKph: 30                 # Minimum speed to continue
MaxBattleDurationSeconds: 300         # Max battle length (0=unlimited)
BattleSeparationDistance: 500         # Auto-end distance
NoOvertakeTimeoutSeconds: 60          # Timeout without position change

# Challenge Settings
ChallengeTimeoutSeconds: 10
ChallengeMaxDistance: 30
LineUpDistance: 10
CountdownSeconds: 3
ChallengeCooldownSeconds: 20

# Leaderboard
EnableLeaderboard: true
LeaderboardPath: "cfg/plugins/SPBattlePlugin/leaderboard.json"
EnableLuaUI: true
BroadcastResults: true
WinRatingPoints: 25
LossRatingPoints: 20
StartingRating: 1000
```

## Driver Level Integration

The plugin exposes methods for external DL system integration:

```csharp
// Set player's driver level
spBattlePlugin.SetDriverLevel(entryCar, level);

// Get player's driver level
int level = spBattlePlugin.GetDriverLevel(entryCar);
```

Max SP is calculated as:
```
MaxSP = TotalSP + (DriverLevel × DriverLevelBonusSPPerLevel)
```

Example with DL 20:
```
MaxSP = 100 + (20 × 5) = 200 SP
```

## Lua UI

The plugin includes a CSP Lua UI that displays:
- Dual SP bars with drain visualization
- Battle timer
- Distance indicator
- Countdown display
- Win/loss announcements
- Leaderboard panel (accessible from online extras menu)

## Credits

- Inspired by Tokyo Xtreme Racer / Shutokou Battle series
- Built for AssettoServer
- Part of the TXR Revival Project
