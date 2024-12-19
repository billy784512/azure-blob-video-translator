namespace App.Utils
{
    public class AppConfig
    {
        public string BLOB_CONNECTION_STRING { get; set; }
        public string SPEECH_SERVICE_KEY { get; set; }
        
        public string TRANSCRIPTION_API_URL { get; set; }
        public string TRANSLATION_API_URL { get; set; }
        public string TRANSLATION_API_VERSION { get; set; }
        public string REGION { get; set; }

        public string BlobContainerName_Source { get; set; }
        public string BlobContainerName_Target { get; set; }
        public string BlobContainerName_Transcription { get; set; }
        public string BlobContainerName_Processing { get; set; }
        public IEnumerable<string> ContainerNames { get; set; }
    }
}