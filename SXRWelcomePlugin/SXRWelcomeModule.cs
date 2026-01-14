using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRWelcomePlugin;

/// <summary>
/// Autofac module for SXR Welcome Plugin
/// </summary>
public class SXRWelcomeModule : AssettoServerModule<SXRWelcomeConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SXRWelcomePlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        builder.RegisterType<SXRWelcomeController>()
            .AsSelf();
    }
}
