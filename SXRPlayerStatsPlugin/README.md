# SXR Player Stats Plugin

A comprehensive player statistics tracking system for AssettoServer with in-game UI.

## Features

### Tracked Statistics

#### Session & Time
- Total time on server
- Number of sessions
- Average session length
- Longest session

#### Distance & Speed
- Total distance driven
- High-speed distance (200+ km/h)
- Top speed achieved
- Average speed
- Longest single drive

#### Racing
- Races participated
- Wins, podiums, DNFs
- Win rate
- SP Battle wins/losses
- Best win streak

#### Collisions
- Total collisions
- Car vs wall breakdown
- Average per race
- Clean race rate

#### Cars
- Favorite car (most driven)
- Unique cars used
- Per-car statistics (distance, time, top speed, wins)

#### Driver Level
- Experience points (XP)
- Level progression
- XP rewards for driving, winning, clean races
- XP penalties for collisions

### Milestones
Automatic achievement tracking:
- Distance milestones (100km, 1000km, 10000km)
- Speed clubs (200, 300, 400 km/h)
- Win milestones (10, 50, 100 victories)
- Time milestones (10, 100, 500 hours)
- Car collector badges
- Win streak achievements
- Level milestones

## Installation

1. Copy `SXRPlayerStatsPlugin.dll` and `lua/` folder to your plugins directory
2. Add configuration to `extra_cfg.yml`:

```yaml
EnablePlugins:
  - SXRPlayerStatsPlugin

---
!PlayerStatsConfiguration
EnableDriverLevel: true
MaxDriverLevel: 100
XPPerKilometer: 10
# ... see cfg/plugin_player_stats_config.yml for all options
```

## In-Game UI

The Lua UI provides a multi-tab statistics panel:

- **Overview**: Driver level, XP progress, quick stats, favorite car
- **Racing**: Win counts, collision stats, clean race rate
- **Driving**: Distance tracking, speed records, time played
- **Records**: Milestones achieved, leaderboard rankings
- **Cars**: Per-car breakdown with distance and top speeds

Access via the online extras menu (CSP required).

## Chat Commands

| Command | Description |
|---------|-------------|
| `/stats` | Overview of your stats |
| `/level` | Driver level and XP |
| `/driving` | Distance and speed stats |
| `/racing` | Race statistics |
| `/collisions` | Crash statistics |
| `/mycar` | Favorite car info |
| `/top <category>` | Leaderboard by category |
| `/statshelp` | List all commands |

### Leaderboard Categories
`DriverLevel`, `TotalDistance`, `TotalTime`, `RaceWins`, `BattleWins`, `TopSpeed`, `AverageSpeed`, `CleanRaceRate`, `UniqueCars`

### Admin Commands
| Command | Description |
|---------|-------------|
| `/savestats` | Force save all stats |
| `/viewstats <player>` | View another player's stats |

## HTTP API

| Endpoint | Description |
|----------|-------------|
| `GET /playerstats/{steamId}` | Full player stats |
| `GET /playerstats/{steamId}/summary` | Lightweight summary |
| `GET /playerstats/{steamId}/cars` | Per-car statistics |
| `GET /playerstats/{steamId}/milestones` | Achieved milestones |
| `GET /playerstats/leaderboard/{category}` | Leaderboard |
| `GET /playerstats/leaderboard/categories` | Available categories |
| `GET /playerstats/top/{category}/{count}` | Top N players |

## XP System

### Earning XP
| Activity | XP |
|----------|-----|
| Per kilometer driven | 10 |
| Per minute active driving | 5 |
| Win race/battle | 100 |
| Complete race | 25 |
| Clean race bonus | 1.5x multiplier |

### Losing XP
| Activity | XP |
|----------|-----|
| Per collision | -2 |

### Level Progression
XP required per level uses exponential scaling:
```
XP for Level N = BaseXP × (ScalingFactor ^ (N-1))
```
Default: 1000 base XP, 1.15 scaling factor

Example progression:
| Level | Total XP Needed |
|-------|-----------------|
| 1 | 0 |
| 10 | ~20,000 |
| 25 | ~100,000 |
| 50 | ~1,000,000 |
| 100 | ~100,000,000 |

## Integration with SPBattlePlugin

SXRPlayerStatsPlugin automatically tracks battle results when used alongside SPBattlePlugin:

```csharp
// SPBattlePlugin can call:
playerStatsPlugin.RecordBattleResult(steamId, isWin);

// SXRPlayerStatsPlugin provides driver level:
int level = playerStatsPlugin.GetPlayerStats(steamId).DriverLevel;
```

## Data Storage

Stats are stored in JSON format with automatic backups. Default location:
```
cfg/plugins/SXRPlayerStatsPlugin/playerstats.json
```

Backup files are created before each save:
```
playerstats.json.backup.20260113120000
```

## Credits

- Part of the TXR Revival Project
- Built for AssettoServer
