using Microsoft.AspNetCore.Mvc;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// HTTP API Controller for Player Stats
/// </summary>
[ApiController]
[Route("playerstats")]
public class SXRPlayerStatsController : ControllerBase
{
    private readonly SXRPlayerStatsService _statsService;
    
    public SXRPlayerStatsController(SXRPlayerStatsService statsService)
    {
        _statsService = statsService;
    }
    
    /// <summary>
    /// Get full stats for a player
    /// </summary>
    [HttpGet("{steamId}")]
    public ActionResult<PlayerStats> GetPlayerStats(string steamId)
    {
        var stats = _statsService.GetStats(steamId);
        if (stats == null || string.IsNullOrEmpty(stats.Name))
            return NotFound();
        return stats;
    }
    
    /// <summary>
    /// Get player summary (lighter payload for UI)
    /// </summary>
    [HttpGet("{steamId}/summary")]
    public ActionResult<PlayerStatsSummary> GetPlayerSummary(string steamId)
    {
        var stats = _statsService.GetStats(steamId);
        if (stats == null || string.IsNullOrEmpty(stats.Name))
            return NotFound();
        
        return new PlayerStatsSummary
        {
            SteamId = stats.SteamId,
            Name = stats.Name,
            DriverLevel = stats.DriverLevel,
            TotalXP = stats.TotalXP,
            XPToNextLevel = stats.XPToNextLevel,
            LevelProgress = _statsService.GetLevelProgress(stats),
            TotalDistanceKm = stats.TotalDistanceKm,
            TotalTimeHours = stats.TotalTimeOnServerSeconds / 3600.0,
            RaceWins = stats.RaceWins,
            BattleWins = stats.BattleWins,
            TopSpeedKph = stats.TopSpeedKph,
            AverageSpeedKph = stats.AverageSpeedKph,
            FavoriteCar = stats.FavoriteCar,
            TotalCollisions = stats.TotalCollisions,
            AverageCollisionsPerRace = stats.AverageCollisionsPerRace,
            CleanRaceRate = stats.CleanRaceRate,
            TotalSessions = stats.TotalSessions,
            UniqueCarsUsed = stats.UniqueCarsUsed,
            MilestoneCount = stats.AchievedMilestones.Count
        };
    }
    
    /// <summary>
    /// Get player's car stats
    /// </summary>
    [HttpGet("{steamId}/cars")]
    public ActionResult<Dictionary<string, CarUsageStats>> GetCarStats(string steamId)
    {
        var stats = _statsService.GetStats(steamId);
        if (stats == null)
            return NotFound();
        return stats.CarStats;
    }
    
    /// <summary>
    /// Get player's milestones
    /// </summary>
    [HttpGet("{steamId}/milestones")]
    public ActionResult<List<MilestoneInfo>> GetMilestones(string steamId)
    {
        var stats = _statsService.GetStats(steamId);
        if (stats == null)
            return NotFound();
        
        return stats.AchievedMilestones
            .Select(m => new MilestoneInfo 
            { 
                Id = m, 
                Name = Milestones.GetDisplayName(m) 
            })
            .ToList();
    }
    
    /// <summary>
    /// Get leaderboard by category
    /// </summary>
    [HttpGet("leaderboard/{category}")]
    public ActionResult<List<LeaderboardEntry>> GetLeaderboard(
        string category, 
        [FromQuery] int count = 10)
    {
        if (!Enum.TryParse<LeaderboardCategory>(category, true, out var cat))
            return BadRequest($"Invalid category: {category}");
        
        return _statsService.GetLeaderboard(cat, Math.Min(count, 100));
    }
    
    /// <summary>
    /// Get available leaderboard categories
    /// </summary>
    [HttpGet("leaderboard/categories")]
    public ActionResult<List<string>> GetCategories()
    {
        return Enum.GetNames<LeaderboardCategory>().ToList();
    }
    
    /// <summary>
    /// Get top N players by category
    /// </summary>
    [HttpGet("top/{category}/{count}")]
    public ActionResult<List<LeaderboardEntry>> GetTopPlayers(string category, int count)
    {
        if (!Enum.TryParse<LeaderboardCategory>(category, true, out var cat))
            return BadRequest($"Invalid category: {category}");
        
        return _statsService.GetLeaderboard(cat, Math.Clamp(count, 1, 100));
    }
}

/// <summary>
/// Lightweight stats summary for UI
/// </summary>
public class SXRPlayerStatsSummary
{
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public int DriverLevel { get; set; }
    public long TotalXP { get; set; }
    public long XPToNextLevel { get; set; }
    public float LevelProgress { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalTimeHours { get; set; }
    public int RaceWins { get; set; }
    public int BattleWins { get; set; }
    public float TopSpeedKph { get; set; }
    public float AverageSpeedKph { get; set; }
    public string FavoriteCar { get; set; } = "";
    public int TotalCollisions { get; set; }
    public double AverageCollisionsPerRace { get; set; }
    public double CleanRaceRate { get; set; }
    public int TotalSessions { get; set; }
    public int UniqueCarsUsed { get; set; }
    public int MilestoneCount { get; set; }
}

/// <summary>
/// Milestone display info
/// </summary>
public class MilestoneInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
