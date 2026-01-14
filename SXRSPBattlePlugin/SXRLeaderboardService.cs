using System.Text.Json;
using AssettoServer.Server;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// Persistent leaderboard tracking for SP Battles
/// </summary>
public class SXRLeaderboardService
{
    private readonly SXRSPBattleConfiguration _config;
    private readonly string _dataPath;
    private readonly object _lock = new();
    private Dictionary<string, PlayerStats> _stats = new();
    
    public SXRLeaderboardService(SXRSPBattleConfiguration config)
    {
        _config = config;
        _dataPath = config.LeaderboardPath;
        
        // Ensure directory exists
        string? dir = Path.GetDirectoryName(_dataPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        Load();
    }
    
    /// <summary>
    /// Record a battle result
    /// </summary>
    public void RecordResult(string winnerSteamId, string winnerName, 
                            string loserSteamId, string loserName)
    {
        lock (_lock)
        {
            var winner = GetOrCreateStats(winnerSteamId, winnerName);
            var loser = GetOrCreateStats(loserSteamId, loserName);
            
            // Update counts
            winner.Wins++;
            winner.Name = winnerName; // Update name in case it changed
            loser.Losses++;
            loser.Name = loserName;
            
            // Calculate rating change (simplified Elo)
            int winnerRating = winner.Rating;
            int loserRating = loser.Rating;
            
            // Expected scores
            double expectedWinner = 1.0 / (1.0 + Math.Pow(10, (loserRating - winnerRating) / 400.0));
            double expectedLoser = 1.0 - expectedWinner;
            
            // K-factor based on games played
            int winnerK = winner.TotalBattles < 30 ? 40 : _config.WinRatingPoints;
            int loserK = loser.TotalBattles < 30 ? 40 : _config.LossRatingPoints;
            
            // Apply rating changes
            winner.Rating = (int)Math.Round(winnerRating + winnerK * (1 - expectedWinner));
            loser.Rating = (int)Math.Max(100, Math.Round(loserRating + loserK * (0 - expectedLoser)));
            
            // Update timestamps
            winner.LastBattle = DateTime.UtcNow;
            loser.LastBattle = DateTime.UtcNow;
            
            Save();
            
            Log.Information("Battle recorded: {Winner} ({WinnerRating}) defeated {Loser} ({LoserRating})",
                winnerName, winner.Rating, loserName, loser.Rating);
        }
    }
    
    /// <summary>
    /// Get top players
    /// </summary>
    public List<LeaderboardEntry> GetTopPlayers(int count = 10)
    {
        lock (_lock)
        {
            return _stats.Values
                .Where(s => s.TotalBattles >= 3) // Minimum battles to appear
                .OrderByDescending(s => s.Rating)
                .Take(count)
                .Select((s, i) => new LeaderboardEntry
                {
                    Rank = i + 1,
                    SteamId = s.SteamId,
                    Name = s.Name,
                    Rating = s.Rating,
                    Wins = s.Wins,
                    Losses = s.Losses
                })
                .ToList();
        }
    }
    
    /// <summary>
    /// Get a player's ranking info
    /// </summary>
    public PlayerRanking? GetPlayerRanking(string steamId)
    {
        lock (_lock)
        {
            if (!_stats.TryGetValue(steamId, out var stats))
                return null;
            
            // Calculate rank
            int rank = _stats.Values
                .Where(s => s.TotalBattles >= 3)
                .Count(s => s.Rating > stats.Rating) + 1;
            
            int totalRanked = _stats.Values.Count(s => s.TotalBattles >= 3);
            
            return new PlayerRanking
            {
                SteamId = steamId,
                Name = stats.Name,
                Rating = stats.Rating,
                Rank = stats.TotalBattles >= 3 ? rank : 0,
                TotalRanked = totalRanked,
                Wins = stats.Wins,
                Losses = stats.Losses,
                WinRate = stats.WinRate
            };
        }
    }
    
    /// <summary>
    /// Get detailed player stats
    /// </summary>
    public PlayerStats? GetPlayerStats(string steamId)
    {
        lock (_lock)
        {
            return _stats.TryGetValue(steamId, out var stats) ? stats : null;
        }
    }
    
    private PlayerStats GetOrCreateStats(string steamId, string name)
    {
        if (!_stats.TryGetValue(steamId, out var stats))
        {
            stats = new PlayerStats
            {
                SteamId = steamId,
                Name = name,
                Rating = _config.StartingRating,
                CreatedAt = DateTime.UtcNow
            };
            _stats[steamId] = stats;
        }
        return stats;
    }
    
    private void Load()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                string json = File.ReadAllText(_dataPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, PlayerStats>>(json);
                if (loaded != null)
                {
                    _stats = loaded;
                    Log.Information("Loaded {Count} player stats from {Path}", _stats.Count, _dataPath);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load leaderboard data from {Path}", _dataPath);
        }
    }
    
    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_stats, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save leaderboard data to {Path}", _dataPath);
        }
    }
}

/// <summary>
/// Player statistics storage
/// </summary>
public class PlayerStats
{
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Rating { get; set; } = 1000;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastBattle { get; set; }
    
    public int TotalBattles => Wins + Losses;
    public double WinRate => TotalBattles > 0 ? (double)Wins / TotalBattles : 0;
}

/// <summary>
/// Leaderboard display entry
/// </summary>
public class LeaderboardEntry
{
    public int Rank { get; set; }
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Rating { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}

/// <summary>
/// Player ranking info for individual lookup
/// </summary>
public class PlayerRanking
{
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Rating { get; set; }
    public int Rank { get; set; }
    public int TotalRanked { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
}

/// <summary>
/// HTTP API Controller for leaderboard
/// </summary>
[ApiController]
[Route("spbattle")]
public class LeaderboardController : ControllerBase
{
    private readonly SXRLeaderboardService _leaderboard;
    
    public LeaderboardController(SXRLeaderboardService leaderboard)
    {
        _leaderboard = leaderboard;
    }
    
    [HttpGet("leaderboard")]
    public ActionResult<List<LeaderboardEntry>> GetLeaderboard([FromQuery] int count = 10)
    {
        return _leaderboard.GetTopPlayers(Math.Min(count, 100));
    }
    
    [HttpGet("leaderboard/{steamId}")]
    public ActionResult<PlayerRanking> GetPlayerRanking(string steamId)
    {
        var ranking = _leaderboard.GetPlayerRanking(steamId);
        if (ranking == null)
            return NotFound();
        return ranking;
    }
    
    [HttpGet("stats/{steamId}")]
    public ActionResult<PlayerStats> GetPlayerStats(string steamId)
    {
        var stats = _leaderboard.GetPlayerStats(steamId);
        if (stats == null)
            return NotFound();
        return stats;
    }
}
