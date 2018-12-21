using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MStorage.WebStorage
{
    public abstract class WebStorage : IStorage
    {
        protected readonly string user;
        protected readonly string apiKey;
        protected readonly string bucket;

        public WebStorage(string user, string apiKey, string bucket)
        {
            this.user = user;
            this.apiKey = apiKey;
            this.bucket = bucket;
        }

        public abstract Task<IEnumerable<string>> ListAsync();

        public abstract Task<Stream> DownloadAsync(string name);

        public abstract Task UploadAsync(string name, Stream file, bool disposeStream = false);

        public virtual async Task UploadAsync(string name, string path, bool deleteSource)
        {
            if (!File.Exists(path)) { throw new ArgumentOutOfRangeException("path", $"No file exists at the given path {path}"); }
            using (var s = File.OpenRead(path))
            {
                await UploadAsync(name, s, false);
            }
            if (deleteSource)
            {
                File.Delete(path);
            }
        }

        public abstract Task DeleteAsync(string name);

        public abstract override string ToString();

        public virtual async Task DeleteAllAsync()
        {
            var items = await ListAsync();
            foreach (var item in items)
            {
                await DeleteAsync(item);
            }
        }

        public virtual async Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource)
        {
            if (Equals(destination))
            {
                // Target is same as source. Nothing to do.
                return Enumerable.Empty<StatusedValue<string>>();
            }

            var returnList = new List<StatusedValue<string>>();
            var items = await ListAsync();
            foreach (var item in items)
            {
                try
                {
                    using (var s = await DownloadAsync(item))
                    {
                        await destination.UploadAsync(item, s, false);
                    }
                }
                catch (Exception ex)
                {
                    returnList.Add(new StatusedValue<string>(item, false, ex));
                    continue;
                }

                // Delete the old file if it was transfered, and deletion was requested.
                if (deleteSource)
                {
                    try
                    {
                        await DeleteAsync(item);
                    }
                    catch (Exception ex)
                    {
                        returnList.Add(new StatusedValue<string>(item, false, ex));
                        continue;
                    }
                }

                returnList.Add(new StatusedValue<string>(item, true, null));
            }

            return returnList;
        }

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

        public override int GetHashCode()
        {
            return HashCode.Combine(user, bucket);
        }

        public void StatusCodeThrower(HttpStatusCode code)
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
                    throw new TemporaryFailureException("503 Service Unavailable");
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
