namespace SXRAdminToolsPlugin;

/// <summary>
/// Admin permission levels
/// </summary>
public enum AdminLevel
{
    None = 0,
    Moderator = 1,
    Admin = 2,
    SuperAdmin = 3
}

/// <summary>
/// Ban record
/// </summary>
public class BanRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string SteamId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string? IpAddress { get; set; }
    public string Reason { get; set; } = "";
    public string BannedBy { get; set; } = "";
    public string BannedByName { get; set; } = "";
    public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    
    public string GetExpiryText()
    {
        if (IsPermanent) return "Permanent";
        if (IsExpired) return "Expired";
        var remaining = ExpiresAt!.Value - DateTime.UtcNow;
        if (remaining.TotalDays >= 1) return $"{remaining.TotalDays:F1} days";
        if (remaining.TotalHours >= 1) return $"{remaining.TotalHours:F1} hours";
        return $"{remaining.TotalMinutes:F0} minutes";
    }
}

/// <summary>
/// Audit log entry
/// </summary>
public class AuditEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string AdminSteamId { get; set; } = "";
    public string AdminName { get; set; } = "";
    public AdminAction Action { get; set; }
    public string TargetSteamId { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Details { get; set; } = "";
    
    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] {AdminName} ({AdminSteamId}): {Action} -> {TargetName} ({TargetSteamId}) - {Details}";
    }
}

/// <summary>
/// Types of admin actions for audit
/// </summary>
public enum AdminAction
{
    Login,
    Logout,
    Kick,
    Ban,
    Unban,
    TempBan,
    Warning,
    Mute,
    Unmute,
    Teleport,
    Spectate,
    ConfigChange,
    Custom
}

/// <summary>
/// Connected player info for monitoring
/// </summary>
public class PlayerInfo
{
    public int SessionId { get; set; }
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public string CarModel { get; set; } = "";
    public string? IpAddress { get; set; }
    public DateTime ConnectedAt { get; set; }
    public int Ping { get; set; }
    public float SpeedKph { get; set; }
    public System.Numerics.Vector3 Position { get; set; }
    public int Collisions { get; set; }
    public int Resets { get; set; }
    public AdminLevel AdminLevel { get; set; }
    public bool IsAfk { get; set; }
    public DateTime LastActivity { get; set; }
    
    public TimeSpan SessionDuration => DateTime.UtcNow - ConnectedAt;
    
    public string GetSessionDurationText()
    {
        var duration = SessionDuration;
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1}h";
        return $"{duration.TotalMinutes:F0}m";
    }
}

/// <summary>
/// Kick request
/// </summary>
public class KickRequest
{
    public string TargetSteamId { get; set; } = "";
    public string? TargetName { get; set; }
    public int? TargetSessionId { get; set; }
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Ban request
/// </summary>
public class BanRequest
{
    public string TargetSteamId { get; set; } = "";
    public string? TargetName { get; set; }
    public string? TargetIp { get; set; }
    public string Reason { get; set; } = "";
    public int DurationHours { get; set; } = 0; // 0 = permanent
    public string AdminSteamId { get; set; } = "";
    public bool BanIp { get; set; } = false;
}

/// <summary>
/// Result of an admin action
/// </summary>
public class AdminActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ErrorCode { get; set; }
    
    public static AdminActionResult Ok(string message = "Success") => 
        new() { Success = true, Message = message };
    
    public static AdminActionResult Fail(string message, string? errorCode = null) => 
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

/// <summary>
/// Interface for custom admin tools
/// </summary>
public interface IAdminTool
{
    /// <summary>
    /// Unique identifier for this tool
    /// </summary>
    string ToolId { get; }
    
    /// <summary>
    /// Display name
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Minimum admin level required
    /// </summary>
    AdminLevel RequiredLevel { get; }
    
    /// <summary>
    /// Initialize the tool
    /// </summary>
    Task InitializeAsync(SXRAdminToolsPlugin plugin);
    
    /// <summary>
    /// Execute the tool action
    /// </summary>
    Task<AdminActionResult> ExecuteAsync(AdminContext context, Dictionary<string, object> parameters);
}

/// <summary>
/// Context passed to admin tools
/// </summary>
public class AdminContext
{
    public string AdminSteamId { get; set; } = "";
    public string AdminName { get; set; } = "";
    public AdminLevel AdminLevel { get; set; }
    public SXRAdminToolsPlugin Plugin { get; set; } = null!;
}

/// <summary>
/// Event args for admin action events
/// </summary>
public class AdminActionEventArgs : EventArgs
{
    public AuditEntry AuditEntry { get; }
    public AdminActionResult Result { get; }
    
