# SXR Development - Claude Code Instructions

## Knowledge Base Location

The SXR Development Knowledge Base is the authoritative reference for this project:
- **File**: `SXR_Development_Knowledge_Base_v6.md` (or latest version)
- **Location**: Project root or `/docs/` directory

## When to Reference the Knowledge Base

**ALWAYS read the knowledge base FIRST when:**
- Starting any new plugin development
- Fixing bugs in existing plugins
- Adding new features to any SXR component
- Working with AssettoServer APIs
- Creating CSP Lua scripts (client-side UI)
- Debugging network/packet issues
- Implementing any pattern that might already be documented

**Specific triggers:**
- "Create a new plugin" → Read: Plugin Development section
- "Add Lua UI" → Read: CSP Lua SDK Reference, Online Script Development
- "Fix validation" → Read: Configuration System, Common Mistakes
- "Add chat command" → Read: Command System section
- "Track player data" → Read: Event System, Core Server Components
- "Traffic/AI behavior" → Read: AI Traffic System section

## How to Use the Knowledge Base

### Before Writing Code
```bash
# Read relevant sections before implementing
cat SXR_Development_Knowledge_Base_v6.md | grep -A 100 "## Plugin Development"
```

### During Development
Reference specific patterns:
- **Configuration validation**: Search for "IValidateConfiguration"
- **Lua UI windows**: Search for "ui.toolWindow" and "inputs=true"
- **Chat messages**: Search for "SendPacket" and "ChatMessage"
- **Event handlers**: Search for "ClientConnected", "ClientDisconnected"

### Code Review Checklist
Before completing any task, verify against knowledge base:
1. Configuration class has proper validator with `Validate()` method (not `IsValid`)
2. Lua `ui.toolWindow()` calls include `inputs=true` parameter
3. YAML config file follows naming: `plugin_<name>_cfg.yml`
4. Embedded resources use correct path format in `.csproj`
5. All chat packets use `SessionId = 255` for server messages

## How to Update the Knowledge Base

### When to Update
Add new information when you:
- Discover a bug pattern and its fix
- Find an undocumented API behavior
- Develop a new reusable pattern
- Encounter a "gotcha" that wasted time
- Successfully implement something complex

### Update Format

**For Bug Fixes:**
```markdown
### [Component] - Issue Description
**Problem**: What went wrong
**Cause**: Why it happened
**Solution**: How to fix it
**Code Example**: (if applicable)
```

**For New Patterns:**
```markdown
### Pattern Name
**Use Case**: When to use this
**Implementation**:
```code
// Example code
```
**Notes**: Any caveats or considerations
```

**For API Documentation:**
```markdown
### ClassName.MethodName
**Purpose**: What it does
**Parameters**: 
- `param1` (Type): Description
**Returns**: Type - Description
**Example**:
```code
// Usage example
```
```

### Update Process
1. Make changes to the knowledge base file
2. Add entry to the changelog section at the top
3. Increment version number if significant changes
4. Commit with message: `docs: Update knowledge base - [brief description]`

## Critical Patterns to Remember

### Configuration Validation (C#)
```csharp
// CORRECT - Method must be named "Validate"
public class MyConfigValidator : AbstractValidator<MyConfig>
{
    public MyConfigValidator()
    {
        RuleFor(x => x.Property).NotEmpty();
    }
}

// WRONG - "IsValid" will not be called
public bool IsValid() { } // ❌ Never use this
```

### Lua Tool Windows
```lua
-- CORRECT - inputs=true enables button clicks
ui.toolWindow("Title", pos, size, true, true, function()
    if ui.button("Click Me") then  -- This works
        doSomething()
    end
end)

-- WRONG - buttons won't respond without inputs parameter
ui.toolWindow("Title", pos, size, function()  -- ❌ Missing inputs=true
    if ui.button("Click Me") then  -- This won't work
        doSomething()
    end
end)
```

### Chat Messages (C#)
```csharp
// CORRECT - Use SessionId 255 for server messages
client.SendPacket(new ChatMessage
{
    SessionId = 255,  // Server message indicator
    Message = "Hello from server"
});

// Access the proper namespace
using AssettoServer.Shared.Network.Packets.Outgoing;
```

### File Naming
```
✅ plugin_sxr_stats_cfg.yml      (correct: _cfg.yml suffix)
❌ plugin_sxr_stats_config.yml   (wrong: _config.yml)
❌ sxr_stats_cfg.yml             (wrong: missing plugin_ prefix)
```

## Project Structure Reference

```
SXRPluginName/
├── SXRPluginName/
│   ├── SXRPluginName.cs           # Main plugin (BackgroundService)
│   ├── SXRPluginNameModule.cs     # Autofac DI registration
│   ├── SXRPluginNameConfiguration.cs  # Config + Validator
│   ├── SXRPluginNameController.cs # HTTP API (if needed)
│   ├── SXRPluginNameCommandModule.cs  # Chat commands (if needed)
│   ├── SXRPluginName.csproj
│   ├── cfg/
│   │   └── plugin_sxr_pluginname_cfg.yml
│   └── lua/
│       └── sxrpluginname.lua      # Client UI (if needed)
├── README.md
└── CHANGELOG.md                   # Required - track all changes
```

