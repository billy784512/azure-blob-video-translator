using Azure.Storage.Blobs;
using App.Utils;

namespace App.Factories
{
    public class BlobContainerClientFactory
    {
        private readonly Dictionary<string, BlobContainerClient> _clients;
        private readonly AppConfig _config;

        public BlobContainerClientFactory(AppConfig config){
            _config = config;
            _clients = new Dictionary<string, BlobContainerClient>
            {
                {
                    _config.BlobContainerName_Source,
                    CreateBlobContainerClient(config.BlobContainerName_Source)
                },
                {
                    _config.BlobContainerName_Target,
                    CreateBlobContainerClient(config.BlobContainerName_Target)
                },
                {
                    _config.BlobContainerName_Transcription,
                    CreateBlobContainerClient(config.BlobContainerName_Transcription)
                },
                {
                    _config.BlobContainerName_Processing,
                    CreateBlobContainerClient(config.BlobContainerName_Processing)
                }
            };
        }

        public BlobContainerClient GetClient(string containerName){
            if (_clients.TryGetValue(containerName, out var client))
            {
                return client;
            }

            throw new KeyNotFoundException($"No Blob Container client found for {containerName}");
        }

        private BlobContainerClient CreateBlobContainerClient(string containerName)
        {
            var client = new BlobContainerClient(_config.BLOB_CONNECTION_STRING, containerName);
            client.CreateIfNotExists();
            return client;
        }
    }
}