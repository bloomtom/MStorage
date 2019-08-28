using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using HttpProgress;

namespace MStorage.WebStorage
{
    internal class S3ProgressTranslation : IProgress<StreamTransferProgressArgs>
    {
        private readonly System.Diagnostics.Stopwatch totalTime = new System.Diagnostics.Stopwatch();

        private readonly System.Diagnostics.Stopwatch instantTime = new System.Diagnostics.Stopwatch();

        private readonly IProgress<ICopyProgress> progress;
        private readonly long expectedBytes;

        public S3ProgressTranslation(IProgress<ICopyProgress> progress, long expectedBytes)
        {
            this.progress = progress;
            this.expectedBytes = expectedBytes;
            totalTime.Start();
            instantTime.Start();
        }

        public void Report(StreamTransferProgressArgs value)
        {
            progress.Report(new CopyProgress(totalTime.Elapsed, Statics.ComputeInstantRate(instantTime.ElapsedTicks, value.IncrementTransferred), value.TransferredBytes, expectedBytes));
            instantTime.Restart();
        }

        long lastTransferred = 0;
        public void Report(UploadProgressArgs value)
        {
            progress.Report(new CopyProgress(totalTime.Elapsed, Statics.ComputeInstantRate(instantTime.ElapsedTicks, value.TransferredBytes - lastTransferred), value.TransferredBytes, expectedBytes));
            lastTransferred = value.TransferredBytes;
            instantTime.Restart();
        }
    }

    /// <summary>
    /// An IStorage implementation for Amazon S3 or an S3 compatible endpoint.
    /// </summary>
    public class S3Storage : WebStorage, IStorage
    {
        private readonly AmazonS3Client client;

        /// <summary>
        /// Generates an S3 client connection to the desired region.
        /// </summary>
        /// <param name="accessKey">The access key / accoun key.</param>
        /// <param name="apiKey">The secret API key.</param>
        /// <param name="endpoint">The AWS region endpoint to connect to.</param>
        /// <param name="bucket">The bucket to use for storage and retrieval.</param>
        public S3Storage(string accessKey, string apiKey, RegionEndpoint endpoint, string bucket) : base(accessKey, apiKey, bucket)
        {
            client = new AmazonS3Client(accessKey, apiKey, endpoint);
        }

        /// <summary>
        /// Generates an S3 compatible client connection to the desired REST endpoint.
        /// </summary>
        /// <param name="accessKey">The access key / account key.</param>
        /// <param name="apiKey">The secret API key.</param>
        /// <param name="endpoint">The HTTPS REST endpoint to connect to.</param>
        /// <param name="bucket">The bucket to use for storage and retrieval.</param>
        public S3Storage(string accessKey, string apiKey, string endpoint, string bucket) : base(accessKey, apiKey, bucket)
        {
            var config = new AmazonS3Config()
            {
                ServiceURL = endpoint,
                Timeout = TimeSpan.FromDays(10)
            };
            client = new AmazonS3Client(accessKey, apiKey, config);
        }

        /// <summary>
        /// Generates an S3 compatible client connection to the desired REST endpoint.
        /// </summary>
        /// <param name="accessKey">The access key / account key.</param>
        /// <param name="apiKey">The secret API key.</param>
        /// <param name="bucket">The bucket to use for storage and retrieval.</param>
        /// <param name="config">Optional S3 options. If left null, sane default options will be chosen.</param>
        /// <param name="endpoint">The HTTPS REST endpoint to connect to. Overrides 'config.ServiceURL' if provided.</param>
        public S3Storage(string accessKey, string apiKey, string bucket, AmazonS3Config config = null, string endpoint = null) : base(accessKey, apiKey, bucket)
        {
            if (config == null)
            {
                config = new AmazonS3Config()
                {
                    Timeout = TimeSpan.FromDays(10)
                };
            }
            if (endpoint != null) { config.ServiceURL = endpoint; }

            client = new AmazonS3Client(accessKey, apiKey, config);
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesn't.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public override async Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                StatusCodeThrower((await client.DeleteObjectAsync(bucket, name, cancel)).HttpStatusCode);
            }
            catch (AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                var response = await client.GetObjectAsync(bucket, name, cancel);
                StatusCodeThrower(response.HttpStatusCode);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public override async Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                using (var response = await client.GetObjectAsync(bucket, name, cancel))
                {
                    await response.ResponseStream.CopyToAsync(output, expectedTotalBytes: response.ContentLength, progressReport: progress, cancelToken: cancel);
                }
            }
            catch (AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public override async Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                return (await client.ListObjectsAsync(bucket, cancel)).S3Objects.Select(x => x.Key);
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires periodically with transfer progress.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0)
        {
            try
            {
                var progressTranslator = progress != null ? new S3ProgressTranslation(progress, Statics.ComputeStreamLength(file, expectedStreamLength)) : null;

                var utility = new TransferUtility(client);

                var transferRequest = new TransferUtilityUploadRequest()
                {
                    BucketName = bucket,
                    Key = name,
                    InputStream = file,
                    AutoCloseStream = disposeStream
                };
                transferRequest.UploadProgressEvent += (sender, e) =>
                {
                    if (progressTranslator != null)
                    {
                        progressTranslator.Report(e);
                    }
                };

                await utility.UploadAsync(transferRequest, cancel);
            }
            catch (AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
            finally
            {
                if (disposeStream) { file.Dispose(); }
            }
        }

        /// <summary>
        /// Aborts multi-part uploads which were started outside a given time boundary.
        /// </summary>
        /// <param name="olderThan">Uploads which were started earlier than this amount of time ago will be aborted.</param>
        /// <param name="cancel">Allows cancellation of the cleanup operation.</param>
        public override async Task CleanupMultipartUploads(TimeSpan olderThan, CancellationToken cancel = default(CancellationToken))
        {
            var uploads = await client.ListMultipartUploadsAsync(bucket, cancel);
            foreach (var upload in uploads.MultipartUploads)
            {
                if (upload.Initiated.Kind != DateTimeKind.Utc) { upload.Initiated = upload.Initiated.ToUniversalTime(); }
                if (DateTime.UtcNow - upload.Initiated > olderThan)
                {
                    await client.AbortMultipartUploadAsync(bucket, upload.Key, upload.UploadId, cancel);
                }
            }
        }

        /// <summary>
        /// Returns "AmazonS3 {bucket}"
        /// </summary>
        public override string ToString()
        {
            return $"AmazonS3 {bucket}";
        }
    }
}