## Quick Reference Commands

```bash
# Search knowledge base for specific topic
grep -n "topic" SXR_Development_Knowledge_Base_v6.md

# Find all sections
grep "^##" SXR_Development_Knowledge_Base_v6.md

# Find code examples for a class
grep -A 20 "ClassName" SXR_Development_Knowledge_Base_v6.md

# Check for common mistakes section
grep -A 50 "Common Mistakes" SXR_Development_Knowledge_Base_v6.md
```

## Integration Notes

### Plugin Dependencies
When a plugin depends on another:
1. Check if target plugin exists at runtime (not compile time)
2. Use interfaces for loose coupling
3. Set providers via setter methods, not constructor injection
4. Handle null gracefully when optional dependency missing

Example:
```csharp
// In dependent plugin
private IStatsProvider? _statsProvider;

public void SetStatsProvider(IStatsProvider provider)
{
    _statsProvider = provider;
    Log.Information("Stats provider connected");
}

// Usage - always null-check
int level = _statsProvider?.GetDriverLevel(steamId) ?? 1;
```

### Event Flow
```
Player Connects → ClientConnected event
                → Initialize player data
                → Send welcome message
                → Start tracking

Player Action   → Specific event (Collision, Chat, etc.)
                → Process in handler
                → Update state
                → Notify other plugins if needed

Player Disconnects → ClientDisconnected event
                   → Save player data
                   → Clean up state
                   → Remove from tracking
```

## Remember

1. **Read the knowledge base first** - Most problems are already documented
2. **Check the changelog** - See what's been tried before
3. **Update when you learn** - Future you will thank present you
4. **Follow the patterns** - Consistency prevents bugs
5. **Test the basics** - Buttons click, configs load, packets send


# AssettoServer Plugin Architecture Documentation

This document provides comprehensive documentation on how plugins are developed and registered in the AssettoServer codebase.

## Table of Contents

