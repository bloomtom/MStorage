using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using HttpProgress;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Runtime.CompilerServices;

namespace MStorage.WebStorage
{
    internal class AzureProgressTranslation : IProgress<long>
    {
        private readonly System.Diagnostics.Stopwatch totalTime = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch instantTime = new System.Diagnostics.Stopwatch();

        private readonly IProgress<ICopyProgress> progress;
        private readonly long expectedBytes;

        private bool enableReporting = true;
        private long lastBytes = 0;

        public AzureProgressTranslation(IProgress<ICopyProgress> progress, long expectedBytes)
        {
            this.progress = progress;
            this.expectedBytes = expectedBytes;
            totalTime.Start();
            instantTime.Start();
        }

        public void Report(long value)
        {
            if (!enableReporting || value == 0) { return; }
            if (value == expectedBytes)
            {
                enableReporting = false;
            }
            else if (lastBytes ==  value)
            {
                return;
            }

            lastBytes = value;
            long bytesPerSecond = value * 1000 / Math.Max(1, instantTime.ElapsedMilliseconds);
            instantTime.Restart();

            progress.Report(new CopyProgress(totalTime.Elapsed, bytesPerSecond, value, expectedBytes));
        }
    }

    /// <summary>
    /// Object storage backend for Microsoft Azure Blob Storage
    /// </summary>
    public class AzureStorage : WebStorage, IStorage
    {
        private readonly BlobContainerClient containerClient;
        private readonly BlobUploadOptions uploadOptions;
        private readonly BlobOpenReadOptions downloadOptions;


        /// <summary>
        /// Create a storage connection to the Azure blob storage API
        /// </summary>
        /// <param name="client">A caller defined blob storage client to use.</param>
        /// <param name="uploadOptions">Options to use on blob upload. These are shared between all upload operations.</param>
        /// 

        /// <summary>
        /// Create a storage connection to the Azure blob storage API
        /// </summary>
        /// <param name="client">A caller defined blob storage client to use.</param>
        /// <param name="uploadOptions"></param>
        /// <param name="downloadOptions"></param>
        public AzureStorage(BlobContainerClient client, BlobUploadOptions uploadOptions = null, BlobOpenReadOptions downloadOptions = null) : base(client.AccountName, null, client.Name)
        {
            this.uploadOptions = uploadOptions ?? new BlobUploadOptions();
            this.downloadOptions = downloadOptions ?? new BlobOpenReadOptions(false);
            this.containerClient = client;
        }

        /// <summary>
        /// Create a storage connection to the Azure blob storage API using API address {account}.blob.core.windows.net/{container}
        /// </summary>
        /// <param name="account">Blob account to use.</param>
        /// <param name="sasToken">The sas authentication token for a new container client.</param>
        /// <param name="container">The blob container to address.</param>
        /// <param name="clientOptions">Called defined client options. Null will generate default options with 3 retries and a timeout of 10 seconds.</param>
        /// <param name="uploadOptions">Options to use on blob upload. These are shared between all upload operations.</param>
        /// <param name="downloadOptions">Options to use on blob download. These are shared between all download operations.</param>
        public AzureStorage(string account, string sasToken, string container, BlobClientOptions clientOptions = null, BlobUploadOptions uploadOptions = null, BlobOpenReadOptions downloadOptions = null) : base(account, sasToken, container)
        {
            clientOptions ??= new BlobClientOptions()
            {
                Retry = {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxRetries = 3,
                    Mode = RetryMode.Exponential,
                    NetworkTimeout = TimeSpan.FromSeconds(10)
                }
            };
            this.uploadOptions = uploadOptions ?? new BlobUploadOptions();
            this.downloadOptions = downloadOptions ?? new BlobOpenReadOptions(false);

            string blobUri = $"https://{account}.blob.core.windows.net/{container}";
            containerClient = new BlobContainerClient(new Uri($"{blobUri}?{sasToken}"), clientOptions);
        }

        /// <summary>
        /// Sends the given file stream to Azure with the given name.
        /// Any existing blob by the specified name is overwritten.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed when this method exits.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it. Setting this will override the progress property in uploadOptions.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default, long expectedStreamLength = 0)
        {
            try
            {
                cancel.ThrowIfCancellationRequested();
                var newBlob = containerClient.GetBlobClient(name);

                var azureProgress = progress != null ? new AzureProgressTranslation(progress, Statics.ComputeStreamLength(file, expectedStreamLength)) : null;

                var uploadOptions = new BlobUploadOptions()
                {
                    AccessTier = this.uploadOptions.AccessTier,
                    Conditions = this.uploadOptions.Conditions,
                    HttpHeaders = this.uploadOptions.HttpHeaders,
                    ImmutabilityPolicy = this.uploadOptions.ImmutabilityPolicy,
                    LegalHold = this.uploadOptions.LegalHold,
                    Metadata = this.uploadOptions.Metadata,
                    ProgressHandler = azureProgress ?? this.uploadOptions.ProgressHandler,
                    Tags = this.uploadOptions.Tags,
                    TransferOptions = this.uploadOptions.TransferOptions,
                    TransferValidation = this.uploadOptions.TransferValidation
                };
                
                await newBlob.UploadAsync(file, uploadOptions, cancel);
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
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();
            var blob = containerClient.GetBlobClient(name);
            if (await blob.ExistsAsync() == false)
            {
                throw new FileNotFoundException();
            }

            return await blob.OpenReadAsync(downloadOptions, cancel);
        }

        /// <summary>
        /// Opens a streaming connection to the given blob name.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public override async Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();
            var blob = containerClient.GetBlobClient(name);
            if (await blob.ExistsAsync() == false)
            {
                throw new FileNotFoundException();
            }

            var blobProperties = (await blob.GetPropertiesAsync(null, cancel)).Value;
            using Stream s = await blob.OpenReadAsync();
            await s.CopyToAsync(output, expectedTotalBytes: blobProperties.ContentLength, progressReport: progress, cancelToken: cancel);
        }

        /// <summary>
        /// Returns a collection of blob names stored in the connected container.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names.</returns>
        public override async IAsyncEnumerable<string> ListAsync([EnumeratorCancellation] CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            var blobs = containerClient.GetBlobsAsync(cancellationToken: cancel);

            await foreach (var blob in blobs)
            {
                yield return blob.Name;
            }
        }
        
        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesn't.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public override async Task DeleteAsync(string name, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();
            var delBlob = containerClient.GetBlobClient(name);
            await delBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, cancellationToken: cancel);
        }

        /// <summary>
        /// Returns "Azure {bucket}"
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Azure {bucket}";
        }

        /// <summary>
        /// This method is unused for this type. It will immediately return a completed task.
        /// </summary>
        public override Task CleanupMultipartUploads(TimeSpan olderThan, CancellationToken cancel = default(CancellationToken))
        {
            return Task.CompletedTask;
        }
    }
}
