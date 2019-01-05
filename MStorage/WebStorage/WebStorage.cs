using HttpProgress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MStorage.WebStorage
{
    /// <summary>
    /// An abstract class for web API based storage backends.
    /// </summary>
    public abstract class WebStorage : IStorage
    {
        /// <summary>
        /// The user or account ID to use for connections.
        /// </summary>
        protected readonly string user;
        /// <summary>
        /// An api key or authentication code to use for connections.
        /// </summary>
        protected readonly string apiKey;
        /// <summary>
        /// The bucket, container, etc. to store items in.
        /// </summary>
        protected readonly string bucket;

        /// <summary>
        /// Initializes the instance with user, apiKey and bucket.
        /// </summary>
        /// <param name="user">The user or account ID to use for connections.</param>
        /// <param name="apiKey">An api key or authentication code to use for connections.</param>
        /// <param name="bucket">The bucket, container, etc. to store items in.</param>
        protected WebStorage(string user, string apiKey, string bucket)
        {
            this.user = user;
            this.apiKey = apiKey;
            this.bucket = bucket;
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names.</returns>
        public abstract Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        public abstract Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public abstract Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public abstract Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0);

        /// <summary>
        /// Uploads the file at the given path. The original file is optionally deleted after being sent.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the upload is complete.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public virtual async Task UploadAsync(string name, string path, bool deleteSource, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return; }

            if (!File.Exists(path)) { throw new ArgumentOutOfRangeException("path", $"No file exists at the given path {path}"); }
            using (var s = File.OpenRead(path))
            {
                await UploadAsync(name, s, false, progress, cancel);
            }
            if (deleteSource)
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesn't.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public abstract Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Returns a string describing the type of backend used.
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();

        /// <summary>
        /// Deletes all stored objects.
        /// </summary>
        /// <param name="cancel">Allows the operation to be canceled.</param>
        /// <param name="progress">Invoked on every delete with the current count of processed items.</param>
        /// <returns></returns>
        public virtual async Task DeleteAllAsync(IProgress<long> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            var items = await ListAsync(cancel);
            long count = 0;
            foreach (var item in items)
            {
                if (cancel.IsCancellationRequested) { return; }

                await DeleteAsync(item, cancel);

                count++;
                if (progress != null) { progress.Report(count); }
            }
        }

        /// <summary>
        /// Transfers every object from this instance to another IStorage instance.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <param name="success">Fires after each successful transfer. Provides the name of the object transferred.</param>
        /// <param name="error">Fires when a transfer or delete error is seen.</param>
        /// <param name="cancel">Allows the transfer operation to be canceled.</param>
        public virtual async Task TransferAsync(IStorage destination, bool deleteSource, IProgress<string> success = null, IProgress<ExceptionWithValue<string>> error = null, CancellationToken cancel = default(CancellationToken))
        {
            if (Equals(destination))
            {
                // Target is same as source. Nothing to do.
                return;
            }

            var items = await ListAsync();
            foreach (var item in items)
            {
                if (cancel.IsCancellationRequested) { return; }

                try
                {
                    using (var s = await DownloadAsync(item))
                    {
                        await destination.UploadAsync(item, s, false, null, cancel);
                    }
                }
                catch (Exception ex)
                {
                    if (error != null)
                    {
                        error.Report(new ExceptionWithValue<string>(item, ex));
                    }
                    continue;
                }

                // Allow cancel before delete is fired.
                if (cancel.IsCancellationRequested) { return; }

                // Delete the old file if it was transferred, and deletion was requested.
                if (deleteSource)
                {
                    try
                    {
                        await DeleteAsync(item, cancel);
                    }
                    catch (Exception ex)
                    {
                        if (error != null)
                        {
                            error.Report(new ExceptionWithValue<string>(item, ex));
                        }
                        continue;
                    }
                }

                if (success != null) { success.Report(item); }
            }
        }

        /// <summary>
        /// If obj is type WebStorage and (x.user == user AND x.bucket == bucket), returns true.
        /// Else returns base.Equals(obj)
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is WebStorage x)
            {
                if (x.user == user && x.bucket == bucket)
                {
                    return true;
                }
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// Returns HashCode.Combine(user, bucket)
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(user, bucket);
        }

        /// <summary>
        /// Takes an HttpStatusCode and throws an exception if the status is not a success signal. The type of exception thrown is dependent on the code given.
        /// </summary>
        /// <param name="code"></param>
        protected void StatusCodeThrower(HttpStatusCode code)
        {
            switch (code)
            {
                case HttpStatusCode.Accepted:
                case HttpStatusCode.Created:
                case HttpStatusCode.NoContent:
                case HttpStatusCode.NotModified:
                case HttpStatusCode.OK:
                case HttpStatusCode.PartialContent:
                    return;
                case HttpStatusCode.BadGateway:
                    throw new InvalidOperationException("502 Bad Gateway");
                case HttpStatusCode.BadRequest:
                    throw new InvalidOperationException("400 Bad Request");
                case HttpStatusCode.Forbidden:
                    throw new UnauthorizedAccessException("403 Forbidden");
                case HttpStatusCode.GatewayTimeout:
                    throw new TimeoutException("504 Gateway Timeout");
                case HttpStatusCode.Gone:
                    throw new FileNotFoundException("410 Gone");
                case HttpStatusCode.InsufficientStorage:
                    throw new IOException("507 Insufficient Storage");
                case HttpStatusCode.InternalServerError:
                    throw new Exception("500 Internal Server Error");
                case HttpStatusCode.NetworkAuthenticationRequired:
                    throw new UnauthorizedAccessException("511 Authentication Required");
                case HttpStatusCode.NotFound:
                    throw new FileNotFoundException("404 Resource Not Found");
                case HttpStatusCode.NotImplemented:
                    throw new InvalidOperationException("501 Not Implemented");
                case HttpStatusCode.ProxyAuthenticationRequired:
                    throw new UnauthorizedAccessException("407 Proxy Authentication Required");
                case HttpStatusCode.RequestedRangeNotSatisfiable:
                    throw new ArgumentOutOfRangeException("416 Requested Range Not Satisfiable");
                case HttpStatusCode.RequestEntityTooLarge:
                    throw new ArgumentOutOfRangeException("413 Requested Entity Too Large");
                case HttpStatusCode.RequestHeaderFieldsTooLarge:
                    throw new ArgumentOutOfRangeException("431 Requested Header Fields Too Large");
                case HttpStatusCode.RequestTimeout:
                    throw new TemporaryFailureException("408 Request Timeout");
                case HttpStatusCode.RequestUriTooLong:
                    throw new ArgumentOutOfRangeException("414 Request Uri Too Long");
                case HttpStatusCode.ServiceUnavailable:
                    throw new TemporaryFailureException("503 Service Unavailable");
                case HttpStatusCode.TooManyRequests:
                    throw new TemporaryFailureException("429 Too Many Requests");
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException("401 Unauthorized");
                case HttpStatusCode.UnavailableForLegalReasons:
                    throw new Exception("451 DMCA Fulfilled on Linux ISO");
                case HttpStatusCode.UnsupportedMediaType:
                    throw new ArgumentException("415 Unsupported Media Type");
                default:
                    throw new Exception($"HTTP Error {code.ToString()}");
            }
        }
    }
}