1. [Repository Structure](#repository-structure)
2. [Plugin Infrastructure](#plugin-infrastructure)
3. [Plugin Loading & Registration Flow](#plugin-loading--registration-flow)
4. [Plugin Development Guide](#plugin-development-guide)
5. [Configuration System](#configuration-system)
6. [Command System](#command-system)
7. [Advanced Plugin Patterns](#advanced-plugin-patterns)
8. [Available Services for Injection](#available-services-for-injection)

---

## Repository Structure

```
AssettoServer_c/
├── AssettoServer/                      # Core server application
│   ├── Server/
│   │   ├── Plugin/                     # Plugin infrastructure (CORE)
│   │   │   ├── ACPluginLoader.cs       # Discovery & loading engine
│   │   │   ├── AssettoServerModule.cs  # Base class for plugins
│   │   │   ├── AvailablePlugin.cs      # Deferred plugin wrapper
│   │   │   ├── IAssettoServerAutostart.cs  # Autostart marker interface
│   │   │   ├── LoadedPlugin.cs         # Runtime plugin metadata
│   │   │   └── PluginConfiguration.cs  # Plugin manifest model
│   │   ├── Configuration/              # Configuration system
│   │   │   ├── ACServerConfiguration.cs
│   │   │   ├── ConfigurationSchemaGenerator.cs
│   │   │   ├── IValidateConfiguration.cs
│   │   │   └── ReferenceConfigurationHelper.cs
│   │   └── ...
│   ├── Commands/                       # Command system
│   │   ├── ACModuleBase.cs             # Base class for command modules
│   │   ├── ChatService.cs              # Command registration & execution
│   │   ├── Contexts/                   # Command context types
│   │   └── Attributes/                 # Permission attributes
│   ├── Startup.cs                      # Application startup & DI
│   └── Program.cs                      # Entry point
├── AssettoServer.Shared/               # Shared utilities and interfaces
└── [Plugin Directories]/               # Plugin implementations
    ├── SamplePlugin/                   # Minimal template plugin
    ├── ReportPlugin/                   # Event handling & HTTP endpoints
    ├── AutoModerationPlugin/           # Background services & Lua injection
    ├── VotingPresetPlugin/             # Voting systems
    └── [11 more plugins...]
```

### Available Plugins (14 total)
- SamplePlugin (template)
- AutoModerationPlugin
- CustomCommandPlugin
- DiscordAuditPlugin
- GeoIPPlugin
- LiveWeatherPlugin
- RaceChallengePlugin
- RandomWeatherPlugin
- ReplayPlugin
- ReportPlugin
- TimeDilationPlugin
- VotingPresetPlugin
- VotingWeatherPlugin
- WordFilterPlugin

---

## Plugin Infrastructure

### Core Infrastructure Files

#### 1. AssettoServerModule.cs
**Location:** `AssettoServer/Server/Plugin/AssettoServerModule.cs`

The base class for all plugins. Inherits from Autofac's `Module` for dependency injection:

```csharp
public abstract class AssettoServerModule : Module
{
    public virtual object? ReferenceConfiguration => null;

    public virtual void ConfigureServices(IServiceCollection services) { }
    public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env) { }
}

public abstract class AssettoServerModule<TConfig> : AssettoServerModule
    where TConfig : new()
{
    public override object? ReferenceConfiguration => new TConfig();
}
```

**Purpose:**
- Inherits from Autofac `Module` for DI registration via `Load()` method
- `ConfigureServices()` hook for ASP.NET Core services
- `Configure()` hook for middleware pipeline
- Generic variant `<TConfig>` enables type-safe configuration with auto-generated reference configs

#### 2. IAssettoServerAutostart.cs
**Location:** `AssettoServer/Server/Plugin/IAssettoServerAutostart.cs`

Marker interface for auto-starting plugins:

```csharp
public interface IAssettoServerAutostart : IHostedService;
```

**Purpose:**
- Inherits from `IHostedService` (provides `StartAsync()` and `StopAsync()`)
- Plugins implementing this are registered as hosted services
- Enables automatic initialization and lifecycle management

#### 3. ACPluginLoader.cs
**Location:** `AssettoServer/Server/Plugin/ACPluginLoader.cs`

The core plugin discovery, loading, and management engine:

```csharp
public class ACPluginLoader
{
    public Dictionary<string, AvailablePlugin> AvailablePlugins { get; } = new();
    public List<LoadedPlugin> LoadedPlugins { get; } = [];

    public ACPluginLoader(bool loadFromWorkdir)
    {
        // Scans ./plugins/ (working dir) and [AppBase]/plugins/
        if (loadFromWorkdir)
        {
            var dir = Path.Join(Directory.GetCurrentDirectory(), "plugins");
            if (Directory.Exists(dir)) ScanDirectory(dir);
            else Directory.CreateDirectory(dir);
        }
        ScanDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));
    }

    private void ScanDirectory(string path)
    {
        foreach (string dir in Directory.GetDirectories(path))
        {
            string dirName = Path.GetFileName(dir);
            string pluginDll = Path.Combine(dir, $"{dirName}.dll");
            if (File.Exists(pluginDll) && !AvailablePlugins.ContainsKey(dirName))
            {
                var loader = PluginLoader.CreateFromAssemblyFile(pluginDll,
                    config => { config.PreferSharedTypes = true; });
                // Load optional configuration.json
                AvailablePlugins.Add(dirName, new AvailablePlugin(config, loader, dir));
            }
        }
    }

    internal void LoadPlugins(List<string> plugins)
    {
        // Phase 1: Load exported assemblies (shared types)
        foreach (var pluginName in plugins)
            AvailablePlugins[pluginName].LoadExportedAssemblies();

        // Phase 2: Load plugins
        foreach (var pluginName in plugins)
            LoadPlugin(pluginName);
    }

    private void LoadPlugin(string name)
    {
        var assembly = AvailablePlugins[name].Load();

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(AssettoServerModule).IsAssignableFrom(type) && !type.IsAbstract)
            {
                var instance = Activator.CreateInstance(type) as AssettoServerModule;

                // Extract configuration type from generic parameter
                Type? configType = null;
                Type? validatorType = null;
                var baseType = type.BaseType!;
                if (baseType.IsGenericType &&
                    baseType.GetGenericTypeDefinition() == typeof(AssettoServerModule<>))
                {
                    configType = baseType.GetGenericArguments()[0];

                    // Find validator from IValidateConfiguration<T>
                    foreach (var iface in configType.GetInterfaces())
                    {
                        if (iface.IsGenericType &&
                            iface.GetGenericTypeDefinition() == typeof(IValidateConfiguration<>))
                        {
                            validatorType = iface.GetGenericArguments()[0];
                        }
                    }
                }

                LoadedPlugins.Add(new LoadedPlugin
                {
                    Name = name,
                    Directory = plugin.Path,
                    Assembly = assembly,
                    Instance = instance,
                    ConfigurationType = configType,
                    ValidatorType = validatorType,
                    ConfigurationFileName = ConfigurationTypeToFilename(configType.Name),
                    // ... schema and reference filenames
                });
            }
        }
    }

    // Converts "SampleConfiguration" -> "plugin_sample_cfg.yml"
    private static string ConfigurationTypeToFilename(string type, string ending = "yml")
    {
        var strat = new SnakeCaseNamingStrategy();
        type = type.Replace("Configuration", "Cfg");
        return $"plugin_{strat.GetPropertyName(type, false)}.{ending}";
    }
}
```

**Key Features:**
- Uses McMaster.NETCore.Plugins for isolated assembly loading
- Two-phase loading: exported assemblies first, then plugins
- Reflection-based discovery of `AssettoServerModule` implementations
- Automatic configuration type and validator type detection
- Snake_case file naming convention

#### 4. LoadedPlugin.cs
**Location:** `AssettoServer/Server/Plugin/LoadedPlugin.cs`

Runtime representation of a loaded plugin:

```csharp
public class LoadedPlugin
{
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required Assembly Assembly { get; init; }
    public required AssettoServerModule Instance { get; init; }

    [MemberNotNullWhen(true, nameof(ConfigurationFileName), nameof(SchemaFileName),
        nameof(ConfigurationType), nameof(ReferenceConfigurationFileName),
        nameof(ReferenceConfiguration))]
    public bool HasConfiguration => ConfigurationType != null;

    public Type? ConfigurationType { get; init; }
    public Type? ValidatorType { get; init; }
    public string? ConfigurationFileName { get; init; }      // plugin_sample_cfg.yml
    public string? SchemaFileName { get; init; }             // plugin_sample_cfg.schema.json
    public string? ReferenceConfigurationFileName { get; init; }  // plugin_sample_cfg.reference.yml
    public object? ReferenceConfiguration { get; init; }
}
```

#### 5. AvailablePlugin.cs
**Location:** `AssettoServer/Server/Plugin/AvailablePlugin.cs`

Wrapper for discovered but not-yet-loaded plugins:

```csharp
public class AvailablePlugin
{
    private readonly PluginConfiguration _configuration;
    private readonly PluginLoader _loader;
    public string Path { get; }

    public Assembly Load() => _loader.LoadDefaultAssembly();

    public void LoadExportedAssemblies()
    {
        foreach (var assemblyName in _configuration.ExportedAssemblies)
        {
            var fullPath = System.IO.Path.Combine(Path, assemblyName);
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }
}
```

#### 6. PluginConfiguration.cs
**Location:** `AssettoServer/Server/Plugin/PluginConfiguration.cs`

Plugin manifest stored in `configuration.json`:

```csharp
public class PluginConfiguration
{
    public List<string> ExportedAssemblies { get; init; } = [];
}
```

---

## Plugin Loading & Registration Flow

### Startup.cs Integration

#### Constructor - Plugin Loader Initialization
```csharp
public Startup(ACServerConfiguration configuration)
{
    _configuration = configuration;
    _loader = new ACPluginLoader(configuration.LoadPluginsFromWorkdir);
}
```

#### ConfigureContainer - Dependency Injection Setup
```csharp
public void ConfigureContainer(ContainerBuilder builder)
{
    // Optional: Generate configs for all plugins
    if (_configuration.GeneratePluginConfigs)
    {
        var loader = new ACPluginLoader(_configuration.LoadPluginsFromWorkdir);
        loader.LoadPlugins(loader.AvailablePlugins.Select(p => p.Key).ToList());
        _configuration.LoadPluginConfiguration(loader, null);
    }

    // Register plugins with Autofac
    foreach (var plugin in _loader.LoadedPlugins)
    {
        if (plugin.ConfigurationType != null)
            builder.RegisterType(plugin.ConfigurationType).AsSelf();
        builder.RegisterModule(plugin.Instance);  // Register as Autofac Module
    }

    // Load plugin configurations
    _configuration.LoadPluginConfiguration(_loader, builder);
}
```

#### ConfigureServices - ASP.NET Core Services
```csharp
public void ConfigureServices(IServiceCollection services)
{
    var mvcBuilder = services.AddControllers();

    if (_configuration.Extra.EnablePlugins != null)
    {
        _loader.LoadPlugins(_configuration.Extra.EnablePlugins);

        foreach (var plugin in _loader.LoadedPlugins)
        {
            plugin.Instance.ConfigureServices(services);
            mvcBuilder.AddApplicationPart(plugin.Assembly);  // Enable controller discovery
        }
    }
}
```

#### Configure - Middleware Pipeline
```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    foreach (var plugin in _loader.LoadedPlugins)
    {
        // Serve static files from plugin wwwroot
        var wwwrootPath = Path.Combine(plugin.Directory, "wwwroot");
        if (Directory.Exists(wwwrootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath),
                RequestPath = $"/static/{plugin.Name}",
                ServeUnknownFileTypes = true,
            });
        }

        plugin.Instance.Configure(app, env);
    }
}
```

### Complete Plugin Lifecycle

```
1. DISCOVERY PHASE (Constructor)
   ├── Scans ./plugins/ and [AppBase]/plugins/
   ├── Looks for pattern: plugins/[Name]/[Name].dll
   ├── Reads optional configuration.json
   └── Creates AvailablePlugin instances (lazy)

2. LOADING PHASE (ConfigureServices)
   ├── Reads EnablePlugins from config
   ├── Phase 1: Load exported assemblies into default context
   └── Phase 2: Load plugin assemblies and reflect for AssettoServerModule

3. DI REGISTRATION PHASE (ConfigureContainer)
   ├── Register configuration types
   ├── Register plugin modules (calls Load() method)
   └── Load and validate configuration files

4. SERVICE CONFIGURATION PHASE (ConfigureServices)
   ├── Call plugin.ConfigureServices(services)
   └── Add plugin assemblies as MVC application parts

5. PIPELINE CONFIGURATION PHASE (Configure)
   ├── Register static file providers for wwwroot
   └── Call plugin.Configure(app, env)

6. RUNTIME PHASE
   ├── Autofac activates registered services
   ├── IAssettoServerAutostart services start automatically
   └── Plugin background services begin execution
```

---

## Plugin Development Guide

### Minimal Plugin Structure

```
MyPlugin/
├── MyPlugin.cs                     # Main plugin class (IAssettoServerAutostart)
├── MyPluginModule.cs               # DI registration (AssettoServerModule)
├── MyPluginConfiguration.cs        # Configuration schema
├── MyPluginConfigurationValidator.cs  # FluentValidation rules
├── MyPluginCommandModule.cs        # Chat commands (optional)
├── MyPluginController.cs           # HTTP endpoints (optional)
└── MyPlugin.csproj                 # Project file
```

### Step-by-Step Implementation

#### 1. Project File (MyPlugin.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <SelfContained>false</SelfContained>
        <DebugType>embedded</DebugType>
        <PublishDir>..\out-$(RuntimeIdentifier)\plugins\$(MSBuildProjectName)\</PublishDir>
    </PropertyGroup>

    <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <OutputPath>..\AssettoServer\bin\$(Configuration)\$(TargetFramework)\plugins\$(MSBuildProjectName)</OutputPath>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\AssettoServer.Shared\AssettoServer.Shared.csproj">
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </ProjectReference>
        <ProjectReference Include="..\AssettoServer\AssettoServer.csproj">
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </ProjectReference>
    </ItemGroup>
</Project>
```

**Key Settings:**
- `EnableDynamicLoading=true` - Required for plugin isolation
- `SelfContained=false` - Uses host's runtime
- `<Private>false</Private>` - Prevents duplicating server assemblies

#### 2. Configuration Class

```csharp
using JetBrains.Annotations;
using YamlDotNet.Serialization;
using AssettoServer.Server.Configuration;

namespace MyPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class MyPluginConfiguration : IValidateConfiguration<MyPluginConfigurationValidator>
{
    [YamlMember(Description = "Enable the plugin feature")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Description = "Timeout in seconds")]
    public int TimeoutSeconds { get; init; } = 30;

    [YamlMember(Description = "Webhook URL for notifications")]
    public string? WebhookUrl { get; init; }

    [YamlIgnore]  // Computed property, not serialized
    public int TimeoutMilliseconds => TimeoutSeconds * 1000;
}
```

**Output File:** `plugin_my_plugin_cfg.yml`

#### 3. Configuration Validator

```csharp
using FluentValidation;
using JetBrains.Annotations;

namespace MyPlugin;

[UsedImplicitly]
public class MyPluginConfigurationValidator : AbstractValidator<MyPluginConfiguration>
{
    public MyPluginConfigurationValidator()
    {
        RuleFor(cfg => cfg.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("Timeout must be positive");

        RuleFor(cfg => cfg.WebhookUrl)
            .Must(url => url == null || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("WebhookUrl must be a valid URL");
    }
}
```

#### 4. Module Class (DI Registration)

```csharp
using Autofac;
using AssettoServer.Server.Plugin;

namespace MyPlugin;

public class MyPluginModule : AssettoServerModule<MyPluginConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MyPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();

        // Register additional services
        builder.RegisterType<MyPluginService>().AsSelf().SingleInstance();
    }
}
```

#### 5. Main Plugin Class

```csharp
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MyPlugin;

public class MyPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly MyPluginConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;

    public MyPlugin(
        MyPluginConfiguration configuration,
        EntryCarManager entryCarManager,
        IHostApplicationLifetime applicationLifetime)
        : base(applicationLifetime)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;

        // Subscribe to events
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;

        Log.Information("MyPlugin initialized with timeout: {Timeout}s",
            configuration.TimeoutSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Debug("MyPlugin background service started");

        // Background work loop
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Periodic work here
        }
    }

    private void OnClientConnected(ACTcpClient sender, EventArgs args)
    {
        Log.Information("Player connected: {Name}", sender.Name);
    }

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args)
    {
        Log.Information("Player disconnected: {Name}", sender.Name);
    }
}
```

#### 6. Command Module (Optional)

```csharp
using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Tcp;
using Qmmands;

namespace MyPlugin;

public class MyPluginCommandModule : ACModuleBase
{
    private readonly MyPluginConfiguration _configuration;
    private readonly MyPlugin _plugin;

    public MyPluginCommandModule(MyPluginConfiguration configuration, MyPlugin plugin)
    {
        _configuration = configuration;
        _plugin = plugin;
    }

    [Command("myplugin")]
    public void MyPluginInfo()
    {
        Reply($"MyPlugin is {(_configuration.Enabled ? "enabled" : "disabled")}");
    }

    [Command("greet")]
    public void Greet([Remainder] string name)
    {
        Reply($"Hello, {name}!");
    }

    [Command("kick"), RequireAdmin]
    public async Task Kick(ACTcpClient player, [Remainder] string? reason = null)
    {
        if (player.SessionId == Client?.SessionId)
        {
            Reply("You cannot kick yourself.");
            return;
        }

        await _entryCarManager.KickAsync(player, reason, Client);
        Broadcast($"{player.Name} was kicked: {reason ?? "No reason"}");
    }

    [Command("adminonly"), RequireAdmin]
    public void AdminOnly()
    {
        Reply("This command is admin-only!");
    }

    [Command("chatonly"), RequireConnectedPlayer]
    public void ChatOnly()
    {
        Reply($"Hello {Client!.Name}, this only works in chat!");
    }
}
```

#### 7. HTTP Controller (Optional)

```csharp
using AssettoServer.Server;
using Microsoft.AspNetCore.Mvc;

namespace MyPlugin;

[ApiController]
public class MyPluginController : ControllerBase
{
    private readonly MyPlugin _plugin;
    private readonly MyPluginConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;

    public MyPluginController(
        MyPlugin plugin,
        MyPluginConfiguration configuration,
        EntryCarManager entryCarManager)
    {
        _plugin = plugin;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }

    [HttpGet("/myplugin")]
    public IActionResult GetStatus()
    {
        return Ok(new { Enabled = _configuration.Enabled });
    }

    [HttpGet("/myplugin/players")]
    public IActionResult GetPlayers()
    {
        var players = _entryCarManager.ConnectedCars.Values
            .Where(c => c.Client != null)
            .Select(c => new { c.Client!.Name, c.Client.Guid, c.SessionId });
        return Ok(players);
    }

    [HttpPost("/myplugin/broadcast")]
    public IActionResult Broadcast([FromBody] string message)
    {
        _entryCarManager.BroadcastChat(message);
        return Ok();
    }
}
```

---

## Configuration System

### Configuration Flow

1. **Definition**: POCO class with `[YamlMember]` attributes implementing `IValidateConfiguration<T>`
2. **Loading**: YAML deserialization from `plugin_*.yml` files
3. **Validation**: FluentValidation framework for rule-based validation
4. **Generation**: Automatic JSON Schema generation for IDE IntelliSense
5. **DI Registration**: Autofac `RegisterInstance()` for constructor injection

### Configuration File Locations

```
cfg/
├── plugin_my_plugin_cfg.yml              # Actual config (user edits this)
├── schemas/
│   └── plugin_my_plugin_cfg.schema.json  # JSON Schema for IDE
└── reference/
    └── plugin_my_plugin_cfg.reference.yml  # Reference with all defaults
```

### YAML File Format

```yaml
# yaml-language-server: $schema=../../schemas/plugin_my_plugin_cfg.schema.json

Enabled: true
TimeoutSeconds: 30
WebhookUrl: https://discord.com/api/webhooks/...
```

### Configuration Loading Code

```csharp
// From ACServerConfiguration.LoadPluginConfiguration()
internal void LoadPluginConfiguration(ACPluginLoader loader, ContainerBuilder? builder)
{
    foreach (var plugin in loader.LoadedPlugins)
    {
        if (!plugin.HasConfiguration) continue;

        var configPath = Path.Join(BaseFolder, plugin.ConfigurationFileName);

        // Generate schema for IDE support
        var schemaPath = ConfigurationSchemaGenerator.WritePluginConfigurationSchema(plugin);
        ReferenceConfigurationHelper.WriteReferenceConfiguration(...);

        if (File.Exists(configPath) && builder != null)
        {
            // Load existing config
            var deserializer = new DeserializerBuilder().Build();
            using var file = File.OpenText(configPath);
            var configObj = deserializer.Deserialize(file, plugin.ConfigurationType)!;

            ValidatePluginConfiguration(plugin, configObj);
            builder.RegisterInstance(configObj).AsSelf();
        }
        else
        {
            // Create default config
            var serializer = new SerializerBuilder().Build();
            using var file = File.CreateText(configPath);
            var configObj = Activator.CreateInstance(plugin.ConfigurationType)!;
            serializer.Serialize(file, configObj, plugin.ConfigurationType);
        }
    }
}
```

---

## Command System

### Architecture Overview

The command system is built on **Qmmands**, a command parsing framework. Commands are organized into modules that inherit from `ACModuleBase`.

### ACModuleBase

```csharp
public class ACModuleBase : ModuleBase<BaseCommandContext>
{
    public ACTcpClient? Client => (Context as ChatCommandContext)?.Client;

    public void Reply(string message) => Context.Reply(message);
    public void Broadcast(string message) => Context.Broadcast(message);
}
```

### Command Contexts

| Context | Description | IsAdministrator |
|---------|-------------|-----------------|
| `ChatCommandContext` | Player chat commands | Based on player |
| `RconCommandContext` | Remote console | Always true |

### Command Attributes

| Attribute | Purpose |
|-----------|---------|
| `[Command("name")]` | Command name (supports aliases) |
| `[RequireAdmin]` | Admin-only command |
| `[RequireConnectedPlayer]` | Chat-only (not RCON) |
| `[Remainder]` | Captures rest of input as single string |

### Player Parameter Parsing

The `ACClientTypeParser` automatically parses player references:

```csharp
// By session ID: /kick 0
// By Steam ID:   /kick 76561198123456789
// By name:       /kick PlayerName
// Partial match: /kick @Play

[Command("kick")]
public Task Kick(ACTcpClient player, [Remainder] string? reason = null)
```

### Command Registration

Commands are automatically discovered from:
1. Main assembly: `_commandService.AddModules(Assembly.GetEntryAssembly())`
2. Plugin assemblies: `_commandService.AddModules(plugin.Assembly)`

### Command Execution Flow

```
1. Player sends: /mycommand arg1 arg2
2. ChatService.OnChatMessageReceived detects "/" prefix
3. ChatCommandContext created with player info
4. CommandService.ExecuteAsync(command, context)
5. Qmmands routes to appropriate module/method
6. Checks run (RequireAdmin, RequireConnectedPlayer)
7. Type parsers convert string args to types
8. Command method executes
9. Reply/Broadcast sends response
```

---

## Advanced Plugin Patterns

### 1. Event-Driven Architecture (ReportPlugin)

```csharp
public class ReportPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public ReportPlugin(EntryCarManager entryCarManager, ChatService chatService, ...)
    {
        // Subscribe to server events
        entryCarManager.ClientConnected += (sender, _) =>
            sender.FirstUpdateSent += OnClientFirstUpdateSent;
        entryCarManager.ClientDisconnected += OnClientDisconnected;
        chatService.MessageReceived += OnChatMessage;
    }

    private void OnClientFirstUpdateSent(ACTcpClient sender, EventArgs args)
    {
        _events.Enqueue(new PlayerConnectedAuditEvent(new AuditClient(sender)));
        DeleteOldEvents();  // Circular buffer cleanup
    }
}
```

### 2. Per-Entity State Management (AutoModerationPlugin)

```csharp
public class AutoModerationPlugin : CriticalBackgroundService
{
    private readonly List<EntryCarAutoModeration> _instances = [];
    private readonly Func<EntryCar, EntryCarAutoModeration> _factory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create state instance per car
        foreach (var entryCar in _entryCarManager.EntryCars)
            _instances.Add(_factory(entryCar));

        // Periodic update loop
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var instance in _instances)
                instance.Update();
        }
    }
}
```

### 3. Lua Script Injection (AutoModerationPlugin)

```csharp
public AutoModerationPlugin(CSPServerScriptProvider scriptProvider, ...)
{
    // Load embedded Lua script
    using var stream = Assembly.GetExecutingAssembly()
        .GetManifestResourceStream("AutoModerationPlugin.lua.automoderation.lua")!;
    using var reader = new StreamReader(stream);
    scriptProvider.AddScript(reader.ReadToEnd(), "automoderation.lua");
}
```

**Lua Script (automoderation.lua):**
```lua
local NoLights = 1
local NoParking = 2
local WrongWay = 4

