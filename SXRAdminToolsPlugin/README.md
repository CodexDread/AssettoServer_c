# Admin Tools Plugin

A comprehensive server administration framework for AssettoServer with in-game UI, HTTP API, and extensibility.

## Features

### Admin Levels
| Level | Access |
|-------|--------|
| **SuperAdmin** | Full access, manage other admins |
| **Admin** | Kick, ban (any duration), all monitoring |
| **Moderator** | Kick, monitor, temp ban (limited duration) |

### Player Monitoring
- Real-time player list
- Position, speed, ping tracking
- Collision/incident counting
- AFK detection
- Session duration

### Kick System
- Kick with reason (logged)
- Kick by session ID or player name
- Kick all non-admins (emergency)
- Cooldown protection

### Ban System
- Permanent bans
- Temporary bans (configurable duration)
- IP bans (optional)
- Offline bans (by Steam ID)
- Auto-expire temporary bans
- Ban list search/management

### Audit Logging
- All admin actions logged
- Searchable audit trail
- File and in-memory logging
- Per-admin and per-target history

## Installation

1. Copy `AdminToolsPlugin.dll` and `lua/` folder to your plugins directory
2. Configure admin Steam IDs in `extra_cfg.yml`:

```yaml
EnablePlugins:
  - AdminToolsPlugin

---
!AdminToolsConfiguration
SuperAdmins:
  - "76561198000000001"
Admins:
  - "76561198000000002"
Moderators:
  - "76561198000000003"
HttpApiKey: "your-secure-api-key"
```

## In-Game Admin Panel

Press **F10** (configurable) to open the admin panel when connected as an admin.

### Tabs
- **Players**: Online player list with quick actions
  - Teleport to Pits
  - Force Lights ON/OFF
  - Kick with reason
  - Temp Ban / Perma Ban
  - Set Restrictor/Ballast
  - Add to Whitelist
- **Server**: Time and weather controls
  - Time slider (Hour/Minute)
  - Quick presets: Dawn, Morning, Noon, Afternoon, Sunset, Night
  - Weather config ID selector
  - CSP Weather type buttons (Clear, Rain, Snow, etc.)
  - Transition duration control
  - Current environment display
- **Bans**: Active ban management
- **Whitelist**: Add/remove whitelisted players
- **Audit**: Recent admin actions

## Chat Commands

All commands match AssettoServer's default admin commands plus additional features.

### All Admins (Moderator+)
| Command | Description |
|---------|-------------|
| `/players` | List online players |
| `/whois <n>` | Player details (IP, Steam profile) |
| `/kick <n> [reason]` | Kick a player |
| `/kick_id <id> [reason]` | Kick by session ID |
| `/pit <n>` | Teleport player to pits |
| `/forcelights <on/off> <n>` | Force headlights on/off |
| `/tempban <n> <hours> [reason]` | Temporary ban |
| `/bans [search]` | List active bans |
| `/adminhelp` | Show admin commands |

### Admin+
| Command | Description |
|---------|-------------|
| `/ban <n> [reason]` | Permanent ban |
| `/ban_id <id> [reason]` | Ban by session ID |
| `/unban <id>` | Remove a ban |
| `/settime <HH:mm>` | Set server time |
| `/setweather <id>` | Set weather config |
| `/setcspweather <type> [sec]` | Set CSP weather type |
| `/cspweather` | List CSP weather types |
| `/restrict <n> <restr> <ballast>` | Set ballast/restrictor |
| `/whitelist <steamid>` | Add to whitelist |
| `/unwhitelist <steamid>` | Remove from whitelist |
| `/whitelistshow` | View whitelist |
| `/audit [count]` | View audit log |

## HTTP API

All endpoints require `X-API-Key` header if `RequireHttpAuth` is enabled.

