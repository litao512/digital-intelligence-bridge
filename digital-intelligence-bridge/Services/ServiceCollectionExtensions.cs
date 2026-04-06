using System;
using System.Net.Http;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.ViewModels;
using DigitalIntelligenceBridge.Views;
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
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        services.AddSingleton<ISupabaseService, SupabaseService>();
        services.AddSingleton<IReleaseCenterService, ReleaseCenterService>();
        services.AddSingleton<ITodoRepository, SupabaseTodoRepository>();
        services.AddSingleton<PluginCatalogService>();
        services.AddSingleton<PluginLoaderService>();
        services.AddSingleton<IDrugExcelImportService, DrugExcelImportService>();
        services.AddSingleton<DrugImportRepository>();
        services.AddSingleton<IDrugImportRepository>(provider => provider.GetRequiredService<DrugImportRepository>());
        services.AddSingleton<IDrugCatalogSyncRepository>(provider => provider.GetRequiredService<DrugImportRepository>());
        services.AddSingleton<IDrugImportPipelineService, DrugImportPipelineService>();
        services.AddSingleton<ISqlServerDrugSyncService, SqlServerDrugSyncService>();
        return services;
    }

    public static IServiceCollection AddViewsAndViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DrugImportViewModel>();
        return services;
    }
}