ac.onSharedEvent("AutoModeration", function(data)
    local flags = ac.structBytes(data)
    if bit.band(flags, NoLights) ~= 0 then
        -- Draw no lights warning
    end
end)
```

### 4. HTTP Endpoint with File Upload (ReportPlugin)

```csharp
[ApiController]
public class ReportController : ControllerBase
{
    [HttpPost("/report")]
    public async Task<ActionResult> PostReport(
        Guid key,
        [FromHeader(Name = "X-Car-Index")] int sessionId)
    {
        // Validate key and session
        var client = _entryCarManager.EntryCars[sessionId].Client;
        if (client == null) return BadRequest();

        // Rate limiting
        if (DateTime.Now - _lastSubmission < TimeSpan.FromSeconds(30))
            return StatusCode(429, "Rate limited");

        // Save uploaded file
        var filePath = Path.Combine("reports", $"{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        using var file = System.IO.File.Create(filePath);
        await Request.Body.CopyToAsync(file);

        return Ok();
    }
}
```

### 5. Voting System (VotingPresetPlugin)

```csharp
public class VotingPresetPlugin : CriticalBackgroundService
{
    private List<PresetChoice> _availablePresets = [];
    private readonly HashSet<ACTcpClient> _alreadyVoted = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for voting interval
            await Task.Delay(_configuration.VotingIntervalMilliseconds, stoppingToken);

