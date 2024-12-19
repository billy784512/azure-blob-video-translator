using System.Collections.Concurrent;

using Azure.Storage.Blobs;

using App.Utils;

namespace App.Factories
{
    public class BlobContainerClientFactory
    {
        private readonly ConcurrentDictionary<string, BlobContainerClient> _clients;
        private readonly AppConfig _config;

        public BlobContainerClientFactory(AppConfig config){
            _config = config;
            _clients = new ConcurrentDictionary<string, BlobContainerClient>();
            
            foreach (var containerName in _config.ContainerNames)
            {
                _clients[containerName] = CreateBlobContainerClient(containerName);
            }
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