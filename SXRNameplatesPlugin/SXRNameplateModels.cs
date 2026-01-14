namespace SXRNameplatesPlugin;

/// <summary>
/// Nameplate data for a single player
/// </summary>
public class SXRNameplateData
{
    public int SessionId { get; set; }
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public int DriverLevel { get; set; } = 1;
    public int PrestigeRank { get; set; } = 0;
    public string CarModel { get; set; } = "";
    public string CarClass { get; set; } = "D";
    public string SafetyRating { get; set; } = "C";
    public string ClubTag { get; set; } = "";
    public int LeaderboardRank { get; set; } = 0;
    public bool IsAdmin { get; set; } = false;
    
    /// <summary>
    /// Formatted level display string [P# - DL] or just [DL]
    /// </summary>
    public string LevelDisplay => PrestigeRank > 0 
        ? $"P{PrestigeRank} - {DriverLevel}" 
        : DriverLevel.ToString();
}

/// <summary>
/// Full nameplate sync packet for all players
/// </summary>
public class SXRNameplateSyncData
{
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public List<SXRNameplateData> Players { get; set; } = new();
    public SXRNameplateDisplayConfig DisplayConfig { get; set; } = new();
}

/// <summary>
/// Display configuration sent to clients
/// </summary>
public class SXRNameplateDisplayConfig
{
    public bool ShowDriverLevel { get; set; } = true;
    public bool ShowCarClass { get; set; } = true;
    public bool ShowClubTag { get; set; } = true;
    public bool ShowRank { get; set; } = true;
    public bool ShowSafetyRating { get; set; } = true;
    public float MaxDistance { get; set; } = 500f;
    public float FadeDistance { get; set; } = 300f;
    public float HeightOffset { get; set; } = 2.5f;
}

/// <summary>
/// Safety rating grades
/// </summary>
public static class SafetyRating
{
    public const string S = "S";
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string F = "F";
    
    /// <summary>
    /// Get color for safety rating (RGB hex)
    /// </summary>
    public static string GetColor(string rating) => rating switch
    {
        "S" => "#FFD700", // Gold
        "A" => "#00FF00", // Green
        "B" => "#00BFFF", // Deep Sky Blue
        "C" => "#FFFF00", // Yellow
        "D" => "#FFA500", // Orange
        "F" => "#FF0000", // Red
        _ => "#FFFFFF"    // White
    };
}

/// <summary>
/// Car class grades
/// </summary>
public static class CarClass
{
    public const string S = "S";
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string E = "E";
    
    /// <summary>
    /// Get color for car class (RGB hex)
    /// </summary>
    public static string GetColor(string carClass) => carClass switch
    {
        "S" => "#9932CC", // Purple
        "A" => "#FF4444", // Red
        "B" => "#FFA500", // Orange
        "C" => "#FFFF00", // Yellow
        "D" => "#00FF00", // Green
        "E" => "#00BFFF", // Blue
        _ => "#AAAAAA"    // Gray
    };
}

/// <summary>
/// Prestige rank colors - cycle through special colors
/// </summary>
public static class PrestigeColors
{
    /// <summary>
    /// Get color for prestige rank (RGB hex)
    /// Colors cycle and get more vibrant at higher prestiges
    /// Note: P50+ uses rainbow gradient (animated in Lua)
    /// </summary>
    public static string GetColor(int prestigeRank) => prestigeRank switch
    {
        0 => "#FFFFFF",      // White (no prestige)
        1 => "#FFD700",      // Gold
        2 => "#FF6B6B",      // Coral Red
        3 => "#9B59B6",      // Purple
        4 => "#3498DB",      // Blue
        5 => "#2ECC71",      // Emerald
        6 => "#E74C3C",      // Red
        7 => "#F39C12",      // Orange
        8 => "#1ABC9C",      // Turquoise
        9 => "#E91E63",      // Pink
        10 => "#FF1493",     // Deep Pink (P10 special)
        _ when prestigeRank > 10 && prestigeRank < 20 => "#FF00FF", // Magenta (P11-19)
        _ when prestigeRank >= 20 && prestigeRank < 50 => "#00FFFF", // Aqua (P20-49)
        _ when prestigeRank >= 50 => "#RAINBOW",  // Rainbow gradient (handled in Lua)
        _ => "#FFD700"       // Default gold
    };
    
    /// <summary>
    /// Get display name for prestige tier
    /// </summary>
    public static string GetTierName(int prestigeRank) => prestigeRank switch
    {
        0 => "",
        1 => "Bronze",
        2 => "Silver", 
        3 => "Gold",
        4 => "Platinum",
        5 => "Diamond",
        >= 6 and < 10 => "Master",
        >= 10 and < 20 => "Grandmaster",
        >= 20 and < 50 => "Legend",
        >= 50 => "Mythic",
        _ => ""
    };
}
