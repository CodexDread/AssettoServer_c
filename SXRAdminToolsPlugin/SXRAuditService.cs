using System.Collections.Concurrent;
using Serilog;

namespace SXRAdminToolsPlugin;

/// <summary>
/// Service for audit logging of admin actions
/// </summary>
public class SXRAuditService
{
    private readonly SXRAdminToolsConfiguration _config;
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private readonly object _fileLock = new();
    private StreamWriter? _logWriter;
    
    public event EventHandler<AuditEntry>? OnAuditEntry;
    
    public SXRAuditService(SXRAdminToolsConfiguration config)
    {
        _config = config;
        
        if (_config.EnableAuditLog)
        {
            InitializeLogFile();
        }
    }
    
    private void InitializeLogFile()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_config.AuditLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            _logWriter = new StreamWriter(_config.AuditLogPath, append: true)
            {
                AutoFlush = true
            };
            
            Log.Information("Audit log initialized at {Path}", _config.AuditLogPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize audit log at {Path}", _config.AuditLogPath);
        }
    }
    
    /// <summary>
    /// Log an admin action
    /// </summary>
    public void Log(
        AdminAction action,
        string adminSteamId,
        string adminName,
        string targetSteamId = "",
        string targetName = "",
        string details = "")
    {
        if (!_config.EnableAuditLog) return;
        
        // Check if this action type should be logged
        if (action == AdminAction.Kick && !_config.LogKicks) return;
        if (action == AdminAction.Ban && !_config.LogBans) return;
        if ((action == AdminAction.Login || action == AdminAction.Logout) && !_config.LogAdminLogins) return;
        
        var entry = new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AdminSteamId = adminSteamId,
            AdminName = adminName,
            Action = action,
            TargetSteamId = targetSteamId,
            TargetName = targetName,
            Details = details
        };
        
        // Add to in-memory queue
        _entries.Enqueue(entry);
        
        // Trim queue if too large
        while (_entries.Count > _config.MaxAuditLogEntries)
        {
            _entries.TryDequeue(out _);
        }
        
        // Write to file
        WriteToFile(entry);
        
        // Fire event
        OnAuditEntry?.Invoke(this, entry);
        
        Serilog.Log.Debug("Audit: {Entry}", entry.ToString());
    }
    
    /// <summary>
    /// Log a kick action
    /// </summary>
    public void LogKick(string adminSteamId, string adminName, string targetSteamId, string targetName, string reason)
    {
        Log(AdminAction.Kick, adminSteamId, adminName, targetSteamId, targetName, $"Reason: {reason}");
    }
    
    /// <summary>
    /// Log a ban action
    /// </summary>
    public void LogBan(string adminSteamId, string adminName, string targetSteamId, string targetName, string reason, int durationHours)
    {
        string duration = durationHours == 0 ? "permanent" : $"{durationHours} hours";
        Log(AdminAction.Ban, adminSteamId, adminName, targetSteamId, targetName, $"Reason: {reason}, Duration: {duration}");
    }
    
    /// <summary>
    /// Log a temp ban action
    /// </summary>
    public void LogTempBan(string adminSteamId, string adminName, string targetSteamId, string targetName, string reason, int durationHours)
    {
        Log(AdminAction.TempBan, adminSteamId, adminName, targetSteamId, targetName, $"Reason: {reason}, Duration: {durationHours} hours");
    }
    
    /// <summary>
    /// Log an unban action
    /// </summary>
    public void LogUnban(string adminSteamId, string adminName, string targetSteamId, string targetName)
    {
        Log(AdminAction.Unban, adminSteamId, adminName, targetSteamId, targetName, "");
    }
    
    /// <summary>
    /// Log admin login
    /// </summary>
    public void LogLogin(string adminSteamId, string adminName, AdminLevel level)
    {
        Log(AdminAction.Login, adminSteamId, adminName, "", "", $"Level: {level}");
    }
    
    /// <summary>
    /// Log admin logout
    /// </summary>
    public void LogLogout(string adminSteamId, string adminName)
    {
        Log(AdminAction.Logout, adminSteamId, adminName, "", "", "");
    }
    
    /// <summary>
    /// Get recent audit entries
    /// </summary>
    public List<AuditEntry> GetRecentEntries(int count = 50, AdminAction? filterAction = null)
    {
        var entries = _entries.Reverse().AsEnumerable();
        
        if (filterAction.HasValue)
        {
            entries = entries.Where(e => e.Action == filterAction.Value);
        }
        
        return entries.Take(count).ToList();
    }
    
    /// <summary>
    /// Get entries by admin
    /// </summary>
    public List<AuditEntry> GetEntriesByAdmin(string adminSteamId, int count = 50)
    {
        return _entries
            .Where(e => e.AdminSteamId == adminSteamId)
            .Reverse()
            .Take(count)
            .ToList();
    }
    
    /// <summary>
    /// Get entries targeting a player
    /// </summary>
    public List<AuditEntry> GetEntriesForTarget(string targetSteamId, int count = 50)
    {
        return _entries
            .Where(e => e.TargetSteamId == targetSteamId)
            .Reverse()
            .Take(count)
            .ToList();
    }
    
    /// <summary>
    /// Search audit entries
    /// </summary>
    public List<AuditEntry> Search(string searchTerm, int count = 50)
    {
        searchTerm = searchTerm.ToLowerInvariant();
        
        return _entries
            .Where(e => 
                e.AdminName.ToLowerInvariant().Contains(searchTerm) ||
                e.TargetName.ToLowerInvariant().Contains(searchTerm) ||
                e.Details.ToLowerInvariant().Contains(searchTerm) ||
                e.AdminSteamId.Contains(searchTerm) ||
                e.TargetSteamId.Contains(searchTerm))
            .Reverse()
            .Take(count)
            .ToList();
    }
    
    private void WriteToFile(AuditEntry entry)
    {
        if (_logWriter == null) return;
        
        lock (_fileLock)
        {
            try
            {
                _logWriter.WriteLine(entry.ToString());
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to write audit entry to file");
            }
        }
    }
    
    public void Dispose()
    {
        _logWriter?.Dispose();
    }
}
