using System.Text.Json;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Azure.Storage.Blobs;

using App.Utils;

namespace App
{
    public class Main
    {
        private readonly BlobContainerClientFactory _blobContainerClientFactory;
        private readonly TranscriptionClient _transcriptionClient;
        private readonly HttpClient _httpClient;
        private readonly AppConfig _appConfig;
        private readonly ILogger<Main> _logger;

        public Main(
            BlobContainerClientFactory blobContainerClientFactory, 
            TranscriptionClient transcriptionClient, 
            HttpClient httpClient, 
            AppConfig appConfig, 
            ILogger<Main> logger)
        {
            _blobContainerClientFactory = blobContainerClientFactory;
            _transcriptionClient = transcriptionClient;
            _httpClient = httpClient;
            _appConfig = appConfig;
            _logger = logger;
        }

        [Function("VideoTranslation")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string reqBody = await new StreamReader(req.Body).ReadToEndAsync();
                var reqData = JsonSerializer.Deserialize<RequestData>(reqBody);

                if (reqData == null || string.IsNullOrEmpty(reqData.blobName) || string.IsNullOrEmpty(reqData.sourceLang) || string.IsNullOrEmpty(reqData.targetLang))
                {
                    return new BadRequestObjectResult("Invalid request parameter.");
                }

                BlobContainerClient containerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Source);
                BlobClient blobClient = containerClient.GetBlobClient(reqData.blobName);

                if (!await blobClient.ExistsAsync())
                {
                    return new NotFoundObjectResult($"Blob '{reqData.blobName}' not found.");
                }


                // Read and transcribe the blob
                string rawJSON;
                using (Stream blobStream = await blobClient.OpenReadAsync())
                {
                    Stream wavStream = await TranscriptionClient.ConvertToWavAsync(blobStream, _logger);
                    wavStream.Position = 0;

                    rawJSON = await _transcriptionClient.TranscribeAsync(wavStream, reqData.sourceLang);
                    _logger.LogInformation($"Transcription completed for blob '{reqData.blobName}'.");                
                }


                // Convert JSON to VTT
                string vttName = Path.ChangeExtension(reqData.blobName, ".vtt");
                var tempVttPath = Path.GetTempFileName();

                _logger.LogInformation($"Temporary VTT file created at: {tempVttPath}");
                await JsonToVttConverter.ConvertJsonToVtt(rawJSON, tempVttPath);

                containerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Transcription);
                blobClient = containerClient.GetBlobClient(vttName);
                
                using FileStream uploadFileStream = File.OpenRead(tempVttPath);
                await blobClient.UploadAsync(uploadFileStream, overwrite: true);

                return new OkObjectResult($"{vttName} is written to blob storage successfully. Container: ${_appConfig.BlobContainerName_Transcription}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing the request: {ex.Message}");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        public class RequestData
        {   
            public string blobName { get; set; }
            public string sourceLang { get; set; }
            public string targetLang { get; set; }
        }
    }
}
