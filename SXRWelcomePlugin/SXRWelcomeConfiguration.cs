using AssettoServer.Server.Configuration;
using JetBrains.Annotations;

namespace SXRWelcomePlugin;

/// <summary>
/// Configuration for SXR Welcome Plugin
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRWelcomeConfiguration : IValidateConfiguration<SXRWelcomeConfigurationValidator>
{
    /// <summary>
    /// Enable the welcome popup
    /// </summary>
    public bool Enabled { get; init; } = true;
    
    /// <summary>
    /// Enable Lua UI script
    /// </summary>
    public bool EnableLuaUI { get; init; } = true;
    
    /// <summary>
    /// Enable HTTP API
    /// </summary>
    public bool EnableHttpApi { get; init; } = true;
    
    // === SERVER INFO ===
    
    /// <summary>
    /// Server name displayed in welcome popup
    /// </summary>
    public string ServerName { get; init; } = "Shuto Expressway Revival";
    
    /// <summary>
    /// Server description/tagline
    /// </summary>
    public string ServerDescription { get; init; } = "Tokyo's Underground Racing Scene";
    
    /// <summary>
    /// Welcome message shown to players
    /// </summary>
    public string WelcomeMessage { get; init; } = "Welcome to the Shuto Expressway! Please read the rules below before driving.";
    
    // === RULES ===
    
    /// <summary>
    /// List of server rules
    /// </summary>
    public List<string> Rules { get; init; } = new()
    {
        "Respect all players - no harassment or toxic behavior",
        "No ramming or intentional crashing",
        "Use headlights at night and in tunnels",
        "Flash lights to challenge for SP Battle",
        "Hazard lights = accepting challenge",
        "Keep right lane for slower traffic",
        "No blocking or brake checking",
        "Report issues in Discord"
    };
    
    // === RESTRICTIONS WARNING ===
    
    /// <summary>
    /// Show car restriction warning if applicable
    /// </summary>
    public bool ShowRestrictionWarning { get; init; } = true;
    
    /// <summary>
    /// Warning message template (placeholders: {car}, {class}, {required}, {current}, {needed})
    /// </summary>
    public string RestrictionWarningTemplate { get; init; } = 
        "⚠️ Your selected car ({car}) requires Driver Level {required}. " +
        "You are currently Level {current} and need {needed} more levels. " +
        "Please select a different car from the list below.";
    
    /// <summary>
    /// Show available cars when restricted
    /// </summary>
    public bool ShowAvailableCars { get; init; } = true;
    
    /// <summary>
    /// Maximum cars to show in available list
    /// </summary>
    public int MaxAvailableCarsToShow { get; init; } = 10;
    
    // === TIMING ===
    
    /// <summary>
    /// Delay before showing popup (seconds) - allows game to load
    /// </summary>
    public float ShowDelaySeconds { get; init; } = 2.0f;
    
    /// <summary>
    /// Auto-dismiss popup after this many seconds (0 = never)
    /// </summary>
    public float AutoDismissSeconds { get; init; } = 0;
    
    /// <summary>
    /// Minimum time popup must be shown before dismiss (for restrictions)
    /// </summary>
    public float MinimumDisplaySeconds { get; init; } = 3.0f;
    
    // === DISPLAY ===
    
    /// <summary>
    /// Show driver level info
    /// </summary>
    public bool ShowDriverLevel { get; init; } = true;
    
    /// <summary>
    /// Show social/Discord links
    /// </summary>
    public bool ShowSocialLinks { get; init; } = true;
    
    /// <summary>
    /// Discord invite URL
    /// </summary>
    public string DiscordUrl { get; init; } = "";
    
    /// <summary>
    /// Website URL
    /// </summary>
    public string WebsiteUrl { get; init; } = "";
}

public class SXRWelcomeConfigurationValidator : IValidator<SXRWelcomeConfiguration>
{
    public bool Validate(SXRWelcomeConfiguration config, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(config.ServerName))
        {
            errorMessage = "ServerName cannot be empty";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
}
