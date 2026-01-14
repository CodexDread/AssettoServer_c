using Microsoft.AspNetCore.Mvc;

namespace SXRNameplatesPlugin;

/// <summary>
/// HTTP API Controller for Nameplates
/// </summary>
[ApiController]
[Route("sxrnameplates")]
public class SXRNameplatesController : ControllerBase
{
    private readonly SXRNameplatesPlugin _plugin;
    private readonly SXRNameplatesConfiguration _config;
    
    public SXRNameplatesController(
        SXRNameplatesPlugin plugin,
        SXRNameplatesConfiguration config)
    {
        _plugin = plugin;
        _config = config;
    }
    
    /// <summary>
    /// Get all nameplate data (for Lua client sync)
    /// </summary>
    [HttpGet("sync")]
    public ActionResult<SXRNameplateSyncData> GetSyncData()
    {
        return _plugin.GetSyncData();
    }
    
    /// <summary>
    /// Get nameplate for specific player
    /// </summary>
    [HttpGet("player/{sessionId}")]
    public ActionResult<SXRNameplateData> GetPlayerNameplate(int sessionId)
    {
        var data = _plugin.GetNameplate(sessionId);
        if (data == null) return NotFound();
        return data;
    }
    
    /// <summary>
    /// Get display configuration
    /// </summary>
    [HttpGet("config")]
    public ActionResult<SXRNameplateDisplayConfig> GetConfig()
    {
        return new SXRNameplateDisplayConfig
        {
            ShowDriverLevel = _config.ShowDriverLevel,
            ShowCarClass = _config.ShowCarClass,
            ShowClubTag = _config.ShowClubTag,
            ShowRank = _config.ShowRank,
            ShowSafetyRating = _config.ShowSafetyRating,
            MaxDistance = _config.MaxVisibleDistance,
            FadeDistance = _config.FadeStartDistance,
            HeightOffset = _config.HeightOffset
        };
    }
    
    /// <summary>
    /// Get safety rating colors
    /// </summary>
    [HttpGet("colors/safety")]
    public ActionResult<Dictionary<string, string>> GetSafetyRatingColors()
    {
        return new Dictionary<string, string>
        {
            { "S", SafetyRating.GetColor("S") },
            { "A", SafetyRating.GetColor("A") },
            { "B", SafetyRating.GetColor("B") },
            { "C", SafetyRating.GetColor("C") },
            { "D", SafetyRating.GetColor("D") },
            { "F", SafetyRating.GetColor("F") }
        };
    }
    
    /// <summary>
    /// Get car class colors
    /// </summary>
    [HttpGet("colors/carclass")]
    public ActionResult<Dictionary<string, string>> GetCarClassColors()
    {
        return new Dictionary<string, string>
        {
            { "S", CarClass.GetColor("S") },
            { "A", CarClass.GetColor("A") },
            { "B", CarClass.GetColor("B") },
            { "C", CarClass.GetColor("C") },
            { "D", CarClass.GetColor("D") },
            { "E", CarClass.GetColor("E") }
        };
    }
}
