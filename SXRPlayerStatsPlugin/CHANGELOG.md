# SXR Player Stats Plugin - Changelog

## [1.1.0] - 2026-01-13

### Prestige System

#### New Features
- **Prestige System**: Level cap of 999, prestige on reaching max
- **Unlimited Prestige**: No limit on prestige ranks (P1, P2, P3, ...)
- **Prestige Display**: Format [P# - DL] for prestiged players
- **Car Access Retention**: Prestiged players keep access to all cars
- **Effective Level**: New property for car unlock calculations
- **New Milestones**: Level 500, Level 999, Prestige 1/5/10

#### Changes
- MaxDriverLevel increased from 100 to 999
- Added PrestigeRank, HighestLevelAchieved, TimesReachedMaxLevel fields
- Added EffectiveLevelForUnlocks property
- Leaderboard now sorts prestiged players higher
- New prestige milestone achievements

#### API Additions
- GetEffectiveLevelForUnlocks(steamId)
- TryPrestige(steamId)
- GetDriverLevelDisplay(steamId)

## [1.0.0] - 2026-01-13

### Initial Release - Comprehensive Player Statistics Tracking

#### Tracked Statistics
- **Racing Stats**: Races participated, wins, podiums, DNFs
- **Driving Stats**: Total distance driven, average speed, top speed achieved
- **Collision Stats**: Total collisions, average per race, wall vs car collisions
- **Session Stats**: Total time on server, number of sessions, average session length
- **Car Stats**: Favorite car (most driven), cars used count, distance per car
- **Driver Level**: Current DL, experience points, level progress
- **Battle Stats**: SP Battles won/lost (integrates with SPBattlePlugin)
- **Achievement Stats**: Personal bests, milestones reached

#### Features
- Persistent JSON storage with automatic backups
- In-game Lua UI panel (accessible from online extras)
- Real-time stat updates during gameplay
- HTTP API for external tools/websites
- Chat commands for quick stat checks
- Leaderboard rankings by various categories

#### Lua UI Features
- Multi-tab stats panel (Overview, Racing, Driving, Records)
- Progress bars for level/achievements
- Car usage breakdown
- Personal records display
- Compare with server averages

#### HTTP Endpoints
- `GET /playerstats/{steamId}` - Full player stats
- `GET /playerstats/leaderboard/{category}` - Leaderboard by category
- `GET /playerstats/top/{category}/{count}` - Top N players

---

## Planned Features
- Weekly/monthly stat breakdowns
- Rival comparison system
- Achievement badges
- Stat export to Discord
