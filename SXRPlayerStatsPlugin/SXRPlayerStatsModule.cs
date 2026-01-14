using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRPlayerStatsPlugin;

/// <summary>
/// Autofac module for Player Stats Plugin dependency injection
/// </summary>
public class SXRPlayerStatsModule : AssettoServerModule<SXRPlayerStatsConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<SXRPlayerStatsPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // Stats service
        builder.RegisterType<SXRPlayerStatsService>()
            .AsSelf()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<SXRPlayerStatsController>()
            .AsSelf();
    }
}
