using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Player Stats Plugin - Comprehensive statistics tracking for players
/// </summary>
public class SXRPlayerStatsPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly SXRPlayerStatsConfiguration _config;
    private readonly SXRPlayerStatsService _statsService;
    private readonly CSPServerScriptProvider _scriptProvider;
    
    private readonly Dictionary<int, string> _sessionToSteamId = new();
    private readonly Timer _saveTimer;
    private readonly Timer _trackingTimer;
    
    public SXRPlayerStatsPlugin(
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        SXRPlayerStatsConfiguration config,
        SXRPlayerStatsService statsService,
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _config = config;
        _statsService = statsService;
        _scriptProvider = scriptProvider;
        
        // Setup auto-save timer
        _saveTimer = new Timer(_ => _statsService.Save(), null, 
            TimeSpan.FromSeconds(_config.SaveIntervalSeconds), 
            TimeSpan.FromSeconds(_config.SaveIntervalSeconds));
        
        // Setup tracking timer
        _trackingTimer = new Timer(TrackingUpdate, null,
            TimeSpan.FromMilliseconds(_config.TrackingUpdateIntervalMs),
            TimeSpan.FromMilliseconds(_config.TrackingUpdateIntervalMs));
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to events
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        
        foreach (var car in _entryCarManager.EntryCars)
        {
            car.PositionUpdateReceived += OnPositionUpdate;
            car.CollisionReceived += OnCollision;
        }
        
        // Load Lua UI
        if (_config.EnableLuaUI)
        {
            try
            {
                string luaPath = Path.Combine(
                    Path.GetDirectoryName(typeof(SXRPlayerStatsPlugin).Assembly.Location) ?? "",
                    "lua", "sxrplayerstats.lua");
                
                if (File.Exists(luaPath))
                {
                    _scriptProvider.AddScript(File.ReadAllText(luaPath), "sxrplayerstats.lua");
                    Log.Information("Player Stats Lua UI loaded from {Path}", luaPath);
                }
                else
                {
                    Log.Warning("Player Stats Lua UI not found at {Path}", luaPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load Player Stats Lua UI");
            }
        }
        
        Log.Information("Player Stats Plugin initialized");
        return Task.CompletedTask;
    }
    
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        string name = client.Name ?? "Unknown";
        string carModel = client.EntryCar.Model;
        
        _sessionToSteamId[client.SessionId] = steamId;
        _statsService.StartSession(steamId, name, carModel);
        
        // Send initial stats to client
        SendStatsToClient(client);
    }
    
    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        _statsService.EndSession(steamId);
        _sessionToSteamId.Remove(client.SessionId);
    }
    
    private void OnPositionUpdate(EntryCar sender, in PositionUpdateIn positionUpdate)
    {
        if (!_sessionToSteamId.TryGetValue(sender.SessionId, out var steamId)) return;
        
        // Speed calculation from velocity
        float speedMs = positionUpdate.Velocity.Length();
        float speedKph = speedMs * 3.6f;
        
        _statsService.UpdateTracking(steamId, positionUpdate.Position, speedKph, 
            _sessionManager.ServerTimeMilliseconds);
    }
    
    private void OnCollision(EntryCar sender, CollisionEventArgs args)
    {
        if (!_sessionToSteamId.TryGetValue(sender.SessionId, out var steamId)) return;
        
        bool isCarCollision = args.TargetCar != null;
        _statsService.RecordCollision(steamId, isCarCollision);
    }
    
    private void TrackingUpdate(object? state)
    {
        // Periodic stat sync to connected clients (every few seconds)
        // This is handled by Lua UI polling the HTTP API instead
    }
    
    /// <summary>
    /// Send current stats to a client via network packet
    /// </summary>
    public void SendStatsToClient(ACTcpClient client)
    {
        string steamId = client.Guid.ToString();
        var stats = _statsService.GetStats(steamId);
        
        client.SendPacket(new PlayerStatsPacket
        {
            DriverLevel = stats.DriverLevel,
            TotalXP = stats.TotalXP,
            XPToNextLevel = stats.XPToNextLevel,
            TotalDistanceKm = (float)stats.TotalDistanceKm,
            TotalTimeHours = (float)(stats.TotalTimeOnServerSeconds / 3600.0),
            RaceWins = stats.RaceWins,
            BattleWins = stats.BattleWins,
            TopSpeedKph = stats.TopSpeedKph,
            AverageSpeedKph = stats.AverageSpeedKph,
            TotalCollisions = stats.TotalCollisions
        });
    }
    
    /// <summary>
    /// Get player stats (for external plugin integration)
    /// </summary>
    public PlayerStats GetPlayerStats(string steamId) => _statsService.GetStats(steamId);
    
    /// <summary>
    /// Get player stats by EntryCar
    /// </summary>
    public PlayerStats? GetPlayerStats(EntryCar car)
    {
        if (_sessionToSteamId.TryGetValue(car.SessionId, out var steamId))
            return _statsService.GetStats(steamId);
        return null;
    }
    
    /// <summary>
    /// Record a battle result (called by SPBattlePlugin integration)
    /// </summary>
    public void RecordBattleResult(string steamId, bool isWin)
    {
        _statsService.RecordBattleResult(steamId, isWin);
    }
    
    /// <summary>
    /// Record race start
    /// </summary>
    public void RecordRaceStart(string steamId)
    {
        _statsService.RecordRaceStart(steamId);
    }
    
    /// <summary>
    /// Record race finish
    /// </summary>
    public void RecordRaceFinish(string steamId, int position, bool isDNF = false)
    {
        _statsService.RecordRaceFinish(steamId, position, isDNF);
    }
    
    /// <summary>
    /// Get leaderboard
    /// </summary>
    public List<LeaderboardEntry> GetLeaderboard(LeaderboardCategory category, int count = 10)
    {
        return _statsService.GetLeaderboard(category, count);
    }
    
    /// <summary>
    /// Force save stats
    /// </summary>
    public void ForceSave()
    {
        _statsService.Save();
    }
    
    public override void Dispose()
    {
        _saveTimer.Dispose();
        _trackingTimer.Dispose();
        _statsService.Save(); // Final save on shutdown
        base.Dispose();
    }
}

/// <summary>
/// Network packet for sending stats to client
/// </summary>
public struct PlayerStatsPacket : IOutgoingNetworkPacket
{
    public int DriverLevel;
    public long TotalXP;
    public long XPToNextLevel;
    public float TotalDistanceKm;
    public float TotalTimeHours;
    public int RaceWins;
    public int BattleWins;
    public float TopSpeedKph;
    public float AverageSpeedKph;
    public int TotalCollisions;
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)0xAD); // CSP extended packet
        writer.Write((ushort)0x80); // Online event
        writer.Write(0x50535400u); // "PST\0" - Player Stats identifier
        
        writer.Write(DriverLevel);
        writer.Write(TotalXP);
        writer.Write(XPToNextLevel);
        writer.Write(TotalDistanceKm);
        writer.Write(TotalTimeHours);
        writer.Write(RaceWins);
        writer.Write(BattleWins);
        writer.Write(TopSpeedKph);
        writer.Write(AverageSpeedKph);
        writer.Write(TotalCollisions);
    }
}
