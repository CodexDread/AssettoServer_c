using System.Collections.Concurrent;
using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Services;
using AssettoServer.Shared.Weather;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SXRSXRAdminToolsPlugin;

/// <summary>
/// Admin Tools Plugin - Server administration framework
/// </summary>
public class SXRSXRAdminToolsPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly WeatherManager _weatherManager;
    private readonly ACServerConfiguration _serverConfig;
    private readonly SXRAdminToolsConfiguration _config;
    private readonly SXRBanService _banService;
    private readonly SXRAuditService _auditService;
    private readonly CSPServerScriptProvider _scriptProvider;
    
    // Connected admins tracking
    private readonly ConcurrentDictionary<string, AdminLevel> _connectedAdmins = new();
    
    // Player monitoring
    private readonly ConcurrentDictionary<int, PlayerInfo> _playerInfos = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastKickTime = new();
    
    // Player restrictions
    private readonly ConcurrentDictionary<int, PlayerRestriction> _restrictions = new();
    
    // Force lights tracking
    private readonly ConcurrentDictionary<int, bool> _forcedLights = new();
    
    // Whitelist
    private readonly ConcurrentDictionary<string, WhitelistEntry> _whitelist = new();
    
    // Custom tools
    private readonly List<IAdminTool> _customTools = new();
    
    // Events
    public event EventHandler<AdminActionEventArgs>? OnAdminAction;
    public event EventHandler<PlayerInfo>? OnPlayerKicked;
    public event EventHandler<BanRecord>? OnPlayerBanned;
    
    public SXRAdminToolsPlugin(
        EntryCarManager entryCarManager,
        SessionManager sessionManager,
        WeatherManager weatherManager,
        ACServerConfiguration serverConfig,
        SXRAdminToolsConfiguration config,
        SXRBanService banService,
        SXRAuditService auditService,
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _weatherManager = weatherManager;
        _serverConfig = serverConfig;
        _config = config;
        _banService = banService;
        _auditService = auditService;
        _scriptProvider = scriptProvider;
        
        // Subscribe to ban events
        _banService.OnPlayerBanned += (_, ban) => OnPlayerBanned?.Invoke(this, ban);
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
            car.ResetInvoked += OnReset;
        }
        
        // Load Lua UI
        if (_config.EnableLuaUI)
        {
            LoadLuaUI();
        }
        
        // Start cleanup timer for expired bans
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                _banService.CleanupExpiredBans();
            }
        }, stoppingToken);
        
        Log.Information("Admin Tools Plugin initialized");
        Log.Information("Configured admins: {SuperAdmins} SuperAdmins, {Admins} Admins, {Mods} Moderators",
            _config.SuperAdmins.Count, _config.Admins.Count, _config.Moderators.Count);
        
        return Task.CompletedTask;
    }
    
    private void LoadLuaUI()
    {
        try
        {
            string luaPath = Path.Combine(
                Path.GetDirectoryName(typeof(SXRAdminToolsPlugin).Assembly.Location) ?? "",
                "lua", "sxradmintools.lua");
            
            if (File.Exists(luaPath))
            {
                _scriptProvider.AddScript(File.ReadAllText(luaPath), "sxradmintools.lua");
                Log.Information("Admin Tools Lua UI loaded from {Path}", luaPath);
            }
            else
            {
                Log.Warning("Admin Tools Lua UI not found at {Path}", luaPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load Admin Tools Lua UI");
        }
    }
    
    // === ADMIN AUTHENTICATION ===
    
    /// <summary>
    /// Get admin level for a Steam ID
    /// </summary>
    public AdminLevel GetAdminLevel(string steamId)
    {
        if (_config.SuperAdmins.Contains(steamId)) return AdminLevel.SuperAdmin;
        if (_config.Admins.Contains(steamId)) return AdminLevel.Admin;
        if (_config.Moderators.Contains(steamId)) return AdminLevel.Moderator;
        return AdminLevel.None;
    }
    
    /// <summary>
    /// Check if a Steam ID is an admin
    /// </summary>
    public bool IsAdmin(string steamId) => GetAdminLevel(steamId) != AdminLevel.None;
    
    /// <summary>
    /// Check if admin has required level
    /// </summary>
    public bool HasPermission(string steamId, AdminLevel requiredLevel)
    {
        return GetAdminLevel(steamId) >= requiredLevel;
    }
    
    /// <summary>
    /// Get all connected admins
    /// </summary>
    public Dictionary<string, AdminLevel> GetConnectedAdmins()
    {
        return new Dictionary<string, AdminLevel>(_connectedAdmins);
    }
    
    // === PLAYER MONITORING ===
    
    /// <summary>
    /// Get all connected players info
    /// </summary>
    public List<PlayerInfo> GetConnectedPlayers()
    {
        return _playerInfos.Values.ToList();
    }
    
    /// <summary>
    /// Get player info by session ID
    /// </summary>
    public PlayerInfo? GetPlayerInfo(int sessionId)
    {
        return _playerInfos.TryGetValue(sessionId, out var info) ? info : null;
    }
    
    /// <summary>
    /// Get player info by Steam ID
    /// </summary>
    public PlayerInfo? GetPlayerInfoBySteamId(string steamId)
    {
        return _playerInfos.Values.FirstOrDefault(p => p.SteamId == steamId);
    }
    
    /// <summary>
    /// Find players by name pattern
    /// </summary>
    public List<PlayerInfo> FindPlayersByName(string pattern)
    {
        pattern = pattern.ToLowerInvariant();
        return _playerInfos.Values
            .Where(p => p.Name.ToLowerInvariant().Contains(pattern))
            .ToList();
    }
    
    // === KICK SYSTEM ===
    
    /// <summary>
    /// Kick a player
    /// </summary>
    public AdminActionResult KickPlayer(KickRequest request)
    {
        // Validate admin
        var adminLevel = GetAdminLevel(request.AdminSteamId);
        if (adminLevel == AdminLevel.None)
        {
            return AdminActionResult.Fail("You don't have permission to kick players", "NO_PERMISSION");
        }
        
        // Find target
        ACTcpClient? target = null;
        PlayerInfo? targetInfo = null;
        
        if (request.TargetSessionId.HasValue)
        {
            var entryCar = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == request.TargetSessionId.Value);
            target = entryCar?.Client;
            targetInfo = GetPlayerInfo(request.TargetSessionId.Value);
        }
        else if (!string.IsNullOrEmpty(request.TargetSteamId))
        {
            targetInfo = GetPlayerInfoBySteamId(request.TargetSteamId);
            if (targetInfo != null)
            {
                var entryCar = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == targetInfo.SessionId);
                target = entryCar?.Client;
            }
        }
        
        if (target == null || targetInfo == null)
        {
            return AdminActionResult.Fail("Player not found", "NOT_FOUND");
        }
        
        // Check if target is admin (can't kick higher or equal level)
        var targetLevel = GetAdminLevel(targetInfo.SteamId);
        if (targetLevel >= adminLevel)
        {
            return AdminActionResult.Fail("Cannot kick an admin of equal or higher level", "PERMISSION_DENIED");
        }
        
        // Check cooldown
        if (_lastKickTime.TryGetValue(targetInfo.SessionId, out var lastKick))
        {
            if ((DateTime.UtcNow - lastKick).TotalSeconds < _config.KickCooldownSeconds)
            {
                return AdminActionResult.Fail("Kick cooldown active", "COOLDOWN");
            }
        }
        
        // Perform kick
        string reason = string.IsNullOrEmpty(request.Reason) ? _config.DefaultKickReason : request.Reason;
        
        try
        {
            target.SendPacket(new AssettoServer.Shared.Network.Packets.Outgoing.KickPacket { SessionId = (byte)target.SessionId, Reason = reason });
            target.Disconnect();
            
            _lastKickTime[targetInfo.SessionId] = DateTime.UtcNow;
            
            // Audit log
            var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
            _auditService.LogKick(request.AdminSteamId, adminInfo?.Name ?? "Console", targetInfo.SteamId, targetInfo.Name, reason);
            
            OnPlayerKicked?.Invoke(this, targetInfo);
            
            Log.Information("Player {Name} ({SteamId}) kicked by {Admin}. Reason: {Reason}",
                targetInfo.Name, targetInfo.SteamId, adminInfo?.Name ?? request.AdminSteamId, reason);
            
            return AdminActionResult.Ok($"Kicked {targetInfo.Name}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to kick player {Name}", targetInfo.Name);
            return AdminActionResult.Fail("Failed to kick player", "ERROR");
        }
    }
    
    /// <summary>
    /// Kick all non-admin players
    /// </summary>
    public AdminActionResult KickAllNonAdmins(string adminSteamId, string reason = "Server maintenance")
    {
        if (!HasPermission(adminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        int kicked = 0;
        foreach (var player in _playerInfos.Values.ToList())
        {
            if (GetAdminLevel(player.SteamId) == AdminLevel.None)
            {
                KickPlayer(new KickRequest
                {
                    TargetSessionId = player.SessionId,
                    Reason = reason,
                    AdminSteamId = adminSteamId
                });
                kicked++;
            }
        }
        
        return AdminActionResult.Ok($"Kicked {kicked} players");
    }
    
    // === BAN SYSTEM ===
    
    /// <summary>
    /// Ban a player
    /// </summary>
    public AdminActionResult BanPlayer(BanRequest request)
    {
        // Validate admin
        var adminLevel = GetAdminLevel(request.AdminSteamId);
        if (adminLevel == AdminLevel.None)
        {
            return AdminActionResult.Fail("You don't have permission to ban players", "NO_PERMISSION");
        }
        
        // Check moderator ban duration limit
        if (adminLevel == AdminLevel.Moderator && _config.ModeratorMaxBanHours > 0)
        {
            if (request.DurationHours == 0 || request.DurationHours > _config.ModeratorMaxBanHours)
            {
                return AdminActionResult.Fail($"Moderators can only ban for up to {_config.ModeratorMaxBanHours} hours", "DURATION_EXCEEDED");
            }
        }
        
        // Check if target is admin
        var targetLevel = GetAdminLevel(request.TargetSteamId);
        if (targetLevel >= adminLevel)
        {
            return AdminActionResult.Fail("Cannot ban an admin of equal or higher level", "PERMISSION_DENIED");
        }
        
        // Get target info if online
        var targetInfo = GetPlayerInfoBySteamId(request.TargetSteamId);
        if (targetInfo != null)
        {
            request.TargetName = targetInfo.Name;
            request.TargetIp = targetInfo.IpAddress;
        }
        
        // Perform ban
        var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
        var result = _banService.BanPlayer(request, adminInfo?.Name ?? "Console");
        
        if (result.Success)
        {
            // Kick if online
            if (targetInfo != null)
            {
                KickPlayer(new KickRequest
                {
                    TargetSessionId = targetInfo.SessionId,
                    Reason = $"Banned: {request.Reason}",
                    AdminSteamId = request.AdminSteamId
                });
            }
            
            // Audit log
            _auditService.LogBan(request.AdminSteamId, adminInfo?.Name ?? "Console", 
                request.TargetSteamId, request.TargetName ?? "Unknown", request.Reason, request.DurationHours);
        }
        
        return result;
    }
    
    /// <summary>
    /// Unban a player
    /// </summary>
    public AdminActionResult UnbanPlayer(string banIdOrSteamId, string adminSteamId)
    {
        if (!HasPermission(adminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        var adminInfo = GetPlayerInfoBySteamId(adminSteamId);
        var result = _banService.UnbanPlayer(banIdOrSteamId, adminSteamId, adminInfo?.Name ?? "Console");
        
        if (result.Success)
        {
            _auditService.LogUnban(adminSteamId, adminInfo?.Name ?? "Console", banIdOrSteamId, "");
        }
        
        return result;
    }
    
    /// <summary>
    /// Get ban list
    /// </summary>
    public List<BanRecord> GetBans(bool activeOnly = true, string? searchTerm = null)
    {
        return _banService.GetBans(activeOnly, searchTerm);
    }
    
    /// <summary>
    /// Check if player is banned
    /// </summary>
    public BanRecord? CheckBan(string steamId, string? ipAddress = null)
    {
        return _banService.GetActiveBan(steamId, ipAddress);
    }
    
    // === TELEPORT TO PITS ===
    
    /// <summary>
    /// Teleport a player to pits
    /// </summary>
    public AdminActionResult TeleportToPits(PitRequest request)
    {
        if (!HasPermission(request.AdminSteamId, AdminLevel.Moderator))
        {
            return AdminActionResult.Fail("You don't have permission", "NO_PERMISSION");
        }
        
        EntryCar? target = null;
        PlayerInfo? targetInfo = null;
        
        if (request.TargetSessionId.HasValue)
        {
            target = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == request.TargetSessionId.Value);
            targetInfo = GetPlayerInfo(request.TargetSessionId.Value);
        }
        else if (!string.IsNullOrEmpty(request.TargetSteamId))
        {
            targetInfo = GetPlayerInfoBySteamId(request.TargetSteamId);
            if (targetInfo != null)
            {
                target = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == targetInfo.SessionId);
            }
        }
        
        if (target == null || targetInfo == null)
        {
            return AdminActionResult.Fail("Player not found", "NOT_FOUND");
        }
        
        try
        {
            // Send player to pits
            target.SendCurrentSession(target.Client);
            
            var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
            _auditService.Log(AdminAction.Teleport, request.AdminSteamId, adminInfo?.Name ?? "Console",
                targetInfo.SteamId, targetInfo.Name, "Teleported to pits");
            
            Log.Information("Player {Name} teleported to pits by {Admin}", targetInfo.Name, adminInfo?.Name ?? request.AdminSteamId);
            
            return AdminActionResult.Ok($"Teleported {targetInfo.Name} to pits");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to teleport player to pits");
            return AdminActionResult.Fail("Failed to teleport player", "ERROR");
        }
    }
    
    // === TIME & WEATHER ===
    
    /// <summary>
    /// Set server time
    /// </summary>
    public AdminActionResult SetTime(SetTimeRequest request)
    {
        if (!HasPermission(request.AdminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        if (request.Hour < 0 || request.Hour > 23 || request.Minute < 0 || request.Minute > 59)
        {
            return AdminActionResult.Fail("Invalid time format", "INVALID_TIME");
        }
        
        try
        {
            double totalSeconds = request.Hour * 3600 + request.Minute * 60;
            _weatherManager.SetTime(totalSeconds);
            
            var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
            _auditService.Log(AdminAction.ConfigChange, request.AdminSteamId, adminInfo?.Name ?? "Console",
                "", "", $"Set time to {request.Hour:D2}:{request.Minute:D2}");
            
            Log.Information("Server time set to {Hour}:{Minute} by {Admin}", 
                request.Hour, request.Minute, adminInfo?.Name ?? request.AdminSteamId);
            
            return AdminActionResult.Ok($"Time set to {request.Hour:D2}:{request.Minute:D2}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set server time");
            return AdminActionResult.Fail("Failed to set time", "ERROR");
        }
    }
    
    /// <summary>
    /// Set weather by config ID
    /// </summary>
    public AdminActionResult SetWeather(SetWeatherRequest request)
    {
        if (!HasPermission(request.AdminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        try
        {
            if (request.WeatherConfigId.HasValue)
            {
                // Set by weather config index
                _weatherManager.SetWeatherConfiguration(request.WeatherConfigId.Value);
                
                var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
                _auditService.Log(AdminAction.ConfigChange, request.AdminSteamId, adminInfo?.Name ?? "Console",
                    "", "", $"Set weather config to {request.WeatherConfigId}");
                
                return AdminActionResult.Ok($"Weather set to config {request.WeatherConfigId}");
            }
            else if (!string.IsNullOrEmpty(request.WeatherType))
            {
                // Set CSP weather type
                if (Enum.TryParse<WeatherFxType>(request.WeatherType, true, out var weatherType))
                {
                    _weatherManager.SetCspWeather(weatherType, request.TransitionDuration);
                    
                    var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
                    _auditService.Log(AdminAction.ConfigChange, request.AdminSteamId, adminInfo?.Name ?? "Console",
                        "", "", $"Set CSP weather to {request.WeatherType}");
                    
                    return AdminActionResult.Ok($"CSP weather set to {request.WeatherType}");
                }
                else
                {
                    return AdminActionResult.Fail($"Unknown weather type: {request.WeatherType}", "INVALID_WEATHER");
                }
            }
            
            return AdminActionResult.Fail("Must specify WeatherConfigId or WeatherType", "MISSING_PARAM");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set weather");
            return AdminActionResult.Fail("Failed to set weather", "ERROR");
        }
    }
    
    /// <summary>
    /// Get available CSP weather types
    /// </summary>
    public List<string> GetCspWeatherTypes()
    {
        return Enum.GetNames<WeatherFxType>().ToList();
    }
    
    /// <summary>
    /// Get current server environment
    /// </summary>
    public ServerEnvironment GetServerEnvironment()
    {
        var currentWeather = _weatherManager.CurrentWeather;
        var sunAngle = _weatherManager.CurrentDateTime;
        
        int totalSeconds = (int)sunAngle.TimeOfDay.TotalSeconds;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        
        return new ServerEnvironment
        {
            TimeHour = hours,
            TimeMinute = minutes,
            WeatherType = currentWeather?.Type.ToString(),
            AmbientTemp = currentWeather?.TemperatureAmbient ?? 20,
            RoadTemp = currentWeather?.TemperatureRoad ?? 25
        };
    }
    
    // === FORCE LIGHTS ===
    
    /// <summary>
    /// Force headlights on/off for a player
    /// </summary>
    public AdminActionResult ForceLights(ForceLightsRequest request)
    {
        if (!HasPermission(request.AdminSteamId, AdminLevel.Moderator))
        {
            return AdminActionResult.Fail("You don't have permission", "NO_PERMISSION");
        }
        
        var target = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == request.TargetSessionId);
        var targetInfo = GetPlayerInfo(request.TargetSessionId);
        
        if (target == null || targetInfo == null)
        {
            return AdminActionResult.Fail("Player not found", "NOT_FOUND");
        }
        
        try
        {
            target.ForceLights = request.ForceOn;
            _forcedLights[request.TargetSessionId] = request.ForceOn;
            
            var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
            string action = request.ForceOn ? "on" : "off";
            
            _auditService.Log(AdminAction.Custom, request.AdminSteamId, adminInfo?.Name ?? "Console",
                targetInfo.SteamId, targetInfo.Name, $"Force lights {action}");
            
            Log.Information("Force lights {Action} for {Name} by {Admin}", 
                action, targetInfo.Name, adminInfo?.Name ?? request.AdminSteamId);
            
            return AdminActionResult.Ok($"Force lights {action} for {targetInfo.Name}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to force lights");
            return AdminActionResult.Fail("Failed to force lights", "ERROR");
        }
    }
    
    // === RESTRICTIONS (BALLAST/RESTRICTOR) ===
    
    /// <summary>
    /// Set ballast and restrictor for a player
    /// </summary>
    public AdminActionResult SetRestriction(SetRestrictionRequest request)
    {
        if (!HasPermission(request.AdminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        if (request.Restrictor < 0 || request.Restrictor > 400)
        {
            return AdminActionResult.Fail("Restrictor must be 0-400", "INVALID_RESTRICTOR");
        }
        
        if (request.BallastKg < 0 || request.BallastKg > 500)
        {
            return AdminActionResult.Fail("Ballast must be 0-500 kg", "INVALID_BALLAST");
        }
        
        var target = _entryCarManager.EntryCars.FirstOrDefault(c => c.SessionId == request.TargetSessionId);
        var targetInfo = GetPlayerInfo(request.TargetSessionId);
        
        if (target == null || targetInfo == null)
        {
            return AdminActionResult.Fail("Player not found", "NOT_FOUND");
        }
        
        try
        {
            target.Restrictor = request.Restrictor;
            target.Ballast = request.BallastKg;
            
            // Broadcast update
            _entryCarManager.BroadcastPacket(new BallastUpdatePacket
            {
                SessionId = (byte)request.TargetSessionId,
                Ballast = (float)request.BallastKg,
                Restrictor = (byte)request.Restrictor
            });
            
            // Track restriction
            _restrictions[request.TargetSessionId] = new PlayerRestriction
            {
                SessionId = request.TargetSessionId,
                SteamId = targetInfo.SteamId,
                Restrictor = request.Restrictor,
                BallastKg = request.BallastKg,
                SetBy = request.AdminSteamId
            };
            
            var adminInfo = GetPlayerInfoBySteamId(request.AdminSteamId);
            _auditService.Log(AdminAction.Custom, request.AdminSteamId, adminInfo?.Name ?? "Console",
                targetInfo.SteamId, targetInfo.Name, $"Set restrictor={request.Restrictor}, ballast={request.BallastKg}kg");
            
            Log.Information("Restriction set for {Name}: restrictor={Restrictor}, ballast={Ballast}kg by {Admin}",
                targetInfo.Name, request.Restrictor, request.BallastKg, adminInfo?.Name ?? request.AdminSteamId);
            
            return AdminActionResult.Ok($"Set {targetInfo.Name}: restrictor={request.Restrictor}, ballast={request.BallastKg}kg");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set restriction");
            return AdminActionResult.Fail("Failed to set restriction", "ERROR");
        }
    }
    
    /// <summary>
    /// Get player's current restriction
    /// </summary>
    public PlayerRestriction? GetRestriction(int sessionId)
    {
        return _restrictions.TryGetValue(sessionId, out var r) ? r : null;
    }
    
    // === WHITELIST ===
    
    /// <summary>
    /// Add player to whitelist
    /// </summary>
    public AdminActionResult AddToWhitelist(string steamId, string adminSteamId, string? reason = null)
    {
        if (!HasPermission(adminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        if (_whitelist.ContainsKey(steamId))
        {
            return AdminActionResult.Fail("Already whitelisted", "ALREADY_EXISTS");
        }
        
        var adminInfo = GetPlayerInfoBySteamId(adminSteamId);
        var targetInfo = GetPlayerInfoBySteamId(steamId);
        
        _whitelist[steamId] = new WhitelistEntry
        {
            SteamId = steamId,
            Name = targetInfo?.Name,
            AddedBy = adminSteamId,
            Reason = reason
        };
        
        _auditService.Log(AdminAction.Custom, adminSteamId, adminInfo?.Name ?? "Console",
            steamId, targetInfo?.Name ?? steamId, "Added to whitelist");
        
        Log.Information("Added {SteamId} to whitelist by {Admin}", steamId, adminInfo?.Name ?? adminSteamId);
        
        return AdminActionResult.Ok($"Added {steamId} to whitelist");
    }
    
    /// <summary>
    /// Remove player from whitelist
    /// </summary>
    public AdminActionResult RemoveFromWhitelist(string steamId, string adminSteamId)
    {
        if (!HasPermission(adminSteamId, AdminLevel.Admin))
        {
            return AdminActionResult.Fail("Requires Admin level", "NO_PERMISSION");
        }
        
        if (!_whitelist.TryRemove(steamId, out _))
        {
            return AdminActionResult.Fail("Not on whitelist", "NOT_FOUND");
        }
        
        var adminInfo = GetPlayerInfoBySteamId(adminSteamId);
        
        _auditService.Log(AdminAction.Custom, adminSteamId, adminInfo?.Name ?? "Console",
            steamId, "", "Removed from whitelist");
        
        return AdminActionResult.Ok($"Removed {steamId} from whitelist");
    }
    
    /// <summary>
    /// Get whitelist
    /// </summary>
    public List<WhitelistEntry> GetWhitelist()
    {
        return _whitelist.Values.ToList();
    }
    
    /// <summary>
    /// Check if player is whitelisted
    /// </summary>
    public bool IsWhitelisted(string steamId)
    {
        return _whitelist.ContainsKey(steamId);
    }
    
    // === FRAMEWORK ===
    
    /// <summary>
    /// Register a custom admin tool
    /// </summary>
    public void RegisterTool(IAdminTool tool)
    {
        _customTools.Add(tool);
        _ = tool.InitializeAsync(this);
        Log.Information("Registered admin tool: {ToolId} - {DisplayName}", tool.ToolId, tool.DisplayName);
    }
    
    /// <summary>
    /// Get registered custom tools
    /// </summary>
    public List<IAdminTool> GetCustomTools()
    {
        return _customTools.ToList();
    }
    
    /// <summary>
    /// Execute a custom tool
    /// </summary>
    public async Task<AdminActionResult> ExecuteToolAsync(string toolId, string adminSteamId, Dictionary<string, object> parameters)
    {
        var tool = _customTools.FirstOrDefault(t => t.ToolId == toolId);
        if (tool == null)
        {
            return AdminActionResult.Fail("Tool not found", "NOT_FOUND");
        }
        
        var adminLevel = GetAdminLevel(adminSteamId);
        if (adminLevel < tool.RequiredLevel)
        {
            return AdminActionResult.Fail($"Requires {tool.RequiredLevel} level", "NO_PERMISSION");
        }
        
        var adminInfo = GetPlayerInfoBySteamId(adminSteamId);
        var context = new AdminContext
        {
            AdminSteamId = adminSteamId,
            AdminName = adminInfo?.Name ?? "Console",
            AdminLevel = adminLevel,
            Plugin = this
        };
        
        return await tool.ExecuteAsync(context, parameters);
    }
    
    // === EVENT HANDLERS ===
    
    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        string? ipAddress = client.TcpClient?.Client?.RemoteEndPoint?.ToString()?.Split(':')[0];
        
        // Check ban
        if (_config.CheckBansOnConnect)
        {
            var ban = _banService.GetActiveBan(steamId, ipAddress);
            if (ban != null)
            {
                string message = _banService.GetBanMessage(ban);
                Log.Information("Banned player {Name} ({SteamId}) attempted to connect", client.Name, steamId);
                client.SendPacket(new AssettoServer.Shared.Network.Packets.Outgoing.KickPacket { SessionId = (byte)client.SessionId, Reason = message });
                client.Disconnect();
                return;
            }
        }
        
        // Track player
        var adminLevel = GetAdminLevel(steamId);
        
        var info = new PlayerInfo
        {
            SessionId = client.SessionId,
            SteamId = steamId,
            Name = client.Name ?? "Unknown",
            CarModel = client.EntryCar.Model,
            IpAddress = ipAddress,
            ConnectedAt = DateTime.UtcNow,
            AdminLevel = adminLevel,
            LastActivity = DateTime.UtcNow
        };
        
        _playerInfos[client.SessionId] = info;
        
        // Track admin login
        if (adminLevel != AdminLevel.None)
        {
            _connectedAdmins[steamId] = adminLevel;
            _auditService.LogLogin(steamId, client.Name ?? "Unknown", adminLevel);
            Log.Information("Admin connected: {Name} ({SteamId}) - Level: {Level}", client.Name, steamId, adminLevel);
        }
    }
    
    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        string steamId = client.Guid.ToString();
        
        _playerInfos.TryRemove(client.SessionId, out _);
        
        if (_connectedAdmins.TryRemove(steamId, out var level))
        {
            _auditService.LogLogout(steamId, client.Name ?? "Unknown");
        }
    }
    
    private void OnPositionUpdate(EntryCar sender, in PositionUpdateIn update)
    {
        if (_playerInfos.TryGetValue(sender.SessionId, out var info))
        {
            info.Position = update.Position;
            info.SpeedKph = update.Velocity.Length() * 3.6f;
            info.Ping = sender.Ping;
            
            // Update AFK status
            if (info.SpeedKph > 5)
            {
                info.LastActivity = DateTime.UtcNow;
                info.IsAfk = false;
            }
            else if ((DateTime.UtcNow - info.LastActivity).TotalMinutes > 5)
            {
                info.IsAfk = true;
            }
        }
    }
    
    private void OnCollision(EntryCar sender, CollisionEventArgs args)
    {
        if (_playerInfos.TryGetValue(sender.SessionId, out var info))
        {
            info.Collisions++;
            info.LastActivity = DateTime.UtcNow;
            
            // Check incident threshold
            if (_config.TrackIncidents && info.Collisions >= _config.IncidentWarningThreshold)
            {
                Log.Warning("Player {Name} reached incident threshold: {Collisions} collisions", info.Name, info.Collisions);
            }
        }
    }
    
    private void OnReset(EntryCar sender, EventArgs args)
    {
        if (_playerInfos.TryGetValue(sender.SessionId, out var info))
        {
            info.Resets++;
            info.LastActivity = DateTime.UtcNow;
        }
    }
    
    public override void Dispose()
    {
        _auditService.Dispose();
        _banService.Save();
        base.Dispose();
    }
}
