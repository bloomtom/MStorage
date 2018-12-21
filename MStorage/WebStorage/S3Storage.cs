using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Microsoft.Extensions.Logging;

namespace MStorage.WebStorage
{
    /// <summary>
    /// An IStorage implementation for Amazon S3 or an S3 compatible endpoint.
    /// </summary>
    public class S3Storage : WebStorage, IStorage
    {
        private readonly Amazon.S3.AmazonS3Client client;

        /// <summary>
        /// Generates an S3 client connection to the desired region.
        /// </summary>
        /// <param name="accessKey">The access key / accoun key.</param>
        /// <param name="apiKey">The secret API key.</param>
        /// <param name="endpoint">The AWS region endpoint to connect to.</param>
        /// <param name="bucket">The bucket to use for storage and retrieval.</param>
        public S3Storage(string accessKey, string apiKey, RegionEndpoint endpoint, string bucket) : base(accessKey, apiKey, bucket)
        {
            client = new Amazon.S3.AmazonS3Client(accessKey, apiKey, endpoint);
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
            var config = new Amazon.S3.AmazonS3Config()
            {
                ServiceURL = endpoint
            };
            client = new Amazon.S3.AmazonS3Client(accessKey, apiKey, config);
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesnt.
        /// </summary>
        public override async Task DeleteAsync(string name)
        {
            try
            {
                StatusCodeThrower((await client.DeleteObjectAsync(bucket, name)).HttpStatusCode);
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name)
        {
            try
            {
                var response = await client.GetObjectAsync(bucket, name);
                StatusCodeThrower(response.HttpStatusCode);
                return response.ResponseStream;
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                StatusCodeThrower(ex.StatusCode);
                throw ex;
            }
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public override async Task<IEnumerable<string>> ListAsync()
        {
            try
            {
                return (await client.ListObjectsAsync(bucket)).S3Objects.Select(x => x.Key);
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
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false)
        {
            try
            {
                var r = await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest()
                {
                    BucketName = bucket,
                    Key = name,
                    InputStream = file,
                    AutoCloseStream = disposeStream
                });
                StatusCodeThrower(r.HttpStatusCode);
            }
            catch (Amazon.S3.AmazonS3Exception ex)
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
        /// Returns "AmazonS3 {bucket}"
        /// </summary>
        public override string ToString()
        {
            return $"AmazonS3 {bucket}";
        }
    }
}
