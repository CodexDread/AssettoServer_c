using Microsoft.AspNetCore.Mvc;

namespace SXRWelcomePlugin;

/// <summary>
/// HTTP API Controller for SXR Welcome
/// </summary>
[ApiController]
[Route("sxrwelcome")]
public class SXRWelcomeController : ControllerBase
{
    private readonly SXRWelcomePlugin _plugin;
    private readonly SXRWelcomeConfiguration _config;
    
    public SXRWelcomeController(
        SXRWelcomePlugin plugin,
        SXRWelcomeConfiguration config)
    {
        _plugin = plugin;
        _config = config;
    }
    
    /// <summary>
    /// Get welcome data for a specific player
    /// </summary>
    [HttpGet("data/{steamId}")]
    public ActionResult<WelcomeData> GetWelcomeData(string steamId)
    {
        var data = _plugin.GetWelcomeData(steamId);
        if (data == null)
        {
            return NotFound(new { message = "Welcome data not found for player" });
        }
        return data;
    }
    
    /// <summary>
    /// Get server info (no player-specific data)
    /// </summary>
    [HttpGet("serverinfo")]
    public ActionResult<ServerInfoData> GetServerInfo()
    {
        return _plugin.GetServerInfo();
    }
    
    /// <summary>
    /// Get just the rules list
    /// </summary>
    [HttpGet("rules")]
    public ActionResult<List<string>> GetRules()
    {
        return _config.Rules;
    }
}
