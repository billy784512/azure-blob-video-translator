using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using NAudio.Wave;

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

        public static async Task<MemoryStream> ConvertToWavAsync (Stream mp4Stream, ILogger logger)
        {
            logger.LogInformation("Starting MP4 to WAV conversion.");

            var tempMp4Path = Path.GetTempFileName();
            logger.LogInformation($"Temporary MP4 file created at: {tempMp4Path}");

            await using (var tempMp4File = new FileStream(tempMp4Path, FileMode.Create, FileAccess.Write))
            {
                await mp4Stream.CopyToAsync(tempMp4File);
                logger.LogInformation($"MP4 stream written to {tempMp4Path}. File size: {tempMp4File.Length} bytes.");
            }

            var wavStream = new MemoryStream();

            try
            {
                logger.LogInformation("Converting MP4 to WAV...");
                using (var mediaReader = new MediaFoundationReader(tempMp4Path))
                {
                    WaveFileWriter.WriteWavFileToStream(wavStream, mediaReader);
                    logger.LogInformation($"WAV data written to MemoryStream. Length: {wavStream.Length} bytes.");
                }
                wavStream.Position = 0;
                logger.LogInformation("MP4 to WAV conversion completed successfully.");
            }
            catch (Exception ex)
            {
                // Log any exception during the conversion process
                logger.LogError($"Error during MP4 to WAV conversion: {ex.Message}");
                throw;
            }
            finally
            {
                File.Delete(tempMp4Path);
            }

            return wavStream;
        }      

        public static async Task<MemoryStream> ConvertToWavAsyncWithFFmpeg (Stream mp4Stream, ILogger logger)
        {
            logger.LogInformation("Starting MP4 to WAV conversion using FFmpeg.");

            var tempMp4Path = Path.GetTempFileName();
            var tempWavPath = Path.ChangeExtension(tempMp4Path, ".wav");
            logger.LogInformation($"Temporary MP4 file created at: {tempMp4Path}");
            logger.LogInformation($"Temporary WAV file will be created at: {tempWavPath}");

            await using (var tempMp4File = new FileStream(tempMp4Path, FileMode.Create, FileAccess.Write))
            {
                await mp4Stream.CopyToAsync(tempMp4File);
                logger.LogInformation($"MP4 stream written to temporary file. File size: {tempMp4File.Length} bytes.");
            }

            try
            {
                // Path to the FFmpeg binary in the repo
                var ffmpegPath = Path.Combine(Environment.CurrentDirectory, "Tools", "ffmpeg");
                logger.LogInformation($"Using FFmpeg binary at: {ffmpegPath}");

                // Create conversion process
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{tempMp4Path}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{tempWavPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                logger.LogInformation("Converting MP4 to WAV...");
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    logger.LogError($"FFmpeg conversion failed. Exit Code: {process.ExitCode}, Error: {error}");
                    throw new Exception("FFmpeg conversion failed.");
                }

                logger.LogInformation("MP4 to WAV conversion completed successfully.");

                var wavStream = new MemoryStream();
                await using (var fileStream = new FileStream(tempWavPath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(wavStream);
                    logger.LogInformation($"WAV data written to MemoryStream. Length: {wavStream.Length} bytes.");
                }

                wavStream.Position = 0; // Reset position for reading
                return wavStream;
            }
            finally
            {
                // Cleanup temporary files
                File.Delete(tempMp4Path);
                File.Delete(tempWavPath);
                logger.LogInformation($"Temporary files deleted: {tempMp4Path}, {tempWavPath}");
            }
        }
    }
}
