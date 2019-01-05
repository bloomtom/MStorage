using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BunAPI;
using HttpProgress;

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
        public override async Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            StatusCodeThrower(await client.DeleteFile(name, cancel));
        }

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        public override async Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            var r = await client.GetFile(name, cancel);
            StatusCodeThrower(r.StatusCode);
            return r.Stream;
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
            var r = await client.GetFile(name, output, progress, cancel);
            StatusCodeThrower(r);
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public override async Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken))
        {
            var r = await client.ListFiles(cancel);
            if (r.StatusCode != HttpStatusCode.OK) { return Enumerable.Empty<string>(); }
            return r.Files.Select(x => x.ObjectName);
        }

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public override async Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0)
        {
            try
            {
                StatusCodeThrower(await client.PutFile(file, name, autoDisposeStream: disposeStream, progress: progress, cancelToken: cancel, expectedContentLength: expectedStreamLength));
            }
            finally
            {
                if (disposeStream) { file.Dispose(); }
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