            // Start voting
            await VotingAsync();

            // Apply winner
            var winner = _availablePresets.OrderByDescending(p => p.Votes).First();
            await ApplyPreset(winner.Preset);
        }
    }

    public void Vote(ACTcpClient client, int choice)
    {
        if (_alreadyVoted.Contains(client)) return;

        _availablePresets[choice].Votes++;
        _alreadyVoted.Add(client);
        client.SendChatMessage($"Vote registered for: {_availablePresets[choice].Preset.Name}");
    }
}
```

### 6. Complex Configuration with Nested Objects

```csharp
public class AutoModerationConfiguration : IValidateConfiguration<AutoModerationConfigurationValidator>
{
    public AfkPenaltyConfiguration AfkPenalty { get; init; } = new();
    public HighPingPenaltyConfiguration HighPingPenalty { get; init; } = new();
    public WrongWayPenaltyConfiguration WrongWayPenalty { get; init; } = new();
}

public class AfkPenaltyConfiguration
{
    [YamlMember(Description = "Enable AFK kick")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Description = "Minutes before kick")]
    public int DurationMinutes { get; init; } = 10;

    [YamlIgnore]
    public int DurationMilliseconds => DurationMinutes * 60 * 1000;
}
```

**Validator with nested rules:**
```csharp
public class AutoModerationConfigurationValidator : AbstractValidator<AutoModerationConfiguration>
{
    public AutoModerationConfigurationValidator()
    {
        RuleFor(cfg => cfg.AfkPenalty).ChildRules(afk =>
        {
            afk.RuleFor(x => x.DurationMinutes)
                .GreaterThan(0)
                .When(x => x.Enabled);
        });
    }
}
```

---

## Available Services for Injection

### Core Server Services

| Service | Purpose |
|---------|---------|
| `ACServer` | Main server instance |
| `EntryCarManager` | Player/car management, events |
| `SessionManager` | Session state management |
| `WeatherManager` | Weather control |
| `ChatService` | Chat events and commands |
| `CSPServerScriptProvider` | Inject Lua scripts |
| `CSPClientMessageTypeManager` | Handle CSP client messages |
| `CSPFeatureManager` | Declare CSP features |
| `IHostApplicationLifetime` | Application lifecycle |
| `ACServerConfiguration` | Server configuration |

### EntryCarManager Events

```csharp
entryCarManager.ClientConnected += (sender, args) => { };
entryCarManager.ClientDisconnected += (sender, args) => { };

