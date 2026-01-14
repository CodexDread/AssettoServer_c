using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRNameplatesPlugin;

/// <summary>
/// Autofac module for Nameplates Plugin dependency injection
/// </summary>
public class SXRNameplatesModule : AssettoServerModule<SXRNameplatesConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<SXRNameplatesPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<SXRNameplatesController>()
            .AsSelf();
    }
}
