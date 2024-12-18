using System.Text.Json;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using App.Utils;
using App.Factories;
using App.Services;

namespace App
{
    public class Main
    {
        private readonly VideoProcessingService _videoService;
        private readonly VideoProcessingStrategyFactory _videoProcessingStrategyFactory;
        private readonly HttpClient _httpClient;
        private readonly AppConfig _appConfig;
        private readonly ILogger<Main> _logger;

        public Main(
            VideoProcessingService videoService,
            VideoProcessingStrategyFactory videoProcessingStrategyFactory,
            HttpClient httpClient,
            AppConfig appConfig, 
            ILogger<Main> logger)
        {
            _videoService = videoService;
            _videoProcessingStrategyFactory = videoProcessingStrategyFactory;
            _httpClient = httpClient;
            _appConfig = appConfig;
            _logger = logger;
        }

        [Function("VideoTranslation")]
        public async Task<IActionResult> VideoTranslation([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request received. Body: {ReqBody}", reqBody);

            var reqData = JsonSerializer.Deserialize<RequestData>(reqBody);
            string mode = req.Query["mode"];

            var strategy = _videoProcessingStrategyFactory.GetStrategy(mode);
            try
            {
                await strategy.ProcessAsync(reqData.blobName, reqData.sourceLang, reqData.targetLang, _logger);
                return new OkObjectResult($"{reqData.blobName} is translated successfully. Check container: ${_appConfig.BlobContainerName_Target}");
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error processing the request: {ex.Message}");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }


        [Function("VideoSplitting")]
        public async Task<IActionResult> VideoSplitting([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request received. Body: {ReqBody}", reqBody);

            var reqData = JsonSerializer.Deserialize<RequestData2>(reqBody);
            if (reqData == null || string.IsNullOrEmpty(reqData.blobName))
            {
                _logger.LogWarning("Invalid request data. 'blobName' is required.");
                return new BadRequestObjectResult("Invalid request. 'blobName' is required.");
            }

            string blobName = reqData.blobName;

            try
            {
                string tempInputPath = await _videoService.SaveVideoAsync(blobName);
                await _videoService.SplitVideoAsync(blobName, tempInputPath);
                return new OkObjectResult($"{reqData.blobName} is split successfully. Check container: ${_appConfig.BlobContainerName_Processing}");
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error processing the request: {ex.Message}");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }


        // private async Task DownloadFileAsync(string url, string outputPath)
        // {
        //     var response = await _httpClient.GetAsync(url);
        //     response.EnsureSuccessStatusCode();

        //     await using FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        //     await response.Content.CopyToAsync(fileStream);
        // }

        private class RequestData
        {   
            public string blobName { get; set; }
            public string sourceLang { get; set; }
            public string targetLang { get; set; }
        }

        private class RequestData2
        {
            public string blobName {get; set;}
        }
    }
}
