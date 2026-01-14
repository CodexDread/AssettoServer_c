using Microsoft.AspNetCore.Mvc;

namespace SXRCarLockPlugin;

/// <summary>
/// HTTP API Controller for SXR Car Lock
/// </summary>
[ApiController]
[Route("sxrcarlock")]
public class SXRCarLockController : ControllerBase
{
    private readonly SXRCarLockPlugin _plugin;
    private readonly SXRCarLockConfiguration _config;
    
    public SXRCarLockController(
        SXRCarLockPlugin plugin,
        SXRCarLockConfiguration config)
    {
        _plugin = plugin;
        _config = config;
    }
    
    /// <summary>
    /// Get all class requirements
    /// </summary>
    [HttpGet("requirements")]
    public ActionResult<ClassRequirementsResponse> GetRequirements()
    {
        return new ClassRequirementsResponse
        {
            Classes = _plugin.GetClassDefinitions()
        };
    }
    
    /// <summary>
    /// Get class requirements as simple dictionary
    /// </summary>
    [HttpGet("levels")]
    public ActionResult<Dictionary<string, int>> GetLevelRequirements()
    {
        return _plugin.GetClassRequirements();
    }
    
    /// <summary>
    /// Get all class definitions
    /// </summary>
    [HttpGet("classes")]
    public ActionResult<List<CarClassDefinition>> GetClasses()
    {
        return _plugin.GetClassDefinitions();
    }
    
    /// <summary>
    /// Get cars available for a specific driver level
    /// </summary>
    [HttpGet("available/level/{level}")]
    public ActionResult<List<CarInfo>> GetAvailableForLevel(int level)
    {
        return _plugin.GetAvailableCarsForLevel(level);
    }
    
    /// <summary>
    /// Get all cars with availability for a player
    /// </summary>
    [HttpGet("available/{steamId}")]
    public ActionResult<AvailableCarsResponse> GetAvailableForPlayer(string steamId)
    {
        return _plugin.GetAllCarsForPlayer(steamId);
    }
    
    /// <summary>
    /// Get car class for a specific model
    /// </summary>
    [HttpGet("carclass/{carModel}")]
    public ActionResult<CarClassInfo> GetCarClass(string carModel)
    {
        string carClass = _plugin.GetCarClass(carModel);
        int required = _plugin.GetRequiredLevel(carClass);
        
        return new CarClassInfo
        {
            CarModel = carModel,
            CarClass = carClass,
            RequiredLevel = required
        };
    }
    
    /// <summary>
    /// Get current enforcement configuration
    /// </summary>
    [HttpGet("config")]
    public ActionResult<EnforcementConfig> GetConfig()
    {
        return new EnforcementConfig
        {
            Mode = _config.Mode.ToString(),
            GracePeriodSeconds = _config.GracePeriodSeconds,
            AdminsBypass = _config.AdminsBypass
        };
    }
    
    /// <summary>
    /// Reload car classes from JSON file
    /// </summary>
    [HttpPost("reload")]
    public ActionResult ReloadCarClasses()
    {
        _plugin.ReloadCarClasses();
        return Ok(new { message = "Car classes reloaded from JSON" });
    }
    
    /// <summary>
    /// Get all car mappings from JSON
    /// </summary>
    [HttpGet("mappings")]
    public ActionResult<CarMappingsResponse> GetCarMappings()
    {
        // Access the internal car class data via reflection or add a public method
        return new CarMappingsResponse
        {
            Classes = _plugin.GetClassDefinitions(),
            LevelRequirements = _plugin.GetClassRequirements()
        };
    }
}

public class CarClassInfo
{
    public string CarModel { get; set; } = "";
    public string CarClass { get; set; } = "";
    public int RequiredLevel { get; set; }
}

public class EnforcementConfig
{
    public string Mode { get; set; } = "";
    public int GracePeriodSeconds { get; set; }
    public bool AdminsBypass { get; set; }
}

public class CarMappingsResponse
{
    public List<CarClassDefinition> Classes { get; set; } = new();
    public Dictionary<string, int> LevelRequirements { get; set; } = new();
}
