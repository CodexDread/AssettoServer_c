using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// SP Battle Plugin - TXR-style spirit point racing battles
/// </summary>
public class SXRSPBattlePlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRSPBattleConfiguration _config;
    private readonly Func<EntryCar, SXREntryCarBattle> _entryCarBattleFactory;
    private readonly SXRLeaderboardService _leaderboard;
    private readonly CSPServerScriptProvider _scriptProvider;
    private readonly Dictionary<int, SXREntryCarBattle> _instances = new();
    
    // Network event types for Lua UI
    private const byte EventType_None = 0;
    private const byte EventType_Challenge = 1;
    private const byte EventType_Countdown = 2;
    private const byte EventType_BattleUpdate = 3;
    private const byte EventType_BattleEnded = 4;
    
    public SXRSPBattlePlugin(
        EntryCarManager entryCarManager,
        SXRSPBattleConfiguration config,
        Func<EntryCar, SXREntryCarBattle> entryCarBattleFactory,
        SXRLeaderboardService leaderboard,
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _config = config;
        _entryCarBattleFactory = entryCarBattleFactory;
        _leaderboard = leaderboard;
        _scriptProvider = scriptProvider;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create battle handlers for each car
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            var battle = _entryCarBattleFactory(entryCar);
            _instances[entryCar.SessionId] = battle;
        }
        
        // Load and register Lua UI script
        if (_config.EnableLuaUI)
        {
            try
            {
                string luaPath = Path.Combine(
                    Path.GetDirectoryName(typeof(SXRSPBattlePlugin).Assembly.Location) ?? "",
                    "lua", "sxrspbattle.lua");
                
                if (File.Exists(luaPath))
                {
                    _scriptProvider.AddScript(File.ReadAllText(luaPath), "sxrspbattle.lua");
                    Log.Information("SP Battle Lua UI loaded from {Path}", luaPath);
                }
                else
                {
                    Log.Warning("SP Battle Lua UI not found at {Path}", luaPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load SP Battle Lua UI");
            }
        }
        
        Log.Information("SP Battle Plugin initialized with {Count} car handlers", _instances.Count);
        Log.Information("SP Battle Config: TotalSP={SP}, DrainRates={Rates}", 
            _config.TotalSP, 
            string.Join(",", _config.DrainRatesPerSecond));
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Get battle handler for a car
    /// </summary>
    internal SXREntryCarBattle GetBattle(EntryCar entryCar) => _instances[entryCar.SessionId];
    
    /// <summary>
    /// Send challenge event to Lua UI
    /// </summary>
    internal void SendChallengeEvent(EntryCar challenger, EntryCar challenged)
    {
        // Send to challenger
        SendBattleStatusEvent(challenger, EventType_Challenge, challenged.SessionId);
        
        // Send to challenged
        SendBattleStatusEvent(challenged, EventType_Challenge, challenger.SessionId);
    }
    
    /// <summary>
    /// Send countdown event to Lua UI
    /// </summary>
    internal void SendBattleCountdown(Battle battle, int countdown)
    {
        // countdown: 3, 2, 1, 0 (GO!)
        long countdownTime = countdown > 0 
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (countdown * 1000)
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        SendBattleStatusEvent(battle.Challenger, EventType_Countdown, countdownTime);
        SendBattleStatusEvent(battle.Challenged, EventType_Countdown, countdownTime);
    }
    
    /// <summary>
    /// Send battle update to Lua UI (called from Battle update loop)
    /// </summary>
    internal void SendBattleUpdate(Battle battle)
    {
        // Send to both participants
        SendBattleUpdateEvent(battle.Challenger, battle);
        SendBattleUpdateEvent(battle.Challenged, battle);
    }
    
    /// <summary>
    /// Send battle ended event
    /// </summary>
    internal void SendBattleEnded(Battle battle)
    {
        int winnerId = battle.Winner?.SessionId ?? 255;
        
        SendBattleStatusEvent(battle.Challenger, EventType_BattleEnded, winnerId);
        SendBattleStatusEvent(battle.Challenged, EventType_BattleEnded, winnerId);
        
        // Record to leaderboard
        if (_config.EnableLeaderboard && battle.Winner != null)
        {
            var loser = battle.Winner == battle.Challenger ? battle.Challenged : battle.Challenger;
            
            string? winnerSteamId = battle.Winner.Client?.Guid.ToString();
            string? loserSteamId = loser.Client?.Guid.ToString();
            
            if (!string.IsNullOrEmpty(winnerSteamId) && !string.IsNullOrEmpty(loserSteamId))
            {
                _leaderboard.RecordResult(
                    winnerSteamId,
                    battle.Winner.Client?.Name ?? "Unknown",
                    loserSteamId,
                    loser.Client?.Name ?? "Unknown");
            }
        }
    }
    
    private void SendBattleStatusEvent(EntryCar car, byte eventType, long eventData)
    {
        car.Client?.SendPacket(new CSPEventPacket
        {
            EventId = 0x53504200, // "SPB\0" - SP Battle identifier
            Data = new SXRBattleStatusEventData
            {
                EventType = eventType,
                EventData = eventData
            }
        });
    }
    
    private void SendBattleUpdateEvent(EntryCar car, Battle battle)
    {
        // Determine which SP values to send (own vs rival from perspective)
        bool isChallenger = car == battle.Challenger;
        
        float ownSP = isChallenger ? battle.ChallengerSP : battle.ChallengedSP;
        float ownMaxSP = isChallenger ? battle.ChallengerMaxSP : battle.ChallengedMaxSP;
        float rivalSP = isChallenger ? battle.ChallengedSP : battle.ChallengerSP;
        float rivalMaxSP = isChallenger ? battle.ChallengedMaxSP : battle.ChallengerMaxSP;
        float ownDrain = isChallenger ? battle.ChallengerDrainRate : battle.ChallengedDrainRate;
        float rivalDrain = isChallenger ? battle.ChallengedDrainRate : battle.ChallengerDrainRate;
        
        car.Client?.SendPacket(new CSPEventPacket
        {
            EventId = 0x53504201, // "SPB\1" - SP Battle update
            Data = new SXRBattleUpdateEventData
            {
                OwnHealth = ownSP / ownMaxSP,
                OwnRate = -ownDrain / ownMaxSP, // Negative = draining
                RivalHealth = rivalSP / rivalMaxSP,
                RivalRate = -rivalDrain / rivalMaxSP,
                Distance = battle.CurrentDistance
            }
        });
    }
    
    /// <summary>
    /// Set driver level for a player (called by external DL system)
    /// </summary>
    public void SetDriverLevel(EntryCar car, int level)
    {
        if (_instances.TryGetValue(car.SessionId, out var battle))
        {
            battle.SetDriverLevel(level);
            Log.Debug("Set driver level {Level} for car {SessionId}", level, car.SessionId);
        }
    }
    
    /// <summary>
    /// Get driver level for a player
    /// </summary>
    public int GetDriverLevel(EntryCar car)
    {
        return _instances.TryGetValue(car.SessionId, out var battle) ? battle.DriverLevel : 0;
    }
}

/// <summary>
/// Network packet data for battle status events
/// </summary>
public struct BattleStatusEventData : IOutgoingNetworkPacket
{
    public byte EventType;
    public long EventData;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(EventType);
        writer.Write(EventData);
    }
}

/// <summary>
/// Network packet data for battle update events
/// </summary>
public struct BattleUpdateEventData : IOutgoingNetworkPacket
{
    public float OwnHealth;
    public float OwnRate;
    public float RivalHealth;
    public float RivalRate;
    public float Distance;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write(OwnHealth);
        writer.Write(OwnRate);
        writer.Write(RivalHealth);
        writer.Write(RivalRate);
        writer.Write(Distance);
    }
}

/// <summary>
/// CSP Event packet wrapper
/// </summary>
public struct CSPEventPacket : IOutgoingNetworkPacket
{
    public uint EventId;
    public IOutgoingNetworkPacket Data;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)0xAD); // CSP extended packet
        writer.Write((ushort)0x80); // Online event type
        writer.Write(EventId);
        Data.ToWriter(ref writer);
    }
}
