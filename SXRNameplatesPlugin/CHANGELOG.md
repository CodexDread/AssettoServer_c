# SXR Nameplates Plugin - Changelog

## [1.1.0] - 2026-01-13

### Prestige Display System

#### New Features
- **Prestige Level Format**: Shows [P# - DL] for prestiged players
- **Prestige Colors**: Level badge color changes based on prestige rank
  - P0: White (no prestige)
  - P1: Gold
  - P2: Coral Red
  - P3: Purple
  - P4: Blue
  - P5: Emerald
  - P6-9: Various vibrant colors
  - P10: Deep Pink
  - P11-19: Magenta
  - P20-49: Aqua (Legend tier)
  - P50+: **Animated Rainbow Gradient** (Mythic tier)
- **Prestige Provider**: Integration point for prestige rank data
- **Badge Glow**: High prestige (P10+) adds subtle color tint to badge
- **Rainbow Animation**: P50+ players get smoothly cycling rainbow colors

#### Changes
- Level display now shows prestige: [P3 - 250] instead of [250]
- Driver level color is now based on prestige rank
- Added PrestigeRank field to SXRNameplateData
- Added LevelDisplay computed property
- Non-prestige players now show white level text

## [1.0.0] - 2026-01-13

### Initial Release - Player Nameplates System

#### Features
- **3D Nameplates**: Floating nameplates above player cars
- **Driver Level Badge**: Shows [DL] prefix
- **Player Name**: Color-coded by Safety Rating (placeholder)
- **Car Class**: Shows car model with class color coding (placeholder)
- **Racer Club**: Shows club tag (placeholder, uses "---" default)
- **Leaderboard Rank**: Shows global rank (placeholder)
- **Distance-based Visibility**: Nameplates fade/hide at distance
- **Toggle from Extended Chat**: Enable/disable via CSP extras menu

#### Display Format
```
[DL] PlayerName | CarClass
  ClubTag  #Rank
```

#### Visibility Settings
- Max visible distance: 500m (configurable)
- Fade start distance: 300m
- Behind camera culling
- Occlusion-aware (optional)

#### Color Coding (Placeholder Values)
- **Safety Rating Colors**:
  - S: Gold
  - A: Green  
  - B: Blue
  - C: Yellow
  - D: Orange
  - F: Red
- **Car Class Colors**:
  - S: Purple
  - A: Red
  - B: Orange
  - C: Yellow
  - D: Green
  - E: Blue

#### Integration Points
- PlayerStatsPlugin: Driver Level, Leaderboard Rank
- Future: SafetyRatingPlugin for SR colors
- Future: CarClassPlugin for class colors
- Future: RacerClubPlugin for club tags

---

## Planned Features
- Nameplate customization (size, opacity)
- Show/hide specific elements
- Team/crew colors
- Battle opponent highlight
- Wanted level indicator
