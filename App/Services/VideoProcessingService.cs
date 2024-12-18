using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Azure.Storage.Blobs;

using App.Utils;
using App.Factories;

namespace App.Services
{
    public class VideoProcessingService
    {
        private ILogger<VideoProcessingService> _logger;
        private readonly BlobContainerClientFactory _blobContainerClientFactory;
        private readonly AppConfig _appConfig;
        private static readonly string _ffmpegPath =  Path.Combine(Environment.CurrentDirectory, "Tools", "ffmpeg");
        private static readonly int _splitSize = 480;

        public VideoProcessingService(BlobContainerClientFactory blobContainerClientFactory, AppConfig appConfig, ILogger<VideoProcessingService> logger){
            _blobContainerClientFactory = blobContainerClientFactory;
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task<List<Tuple<string, string>>> SplitVideoAsync(string blobName, string tempInputPath)
        {
            _logger.LogInformation("Splitting video for '{tempInputPath}'...", tempInputPath);

            var tempOutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempOutputDirectory);

            try
            {
                var splitDuration = CalculateSplitDuration(tempInputPath);
                var outputFormat = Path.Combine(tempOutputDirectory, "output%03d.mp4");
                var arguments = $"-i \"{tempInputPath}\" -c copy -map 0 -f segment -segment_time {splitDuration} -reset_timestamps 1 \"{outputFormat}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = arguments,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _logger.LogInformation("Splitting...");
                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"FFmpeg failed: {process.StandardError.ReadToEnd()}");
                }

                int cnt = 1;
                List<Tuple<string, string>> output = [];
                foreach (var file in Directory.GetFiles(tempOutputDirectory, "*.mp4"))
                {
                    string remoteFileName = $"{Path.GetFileNameWithoutExtension(blobName)}_{cnt++}.mp4";
                    string blobUrl = await UploadVideoAsync(file, remoteFileName);
                    output.Add(Tuple.Create(file, blobUrl));
                    File.Delete(file);
                }

                return output;
            }
            catch(Exception ex)
            {
                foreach (var file in Directory.GetFiles(tempOutputDirectory, "*.mp4"))
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                throw;
            }
        }

        public async Task<string> SaveVideoAsync(string blobName)
        {
            string tempPath = Path.GetTempFileName();
            _logger.LogInformation("Temporary input file created at: {TempInputPath}", tempPath);

            _logger.LogInformation("Downloading blob '{BlobName}' from container '{SourceContainer}'...", blobName, _appConfig.BlobContainerName_Source);
            BlobContainerClient inputContainerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Source);
            BlobClient blobClient = inputContainerClient.GetBlobClient(blobName);
            await blobClient.DownloadToAsync(tempPath);
            _logger.LogInformation("Blob '{BlobName}' downloaded successfully to: {TempInputPath}", blobName, tempPath);

            return tempPath;
        }

        public async Task<string> UploadVideoAsync(string localFilePath, string RemoteFileName)
        {
            BlobContainerClient containerClient = _blobContainerClientFactory.GetClient(_appConfig.BlobContainerName_Processing);
            BlobClient blobClient = containerClient.GetBlobClient(RemoteFileName);
       
            await blobClient.UploadAsync(localFilePath, overwrite: true);

            _logger.LogInformation("File '{RemoteFileName}' uploaded successfully.", RemoteFileName);
            return blobClient.Uri.ToString();
        }

        public static async Task<MemoryStream> ConvertToWavAsync (Stream mp4Stream, ILogger logger)
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
                // Create conversion process
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
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
        
        private int CalculateSplitDuration(string inputPath)
        {
            var arguments = $"-i \"{inputPath}\" -hide_banner";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // 從 FFmpeg 輸出中提取時長
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d+):(\d+):(\d+)");
            if (match.Success)
            {
                var hours = int.Parse(match.Groups[1].Value);
                var minutes = int.Parse(match.Groups[2].Value);
                var seconds = int.Parse(match.Groups[3].Value);
                var totalSeconds = hours * 3600 + minutes * 60 + seconds;

                // 計算分片時長
                var fileSizeMB = new FileInfo(inputPath).Length / (1024 * 1024);
                return (int)Math.Ceiling((double)totalSeconds * _splitSize / fileSizeMB);
            }

            throw new Exception("Failed to get video duration.");
        }
    }
}
