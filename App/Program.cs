using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using App.Utils;
using App.Services;
using App.Factories;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration(config => {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) => {
        var config = context.Configuration.Get<AppConfig>() ?? throw new InvalidOperationException("AppConfig could not be loaded");
        services.AddSingleton(config);

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient();

        services.AddSingleton<VideoProcessingStrategyFactory>();
        services.AddSingleton<BlobContainerClientFactory>();

        services.AddScoped<SimpleVideoProcessingStrategy>();

        services.AddSingleton<VideoProcessingService>();
        services.AddTransient<VideoTranslationService>();

        // services.AddSingleton<TranscriptionClient>();
    })
    .Build();

host.Run();
