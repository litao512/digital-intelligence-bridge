using System;
using System.Net.Http;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.ViewModels;
using DigitalIntelligenceBridge.Views;
using PluginAuthorizedResourceCacheService = DigitalIntelligenceBridge.Plugin.Abstractions.IAuthorizedResourceCacheService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DigitalIntelligenceBridge.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAppConfiguration();
        services.AddLogging(builder => { builder.AddSerilog(dispose: true); });
        services.AddSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));
        services.AddSingleton<IApplicationService, ApplicationService>();
        services.AddSingleton<ISiteRegistrationDialogService, SiteRegistrationDialogService>();
        services.AddSingleton<IResourceApplicationDialogService, ResourceApplicationDialogService>();
        var authorizedResourceCacheService = new AuthorizedResourceCacheService();
        services.AddSingleton(authorizedResourceCacheService);
        services.AddSingleton<IAuthorizedResourceCacheService>(authorizedResourceCacheService);
        services.AddSingleton<PluginAuthorizedResourceCacheService>(authorizedResourceCacheService);
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        services.AddSingleton<ISupabaseService, SupabaseService>();
        services.AddSingleton<IReleaseCenterService, ReleaseCenterService>();
        services.AddSingleton<ITodoRepository, SupabaseTodoRepository>();
        services.AddSingleton<PluginCatalogService>();
        services.AddSingleton<PluginLoaderService>();
        return services;
    }

    public static IServiceCollection AddViewsAndViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        return services;
    }
}

