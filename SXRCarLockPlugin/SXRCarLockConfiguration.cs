using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SXRCarLockPlugin;

/// <summary>
/// Configuration for SXR Car Lock Plugin
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRCarLockConfiguration : IValidateConfiguration<SXRCarLockConfigurationValidator>
{
    /// <summary>
    /// Enable the car lock system
    /// </summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>
    /// Enable HTTP API
    /// </summary>
    public bool EnableHttpApi { get; init; } = true;
    
    // === ENFORCEMENT ===
    
    /// <summary>
    /// Enforcement mode: Spectate, Kick, or Warning
    /// </summary>
    public EnforcementMode Mode { get; init; } = EnforcementMode.Spectate;
    
    /// <summary>
    /// Grace period in seconds before enforcing (allows welcome popup to show)
    /// </summary>
    public int GracePeriodSeconds { get; init; } = 10;
    
    /// <summary>
    /// Message shown when player is kicked for not meeting requirements
    /// </summary>
    public string KickMessage { get; init; } = "Your driver level is too low for this vehicle. Please choose a different car.";
    
    /// <summary>
    /// Allow admins to bypass restrictions
    /// </summary>
    public bool AdminsBypass { get; init; } = true;
    
    // === CLASS REQUIREMENTS ===
    // Minimum driver level required for each class
    
    /// <summary>
    /// Minimum level for S-Class (Supercars)
    /// </summary>
    public int SClassMinLevel { get; init; } = 50;
    
    /// <summary>
    /// Minimum level for A-Class (Sports)
    /// </summary>
    public int AClassMinLevel { get; init; } = 30;
    
    /// <summary>
    /// Minimum level for B-Class (Tuners)
    /// </summary>
    public int BClassMinLevel { get; init; } = 15;
    
    /// <summary>
    /// Minimum level for C-Class (Street)
    /// </summary>
    public int CClassMinLevel { get; init; } = 5;
    
    /// <summary>
    /// Minimum level for D-Class (Starter)
    /// </summary>
    public int DClassMinLevel { get; init; } = 1;
    
    /// <summary>
    /// Minimum level for E-Class (Entry/Kei)
    /// </summary>
    public int EClassMinLevel { get; init; } = 1;
    
    // === CAR CLASS MAPPINGS ===
    
    /// <summary>
    /// Path to JSON file containing car class mappings (relative to plugin directory)
    /// </summary>
    public string CarClassesJsonFile { get; init; } = "car_classes.json";
    
    /// <summary>
    /// Default class for cars not in mappings
    /// </summary>
    public string DefaultCarClass { get; init; } = "D";
    
    /// <summary>
    /// Auto-reload JSON file when it changes
    /// </summary>
    public bool AutoReloadJson { get; init; } = true;
}

public enum EnforcementMode
{
    /// <summary>
    /// Move player to spectator mode
    /// </summary>
    Spectate,
    
    /// <summary>
    /// Kick player from server
    /// </summary>
    Kick,
    
    /// <summary>
    /// Just warn via welcome popup, no enforcement
    /// </summary>
    Warning
}

public class SXRCarLockConfigurationValidator : IValidator<SXRCarLockConfiguration>
{
    public bool Validate(SXRCarLockConfiguration config, out string? errorMessage)
    {
        if (config.GracePeriodSeconds < 0)
        {
            errorMessage = "GracePeriodSeconds must be >= 0";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
