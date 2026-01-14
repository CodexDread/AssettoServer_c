using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// Autofac module for SP Battle Plugin dependency injection
/// </summary>
public class SXRSPBattleModule : AssettoServerModule<SXRSPBattleConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<SXRSPBattlePlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // Per-car battle handlers (factory registered)
        builder.RegisterType<SXREntryCarBattle>().AsSelf();
        
        // Battle instances (factory registered)
        builder.RegisterType<Battle>().AsSelf();
        
        // Leaderboard service
        builder.RegisterType<SXRLeaderboardService>()
            .AsSelf()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<LeaderboardController>().AsSelf();
    }
}
