using System.Collections.Concurrent;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRNameplatesPlugin;

/// <summary>
/// Nameplates Plugin - Display player information above cars
/// </summary>
public class SXRNameplatesPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRNameplatesConfiguration _config;
    private readonly CSPServerScriptProvider _scriptProvider;
    
    // Player nameplate data cache
    private readonly ConcurrentDictionary<int, SXRNameplateData> _nameplates = new();
    
    // External data providers (set via integration)
    private Func<string, int>? _getDriverLevel;
    private Func<string, int>? _getPrestigeRank;
    private Func<string, int>? _getLeaderboardRank;
    private Func<string, string>? _getSafetyRating;
    private Func<string, string>? _getClubTag;
    
    public SXRNameplatesPlugin(
        EntryCarManager entryCarManager,
        SXRNameplatesConfiguration config,
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _config = config;
        _scriptProvider = scriptProvider;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled) return;
        
        // Subscribe to events
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        
        // Load Lua UI
        if (_config.EnableLuaUI)
        {
            LoadLuaUI();
        }
        
        Log.Information("Nameplates Plugin initialized");
        
        // Periodic sync loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_config.SyncIntervalMs, stoppingToken);
            BroadcastSXRNameplateData();
        }
    }
    
    private void LoadLuaUI()
    {
        try
        {
            string luaPath = Path.Combine(
                Path.GetDirectoryName(typeof(SXRNameplatesPlugin).Assembly.Location) ?? "",
                "lua", "sxrnameplates.lua");
            
            if (File.Exists(luaPath))
            {
                _scriptProvider.AddScript(File.ReadAllText(luaPath), "sxrnameplates.lua");
                Log.Information("Nameplates Lua UI loaded from {Path}", luaPath);
            }
            else
            {
                Log.Warning("Nameplates Lua UI not found at {Path}", luaPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load Nameplates Lua UI");
        }
    }
    
    // === INTEGRATION METHODS ===
    
    /// <summary>
    /// Set the driver level provider function
    /// </summary>
    public void SetDriverLevelProvider(Func<string, int> provider)
    {
        _getDriverLevel = provider;
        Log.Debug("Driver level provider set");
    }
    
    /// <summary>
    /// Set the prestige rank provider function
    /// </summary>
    public void SetPrestigeRankProvider(Func<string, int> provider)
    {
        _getPrestigeRank = provider;
        Log.Debug("Prestige rank provider set");
    }
    
    /// <summary>
    /// Set the leaderboard rank provider function
    /// </summary>
    public void SetLeaderboardRankProvider(Func<string, int> provider)
    {
        _getLeaderboardRank = provider;
        Log.Debug("Leaderboard rank provider set");
    }
    
    /// <summary>
    /// Set the safety rating provider function
    /// </summary>
    public void SetSafetyRatingProvider(Func<string, string> provider)
    {
        _getSafetyRating = provider;
        Log.Debug("Safety rating provider set");
    }
    
    /// <summary>
    /// Set the club tag provider function
    /// </summary>
    public void SetClubTagProvider(Func<string, string> provider)
    {
        _getClubTag = provider;
        Log.Debug("Club tag provider set");
    }
    
    // === DATA MANAGEMENT ===
    
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        string carModel = client.EntryCar.Model;
        
        var data = new SXRNameplateData
        {
            SessionId = client.SessionId,
            SteamId = steamId,
            Name = client.Name ?? "Unknown",
            CarModel = carModel,
            CarClass = GetCarClass(carModel),
            DriverLevel = _getDriverLevel?.Invoke(steamId) ?? 1,
            PrestigeRank = _getPrestigeRank?.Invoke(steamId) ?? 0,
            LeaderboardRank = _getLeaderboardRank?.Invoke(steamId) ?? 0,
            SafetyRating = _getSafetyRating?.Invoke(steamId) ?? "C",
            ClubTag = _getClubTag?.Invoke(steamId) ?? ""
        };
        
        _nameplates[client.SessionId] = data;
        
        // Send current data to new client
        SendSXRNameplateDataToClient(client);
    }
    
    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        _nameplates.TryRemove(client.SessionId, out _);
    }
    
    /// <summary>
    /// Update nameplate data for a player
    /// </summary>
    public void UpdateNameplate(int sessionId, Action<SXRNameplateData> updater)
    {
        if (_nameplates.TryGetValue(sessionId, out var data))
        {
            updater(data);
        }
    }
    
    /// <summary>
    /// Update nameplate data by Steam ID
    /// </summary>
    public void UpdateNameplateBySteamId(string steamId, Action<SXRNameplateData> updater)
    {
        var data = _nameplates.Values.FirstOrDefault(n => n.SteamId == steamId);
        if (data != null)
        {
            updater(data);
        }
    }
    
    /// <summary>
    /// Get nameplate data for a player
    /// </summary>
    public SXRNameplateData? GetNameplate(int sessionId)
    {
        return _nameplates.TryGetValue(sessionId, out var data) ? data : null;
    }
    
    /// <summary>
    /// Get all nameplate data
    /// </summary>
    public List<SXRNameplateData> GetAllNameplates()
    {
        return _nameplates.Values.ToList();
    }
    
    /// <summary>
    /// Get sync data packet
    /// </summary>
    public SXRNameplateSyncData GetSyncData()
    {
        return new SXRNameplateSyncData
        {
            Players = _nameplates.Values.ToList(),
            DisplayConfig = new SXRNameplateDisplayConfig
            {
                ShowDriverLevel = _config.ShowDriverLevel,
                ShowCarClass = _config.ShowCarClass,
                ShowClubTag = _config.ShowClubTag,
                ShowRank = _config.ShowRank,
                ShowSafetyRating = _config.ShowSafetyRating,
                MaxDistance = _config.MaxVisibleDistance,
                FadeDistance = _config.FadeStartDistance,
                HeightOffset = _config.HeightOffset
            }
        };
    }
    
    // === CAR CLASS ===
    
    private string GetCarClass(string carModel)
    {
        // Check mappings
        foreach (var mapping in _config.CarClassMappings)
        {
            if (carModel.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.Value;
            }
        }
        
        return _config.DefaultCarClass;
    }
    
    // === BROADCASTING ===
    
    private void BroadcastSXRNameplateData()
    {
        // Refresh external data
        foreach (var data in _nameplates.Values)
        {
            data.DriverLevel = _getDriverLevel?.Invoke(data.SteamId) ?? data.DriverLevel;
            data.PrestigeRank = _getPrestigeRank?.Invoke(data.SteamId) ?? data.PrestigeRank;
            data.LeaderboardRank = _getLeaderboardRank?.Invoke(data.SteamId) ?? data.LeaderboardRank;
            data.SafetyRating = _getSafetyRating?.Invoke(data.SteamId) ?? data.SafetyRating;
            data.ClubTag = _getClubTag?.Invoke(data.SteamId) ?? data.ClubTag;
        }
        
        // Data is synced via HTTP API - Lua clients poll for updates
    }
    
    private void SendSXRNameplateDataToClient(ACTcpClient client)
    {
        // Initial sync via CSP online event packet
        // Lua script will poll HTTP API for updates
    }
}
