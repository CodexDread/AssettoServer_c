using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Service for tracking and persisting player statistics
/// </summary>
public class SXRPlayerStatsService
{
    private readonly SXRPlayerStatsConfiguration _config;
    private readonly ConcurrentDictionary<string, PlayerStats> _stats = new();
    private readonly ConcurrentDictionary<string, SessionTracker> _sessions = new();
    private readonly object _saveLock = new();
    private DateTime _lastSave = DateTime.UtcNow;
    
    public SXRPlayerStatsService(SXRPlayerStatsConfiguration config)
    {
        _config = config;
        Load();
    }
    
    /// <summary>
    /// Get or create player stats
    /// </summary>
    public PlayerStats GetStats(string steamId, string name = "")
    {
        return _stats.GetOrAdd(steamId, _ => new PlayerStats
        {
            SteamId = steamId,
            Name = name,
            DriverLevel = _config.StartingDriverLevel,
            XPToNextLevel = CalculateXPForLevel(_config.StartingDriverLevel + 1)
        });
    }
    
    /// <summary>
    /// Get session tracker for a player
    /// </summary>
    public SessionTracker GetSession(string steamId)
    {
        return _sessions.GetOrAdd(steamId, _ => new SessionTracker { SteamId = steamId });
    }
    
    /// <summary>
    /// Start a new session for player
    /// </summary>
    public void StartSession(string steamId, string name, string carModel)
    {
        var stats = GetStats(steamId, name);
        stats.Name = name;
        stats.LastSeen = DateTime.UtcNow;
        stats.TotalSessions++;
        
        var session = GetSession(steamId);
        session.Reset();
        session.CurrentCar = carModel;
        
        Log.Debug("Started session for {Name} ({SteamId}) in {Car}", name, steamId, carModel);
    }
    
    /// <summary>
    /// End a player's session and commit stats
    /// </summary>
    public void EndSession(string steamId)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        var sessionDuration = (long)(DateTime.UtcNow - session.SessionStart).TotalSeconds;
        
        // Only count if session was long enough
        if (sessionDuration < _config.MinSessionTimeSeconds)
        {
            Log.Debug("Session too short for {SteamId}: {Duration}s", steamId, sessionDuration);
            return;
        }
        
        // Commit session stats
        stats.TotalTimeOnServerSeconds += sessionDuration;
        stats.TotalActiveTimeSeconds += session.SessionActiveTimeMs / 1000;
        stats.TotalDistanceMeters += session.SessionDistanceMeters;
        stats.TotalCollisions += session.SessionCollisions;
        
        // Update longest session
        if (sessionDuration > stats.LongestSessionSeconds)
            stats.LongestSessionSeconds = sessionDuration;
        
        // Update longest drive
        if (session.SessionDistanceMeters > stats.LongestDriveMeters)
            stats.LongestDriveMeters = session.SessionDistanceMeters;
        
        // Update top speed
        if (session.SessionTopSpeed > stats.TopSpeedKph)
            stats.TopSpeedKph = session.SessionTopSpeed;
        
        // Update speed averages
        stats.TotalSpeedSamples += session.SessionSpeedSamples;
        stats.SpeedSampleCount += session.SessionSpeedSampleCount;
        
        // Update car stats
        if (!string.IsNullOrEmpty(session.CurrentCar))
        {
            var carStats = stats.GetCarStats(session.CurrentCar);
            carStats.DistanceMeters += session.SessionDistanceMeters;
            carStats.TimeUsedSeconds += sessionDuration;
            carStats.CollisionsWithCar += session.SessionCollisions;
            carStats.LastUsed = DateTime.UtcNow;
            
            if (session.SessionTopSpeed > carStats.TopSpeedKph)
                carStats.TopSpeedKph = session.SessionTopSpeed;
        }
        
        // Update favorite car
        stats.UpdateFavoriteCar();
        
        // Check milestones
        CheckMilestones(stats);
        
