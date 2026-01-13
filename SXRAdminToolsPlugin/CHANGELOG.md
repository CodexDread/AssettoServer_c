# Admin Tools Plugin - Changelog

## [1.1.0] - 2026-01-13

### Added - Full AssettoServer Command Parity

#### New Features
- **Teleport to Pits** (`/pit <player>`) - Send players back to pits
- **Time Control** (`/settime HH:mm`) - Set server time with presets (dawn, noon, sunset, night)
- **Weather Control** 
  - `/setweather <id>` - Set weather by config ID
  - `/setcspweather <type> [transition]` - Set CSP weather type with transition duration
  - `/cspweather` - List all CSP weather types
- **Force Headlights** (`/forcelights <on/off> <player>`) - Force player lights on/off
- **Player Restrictions** (`/restrict <player> <restrictor> <ballast>`) - Set ballast/restrictor
- **Whitelist Management**
  - `/whitelist <steamid>` - Add to whitelist
  - `/unwhitelist <steamid>` - Remove from whitelist
  - `/whitelistshow` - View whitelist

#### In-Game UI Enhancements
- **Server Tab** with time/weather controls
  - Time slider with hour/minute controls
  - Quick time presets (Dawn, Morning, Noon, Afternoon, Sunset, Night)
  - CSP weather type buttons
  - Weather transition duration control
  - Current environment display
- **Player Actions** expanded
  - Teleport to Pits button
  - Force Lights ON/OFF buttons
  - Restrictor/Ballast sliders with Apply/Clear
  - Quick Whitelist button
- **Whitelist Tab** for managing whitelisted players
- **Enhanced Player Info** showing IP address and Steam ID

#### HTTP API Endpoints
- `POST /admin/pit` - Teleport to pits
- `GET /admin/environment` - Get server time/weather
- `POST /admin/time` - Set server time
- `POST /admin/weather` - Set weather (config or CSP type)
- `GET /admin/weather/types` - List CSP weather types
- `POST /admin/forcelights` - Force headlights
- `POST /admin/restrict` - Set ballast/restrictor
- `GET /admin/restrict/{sessionId}` - Get player restriction
- `GET /admin/whitelist` - Get whitelist
- `POST /admin/whitelist` - Add to whitelist
- `DELETE /admin/whitelist/{steamId}` - Remove from whitelist
- `GET /admin/whitelist/check/{steamId}` - Check if whitelisted

---

## [1.0.0] - 2026-01-13

### Initial Release - Server Administration Framework

#### Core Features
- **Admin Authentication**: Steam ID-based admin roles (SuperAdmin, Admin, Moderator)
- **Player Monitoring**: Real-time view of all connected players with stats
- **Kick System**: Remove players with reason logging
- **Ban System**: Persistent bans with expiration support
- **Audit Logging**: All admin actions logged with timestamps
- **Extensible Framework**: Hook system for adding custom admin tools

#### Admin Levels
- **SuperAdmin**: Full access, can manage other admins
- **Admin**: Can kick, ban, and use all monitoring tools
- **Moderator**: Can kick and use monitoring tools, limited ban duration

---

## Planned Features
- Discord webhook integration
- Warning system (strikes before ban)
- Spectate player
- Mute player (chat)
- Config value editing (`/set` command)
