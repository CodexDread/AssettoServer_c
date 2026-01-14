using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Configuration for Player Stats Plugin
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRPlayerStatsConfiguration : IValidateConfiguration<SXRPlayerStatsConfigurationValidator>
{
    /// <summary>
    /// Path to player stats database file
    /// </summary>
    public string DatabasePath { get; init; } = "cfg/plugins/SXRPlayerStatsPlugin/playerstats.json";
    
    /// <summary>
    /// How often to save stats to disk (seconds)
    /// </summary>
    public int SaveIntervalSeconds { get; init; } = 60;
    
    /// <summary>
    /// Create backup before saving
    /// </summary>
    public bool EnableBackups { get; init; } = true;
    
    /// <summary>
    /// Number of backup files to keep
    /// </summary>
    public int MaxBackupCount { get; init; } = 5;
    
    /// <summary>
    /// Enable the in-game Lua UI
    /// </summary>
    public bool EnableLuaUI { get; init; } = true;
    
    /// <summary>
    /// Enable HTTP API endpoints
    /// </summary>
    public bool EnableHttpApi { get; init; } = true;
    
    /// <summary>
    /// Minimum session time (seconds) to count as a valid session
    /// </summary>
    public int MinSessionTimeSeconds { get; init; } = 60;
    
    /// <summary>
    /// Minimum distance (meters) to count a race as participated
    /// </summary>
    public float MinRaceDistanceMeters { get; init; } = 500;
    
    // === Driver Level System ===
    
    /// <summary>
    /// Enable driver level progression system
    /// </summary>
    public bool EnableDriverLevel { get; init; } = true;
    
    /// <summary>
    /// Starting driver level
    /// </summary>
    public int StartingDriverLevel { get; init; } = 1;
    
    /// <summary>
    /// Maximum driver level (prestige triggers at this level)
    /// </summary>
    public int MaxDriverLevel { get; init; } = 999;
    
    /// <summary>
    /// Base XP required for level 2
    /// </summary>
    public int BaseXPPerLevel { get; init; } = 1000;
    
    /// <summary>
    /// XP scaling factor per level (exponential)
    /// Level N requires: BaseXP * (XPScalingFactor ^ (N-1))
    /// </summary>
    public float XPScalingFactor { get; init; } = 1.15f;
    
    // === XP Rewards ===
    
    /// <summary>
    /// XP per kilometer driven
    /// </summary>
    public float XPPerKilometer { get; init; } = 10f;
    
    /// <summary>
    /// XP per minute on server (active driving)
    /// </summary>
    public float XPPerMinuteActive { get; init; } = 5f;
    
    /// <summary>
    /// XP for winning a race/battle
    /// </summary>
    public int XPForWin { get; init; } = 100;
    
    /// <summary>
    /// XP for completing a race (not DNF)
    /// </summary>
    public int XPForRaceComplete { get; init; } = 25;
    
    /// <summary>
    /// XP penalty for collision (per collision)
    /// </summary>
    public int XPPenaltyPerCollision { get; init; } = 2;
    
    /// <summary>
    /// Bonus XP multiplier for clean race (no collisions)
    /// </summary>
    public float CleanRaceXPMultiplier { get; init; } = 3.5f;
    
    // === Tracking Settings ===
    
    /// <summary>
    /// Update interval for distance/speed tracking (ms)
    /// </summary>
    public int TrackingUpdateIntervalMs { get; init; } = 500;
    
    /// <summary>
    /// Minimum speed (km/h) to count as "active driving"
    /// </summary>
    public float MinActiveSpeedKph { get; init; } = 50f;
    
    /// <summary>
    /// Speed threshold for "high speed" tracking (km/h)
    /// </summary>
    public float HighSpeedThresholdKph { get; init; } = 200f;
}

public class SXRPlayerStatsConfigurationValidator : IValidator<SXRPlayerStatsConfiguration>
{
    public bool Validate(SXRPlayerStatsConfiguration config, out string? errorMessage)
    {
        if (config.SaveIntervalSeconds < 10)
        {
            errorMessage = "SaveIntervalSeconds must be at least 10";
            return false;
        }
        
        if (config.MaxDriverLevel < 1)
        {
            errorMessage = "MaxDriverLevel must be at least 1";
            return false;
        }
        
        if (config.XPScalingFactor < 1.0f)
        {
            errorMessage = "XPScalingFactor must be at least 1.0";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
