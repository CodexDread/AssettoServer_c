# SXR SP Battle Plugin - Changelog

## [1.0.0] - 2026-01-13

### Initial Release - TXR-Style SP Battle System

#### Core Features
- **SP (Spirit Points) Battle System**: Tokyo Xtreme Racer inspired racing battles
- **Distance-Based SP Drain**: Configurable drain rates based on follow distance
- **Collision Penalties**: SP loss on vehicle collisions
- **Driver Level (DL) Bonus**: Extra SP pool based on driver level (placeholder for future integration)
- **Light Flash Challenge**: Flash headlights 3x to challenge nearby opponents
- **Leaderboard System**: Persistent win/loss tracking with HTTP API

#### Configuration Options
- `TotalSP`: Base SP pool (default: 100)
- `DriverLevelBonusSPPerLevel`: Extra SP per driver level
- `FollowDistanceThresholds`: Distance brackets for drain rates
- `DrainRatesPerSecond`: SP drain rate for each distance bracket
- `CollisionSPPenalty`: SP loss on collision
- `OvertakeSPBonus`: SP gained when overtaking
- `MaxBattleDuration`: Maximum battle length in seconds
- `ChallengeTimeout`: Time to accept challenge
- `LineUpDistance`: Required proximity to start

#### Lua UI Features
- Real-time SP bars for both racers
- Distance-based drain rate indicator
- Countdown timer display
- Win/loss announcements
- Leaderboard panel (accessible from online extras menu)

#### HTTP Endpoints
- `GET /spbattle/leaderboard` - Top 10 players
- `GET /spbattle/leaderboard/{steamId}` - Individual ranking
- `GET /spbattle/stats/{steamId}` - Detailed player stats

---

## Planned Features
- Driver Level integration with progression system
- Crew/Team battles
- Territory control mode
- Rival system with persistent matchups
