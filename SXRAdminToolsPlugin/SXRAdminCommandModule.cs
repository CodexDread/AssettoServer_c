using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace SXRAdminToolsPlugin;

/// <summary>
/// Admin chat commands
/// </summary>
[RequireConnectedPlayer]
public class SXRAdminCommandModule : ACModuleBase
{
    private readonly SXRAdminToolsPlugin _plugin;
    private readonly SXRBanService _banService;
    private readonly SXRAuditService _auditService;
    
    public SXRAdminCommandModule(
        SXRAdminToolsPlugin plugin,
        SXRBanService banService,
        SXRAuditService auditService)
    {
        _plugin = plugin;
        _banService = banService;
        _auditService = auditService;
    }
    
    private string MySteamId => Client!.Guid.ToString();
    private AdminLevel MyLevel => _plugin.GetAdminLevel(MySteamId);
    
    // === INFORMATION ===
    
    /// <summary>
    /// Show your admin level
    /// </summary>
    [Command("adminlevel", "mylevel")]
    public void ShowMyLevel()
    {
        var level = MyLevel;
        if (level == AdminLevel.None)
        {
            Reply("You are not an admin.");
        }
        else
        {
            Reply($"Your admin level: {level}");
        }
    }
    
    /// <summary>
    /// List online players
    /// </summary>
    [Command("players", "who", "online")]
    public void ListPlayers()
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        var players = _plugin.GetConnectedPlayers();
        
        if (players.Count == 0)
        {
            Reply("No players online.");
            return;
        }
        
        string message = $"[Online Players: {players.Count}]\n";
        foreach (var p in players.Take(15))
        {
            string adminTag = p.AdminLevel != AdminLevel.None ? $" [{p.AdminLevel}]" : "";
            string afkTag = p.IsAfk ? " [AFK]" : "";
            message += $"#{p.SessionId} {p.Name}{adminTag}{afkTag} - {p.CarModel} ({p.Ping}ms)\n";
        }
        
        if (players.Count > 15)
        {
            message += $"...and {players.Count - 15} more";
        }
        
