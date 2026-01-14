using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SXRSXRAdminToolsPlugin;

/// <summary>
/// Configuration for Admin Tools Plugin
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRSXRAdminToolsConfiguration : IValidateConfiguration<SXRAdminToolsConfigurationValidator>
{
    // === ADMIN AUTHENTICATION ===
    
    /// <summary>
    /// List of SuperAdmin Steam IDs (full access)
    /// </summary>
    public List<string> SuperAdmins { get; init; } = new();
    
    /// <summary>
    /// List of Admin Steam IDs (kick, ban, monitor)
    /// </summary>
    public List<string> Admins { get; init; } = new();
    
    /// <summary>
    /// List of Moderator Steam IDs (kick, monitor, limited ban)
    /// </summary>
    public List<string> Moderators { get; init; } = new();
    
    /// <summary>
    /// Maximum ban duration for Moderators (hours, 0 = no limit)
    /// </summary>
    public int ModeratorMaxBanHours { get; init; } = 24;
    
    // === BAN SYSTEM ===
    
    /// <summary>
    /// Path to ban database file
    /// </summary>
    public string BanDatabasePath { get; init; } = "cfg/plugins/SXRAdminToolsPlugin/bans.json";
    
    /// <summary>
    /// Enable IP banning (in addition to Steam ID)
    /// </summary>
    public bool EnableIPBans { get; init; } = false;
    
    /// <summary>
    /// Default ban duration in hours (0 = permanent)
    /// </summary>
    public int DefaultBanDurationHours { get; init; } = 0;
    
    /// <summary>
    /// Check bans on connection (recommended)
    /// </summary>
    public bool CheckBansOnConnect { get; init; } = true;
    
    /// <summary>
    /// Message shown to banned players
    /// </summary>
    public string BanMessage { get; init; } = "You are banned from this server. Reason: {reason}. Expires: {expires}";
    
    // === KICK SYSTEM ===
    
    /// <summary>
    /// Default kick reason if none provided
    /// </summary>
    public string DefaultKickReason { get; init; } = "Kicked by admin";
    
    /// <summary>
    /// Cooldown between kicks of same player (seconds)
    /// </summary>
    public int KickCooldownSeconds { get; init; } = 5;
    
    // === AUDIT LOGGING ===
    
    /// <summary>
    /// Enable audit logging of admin actions
    /// </summary>
    public bool EnableAuditLog { get; init; } = true;
    
    /// <summary>
    /// Path to audit log file
    /// </summary>
    public string AuditLogPath { get; init; } = "cfg/plugins/SXRAdminToolsPlugin/audit.log";
    
    /// <summary>
    /// Maximum audit log entries to keep in memory
    /// </summary>
    public int MaxAuditLogEntries { get; init; } = 1000;
    
    /// <summary>
    /// Log kicks to audit
    /// </summary>
    public bool LogKicks { get; init; } = true;
    
    /// <summary>
    /// Log bans to audit
    /// </summary>
    public bool LogBans { get; init; } = true;
    
    /// <summary>
    /// Log admin logins to audit
    /// </summary>
    public bool LogAdminLogins { get; init; } = true;
    
    // === UI SETTINGS ===
    
    /// <summary>
    /// Enable in-game Lua admin panel
    /// </summary>
    public bool EnableLuaUI { get; init; } = true;
    
    /// <summary>
    /// Hotkey to open admin panel (CSP key code)
    /// </summary>
    public int AdminPanelHotkey { get; init; } = 0x79; // F10
    
    /// <summary>
    /// Show admin indicator in player list
    /// </summary>
    public bool ShowAdminIndicator { get; init; } = true;
    
    // === HTTP API ===
    
    /// <summary>
    /// Enable HTTP API endpoints
    /// </summary>
    public bool EnableHttpApi { get; init; } = true;
    
    /// <summary>
    /// Require authentication for HTTP API
    /// </summary>
    public bool RequireHttpAuth { get; init; } = true;
    
    /// <summary>
    /// HTTP API key (if RequireHttpAuth is true)
    /// </summary>
    public string HttpApiKey { get; init; } = "";
    
    // === MONITORING ===
    
    /// <summary>
    /// Update interval for player monitoring (ms)
    /// </summary>
    public int MonitoringUpdateIntervalMs { get; init; } = 1000;
    
    /// <summary>
    /// Track player incidents (collisions, resets)
    /// </summary>
    public bool TrackIncidents { get; init; } = true;
    
    /// <summary>
    /// Incident threshold for auto-warning
    /// </summary>
    public int IncidentWarningThreshold { get; init; } = 10;
    
    // === FRAMEWORK ===
    
    /// <summary>
    /// Enable custom admin tools loading
    /// </summary>
    public bool EnableCustomTools { get; init; } = true;
}

public class SXRSXRAdminToolsConfigurationValidator : IValidator<SXRAdminToolsConfiguration>
{
    public bool Validate(SXRAdminToolsConfiguration config, out string? errorMessage)
    {
        if (config.SuperAdmins.Count == 0 && config.Admins.Count == 0)
        {
            errorMessage = "At least one SuperAdmin or Admin must be configured";
            return false;
        }
        
        if (config.RequireHttpAuth && string.IsNullOrEmpty(config.HttpApiKey))
        {
            errorMessage = "HttpApiKey must be set when RequireHttpAuth is enabled";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
