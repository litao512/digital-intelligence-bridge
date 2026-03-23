using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.Services;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class AppPluginRegistrationTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterPluginHostServices()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<PluginCatalogService>());
        Assert.NotNull(provider.GetService<PluginLoaderService>());
    }

    [Fact]
    public void PrismContainer_ShouldResolvePluginHostServices_WhenRegisteredByApp()
    {
        var container = new Container();

        container.Register<PluginCatalogService>(Reuse.Singleton);
        container.Register<PluginLoaderService>(Reuse.Singleton);

        Assert.NotNull(container.Resolve<PluginCatalogService>());
        Assert.NotNull(container.Resolve<PluginLoaderService>());
    }
}
