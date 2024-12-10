namespace App.Utils
{
    public class AppConfig
    {
        public string BLOB_CONNECTION_STRING { get; set; }
        public string SPEECH_SERVICE_KEY { get; set; }
        public string TRANSCRIPTION_API_URL { get; set; }

        public string BlobContainerName_Source { get; set; }
        public string BlobContainerName_Target { get; set; }
        public string BlobContainerName_Transcription { get; set; }
    }
}