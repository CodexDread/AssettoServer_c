using System.Text.Json.Serialization;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Complete player statistics data
/// </summary>
public class PlayerStats
{
    // === Identity ===
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    // === Driver Level ===
    public int DriverLevel { get; set; } = 1;
    public long TotalXP { get; set; } = 0;
    public long XPToNextLevel { get; set; } = 1000;
    
    // === Prestige System ===
    /// <summary>
    /// Number of times player has prestiged (0 = never prestiged)
    /// </summary>
    public int PrestigeRank { get; set; } = 0;
    
    /// <summary>
    /// Highest level ever achieved (for car unlock purposes)
    /// </summary>
    public int HighestLevelAchieved { get; set; } = 1;
    
    /// <summary>
    /// Total times reached max level (999)
    /// </summary>
    public int TimesReachedMaxLevel { get; set; } = 0;
    
    /// <summary>
    /// Effective level for car unlocks (considers prestige)
    /// Prestiged players keep max level access
    /// </summary>
    public int EffectiveLevelForUnlocks => PrestigeRank > 0 ? 999 : DriverLevel;
    
    // === Session Stats ===
    public int TotalSessions { get; set; } = 0;
    public long TotalTimeOnServerSeconds { get; set; } = 0;
    public long TotalActiveTimeSeconds { get; set; } = 0;  // Time actually driving
    public long LongestSessionSeconds { get; set; } = 0;
    public double AverageSessionMinutes => TotalSessions > 0 
        ? (TotalTimeOnServerSeconds / 60.0) / TotalSessions : 0;
    
    // === Distance & Speed ===
    public double TotalDistanceMeters { get; set; } = 0;
    public double TotalDistanceKm => TotalDistanceMeters / 1000.0;
    public float TopSpeedKph { get; set; } = 0;
    public double TotalSpeedSamples { get; set; } = 0;  // For calculating average
    public double SpeedSampleCount { get; set; } = 0;
    public float AverageSpeedKph => SpeedSampleCount > 0 
        ? (float)(TotalSpeedSamples / SpeedSampleCount) : 0;
    public double HighSpeedDistanceMeters { get; set; } = 0;  // Distance at 200+ km/h
    
    // === Racing Stats ===
    public int RacesParticipated { get; set; } = 0;
    public int RaceWins { get; set; } = 0;
    public int RacePodiums { get; set; } = 0;  // Top 3 finishes
    public int RaceDNFs { get; set; } = 0;
    public int RacesCompleted => RacesParticipated - RaceDNFs;
    public double WinRate => RacesParticipated > 0 
        ? (double)RaceWins / RacesParticipated : 0;
    
    // === SP Battle Stats (integration with SPBattlePlugin) ===
    public int BattlesParticipated { get; set; } = 0;
    public int BattleWins { get; set; } = 0;
    public int BattleLosses { get; set; } = 0;
    public double BattleWinRate => BattlesParticipated > 0 
        ? (double)BattleWins / BattlesParticipated : 0;
    public int CurrentWinStreak { get; set; } = 0;
    public int BestWinStreak { get; set; } = 0;
    
    // === Collision Stats ===
    public int TotalCollisions { get; set; } = 0;
    public int CarCollisions { get; set; } = 0;
    public int WallCollisions { get; set; } = 0;
    public int CleanRaces { get; set; } = 0;  // Races with 0 collisions
    public double AverageCollisionsPerRace => RacesParticipated > 0 
        ? (double)TotalCollisions / RacesParticipated : 0;
    public double CleanRaceRate => RacesParticipated > 0 
        ? (double)CleanRaces / RacesParticipated : 0;
    
    // === Car Stats ===
    public Dictionary<string, CarUsageStats> CarStats { get; set; } = new();
    public string FavoriteCar { get; set; } = "";
    public double FavoriteCarDistanceKm { get; set; } = 0;
    public int UniqueCarsUsed => CarStats.Count;
    
    // === Records & Achievements ===
    public float PersonalBestLapTime { get; set; } = 0;  // Best lap in seconds
    public string PersonalBestLapTrack { get; set; } = "";
    public string PersonalBestLapCar { get; set; } = "";
    public double LongestDriveMeters { get; set; } = 0;  // Single session
    public int MostCollisionsInRace { get; set; } = 0;
    public float FastestOvertakeSpeedKph { get; set; } = 0;
    
    // === Milestones ===
    public List<string> AchievedMilestones { get; set; } = new();
    
    /// <summary>
    /// Update favorite car based on distance driven
    /// </summary>
    public void UpdateFavoriteCar()
    {
        if (CarStats.Count == 0) return;
        
        var favorite = CarStats.MaxBy(c => c.Value.DistanceMeters);
        if (favorite.Value != null)
        {
            FavoriteCar = favorite.Key;
            FavoriteCarDistanceKm = favorite.Value.DistanceMeters / 1000.0;
        }
    }
    
    /// <summary>
    /// Get or create car stats entry
    /// </summary>
    public CarUsageStats GetCarStats(string carModel)
    {
        if (!CarStats.TryGetValue(carModel, out var stats))
        {
            stats = new CarUsageStats { CarModel = carModel };
            CarStats[carModel] = stats;
        }
        return stats;
    }
}

