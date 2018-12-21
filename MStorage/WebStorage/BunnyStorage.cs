using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BunAPI;
using Microsoft.Extensions.Logging;

namespace MStorage.WebStorage
{
    /// <summary>
    /// An IStorage implementation for BunnyCDN.
    /// </summary>
    public class BunnyStorage : WebStorage, IStorage
    {
        BunClient client;

        /// <summary>
        /// Creates a storage connection to BunnyCDN.
        /// </summary>
        /// <param name="apiKey">Your BunnyCDN API Key.</param>
        /// <param name="storageZone">The storage zone to connect to.</param>
        public BunnyStorage(string apiKey, string storageZone) : base(apiKey, apiKey, storageZone)
        {
            client = new BunClient(apiKey, storageZone);
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesnt.
        /// </summary>
        public override async Task DeleteAsync(string name)
        {
            StatusCodeThrower(await client.DeleteFile(name));
        }

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name)
        {
            var r = await client.GetFile(name);
            StatusCodeThrower(r.StatusCode);
            return r.Stream;
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public override async Task<IEnumerable<string>> ListAsync()
        {
            var r = await client.ListFiles();
            if (r.StatusCode != HttpStatusCode.OK) { return Enumerable.Empty<string>(); }
            return r.Files.Select(x => x.ObjectName);
        }

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        public override async Task UploadAsync(string name, Stream s, bool disposeStream)
        {
            try
            {
                StatusCodeThrower(await client.PutFile(s, name, disposeStream));
            }
            finally
            {
                if (disposeStream) { s.Dispose(); }
            }
        }

        /// <summary>
        /// Returns "BunnyCDN {bucket}"
        /// </summary>
        public override string ToString()
        {
            return $"BunnyCDN {bucket}";
        }
    }
}
