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
        private readonly TranslationClient _translationClient;
        private readonly HttpClient _httpClient;
        private readonly AppConfig _appConfig;
        private readonly ILogger<Main> _logger;

        public Main(
            BlobContainerClientFactory blobContainerClientFactory, 
            TranscriptionClient transcriptionClient, 
            TranslationClient translationClient,
            HttpClient httpClient,
            AppConfig appConfig, 
            ILogger<Main> logger)
        {
            _blobContainerClientFactory = blobContainerClientFactory;
            _transcriptionClient = transcriptionClient;
            _translationClient = translationClient;
            _httpClient = httpClient;
            _appConfig = appConfig;
            _logger = logger;
        }

        [Function("VideoTranslation")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            string tempVttPath = "";
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

                string mp4Url = blobClient.Uri.ToString(); // Used in translation phrase

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
                tempVttPath = Path.GetTempFileName();

                _logger.LogInformation($"Temporary VTT file created at: {tempVttPath}");
                await JsonToVttConverter.Convert(rawJSON, tempVttPath);


                // Upload VTT to blob
                containerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Transcription);
                blobClient = containerClient.GetBlobClient(vttName);
                
                using FileStream uploadFileStream = File.OpenRead(tempVttPath);
                await blobClient.UploadAsync(uploadFileStream, overwrite: true);


                // Call Translation
                string vttUrl = blobClient.Uri.ToString();
                string videoUrl = await _translationClient.StartProcessAsync(reqData.sourceLang, reqData.targetLang, mp4Url,vttUrl, reqData.blobName);

                string tempVideoPath = Path.Combine(Path.GetTempPath(), "temp.mp4");
                try
                {
                    await DownloadFileAsync(videoUrl, tempVideoPath);
                    containerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Target);
                    blobClient = containerClient.GetBlobClient(reqData.blobName);
                    
                    using FileStream uploadFileStream2 = File.OpenRead(tempVideoPath);
                    await blobClient.UploadAsync(uploadFileStream2, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempVideoPath))
                    {
                        File.Delete(tempVideoPath);
                    }
                }

                return new OkObjectResult($"{reqData.blobName} is translated successfully. Check container: ${_appConfig.BlobContainerName_Target}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing the request: {ex.Message}");
                return new ObjectResult(new { error = ex.Message }) { StatusCode = StatusCodes.Status500InternalServerError };
            }
            finally
            {
                File.Delete(tempVttPath);
            }
        }

        private async Task DownloadFileAsync(string url, string outputPath)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
        }

        private class RequestData
        {   
            public string blobName { get; set; }
            public string sourceLang { get; set; }
            public string targetLang { get; set; }
        }
    }
}
