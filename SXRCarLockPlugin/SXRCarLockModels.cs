namespace SXRCarLockPlugin;

/// <summary>
/// Root object for car_classes.json file
/// </summary>
public class CarClassData
{
    public string Version { get; set; } = "1.0";
    public string Description { get; set; } = "";
    public List<CarClassEntry> Cars { get; set; } = new();
    public List<string> AlwaysAllowed { get; set; } = new();
    public List<string> AlwaysBlocked { get; set; } = new();
}

/// <summary>
/// Individual car entry in the JSON file
/// </summary>
public class CarClassEntry
{
    /// <summary>
    /// Car model name (or prefix/pattern based on MatchMode)
    /// </summary>
    public string Model { get; set; } = "";
    
    /// <summary>
    /// Class letter (S, A, B, C, D, E)
    /// </summary>
    public string Class { get; set; } = "D";
    
    /// <summary>
    /// Human-readable display name
    /// </summary>
    public string DisplayName { get; set; } = "";
    
    /// <summary>
    /// How to match the model: "exact", "prefix", "contains" (default: prefix)
    /// </summary>
    public string? MatchMode { get; set; }
    
    /// <summary>
    /// Optional notes about this car
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Car class definition with requirements
/// </summary>
public class CarClassDefinition
{
    public string ClassName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int MinLevel { get; set; }
    public string Color { get; set; } = "#FFFFFF";
    public string Description { get; set; } = "";
}

/// <summary>
/// Result of checking if a player can drive their car
/// </summary>
public class CarLockCheckResult
{
    public int SessionId { get; set; }
    public string SteamId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string CarModel { get; set; } = "";
    public string CarClass { get; set; } = "";
    public int RequiredLevel { get; set; }
    public int PlayerLevel { get; set; }
    public int PrestigeRank { get; set; }
    public int EffectiveLevel { get; set; }
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = "";
    public bool IsBypassed { get; set; }
}

/// <summary>
/// Information about a car and its requirements
/// </summary>
public class CarInfo
{
    public string Model { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CarClass { get; set; } = "";
    public int RequiredLevel { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// List of available cars for a player
/// </summary>
public class AvailableCarsResponse
{
    public string SteamId { get; set; } = "";
    public int PlayerLevel { get; set; }
    public List<CarInfo> AvailableCars { get; set; } = new();
    public List<CarInfo> LockedCars { get; set; } = new();
    public Dictionary<string, int> ClassRequirements { get; set; } = new();
}

/// <summary>
/// Class requirements response
/// </summary>
public class ClassRequirementsResponse
{
    public List<CarClassDefinition> Classes { get; set; } = new();
}

/// <summary>
/// Data sent to welcome plugin about restrictions
/// </summary>
public class RestrictionData
{
    public bool HasRestriction { get; set; }
    public string CurrentCar { get; set; } = "";
    public string CurrentCarClass { get; set; } = "";
    public int RequiredLevel { get; set; }
    public int PlayerLevel { get; set; }
    public int LevelsNeeded { get; set; }
    public List<CarInfo> AvailableCars { get; set; } = new();
    public string EnforcementMode { get; set; } = "";
    public int GracePeriodSeconds { get; set; }
}