        // Award session XP
        AwardXP(stats, session.SessionXPEarned);
        
        stats.LastSeen = DateTime.UtcNow;
        
        Log.Information("Session ended for {Name}: {Distance:F1}km, {Time}min, {XP} XP", 
            stats.Name, 
            session.SessionDistanceMeters / 1000.0,
            sessionDuration / 60,
            session.SessionXPEarned);
        
        // Clear session
        session.Reset();
        
        // Trigger save if needed
        CheckAutoSave();
    }
    
    /// <summary>
    /// Update tracking data from position update
    /// </summary>
    public void UpdateTracking(string steamId, System.Numerics.Vector3 position, float speedKph, long serverTime)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        // Calculate time delta
        long deltaMs = serverTime - session.LastUpdateTick;
        if (deltaMs <= 0 || deltaMs > 5000) // Skip bad deltas
        {
            session.LastUpdateTick = serverTime;
            session.LastPosition = position;
            return;
        }
        
        // Calculate distance traveled
        float distance = System.Numerics.Vector3.Distance(session.LastPosition, position);
        
        // Sanity check - ignore teleports
        if (distance < 500) // Max reasonable distance in one update
        {
            session.SessionDistanceMeters += distance;
            
            // Track high speed distance
            if (speedKph >= _config.HighSpeedThresholdKph)
            {
                stats.HighSpeedDistanceMeters += distance;
            }
            
            // Track active time
            if (speedKph >= _config.MinActiveSpeedKph)
            {
                session.SessionActiveTimeMs += deltaMs;
                
                // Award XP for driving
                float distanceKm = distance / 1000f;
                session.SessionXPEarned += (long)(distanceKm * _config.XPPerKilometer);
                
                float activeMinutes = deltaMs / 60000f;
                session.SessionXPEarned += (long)(activeMinutes * _config.XPPerMinuteActive);
            }
        }
        
        // Track speed
        if (speedKph > 0)
        {
            session.SessionSpeedSamples += speedKph;
            session.SessionSpeedSampleCount++;
            
            if (speedKph > session.SessionTopSpeed)
                session.SessionTopSpeed = speedKph;
        }
        
        // Update car stats top speed
        if (!string.IsNullOrEmpty(session.CurrentCar) && speedKph > 0)
        {
            var carStats = stats.GetCarStats(session.CurrentCar);
            if (speedKph > carStats.TopSpeedKph)
                carStats.TopSpeedKph = speedKph;
        }
        
        session.LastPosition = position;
        session.LastUpdateTick = serverTime;
    }
    
    /// <summary>
    /// Record a collision
    /// </summary>
    public void RecordCollision(string steamId, bool isCarCollision)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        session.SessionCollisions++;
        
        if (session.InRace)
            session.RaceCollisions++;
        
        if (isCarCollision)
            stats.CarCollisions++;
        else
            stats.WallCollisions++;
        
        // XP penalty
        session.SessionXPEarned = Math.Max(0, session.SessionXPEarned - _config.XPPenaltyPerCollision);
    }
    
    /// <summary>
    /// Record race participation
    /// </summary>
    public void RecordRaceStart(string steamId)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        session.InRace = true;
        session.RaceCollisions = 0;
        session.RaceDistanceMeters = 0;
        
        stats.RacesParticipated++;
        
        if (!string.IsNullOrEmpty(session.CurrentCar))
        {
            var carStats = stats.GetCarStats(session.CurrentCar);
            carStats.RacesUsed++;
        }
    }
    
    /// <summary>
    /// Record race finish
    /// </summary>
    public void RecordRaceFinish(string steamId, int position, bool isDNF = false)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        session.InRace = false;
        
        if (isDNF)
        {
            stats.RaceDNFs++;
        }
        else
        {
            // Award completion XP
            session.SessionXPEarned += _config.XPForRaceComplete;
            
            // Check for clean race
            if (session.RaceCollisions == 0)
            {
                stats.CleanRaces++;
                session.SessionXPEarned = (long)(session.SessionXPEarned * _config.CleanRaceXPMultiplier);
            }
            
            // Track worst collision race
            if (session.RaceCollisions > stats.MostCollisionsInRace)
                stats.MostCollisionsInRace = session.RaceCollisions;
            
            // Position rewards
            if (position == 1)
            {
                stats.RaceWins++;
                session.SessionXPEarned += _config.XPForWin;
                
                if (!string.IsNullOrEmpty(session.CurrentCar))
                {
                    var carStats = stats.GetCarStats(session.CurrentCar);
                    carStats.WinsWithCar++;
                }
            }
            
            if (position <= 3)
                stats.RacePodiums++;
        }
    }
    
    /// <summary>
    /// Record SP Battle result (integration with SPBattlePlugin)
    /// </summary>
    public void RecordBattleResult(string steamId, bool isWin)
    {
        if (!_sessions.TryGetValue(steamId, out var session)) return;
        if (!_stats.TryGetValue(steamId, out var stats)) return;
        
        stats.BattlesParticipated++;
        
        if (isWin)
        {
            stats.BattleWins++;
            stats.CurrentWinStreak++;
            
            if (stats.CurrentWinStreak > stats.BestWinStreak)
                stats.BestWinStreak = stats.CurrentWinStreak;
            
            session.SessionXPEarned += _config.XPForWin;
        }
        else
        {
            stats.BattleLosses++;
            stats.CurrentWinStreak = 0;
        }
        
        CheckMilestones(stats);
    }
    
    /// <summary>
    /// Award XP and handle leveling and prestige
    /// </summary>
    public void AwardXP(PlayerStats stats, long amount)
    {
        if (!_config.EnableDriverLevel || amount <= 0) return;
        
        stats.TotalXP += amount;
        
        // Check for level ups
        while (stats.TotalXP >= stats.XPToNextLevel && stats.DriverLevel < _config.MaxDriverLevel)
        {
            stats.DriverLevel++;
            
            // Track highest level achieved
            if (stats.DriverLevel > stats.HighestLevelAchieved)
                stats.HighestLevelAchieved = stats.DriverLevel;
            
            long xpForNextLevel = CalculateXPForLevel(stats.DriverLevel + 1);
            stats.XPToNextLevel = xpForNextLevel;
            
            Log.Information("{Name} reached Driver Level {Level}!", stats.Name, stats.DriverLevel);
            
            CheckMilestones(stats);
        }
        
        // Check for prestige at max level
        if (stats.DriverLevel >= _config.MaxDriverLevel)
        {
            stats.DriverLevel = _config.MaxDriverLevel;
            
            // Check if player wants to prestige (auto-prestige on max level)
            if (stats.DriverLevel == 999)
            {
                stats.TimesReachedMaxLevel++;
                
                // First time reaching max level triggers prestige
                if (stats.TotalXP >= stats.XPToNextLevel)
                {
                    Prestige(stats);
                }
            }
        }
    }
    
    /// <summary>
    /// Prestige a player - reset level but keep car access
    /// </summary>
    public void Prestige(PlayerStats stats)
    {
        stats.PrestigeRank++;
        int previousLevel = stats.DriverLevel;
        
        // Reset level to 1
        stats.DriverLevel = 1;
        stats.XPToNextLevel = CalculateXPForLevel(2);
        // Note: TotalXP is NOT reset - it continues accumulating
        
        // Make sure highest level is tracked
        if (previousLevel > stats.HighestLevelAchieved)
            stats.HighestLevelAchieved = previousLevel;
        
        Log.Information("{Name} has PRESTIGED to rank {Prestige}!", stats.Name, stats.PrestigeRank);
        
        // Award prestige milestones
        CheckPrestigeMilestones(stats);
        
        Save(); // Save immediately on prestige
    }
    
    /// <summary>
    /// Manually prestige a player (admin command or player request)
    /// Only works if at max level
    /// </summary>
    public bool TryPrestige(string steamId)
    {
        if (!_stats.TryGetValue(steamId, out var stats)) return false;
        if (stats.DriverLevel < _config.MaxDriverLevel) return false;
        
        Prestige(stats);
        return true;
    }
    
    /// <summary>
    /// Get effective level for car unlocks (considers prestige)
    /// </summary>
    public int GetEffectiveLevelForUnlocks(string steamId)
    {
        if (!_stats.TryGetValue(steamId, out var stats)) return 1;
        
        // Prestiged players keep max level car access
        return stats.PrestigeRank > 0 ? 999 : stats.DriverLevel;
    }
    
    /// <summary>
    /// Check prestige-related milestones
    /// </summary>
    private void CheckPrestigeMilestones(PlayerStats stats)
    {
        var achieved = stats.AchievedMilestones;
        
        if (stats.PrestigeRank >= 1 && !achieved.Contains(Milestones.Prestige1))
            achieved.Add(Milestones.Prestige1);
        if (stats.PrestigeRank >= 5 && !achieved.Contains(Milestones.Prestige5))
            achieved.Add(Milestones.Prestige5);
        if (stats.PrestigeRank >= 10 && !achieved.Contains(Milestones.Prestige10))
            achieved.Add(Milestones.Prestige10);
    }
    
    /// <summary>
    /// Calculate total XP required for a level
    /// </summary>
    public long CalculateXPForLevel(int level)
    {
        if (level <= 1) return 0;
        
        // Exponential scaling
        return (long)(_config.BaseXPPerLevel * Math.Pow(_config.XPScalingFactor, level - 2));
    }
    
    /// <summary>
    /// Get XP progress to next level (0-1)
    /// </summary>
    public float GetLevelProgress(PlayerStats stats)
    {
        if (stats.DriverLevel >= _config.MaxDriverLevel) return 1f;
        
        long currentLevelXP = CalculateXPForLevel(stats.DriverLevel);
        long nextLevelXP = stats.XPToNextLevel;
        long xpIntoLevel = stats.TotalXP - currentLevelXP;
        long xpNeeded = nextLevelXP - currentLevelXP;
        
        return xpNeeded > 0 ? (float)xpIntoLevel / xpNeeded : 1f;
    }
    
    /// <summary>
    /// Check and award milestones
    /// </summary>
    private void CheckMilestones(PlayerStats stats)
    {
        var achieved = stats.AchievedMilestones;
        
        // Distance milestones
        if (stats.TotalDistanceKm >= 100 && !achieved.Contains(Milestones.Distance100Km))
            achieved.Add(Milestones.Distance100Km);
        if (stats.TotalDistanceKm >= 1000 && !achieved.Contains(Milestones.Distance1000Km))
            achieved.Add(Milestones.Distance1000Km);
        if (stats.TotalDistanceKm >= 10000 && !achieved.Contains(Milestones.Distance10000Km))
            achieved.Add(Milestones.Distance10000Km);
        
        // Speed milestones
        if (stats.TopSpeedKph >= 200 && !achieved.Contains(Milestones.Speed200Kph))
            achieved.Add(Milestones.Speed200Kph);
        if (stats.TopSpeedKph >= 300 && !achieved.Contains(Milestones.Speed300Kph))
            achieved.Add(Milestones.Speed300Kph);
        if (stats.TopSpeedKph >= 400 && !achieved.Contains(Milestones.Speed400Kph))
            achieved.Add(Milestones.Speed400Kph);
        
        // Win milestones
        int totalWins = stats.RaceWins + stats.BattleWins;
        if (totalWins >= 10 && !achieved.Contains(Milestones.Wins10))
            achieved.Add(Milestones.Wins10);
        if (totalWins >= 50 && !achieved.Contains(Milestones.Wins50))
            achieved.Add(Milestones.Wins50);
        if (totalWins >= 100 && !achieved.Contains(Milestones.Wins100))
            achieved.Add(Milestones.Wins100);
        
        // Time milestones
        double hours = stats.TotalTimeOnServerSeconds / 3600.0;
        if (hours >= 10 && !achieved.Contains(Milestones.Hours10))
            achieved.Add(Milestones.Hours10);
        if (hours >= 100 && !achieved.Contains(Milestones.Hours100))
            achieved.Add(Milestones.Hours100);
        if (hours >= 500 && !achieved.Contains(Milestones.Hours500))
            achieved.Add(Milestones.Hours500);
        
        // Car milestones
        if (stats.UniqueCarsUsed >= 10 && !achieved.Contains(Milestones.Cars10))
            achieved.Add(Milestones.Cars10);
        if (stats.UniqueCarsUsed >= 25 && !achieved.Contains(Milestones.Cars25))
            achieved.Add(Milestones.Cars25);
        
        // Win streak
        if (stats.BestWinStreak >= 5 && !achieved.Contains(Milestones.WinStreak5))
            achieved.Add(Milestones.WinStreak5);
        if (stats.BestWinStreak >= 10 && !achieved.Contains(Milestones.WinStreak10))
            achieved.Add(Milestones.WinStreak10);
        
        // Level milestones
        if (stats.DriverLevel >= 25 && !achieved.Contains(Milestones.Level25))
            achieved.Add(Milestones.Level25);
        if (stats.DriverLevel >= 50 && !achieved.Contains(Milestones.Level50))
            achieved.Add(Milestones.Level50);
        if (stats.DriverLevel >= 100 && !achieved.Contains(Milestones.Level100))
            achieved.Add(Milestones.Level100);
        if (stats.DriverLevel >= 500 && !achieved.Contains(Milestones.Level500))
            achieved.Add(Milestones.Level500);
        if (stats.DriverLevel >= 999 && !achieved.Contains(Milestones.Level999))
            achieved.Add(Milestones.Level999);
    }
    
    /// <summary>
    /// Get leaderboard for a category
    /// </summary>
    public List<LeaderboardEntry> GetLeaderboard(LeaderboardCategory category, int count = 10)
    {
        var entries = _stats.Values
            .Select(s => new { Stats = s, Value = GetStatValue(s, category) })
            .OrderByDescending(x => x.Value)
            .Take(count)
            .Select((x, i) => new LeaderboardEntry
            {
                Rank = i + 1,
                SteamId = x.Stats.SteamId,
                Name = x.Stats.Name,
                Value = x.Value,
                FormattedValue = FormatStatValue(x.Value, category)
            })
            .ToList();
        
        return entries;
    }
    
    private double GetStatValue(PlayerStats stats, LeaderboardCategory category) => category switch
    {
        LeaderboardCategory.DriverLevel => stats.PrestigeRank > 0 
            ? (stats.PrestigeRank * 1000) + stats.DriverLevel  // Sort prestiged players higher
            : stats.DriverLevel,
        LeaderboardCategory.TotalXP => stats.TotalXP,
        LeaderboardCategory.PrestigeRank => stats.PrestigeRank,
        LeaderboardCategory.TotalDistance => stats.TotalDistanceKm,
        LeaderboardCategory.TotalTime => stats.TotalTimeOnServerSeconds / 3600.0,
        LeaderboardCategory.RaceWins => stats.RaceWins,
        LeaderboardCategory.BattleWins => stats.BattleWins,
        LeaderboardCategory.WinRate => stats.WinRate * 100,
        LeaderboardCategory.TopSpeed => stats.TopSpeedKph,
        LeaderboardCategory.AverageSpeed => stats.AverageSpeedKph,
        LeaderboardCategory.CleanRaceRate => stats.CleanRaceRate * 100,
        LeaderboardCategory.LongestSession => stats.LongestSessionSeconds / 60.0,
        LeaderboardCategory.UniqueCars => stats.UniqueCarsUsed,
        _ => 0
    };
    
    private string FormatStatValue(double value, LeaderboardCategory category) => category switch
    {
        LeaderboardCategory.DriverLevel => FormatDriverLevel((int)value),
        LeaderboardCategory.TotalXP => $"{value:N0} XP",
        LeaderboardCategory.PrestigeRank => value > 0 ? $"P{value:F0}" : "None",
        LeaderboardCategory.TotalDistance => $"{value:N1} km",
        LeaderboardCategory.TotalTime => $"{value:N1} hrs",
        LeaderboardCategory.RaceWins or LeaderboardCategory.BattleWins => $"{value:F0} wins",
        LeaderboardCategory.WinRate or LeaderboardCategory.CleanRaceRate => $"{value:F1}%",
        LeaderboardCategory.TopSpeed or LeaderboardCategory.AverageSpeed => $"{value:F1} km/h",
        LeaderboardCategory.LongestSession => $"{value:F0} min",
        LeaderboardCategory.UniqueCars => $"{value:F0} cars",
        _ => value.ToString("N1")
    };
    
    /// <summary>
    /// Format driver level with prestige
    /// </summary>
    private string FormatDriverLevel(int combinedValue)
    {
        if (combinedValue >= 1000)
        {
            int prestige = combinedValue / 1000;
            int level = combinedValue % 1000;
            return $"P{prestige} Lv.{level}";
        }
        return $"Lv.{combinedValue}";
    }
    
    /// <summary>
    /// Format driver level display string
    /// </summary>
    public string GetDriverLevelDisplay(string steamId)
    {
        if (!_stats.TryGetValue(steamId, out var stats)) return "Lv.1";
        
        if (stats.PrestigeRank > 0)
            return $"P{stats.PrestigeRank} - {stats.DriverLevel}";
        return $"{stats.DriverLevel}";
    }
    
    /// <summary>
    /// Check if auto-save is needed
    /// </summary>
    private void CheckAutoSave()
    {
        if ((DateTime.UtcNow - _lastSave).TotalSeconds >= _config.SaveIntervalSeconds)
        {
            Save();
        }
    }
    
    /// <summary>
    /// Load stats from disk
    /// </summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(_config.DatabasePath)) return;
            
            string json = File.ReadAllText(_config.DatabasePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, PlayerStats>>(json);
            
            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    _stats[kvp.Key] = kvp.Value;
                }
                Log.Information("Loaded {Count} player stats from {Path}", _stats.Count, _config.DatabasePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load player stats from {Path}", _config.DatabasePath);
        }
    }
    
    /// <summary>
    /// Save stats to disk
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_config.DatabasePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                
                // Create backup
                if (_config.EnableBackups && File.Exists(_config.DatabasePath))
                {
                    string backupPath = _config.DatabasePath + $".backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                    File.Copy(_config.DatabasePath, backupPath, true);
                    
                    // Clean old backups
                    CleanOldBackups();
                }
                
                string json = JsonSerializer.Serialize(_stats, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_config.DatabasePath, json);
                
                _lastSave = DateTime.UtcNow;
                Log.Debug("Saved {Count} player stats to {Path}", _stats.Count, _config.DatabasePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save player stats to {Path}", _config.DatabasePath);
            }
        }
    }
    
    private void CleanOldBackups()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_config.DatabasePath);
            string filename = Path.GetFileName(_config.DatabasePath);
            
            if (string.IsNullOrEmpty(dir)) return;
            
            var backups = Directory.GetFiles(dir, filename + ".backup.*")
                .OrderByDescending(f => f)
                .Skip(_config.MaxBackupCount)
                .ToList();
            
            foreach (var backup in backups)
            {
                File.Delete(backup);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clean old backups");
        }
    }
}