        Reply(message.TrimEnd());
    }
    
    /// <summary>
    /// Get info about a player
    /// </summary>
    [Command("playerinfo", "info")]
    public void PlayerInfo(ACTcpClient target)
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        var info = _plugin.GetPlayerInfo(target.SessionId);
        if (info == null)
        {
            Reply("Player not found.");
            return;
        }
        
        Reply($"[Player Info: {info.Name}]\n" +
              $"Steam ID: {info.SteamId}\n" +
              $"Session: #{info.SessionId}\n" +
              $"Car: {info.CarModel}\n" +
              $"Connected: {info.GetSessionDurationText()}\n" +
              $"Ping: {info.Ping}ms\n" +
              $"Speed: {info.SpeedKph:F0} km/h\n" +
              $"Collisions: {info.Collisions}\n" +
              $"Admin Level: {info.AdminLevel}");
    }
    
    // === KICK ===
    
    /// <summary>
    /// Kick a player
    /// </summary>
    [Command("kick", "k")]
    public void Kick(ACTcpClient target, [Remainder] string reason = "")
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        var result = _plugin.KickPlayer(new KickRequest
        {
            TargetSessionId = target.SessionId,
            Reason = reason,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Kick by session ID
    /// </summary>
    [Command("kickid")]
    public void KickById(int sessionId, [Remainder] string reason = "")
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        var result = _plugin.KickPlayer(new KickRequest
        {
            TargetSessionId = sessionId,
            Reason = reason,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    // === BAN ===
    
    /// <summary>
    /// Ban a player permanently
    /// </summary>
    [Command("ban")]
    public void Ban(ACTcpClient target, [Remainder] string reason = "")
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Moderator))
        {
            Reply("You don't have permission.");
            return;
        }
        
        var result = _plugin.BanPlayer(new BanRequest
        {
            TargetSteamId = target.Guid.ToString(),
            TargetName = target.Name,
            Reason = reason,
            DurationHours = 0, // Permanent
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Temporarily ban a player
    /// </summary>
    [Command("tempban", "tban")]
    public void TempBan(ACTcpClient target, int hours, [Remainder] string reason = "")
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Moderator))
        {
            Reply("You don't have permission.");
            return;
        }
        
        var result = _plugin.BanPlayer(new BanRequest
        {
            TargetSteamId = target.Guid.ToString(),
            TargetName = target.Name,
            Reason = reason,
            DurationHours = hours,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Ban by Steam ID (offline ban)
    /// </summary>
    [Command("banid")]
    public void BanBySteamId(string steamId, int hours = 0, [Remainder] string reason = "")
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.BanPlayer(new BanRequest
        {
            TargetSteamId = steamId,
            Reason = reason,
            DurationHours = hours,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Unban a player
    /// </summary>
    [Command("unban")]
    public void Unban(string steamIdOrBanId)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.UnbanPlayer(steamIdOrBanId, MySteamId);
        Reply(result.Message);
    }
    
    /// <summary>
    /// List active bans
    /// </summary>
    [Command("bans", "banlist")]
    public void ListBans([Remainder] string search = "")
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Moderator))
        {
            Reply("You don't have permission.");
            return;
        }
        
        var bans = _banService.GetBans(true, string.IsNullOrEmpty(search) ? null : search);
        
        if (bans.Count == 0)
        {
            Reply("No active bans found.");
            return;
        }
        
        string message = $"[Active Bans: {bans.Count}]\n";
        foreach (var ban in bans.Take(10))
        {
            message += $"{ban.Id}: {ban.PlayerName} - {ban.GetExpiryText()} ({ban.Reason.Substring(0, Math.Min(20, ban.Reason.Length))}...)\n";
        }
        
        if (bans.Count > 10)
        {
            message += $"...and {bans.Count - 10} more";
        }
        
        Reply(message.TrimEnd());
    }
    
    // === AUDIT ===
    
    /// <summary>
    /// View recent admin actions
    /// </summary>
    [Command("audit", "log")]
    public void ViewAudit(int count = 5)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var entries = _auditService.GetRecentEntries(Math.Min(count, 10));
        
        if (entries.Count == 0)
        {
            Reply("No audit entries.");
            return;
        }
        
        string message = "[Recent Admin Actions]\n";
        foreach (var entry in entries)
        {
            message += $"{entry.Timestamp:HH:mm} {entry.AdminName}: {entry.Action}";
            if (!string.IsNullOrEmpty(entry.TargetName))
                message += $" -> {entry.TargetName}";
            message += "\n";
        }
        
        Reply(message.TrimEnd());
    }
    
    
    // === HELP ===
    
    /// <summary>
    /// Show admin commands help
    /// </summary>
    [Command("adminhelp", "ahelp")]
    public void AdminHelp()
    {
        var level = MyLevel;
        
        if (level == AdminLevel.None)
        {
            Reply("You are not an admin.");
            return;
        }
        
        string message = $"[Admin Commands - Level: {level}]\n";
        message += "/players - List online players\n";
        message += "/playerinfo <n> - Player details\n";
        message += "/kick <n> [reason] - Kick player\n";
        message += "/kickid <id> [reason] - Kick by session ID\n";
        message += "/pit <n> - Teleport to pits\n";
        message += "/forcelights <on/off> <n> - Force headlights\n";
        
        if (level >= AdminLevel.Moderator)
        {
            message += "/tempban <n> <hours> [reason] - Temp ban\n";
            message += "/bans [search] - List bans\n";
        }
        
        if (level >= AdminLevel.Admin)
        {
            message += "/ban <n> [reason] - Permanent ban\n";
            message += "/banid <steamid> [hours] [reason] - Offline ban\n";
            message += "/unban <id> - Remove ban\n";
            message += "/audit [count] - View audit log\n";
            message += "/settime <HH:mm> - Set server time\n";
            message += "/setweather <id> - Set weather config\n";
            message += "/setcspweather <type> [sec] - Set CSP weather\n";
            message += "/cspweather - List CSP weather types\n";
            message += "/restrict <n> <restrictor> <ballast> - Set restrictions\n";
            message += "/whitelist <steamid> - Add to whitelist\n";
            message += "/unwhitelist <steamid> - Remove from whitelist\n";
            message += "/whitelistshow - View whitelist\n";
        }
        
        Reply(message.TrimEnd());
    }
    
    // === TELEPORT ===
    
    /// <summary>
    /// Teleport a player to pits
    /// </summary>
    [Command("pit")]
    public void TeleportToPits(ACTcpClient target)
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        var result = _plugin.TeleportToPits(new PitRequest
        {
            TargetSessionId = target.SessionId,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    // === FORCE LIGHTS ===
    
    /// <summary>
    /// Force headlights on/off
    /// </summary>
    [Command("forcelights")]
    public void ForceLights(string state, ACTcpClient target)
    {
        if (MyLevel == AdminLevel.None)
        {
            Reply("You don't have permission.");
            return;
        }
        
        bool forceOn = state.ToLowerInvariant() == "on";
        
        var result = _plugin.ForceLights(new ForceLightsRequest
        {
            TargetSessionId = target.SessionId,
            ForceOn = forceOn,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    // === TIME & WEATHER ===
    
    /// <summary>
    /// Set server time
    /// </summary>
    [Command("settime")]
    public void SetTime(string timeStr)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        // Parse HH:mm format
        var parts = timeStr.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute))
        {
            Reply("Invalid time format. Use HH:mm (e.g., 14:30)");
            return;
        }
        
        var result = _plugin.SetTime(new SetTimeRequest
        {
            Hour = hour,
            Minute = minute,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Set weather by config ID
    /// </summary>
    [Command("setweather")]
    public void SetWeather(int weatherId)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.SetWeather(new SetWeatherRequest
        {
            WeatherConfigId = weatherId,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// Set CSP weather type
    /// </summary>
    [Command("setcspweather")]
    public void SetCspWeather(string weatherType, float transitionSeconds = 30f)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.SetWeather(new SetWeatherRequest
        {
            WeatherType = weatherType,
            TransitionDuration = transitionSeconds,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    /// <summary>
    /// List CSP weather types
    /// </summary>
    [Command("cspweather")]
    public void ListCspWeatherTypes()
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var types = _plugin.GetCspWeatherTypes();
        Reply($"[CSP Weather Types]\n{string.Join(", ", types)}");
    }
    
    // === RESTRICTIONS ===
    
    /// <summary>
    /// Set ballast and restrictor for a player
    /// </summary>
    [Command("restrict")]
    public void SetRestriction(ACTcpClient target, int restrictor, int ballast)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.SetRestriction(new SetRestrictionRequest
        {
            TargetSessionId = target.SessionId,
            Restrictor = restrictor,
            BallastKg = ballast,
            AdminSteamId = MySteamId
        });
        
        Reply(result.Message);
    }
    
    // === WHITELIST ===
    
    /// <summary>
    /// Add Steam ID to whitelist
    /// </summary>
    [Command("whitelist")]
    public void AddToWhitelist(string steamId)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.AddToWhitelist(steamId, MySteamId);
        Reply(result.Message);
    }
    
    /// <summary>
    /// Remove Steam ID from whitelist
    /// </summary>
    [Command("unwhitelist")]
    public void RemoveFromWhitelist(string steamId)
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var result = _plugin.RemoveFromWhitelist(steamId, MySteamId);
        Reply(result.Message);
    }
    
    /// <summary>
    /// Show whitelist
    /// </summary>
    [Command("whitelistshow")]
    public void ShowWhitelist()
    {
        if (!_plugin.HasPermission(MySteamId, AdminLevel.Admin))
        {
            Reply("Requires Admin level.");
            return;
        }
        
        var whitelist = _plugin.GetWhitelist();
        
        if (whitelist.Count == 0)
        {
            Reply("Whitelist is empty.");
            return;
        }
        
        string message = $"[Whitelist: {whitelist.Count} entries]\n";
        foreach (var entry in whitelist.Take(10))
        {
            message += $"{entry.SteamId} ({entry.Name ?? "Unknown"})\n";
        }
        
        if (whitelist.Count > 10)
        {
            message += $"...and {whitelist.Count - 10} more";
        }
        
        Reply(message.TrimEnd());
    }
}
