using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace App
{
    public class JsonToVttConverter
    {
        public static async Task ConvertJsonToVtt(string jsonString, string vttPath)
        {
            var transcription = JsonSerializer.Deserialize<TranscriptionResult>(jsonString);

            using (StreamWriter writer = new StreamWriter(vttPath))
            {
                // Write the WebVTT header
                await writer.WriteLineAsync("WEBVTT");
                await writer.WriteLineAsync();

                // Write each phrase as a VTT entry
                foreach (var phrase in transcription.Phrases)
                {
                    // Convert offset and duration to WebVTT timestamps
                    string startTime = ConvertMillisecondsToTimestamp(phrase.OffsetMilliseconds);
                    string endTime = ConvertMillisecondsToTimestamp(phrase.OffsetMilliseconds + phrase.DurationMilliseconds);

                    // Write the timestamps and text
                    await writer.WriteLineAsync($"{startTime} --> {endTime}");
                    await writer.WriteLineAsync(phrase.Text);
                    await writer.WriteLineAsync(); // Blank line after each entry
                }
            }

            Console.WriteLine($"WebVTT file created at: {vttPath}");
        }

        private static string ConvertMillisecondsToTimestamp(int milliseconds)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);
            return timeSpan.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        }

        // Define the JSON structure
        public class TranscriptionResult
        {
            [JsonPropertyName("phrases")]
            public Phrase[] Phrases { get; set; }
        }

        public class Phrase
        {
            [JsonPropertyName("offsetMilliseconds")]
            public int OffsetMilliseconds { get; set; }

            [JsonPropertyName("durationMilliseconds")]
            public int DurationMilliseconds { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }
        }
    }
}
