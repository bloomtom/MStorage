using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage
{
    public class NullStorage : IStorage
    {
        private readonly ILogger log;
        private readonly Dictionary<string, long> stored = new Dictionary<string, long>();

        public NullStorage(ILogger log)
        {
            this.log = log;
            log.LogInformation("Null storage backend initialized. This backend is normally used for testing only.");
        }

        public Task DeleteAllAsync()
        {
            stored.Clear();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name)
        {
            stored.Remove(name);
            return Task.CompletedTask;
        }

        public Task<Stream> DownloadAsync(string name)
        {
            if (stored.TryGetValue(name, out long length))
            {
                new MemoryStream(new byte[length]);
            }
            return null;
        }

        public Task<IEnumerable<string>> ListAsync()
        {
            return Task.FromResult<IEnumerable<string>>(stored.Keys);
        }

        public Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource)
        {
            var result = new List<StatusedValue<string>>();
            foreach (var item in stored)
            {
                result.Add(new StatusedValue<string>(true, item.Key));
            }
            if (deleteSource)
            {
                stored.Clear();
            }
            return Task.FromResult<IEnumerable<StatusedValue<string>>>(result);
        }

        public Task UploadAsync(string name, Stream file)
        {
            long length = 0;
            while(file.ReadByte() != -1)
            {
                length++;
            }
            stored[name] = length;
            return Task.CompletedTask;
        }

        public Task UploadAsync(string name, string path, bool deleteSource)
        {
            stored[name] = new FileInfo(path).Length;
            if (deleteSource) { File.Delete(path); }
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return $"NullStorage";
        }
    }
}
