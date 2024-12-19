using Microsoft.Extensions.Logging;

using App.Services;

public class SimpleVideoProcessingStrategy : BaseVideoProcessingStrategy
{
    private ILogger<SimpleVideoProcessingStrategy> _logger;
    private readonly VideoProcessingService _videoService;

    public SimpleVideoProcessingStrategy(
        ILogger<SimpleVideoProcessingStrategy> logger, 
        IServiceProvider serviceProvider, 
        VideoProcessingService videoService)
        : base(serviceProvider)
    {
        _logger = logger;
        _videoService = videoService;
    }

    public override async Task ProcessAsync(string blobName, string sourceLang, string targetLang)
    {
        _logger.LogInformation("Processing in simple mode...");
        string tempInputPath = "";

        try
        {
            tempInputPath = await _videoService.SaveVideoAsync(blobName);
            var tempOutput = await _videoService.SplitVideoAsync(blobName, tempInputPath);
            var videoUrls = await TranslateInParallel(sourceLang, targetLang, tempOutput, _logger);

            _logger.LogInformation("All split files uploaded successfully.");
            foreach (var url in videoUrls.Where(url => url != null))
            {
                _logger.LogInformation("Video URL: {VideoUrl}", url);

            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing the request for blob {BlobName}: {ErrorMessage}", blobName, ex.Message);
        }
        finally
        {
            if (File.Exists(tempInputPath))
            {
                File.Delete(tempInputPath);
                _logger.LogInformation("Temporary input file deleted: {TempInputPath}", tempInputPath);
            }
        }
    }
}