using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading;
using HttpProgress;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Core.Util;

namespace MStorage.WebStorage
{
    internal class AzureProgressTranslation : IProgress<StorageProgress>
    {
        private readonly System.Diagnostics.Stopwatch totalTime = new System.Diagnostics.Stopwatch();

        private readonly System.Diagnostics.Stopwatch instantTime = new System.Diagnostics.Stopwatch();
        private long lastTransferredBytes = 0;

        private readonly IProgress<ICopyProgress> progress;
        private readonly long expectedBytes;

        private bool enableReporting = true;

        public AzureProgressTranslation(IProgress<ICopyProgress> progress, long expectedBytes)
        {
            this.progress = progress;
            this.expectedBytes = expectedBytes;
            totalTime.Start();
            instantTime.Start();
        }

        public void Report(StorageProgress value)
        {
            if (!enableReporting || value.BytesTransferred == 0) { return; }
            if (value.BytesTransferred == expectedBytes)
            {
                enableReporting = false;
            }

            progress.Report(new CopyProgress(totalTime.Elapsed, Statics.ComputeInstantRate(instantTime.ElapsedTicks, value.BytesTransferred - lastTransferredBytes), value.BytesTransferred, expectedBytes));

            lastTransferredBytes = value.BytesTransferred;
            instantTime.Restart();
        }
    }

    /// <summary>
    /// Object storage backend for Microsoft Azure Blob Storage.
    /// </summary>
    public class AzureStorage : WebStorage, IStorage
    {
        private readonly CloudBlobClient client;
        private readonly CloudBlobContainer container;
        private readonly BlobRequestOptions requestOptions;
        private readonly AccessCondition emptyCondition = AccessCondition.GenerateEmptyCondition();
        private readonly OperationContext oc = new OperationContext();

        /// <summary>
        /// Create a storage connection to the Azure blob storage API
        /// </summary>
        /// <param name="account">The Azure storage account to use.</param>
        /// <param name="sasToken">The Azure SAS token (shared access signature)</param>
        /// <param name="container">The blob service container to use for storage and retrieval.</param>
        /// <param name="retryPolicy">An Azure retry policy. The default policy (if null) is to retry three times with five seconds between attempts.</param>
        /// <param name="maximumExecutionTime">The maximum amount of time to wait for any operation to complete. No limit if null.</param>
        public AzureStorage(string account, string sasToken, string container, IRetryPolicy retryPolicy = null, TimeSpan? maximumExecutionTime = null) : base(account, sasToken, container)
        {
            if (retryPolicy == null) { retryPolicy = new LinearRetry(TimeSpan.FromSeconds(5), 3); }
            requestOptions = new BlobRequestOptions()
            {
                RetryPolicy = retryPolicy,
                MaximumExecutionTime = maximumExecutionTime
            };

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
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0)
        {
            cancel.ThrowIfCancellationRequested();
            var newBlob = container.GetBlockBlobReference(name);

            try
            {
                IProgress<StorageProgress> azureProgress = progress != null ? new AzureProgressTranslation(progress, Statics.ComputeStreamLength(file, expectedStreamLength)) : null;
                await newBlob.UploadFromStreamAsync(file, emptyCondition, requestOptions, oc, azureProgress, cancel);
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
        public override async Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();
            var blob = container.GetBlobReference(name);
            if (await blob.ExistsAsync() == false)
            {
                throw new FileNotFoundException();
            }
            return await blob.OpenReadAsync(emptyCondition, requestOptions, oc, cancel);
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
            var blob = container.GetBlobReference(name);
            if (await blob.ExistsAsync() == false)
            {
                throw new FileNotFoundException();
            }
            using (Stream s = await blob.OpenReadAsync())
            {
                await s.CopyToAsync(output, expectedTotalBytes: blob.Properties.Length, progressReport: progress, cancelToken: cancel);
            }
        }

        /// <summary>
        /// Returns a collection of blob names stored in the connected container.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names.</returns>
        public override async Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();
            BlobContinuationToken continuationToken = null;
            List<string> results = new List<string>();
            do
            {
                if (cancel.IsCancellationRequested) { return await Task.FromCanceled<IEnumerable<string>>(cancel); }

                var response = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.None, null, continuationToken, requestOptions, oc, cancel);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results.Select(x => {
                    if (x is CloudBlockBlob y)
                    {
                        return y.Name;
                    }
                    return x.StorageUri.PrimaryUri.Segments.Last();
                }));
            }
            while (continuationToken != null);
            return results;
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesn't.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public override async Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();
            var delBlob = container.GetBlockBlobReference(name);
            await delBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.None, emptyCondition, requestOptions, oc, cancel);
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
