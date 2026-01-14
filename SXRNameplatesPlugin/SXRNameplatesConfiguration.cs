using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SXRNameplatesPlugin;

/// <summary>
/// Configuration for Nameplates Plugin
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRNameplatesConfiguration : IValidateConfiguration<SXRNameplatesConfigurationValidator>
{
    /// <summary>
    /// Enable the nameplates system
    /// </summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>
    /// Enable the Lua UI script
    /// </summary>
    public bool EnableLuaUI { get; init; } = true;
    
    /// <summary>
    /// Enable HTTP API for nameplate data
    /// </summary>
    public bool EnableHttpApi { get; init; } = true;
    
    // === VISIBILITY ===
    
    /// <summary>
    /// Maximum distance to show nameplates (meters)
    /// </summary>
    public float MaxVisibleDistance { get; init; } = 500f;
    
    /// <summary>
    /// Distance at which nameplates start fading (meters)
    /// </summary>
    public float FadeStartDistance { get; init; } = 300f;
    
    /// <summary>
    /// Minimum opacity for distant nameplates (0-1)
    /// </summary>
    public float MinOpacity { get; init; } = 0.3f;
    
    /// <summary>
    /// Height offset above car (meters)
    /// </summary>
    public float HeightOffset { get; init; } = 2.5f;
    
    // === DISPLAY OPTIONS ===
    
    /// <summary>
    /// Show driver level badge
    /// </summary>
    public bool ShowDriverLevel { get; init; } = true;
    
    /// <summary>
    /// Show car class/model
    /// </summary>
    public bool ShowCarClass { get; init; } = true;
    
    /// <summary>
    /// Show racer club tag
    /// </summary>
    public bool ShowClubTag { get; init; } = true;
    
    /// <summary>
    /// Show leaderboard rank
    /// </summary>
    public bool ShowRank { get; init; } = true;
    
    /// <summary>
    /// Show safety rating indicator
    /// </summary>
    public bool ShowSafetyRating { get; init; } = true;
    
    // === INTEGRATION ===
    
    /// <summary>
    /// Sync interval for nameplate data (ms)
    /// </summary>
    public int SyncIntervalMs { get; init; } = 5000;
    
    /// <summary>
    /// Use PlayerStatsPlugin for driver level
    /// </summary>
    public bool IntegratePlayerStats { get; init; } = true;
    
    // === CAR CLASS DEFINITIONS ===
    // Placeholder - will be replaced by CarClassPlugin
    
    /// <summary>
    /// Default car class mappings (car model prefix -> class)
    /// </summary>
    public Dictionary<string, string> CarClassMappings { get; init; } = new()
    {
        // Example mappings - customize for your server
        { "ks_ferrari", "S" },
        { "ks_lamborghini", "S" },
        { "ks_porsche_911", "A" },
        { "ks_nissan_gtr", "A" },
        { "ks_toyota_supra", "B" },
        { "ks_mazda_rx7", "B" },
        { "ks_honda_nsx", "B" },
        { "ks_bmw_m3", "C" },
        { "ks_audi_", "C" }
    };
    
    /// <summary>
    /// Default class for cars not in mappings
    /// </summary>
    public string DefaultCarClass { get; init; } = "D";
}

public class SXRNameplatesConfigurationValidator : IValidator<SXRNameplatesConfiguration>
{
    public bool Validate(SXRNameplatesConfiguration config, out string? errorMessage)
    {
        if (config.MaxVisibleDistance < config.FadeStartDistance)
        {
            errorMessage = "MaxVisibleDistance must be >= FadeStartDistance";
            return false;
        }
        
        if (config.HeightOffset < 0)
        {
            errorMessage = "HeightOffset must be positive";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
