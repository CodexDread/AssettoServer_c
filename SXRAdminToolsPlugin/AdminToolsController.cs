using Microsoft.AspNetCore.Mvc;

namespace AdminToolsPlugin;

/// <summary>
/// HTTP API Controller for Admin Tools
/// </summary>
[ApiController]
[Route("admin")]
public class AdminToolsController : ControllerBase
{
    private readonly AdminToolsPlugin _plugin;
    private readonly BanService _banService;
    private readonly AuditService _auditService;
    private readonly AdminToolsConfiguration _config;
    
    public AdminToolsController(
        AdminToolsPlugin plugin,
        BanService banService,
        AuditService auditService,
        AdminToolsConfiguration config)
    {
        _plugin = plugin;
        _banService = banService;
        _auditService = auditService;
        _config = config;
    }
    
    /// <summary>
    /// Validate API key if required
    /// </summary>
    private bool ValidateAuth()
    {
        if (!_config.RequireHttpAuth) return true;
        
        var apiKey = Request.Headers["X-API-Key"].FirstOrDefault() 
                  ?? Request.Query["apikey"].FirstOrDefault();
        
        return apiKey == _config.HttpApiKey;
    }
    
    // === PLAYER MANAGEMENT ===
    
    /// <summary>
    /// Get all connected players
    /// </summary>
    [HttpGet("players")]
    public ActionResult<List<PlayerInfo>> GetPlayers()
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.GetConnectedPlayers();
    }
    
    /// <summary>
    /// Get player by session ID
    /// </summary>
    [HttpGet("players/{sessionId}")]
    public ActionResult<PlayerInfo> GetPlayer(int sessionId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        var player = _plugin.GetPlayerInfo(sessionId);
        if (player == null) return NotFound();
        return player;
    }
    
    /// <summary>
    /// Search players by name
    /// </summary>
    [HttpGet("players/search")]
    public ActionResult<List<PlayerInfo>> SearchPlayers([FromQuery] string name)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.FindPlayersByName(name);
    }
    
    /// <summary>
    /// Kick a player
    /// </summary>
    [HttpPost("kick")]
    public ActionResult<AdminActionResult> KickPlayer([FromBody] KickRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.KickPlayer(request);
    }
    
    /// <summary>
    /// Kick all non-admin players
    /// </summary>
    [HttpPost("kickall")]
    public ActionResult<AdminActionResult> KickAllNonAdmins([FromQuery] string adminSteamId, [FromQuery] string reason = "Server maintenance")
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.KickAllNonAdmins(adminSteamId, reason);
    }
    
    // === BAN MANAGEMENT ===
    
    /// <summary>
    /// Get ban list
    /// </summary>
    [HttpGet("bans")]
    public ActionResult<List<BanRecord>> GetBans(
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? search = null)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _banService.GetBans(activeOnly, search);
    }
    
    /// <summary>
    /// Get ban by ID
    /// </summary>
    [HttpGet("bans/{banId}")]
    public ActionResult<BanRecord> GetBan(string banId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        var ban = _banService.GetBan(banId);
        if (ban == null) return NotFound();
        return ban;
    }
    
    /// <summary>
    /// Ban a player
    /// </summary>
    [HttpPost("ban")]
    public ActionResult<AdminActionResult> BanPlayer([FromBody] BanRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        if (string.IsNullOrEmpty(request.TargetSteamId))
        {
            return BadRequest("TargetSteamId is required");
        }
        
        return _plugin.BanPlayer(request);
    }
    
    /// <summary>
    /// Unban a player
    /// </summary>
    [HttpDelete("bans/{banIdOrSteamId}")]
    public ActionResult<AdminActionResult> UnbanPlayer(
        string banIdOrSteamId,
        [FromQuery] string adminSteamId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(adminSteamId))
        {
            return BadRequest("adminSteamId query parameter is required");
        }
        
        return _plugin.UnbanPlayer(banIdOrSteamId, adminSteamId);
    }
    
    /// <summary>
    /// Check if a player is banned
    /// </summary>
    [HttpGet("bans/check/{steamId}")]
    public ActionResult<BanCheckResult> CheckBan(string steamId, [FromQuery] string? ip = null)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        var ban = _plugin.CheckBan(steamId, ip);
        return new BanCheckResult
        {
            IsBanned = ban != null,
            Ban = ban
        };
    }
    
    /// <summary>
    /// Get ban statistics
    /// </summary>
    [HttpGet("bans/stats")]
    public ActionResult<BanStats> GetBanStats()
    {
        if (!ValidateAuth()) return Unauthorized();
        return _banService.GetStats();
    }
    
    // === AUDIT LOG ===
    
    /// <summary>
    /// Get recent audit entries
    /// </summary>
    [HttpGet("audit")]
    public ActionResult<List<AuditEntry>> GetAuditLog(
        [FromQuery] int count = 50,
        [FromQuery] AdminAction? action = null)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _auditService.GetRecentEntries(count, action);
    }
    
    /// <summary>
    /// Search audit log
    /// </summary>
    [HttpGet("audit/search")]
    public ActionResult<List<AuditEntry>> SearchAudit([FromQuery] string q, [FromQuery] int count = 50)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _auditService.Search(q, count);
    }
    
    /// <summary>
    /// Get audit entries by admin
    /// </summary>
    [HttpGet("audit/admin/{steamId}")]
    public ActionResult<List<AuditEntry>> GetAdminAudit(string steamId, [FromQuery] int count = 50)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _auditService.GetEntriesByAdmin(steamId, count);
    }
    
    /// <summary>
    /// Get audit entries for a target player
    /// </summary>
    [HttpGet("audit/target/{steamId}")]
    public ActionResult<List<AuditEntry>> GetTargetAudit(string steamId, [FromQuery] int count = 50)
    {
        if (!ValidateAuth()) return Unauthorized();
        return _auditService.GetEntriesForTarget(steamId, count);
    }
    
    // === SERVER STATUS ===
    
    /// <summary>
    /// Get server admin status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ServerAdminStatus> GetStatus()
    {
        if (!ValidateAuth()) return Unauthorized();
        
        var players = _plugin.GetConnectedPlayers();
        var admins = _plugin.GetConnectedAdmins();
        var banStats = _banService.GetStats();
        
        return new ServerAdminStatus
        {
            PlayersOnline = players.Count,
            AdminsOnline = admins.Count,
            TotalBans = banStats.ActiveBans,
            BansLast24h = banStats.Last24Hours,
            ConnectedAdmins = admins.Select(a => new AdminInfo 
            { 
                SteamId = a.Key, 
                Level = a.Value 
            }).ToList()
        };
    }
    
    // === TELEPORT ===
    
    /// <summary>
    /// Teleport player to pits
    /// </summary>
    [HttpPost("pit")]
    public ActionResult<AdminActionResult> TeleportToPits([FromBody] PitRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.TeleportToPits(request);
    }
    
    // === TIME & WEATHER ===
    
    /// <summary>
    /// Get server environment (time, weather)
    /// </summary>
    [HttpGet("environment")]
    public ActionResult<ServerEnvironment> GetEnvironment()
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.GetServerEnvironment();
    }
    
    /// <summary>
    /// Set server time
    /// </summary>
    [HttpPost("time")]
    public ActionResult<AdminActionResult> SetTime([FromBody] SetTimeRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.SetTime(request);
    }
    
    /// <summary>
    /// Set weather
    /// </summary>
    [HttpPost("weather")]
    public ActionResult<AdminActionResult> SetWeather([FromBody] SetWeatherRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.SetWeather(request);
    }
    
    /// <summary>
    /// Get CSP weather types
    /// </summary>
    [HttpGet("weather/types")]
    public ActionResult<List<string>> GetWeatherTypes()
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.GetCspWeatherTypes();
    }
    
    // === FORCE LIGHTS ===
    
    /// <summary>
    /// Force headlights on/off
    /// </summary>
    [HttpPost("forcelights")]
    public ActionResult<AdminActionResult> ForceLights([FromBody] ForceLightsRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.ForceLights(request);
    }
    
    // === RESTRICTIONS ===
    
    /// <summary>
    /// Set ballast and restrictor
    /// </summary>
    [HttpPost("restrict")]
    public ActionResult<AdminActionResult> SetRestriction([FromBody] SetRestrictionRequest request)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(request.AdminSteamId))
        {
            return BadRequest("AdminSteamId is required");
        }
        
        return _plugin.SetRestriction(request);
    }
    
    /// <summary>
    /// Get player restriction
    /// </summary>
    [HttpGet("restrict/{sessionId}")]
    public ActionResult<PlayerRestriction> GetRestriction(int sessionId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        var restriction = _plugin.GetRestriction(sessionId);
        if (restriction == null) return NotFound();
        return restriction;
    }
    
    // === WHITELIST ===
    
    /// <summary>
    /// Get whitelist
    /// </summary>
    [HttpGet("whitelist")]
    public ActionResult<List<WhitelistEntry>> GetWhitelist()
    {
        if (!ValidateAuth()) return Unauthorized();
        return _plugin.GetWhitelist();
    }
    
    /// <summary>
    /// Add to whitelist
    /// </summary>
    [HttpPost("whitelist")]
    public ActionResult<AdminActionResult> AddToWhitelist(
        [FromQuery] string steamId,
        [FromQuery] string adminSteamId,
        [FromQuery] string? reason = null)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(adminSteamId))
        {
            return BadRequest("steamId and adminSteamId are required");
        }
        
        return _plugin.AddToWhitelist(steamId, adminSteamId, reason);
    }
    
    /// <summary>
    /// Remove from whitelist
    /// </summary>
    [HttpDelete("whitelist/{steamId}")]
    public ActionResult<AdminActionResult> RemoveFromWhitelist(
        string steamId,
        [FromQuery] string adminSteamId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        if (string.IsNullOrEmpty(adminSteamId))
        {
            return BadRequest("adminSteamId query parameter is required");
        }
        
        return _plugin.RemoveFromWhitelist(steamId, adminSteamId);
    }
    
    /// <summary>
    /// Check if player is whitelisted
    /// </summary>
    [HttpGet("whitelist/check/{steamId}")]
    public ActionResult<WhitelistCheckResult> CheckWhitelist(string steamId)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        return new WhitelistCheckResult
        {
            IsWhitelisted = _plugin.IsWhitelisted(steamId)
        };
    }
    
    // === TOOLS ===
    
    /// <summary>
    /// Get available admin tools
    /// </summary>
    [HttpGet("tools")]
    public ActionResult<List<ToolInfo>> GetTools()
    {
        if (!ValidateAuth()) return Unauthorized();
        
        return _plugin.GetCustomTools().Select(t => new ToolInfo
        {
            ToolId = t.ToolId,
            DisplayName = t.DisplayName,
            RequiredLevel = t.RequiredLevel
        }).ToList();
    }
    
    /// <summary>
    /// Execute a custom tool
    /// </summary>
    [HttpPost("tools/{toolId}")]
    public async Task<ActionResult<AdminActionResult>> ExecuteTool(
        string toolId,
        [FromQuery] string adminSteamId,
        [FromBody] Dictionary<string, object>? parameters = null)
    {
        if (!ValidateAuth()) return Unauthorized();
        
        return await _plugin.ExecuteToolAsync(toolId, adminSteamId, parameters ?? new());
    }
}

// === RESPONSE MODELS ===

public class BanCheckResult
{
    public bool IsBanned { get; set; }
    public BanRecord? Ban { get; set; }
}

public class ServerAdminStatus
{
    public int PlayersOnline { get; set; }
    public int AdminsOnline { get; set; }
    public int TotalBans { get; set; }
    public int BansLast24h { get; set; }
    public List<AdminInfo> ConnectedAdmins { get; set; } = new();
}

public class AdminInfo
{
    public string SteamId { get; set; } = "";
    public AdminLevel Level { get; set; }
}

public class ToolInfo
{
    public string ToolId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public AdminLevel RequiredLevel { get; set; }
}

public class WhitelistCheckResult
{
    public bool IsWhitelisted { get; set; }
}
