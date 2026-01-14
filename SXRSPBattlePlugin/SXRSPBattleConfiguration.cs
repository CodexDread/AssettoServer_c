using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// Configuration for SP Battle Plugin - TXR-style spirit point racing battles
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRSPBattleConfiguration : IValidateConfiguration<SXRSPBattleConfigurationValidator>
{
    /// <summary>
    /// Base SP (Spirit Points) pool for all players
    /// </summary>
    public float TotalSP { get; init; } = 100f;
    
    /// <summary>
    /// Bonus SP added per Driver Level (DL)
    /// Total SP = TotalSP + (DriverLevel * DriverLevelBonusSPPerLevel)
    /// </summary>
    public float DriverLevelBonusSPPerLevel { get; init; } = 5f;
    
    /// <summary>
    /// Maximum driver level for SP calculation
    /// </summary>
    public int MaxDriverLevel { get; init; } = 50;
    
    /// <summary>
    /// Distance thresholds for SP drain rates (in meters)
    /// Each threshold defines the START of a drain zone
    /// Example: [10, 25, 50, 100] means:
    ///   0-10m: No drain (too close)
    ///   10-25m: Drain rate 0
    ///   25-50m: Drain rate 1
    ///   50-100m: Drain rate 2
    ///   100m+: Drain rate 3 (max drain)
    /// </summary>
    public float[] FollowDistanceThresholds { get; init; } = { 10f, 25f, 50f, 100f, 200f };
    
    /// <summary>
    /// SP drain rates per second for each distance bracket
    /// Must have one more entry than FollowDistanceThresholds
    /// Index 0 = within first threshold (close/drafting - no drain)
    /// Last index = beyond last threshold (max drain)
    /// </summary>
    public float[] DrainRatesPerSecond { get; init; } = { 0f, 2f, 5f, 10f, 20f, 30f };
    
    /// <summary>
    /// SP penalty applied on collision with opponent
    /// </summary>
    public float CollisionSPPenalty { get; init; } = 10f;
    
    /// <summary>
    /// SP penalty for wall/barrier collisions during battle
    /// </summary>
    public float WallCollisionSPPenalty { get; init; } = 5f;
    
    /// <summary>
    /// SP bonus when overtaking the opponent
    /// </summary>
    public float OvertakeSPBonus { get; init; } = 5f;
    
    /// <summary>
    /// SP bonus for maintaining lead per second
    /// </summary>
    public float LeadBonusPerSecond { get; init; } = 0.5f;
    
    /// <summary>
    /// Minimum speed (km/h) required for battle to continue
    /// If both cars drop below this, battle may be cancelled
    /// </summary>
    public float MinBattleSpeedKph { get; init; } = 30f;
    
    /// <summary>
    /// Maximum battle duration in seconds (0 = unlimited)
    /// </summary>
    public float MaxBattleDurationSeconds { get; init; } = 300f;
    
    /// <summary>
    /// Time in seconds to accept a challenge
    /// </summary>
    public float ChallengeTimeoutSeconds { get; init; } = 10f;
    
    /// <summary>
    /// Maximum distance to challenge another car (meters)
    /// </summary>
    public float ChallengeMaxDistance { get; init; } = 30f;
    
    /// <summary>
    /// Distance required to line up before battle starts (meters)
    /// </summary>
    public float LineUpDistance { get; init; } = 10f;
    
    /// <summary>
    /// Time to line up before battle is cancelled (seconds)
    /// </summary>
    public float LineUpTimeoutSeconds { get; init; } = 15f;
    
    /// <summary>
    /// Countdown duration after lineup (seconds)
    /// </summary>
    public int CountdownSeconds { get; init; } = 3;
    
    /// <summary>
    /// Cooldown between challenges from the same player (seconds)
    /// </summary>
    public float ChallengeCooldownSeconds { get; init; } = 20f;
    
    /// <summary>
    /// Distance at which the battle is automatically ended (meters)
    /// </summary>
    public float BattleSeparationDistance { get; init; } = 500f;
    
    /// <summary>
    /// Time without overtake before battle ends (seconds)
    /// </summary>
    public float NoOvertakeTimeoutSeconds { get; init; } = 60f;
    
    /// <summary>
    /// Enable leaderboard tracking
    /// </summary>
    public bool EnableLeaderboard { get; init; } = true;
    
    /// <summary>
    /// Path to leaderboard data file
    /// </summary>
    public string LeaderboardPath { get; init; } = "cfg/plugins/SXRSPBattlePlugin/leaderboard.json";
    
    /// <summary>
    /// Enable in-game Lua UI
    /// </summary>
    public bool EnableLuaUI { get; init; } = true;
    
    /// <summary>
    /// Broadcast battle results to all players
    /// </summary>
    public bool BroadcastResults { get; init; } = true;
    
    /// <summary>
    /// Rating points gained for a win (Elo-style)
    /// </summary>
    public int WinRatingPoints { get; init; } = 25;
    
    /// <summary>
    /// Rating points lost for a loss
    /// </summary>
    public int LossRatingPoints { get; init; } = 20;
    
    /// <summary>
    /// Starting rating for new players
    /// </summary>
    public int StartingRating { get; init; } = 1000;
}

public class SXRSPBattleConfigurationValidator : IValidator<SXRSPBattleConfiguration>
{
    public bool Validate(SXRSPBattleConfiguration config, out string? errorMessage)
    {
        if (config.TotalSP <= 0)
        {
            errorMessage = "TotalSP must be greater than 0";
            return false;
        }
        
        if (config.FollowDistanceThresholds.Length == 0)
        {
            errorMessage = "FollowDistanceThresholds must have at least one entry";
            return false;
        }
        
        if (config.DrainRatesPerSecond.Length != config.FollowDistanceThresholds.Length + 1)
        {
            errorMessage = "DrainRatesPerSecond must have exactly one more entry than FollowDistanceThresholds";
            return false;
        }
        
        // Verify thresholds are in ascending order
        for (int i = 1; i < config.FollowDistanceThresholds.Length; i++)
        {
            if (config.FollowDistanceThresholds[i] <= config.FollowDistanceThresholds[i - 1])
            {
                errorMessage = "FollowDistanceThresholds must be in ascending order";
                return false;
            }
        }
        
        errorMessage = null;
        return true;
    }
}
