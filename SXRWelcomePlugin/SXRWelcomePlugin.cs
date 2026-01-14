using System.Collections.Concurrent;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRWelcomePlugin;

/// <summary>
/// SXR Welcome Plugin - Welcome popup with server info and restrictions
/// </summary>
public class SXRWelcomePlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRWelcomeConfiguration _config;
    private readonly CSPServerScriptProvider _scriptProvider;
    
    // Cached welcome data per player
    private readonly ConcurrentDictionary<string, WelcomeData> _playerWelcomeData = new();
    
    // Integration providers
    private Func<string, int>? _getDriverLevel;
    private Func<string, int>? _getPrestigeRank;
    private Func<string, int>? _getDriverXp;
    private Func<string, int>? _getXpToNextLevel;
    private Func<ACTcpClient, RestrictionDataDto>? _getRestrictionData;
    private Func<int, List<AvailableCarInfo>>? _getAvailableCars;
    
    public SXRWelcomePlugin(
        EntryCarManager entryCarManager,
        SXRWelcomeConfiguration config,
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
        
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
        
        if (_config.EnableLuaUI)
        {
            LoadLuaUI();
        }
        
        Log.Information("SXR Welcome Plugin initialized");
        
        await Task.CompletedTask;
    }
    
    private void LoadLuaUI()
    {
        try
        {
            string luaPath = Path.Combine(
                Path.GetDirectoryName(typeof(SXRWelcomePlugin).Assembly.Location) ?? "",
                "lua", "sxrwelcome.lua");
            
            if (File.Exists(luaPath))
            {
                _scriptProvider.AddScript(File.ReadAllText(luaPath), "sxrwelcome.lua");
                Log.Information("SXR Welcome Lua UI loaded");
            }
            else
            {
                Log.Warning("SXR Welcome Lua UI not found at {Path}", luaPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load SXR Welcome Lua UI");
        }
    }
    
    // === INTEGRATION METHODS ===
    
    public void SetDriverLevelProvider(Func<string, int> provider)
    {
        _getDriverLevel = provider;
    }
    
    public void SetPrestigeRankProvider(Func<string, int> provider)
    {
        _getPrestigeRank = provider;
    }
    
    public void SetDriverXpProvider(Func<string, int> provider)
    {
        _getDriverXp = provider;
    }
    
    public void SetXpToNextLevelProvider(Func<string, int> provider)
    {
        _getXpToNextLevel = provider;
    }
    
    public void SetRestrictionDataProvider(Func<ACTcpClient, RestrictionDataDto> provider)
    {
        _getRestrictionData = provider;
    }
    
    public void SetAvailableCarsProvider(Func<int, List<AvailableCarInfo>> provider)
    {
        _getAvailableCars = provider;
    }
    
    /// <summary>
    /// Called by SXRCarLockPlugin when restriction is checked
    /// </summary>
    public void OnRestrictionChecked(ACTcpClient client, RestrictionDataDto restriction)
    {
        string steamId = client.Guid.ToString();
        
        // Build welcome data with restriction info
        var data = BuildWelcomeData(client, restriction);
        _playerWelcomeData[steamId] = data;
        
        Log.Debug("Welcome data prepared for {Name} - Restriction: {HasRestriction}", 
            client.Name, restriction.HasRestriction);
    }
    
    // === DATA BUILDING ===
    
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        
        // Get restriction data if available
        RestrictionDataDto? restriction = null;
        try
        {
            restriction = _getRestrictionData?.Invoke(client);
        }
        catch { /* SXRCarLockPlugin may not be loaded */ }
        
        var data = BuildWelcomeData(client, restriction);
        _playerWelcomeData[steamId] = data;
    }
    
    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        _playerWelcomeData.TryRemove(client.Guid.ToString(), out _);
    }
    
    private WelcomeData BuildWelcomeData(ACTcpClient client, RestrictionDataDto? restriction)
    {
        string steamId = client.Guid.ToString();
        int driverLevel = _getDriverLevel?.Invoke(steamId) ?? 1;
        int prestigeRank = _getPrestigeRank?.Invoke(steamId) ?? 0;
        int driverXp = _getDriverXp?.Invoke(steamId) ?? 0;
        int xpToNext = _getXpToNextLevel?.Invoke(steamId) ?? 100;
        
        var data = new WelcomeData
        {
            // Server info
            ServerName = _config.ServerName,
            ServerDescription = _config.ServerDescription,
            WelcomeMessage = _config.WelcomeMessage,
            Rules = _config.Rules,
            
            // Player info
            PlayerName = client.Name ?? "Driver",
            SteamId = steamId,
            DriverLevel = driverLevel,
            PrestigeRank = prestigeRank,
            DriverXp = driverXp,
            XpToNextLevel = xpToNext,
            
            // Social
            DiscordUrl = string.IsNullOrEmpty(_config.DiscordUrl) ? null : _config.DiscordUrl,
            WebsiteUrl = string.IsNullOrEmpty(_config.WebsiteUrl) ? null : _config.WebsiteUrl,
            
            // Timing
            ShowDelaySeconds = _config.ShowDelaySeconds,
            AutoDismissSeconds = _config.AutoDismissSeconds,
            MinimumDisplaySeconds = _config.MinimumDisplaySeconds
        };
        
        // Add restriction info if applicable
        // Note: Prestiged players bypass restrictions, so this should rarely trigger for them
        if (restriction != null && restriction.HasRestriction && _config.ShowRestrictionWarning)
        {
            data.HasRestriction = true;
            data.CurrentCar = restriction.CurrentCar;
            data.CurrentCarClass = restriction.CurrentCarClass;
            data.RequiredLevel = restriction.RequiredLevel;
            data.LevelsNeeded = restriction.LevelsNeeded;
            data.EnforcementMode = restriction.EnforcementMode;
            data.GracePeriodSeconds = restriction.GracePeriodSeconds;
            
            // Build warning message from template
            data.RestrictionWarning = _config.RestrictionWarningTemplate
                .Replace("{car}", restriction.CurrentCar ?? "Unknown")
                .Replace("{class}", restriction.CurrentCarClass ?? "?")
                .Replace("{required}", restriction.RequiredLevel.ToString())
                .Replace("{current}", driverLevel.ToString())
                .Replace("{needed}", restriction.LevelsNeeded.ToString());
            
            // Get available cars
            if (_config.ShowAvailableCars)
            {
                var available = _getAvailableCars?.Invoke(driverLevel) ?? new List<AvailableCarInfo>();
                data.AvailableCars = available.Take(_config.MaxAvailableCarsToShow).ToList();
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Get welcome data for a player
    /// </summary>
    public WelcomeData? GetWelcomeData(string steamId)
    {
        return _playerWelcomeData.TryGetValue(steamId, out var data) ? data : null;
    }
    
    /// <summary>
    /// Get server info (for clients without player data yet)
    /// </summary>
    public ServerInfoData GetServerInfo()
    {
        return new ServerInfoData
        {
            ServerName = _config.ServerName,
            ServerDescription = _config.ServerDescription,
            WelcomeMessage = _config.WelcomeMessage,
            Rules = _config.Rules,
            DiscordUrl = _config.DiscordUrl,
            WebsiteUrl = _config.WebsiteUrl
        };
    }
}

/// <summary>
/// DTO for restriction data from SXRCarLockPlugin
/// </summary>
public class RestrictionDataDto
{
    public bool HasRestriction { get; set; }
    public string? CurrentCar { get; set; }
    public string? CurrentCarClass { get; set; }
    public int RequiredLevel { get; set; }
    public int LevelsNeeded { get; set; }
    public string? EnforcementMode { get; set; }
    public int GracePeriodSeconds { get; set; }
}

/// <summary>
/// Server info response
/// </summary>
public class ServerInfoData
{
    public string ServerName { get; set; } = "";
    public string ServerDescription { get; set; } = "";
    public string WelcomeMessage { get; set; } = "";
    public List<string> Rules { get; set; } = new();
    public string? DiscordUrl { get; set; }
    public string? WebsiteUrl { get; set; }
}