/// <summary>
/// Per-car usage statistics
/// </summary>
public class CarUsageStats
{
    public string CarModel { get; set; } = "";
    public double DistanceMeters { get; set; } = 0;
    public long TimeUsedSeconds { get; set; } = 0;
    public float TopSpeedKph { get; set; } = 0;
    public int RacesUsed { get; set; } = 0;
    public int WinsWithCar { get; set; } = 0;
    public int CollisionsWithCar { get; set; } = 0;
    public DateTime FirstUsed { get; set; } = DateTime.UtcNow;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    
    public double DistanceKm => DistanceMeters / 1000.0;
    public double TimeUsedHours => TimeUsedSeconds / 3600.0;
}

/// <summary>
/// Current session tracking data (not persisted)
/// </summary>
public class SessionTracker
{
    public string SteamId { get; set; } = "";
    public DateTime SessionStart { get; set; } = DateTime.UtcNow;
    public string CurrentCar { get; set; } = "";
    
    // Position tracking
    public System.Numerics.Vector3 LastPosition { get; set; }
    public DateTime LastPositionTime { get; set; } = DateTime.UtcNow;
    public long LastUpdateTick { get; set; }
    
    // Session accumulators
    public double SessionDistanceMeters { get; set; } = 0;
    public long SessionActiveTimeMs { get; set; } = 0;
    public int SessionCollisions { get; set; } = 0;
    public float SessionTopSpeed { get; set; } = 0;
    public double SessionSpeedSamples { get; set; } = 0;
    public int SessionSpeedSampleCount { get; set; } = 0;
    public long SessionXPEarned { get; set; } = 0;
    
    // Race tracking
    public bool InRace { get; set; } = false;
    public int RaceCollisions { get; set; } = 0;
    public double RaceDistanceMeters { get; set; } = 0;
    
    /// <summary>
    /// Reset for new session
    /// </summary>
    public void Reset()
    {
        SessionStart = DateTime.UtcNow;
        SessionDistanceMeters = 0;
        SessionActiveTimeMs = 0;
        SessionCollisions = 0;
        SessionTopSpeed = 0;
        SessionSpeedSamples = 0;
        SessionSpeedSampleCount = 0;
        SessionXPEarned = 0;
        InRace = false;
        RaceCollisions = 0;
        RaceDistanceMeters = 0;
    }
    
    public float SessionAverageSpeed => SessionSpeedSampleCount > 0 
        ? (float)(SessionSpeedSamples / SessionSpeedSampleCount) : 0;
}

/// <summary>
/// Leaderboard entry for rankings
/// </summary>
public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public string FormattedValue { get; set; } = "";
}

/// <summary>
/// Categories for leaderboard rankings
/// </summary>
public enum LeaderboardCategory
{
    DriverLevel,
    TotalXP,
    PrestigeRank,
    TotalDistance,
    TotalTime,
    RaceWins,
    BattleWins,
    WinRate,
    TopSpeed,
    AverageSpeed,
    CleanRaceRate,
    LongestSession,
    UniqueCars
}

/// <summary>
/// Milestones that can be achieved
/// </summary>
public static class Milestones
{
    public const string Distance100Km = "100KM_CLUB";
    public const string Distance1000Km = "1000KM_CLUB";
    public const string Distance10000Km = "10000KM_CLUB";
    public const string Speed200Kph = "200_CLUB";
    public const string Speed300Kph = "300_CLUB";
    public const string Speed400Kph = "400_CLUB";
    public const string Wins10 = "10_WINS";
    public const string Wins50 = "50_WINS";
    public const string Wins100 = "100_WINS";
    public const string Hours10 = "10_HOURS";
    public const string Hours100 = "100_HOURS";
    public const string Hours500 = "500_HOURS";
    public const string Cars10 = "10_CARS";
    public const string Cars25 = "25_CARS";
    public const string CleanStreak5 = "CLEAN_5";
    public const string CleanStreak10 = "CLEAN_10";
    public const string WinStreak5 = "WIN_STREAK_5";
    public const string WinStreak10 = "WIN_STREAK_10";
    public const string Level25 = "LEVEL_25";
    public const string Level50 = "LEVEL_50";
    public const string Level100 = "LEVEL_100";
    public const string Level500 = "LEVEL_500";
    public const string Level999 = "LEVEL_999";
    public const string Prestige1 = "PRESTIGE_1";
    public const string Prestige5 = "PRESTIGE_5";
    public const string Prestige10 = "PRESTIGE_10";
    
    public static string GetDisplayName(string milestone) => milestone switch
    {
        Distance100Km => "100km Driven",
        Distance1000Km => "1,000km Driven",
        Distance10000Km => "10,000km Driven",
        Speed200Kph => "200 km/h Club",
        Speed300Kph => "300 km/h Club",
        Speed400Kph => "400 km/h Club",
        Wins10 => "10 Victories",
        Wins50 => "50 Victories",
        Wins100 => "Century of Wins",
        Hours10 => "10 Hours Driven",
        Hours100 => "100 Hours Driven",
        Hours500 => "500 Hours Driven",
        Cars10 => "Car Collector (10)",
        Cars25 => "Car Enthusiast (25)",
        CleanStreak5 => "5 Clean Races",
        CleanStreak10 => "10 Clean Races",
        WinStreak5 => "5 Win Streak",
        WinStreak10 => "10 Win Streak",
        Level25 => "Reached Level 25",
        Level50 => "Reached Level 50",
        Level100 => "Reached Level 100",
        Level500 => "Reached Level 500",
        Level999 => "MAX LEVEL!",
        Prestige1 => "First Prestige",
        Prestige5 => "Prestige 5",
        Prestige10 => "Prestige Master",
        _ => milestone
    };
}
