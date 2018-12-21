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

        public S3Storage(string accessKey, string apiKey, RegionEndpoint endpoint, string bucket) : base(accessKey, apiKey, bucket)
        {
            client = new Amazon.S3.AmazonS3Client(accessKey, apiKey, endpoint);
        }

        public S3Storage(string accessKey, string apiKey, string endpoint, string bucket) : base(accessKey, apiKey, bucket)
        {
            var config = new Amazon.S3.AmazonS3Config()
            {
                ServiceURL = endpoint
            };
            client = new Amazon.S3.AmazonS3Client(accessKey, apiKey, config);
        }

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


        public override string ToString()
        {
            return $"AmazonS3 {bucket}";
        }
    }
}
