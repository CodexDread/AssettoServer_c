# SXR Car Lock Plugin - Changelog

## [1.1.0] - 2026-01-13

### Prestige Support

#### New Features
- **Prestige Bypass**: Prestiged players can drive any car
- **Effective Level Check**: Uses highest level for unlock calculations
- **Prestige Provider**: New integration point for prestige rank

#### Changes
- CheckPlayer now considers prestige rank
- Added PrestigeRank and EffectiveLevel to CarLockCheckResult
- Prestiged players automatically bypass level requirements

## [1.0.0] - 2026-01-13

### Initial Release - Driver Level Vehicle Restrictions

#### Features
- **Car Class Requirements**: Define minimum driver level for each car class
- **JSON-Based Car Mappings**: External JSON file for easy editing
- **Auto-Reload**: File watcher detects changes and reloads automatically
- **Join Validation**: Check player level when they connect
- **Automatic Enforcement**: Spectate or kick players who don't meet requirements
- **Grace Period**: Optional time to let players see the welcome message before action
- **Integration**: Hooks into SXRPlayerStatsPlugin for driver level data
- **Display Names**: Human-readable car names for UI

#### JSON File Features
- Separate `car_classes.json` file for car mappings
- Support for exact, prefix, and contains matching
- Display names for better UI presentation
- Always-allowed and always-blocked lists
- Comments supported in JSON
- Hot reload without server restart

#### Car Class System
- S-Class: Level 50+ (Supercars)
- A-Class: Level 30+ (Sports Cars)  
- B-Class: Level 15+ (Tuners)
- C-Class: Level 5+ (Street)
- D-Class: Level 1+ (Starter)
- E-Class: Level 1+ (Entry/Kei)

#### Enforcement Modes
- `Spectate`: Move player to spectator (can rejoin with valid car)
- `Kick`: Kick player with message to choose different car
- `Warning`: Just warn via welcome popup (no enforcement)

#### Integration Points
- SXRPlayerStatsPlugin: Gets driver level
- SXRWelcomePlugin: Provides restriction data for popup
- HTTP API for Lua clients and external tools

#### HTTP API
- GET /sxrcarlock/requirements - Get all class requirements
- GET /sxrcarlock/check/{sessionId} - Check if player meets requirement
- GET /sxrcarlock/available/{steamId} - Get cars available for player's level
- GET /sxrcarlock/classes - Get all car class definitions
- GET /sxrcarlock/mappings - Get all car mappings
- POST /sxrcarlock/reload - Reload JSON file

---

## Planned Features
- Temporary unlock tokens
- Event mode (disable restrictions)
- Per-car level overrides in JSON
- Rental system integration
