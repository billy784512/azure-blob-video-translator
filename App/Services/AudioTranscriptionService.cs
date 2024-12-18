using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using App.Utils;

namespace App
{
    public class TranscriptionClient
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfig _appConfig;
        private readonly ILogger<TranscriptionClient> _logger;

        public TranscriptionClient(HttpClient httpClient, AppConfig appConfig, ILogger<TranscriptionClient> logger)
        {
            _httpClient = httpClient;
            _appConfig = appConfig;
            _logger = logger;
        } 

        public async Task<string> TranscribeAsync(Stream wavStream, string locales)
        {
            if (wavStream == null || wavStream.Length == 0)
            {
                _logger.LogError("The WAV stream is empty or null");
                throw new ArgumentException("The WAV stream is empty or null", nameof(wavStream));
            }

            // Reset streaming pointer
            wavStream.Position = 0;

            // Add the subscription key in headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _appConfig.SPEECH_SERVICE_KEY);
            
            // Prepare the multipart form data
            using var content = new MultipartFormDataContent();

            // Add WAV to form data
            var streamContent = new StreamContent(wavStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(streamContent, "audio", "audio.wav");

            // Add definition
            var definition = new {
                locales = new[] { locales }
            };
            var definitionJson = JsonSerializer.Serialize(definition);
            content.Add(new StringContent(definitionJson), "definition");


            // Call API
            var response = await _httpClient.PostAsync(_appConfig.TRANSCRIPTION_API_URL, content);


            if (!response.IsSuccessStatusCode)
            {
                string message = $"Fast Transcription API failed, Status Code: {response.StatusCode}, Message: {response.ReasonPhrase}"; 
                _logger.LogError(message);
                throw new Exception(message);
            }

            return await response.Content.ReadAsStringAsync();
        }   
    }
}
