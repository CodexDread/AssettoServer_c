# SXR Welcome Plugin - Changelog

## [1.1.0] - 2026-01-13

### Prestige Display

#### New Features
- **Prestige Display**: Shows [P# - Level] format for prestiged players
- **Prestige Colors**: Level color changes based on prestige rank
- **Prestige Provider**: Integration point for prestige rank data

#### Changes
- Added PrestigeRank field to WelcomeData
- Added DriverLevelDisplay computed property
- Lua UI shows prestige-colored level display
- Note: Prestiged players bypass car restrictions

## [1.0.0] - 2026-01-13

### Initial Release - Welcome Popup System

#### Features
- **Welcome Popup**: Displays when player joins server
- **Server Info**: Shows server name, rules, and custom messages
- **Car Restriction Warning**: Integrates with SXRCarLockPlugin
- **Available Cars List**: Shows cars player CAN drive based on level
- **Dismissable**: Player can close popup when ready
- **Auto-dismiss Option**: Automatically close after set time
- **Remember Preference**: Option to not show again (per session)

#### Display Sections
1. **Header**: Server name and welcome message
2. **Rules**: Server rules (configurable)
3. **Warning** (if applicable): Car restriction warning with countdown
4. **Available Cars**: List of cars player can use
5. **Driver Stats**: Shows current driver level and progress

#### Integration
- SXRCarLockPlugin: Gets restriction data
- SXRPlayerStatsPlugin: Gets driver level info
- HTTP API for custom welcome data

#### Customization
- Server name and description
- Custom rules list
- Welcome message
- Warning message templates
- Colors and styling
- Auto-dismiss timing

---

## Planned Features
- Daily login rewards display
- Event announcements
- MOTD (Message of the Day)
- Social links
- Discord integration
