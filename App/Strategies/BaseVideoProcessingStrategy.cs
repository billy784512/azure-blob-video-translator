using App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public abstract class BaseVideoProcessingStrategy
{
    private readonly IServiceProvider _serviceProvider;

    public BaseVideoProcessingStrategy(IServiceProvider serviceProvider){
        _serviceProvider = serviceProvider;
    }
    public abstract Task ProcessAsync(string blobName, string sourceLang, string targetLang, ILogger logger);

    public async Task<string[]> TranslateInParallel(string sourceLang, string targetLang, List<Tuple<string, string>> tempOutput, ILogger logger)
    {
        var semaphore = new SemaphoreSlim(5);           // Cocurrent limit: 5
        using var cts = new CancellationTokenSource();
        var translationTasks = tempOutput.Select(async temp =>
        {
            await semaphore.WaitAsync();
            try
            {
                cts.Token.ThrowIfCancellationRequested();

                using var scope = _serviceProvider.CreateScope();
                var translationService = scope.ServiceProvider.GetRequiredService<VideoTranslationService>();
                
                var videoUrl = await translationService.StartProcessAsync(sourceLang, targetLang, temp.Item1, temp.Item2);
                logger.LogInformation("Task completed for {TempFile}, video URL: {VideoUrl}", temp.Item1, videoUrl);
                return videoUrl;
            }
            catch (Exception ex)
            {
                logger.LogError("Error in task for {TempFile}: {ErrorMessage}", temp.Item1, ex.Message);
                cts.Cancel();
                throw;
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(translationTasks);
    }
}
