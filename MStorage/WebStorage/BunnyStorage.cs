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

        public BunnyStorage(string apiKey, string storageZone) : base(apiKey, apiKey, storageZone)
        {
            client = new BunClient(apiKey, storageZone);
        }

        public override async Task DeleteAsync(string name)
        {
            StatusCodeThrower(await client.DeleteFile(name));
        }

        public override async Task<Stream> DownloadAsync(string name)
        {
            var r = await client.GetFile(name);
            StatusCodeThrower(r.StatusCode);
            return r.Stream;
        }

        public override async Task<IEnumerable<string>> ListAsync()
        {
            var r = await client.ListFiles();
            if (r.StatusCode != HttpStatusCode.OK) { return Enumerable.Empty<string>(); }
            return r.Files.Select(x => x.ObjectName);
        }

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

        public override string ToString()
        {
            return $"BunnyCDN {bucket}";
        }
    }
}
