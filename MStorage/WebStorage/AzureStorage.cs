using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading;

namespace MStorage.WebStorage
{
    class AzureStorage : WebStorage, IStorage
    {
        private readonly CloudBlobClient client;
        private readonly CloudBlobContainer container;

        public AzureStorage(string account, string sasKey, string container, ILogger log) : base(account, sasKey, container, log)
        {
            StorageCredentials sasCredentials = new StorageCredentials(sasKey);
            CloudStorageAccount sasAccount = new CloudStorageAccount(sasCredentials, account, null, true);
            client = sasAccount.CreateCloudBlobClient();
            this.container = client.GetContainerReference(bucket);
        }

        /// <summary>
        /// Sends the given file stream to Azure with the given name.
        /// Any existing blob by the specified name is overwritten.
        /// </summary>
        public override async Task UploadAsync(string name, Stream file)
        {
            var newBlob = container.GetBlockBlobReference(name);
            await newBlob.UploadFromStreamAsync(file);
        }

        /// <summary>
        /// Opens a streaming connection to the given blob name.
        /// </summary>
        public override async Task<Stream> DownloadAsync(string name)
        {
            var blob = container.GetBlobReference(name);
            return await blob.OpenReadAsync();
        }

        /// <summary>
        /// Returns a collection of blob names stored in the connected container.
        /// Due to the design of WindowsAzure.Storage, this call will block until all results are loaded into memory.
        /// </summary>
        public override Task<IEnumerable<string>> ListAsync()
        {
            BlobContinuationToken continuationToken = null;
            List<string> results = new List<string>();
            do
            {
                var response = container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.Result.ContinuationToken;
                results.AddRange(response.Result.Results.Select(x => x.ToString()));
            }
            while (continuationToken != null);
            return Task.FromResult<IEnumerable<string>>(results);
        }

        /// <summary>
        /// Calls DeleteIfExistsAsync on the given blob name.
        /// </summary>
        public override async Task DeleteAsync(string name)
        {
            var delBlob = container.GetBlockBlobReference(name);
            await delBlob.DeleteIfExistsAsync();
        }

        /// <summary>
        /// Returns "Azure {bucket}"
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Azure {bucket}";
        }
    }
}
