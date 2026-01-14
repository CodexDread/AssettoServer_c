using System.Collections.Concurrent;
using System.Text.Json;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRCarLockPlugin;

/// <summary>
/// SXR Car Lock Plugin - Restricts vehicles based on driver level
/// </summary>
public class SXRCarLockPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRCarLockConfiguration _config;
    private readonly ACServerConfiguration _serverConfig;
    
    // Pending enforcement (players in grace period)
    private readonly ConcurrentDictionary<int, DateTime> _pendingEnforcement = new();
    
    // Car class mappings loaded from JSON
    private CarClassData _carClassData = new();
    private readonly object _carClassLock = new();
    private FileSystemWatcher? _jsonWatcher;
    private string _jsonFilePath = "";
    
    // Player level provider (from SXRPlayerStatsPlugin)
    private Func<string, int>? _getDriverLevel;
    
    // Prestige rank provider (from SXRPlayerStatsPlugin)  
    private Func<string, int>? _getPrestigeRank;
    
    // Admin check provider (from SXRAdminToolsPlugin)
    private Func<string, bool>? _isAdmin;
    
    // Car class definitions cache
    private readonly Dictionary<string, CarClassDefinition> _classDefinitions = new();
    
    // Event for welcome plugin integration
    public event Action<ACTcpClient, RestrictionData>? OnPlayerRestrictionChecked;
    
    public SXRCarLockPlugin(
        EntryCarManager entryCarManager,
        SXRCarLockConfiguration config,
        ACServerConfiguration serverConfig,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _config = config;
        _serverConfig = serverConfig;
        
        InitializeClassDefinitions();
    }
    
    private void InitializeClassDefinitions()
    {
        _classDefinitions["S"] = new CarClassDefinition
        {
            ClassName = "S",
            DisplayName = "S-Class",
            MinLevel = _config.SClassMinLevel,
            Color = "#9932CC",
            Description = "Supercars - Elite drivers only"
        };
        
        _classDefinitions["A"] = new CarClassDefinition
        {
            ClassName = "A",
            DisplayName = "A-Class",
            MinLevel = _config.AClassMinLevel,
            Color = "#FF4444",
            Description = "Sports Cars - Experienced drivers"
        };
        
        _classDefinitions["B"] = new CarClassDefinition
        {
            ClassName = "B",
            DisplayName = "B-Class",
            MinLevel = _config.BClassMinLevel,
            Color = "#FFA500",
            Description = "Tuners - Intermediate drivers"
        };
        
        _classDefinitions["C"] = new CarClassDefinition
        {
            ClassName = "C",
            DisplayName = "C-Class",
            MinLevel = _config.CClassMinLevel,
            Color = "#FFFF00",
            Description = "Street Cars - Developing drivers"
        };
        
        _classDefinitions["D"] = new CarClassDefinition
        {
            ClassName = "D",
            DisplayName = "D-Class",
            MinLevel = _config.DClassMinLevel,
            Color = "#00FF00",
            Description = "Starter Cars - New drivers"
        };
        
        _classDefinitions["E"] = new CarClassDefinition
        {
            ClassName = "E",
            DisplayName = "E-Class",
            MinLevel = _config.EClassMinLevel,
            Color = "#00BFFF",
            Description = "Entry Cars - Everyone welcome"
        };
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled) return;
        
        // Load car classes from JSON
        LoadCarClassesFromJson();
        
        // Setup file watcher for auto-reload
        if (_config.AutoReloadJson)
        {
            SetupJsonFileWatcher();
        }
        
        _entryCarManager.ClientConnected += OnClientConnected;
        
        Log.Information("SXR Car Lock Plugin initialized");
        Log.Information("Enforcement mode: {Mode}, Grace period: {Grace}s", 
            _config.Mode, _config.GracePeriodSeconds);
        Log.Information("Loaded {Count} car class mappings from JSON", _carClassData.Cars.Count);
        
        // Enforcement loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
            ProcessPendingEnforcements();
        }
        
        // Cleanup
        _jsonWatcher?.Dispose();
    }
    
    // === JSON LOADING ===
    
    private void LoadCarClassesFromJson()
    {
        try
        {
            // Determine JSON file path
            string pluginDir = Path.GetDirectoryName(typeof(SXRCarLockPlugin).Assembly.Location) ?? "";
            _jsonFilePath = Path.Combine(pluginDir, _config.CarClassesJsonFile);
            
            // Also check cfg directory
            if (!File.Exists(_jsonFilePath))
            {
                _jsonFilePath = Path.Combine(pluginDir, "cfg", _config.CarClassesJsonFile);
            }
            
            if (!File.Exists(_jsonFilePath))
            {
                Log.Warning("Car classes JSON file not found at {Path}, creating default", _jsonFilePath);
                CreateDefaultJsonFile(_jsonFilePath);
            }
            
            string json = File.ReadAllText(_jsonFilePath);
            var data = JsonSerializer.Deserialize<CarClassData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            
            if (data != null)
            {
                lock (_carClassLock)
                {
                    _carClassData = data;
                }
                Log.Information("Loaded {Count} car mappings from {Path}", data.Cars.Count, _jsonFilePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load car classes JSON file");
        }
    }
    
    private void CreateDefaultJsonFile(string path)
    {
        var defaultData = new CarClassData
        {
            Version = "1.0",
            Description = "Car class mappings for SXR Car Lock Plugin",
            Cars = new List<CarClassEntry>
            {
                // S-Class
                new() { Model = "ks_ferrari_488", Class = "S", DisplayName = "Ferrari 488" },
                new() { Model = "ks_ferrari_f40", Class = "S", DisplayName = "Ferrari F40" },
                new() { Model = "ks_lamborghini_huracan", Class = "S", DisplayName = "Lamborghini Huracan" },
                new() { Model = "ks_lamborghini_aventador", Class = "S", DisplayName = "Lamborghini Aventador" },
                new() { Model = "ks_mclaren_p1", Class = "S", DisplayName = "McLaren P1" },
                new() { Model = "ks_pagani_huayra", Class = "S", DisplayName = "Pagani Huayra" },
                new() { Model = "ks_porsche_918", Class = "S", DisplayName = "Porsche 918" },
                
                // A-Class
                new() { Model = "ks_porsche_911_gt3", Class = "A", DisplayName = "Porsche 911 GT3" },
                new() { Model = "ks_nissan_gtr", Class = "A", DisplayName = "Nissan GT-R" },
                new() { Model = "ks_corvette_c7", Class = "A", DisplayName = "Corvette C7" },
                new() { Model = "ks_audi_r8", Class = "A", DisplayName = "Audi R8" },
                new() { Model = "ks_bmw_m4", Class = "A", DisplayName = "BMW M4" },
                new() { Model = "ks_mercedes_amg_gt", Class = "A", DisplayName = "Mercedes AMG GT" },
                
                // B-Class
                new() { Model = "ks_toyota_supra_mkiv", Class = "B", DisplayName = "Toyota Supra MK4" },
                new() { Model = "ks_mazda_rx7_tuned", Class = "B", DisplayName = "Mazda RX-7" },
                new() { Model = "ks_nissan_skyline_r34", Class = "B", DisplayName = "Nissan Skyline R34" },
                new() { Model = "ks_honda_nsx", Class = "B", DisplayName = "Honda NSX" },
                new() { Model = "ks_mitsubishi_evo", Class = "B", DisplayName = "Mitsubishi EVO" },
                new() { Model = "ks_subaru_wrx", Class = "B", DisplayName = "Subaru WRX STI" },
                new() { Model = "ks_nissan_370z", Class = "B", DisplayName = "Nissan 370Z" },
                new() { Model = "ks_toyota_gt86", Class = "B", DisplayName = "Toyota GT86" },
                
                // C-Class
                new() { Model = "ks_bmw_m3_e30", Class = "C", DisplayName = "BMW M3 E30" },
                new() { Model = "ks_bmw_m3_e92", Class = "C", DisplayName = "BMW M3 E92" },
                new() { Model = "ks_audi_s1", Class = "C", DisplayName = "Audi S1" },
                new() { Model = "ks_mazda_mx5_cup", Class = "C", DisplayName = "Mazda MX-5 Cup" },
                new() { Model = "ks_honda_s2000", Class = "C", DisplayName = "Honda S2000" },
                
                // D-Class
                new() { Model = "ks_toyota_ae86", Class = "D", DisplayName = "Toyota AE86" },
                new() { Model = "ks_fiat_500", Class = "D", DisplayName = "Fiat 500" },
                new() { Model = "ks_abarth_500", Class = "D", DisplayName = "Abarth 500" },
                
                // E-Class
                new() { Model = "ks_mazda_miata", Class = "E", DisplayName = "Mazda Miata" },
                new() { Model = "traffic_", Class = "E", DisplayName = "Traffic Car", MatchMode = "prefix" }
            },
            AlwaysAllowed = new List<string>(),
            AlwaysBlocked = new List<string>()
        };
        
        // Ensure directory exists
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        string json = JsonSerializer.Serialize(defaultData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        File.WriteAllText(path, json);
        Log.Information("Created default car classes JSON at {Path}", path);
    }
    
    private void SetupJsonFileWatcher()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_jsonFilePath);
            string fileName = Path.GetFileName(_jsonFilePath);
            
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            
            _jsonWatcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            
            _jsonWatcher.Changed += (sender, args) =>
            {
                // Debounce - wait a bit for file to finish writing
                Task.Delay(500).ContinueWith(_ =>
                {
                    Log.Information("Car classes JSON file changed, reloading...");
                    LoadCarClassesFromJson();
                });
            };
            
            _jsonWatcher.EnableRaisingEvents = true;
            Log.Debug("File watcher set up for {Path}", _jsonFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set up file watcher for car classes JSON");
        }
    }
    
    /// <summary>
    /// Manually reload car classes from JSON
    /// </summary>
    public void ReloadCarClasses()
    {
        LoadCarClassesFromJson();
    }
    
    // === INTEGRATION METHODS ===
    
    /// <summary>
    /// Set the driver level provider (from SXRPlayerStatsPlugin)
    /// This should return the EFFECTIVE level for unlocks, not the display level
    /// </summary>
    public void SetDriverLevelProvider(Func<string, int> provider)
    {
        _getDriverLevel = provider;
        Log.Debug("Driver level provider set for Car Lock");
    }
    
    /// <summary>
    /// Set the prestige rank provider (from SXRPlayerStatsPlugin)
    /// </summary>
    public void SetPrestigeRankProvider(Func<string, int> provider)
    {
        _getPrestigeRank = provider;
        Log.Debug("Prestige rank provider set for Car Lock");
    }
    
    /// <summary>
    /// Set the admin check provider (from SXRAdminToolsPlugin)
    /// </summary>
    public void SetAdminCheckProvider(Func<string, bool> provider)
    {
        _isAdmin = provider;
        Log.Debug("Admin check provider set for Car Lock");
    }
    
    // === CAR CLASS METHODS ===
    
    /// <summary>
    /// Get the class for a car model
    /// </summary>
    public string GetCarClass(string carModel)
    {
        lock (_carClassLock)
        {
            // Check always allowed first
            if (_carClassData.AlwaysAllowed.Any(c => 
                carModel.Equals(c, StringComparison.OrdinalIgnoreCase) ||
                carModel.StartsWith(c, StringComparison.OrdinalIgnoreCase)))
            {
                return "E"; // Treat as lowest class (always allowed)
            }
            
            // Check car mappings
            foreach (var entry in _carClassData.Cars)
            {
                bool match = entry.MatchMode?.ToLower() switch
                {
                    "prefix" => carModel.StartsWith(entry.Model, StringComparison.OrdinalIgnoreCase),
                    "contains" => carModel.Contains(entry.Model, StringComparison.OrdinalIgnoreCase),
                    "exact" => carModel.Equals(entry.Model, StringComparison.OrdinalIgnoreCase),
                    _ => carModel.Equals(entry.Model, StringComparison.OrdinalIgnoreCase) ||
                         carModel.StartsWith(entry.Model, StringComparison.OrdinalIgnoreCase)
                };
                
                if (match)
                {
                    return entry.Class.ToUpper();
                }
            }
        }
        
        return _config.DefaultCarClass;
    }
    
    /// <summary>
    /// Check if car is always blocked
    /// </summary>
    public bool IsCarBlocked(string carModel)
    {
        lock (_carClassLock)
        {
            return _carClassData.AlwaysBlocked.Any(c => 
                carModel.Equals(c, StringComparison.OrdinalIgnoreCase) ||
                carModel.StartsWith(c, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    /// <summary>
    /// Check if car is always allowed
    /// </summary>
    public bool IsCarAlwaysAllowed(string carModel)
    {
        lock (_carClassLock)
        {
            return _carClassData.AlwaysAllowed.Any(c => 
                carModel.Equals(c, StringComparison.OrdinalIgnoreCase) ||
                carModel.StartsWith(c, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    /// <summary>
    /// Get display name for a car model
    /// </summary>
    public string GetCarDisplayName(string carModel)
    {
        lock (_carClassLock)
        {
            var entry = _carClassData.Cars.FirstOrDefault(c =>
                carModel.Equals(c.Model, StringComparison.OrdinalIgnoreCase) ||
                carModel.StartsWith(c.Model, StringComparison.OrdinalIgnoreCase));
            
            if (entry != null && !string.IsNullOrEmpty(entry.DisplayName))
            {
                return entry.DisplayName;
            }
        }
        
        // Fallback: format model name
        return carModel.Replace("ks_", "").Replace("_", " ");
    }
    
    /// <summary>
    /// Get required level for a car class
    /// </summary>
    public int GetRequiredLevel(string carClass)
    {
        return carClass.ToUpper() switch
        {
            "S" => _config.SClassMinLevel,
            "A" => _config.AClassMinLevel,
            "B" => _config.BClassMinLevel,
            "C" => _config.CClassMinLevel,
            "D" => _config.DClassMinLevel,
            "E" => _config.EClassMinLevel,
            _ => _config.DClassMinLevel
        };
    }
    
    /// <summary>
    /// Get all class definitions
    /// </summary>
    public List<CarClassDefinition> GetClassDefinitions()
    {
        return _classDefinitions.Values.OrderByDescending(c => c.MinLevel).ToList();
    }
    
    /// <summary>
    /// Get class requirements as dictionary
    /// </summary>
    public Dictionary<string, int> GetClassRequirements()
    {
        return new Dictionary<string, int>
        {
            { "S", _config.SClassMinLevel },
            { "A", _config.AClassMinLevel },
            { "B", _config.BClassMinLevel },
            { "C", _config.CClassMinLevel },
            { "D", _config.DClassMinLevel },
            { "E", _config.EClassMinLevel }
        };
    }
    
    // === CHECK METHODS ===
    
    /// <summary>
    /// Check if a player can drive their current car
    /// </summary>
    public CarLockCheckResult CheckPlayer(ACTcpClient client)
    {
        string steamId = client.Guid.ToString();
        string carModel = client.EntryCar.Model;
        string carClass = GetCarClass(carModel);
        int requiredLevel = GetRequiredLevel(carClass);
        int playerLevel = _getDriverLevel?.Invoke(steamId) ?? 1;
        int prestigeRank = _getPrestigeRank?.Invoke(steamId) ?? 0;
        
        // Effective level for unlocks - prestiged players get max level access
        int effectiveLevel = prestigeRank > 0 ? 999 : playerLevel;
        
        var result = new CarLockCheckResult
        {
            SessionId = client.SessionId,
            SteamId = steamId,
            PlayerName = client.Name ?? "Unknown",
            CarModel = carModel,
            CarClass = carClass,
            RequiredLevel = requiredLevel,
            PlayerLevel = playerLevel,
            PrestigeRank = prestigeRank,
            EffectiveLevel = effectiveLevel
        };
        
        // Check always blocked
        if (IsCarBlocked(carModel))
        {
            result.IsAllowed = false;
            result.Reason = "This car is not allowed on this server";
            return result;
        }
        
        // Check always allowed
        if (IsCarAlwaysAllowed(carModel))
        {
            result.IsAllowed = true;
            result.IsBypassed = true;
            result.Reason = "Car is always allowed";
            return result;
        }
        
        // Check admin bypass
        if (_config.AdminsBypass && (_isAdmin?.Invoke(steamId) ?? false))
        {
            result.IsAllowed = true;
            result.IsBypassed = true;
            result.Reason = "Admin bypass";
            return result;
        }
        
        // Check prestige bypass - prestiged players can drive any car
        if (prestigeRank > 0)
        {
            result.IsAllowed = true;
            result.IsBypassed = true;
            result.Reason = $"Prestige {prestigeRank} - All cars unlocked";
            return result;
        }
        
        // Check level requirement
        if (playerLevel >= requiredLevel)
        {
            result.IsAllowed = true;
            result.Reason = "Level requirement met";
        }
        else
        {
            result.IsAllowed = false;
            result.Reason = $"Requires Driver Level {requiredLevel} ({carClass}-Class). You are Level {playerLevel}.";
        }
        
        return result;
    }
    
    /// <summary>
    /// Get restriction data for welcome plugin
    /// </summary>
    public RestrictionData GetRestrictionData(ACTcpClient client)
    {
        var check = CheckPlayer(client);
        string steamId = client.Guid.ToString();
        int playerLevel = _getDriverLevel?.Invoke(steamId) ?? 1;
        
        var data = new RestrictionData
        {
            HasRestriction = !check.IsAllowed,
            CurrentCar = check.CarModel,
            CurrentCarClass = check.CarClass,
            RequiredLevel = check.RequiredLevel,
            PlayerLevel = playerLevel,
            LevelsNeeded = Math.Max(0, check.RequiredLevel - playerLevel),
            EnforcementMode = _config.Mode.ToString(),
            GracePeriodSeconds = _config.GracePeriodSeconds,
            AvailableCars = GetAvailableCarsForLevel(playerLevel)
        };
        
        return data;
    }
    
    /// <summary>
    /// Get available cars for a player's level
    /// </summary>
    public List<CarInfo> GetAvailableCarsForLevel(int playerLevel)
    {
        var available = new List<CarInfo>();
        
        // Get all entry cars from server config
        foreach (var entryCar in _serverConfig.EntryCars)
        {
            string model = entryCar.Model;
            string carClass = GetCarClass(model);
            int required = GetRequiredLevel(carClass);
            
            if (playerLevel >= required)
            {
                available.Add(new CarInfo
                {
                    Model = model,
                    DisplayName = model.Replace("ks_", "").Replace("_", " "),
                    CarClass = carClass,
                    RequiredLevel = required,
                    IsAvailable = true
                });
            }
        }
        
        return available.DistinctBy(c => c.Model).ToList();
    }
    
    /// <summary>
    /// Get all cars with availability status for a player
    /// </summary>
    public AvailableCarsResponse GetAllCarsForPlayer(string steamId)
    {
        int playerLevel = _getDriverLevel?.Invoke(steamId) ?? 1;
        
        var response = new AvailableCarsResponse
        {
            SteamId = steamId,
            PlayerLevel = playerLevel,
            ClassRequirements = GetClassRequirements()
        };
        
        foreach (var entryCar in _serverConfig.EntryCars)
        {
            string model = entryCar.Model;
            string carClass = GetCarClass(model);
            int required = GetRequiredLevel(carClass);
            
            var info = new CarInfo
            {
                Model = model,
                DisplayName = model.Replace("ks_", "").Replace("_", " "),
                CarClass = carClass,
                RequiredLevel = required,
                IsAvailable = playerLevel >= required
            };
            
            if (info.IsAvailable)
                response.AvailableCars.Add(info);
            else
                response.LockedCars.Add(info);
        }
        
        response.AvailableCars = response.AvailableCars.DistinctBy(c => c.Model).ToList();
        response.LockedCars = response.LockedCars.DistinctBy(c => c.Model).ToList();
        
        return response;
    }
    
    // === ENFORCEMENT ===
    
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        if (!_config.Enabled) return;
        
        var check = CheckPlayer(client);
        var restrictionData = GetRestrictionData(client);
        
        // Notify welcome plugin
        OnPlayerRestrictionChecked?.Invoke(client, restrictionData);
        
        if (!check.IsAllowed)
        {
            Log.Warning("Player {Name} ({SteamId}) does not meet requirements for {Car} ({Class}). " +
                       "Required: Level {Required}, Has: Level {Has}",
                client.Name, client.Guid, check.CarModel, check.CarClass, 
                check.RequiredLevel, check.PlayerLevel);
            
            if (_config.Mode != EnforcementMode.Warning)
            {
                // Add to pending enforcement (grace period)
                _pendingEnforcement[client.SessionId] = DateTime.UtcNow.AddSeconds(_config.GracePeriodSeconds);
            }
        }
        else
        {
            Log.Debug("Player {Name} approved for {Car} ({Class})", 
                client.Name, check.CarModel, check.CarClass);
        }
    }
    
    private void ProcessPendingEnforcements()
    {
        var now = DateTime.UtcNow;
        var toRemove = new List<int>();
        
        foreach (var kvp in _pendingEnforcement)
        {
            if (now >= kvp.Value)
            {
                toRemove.Add(kvp.Key);
                
                // Find client
                var client = _entryCarManager.EntryCars
                    .Select(e => e.Client)
                    .FirstOrDefault(c => c?.SessionId == kvp.Key);
                
                if (client != null)
                {
                    EnforceRestriction(client);
                }
            }
        }
        
        foreach (var id in toRemove)
        {
            _pendingEnforcement.TryRemove(id, out _);
        }
    }
    
    private void EnforceRestriction(ACTcpClient client)
    {
        var check = CheckPlayer(client);
        
        // Re-check in case level changed during grace period
        if (check.IsAllowed) return;
        
        switch (_config.Mode)
        {
            case EnforcementMode.Spectate:
                Log.Information("Moving {Name} to spectator - car restriction", client.Name);
                // Send to spectator via pit/teleport
                client.SendCurrentSession();
                // TODO: Implement proper spectator mode if available
                break;
                
            case EnforcementMode.Kick:
                Log.Information("Kicking {Name} - car restriction: {Reason}", client.Name, check.Reason);
                _ = client.KickAsync(_config.KickMessage);
                break;
                
            case EnforcementMode.Warning:
                // No enforcement, just warning
                break;
        }
    }
    
    /// <summary>
    /// Cancel pending enforcement (e.g., if player disconnects)
    /// </summary>
    public void CancelEnforcement(int sessionId)
    {
        _pendingEnforcement.TryRemove(sessionId, out _);
    }
}
