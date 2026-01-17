# SXR Admin Tools Plugin - Changelog

## [2.0.0] - 2026-01-16

### Complete Refactoring - RAG Compliance

This version is a complete refactoring based on the SXR RAG knowledge base documentation.

#### 🔴 CRITICAL LUA FIXES (UI Would Not Work)
- **Created Lua Admin Panel** - Previous versions had empty/missing Lua file
- **Uses `ui.registerOnlineExtra()` correctly** - All 8 parameters with proper flags (`ui.OnlineExtraFlags.Admin + ui.OnlineExtraFlags.Tool`)
- **Uses `ui.childWindow()` for scrollable content** - Proper pattern for dynamic lists
- **All loops use `ui.pushID(i)` / `ui.popID()`** - Required for ImGui elements in loops
- **Proper error handling on ALL web callbacks** - Checks err, response, and status
- **Uses `ac.getServerHTTPPort()`** - Correct function for HTTP port

#### 🟡 C# Server-Side Fixes
- **Added all missing chat commands** documented in README:
  - `/pit <player>` - Teleport to pits
  - `/forcelights <on/off> <player>` - Force headlights
  - `/settime <HH:mm>` - Set server time
  - `/setweather <id>` - Set weather by config ID
  - `/setcspweather <type> [transition]` - Set CSP weather type
  - `/cspweather` - List CSP weather types
  - `/restrict <player> <restrictor> <ballast>` - Set restrictions
  - `/whitelist <steamid>` - Add to whitelist
  - `/unwhitelist <steamid>` - Remove from whitelist
  - `/whitelistshow` - View whitelist

#### 📝 Code Quality Improvements
- Added structured logging throughout Lua code
- Added error state display in UI footer
- Proper state initialization patterns
- Better separation of concerns in Lua code
- Auto-refresh for player list

#### ⚠️ NOTES FOR TESTING
- The `TeleportToPits`, `ForceLights`, and `SetRestriction` methods in the main plugin need verification against actual AssettoServer API
- HTTP API endpoints must exist and match the Lua client calls

---

## [1.2.0] - 2026-01-15

### Compliance Review & Bug Fixes

#### 🔴 CRITICAL BUGS FIXED (Would Not Compile)
- **Doubled SXR Prefix**: All namespaces and classes had `SXRSXR` instead of `SXR`
  - Fixed namespace: `SXRSXRAdminToolsPlugin` → `SXRAdminToolsPlugin`
  - Fixed all class names to match (Plugin, Module, Controller, Services)
- **Constructor Mismatches**: Constructors didn't match class names
- **DI Type Registration**: Module tried to register non-existent type names
- **Command Module Not Registered**: Added missing `SXRAdminCommandModule` registration

#### 🟡 Non-Compliance Fixes
- **Config File Naming**: `plugin_sxr_admin_tools_config.yml` → `plugin_sxr_admin_tools_cfg.yml`
- **csproj Missing cfg Copy**: Added cfg folder content to build output
- **Config Paths**: Fixed folder name `AdminToolsPlugin` → `SXRAdminToolsPlugin`

### New Features

#### Feature Availability System
- Added `ServerCapabilities` model to track available plugin integrations
- New `/admin/capabilities` API endpoint
- Lua UI checks capabilities before showing management sections
- Shows "Coming Soon" for unimplemented features

#### New Admin Tabs
- **Stats Tab**: Player statistics management (when SXRPlayerStatsPlugin loaded)
- **Time Trials Tab**: Placeholder for planned time trials management
  - Shows planned features while system is in development
  - Route management preview
  - Leaderboard management preview
  - Event management preview

#### Plugin Integration Detection
Runtime checks for loaded SXR plugins:
- SXRPlayerStatsPlugin
- SXRNameplatesPlugin
- SXRSPBattlePlugin
- SXRCarLockPlugin (car unlock/restrictions based on driver level)

Planned systems (not yet implemented):
- SXRConvoyPlugin (team convoys with team battles integration)
  - **Design Direction:** Convoy vs Convoy battles
  - Teams form convoys and challenge other convoys
  - Integrates with SXRSPBattlePlugin for team SP mechanics
- SXRClubsPlugin (club/team management)
- SXRTimeTrialsPlugin (time trials system)
- SXRRankingsPlugin (global rankings)
- SXRTournamentPlugin (tournament management)
- SXRAchievementsPlugin (achievements)

---

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

### Time Trials Management (In Development)
- Route creation and management
- Checkpoint configuration
- Leaderboard management
- Event scheduling
- Time validation and anti-cheat

### Other Planned Features
- Discord webhook integration
- Warning system (strikes before ban)
- Spectate player
- Mute player (chat)
- Config value editing (`/set` command)
- SXRPlayerStats integration (reset XP, grant levels)
- SXRNameplates integration (manage nameplate styles)
- Tournament bracket management
