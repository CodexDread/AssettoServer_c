using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace SPBattlePlugin;

/// <summary>
/// Chat commands for SP Battle Plugin
/// </summary>
[RequireConnectedPlayer]
public class SPBattleCommandModule : ACModuleBase
{
    private readonly SPBattlePlugin _plugin;
    private readonly LeaderboardService _leaderboard;
    private readonly SPBattleConfiguration _config;
    
    public SPBattleCommandModule(
        SPBattlePlugin plugin, 
        LeaderboardService leaderboard,
        SPBattleConfiguration config)
    {
        _plugin = plugin;
        _leaderboard = leaderboard;
        _config = config;
    }
    
    /// <summary>
    /// Challenge a specific player by name
    /// </summary>
    [Command("battle", "race", "challenge")]
    public void ChallengePlayer(ACTcpClient target)
    {
        _plugin.GetBattle(Client!.EntryCar).ChallengeCar(target.EntryCar);
    }
    
    /// <summary>
    /// Accept a pending challenge
    /// </summary>
    [Command("accept")]
    public async ValueTask AcceptChallenge()
    {
        await _plugin.GetBattle(Client!.EntryCar).AcceptChallengeAsync();
    }
    
    /// <summary>
    /// Show your current stats
    /// </summary>
    [Command("mystats", "spstats")]
    public void ShowMyStats()
    {
        string? steamId = Client!.Guid.ToString();
        if (string.IsNullOrEmpty(steamId))
        {
            Reply("Unable to retrieve your stats.");
            return;
        }
        
        var ranking = _leaderboard.GetPlayerRanking(steamId);
        if (ranking == null || ranking.Wins + ranking.Losses == 0)
        {
            Reply("You haven't completed any SP Battles yet.");
            return;
        }
        
        string rankText = ranking.Rank > 0 ? $"#{ranking.Rank}" : "Unranked";
        Reply($"[SP Battle Stats]\n" +
              $"Rating: {ranking.Rating} ({rankText})\n" +
              $"Record: {ranking.Wins}W - {ranking.Losses}L ({ranking.WinRate:P0})");
    }
    
    /// <summary>
    /// Show top players
    /// </summary>
    [Command("sptop", "leaderboard", "top")]
    public void ShowLeaderboard()
    {
        var top = _leaderboard.GetTopPlayers(5);
        
        if (top.Count == 0)
        {
            Reply("No ranked players yet. Win some battles!");
            return;
        }
        
        string message = "[SP Battle Top 5]\n";
        foreach (var entry in top)
        {
            message += $"#{entry.Rank} {entry.Name} - {entry.Rating} ({entry.Wins}W/{entry.Losses}L)\n";
        }
        
        Reply(message.TrimEnd());
    }
    
    /// <summary>
    /// Show SP Battle configuration info
    /// </summary>
    [Command("spinfo")]
    public void ShowInfo()
    {
        int driverLevel = _plugin.GetDriverLevel(Client!.EntryCar);
        float maxSP = _config.TotalSP + (driverLevel * _config.DriverLevelBonusSPPerLevel);
        
        Reply($"[SP Battle System]\n" +
              $"Your Driver Level: {driverLevel}\n" +
              $"Your Max SP: {maxSP:F0}\n" +
              $"Flash lights 3x to challenge!\n" +
              $"Commands: /battle <name>, /accept, /mystats, /sptop");
    }
}

/// <summary>
/// Admin commands for SP Battle Plugin
/// </summary>
[RequireAdmin]
public class SPBattleAdminCommandModule : ACModuleBase
{
    private readonly SPBattlePlugin _plugin;
    
    public SPBattleAdminCommandModule(SPBattlePlugin plugin)
    {
        _plugin = plugin;
    }
    
    /// <summary>
    /// Set a player's driver level (admin only)
    /// </summary>
    [Command("setdl", "setdriverlevel")]
    public void SetDriverLevel(ACTcpClient target, int level)
    {
        _plugin.SetDriverLevel(target.EntryCar, level);
        Reply($"Set {target.Name}'s driver level to {level}");
    }
}