// On sender (ACTcpClient):
sender.FirstUpdateSent += (client, args) => { };
sender.LoggedInAsAdministrator += (client, args) => { };
```

### ChatService Events

```csharp
chatService.MessageReceived += (sender, args) => {
    var client = args.Client;
    var message = args.Message;
};
```

### Broadcasting

```csharp
// Chat messages
entryCarManager.BroadcastChat("Server message");

// To specific player
client.SendChatMessage("Private message");

// Binary packets
entryCarManager.BroadcastPacket(new MyPacket { ... });
client.SendPacket(new MyPacket { ... });
```

---

## Summary

The AssettoServer plugin system provides:

1. **Robust Discovery** - Automatic plugin detection from directory structure
2. **Assembly Isolation** - McMaster.NETCore.Plugins for clean loading
3. **Full DI Integration** - Autofac container with constructor injection
4. **Type-Safe Configuration** - YAML with validation and schema generation
5. **Multiple Extension Points**:
   - Background services (`IAssettoServerAutostart`)
   - Chat commands (`ACModuleBase`)
   - HTTP endpoints (`ControllerBase`)
   - Event subscriptions
   - Lua script injection
6. **Lifecycle Management** - Integrated with .NET hosted services

### Quick Reference

| To do this... | Inherit from / Implement |
|---------------|-------------------------|
| Create a plugin | `AssettoServerModule<TConfig>` |
| Auto-start service | `IAssettoServerAutostart` + `CriticalBackgroundService` |
| Add configuration | `IValidateConfiguration<TValidator>` |
| Add chat commands | `ACModuleBase` |
| Add HTTP endpoints | `ControllerBase` |
| Validate config | `AbstractValidator<TConfig>` |

### File Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Plugin DLL | `plugins/{Name}/{Name}.dll` | `plugins/MyPlugin/MyPlugin.dll` |
| Configuration | `plugin_{snake_name}_cfg.yml` | `plugin_my_plugin_cfg.yml` |
| Schema | `plugin_{snake_name}_cfg.schema.json` | `plugin_my_plugin_cfg.schema.json` |
| Reference | `plugin_{snake_name}_cfg.reference.yml` | `plugin_my_plugin_cfg.reference.yml` |
