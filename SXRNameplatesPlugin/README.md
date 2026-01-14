# SXR Nameplates Plugin

Display floating nameplates above player cars with driver information.

## Design

```
┌─────────────────────────────┐
│ [15] PlayerName | S         │
│       CLUB   #42            │
└─────────────────────────────┘
```

- **[15]** - Driver Level badge (cyan accent)
- **PlayerName** - Color-coded by Safety Rating
- **S** - Car Class (color-coded)
- **CLUB** - Racer Club tag (dimmed)
- **#42** - Leaderboard Rank (cyan accent)

## Features

### Display Elements
| Element | Description | Color Coding |
|---------|-------------|--------------|
| Driver Level | `[DL]` badge | Cyan accent |
| Player Name | Driver name | Safety Rating |
| Car Class | S/A/B/C/D/E | Class tier |
| Club Tag | Racer club | Dimmed |
| Rank | `#N` position | Cyan accent |

### Safety Rating Colors
| Rating | Color | Description |
|--------|-------|-------------|
| S | Gold | Excellent |
| A | Green | Good |
| B | Blue | Average |
| C | Yellow | Below Average |
| D | Orange | Poor |
| F | Red | Dangerous |

### Car Class Colors
| Class | Color | Description |
|-------|-------|-------------|
| S | Purple | Supercars |
| A | Red | Sports Cars |
| B | Orange | Tuners |
| C | Yellow | Street |
| D | Green | Economy |
| E | Blue | Entry |

### Visibility
- Maximum visible distance: 500m (configurable)
- Fade start distance: 300m
- Behind-camera culling
- Distance-based opacity

## Installation

1. Copy `SXRNameplatesPlugin.dll` and `lua/` folder to your plugins directory
2. Add configuration to `extra_cfg.yml`:

```yaml
EnablePlugins:
  - SXRNameplatesPlugin

---
!NameplatesConfiguration
Enabled: true
MaxVisibleDistance: 500
FadeStartDistance: 300
HeightOffset: 2.5
```

## Toggle

Access via **Extended Chat Features** (CSP online extras menu):
- Toggle nameplates on/off
- Adjust scale
- Debug: show own nameplate

## Integration

### With PlayerStatsPlugin

```csharp
// In your startup
var nameplates = serviceProvider.GetRequiredService<SXRNameplatesPlugin>();
var playerStats = serviceProvider.GetRequiredService<PlayerStatsPlugin>();

nameplates.SetDriverLevelProvider(steamId => 
    playerStats.GetPlayerStats(steamId)?.DriverLevel ?? 1);

nameplates.SetLeaderboardRankProvider(steamId =>
    // Get rank from leaderboard
);
```

### Updating Data

```csharp
// Update a player's nameplate
nameplatesPlugin.UpdateNameplateBySteamId(steamId, data => {
    data.SafetyRating = "A";
    data.ClubTag = "SRTC";
});
```

## HTTP API

| Endpoint | Description |
|----------|-------------|
| `GET /nameplates/sync` | Get all nameplate data |
| `GET /nameplates/player/{id}` | Get specific player |
| `GET /nameplates/config` | Get display config |
| `GET /nameplates/colors/safety` | Safety rating colors |
| `GET /nameplates/colors/carclass` | Car class colors |

## Car Class Configuration

Define car class mappings in the config:

```yaml
CarClassMappings:
  "ks_ferrari": "S"
  "ks_lamborghini": "S"
  "ks_porsche_911": "A"
  "ks_nissan_gtr": "A"
  "ks_toyota_supra": "B"
  "ks_mazda_rx7": "B"
  "ks_bmw_m3": "C"

DefaultCarClass: "D"
```

Cars are matched by model prefix. First matching entry wins.

## Future Integration Points

The plugin is designed to integrate with future systems:

- **SafetyRatingPlugin**: Dynamic safety rating calculation
- **CarClassPlugin**: Performance-based car classification
- **RacerClubPlugin**: Club/team management
- **LeaderboardPlugin**: Global ranking system

## Credits

- Part of the TXR Revival Project
- Built for AssettoServer
- Requires CSP (Custom Shaders Patch)
