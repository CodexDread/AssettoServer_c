using System.Collections.Concurrent;
using System.Text.Json;
using Serilog;

namespace AdminToolsPlugin;

/// <summary>
/// Service for managing player bans
/// </summary>
public class BanService
{
    private readonly AdminToolsConfiguration _config;
    private readonly ConcurrentDictionary<string, BanRecord> _bans = new();
    private readonly ConcurrentDictionary<string, string> _ipToBanId = new(); // IP -> Ban ID mapping
    private readonly object _saveLock = new();
    
    public event EventHandler<BanRecord>? OnPlayerBanned;
    public event EventHandler<BanRecord>? OnPlayerUnbanned;
    
    public BanService(AdminToolsConfiguration config)
    {
        _config = config;
        Load();
    }
    
    /// <summary>
    /// Check if a player is banned
    /// </summary>
    public BanRecord? GetActiveBan(string steamId, string? ipAddress = null)
    {
        // Check by Steam ID
        var ban = _bans.Values
            .Where(b => b.IsActive && !b.IsExpired && b.SteamId == steamId)
            .FirstOrDefault();
        
        if (ban != null) return ban;
        
        // Check by IP if enabled
        if (_config.EnableIPBans && !string.IsNullOrEmpty(ipAddress))
        {
            if (_ipToBanId.TryGetValue(ipAddress, out var banId))
            {
                if (_bans.TryGetValue(banId, out ban) && ban.IsActive && !ban.IsExpired)
                {
                    return ban;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if a player is banned (simple bool check)
    /// </summary>
    public bool IsBanned(string steamId, string? ipAddress = null)
    {
        return GetActiveBan(steamId, ipAddress) != null;
    }
    
    /// <summary>
    /// Ban a player
    /// </summary>
    public AdminActionResult BanPlayer(BanRequest request, string adminName)
    {
        // Check for existing active ban
        var existingBan = GetActiveBan(request.TargetSteamId);
        if (existingBan != null)
        {
            return AdminActionResult.Fail($"Player is already banned (expires: {existingBan.GetExpiryText()})", "ALREADY_BANNED");
        }
        
        var ban = new BanRecord
        {
            SteamId = request.TargetSteamId,
            PlayerName = request.TargetName ?? "Unknown",
            IpAddress = request.BanIp ? request.TargetIp : null,
            Reason = string.IsNullOrEmpty(request.Reason) ? "No reason provided" : request.Reason,
            BannedBy = request.AdminSteamId,
            BannedByName = adminName,
            ExpiresAt = request.DurationHours > 0 
                ? DateTime.UtcNow.AddHours(request.DurationHours) 
                : null
        };
        
        _bans[ban.Id] = ban;
        
        // Add IP mapping if applicable
        if (_config.EnableIPBans && !string.IsNullOrEmpty(ban.IpAddress))
        {
            _ipToBanId[ban.IpAddress] = ban.Id;
        }
        
        Save();
        
        OnPlayerBanned?.Invoke(this, ban);
        
        string durationText = ban.IsPermanent ? "permanently" : $"for {request.DurationHours} hours";
        Log.Information("Player {Name} ({SteamId}) banned {Duration} by {Admin}. Reason: {Reason}",
            ban.PlayerName, ban.SteamId, durationText, adminName, ban.Reason);
        
        return AdminActionResult.Ok($"Banned {ban.PlayerName} {durationText}");
    }
    
    /// <summary>
    /// Unban a player
    /// </summary>
    public AdminActionResult UnbanPlayer(string banIdOrSteamId, string adminSteamId, string adminName)
    {
        BanRecord? ban = null;
        
        // Try to find by ban ID first
        if (_bans.TryGetValue(banIdOrSteamId, out ban))
        {
            // Found by ID
        }
        else
        {
            // Try to find by Steam ID
            ban = _bans.Values.FirstOrDefault(b => b.SteamId == banIdOrSteamId && b.IsActive);
        }
        
        if (ban == null)
        {
            return AdminActionResult.Fail("Ban not found", "NOT_FOUND");
        }
        
        ban.IsActive = false;
        
        // Remove IP mapping
        if (!string.IsNullOrEmpty(ban.IpAddress))
        {
            _ipToBanId.TryRemove(ban.IpAddress, out _);
        }
        
        Save();
        
        OnPlayerUnbanned?.Invoke(this, ban);
        
        Log.Information("Player {Name} ({SteamId}) unbanned by {Admin}",
            ban.PlayerName, ban.SteamId, adminName);
        
        return AdminActionResult.Ok($"Unbanned {ban.PlayerName}");
    }
    
    /// <summary>
    /// Get all bans (optionally filtered)
    /// </summary>
    public List<BanRecord> GetBans(bool activeOnly = true, string? searchTerm = null)
    {
        var query = _bans.Values.AsEnumerable();
        
        if (activeOnly)
        {
            query = query.Where(b => b.IsActive && !b.IsExpired);
        }
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLowerInvariant();
            query = query.Where(b => 
                b.SteamId.Contains(searchTerm) ||
                b.PlayerName.ToLowerInvariant().Contains(searchTerm) ||
                b.Reason.ToLowerInvariant().Contains(searchTerm));
        }
        
        return query.OrderByDescending(b => b.BannedAt).ToList();
    }
    
    /// <summary>
    /// Get ban by ID
    /// </summary>
    public BanRecord? GetBan(string banId)
    {
        return _bans.TryGetValue(banId, out var ban) ? ban : null;
    }
    
    /// <summary>
    /// Clean up expired bans
    /// </summary>
    public int CleanupExpiredBans()
    {
        var expiredBans = _bans.Values.Where(b => b.IsExpired && b.IsActive).ToList();
        
        foreach (var ban in expiredBans)
        {
            ban.IsActive = false;
            
            if (!string.IsNullOrEmpty(ban.IpAddress))
            {
                _ipToBanId.TryRemove(ban.IpAddress, out _);
            }
        }
        
        if (expiredBans.Count > 0)
        {
            Save();
            Log.Information("Cleaned up {Count} expired bans", expiredBans.Count);
        }
        
        return expiredBans.Count;
    }
    
    /// <summary>
    /// Get the ban message for a player
    /// </summary>
    public string GetBanMessage(BanRecord ban)
    {
        return _config.BanMessage
            .Replace("{reason}", ban.Reason)
            .Replace("{expires}", ban.GetExpiryText())
            .Replace("{bannedby}", ban.BannedByName)
            .Replace("{bannedat}", ban.BannedAt.ToString("yyyy-MM-dd HH:mm"));
    }
    
    /// <summary>
    /// Get ban statistics
    /// </summary>
    public BanStats GetStats()
    {
        var allBans = _bans.Values.ToList();
        return new BanStats
        {
            TotalBans = allBans.Count,
            ActiveBans = allBans.Count(b => b.IsActive && !b.IsExpired),
            PermanentBans = allBans.Count(b => b.IsActive && b.IsPermanent),
            TempBans = allBans.Count(b => b.IsActive && !b.IsPermanent && !b.IsExpired),
            ExpiredBans = allBans.Count(b => b.IsExpired),
            Last24Hours = allBans.Count(b => b.BannedAt > DateTime.UtcNow.AddHours(-24))
        };
    }
    
    private void Load()
    {
        try
        {
            if (!File.Exists(_config.BanDatabasePath)) return;
            
            string json = File.ReadAllText(_config.BanDatabasePath);
            var loaded = JsonSerializer.Deserialize<List<BanRecord>>(json);
            
            if (loaded != null)
            {
                foreach (var ban in loaded)
                {
                    _bans[ban.Id] = ban;
                    
                    if (_config.EnableIPBans && !string.IsNullOrEmpty(ban.IpAddress) && ban.IsActive)
                    {
                        _ipToBanId[ban.IpAddress] = ban.Id;
                    }
                }
                
                Log.Information("Loaded {Count} bans from {Path}", _bans.Count, _config.BanDatabasePath);
            }
            
            // Clean up expired bans on load
            CleanupExpiredBans();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load bans from {Path}", _config.BanDatabasePath);
        }
    }
    
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_config.BanDatabasePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                var banList = _bans.Values.ToList();
                string json = JsonSerializer.Serialize(banList, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(_config.BanDatabasePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save bans to {Path}", _config.BanDatabasePath);
            }
        }
    }
}

/// <summary>
/// Ban statistics
/// </summary>
public class BanStats
{
    public int TotalBans { get; set; }
    public int ActiveBans { get; set; }
    public int PermanentBans { get; set; }
    public int TempBans { get; set; }
    public int ExpiredBans { get; set; }
    public int Last24Hours { get; set; }
}