### Players
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/players` | GET | List online players |
| `/admin/players/{id}` | GET | Get player by session ID |
| `/admin/players/search?name=` | GET | Search by name |
| `/admin/kick` | POST | Kick player |
| `/admin/kickall?adminSteamId=&reason=` | POST | Kick all non-admins |
| `/admin/pit` | POST | Teleport player to pits |

### Bans
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/bans` | GET | List bans |
| `/admin/bans/{id}` | GET | Get ban by ID |
| `/admin/ban` | POST | Ban player |
| `/admin/bans/{id}?adminSteamId=` | DELETE | Unban |
| `/admin/bans/check/{steamId}` | GET | Check if banned |
| `/admin/bans/stats` | GET | Ban statistics |

### Server Environment
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/environment` | GET | Get current time/weather |
| `/admin/time` | POST | Set server time |
| `/admin/weather` | POST | Set weather (config or CSP) |
| `/admin/weather/types` | GET | List CSP weather types |

### Player Control
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/forcelights` | POST | Force headlights on/off |
| `/admin/restrict` | POST | Set ballast/restrictor |
| `/admin/restrict/{sessionId}` | GET | Get player restriction |

### Whitelist
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/whitelist` | GET | Get whitelist |
| `/admin/whitelist?steamId=&adminSteamId=` | POST | Add to whitelist |
| `/admin/whitelist/{steamId}?adminSteamId=` | DELETE | Remove from whitelist |
| `/admin/whitelist/check/{steamId}` | GET | Check if whitelisted |

### Audit
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/audit` | GET | Recent entries |
| `/admin/audit/search?q=` | GET | Search audit |
| `/admin/audit/admin/{steamId}` | GET | By admin |
| `/admin/audit/target/{steamId}` | GET | For target |

### Status
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/admin/status` | GET | Server admin status |

### Example: Kick via API
```bash
curl -X POST http://server:8081/admin/kick \
  -H "X-API-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{"TargetSessionId": 5, "Reason": "Rule violation", "AdminSteamId": "76561198..."}'
```

### Example: Ban via API
```bash
curl -X POST http://server:8081/admin/ban \
  -H "X-API-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{"TargetSteamId": "76561198...", "Reason": "Cheating", "DurationHours": 0, "AdminSteamId": "76561198..."}'
```

## Extending with Custom Tools

The plugin provides an extensibility framework for adding custom admin tools.

### Creating a Custom Tool

```csharp
public class MyCustomTool : IAdminTool
{
    public string ToolId => "my_tool";
    public string DisplayName => "My Custom Tool";
    public AdminLevel RequiredLevel => AdminLevel.Admin;
    
    public Task InitializeAsync(AdminToolsPlugin plugin)
    {
        // Setup code
        return Task.CompletedTask;
    }
    
    public Task<AdminActionResult> ExecuteAsync(AdminContext context, Dictionary<string, object> parameters)
    {
        // Tool logic
        return Task.FromResult(AdminActionResult.Ok("Done"));
    }
}
```

### Registering a Tool

```csharp
adminToolsPlugin.RegisterTool(new MyCustomTool());
```

### Using Events

```csharp
adminToolsPlugin.OnPlayerKicked += (sender, playerInfo) => 
{
    // Handle kick event
};

adminToolsPlugin.OnPlayerBanned += (sender, banRecord) =>
{
    // Handle ban event (e.g., Discord webhook)
};
```

## Data Storage

### Bans
Stored in JSON format at configured path (default: `cfg/plugins/AdminToolsPlugin/bans.json`)

### Audit Log
Appended to text file at configured path (default: `cfg/plugins/AdminToolsPlugin/audit.log`)

Format: `[timestamp] AdminName (AdminSteamId): Action -> TargetName (TargetSteamId) - Details`

## Security Considerations

1. **Use strong API keys** - Generate random 32+ character strings
2. **Limit SuperAdmins** - Only trusted individuals should have full access
3. **Enable audit logging** - Track all admin actions
4. **Secure the API** - Use HTTPS if exposing API externally
5. **Review bans regularly** - Check for abuse

## Credits

- Part of the TXR Revival Project
- Built for AssettoServer
