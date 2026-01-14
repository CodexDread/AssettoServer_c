using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRCarLockPlugin;

/// <summary>
/// Autofac module for SXR Car Lock Plugin
/// </summary>
public class SXRCarLockModule : AssettoServerModule<SXRCarLockConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SXRCarLockPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        builder.RegisterType<SXRCarLockController>()
            .AsSelf();
    }
}
