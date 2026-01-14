# SXR Car Lock Plugin

Restricts vehicles based on driver level, ensuring players progress through car classes as they gain experience.

## Features

- **Level-based restrictions**: Each car class requires a minimum driver level
- **JSON-based car mappings**: Easy to edit car classes in a separate file
- **Auto-reload**: Changes to JSON file apply without server restart
- **Automatic enforcement**: Spectate, kick, or just warn players
- **Grace period**: Gives players time to see the welcome popup
- **Admin bypass**: Admins can drive any car
- **Integration**: Works with SXRPlayerStatsPlugin and SXRWelcomePlugin

## Car Classes

| Class | Description | Default Level |
|-------|-------------|---------------|
| S | Supercars | 50+ |
| A | Sports Cars | 30+ |
| B | Tuners | 15+ |
| C | Street Cars | 5+ |
| D | Starter Cars | 1+ |
| E | Entry/Kei | 1+ |

## Configuration

### Plugin Config (extra_cfg.yml)

```yaml
!SXRCarLockConfiguration
Enabled: true

# Enforcement: Spectate, Kick, or Warning
Mode: Spectate
GracePeriodSeconds: 10

# Level requirements
SClassMinLevel: 50
AClassMinLevel: 30
BClassMinLevel: 15
CClassMinLevel: 5
DClassMinLevel: 1
EClassMinLevel: 1

# JSON file for car mappings
CarClassesJsonFile: "cfg/car_classes.json"
AutoReloadJson: true
```

### Car Classes JSON (car_classes.json)

```json
{
  "version": "1.0",
  "description": "Car class mappings",
  "cars": [
    {
      "model": "ks_ferrari_488",
      "class": "S",
      "displayName": "Ferrari 488 GTB"
    },
    {
      "model": "ks_toyota_supra",
      "class": "B",
      "displayName": "Toyota Supra MK4",
      "matchMode": "prefix"
    }
  ],
  "alwaysAllowed": [],
  "alwaysBlocked": []
}
```

### JSON Car Entry Fields

| Field | Required | Description |
|-------|----------|-------------|
| `model` | Yes | Car model name or pattern |
| `class` | Yes | Class letter (S, A, B, C, D, E) |
| `displayName` | No | Human-readable name for UI |
| `matchMode` | No | How to match: `exact`, `prefix`, `contains` |
| `notes` | No | Optional notes (ignored by plugin) |

### Match Modes

- **exact**: Model must match exactly
- **prefix**: Model must start with the pattern (default)
- **contains**: Model must contain the pattern

## Enforcement Modes

- **Spectate**: Moves player to spectator after grace period
- **Kick**: Kicks player with message to choose different car
- **Warning**: Only shows warning in welcome popup, no action taken

## HTTP API

| Endpoint | Description |
|----------|-------------|
| `GET /sxrcarlock/requirements` | Get all class definitions |
| `GET /sxrcarlock/levels` | Get level requirements |
| `GET /sxrcarlock/classes` | Get class info with colors |
| `GET /sxrcarlock/available/{steamId}` | Get available cars for player |
| `GET /sxrcarlock/available/level/{level}` | Get available cars for level |
| `GET /sxrcarlock/carclass/{model}` | Get class for a car model |
| `GET /sxrcarlock/mappings` | Get all car mappings |
| `POST /sxrcarlock/reload` | Reload JSON file |

## Hot Reload

With `AutoReloadJson: true`, changes to the JSON file are automatically detected and applied. You can also manually reload via the API:

```bash
curl -X POST http://localhost:8080/sxrcarlock/reload
```

## Integration

### With SXRPlayerStatsPlugin

```csharp
var carLock = serviceProvider.GetRequiredService<SXRCarLockPlugin>();
var playerStats = serviceProvider.GetRequiredService<SXRPlayerStatsPlugin>();

carLock.SetDriverLevelProvider(steamId => 
    playerStats.GetDriverLevel(steamId));
```

### With SXRWelcomePlugin

The car lock plugin automatically notifies the welcome plugin when a restriction is detected, allowing the welcome popup to display the warning.

## How It Works

1. Player connects to server
2. Plugin checks their driver level vs car's required level
3. If not met:
   - Restriction data sent to SXRWelcomePlugin
   - Grace period timer starts
   - After grace period, enforcement action taken
4. If met: Player drives normally

## Credits

Part of the SXR (Shuto Expressway Revival) Project
