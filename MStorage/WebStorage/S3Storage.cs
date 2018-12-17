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
    /// An IStorage implementation for Amazon S3.
    /// </summary>
    public class S3Storage : WebStorage, IStorage
    {
        private readonly Amazon.S3.AmazonS3Client client;

        public S3Storage(string accessKey, string apiKey, RegionEndpoint endpoint, string bucket, ILogger log) : base(accessKey, apiKey, bucket, log)
        {
            client = new Amazon.S3.AmazonS3Client(accessKey, apiKey, endpoint);
            base.log.LogInformation($"AmazonS3 storage backend initialized to bucket {bucket}.");
        }

        public override async Task DeleteAsync(string name)
        {
            StatusCodeThrower((await client.DeleteObjectAsync(bucket, name)).HttpStatusCode);
        }

        public override async Task<Stream> DownloadAsync(string name)
        {
            var response = await client.GetObjectAsync(bucket, name);
            StatusCodeThrower(response.HttpStatusCode);
            return response.ResponseStream;
        }

        public override async Task<IEnumerable<string>> ListAsync()
        {
            return (await client.ListObjectsAsync(bucket)).S3Objects.Select(x => x.Key);
        }

        public override async Task UploadAsync(string name, Stream file)
        {
            var r = await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest()
            {
                BucketName = bucket,
                Key = name,
                InputStream = file
            });
            StatusCodeThrower(r.HttpStatusCode);
        }


        public override string ToString()
        {
            return $"AmazonS3 {bucket}";
        }
    }
}
