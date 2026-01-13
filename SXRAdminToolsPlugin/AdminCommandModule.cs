using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace AdminToolsPlugin;

/// <summary>
/// Admin chat commands
/// </summary>
[RequireConnectedPlayer]
public class AdminCommandModule : ACModuleBase
{
    private readonly AdminToolsPlugin _plugin;
    private readonly BanService _banService;
    private readonly AuditService _auditService;
    
    public AdminCommandModule(
        AdminToolsPlugin plugin,
        BanService banService,
        AuditService auditService)
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
        message += "/playerinfo <name> - Player details\n";
        message += "/kick <name> [reason] - Kick player\n";
        message += "/kickid <id> [reason] - Kick by session ID\n";
        
        if (level >= AdminLevel.Moderator)
        {
            message += "/tempban <name> <hours> [reason] - Temp ban\n";
            message += "/bans [search] - List bans\n";
        }
        
        if (level >= AdminLevel.Admin)
        {
            message += "/ban <name> [reason] - Permanent ban\n";
            message += "/banid <steamid> [hours] [reason] - Offline ban\n";
            message += "/unban <id> - Remove ban\n";
            message += "/audit [count] - View audit log\n";
        }
        
        Reply(message.TrimEnd());
    }
}
