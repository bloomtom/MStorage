﻿using HttpProgress;
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
        protected readonly string user;
        protected readonly string apiKey;
        protected readonly string bucket;

        protected WebStorage(string user, string apiKey, string bucket)
        {
            this.user = user;
            this.apiKey = apiKey;
            this.bucket = bucket;
        }

        public abstract Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken));

        public abstract Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken));

        public abstract Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken));

        public abstract Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0);

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

        public abstract Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken));

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

                // Delete the old file if it was transfered, and deletion was requested.
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
        /// If obj is type WebStorage and (x.user == user && x.bucket == bucket), returns true.
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
