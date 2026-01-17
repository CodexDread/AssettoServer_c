# SXR Development Knowledge Base v5

## Overview

This document provides comprehensive technical reference for developing plugins and modifications for the AssettoServer codebase used in the SXR (Shuto Expressway Revival) project. It covers:

**Server-Side Development (C#)**
- Server architecture and core components
- Plugin development patterns
- Network protocol and packet handling
- AI traffic system

**Client-Side Development (CSP Lua)**
- Online script development
- Regular Lua app creation
- UI SDK reference
- Troubleshooting and debugging

---

# Table of Contents

## Part 1: Server-Side Development (C#)
1. [Architecture Overview](#architecture-overview)
2. [Core Server Components](#core-server-components)
3. [Network Protocol](#network-protocol)
4. [Plugin Development](#plugin-development)
5. [Configuration System](#configuration-system)
6. [Command System](#command-system)
7. [Event System](#event-system)
8. [AI Traffic System](#ai-traffic-system)
9. [C# Patterns & Idioms](#c-patterns--idioms)
10. [Common Development Tasks](#common-development-tasks)
11. [Debugging & Testing](#debugging--testing)
12. [Performance Considerations](#performance-considerations)

## Part 2: Client-Side Development (CSP Lua)
13. [CSP Lua Script Types](#csp-lua-script-types)
14. [CSP Lua Troubleshooting](#csp-lua-troubleshooting)
15. [CSP Lua SDK Reference](#csp-lua-sdk-reference)
16. [Regular Lua Apps](#regular-lua-apps)
17. [Online Script Development](#online-script-development)
18. [Server Script Configuration](#server-script-configuration)
19. [CSP Development Environment](#csp-development-environment)
20. [Lua Code Patterns & Examples](#lua-code-patterns--examples)
21. [Lua Debugging Techniques](#lua-debugging-techniques)
22. [CSP Resources & References](#csp-resources--references)

---

# Architecture Overview

## Solution Structure

```
AssettoServer_c/
├── AssettoServer/                 # Core server application (main executable)
│   ├── Commands/                  # Chat command system
│   ├── Network/                   # TCP, UDP, HTTP, RCON networking
│   ├── Server/                    # Core server logic
│   │   ├── Ai/                    # Traffic AI system
│   │   ├── Configuration/         # Configuration loading/validation
│   │   ├── Plugin/                # Plugin infrastructure
│   │   ├── Weather/               # Weather system
│   │   └── [Services]             # Various server services
│   ├── Utils/                     # Utility classes
│   └── Vendor/                    # Third-party code
├── AssettoServer.Shared/          # Shared library (packets, models, utilities)
│   ├── Network/                   # Packet definitions
│   │   └── Packets/               # All packet types
│   ├── Model/                     # Shared data models
│   └── Utils/                     # Shared utilities
├── [Plugin Directories]/          # Plugin implementations
└── ACDyno/                        # Car analysis tool (separate app)
```

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Autofac` | Dependency injection container |
| `Qmmands` | Command parsing and execution |
| `FluentValidation` | Configuration validation |
| `YamlDotNet` | YAML configuration parsing |
| `Serilog` | Structured logging |
| `McMaster.NETCore.Plugins` | Plugin assembly loading |
| `CommunityToolkit.Mvvm` | MVVM helpers (ObservableObject) |
| `Prometheus-net` | Metrics exposure |

## Application Startup Flow

```
Program.cs
    └── Startup.cs
        ├── ConfigureContainer(ContainerBuilder)  // Autofac DI registration
        ├── ConfigureServices(IServiceCollection) // ASP.NET Core services
        └── Configure(IApplicationBuilder)        // HTTP middleware pipeline
```

---

# Core Server Components

## Primary Services

### ACServer
**File:** `AssettoServer/Server/ACServer.cs`

Main server orchestrator. Implements `IHostedService` for lifecycle management.

```csharp
public class ACServer : IHostedService
{
    public Task StartAsync(CancellationToken ct);  // Server initialization
    public Task StopAsync(CancellationToken ct);   // Graceful shutdown
}
```

### EntryCarManager
**File:** `AssettoServer/Server/EntryCarManager.cs`

Manages all player slots and car connections. Central hub for player events.

```csharp
public class EntryCarManager
{
    // Properties
    public EntryCar[] EntryCars { get; }           // All car slots
    internal ConcurrentDictionary<int, EntryCar> ConnectedCars { get; }
    
    // Events
    public event EventHandler<ACTcpClient, EventArgs>? ClientConnected;
    public event EventHandler<ACTcpClient, EventArgs>? ClientDisconnected;
    public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientKicked;
    public event EventHandler<ACTcpClient, ClientAuditEventArgs>? ClientBanned;
    
    // Methods
    public async Task KickAsync(ACTcpClient? client, string? reason = null, ACTcpClient? admin = null);
    public async Task BanAsync(ACTcpClient? client, string? reason = null, ACTcpClient? admin = null);
    public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient? sender = null);
    public void BroadcastPacketUdp<TPacket>(in TPacket packet, ...);
    public void BroadcastChat(string message, byte senderId = 255);
}
```

### SessionManager
**File:** `AssettoServer/Server/SessionManager.cs`

Controls game session state (practice, qualifying, race).

```csharp
public class SessionManager
{
    public long ServerTimeMilliseconds { get; }     // Current server time
    public bool IsOpen { get; }                     // Can players join?
    
    public bool NextSession();                      // Move to next session
    public bool RestartSession();                   // Restart current session
    public void SendCurrentSession(ACTcpClient client);  // Send session info to client
}
```

### WeatherManager
**File:** `AssettoServer/Server/Weather/WeatherManager.cs`

Controls in-game weather and time of day.

```csharp
public class WeatherManager
{
    public WeatherData CurrentWeather { get; }
    
    public void SetTime(int secondsFromMidnight);
    public void SetCspWeather(WeatherFxType type, int transitionDuration);
    public bool SetWeatherConfiguration(int id);
    public void SendWeather();
}
```

### ChatService
**File:** `AssettoServer/Commands/ChatService.cs`

Handles chat messages and command processing.

```csharp
public class ChatService
{
    // Events
    public event EventHandler<ACTcpClient, ChatEventArgs>? MessageReceived;
    
    // Methods
    public async Task ProcessCommandAsync(BaseCommandContext context, string command);
}
```

### CSPServerScriptProvider
**File:** `AssettoServer/Server/CSPServerScriptProvider.cs`

Injects Lua scripts to clients with CSP (Custom Shaders Patch).

```csharp
public class CSPServerScriptProvider
{
    public void AddScript(string script, string name);  // Add Lua script
    public void RemoveScript(string name);               // Remove script
}
```

### CSPClientMessageTypeManager
**File:** `AssettoServer/Server/CSPClientMessageTypeManager.cs`

Registers handlers for CSP client-to-server messages.

```csharp
public class CSPClientMessageTypeManager
{
    public void RegisterClientMessageType<T>(ushort type) where T : ICSPClientMessage;
}
```

---

# Network Protocol

## Protocol Overview

AssettoServer uses three network channels:

| Channel | Protocol | Purpose |
|---------|----------|---------|
| Game | TCP | Reliable data: handshake, chat, session state, events |
| Game | UDP | Real-time data: position updates, car states |
| HTTP | HTTP/HTTPS | Server info API, lobby registration, content delivery |
| RCON | TCP | Remote console administration |

## Packet IDs (ACServerProtocol)

**File:** `AssettoServer.Shared/Network/Packets/Protocol.cs`

```csharp
public enum ACServerProtocol : byte
{
    // Connection
    RequestNewConnection   = 0x3D,  // Client handshake request
    NewCarConnection       = 0x3E,  // Server handshake response
    CleanExitDrive         = 0x43,  // Clean client disconnect
    CarDisconnected        = 0x4D,  // Notify others of disconnect
    CarConnected           = 0x5A,  // Notify others of connect
    
    // Authentication
    Blacklisted            = 0x3B,
    WrongPassword          = 0x3C,
    NoSlotsAvailable       = 0x45,
    SessionClosed          = 0x6E,
    AuthFailed             = 0x6F,
    UnsupportedProtocol    = 0x42,
    
    // Game State
    CarList                = 0x40,
    PositionUpdate         = 0x46,
    CurrentSessionUpdate   = 0x4A,
    SunAngleUpdate         = 0x54,
    WeatherUpdate          = 0x78,
    
    // Events
    Chat                   = 0x47,
    LapCompleted           = 0x49,
    SectorSplit            = 0x58,
    DamageUpdate           = 0x56,
    TyreCompoundChange     = 0x50,
    
    // Admin
    KickCar                = 0x68,
    
    // CSP Extended
    Extended               = 0xAB,
}
```

## CSP Message Types

```csharp
public enum CSPMessageTypeTcp : byte
{
    SpectateCar          = 0x00,
    CarVisibilityUpdate  = 0x02,
    ClientMessage        = 0x03,  // Custom plugin messages
    SystemMessage        = 0x04,
    KickBanMessage       = 0x05,
}

public enum CSPMessageTypeUdp : byte
{
    WeatherUpdate        = 0x01,
    CustomUpdate         = 0x03,
    ClientMessage        = 0x05,
}
```

## Packet Structure

### Reading Packets (PacketReader)

**File:** `AssettoServer.Shared/Network/Packets/PacketReader.cs`

```csharp
public struct PacketReader
{
    // Core reading methods
    public T Read<T>() where T : unmanaged;          // Read primitive/struct
    public string ReadUTF8String(bool bigLength = false);  // Read length-prefixed UTF8
    public string ReadUTF32String(bool bigLength = false); // Read UTF32 (AC native)
    public void ReadBytes(Memory<byte> buffer);      // Read raw bytes
    
    // Packet deserialization
    public TPacket ReadPacket<TPacket>() where TPacket : IIncomingNetworkPacket, new();
}

// Incoming packet interface
public interface IIncomingNetworkPacket
{
    void FromReader(PacketReader reader);
}
```

### Writing Packets (PacketWriter)

**File:** `AssettoServer.Shared/Network/Packets/PacketWriter.cs`

```csharp
public struct PacketWriter
{
    // Core writing methods
    public void Write<T>(T value) where T : struct;  // Write primitive/struct
    public void WriteUTF8String(string? str, bool bigLength = false);
    public void WriteUTF32String(string? str, bool bigLength = false);
    public void WriteBytes(ReadOnlySpan<byte> bytes);
    
    // Packet serialization
    public int WritePacket<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket;
}

// Outgoing packet interface
public interface IOutgoingNetworkPacket
{
    void ToWriter(ref PacketWriter writer);
}
```

### Example Packet Implementation

```csharp
// Outgoing packet
public readonly struct BallastUpdate : IOutgoingNetworkPacket
{
    public byte SessionId { get; init; }
    public float BallastKg { get; init; }
    public int Restrictor { get; init; }
    
    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.BoPUpdate);
        writer.Write(SessionId);
        writer.Write(BallastKg);
        writer.Write(Restrictor);
    }
}

// Incoming packet
public struct HandshakeRequest : IIncomingNetworkPacket
{
    public ushort ClientVersion;
    public ulong Guid;
    public string Name;
    public string RequestedCar;
    // ...
    
    public void FromReader(PacketReader reader)
    {
        ClientVersion = reader.Read<ushort>();
        Guid = reader.Read<ulong>();
        Name = reader.ReadUTF32String();
        RequestedCar = reader.ReadUTF8String();
        // ...
    }
}
```

## ACTcpClient

**File:** `AssettoServer/Network/Tcp/ACTcpClient.cs`

Represents a connected player. Key members:

```csharp
public class ACTcpClient : IClient
{
    // Identity
    public byte SessionId { get; set; }
    public string? Name { get; set; }
    public ulong Guid { get; internal set; }         // Steam ID
    public string HashedGuid { get; }                // Privacy-safe ID
    public bool IsAdministrator { get; internal set; }
    
    // State
    public EntryCar EntryCar { get; internal set; }  // Associated car slot
    public bool IsConnected { get; set; }
    public bool HasSentFirstUpdate { get; }          // Player is fully loaded
    public int? CSPVersion { get; }                  // Client CSP version
    
    // Networking
    public TcpClient TcpClient { get; }
    public ILogger Logger { get; }
    
    // Events (subscribe in ClientConnected handler)
    public event EventHandler<ACTcpClient, EventArgs>? ChecksumPassed;
    public event EventHandler<ACTcpClient, EventArgs>? FirstUpdateSent;
    public event EventHandler<ACTcpClient, ChatMessageEventArgs>? ChatMessageReceived;
    public event EventHandler<ACTcpClient, CollisionEventArgs>? Collision;
    public event EventHandler<ACTcpClient, LapCompletedEventArgs>? LapCompleted;
    public event EventHandler<ACTcpClient, EventArgs>? Disconnecting;
    public event EventHandler<ACTcpClient, EventArgs>? LoggedInAsAdministrator;
    
    // Methods
    public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendChatMessage(string message) => SendPacket(new ChatMessage { SessionId = 255, Message = message });
    public Task DisconnectAsync();
}
```

---

# Plugin Development

## Plugin Structure

```
MyPlugin/
├── MyPlugin.csproj           # Project file
├── MyPluginModule.cs         # Autofac module (DI registration)
├── MyPlugin.cs               # Main plugin class
├── MyPluginConfiguration.cs  # Configuration model
├── MyPluginConfigValidator.cs # Optional: FluentValidation
├── MyCommandModule.cs        # Optional: Chat commands
├── MyController.cs           # Optional: HTTP endpoints
├── lua/
│   └── myplugin.lua          # Optional: CSP Lua scripts
└── wwwroot/                   # Optional: Static files
```

## Minimum Viable Plugin

```csharp
// MyPluginModule.cs
public class MyPluginModule : AssettoServerModule<MyPluginConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MyPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()  // Auto-start as background service
            .SingleInstance();
    }
}

// MyPluginConfiguration.cs
public class MyPluginConfiguration
{
    [YamlMember(Description = "Enable the plugin")]
    public bool Enabled { get; init; } = true;
    
    [YamlMember(Description = "Some numeric setting")]
    public int SomeSetting { get; init; } = 100;
}

// MyPlugin.cs
public class MyPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly MyPluginConfiguration _config;
    private readonly EntryCarManager _entryCarManager;
    
    public MyPlugin(
        MyPluginConfiguration config,
        EntryCarManager entryCarManager,
        IHostApplicationLifetime lifetime) 
        : base(lifetime)
    {
        _config = config;
        _entryCarManager = entryCarManager;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _entryCarManager.ClientConnected += OnClientConnected;
        return Task.CompletedTask;
    }
    
    private void OnClientConnected(ACTcpClient sender, EventArgs args)
    {
        sender.SendChatMessage($"Welcome! Plugin setting: {_config.SomeSetting}");
    }
}
```

## Background Service Patterns

### One-Time Initialization
```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Subscribe to events
    _entryCarManager.ClientConnected += OnClientConnected;
    
    // Initialize state
    LoadData();
    
    return Task.CompletedTask;  // Don't block
}
```

### Periodic Updates
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
        UpdateAllPlayers();
    }
}
```

### State Per Player
```csharp
private readonly ConcurrentDictionary<ulong, PlayerState> _states = new();

private void OnClientConnected(ACTcpClient sender, EventArgs args)
{
    _states[sender.Guid] = new PlayerState();
    
    sender.Disconnecting += (client, _) => _states.TryRemove(client.Guid, out _);
}
```

## CSP Lua Script Injection

```csharp
public class MyPlugin : BackgroundService
{
    private readonly CSPServerScriptProvider _scriptProvider;
    
    public MyPlugin(CSPServerScriptProvider scriptProvider)
    {
        _scriptProvider = scriptProvider;
        
        // Load embedded Lua script
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("MyPlugin.lua.myscript.lua")!;
        using var reader = new StreamReader(stream);
        _scriptProvider.AddScript(reader.ReadToEnd(), "myscript.lua");
    }
}
```

Don't forget to embed the Lua file in .csproj:
```xml
<ItemGroup>
    <EmbeddedResource Include="lua\myscript.lua" />
</ItemGroup>
```

---

# Configuration System

## Configuration File Naming

| Type | Pattern | Example |
|------|---------|---------|
| Plugin Config | `plugin_{snake_name}_cfg.yml` | `plugin_my_plugin_cfg.yml` |
| Schema | `plugin_{snake_name}_cfg.schema.json` | `plugin_my_plugin_cfg.schema.json` |
| Reference | `plugin_{snake_name}_cfg.reference.yml` | `plugin_my_plugin_cfg.reference.yml` |

## YAML Attributes

```csharp
using YamlDotNet.Serialization;

public class MyConfiguration
{
    [YamlMember(Description = "Description shown in reference config")]
    public string Setting { get; init; } = "default";
    
    [YamlMember(Description = "Another setting", DefaultValuesHandling = DefaultValuesHandling.OmitDefaults)]
    public int OptionalSetting { get; init; } = 0;  // Omitted if default
    
    [YamlIgnore]  // Not serialized
    public int ComputedValue => Setting.Length * 2;
}
```

## Configuration Validation (FluentValidation)

```csharp
// Marker interface for auto-discovery
public class MyConfiguration : IValidateConfiguration<MyConfigurationValidator>
{
    public int MinPlayers { get; init; } = 1;
    public int MaxPlayers { get; init; } = 100;
    public string? WebhookUrl { get; init; }
}

// Validator class
public class MyConfigurationValidator : AbstractValidator<MyConfiguration>
{
    public MyConfigurationValidator()
    {
        RuleFor(x => x.MinPlayers)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MinPlayers must be at least 1");
            
        RuleFor(x => x.MaxPlayers)
            .GreaterThan(x => x.MinPlayers)
            .WithMessage("MaxPlayers must be greater than MinPlayers");
            
        RuleFor(x => x.WebhookUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrEmpty(x.WebhookUrl))
            .WithMessage("Invalid webhook URL format");
    }
}
```

## Nested Configuration

```csharp
public class ParentConfiguration
{
    public NestedConfig Subsection { get; init; } = new();
}

public class NestedConfig
{
    public bool Enabled { get; init; } = true;
    public int Value { get; init; } = 10;
}

// Validator with nested rules
public class ParentConfigValidator : AbstractValidator<ParentConfiguration>
{
    public ParentConfigValidator()
    {
        RuleFor(x => x.Subsection).ChildRules(sub =>
        {
            sub.RuleFor(s => s.Value)
                .GreaterThan(0)
                .When(s => s.Enabled);
        });
    }
}
```

---

# Command System

## Command Module Base

```csharp
public class MyCommandModule : ACModuleBase
{
    private readonly MyPlugin _plugin;
    
    public MyCommandModule(MyPlugin plugin)
    {
        _plugin = plugin;
    }
    
    // Properties from base class
    // ACTcpClient? Client - The player who sent the command (null for RCON)
    
    // Methods from base class
    // void Reply(string message) - Send to command sender only
    // void Broadcast(string message) - Send to all players
    
    [Command("mycommand")]
    public void MyCommand()
    {
        Reply("Hello from my plugin!");
    }
}
```

## Command Attributes

```csharp
// Multiple command aliases
[Command("cmd", "c", "mycommand")]

// Admin-only command
[RequireAdmin]
[Command("admincmd")]

// Requires caller to be connected player (not RCON)
[RequireConnectedPlayer]
[Command("playercmd")]

// Command with parameters
[Command("greet")]
public void Greet([Remainder] string message)
{
    Broadcast($"{Client?.Name} says: {message}");
}

// Command with optional parameters
[Command("setvalue")]
public void SetValue(int value, string? label = null)
{
    Reply($"Set to {value}" + (label != null ? $" ({label})" : ""));
}

// Command targeting another player
[Command("teleport")]
public void Teleport(ACTcpClient target)
{
    Reply($"Teleporting {target.Name}...");
}
```

## Custom Type Parser

```csharp
// For custom parameter types
public class MyTypeParser : TypeParser<MyType>
{
    public override ValueTask<TypeParserResult<MyType>> ParseAsync(
        Parameter parameter, 
        string value, 
        CommandContext context)
    {
        if (MyType.TryParse(value, out var result))
            return TypeParserResult<MyType>.Successful(result);
            
        return TypeParserResult<MyType>.Failed("Invalid format");
    }
}

// Register in plugin module
builder.RegisterType<MyTypeParser>().AsSelf();

// Or in ChatService startup (core changes)
_commandService.AddTypeParser(new MyTypeParser());
```

---

# Event System

## Custom Event Handler Delegate

AssettoServer uses a typed event handler pattern:

```csharp
// Delegate signature
public delegate void EventHandler<TSender, TArgs>(TSender sender, TArgs args);

// Usage pattern
public event EventHandler<ACTcpClient, MyEventArgs>? MyEvent;

// Invoking (extension method)
MyEvent?.Invoke(client, new MyEventArgs { Data = "value" });
```

## Common Events

### EntryCarManager Events
```csharp
entryCarManager.ClientConnected += (client, args) => { };
entryCarManager.ClientDisconnected += (client, args) => { };
entryCarManager.ClientKicked += (client, auditArgs) => { };
entryCarManager.ClientBanned += (client, auditArgs) => { };
```

### ACTcpClient Events (per-player)
```csharp
client.ChecksumPassed += (sender, args) => { };     // Checksum OK
client.FirstUpdateSent += (sender, args) => { };   // Player visible in-game
client.ChatMessageReceived += (sender, chatArgs) => { };
client.Collision += (sender, collisionArgs) => { };
client.LapCompleted += (sender, lapArgs) => { };
client.SectorSplit += (sender, sectorArgs) => { };
client.TyreCompoundChange += (sender, tyreArgs) => { };
client.Damage += (sender, damageArgs) => { };
client.Disconnecting += (sender, args) => { };
client.LoggedInAsAdministrator += (sender, args) => { };
```

### ChatService Events
```csharp
chatService.MessageReceived += (client, chatArgs) => 
{
    // Cancel to prevent broadcast
    if (chatArgs.Message.Contains("spam"))
        chatArgs.Cancel = true;
};
```

## Event Args Classes

```csharp
// Collision event
public class CollisionEventArgs : EventArgs
{
    public float Speed { get; init; }
    public Vector3 RelativePosition { get; init; }
    public ACTcpClient? TargetCar { get; init; }  // null = environment collision
}

// Lap completed event
public class LapCompletedEventArgs : EventArgs
{
    public uint LapTime { get; init; }           // Milliseconds
    public byte Cuts { get; init; }
    public LapCompletedIncomingPacket Packet { get; init; }
}

// Chat event
public class ChatEventArgs : EventArgs
{
    public string Message { get; }
    public bool Cancel { get; set; }  // Set true to prevent broadcast
}
```

---

# AI Traffic System

## Key Files

| File | Purpose |
|------|---------|
| `Server/Ai/AiState.cs` | Individual AI vehicle state and behavior |
| `Server/Ai/AiBehavior.cs` | High-level behavior decisions |
| `Server/Ai/AiModule.cs` | DI registration |
| `Server/Ai/Splines/` | Track spline data processing |
| `Server/Configuration/Extra/AiParams.cs` | General AI configuration |
| `Server/Configuration/Extra/LaneChangeParams.cs` | Lane change (MOBIL) config |

## AiState Properties

```csharp
public class AiState : IDisposable
{
    // Position/State
    public CarStatus Status { get; }
    public int CurrentSplinePointId { get; }
    public float CurrentSpeed { get; }
    public float TargetSpeed { get; }
    public float MaxSpeed { get; }
    
    // Personality (SXR addition)
    public float Aggressiveness { get; }    // 0 = passive, 1 = aggressive
    public float LateralOffset { get; }     // Current lane offset
    public bool IsChangingLanes { get; }
    
    // Methods
    public void Spawn(int splinePoint);
    public void Despawn();
    public void Update();
}
```

## Traffic Configuration

### AiParams (extra_cfg.yml)
```yaml
AiParams:
  MaxAiTargetCount: 500              # Max total AI vehicles
  AiPerPlayerTargetCount: 40         # AI per player
  TrafficDensity: 1.0                # Multiplier
  MaxSpeedKph: 90                    # Default speed limit
  TwoWayTraffic: false
  WrongWayTraffic: true              # AI can go opposite direction
  HourlyTrafficDensity: [0.15, 0.10, ...] # Per-hour density (24 values)
```

### LaneChangeParams
```yaml
LaneChangeParams:
  EnableLaneChanges: true
  LaneChangeCooldownSeconds: 5.0
  LaneWidthMeters: 3.5
  
  # MOBIL Algorithm
  MobilPoliteness: 0.25              # 0 = selfish, 0.5 = cooperative
  MobilSafeDeceleration: 4.0         # m/s²
  MobilThreshold: 0.15               # Min advantage for lane change
  MobilKeepSlowLaneBias: 0.3         # Bias toward slow lane
  
  # Personality System
  EnablePersonalitySystem: true
  PassiveSpeedOffsetKmh: -10.0       # Aggression=0 speed offset
  AggressiveSpeedOffsetKmh: 30.0     # Aggression=1 speed offset
```

## Algorithms

### IDM (Intelligent Driver Model)
Car-following model that maintains safe gaps.

```
a = a_max * [1 - (v/v0)^4 - (s*/s)^2]

Where:
- a = acceleration
- v = current speed
- v0 = desired speed
- s = gap to vehicle ahead
- s* = desired gap (function of speed difference)
```

### MOBIL (Minimizing Overall Braking Induced by Lane changes)
Lane change decision model.

```
Advantage = a_new - a_old - p * (Δa_follower)

Change if:
- Advantage > threshold
- Follower's deceleration < safe_decel_limit
```

---

# C# Patterns & Idioms

## Dependency Injection (Autofac)

### Registration Patterns
```csharp
// Single instance (singleton)
builder.RegisterType<MyService>().AsSelf().SingleInstance();

// As interface
builder.RegisterType<MyService>().As<IMyService>().SingleInstance();

// Multiple interfaces
builder.RegisterType<MyService>()
    .AsSelf()
    .As<IMyService>()
    .As<IHostedService>()
    .SingleInstance();

// With auto-activation (constructed immediately)
builder.RegisterType<MyService>().AsSelf().SingleInstance().AutoActivate();

// Factory delegate
builder.RegisterType<EntryCar>().AsSelf();  // Creates delegate: Func<string, string, byte, EntryCar>

// Instance registration
builder.RegisterInstance(_configuration);
```

### Constructor Injection
```csharp
public class MyPlugin
{
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;
    
    public MyPlugin(
        EntryCarManager entryCarManager,
        ACServerConfiguration configuration)
    {
        _entryCarManager = entryCarManager;
        _configuration = configuration;
    }
}
```

### Factory Injection
```csharp
public class MyPlugin
{
    private readonly Func<EntryCar, PlayerState> _stateFactory;
    
    public MyPlugin(Func<EntryCar, PlayerState> stateFactory)
    {
        _stateFactory = stateFactory;
    }
    
    private void OnConnect(EntryCar car)
    {
        var state = _stateFactory(car);
    }
}
```

## Async Patterns

### Task-based Async
```csharp
// Async method
public async Task<bool> ValidateAsync(string input)
{
    await Task.Delay(100);
    return !string.IsNullOrEmpty(input);
}

// Fire and forget (with proper error handling)
_ = Task.Run(async () => 
{
    try { await DoWorkAsync(); }
    catch (Exception ex) { Log.Error(ex, "Background task failed"); }
});

// Wait with timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try { await LongRunningTask(cts.Token); }
catch (OperationCanceledException) { /* Timeout */ }
```

### Channels (Producer/Consumer)
```csharp
// Create bounded channel
Channel<IOutgoingNetworkPacket> _channel = Channel.CreateBounded<IOutgoingNetworkPacket>(256);

// Producer
if (!_channel.Writer.TryWrite(packet))
    Log.Warning("Channel full, dropping packet");

// Consumer
await foreach (var packet in _channel.Reader.ReadAllAsync(cancellationToken))
{
    await SendPacketAsync(packet);
}
```

### PeriodicTimer (Modern Timer Pattern)
```csharp
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    DoPeriodicWork();
}
```

## Memory & Performance

### Span<T> and Memory<T>
```csharp
// Stack allocation for small buffers
Span<byte> buffer = stackalloc byte[256];

// Slice without allocation
ReadOnlySpan<byte> slice = buffer.Slice(10, 20);

// For packet writing
public void ToWriter(ref PacketWriter writer)
{
    writer.WriteBytes(data.Span);  // Zero-copy write
}
```

### ArrayPool
```csharp
// Rent buffer
byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### Thread-Local Storage
```csharp
private static ThreadLocal<byte[]> SendBuffer { get; } = 
    new(() => GC.AllocateArray<byte>(1500, pinned: true));

// Usage
byte[] buffer = SendBuffer.Value!;
```

## Struct Patterns

### Readonly Struct for Packets
```csharp
public readonly struct PositionUpdate : IOutgoingNetworkPacket
{
    public byte SessionId { get; init; }
    public Vector3 Position { get; init; }
    public Vector3 Velocity { get; init; }
    
    public void ToWriter(ref PacketWriter writer) { /* ... */ }
}
```

### Ref Struct for Temporary Data
```csharp
public ref struct PacketWriter
{
    public readonly Stream? Stream;
    public Memory<byte> Buffer { get; private set; }
    private int _writePosition;
}
```

## Collection Patterns

### ConcurrentDictionary
```csharp
private readonly ConcurrentDictionary<ulong, PlayerState> _states = new();

// Add or update
_states.AddOrUpdate(guid, 
    addValueFactory: _ => new PlayerState(),
    updateValueFactory: (_, existing) => { existing.Update(); return existing; });

// Try operations
if (_states.TryGetValue(guid, out var state)) { /* ... */ }
if (_states.TryRemove(guid, out var removed)) { /* ... */ }
```

### Thread-Safe Enumeration
```csharp
// Snapshot iteration
foreach (var car in _entryCarManager.EntryCars.ToArray())
{
    // Safe even if collection changes
}

// Or use ConnectedCars dictionary
foreach (var kvp in _entryCarManager.ConnectedCars)
{
    var entryCar = kvp.Value;
}
```

---

# Common Development Tasks

## Send Custom CSP Message

```csharp
// Define message type
public class MyCustomMessage : CSPClientMessageOutgoing
{
    public int Data1 { get; init; }
    public string Data2 { get; init; } = "";
    
    protected override void ToWriter(BinaryWriter writer)
    {
        writer.Write(Data1);
        writer.Write(Data2);
    }
}

// Register type (once, in plugin constructor)
_clientMessageTypeManager.RegisterClientMessageType<MyCustomMessage>(0x1234);

// Send to player
client.SendPacket(new MyCustomMessage 
{ 
    SessionId = client.SessionId,
    Type = (CSPClientMessageType)0x1234,
    Data1 = 42,
    Data2 = "Hello"
});
```

## HTTP Endpoint

```csharp
[ApiController]
public class MyController : ControllerBase
{
    private readonly MyPlugin _plugin;
    
    public MyController(MyPlugin plugin)
    {
        _plugin = plugin;
    }
    
    [HttpGet("/myplugin/status")]
    public ActionResult<StatusResponse> GetStatus()
    {
        return Ok(new StatusResponse { Status = "Running" });
    }
    
    [HttpPost("/myplugin/action")]
    public async Task<ActionResult> PostAction([FromBody] ActionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
            
        await _plugin.PerformActionAsync(request);
        return Ok();
    }
}
```

### Alternative: Route Attribute Pattern
```csharp
[ApiController]
[Route("myplugin")]  // Base route for all endpoints in controller
public class MyController : ControllerBase
{
    private readonly MyPlugin _plugin;
    private readonly MyPluginConfiguration _config;
    
    public MyController(MyPlugin plugin, MyPluginConfiguration config)
    {
        _plugin = plugin;
        _config = config;
    }
    
    // Becomes: GET /myplugin/status
    [HttpGet("status")]
    public ActionResult<StatusResponse> GetStatus()
    {
        return Ok(new StatusResponse { Status = "Running" });
    }
    
    // Becomes: GET /myplugin/data/{id}
    [HttpGet("data/{id}")]
    public ActionResult<DataResponse> GetData(string id)
    {
        var data = _plugin.GetData(id);
        if (data == null)
            return NotFound(new { message = "Data not found" });
        return data;
    }
    
    // Becomes: GET /myplugin/config (return config values)
    [HttpGet("config")]
    public ActionResult<List<string>> GetConfigItems()
    {
        return _config.SomeList;  // Can access config directly
    }
}
```

## Persist Data (JSON)

```csharp
public class DataStorage<T> where T : new()
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task<T> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_path))
                return new T();
                
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        finally { _lock.Release(); }
    }
    
    public async Task SaveAsync(T data)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_path, json);
        }
        finally { _lock.Release(); }
    }
}
```

## Cross-Plugin Communication

### Provider Pattern
```csharp
// In consuming plugin
public class MyPlugin
{
    private Func<ulong, int>? _getLevelProvider;
    
    public void SetLevelProvider(Func<ulong, int> provider)
    {
        _getLevelProvider = provider;
    }
    
    private int GetPlayerLevel(ulong guid)
    {
        return _getLevelProvider?.Invoke(guid) ?? 0;
    }
}

// In providing plugin
public class StatsPlugin
{
    public int GetLevel(ulong guid) => /* ... */;
}

// Connection (in Startup or common initialization)
myPlugin.SetLevelProvider(statsPlugin.GetLevel);
```

### Shared Interface
```csharp
// In AssettoServer.Shared or separate shared assembly
public interface IPlayerStatsProvider
{
    int GetLevel(ulong guid);
    int GetPrestige(ulong guid);
}

// Implementation
public class StatsPlugin : IPlayerStatsProvider { /* ... */ }

// Register
builder.RegisterType<StatsPlugin>().As<IPlayerStatsProvider>().SingleInstance();

// Consume
public class MyPlugin
{
    private readonly IPlayerStatsProvider? _stats;  // Optional injection
    
    public MyPlugin(IPlayerStatsProvider? stats = null)
    {
        _stats = stats;
    }
}
```

---

# Debugging & Testing

## Logging

```csharp
using Serilog;

// Global logger
Log.Information("Server started");
Log.Warning("High memory usage: {MemoryMB} MB", memoryMb);
Log.Error(exception, "Failed to process request");

// Per-client logger (enriched with client info)
client.Logger.Information("Player {Action}", "connected");
client.Logger.Debug("Position: {Position}", position);
```

## Debug Configuration

```yaml
# extra_cfg.yml
Debug: true  # Enable debug logging

AiParams:
  Debug: true  # AI debug overlay
```

## Unit Testing

```csharp
[Fact]
public void TestPacketSerialization()
{
    var packet = new MyPacket { Value = 42 };
    var buffer = new byte[100];
    var writer = new PacketWriter(buffer.AsMemory());
    writer.WritePacket(packet);
    
    var reader = new PacketReader(null, buffer.AsMemory());
    reader.SliceBuffer(writer.WritePosition);
    var result = reader.ReadPacket<MyPacket>();
    
    Assert.Equal(42, result.Value);
}
```

---

# Performance Considerations

## Network Optimization

1. **Batch packets when possible**
   ```csharp
   client.SendPacket(new BatchedPacket { Packets = [packet1, packet2, packet3] });
   ```

2. **Use UDP for high-frequency updates**
   ```csharp
   client.SendPacketUdp(positionUpdate);  // Non-blocking, no TCP overhead
   ```

3. **Range-based broadcasting**
   ```csharp
   _entryCarManager.BroadcastPacketUdp(packet, sender: client, range: 500f);
   ```

## Memory Optimization

1. **Reuse buffers** - Use `ThreadLocal<byte[]>` or `ArrayPool`
2. **Avoid allocations in hot paths** - Use `Span<T>` and stack allocation
3. **Use struct packets** - `readonly struct` for value semantics

## Threading

1. **Don't block async methods** - Use `await` throughout
2. **Use ConcurrentDictionary** for shared state
3. **Avoid locks** - Use channels or Interlocked operations
4. **Be careful with events** - Subscribe in ClientConnected, unsubscribe in Disconnecting

---

# Quick Reference

## File Naming Conventions

| Type | Pattern |
|------|---------|
| Plugin DLL | `plugins/{Name}/{Name}.dll` |
| Plugin Config | `plugin_{snake_name}_cfg.yml` |
| Schema | `plugin_{snake_name}_cfg.schema.json` |

## Common Imports

```csharp
// Core
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Network.Tcp;

// Packets
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;

// DI
using Autofac;
using Microsoft.Extensions.Hosting;

// Commands
using AssettoServer.Commands;
using Qmmands;

// Logging
using Serilog;
```

## Service Lifetimes

| Lifetime | Registration | Use Case |
|----------|--------------|----------|
| Singleton | `.SingleInstance()` | Services, managers |
| Transient | (default) | Factories, per-request |
| Scoped | `.InstancePerLifetimeScope()` | Per-connection state |

---

---

# Part 2: Client-Side Development (CSP Lua)

---

# CSP Lua Script Types

## Critical Distinction: Regular Lua Apps vs Online Scripts

| Feature | Regular Lua App | Online Script |
|---------|----------------|---------------|
| **Location** | `assettocorsa/apps/lua/` | Server-provided URL or `extension/lua/online/` |
| **Entry Points** | `script.windowMain(dt)` | `script.drawUI()`, `script.update(dt)` |
| **Manifest** | Required (`manifest.ini`) | Not used - configured in server `csp_extra_options.ini` |
| **Window Creation** | Automatic via manifest | Manual via `ui.toolWindow()` or `ui.transparentWindow()` |
| **Input Handling** | Automatic | **Must explicitly enable with `inputs=true`** |
| **File Access** | Full (with restrictions) | Limited to track/cars folders |
| **Process Execution** | Allowed | Not allowed |

## Script Type Entry Points

**Online Scripts** (`ac_online_script.lua`):
```lua
---@class ScriptData
---@field update fun(dt: number)        -- Called each frame
---@field draw3D fun()                   -- Called for transparent objects rendering
---@field drawUI fun()                   -- For custom HUD elements online
---@field frameBegin fun(dt: number, gameDT: number)  -- Before scene rendering
script = {}
```

**Regular Lua Apps** (via manifest.ini):
```lua
function script.windowMain(dt)      -- Main window content
function script.windowSettings(dt)  -- Settings window content (optional)
function script.update(dt)          -- Called each frame
function script.Draw3D(dt)          -- 3D rendering callback (optional)
```

---

# CSP Lua Troubleshooting

## Issue #1: Missing `inputs` Parameter (CRITICAL)

### Problem
```lua
-- Current code (BROKEN - inputs disabled)
ui.toolWindow('adminPanel', panelPos, panelSize, true, function()
    -- ... content
end)
```

### Analysis
The `ui.toolWindow()` function signature from SDK (`ac_ui.lua` lines 179-184):
```lua
---@param id string @Window ID, has to be unique within your script.
---@param pos vec2 @Window position.
---@param size vec2 @Window size.
---@param noPadding boolean? @Disables window padding. Default value: `false`.
---@param inputs boolean? @Enables inputs (buttons and such). Default value: `false`.
---@param content fun(): T? @Window content callback.
function ui.toolWindow(id, pos, size, noPadding, inputs, content)
  if type(noPadding) == 'function' then content, noPadding, inputs = noPadding, nil, nil end
  if type(inputs) == 'function' then content, inputs = inputs, nil end
  ui.beginToolWindow(id, pos, size, noPadding == true, inputs == true)
  return using(content, ui.endToolWindow)
end
```

**The overload handling**: When calling with only 5 arguments and the 4th is `true`:
- If `noPadding` is a function → reassigns to content (not our case)
- If `inputs` is a function → reassigns to content, `inputs` becomes `nil`

**Result**: `inputs == true` evaluates to `false`, disabling all interactivity.

### Solution
```lua
-- Fixed code (CORRECT - inputs enabled)
ui.toolWindow('adminPanel', panelPos, panelSize, true, true, function()
    -- ... content now responds to input
end)
```

---

## Issue #2: Admin Status Race Condition

### Problem
```lua
CheckAdminStatus()  -- Async web request

setTimeout(function()
    if state.isAdmin then  -- May still be false if request hasn't returned
        ui.registerOnlineExtra(...)
    end
end, 2000)
```

### Solution - Callback-based Registration
```lua
local extraRegistered = false

function CheckAdminStatus()
    local url = GetBaseUrl()
    if not url then return end
    
    web.get(url .. "/status", function(err, response)
        if err then
            state.isAdmin = false
            return
        end
        
        local data = stringify.tryParse(response.body)
        if data then
            state.status = data
            for _, admin in ipairs(data.ConnectedAdmins or {}) do
                if admin.SteamId == steamId then
                    state.isAdmin = true
                    state.adminLevel = admin.Level
                    RegisterOnlineExtra()  -- Register immediately upon confirmation
                    return
                end
            end
        end
        state.isAdmin = false
    end)
end

function RegisterOnlineExtra()
    if extraRegistered or not state.isAdmin then return end
    extraRegistered = true
    
    ui.registerOnlineExtra(ui.Icons.Settings, "Admin Panel", 
        function() return state.isAdmin end, 
        function()
            state.panelOpen = true
            FetchAllData()
            return false
        end)
end
```

---

## Issue #3: Base URL Construction Timing

### Problem
```lua
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/admin"
```

This runs at script load time before connection is fully established.

### Solution - Lazy URL Construction
```lua
local baseUrl = nil

function GetBaseUrl()
    if baseUrl then return baseUrl end
    
    local ip = ac.getServerIP()
    local port = ac.getServerPortHTTP()
    
    if not ip or ip == "" or not port or port == 0 then
        ac.log("Admin Tools: Server connection not ready")
        return nil
    end
    
    baseUrl = string.format("http://%s:%d/admin", ip, port)
    return baseUrl
end
```

---

## Issue #4: Hotkey Detection Method

### Problem
```lua
function script.update(dt)
    if ac.isKeyPressed(config.hotkey) then  -- True every frame while held
        -- Toggle panel - rapid toggling!
    end
end
```

### Solution - Edge Detection
```lua
local hotkeyWasPressed = false

function script.update(dt)
    local hotkeyIsPressed = ac.isKeyDown(config.hotkey)
    
    -- Detect rising edge (key just pressed)
    if hotkeyIsPressed and not hotkeyWasPressed then
        if state.isAdmin then
            state.panelOpen = not state.panelOpen
        end
    end
    
    hotkeyWasPressed = hotkeyIsPressed
end
```

---

# CSP Lua SDK Reference

## Window Functions

### ui.toolWindow()
Semi-transparent background window for tool UIs.

```lua
---@param id string @Window ID, must be unique within your script
---@param pos vec2 @Window position
---@param size vec2 @Window size
---@param noPadding boolean? @Disables window padding (default: false)
---@param inputs boolean? @Enables inputs like buttons (default: false)
---@param content fun(): T? @Window content callback
---@return T
function ui.toolWindow(id, pos, size, noPadding, inputs, content)

-- Overloads:
ui.toolWindow(id, pos, size, content)           -- noPadding=nil, inputs=nil
ui.toolWindow(id, pos, size, noPadding, content) -- inputs=nil
```

### ui.transparentWindow()
Fully transparent window for HUD elements.

```lua
---@param id string @Window ID
---@param pos vec2 @Window position
---@param size vec2 @Window size
---@param noPadding boolean? @Default: false
---@param inputs boolean? @Default: false
---@param content fun(): T? @Window content callback
function ui.transparentWindow(id, pos, size, noPadding, inputs, content)
```

## Web Request Functions

### web.get()
```lua
---@param url string @URL
---@param headers table<string, string|number|boolean>? @Optional headers
---@param callback fun(err: string, response: WebResponse)
function web.get(url, headers, callback)
-- Overload: web.get(url, callback)

-- WebResponse structure:
-- response.status: HTTP status code
-- response.body: Response body as string
-- response.headers: Response headers table
```

### web.post()
```lua
---@param url string @URL
---@param headers table<string, string|number|boolean>? @Optional headers
---@param data WebPayload? @Optional POST data
---@param callback fun(err: string, response: WebResponse)
function web.post(url, headers, data, callback)
-- Overloads:
-- web.post(url, data, callback)
-- web.post(url, callback)
```

### web.request()
```lua
---@param method "'GET'"|"'POST'"|"'PUT'"|"'HEAD'"|"'DELETE'"|"'PATCH'"|"'OPTIONS'"
---@param url string @URL
---@param headers table<string, string|number|boolean>? @Optional headers
---@param data WebPayload? @Optional data
---@param callback fun(err: string, response: WebResponse)
function web.request(method, url, headers, data, callback)
```

### web.socket()
```lua
---@param url string @WebSocket URL
---@param headers table<string, string|number|boolean>? @Optional headers
---@param callback nil|fun(data: binary) @Message handler
---@param params web.SocketParams? @{onError, onClose, encoding, reconnect}
---@return web.Socket @{close: fun()}|fun(data: binary)
function web.socket(url, headers, callback, params)
```

### web.timeouts()
```lua
---@param resolve integer? @DNS resolve timeout ms (default: 4000)
---@param connect integer? @Connection timeout ms (default: 10000)
---@param send integer? @Send timeout ms (default: 30000)
---@param receive integer? @Receive timeout ms (default: 30000)
function web.timeouts(resolve, connect, send, receive)
```

## Timer Functions

### setTimeout()
```lua
---Runs callback after delay. Returns cancellation ID.
---@param callback fun()
---@param delay number? @Delay in seconds (default: 0)
---@param uniqueKey any? @If set, prevents duplicate timers with same key
---@return integer @Cancellation ID
function setTimeout(callback, delay, uniqueKey)
```

### setInterval()
```lua
---Repeatedly runs callback. Returns cancellation ID.
---@param callback fun(): function? @Return clearInterval to stop
---@param period number? @Period in seconds (default: 0)
---@param uniqueKey any? @If set, prevents duplicate timers
---@return integer @Cancellation ID
function setInterval(callback, period, uniqueKey)
```

### clearTimeout() / clearInterval()
```lua
---@param cancellationID integer
---@return boolean @True if timer was found and stopped
function clearTimeout(cancellationID)
function clearInterval(cancellationID)  -- Same as clearTimeout
```

## Storage Functions

### ac.storage()
Persistent storage across sessions.

```lua
-- Table-based storage with auto-sync:
local settings = ac.storage({
    panelPosition = vec2(100, 100),
    selectedTab = 1,
    volume = 0.8
})

-- Access like normal table (auto-syncs):
settings.volume = 0.5
local pos = settings.panelPosition

-- Single value storage:
local storedValue = ac.storage('myKey', defaultValue)
local current = storedValue:get()
storedValue:set(newValue)
```

## UI Controls

### ui.button()
```lua
---@param label string @Button text
---@param size vec2? @Button size
---@param flags ui.ButtonFlags? @Optional flags
---@return boolean @True if clicked
function ui.button(label, size, flags)
```

### ui.slider()
```lua
---@param label string @Slider label
---@param value refnumber|number @Current value
---@param min number? @Minimum (default: 0)
---@param max number? @Maximum (default: 1)
---@param format string? @Display format (default: '%.3f')
---@param power number|boolean? @Power for non-linear, or true for integer
---@return number, boolean @Value, changed
function ui.slider(label, value, min, max, format, power)
```

### ui.inputText()
```lua
---@param label string
---@param str string @Current text
---@param flags ui.InputTextFlags?
---@param size vec2? @Multiline if specified
---@return string, boolean, boolean @Text, changed, enterPressed
function ui.inputText(label, str, flags, size)
```

### ui.checkbox()
```lua
---@param label string
---@param value boolean|refbool
---@return boolean @Changed state
function ui.checkbox(label, value)
```

### ui.combo()
```lua
-- With callback:
ui.combo(label, previewValue, flags, function()
    if ui.selectable('Option 1', selected == 1) then selected = 1 end
    if ui.selectable('Option 2', selected == 2) then selected = 2 end
end)

-- With string array:
local selected, changed = ui.combo('Label', selectedIndex, flags, {'Option 1', 'Option 2'})
```

### ui.tabBar() / ui.tabItem()
```lua
ui.tabBar('myTabs', function()
    ui.tabItem('Tab 1', function()
        -- Tab 1 content
    end)
    ui.tabItem('Tab 2', function()
        -- Tab 2 content
    end)
end)
```

### ui.treeNode()
```lua
ui.treeNode('Section', ui.TreeNodeFlags.DefaultOpen, function()
    -- Collapsible content
end)
```

## Primitive Types Reference

```lua
-- Vector types
vec2(x, y)           -- 2D vector
vec3(x, y, z)        -- 3D vector
vec4(x, y, z, w)     -- 4D vector

-- Color types
rgb(r, g, b)         -- RGB color (0-1 range)
rgbm(r, g, b, mult)  -- RGB with multiplier

-- Reference types (for UI controls)
refnumber(initial)   -- Mutable number reference
refbool(initial)     -- Mutable boolean reference

-- Usage:
local ref = refnumber(50)
if ui.slider('Value', ref, 0, 100) then
    local newValue = ref.value
end
```

## UI Flags Reference

### ui.WindowFlags
```lua
ui.WindowFlags.None                      -- Default
ui.WindowFlags.NoTitleBar                -- Hide title bar
ui.WindowFlags.NoResize                  -- Disable resizing
ui.WindowFlags.NoMove                    -- Disable moving
ui.WindowFlags.NoScrollbar               -- Hide scrollbars
ui.WindowFlags.NoScrollWithMouse         -- Disable mouse wheel scroll
ui.WindowFlags.NoCollapse                -- Disable collapse button
ui.WindowFlags.AlwaysAutoResize          -- Auto-fit to content
ui.WindowFlags.NoBackground              -- Transparent background
ui.WindowFlags.NoMouseInputs             -- Pass-through mouse
ui.WindowFlags.HorizontalScrollbar       -- Enable horizontal scroll
ui.WindowFlags.NoFocusOnAppearing        -- Don't grab focus
ui.WindowFlags.NoDecoration              -- NoTitleBar | NoResize | NoScrollbar | NoCollapse
ui.WindowFlags.NoInputs                  -- NoMouseInputs | NoNavInputs | NoNavFocus
```

### ui.ButtonFlags
```lua
ui.ButtonFlags.None
ui.ButtonFlags.Repeat                    -- Hold to repeat
ui.ButtonFlags.PressedOnClick            -- Return true on click
ui.ButtonFlags.PressedOnDoubleClick      -- Return true on double-click
ui.ButtonFlags.Disabled                  -- Disable interactions
ui.ButtonFlags.Error                     -- Error style (red)
ui.ButtonFlags.Confirm                   -- Confirm style (green)
ui.ButtonFlags.Cancel                    -- Cancel style
```

### ui.InputTextFlags
```lua
ui.InputTextFlags.CharsDecimal           -- Allow 0-9.+-*/
ui.InputTextFlags.CharsHexadecimal       -- Allow hex chars
ui.InputTextFlags.CharsUppercase         -- Force uppercase
ui.InputTextFlags.AutoSelectAll          -- Select all on focus
ui.InputTextFlags.ReadOnly               -- Read-only mode
ui.InputTextFlags.Password               -- Show as asterisks
ui.InputTextFlags.Placeholder            -- Show label as placeholder
ui.InputTextFlags.ClearButton            -- Add clear button
```

---

# Regular Lua Apps

## Directory Structure
```
assettocorsa/apps/lua/MyApp/
├── manifest.ini          # Required configuration
├── MyApp.lua            # Main script (same name as folder)
├── icon.png             # App icon (recommended 64x64)
└── other_modules.lua    # Optional additional files
```

## Manifest Format
```ini
[ABOUT]
NAME = My App Name
AUTHOR = Your Name
VERSION = 1.0
DESCRIPTION = What your app does
REQUIRED_VERSION = 2144    ; Minimum CSP build number
URL = https://github.com/...

[CORE]
LAZY = FULL
; • NONE (0): Load at AC startup, run until AC closes
; • PARTIAL (1): Load on first open, keep running
; • FULL (2): Load on open, unload when all windows close

[WINDOW_...]
ID = main                  ; Unique window identifier
NAME = Window Title        ; Title bar text
ICON = icon.png           ; Window icon
SIZE = 400, 200           ; Default size (width, height)
MIN_SIZE = 200, 100       ; Minimum size
MAX_SIZE = 800, 600       ; Maximum size
PADDING = 8, 8            ; Window padding
FLAGS = SETTINGS, FIXED_SIZE  ; Comma-separated flags
FUNCTION_MAIN = windowMain
FUNCTION_SETTINGS = windowSettings  ; Optional
FUNCTION_ON_SHOW = onShow           ; Optional
FUNCTION_ON_HIDE = onHide           ; Optional

; Optional render callbacks
[RENDER_CALLBACKS]
TRANSPARENT = Draw3D

; Optional sim callbacks
[SIM_CALLBACKS]
FRAME_BEGIN = frameBegin
UPDATE = simUpdate

; Optional fullscreen UI callback
[UI_CALLBACKS]
IN_GAME = drawHUD
```

## Window Flags
- `AUTO_RESIZE`: Auto-size to content
- `DARK_HEADER`: Dark title bar text
- `FADING`: Fade when inactive
- `FIXED_SIZE`: Prevent resizing
- `HIDDEN_OFFLINE`: Hide in singleplayer
- `HIDDEN_ONLINE`: Hide when connected to server
- `HIDDEN_RENDER_VR`: Hide in VR mode
- `MAIN`: Mark as main window
- `NO_BACKGROUND`: Transparent background
- `NO_COLLAPSE`: Hide collapse button
- `NO_TITLE_BAR`: Hide title bar
- `SETTINGS`: Add settings button
- `SETUP`: Show in setup screen

## Minimal App Example
```lua
-- MyApp.lua
local sim = ac.getSim()

function script.windowMain(dt)
    ui.text('Hello World!')
    ui.text('Speed: %.1f km/h' % (sim.cameraCarSpeed * 3.6))
    
    if ui.button('Click Me', vec2(100, 30)) then
        ac.log('Button clicked!')
    end
end

function script.update(dt)
    -- Called every frame, even when window is closed
end
```

---

# Online Script Development

## Script Location
For development: `assettocorsa/extension/lua/online/`

## Entry Points
```lua
-- Called each frame
function script.update(dt)
    -- dt: time since last update in seconds
end

-- Called for UI rendering
function script.drawUI()
    -- Draw HUD elements, windows, etc.
end

-- Called for 3D rendering (transparent pass)
function script.draw3D()
    -- Draw debug shapes, markers, etc.
end

-- Called at frame start before rendering
function script.frameBegin(dt, gameDT)
    -- dt: real time delta
    -- gameDT: simulation time delta (affected by replay speed)
end
```

## Global Variables Available
```lua
car  -- Reference to player's car state (ac.StateCar)
sim  -- Reference to simulation state (ac.StateSim)
```

## Online Extra Registration
```lua
ui.registerOnlineExtra(
    ui.Icons.Settings,           -- Icon
    "My Feature",                -- Name
    function() return enabled end, -- Visibility check
    function()                    -- Click handler
        -- Open your UI
        return false  -- Return true to keep popup open
    end,
    ui.OnlineExtraFlags.Tool     -- Optional: Tool (window) vs Modal (popup)
)
```

## Complete Online Script Template
```lua
-- Configuration
local config = {
    hotkey = ac.KeyIndex.F8,
    panelSize = vec2(400, 300)
}

-- State
local state = {
    panelOpen = false,
    isAdmin = false,
    data = {}
}

-- Cached references
local sim = nil
local car = nil
local baseUrl = nil
local hotkeyWasPressed = false

-- Initialize on script load
function script.__init__()
    sim = ac.getSim()
    car = ac.getCar(0)
end

-- Get base URL lazily
local function getBaseUrl()
    if baseUrl then return baseUrl end
    local ip = ac.getServerIP()
    local port = ac.getServerPortHTTP()
    if not ip or ip == "" or not port or port == 0 then return nil end
    baseUrl = string.format("http://%s:%d", ip, port)
    return baseUrl
end

-- Check admin status
local function checkAdmin()
    local url = getBaseUrl()
    if not url then
        setTimeout(checkAdmin, 1)
        return
    end
    
    web.get(url .. "/admin/status", function(err, response)
        if err then return end
        local data = stringify.tryParse(response.body)
        if data then
            state.isAdmin = data.isAdmin == true
            if state.isAdmin then
                registerOnlineExtra()
            end
        end
    end)
end

-- Register online extra button
local registered = false
local function registerOnlineExtra()
    if registered then return end
    registered = true
    
    ui.registerOnlineExtra(
        ui.Icons.Settings,
        "Admin Panel",
        function() return state.isAdmin end,
        function()
            state.panelOpen = true
            return false
        end,
        ui.OnlineExtraFlags.Tool
    )
end

-- Update loop
function script.update(dt)
    -- Hotkey handling with edge detection
    local hotkeyIsPressed = ac.isKeyDown(config.hotkey)
    if hotkeyIsPressed and not hotkeyWasPressed then
        if state.isAdmin then
            state.panelOpen = not state.panelOpen
        end
    end
    hotkeyWasPressed = hotkeyIsPressed
end

-- UI rendering
function script.drawUI()
    if not state.panelOpen or not state.isAdmin then return end
    
    local uiState = ac.getUI()
    local pos = vec2(
        (uiState.windowSize.x - config.panelSize.x) / 2,
        (uiState.windowSize.y - config.panelSize.y) / 2
    )
    
    -- CRITICAL: Include inputs=true (5th parameter)!
    ui.toolWindow('myPanel', pos, config.panelSize, false, true, function()
        ui.text('Admin Panel')
        ui.separator()
        
        if ui.button('Close', vec2(100, 30)) then
            state.panelOpen = false
        end
    end)
end

-- Start admin check
checkAdmin()
```

---

# Server Script Configuration

## csp_extra_options.ini Script Section
```ini
[SCRIPT_...]
SCRIPT = 'https://example.com/script.lua'
; Or for local development:
; SCRIPT = 'myscript.lua'  ; Loads from extension/lua/online/

REQUIRED = 0
; 0 = Optional script
; 1 = Required to join (CSP closes game if script fails to load)

REFRESH_PERIOD = 0
; Periodic refresh in seconds (0 = no refresh)

; Custom parameters accessible via ac.configValues():
MY_PARAM = some_value
API_KEY = abc123
```

## URL Parameter Substitution
```ini
[SCRIPT_...]
SCRIPT = 'https://server.com/script?s={SessionID}&t={SteamID}&c={CarID}&k={CarSkinID}&v={CSPBuildID}'
```

Available parameters:
- `{SessionID}`: 0-based entry list index
- `{SteamID}`: Player's Steam ID
- `{CarID}`: Car folder name
- `{CarSkinID}`: Skin folder name
- `{CSPBuildID}`: CSP build number
- `{ServerIP}`: Server IP address
- `{ServerName}`: Server name
- `{ServerHTTPPort}`: HTTP port
- `{ServerTCPPort}`: TCP port
- `{ServerUDPPort}`: UDP port

## Accessing Config Values in Script
```lua
local config = ac.configValues({
    myParam = 'default',      -- String parameter
    apiKey = '',              -- String parameter
    maxPlayers = 16,          -- Number parameter
    enabled = true            -- Boolean parameter
})

-- Use like regular table:
print(config.myParam)
```

---

# CSP Development Environment

## Prerequisites
1. **CSP 0.1.76+** (preferably latest stable)
2. **Visual Studio Code** with sumneko Lua extension
3. Access to `extension/internal/lua-sdk/` folder

## VS Code Setup
1. Install [Lua extension by sumneko](https://marketplace.visualstudio.com/items?itemName=sumneko.lua)
2. Open your script folder in VS Code
3. Create `.vscode/settings.json`:
```json
{
    "Lua.workspace.library": [
        "C:/Program Files (x86)/Steam/steamapps/common/assettocorsa/extension/internal/lua-sdk"
    ],
    "Lua.diagnostics.globals": [
        "ac", "ui", "vec2", "vec3", "rgb", "rgbm", "sim", "car",
        "setTimeout", "setInterval", "clearTimeout", "clearInterval",
        "stringify", "web", "refnumber", "refbool"
    ]
}
```

## Enabling Lua Debug App
1. Open CSP settings
2. Navigate to: `GUI > Developer Apps`
3. Enable **Lua Debug** app
4. In-game, find it at the bottom of the new app taskbar

The Lua Debug app shows:
- Running scripts status
- Error messages
- Live debug output
- Performance metrics

## Hot Reloading for Development
For online scripts, set local path:
```ini
[SCRIPT_...]
SCRIPT = 'myscript.lua'  ; Loads from extension/lua/online/
```

Then edit the file and the script will reload on server reconnect.

---

# Lua Code Patterns & Examples

## Rate-Limited Data Fetching
```lua
local dataCache = {
    players = {},
    lastFetch = 0,
    fetching = false,
    minInterval = 1  -- seconds
}

function FetchPlayers()
    if dataCache.fetching then return end
    if os.clock() - dataCache.lastFetch < dataCache.minInterval then return end
    
    dataCache.fetching = true
    web.get(GetBaseUrl() .. "/players", function(err, response)
        dataCache.fetching = false
        dataCache.lastFetch = os.clock()
        
        if not err and response.body then
            local data = stringify.tryParse(response.body)
            if data then
                dataCache.players = data
            end
        end
    end)
end
```

## Safe JSON Parsing
```lua
function SafeParse(jsonString)
    if not jsonString or jsonString == "" then
        return nil, "Empty response"
    end
    
    local success, result = pcall(function()
        return stringify.parse(jsonString)
    end)
    
    if success then
        return result, nil
    else
        return nil, "Parse error: " .. tostring(result)
    end
end

-- Or use built-in tryParse:
local data = stringify.tryParse(response.body)
if not data then
    ac.log("JSON parse failed")
    return
end
```

## Tab-Based UI Panel
```lua
local currentTab = 1

function DrawPanel()
    ui.tabBar('mainTabs', function()
        ui.tabItem('Players', function()
            currentTab = 1
            DrawPlayersTab()
        end)
        ui.tabItem('Settings', function()
            currentTab = 2
            DrawSettingsTab()
        end)
        ui.tabItem('Logs', function()
            currentTab = 3
            DrawLogsTab()
        end)
    end)
end
```

## Scrollable List with Child Window
```lua
function DrawPlayerList(players)
    ui.childWindow('playerList', vec2(-1, 200), true, ui.WindowFlags.None, function()
        for i, player in ipairs(players) do
            ui.pushID(i)
            
            if ui.selectable(player.name, selectedPlayer == i) then
                selectedPlayer = i
            end
            
            -- Right-click context menu
            ui.itemPopup(ui.MouseButton.Right, function()
                if ui.selectable('Kick') then KickPlayer(player.id) end
                if ui.selectable('Ban') then BanPlayer(player.id) end
            end)
            
            ui.popID()
        end
    end)
end
```

## Confirmation Dialog Pattern
```lua
local confirmAction = nil
local confirmMessage = ""

function ShowConfirm(message, action)
    confirmMessage = message
    confirmAction = action
end

function DrawConfirmDialog()
    if not confirmAction then return end
    
    local size = vec2(300, 120)
    local uiState = ac.getUI()
    local pos = vec2(
        (uiState.windowSize.x - size.x) / 2,
        (uiState.windowSize.y - size.y) / 2
    )
    
    ui.toolWindow('confirmDialog', pos, size, false, true, function()
        ui.text(confirmMessage)
        ui.spacing()
        
        if ui.button('Confirm', vec2(100, 30), ui.ButtonFlags.Confirm) then
            confirmAction()
            confirmAction = nil
        end
        
        ui.sameLine()
        
        if ui.button('Cancel', vec2(100, 30), ui.ButtonFlags.Cancel) then
            confirmAction = nil
        end
    end)
end

-- Usage:
ShowConfirm("Are you sure you want to kick this player?", function()
    KickPlayer(selectedPlayer)
end)
```

## Dynamic UI Panel Sizing
```lua
-- Adjust panel size based on content
function DrawPopup(data)
    local uiState = ac.getUI()
    
    -- Dynamic height based on content
    local panelWidth = 500
    local panelHeight = data.HasExtraContent and 650 or 500
    
    local panelPos = vec2(
        uiState.windowSize.x / 2 - panelWidth / 2,
        uiState.windowSize.y / 2 - panelHeight / 2
    )
    
    -- CRITICAL: Always include inputs=true (5th parameter) for interactive windows
    ui.toolWindow('myPopup', panelPos, vec2(panelWidth, panelHeight), true, true, function()
        -- content...
    end)
end
```

## Tiered Color System (Prestige/Rank Colors)
```lua
-- Define tier colors
local tierColors = {
    default = rgbm(1, 1, 1, 1),       -- White
    tier1 = rgbm(1, 0.84, 0, 1),      -- Gold
    tier2 = rgbm(1, 0.42, 0.42, 1),   -- Rose
    tier3 = rgbm(0.6, 0.2, 0.8, 1),   -- Purple
    tier4 = rgbm(0.18, 0.8, 0.44, 1), -- Emerald
    tier5 = rgbm(1, 0, 1, 1),         -- Magenta
    tier6 = rgbm(0, 1, 1, 1),         -- Aqua
}

-- Get color based on tier/rank
function GetTierColor(rank)
    if rank >= 50 then
        -- Animated rainbow for highest tier
        local hue = (os.clock() * 0.5) % 1.0
        return HSVToRGB(hue, 1, 1)
    elseif rank >= 20 then return tierColors.tier6
    elseif rank >= 10 then return tierColors.tier5
    elseif rank >= 5 then return tierColors.tier4
    elseif rank >= 3 then return tierColors.tier3
    elseif rank >= 2 then return tierColors.tier2
    elseif rank >= 1 then return tierColors.tier1
    else return tierColors.default
    end
end

-- HSV to RGB conversion for rainbow effect
function HSVToRGB(h, s, v)
    local i = math.floor(h * 6)
    local f = h * 6 - i
    local q = v * (1 - f)
    i = i % 6
    
    if i == 0 then return rgbm(v, v * f, 0, 1)
    elseif i == 1 then return rgbm(q, v, 0, 1)
    elseif i == 2 then return rgbm(0, v, v * f, 1)
    elseif i == 3 then return rgbm(0, q, v, 1)
    elseif i == 4 then return rgbm(v * f, 0, v, 1)
    else return rgbm(v, 0, q, 1)
    end
end

-- Usage:
ui.textColored(levelDisplay, GetTierColor(prestigeRank))
```

## Server-to-Client Online Events (ac.OnlineEvent)
```lua
-- Define event structure matching server-side packet
local battleStatusEvent = ac.OnlineEvent({
    eventType = ac.StructItem.byte(),
    eventData = ac.StructItem.int64(),
}, function(sender, data)
    -- sender is nil when packet is from server
    if sender ~= nil then return end
    
    ac.debug("Event Type", data.eventType)
    
    if data.eventType == EventTypes.Challenge then
        -- Handle challenge event
        state.active = true
        state.rivalId = data.eventData
    end
end)

-- Common struct item types:
-- ac.StructItem.byte()    - 1 byte integer
-- ac.StructItem.int16()   - 2 byte integer
-- ac.StructItem.int32()   - 4 byte integer  
-- ac.StructItem.int64()   - 8 byte integer
-- ac.StructItem.float()   - 4 byte float
-- ac.StructItem.double()  - 8 byte float
-- ac.StructItem.string(maxLen) - length-prefixed string
-- ac.StructItem.vec2()    - 2x float vector
-- ac.StructItem.vec3()    - 3x float vector
```

## Online Extras Menu Registration (ui.registerOnlineExtra)
```lua
-- Register a panel in CSP's online extras menu
-- This creates a button in the online extras that opens your panel
ui.registerOnlineExtra(
    ui.Icons.Leaderboard,           -- Icon to display
    "My Feature Panel",              -- Button text
    function() return true end,      -- Condition function (return true to show button)
    function()
        -- Panel content callback
        local close = false
        
        ui.childWindow('myPanel', vec2(0, 300), false, ui.WindowFlags.None, function()
            ui.text("Panel content here")
            
            if ui.button("Close") then
                close = true
            end
        end)
        
        return close  -- Return true to close panel
    end
)

-- Note: ui.registerOnlineExtra handles input internally
-- You don't need inputs=true for buttons inside this callback
```

## Display-Only HUD Pattern
```lua
-- For HUDs that only display information (no buttons/interaction),
-- you can omit the inputs parameter since it's false by default
function DrawBattleHUD()
    local uiState = ac.getUI()
    local hudX = uiState.windowSize.x / 2 - 200
    local hudY = 25
    
    -- No buttons = no need for inputs=true (4th param is noPadding)
    ui.toolWindow('battleHUD', vec2(hudX, hudY), vec2(400, 100), true, function()
        -- Draw bars, text, etc. - no interactive elements
        ui.text("SP: 85%")
        DrawHealthBar(0.85)
    end)
end

-- IMPORTANT: If you later add buttons, you MUST add inputs=true:
-- ui.toolWindow('battleHUD', vec2(hudX, hudY), vec2(400, 100), true, true, function()
```

---

# Lua Debugging Techniques

## Console Logging
```lua
ac.log("Debug message")           -- Basic logging
ac.log("Value: %s" % tostring(x)) -- String formatting
ac.debug("Label", value)          -- Shows in debug overlay
```

## Visual Debug Overlay
```lua
function script.drawUI()
    -- Always-visible debug panel
    ui.transparentWindow('debug', vec2(10, 10), vec2(300, 100), function()
        ui.text("Admin: " .. tostring(state.isAdmin))
        ui.text("Panel: " .. tostring(state.panelOpen))
        ui.text("Players: " .. #state.players)
        ui.text("FPS: %.1f" % (1 / ac.getUI().dt))
    end)
    
    -- Main panel
    DrawMainPanel()
end
```

## Error Boundary Pattern
```lua
function SafeCall(func, ...)
    local success, result = pcall(func, ...)
    if not success then
        ac.log("Error: " .. tostring(result))
        return nil
    end
    return result
end

-- Usage:
SafeCall(DrawComplexUI)
```

## Network Request Debugging
```lua
function DebugRequest(method, url, data)
    ac.log(string.format("[%s] %s", method, url))
    if data then
        ac.log("Data: " .. stringify.stringify(data))
    end
end

function DebugResponse(err, response)
    if err then
        ac.log("Error: " .. tostring(err))
    else
        ac.log(string.format("Status: %d", response.status))
        ac.log("Body: " .. (response.body or "nil"))
    end
end
```

## Check Script Loading
In AC logs (`assettocorsa/logs/log.txt`) or CSP console:
```
[SCRIPT] online: Loading script from ...
[SCRIPT] online: Script loaded successfully
```

---

# CSP Resources & References

## Official Documentation
- [CSP Lua SDK Repository](https://github.com/ac-custom-shaders-patch/acc-lua-sdk)
- [CSP Lua Apps Wiki](https://github.com/ac-custom-shaders-patch/acc-lua-sdk/wiki/Lua-apps)
- [CSP Server Extra Options](https://cup.acstuff.club/docs/csp/misc/server-extra-options)

## Community Resources
- [CheesyLua Getting Started Guide](https://github.com/CheesyManiac/cheesy-lua/wiki/Getting-Started-with-CSP-Lua-Scripting)
- [CSP Example Apps](https://github.com/ac-custom-shaders-patch/app-csp-defaults)
- [CSP Paintshop App](https://github.com/ac-custom-shaders-patch/app-paintshop) - Advanced API showcase

## Server Resources
- [AssettoServer Documentation](https://assettoserver.org/docs/)
- [AssettoServer FAQ](https://assettoserver.org/docs/next/faq/)

## File Locations Reference

| File Type | Location |
|-----------|----------|
| CSP Lua SDK | `extension/internal/lua-sdk/` |
| Regular Lua Apps | `assettocorsa/apps/lua/` |
| Online scripts (dev) | `extension/lua/online/` |
| CSP logs | `assettocorsa/logs/log.txt` |
| Server config | Server's `cfg/csp_extra_options.ini` |
| Internal CSP apps | `extension/internal/lua-apps/` |

---

# Quick Reference: Common Mistakes

| Mistake | Symptom | Fix |
|---------|---------|-----|
| Missing `inputs=true` | Window shows but no interaction | Add 5th parameter to `ui.toolWindow()` |
| Async race condition | Feature works sometimes | Use callbacks, not fixed timers |
| Wrong script type | Functions not called | Use `script.drawUI()` for online scripts |
| No URL validation | Silent failures | Check `ac.getServerIP()` return value |
| Key held detection | Rapid toggling | Track key state for edge detection |
| No error handling | Silent crashes | Wrap web callbacks in error checks |
| Blocking operations | UI freezes | Use async patterns with callbacks |

---

# Version Information

- **Document Version**: 5.0.0
- **Last Updated**: 2026-01-15
- **CSP Version Reference**: 0.1.76+ (0.2.x recommended)
- **Lua Version**: 5.1 (LuaJIT 2.1.0-beta3)
- **AssettoServer**: SXR Fork
- **Plugin Version**: SXR Admin Tools 1.1.0+

## Revision History

### v5.0.0 (2026-01-15) - SXRSPBattlePlugin Review
- Added: `ac.OnlineEvent` pattern for server-to-client custom events (Lua)
- Added: `ui.registerOnlineExtra` pattern for CSP extras menu (Lua)
- Added: Display-only HUD pattern clarification (when inputs=true is NOT needed)
- Added: Common `ac.StructItem` types reference
- Clarified: Input handling differences between toolWindow and registerOnlineExtra

### v4.0.0 (2026-01-15) - SXRWelcomePlugin Review
- Added: Dynamic UI Panel Sizing pattern (Lua)
- Added: Tiered Color System / Prestige Colors pattern (Lua)
- Added: HSV to RGB rainbow animation helper (Lua)
- Added: Alternative HTTP Controller pattern with `[Route]` attribute (C#)
- Updated: Confirmation Dialog pattern to emphasize `inputs=true` parameter
- Clarified: `ui.toolWindow()` critical 5th parameter requirement

### v3.0.0 (2026-01-15)
- Initial comprehensive version combining C# and Lua documentation

---

*This document is maintained as part of the SXR project. For updates, see CHANGELOG.md.*
