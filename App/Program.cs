using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using App.Utils;
using App;

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

        services.AddSingleton<BlobContainerClientFactory>();
        services.AddSingleton<TranscriptionClient>();
        services.AddHttpClient();
    })
    .Build();

host.Run();
