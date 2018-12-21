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
    /// <summary>
    /// Object storage backend for Microsoft Azure Blob Storage.
    /// </summary>
    public class AzureStorage : WebStorage, IStorage
    {
        private readonly CloudBlobClient client;
        private readonly CloudBlobContainer container;

        /// <summary>
        /// Create a storage connection to the Azure blob storage API
        /// </summary>
        /// <param name="account">The Azure storage account to use.</param>
        /// <param name="sasToken">The Azure SAS token (shared access signature)</param>
        /// <param name="container">The blob service container to use for storage and retrieval.</param>
        public AzureStorage(string account, string sasToken, string container) : base(account, sasToken, container)
        {
            StorageCredentials sasCredentials = new StorageCredentials(sasToken);
            CloudStorageAccount sasAccount = new CloudStorageAccount(sasCredentials, account, null, true);
            client = sasAccount.CreateCloudBlobClient();
            this.container = client.GetContainerReference(container);
        }

        /// <summary>
        /// Sends the given file stream to Azure with the given name.
        /// Any existing blob by the specified name is overwritten.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false)
        {
            var newBlob = container.GetBlockBlobReference(name);

            try
            {
                await newBlob.UploadFromStreamAsync(file);
            }
            finally
            {
                if (disposeStream) { file.Dispose(); }
            }
        }

        /// <summary>
        /// Opens a streaming connection to the given blob name.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name)
        {
            var blob = container.GetBlobReference(name);
            if (await blob.ExistsAsync() == false)
            {
                throw new FileNotFoundException();
            }
            return await blob.OpenReadAsync();
        }

        /// <summary>
        /// Returns a collection of blob names stored in the connected container.
        /// Due to the design of WindowsAzure.Storage, this call will block until all results are loaded into memory.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public override Task<IEnumerable<string>> ListAsync()
        {
            BlobContinuationToken continuationToken = null;
            List<string> results = new List<string>();
            do
            {
                var response = container.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.Result.ContinuationToken;
                results.AddRange(response.Result.Results.Select(x => {
                    if (x is CloudBlockBlob y)
                    {
                        return y.Name;
                    }
                    return x.StorageUri.PrimaryUri.Segments.Last();
                }));
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
