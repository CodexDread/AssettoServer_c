namespace SXRWelcomePlugin;

/// <summary>
/// Complete welcome data sent to client
/// </summary>
public class WelcomeData
{
    // Server info
    public string ServerName { get; set; } = "";
    public string ServerDescription { get; set; } = "";
    public string WelcomeMessage { get; set; } = "";
    
    // Rules
    public List<string> Rules { get; set; } = new();
    
    // Player info
    public string PlayerName { get; set; } = "";
    public string SteamId { get; set; } = "";
    public int DriverLevel { get; set; } = 1;
    public int PrestigeRank { get; set; } = 0;
    public int DriverXp { get; set; } = 0;
    public int XpToNextLevel { get; set; } = 100;
    
    // Formatted display string
    public string DriverLevelDisplay => PrestigeRank > 0 
        ? $"P{PrestigeRank} - {DriverLevel}" 
        : DriverLevel.ToString();
    
    // Car restriction
    public bool HasRestriction { get; set; }
    public string? RestrictionWarning { get; set; }
    public string? CurrentCar { get; set; }
    public string? CurrentCarClass { get; set; }
    public int? RequiredLevel { get; set; }
    public int? LevelsNeeded { get; set; }
    public string? EnforcementMode { get; set; }
    public int? GracePeriodSeconds { get; set; }
    
    // Available cars
    public List<AvailableCarInfo> AvailableCars { get; set; } = new();
    
    // Social links
    public string? DiscordUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    
    // Timing
    public float ShowDelaySeconds { get; set; }
    public float AutoDismissSeconds { get; set; }
    public float MinimumDisplaySeconds { get; set; }
}

/// <summary>
/// Available car info for welcome popup
/// </summary>
public class AvailableCarInfo
{
    public string Model { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CarClass { get; set; } = "";
    public int RequiredLevel { get; set; }
}

/// <summary>
/// Request to get welcome data for a specific player
/// </summary>
public class WelcomeDataRequest
{
    public string SteamId { get; set; } = "";
}
