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
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpClient();

        var config = context.Configuration.Get<AppConfig>() ?? throw new InvalidOperationException("AppConfig could not be loaded");
        var containerNames = Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAMES")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? throw new InvalidOperationException("BLOB_CONTAINER_NAMES is not set in environment variables.");
        config.ContainerNames = containerNames;

        services.AddSingleton(config);

        services.AddSingleton<BlobContainerClientFactory>();

        services.AddTransient<SimpleVideoProcessingStrategy>();
        services.AddTransient<VideoProcessingStrategyFactory>();

        services.AddTransient<VideoProcessingService>();
        services.AddTransient<VideoTranslationService>();

        // services.AddSingleton<TranscriptionClient>();
    })
    .Build();

host.Run();
