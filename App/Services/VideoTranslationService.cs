using System.Text.Json;
using System.Text;

using Microsoft.Extensions.Logging;

using App.Utils;

namespace App.Services
{
    public class VideoTranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfig _appConfig;
        private readonly ILogger<VideoTranslationService> _logger;

        private readonly string baseUrl;
        private readonly string translationId;
        private string iterationId;
        private string operationId;

        public VideoTranslationService(HttpClient httpClient, AppConfig appConfig, ILogger<VideoTranslationService> logger)
        {
            _httpClient = httpClient;
            _appConfig = appConfig;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _appConfig.SPEECH_SERVICE_KEY);

            baseUrl = String.Format(_appConfig.TRANSLATION_API_URL, _appConfig.REGION);
            translationId = Guid.NewGuid().ToString();
        }

        public async Task<string> StartProcessAsync(string sourceLang, string targetLang, string mp4Name, string mp4Url, string vttUrl = null)
        {
            await CreateTranslationAsync(sourceLang, targetLang, mp4Url, mp4Name);
            await Polling();
            await CreateIteraionAsync(vttUrl);
            await Polling();
            string videoUrl = await GetIterationAsync();
            return videoUrl;
        }

        private async Task CreateTranslationAsync (string sourceLang, string targetLang, string mp4Url, string mp4Name)
        {
            this.operationId = Guid.NewGuid().ToString();
            string urlTemplate = baseUrl + "/translations/{0}?api-version={1}";
            string url = String.Format(urlTemplate, translationId, _appConfig.TRANSLATION_API_VERSION);

            var rawContent = new 
            {
                displayName = mp4Name,
                description = "",
                input = new{
                            sourceLocale = sourceLang,
                            targetLocale = targetLang,
                            voiceKind = "PlatformVoice",
                            videoFileUrl = mp4Url
                        }
            };

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(rawContent), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Operation-Id", operationId);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode(); 
            return;           
        }

        private async Task CreateIteraionAsync (string vttUrl)
        {
            iterationId = Guid.NewGuid().ToString();
            operationId = Guid.NewGuid().ToString();

            string urlTemplate = baseUrl + "/translations/{0}/iterations/{1}?api-version={2}";
            string url = String.Format(urlTemplate, translationId, iterationId, _appConfig.TRANSLATION_API_VERSION);

            HttpRequestMessage request;

            if (!string.IsNullOrEmpty(vttUrl))
            {
                var rawContent = new 
                {
                    input = new
                    {
                        webvttFile = new
                        {
                            Kind = "SourceLocaleSubtitle",
                            url = vttUrl
                        }
                    }
                };

                request = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(rawContent), Encoding.UTF8, "application/json")
                };
            }
            else
            {
                var emptyContent = new {};
                request = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(emptyContent), Encoding.UTF8, "application/json")
                };
            }
            request.Headers.Add("Operation-Id", operationId);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return;
        }

        private async Task<bool> CheckOperationSucceededAsync()
        {
            string urlTemplate = baseUrl + "/operations/{0}?api-version={1}";
            string url = String.Format(urlTemplate, operationId, _appConfig.TRANSLATION_API_VERSION);

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonBody = JsonDocument.Parse(responseBody);

            string status = jsonBody.RootElement.GetProperty("status").GetString();
            if (status == "Succeeded")
            {
                return true;
            }
            return false;
        }

        private async Task<string> GetIterationAsync()
        {
            string urlTemplate = baseUrl + "/translations/{0}/iterations/{1}?api-version={2}";
            string url = String.Format(urlTemplate, translationId, iterationId, _appConfig.TRANSLATION_API_VERSION);

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonBody = JsonDocument.Parse(responseBody);
            string videoUrl = jsonBody.RootElement.GetProperty("result").GetProperty("translatedVideoFileUrl").GetString();

            return videoUrl;
        }

        private async Task Polling()
        {
            _logger.LogInformation("Polling...");
            bool success = false;
            while(! success)
            {
                await Task.Delay(10000);
                success = await CheckOperationSucceededAsync();
            }
            _logger.LogInformation("Polling Done!");
            return;
        }
    }
}