    public AdminActionEventArgs(AuditEntry entry, AdminActionResult result)
    {
        AuditEntry = entry;
        Result = result;
    }
}

/// <summary>
/// Whitelist entry
/// </summary>
public class WhitelistEntry
{
    public string SteamId { get; set; } = "";
    public string? Name { get; set; }
    public string AddedBy { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

/// <summary>
/// Player restriction settings (ballast/restrictor)
/// </summary>
public class PlayerRestriction
{
    public int SessionId { get; set; }
    public string SteamId { get; set; } = "";
    public int Restrictor { get; set; } // 0-400, cuts engine power
    public int BallastKg { get; set; }  // Additional weight in kg
    public string SetBy { get; set; } = "";
    public DateTime SetAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to teleport a player to pits
/// </summary>
public class PitRequest
{
    public int? TargetSessionId { get; set; }
    public string? TargetSteamId { get; set; }
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Request to set server time
/// </summary>
public class SetTimeRequest
{
    public int Hour { get; set; }
    public int Minute { get; set; }
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Request to set weather
/// </summary>
public class SetWeatherRequest
{
    public int? WeatherConfigId { get; set; }
    public string? WeatherType { get; set; }  // CSP weather type
    public float TransitionDuration { get; set; } = 30f;
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Request to force headlights
/// </summary>
public class ForceLightsRequest
{
    public int TargetSessionId { get; set; }
    public bool ForceOn { get; set; }
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Request to set restrictions
/// </summary>
public class SetRestrictionRequest
{
    public int TargetSessionId { get; set; }
    public int Restrictor { get; set; } // 0-400
    public int BallastKg { get; set; }
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Request to change a config value
/// </summary>
public class SetConfigRequest
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

/// <summary>
/// Server environment state for UI
/// </summary>
public class ServerEnvironment
{
    public int TimeHour { get; set; }
    public int TimeMinute { get; set; }
    public string TimeString => $"{TimeHour:D2}:{TimeMinute:D2}";
    public int WeatherConfigId { get; set; }
    public string? WeatherType { get; set; }
    public string? WeatherDescription { get; set; }
    public float AmbientTemp { get; set; }
    public float RoadTemp { get; set; }
}

/// <summary>
/// Server capabilities for feature availability checks
/// Allows admin panel to show/hide sections based on what's available
/// </summary>
public class ServerCapabilities
{
    // === CORE ADMIN SYSTEMS (Always Available) ===
    public bool PlayerManagement { get; set; } = true;
    public bool BanSystem { get; set; } = true;
    public bool AuditLog { get; set; } = true;
    public bool TimeWeatherControl { get; set; } = true;
    public bool WhitelistManagement { get; set; } = true;
    
    // === SXR PLUGIN INTEGRATIONS ===
    // These are checked at runtime to see if plugins are loaded
    
    /// <summary>Player stats, XP, leveling, prestige system</summary>
    public bool PlayerStatsAvailable { get; set; }
    
    /// <summary>Custom nameplates and driver info display</summary>
    public bool NameplatesAvailable { get; set; }
    
    /// <summary>SP Battle highway racing system</summary>
    public bool SPBattleAvailable { get; set; }
    
    /// <summary>Car lock/unlock system based on driver level</summary>
    public bool CarLockAvailable { get; set; }
    
    // === PLANNED SYSTEMS (Not Yet Implemented) ===
    
    /// <summary>Club/team management system (PLANNED)</summary>
    public bool ClubSystemAvailable { get; set; }
    
    /// <summary>Time trials / time attack system (PLANNED)</summary>
    public bool TimeTrialsAvailable { get; set; }
    
    /// <summary>Global rankings and leaderboards (PLANNED)</summary>
    public bool RankingsAvailable { get; set; }
    
    /// <summary>Tournament management system (PLANNED)</summary>
    public bool TournamentAvailable { get; set; }
    
    /// <summary>Economy / currency system (PLANNED)</summary>
    public bool EconomyAvailable { get; set; }
    
    /// <summary>Achievement system (PLANNED)</summary>
    public bool AchievementsAvailable { get; set; }
    
    // === FEATURE INFO ===
    
    /// <summary>Message to display for unavailable features</summary>
    public string UnavailableMessage { get; set; } = "Coming Soon";
    
    /// <summary>Server version info</summary>
    public string ServerVersion { get; set; } = "1.0.0";
    
    /// <summary>List of loaded SXR plugins</summary>
    public List<string> LoadedPlugins { get; set; } = new();
}
