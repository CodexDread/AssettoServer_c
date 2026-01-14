# SXR Welcome Plugin

Displays a welcome popup when players join, showing server info, rules, and car restriction warnings.

## Features

- **Welcome popup**: Shows on player join
- **Server info**: Name, description, rules
- **Restriction warnings**: Integrates with SXRCarLockPlugin
- **Available cars**: Shows what cars player CAN drive
- **Driver level display**: Shows current level and XP
- **Dismissable**: Player must acknowledge before playing
- **Customizable**: Configure all text and timing

## Popup Layout

```
┌─────────────────────────────────────────┐
│  Shuto Expressway Revival               │
│  Tokyo's Underground Racing Scene       │
├─────────────────────────────────────────┤
│  Welcome, PlayerName!                   │
│  Driver Level: 12    XP: 1500/2000      │
├─────────────────────────────────────────┤
│  ⚠️ CAR RESTRICTION                     │
│  Your car (Nissan GTR) requires Level   │
│  30. You need 18 more levels.           │
│                                         │
│  Cars you CAN drive:                    │
│  [D] Toyota AE86        Lvl 1+          │
│  [C] BMW M3 E30         Lvl 5+          │
│  [B] Mazda RX-7         Lvl 15+         │
├─────────────────────────────────────────┤
│  Server Rules                           │
│  1. Respect all players                 │
│  2. No ramming                          │
│  ...                                    │
├─────────────────────────────────────────┤
│       [I Understand - Enter Server]     │
└─────────────────────────────────────────┘
```

## Configuration

```yaml
!SXRWelcomeConfiguration
Enabled: true

# Server Info
ServerName: "Shuto Expressway Revival"
ServerDescription: "Tokyo's Underground Racing Scene"
WelcomeMessage: "Welcome! Please read the rules."

# Rules
Rules:
  - "Respect all players"
  - "No ramming"
  - "Use headlights at night"

# Restriction Warning
ShowRestrictionWarning: true
ShowAvailableCars: true
MaxAvailableCarsToShow: 10

# Timing
ShowDelaySeconds: 2.0
MinimumDisplaySeconds: 3.0
AutoDismissSeconds: 0

# Social Links
DiscordUrl: "discord.gg/yourserver"
WebsiteUrl: "https://yourserver.com"
```

## HTTP API

| Endpoint | Description |
|----------|-------------|
| `GET /sxrwelcome/data/{steamId}` | Get welcome data for player |
| `GET /sxrwelcome/serverinfo` | Get server info only |
| `GET /sxrwelcome/rules` | Get rules list |

## Integration

Works automatically with:
- **SXRCarLockPlugin**: Gets car restriction data
- **SXRPlayerStatsPlugin**: Gets driver level and XP

## Customization

### Warning Message Template

Use placeholders in `RestrictionWarningTemplate`:
- `{car}` - Current car model
- `{class}` - Car's class letter
- `{required}` - Required driver level
- `{current}` - Player's current level
- `{needed}` - Levels needed to unlock

### Timing

- `ShowDelaySeconds`: Wait before showing popup (lets game load)
- `MinimumDisplaySeconds`: Force player to read for this long
- `AutoDismissSeconds`: Auto-close after this time (0 = never)

## Credits

Part of the SXR (Shuto Expressway Revival) Project
