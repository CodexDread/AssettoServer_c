using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Chat commands for Player Stats
/// </summary>
[RequireConnectedPlayer]
public class SXRPlayerStatsCommandModule : ACModuleBase
{
    private readonly SXRPlayerStatsService _statsService;
    private readonly SXRPlayerStatsConfiguration _config;
    
    public SXRPlayerStatsCommandModule(
        SXRPlayerStatsService statsService,
        SXRPlayerStatsConfiguration config)
    {
        _statsService = statsService;
        _config = config;
    }
    
    /// <summary>
    /// Show your stats summary
    /// </summary>
    [Command("stats", "mystats", "me")]
    public void ShowStats()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        double hours = stats.TotalTimeOnServerSeconds / 3600.0;
        int totalWins = stats.RaceWins + stats.BattleWins;
        
        Reply($"[Your Stats - {stats.Name}]\n" +
              $"Driver Level: {stats.DriverLevel} ({_statsService.GetLevelProgress(stats) * 100:F0}%)\n" +
              $"Distance: {stats.TotalDistanceKm:N1} km\n" +
              $"Time: {hours:N1} hours ({stats.TotalSessions} sessions)\n" +
              $"Wins: {totalWins} (Race: {stats.RaceWins}, Battle: {stats.BattleWins})\n" +
              $"Top Speed: {stats.TopSpeedKph:F1} km/h");
    }
    
    /// <summary>
    /// Show driving stats
    /// </summary>
    [Command("driving", "drive")]
    public void ShowDrivingStats()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        Reply($"[Driving Stats]\n" +
              $"Total Distance: {stats.TotalDistanceKm:N1} km\n" +
              $"High Speed Distance: {stats.HighSpeedDistanceMeters / 1000:N1} km (200+ km/h)\n" +
              $"Top Speed: {stats.TopSpeedKph:F1} km/h\n" +
              $"Average Speed: {stats.AverageSpeedKph:F1} km/h\n" +
              $"Longest Drive: {stats.LongestDriveMeters / 1000:N1} km");
    }
    
    /// <summary>
    /// Show collision stats
    /// </summary>
    [Command("collisions", "crashes")]
    public void ShowCollisionStats()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        Reply($"[Collision Stats]\n" +
              $"Total Collisions: {stats.TotalCollisions}\n" +
              $"Car Collisions: {stats.CarCollisions}\n" +
              $"Wall Collisions: {stats.WallCollisions}\n" +
              $"Avg per Race: {stats.AverageCollisionsPerRace:F1}\n" +
              $"Clean Races: {stats.CleanRaces} ({stats.CleanRaceRate:P0})");
    }
    
    /// <summary>
    /// Show racing stats
    /// </summary>
    [Command("racing", "races")]
    public void ShowRacingStats()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        Reply($"[Racing Stats]\n" +
              $"Races: {stats.RacesParticipated} ({stats.RacesCompleted} completed)\n" +
              $"Wins: {stats.RaceWins} ({stats.WinRate:P0})\n" +
              $"Podiums: {stats.RacePodiums}\n" +
              $"DNFs: {stats.RaceDNFs}\n" +
              $"SP Battles: {stats.BattleWins}W / {stats.BattleLosses}L");
    }
    
    /// <summary>
    /// Show your favorite car
    /// </summary>
    [Command("mycar", "favorite")]
    public void ShowFavoriteCar()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        if (string.IsNullOrEmpty(stats.FavoriteCar))
        {
            Reply("No favorite car yet - drive more!");
            return;
        }
        
        var carStats = stats.CarStats[stats.FavoriteCar];
        
        Reply($"[Favorite Car: {stats.FavoriteCar}]\n" +
              $"Distance: {carStats.DistanceKm:N1} km\n" +
              $"Time: {carStats.TimeUsedHours:N1} hours\n" +
              $"Top Speed: {carStats.TopSpeedKph:F1} km/h\n" +
              $"Wins: {carStats.WinsWithCar}\n" +
              $"Cars Used: {stats.UniqueCarsUsed} total");
    }
    
    /// <summary>
    /// Show driver level and XP
    /// </summary>
    [Command("level", "xp", "dl")]
    public void ShowLevel()
    {
        string steamId = Client!.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        float progress = _statsService.GetLevelProgress(stats);
        long currentXP = stats.TotalXP;
        long needed = stats.XPToNextLevel;
        
        Reply($"[Driver Level]\n" +
              $"Level: {stats.DriverLevel} / {_config.MaxDriverLevel}\n" +
              $"XP: {currentXP:N0} / {needed:N0} ({progress:P0})\n" +
              $"Best Streak: {stats.BestWinStreak} wins\n" +
              $"Milestones: {stats.AchievedMilestones.Count}");
    }
    
    /// <summary>
    /// Show leaderboard top 5
    /// </summary>
    [Command("top", "leaderboard")]
    public void ShowLeaderboard([Remainder] string category = "driverlevel")
    {
        if (!Enum.TryParse<LeaderboardCategory>(category, true, out var cat))
        {
            Reply($"Unknown category. Valid: {string.Join(", ", Enum.GetNames<LeaderboardCategory>())}");
            return;
        }
        
        var top = _statsService.GetLeaderboard(cat, 5);
        
        string message = $"[Top 5 - {cat}]\n";
        foreach (var entry in top)
        {
            message += $"#{entry.Rank} {entry.Name} - {entry.FormattedValue}\n";
        }
        
        Reply(message.TrimEnd());
    }
    
    /// <summary>
    /// Show available stats commands
    /// </summary>
    [Command("statshelp")]
    public void ShowHelp()
    {
        Reply("[Stats Commands]\n" +
              "/stats - Overview\n" +
              "/level - Driver Level & XP\n" +
              "/driving - Distance & Speed\n" +
              "/racing - Race stats\n" +
              "/collisions - Crash stats\n" +
              "/mycar - Favorite car\n" +
              "/top <category> - Leaderboard");
    }
}

/// <summary>
/// Admin commands for Player Stats
/// </summary>
[RequireAdmin]
public class SXRPlayerStatsAdminCommandModule : ACModuleBase
{
    private readonly SXRPlayerStatsPlugin _plugin;
    private readonly SXRPlayerStatsService _statsService;
    
    public PlayerStatsAdminCommandModule(
        SXRPlayerStatsPlugin plugin,
        SXRPlayerStatsService statsService)
    {
        _plugin = plugin;
        _statsService = statsService;
    }
    
    /// <summary>
    /// Force save all stats
    /// </summary>
    [Command("savestats")]
    public void SaveStats()
    {
        _plugin.ForceSave();
        Reply("Player stats saved.");
    }
    
    /// <summary>
    /// View another player's stats
    /// </summary>
    [Command("viewstats")]
    public void ViewStats(ACTcpClient target)
    {
        string steamId = target.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        Reply($"[{stats.Name}'s Stats]\n" +
              $"Level: {stats.DriverLevel}, XP: {stats.TotalXP:N0}\n" +
              $"Distance: {stats.TotalDistanceKm:N1} km\n" +
              $"Wins: {stats.RaceWins + stats.BattleWins}\n" +
              $"Sessions: {stats.TotalSessions}");
    }
}
