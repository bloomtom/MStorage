using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// A null storage backend. Simulates all aspects of a normal storage backend without actually storing object data.
    /// </summary>
    public class NullStorage : IStorage
    {
        private readonly Dictionary<string, long> stored = new Dictionary<string, long>();

        public NullStorage()
        {
        }

        /// <summary>
        /// Clears the virtual object store.
        /// </summary>
        public Task DeleteAllAsync()
        {
            stored.Clear();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Task DeleteAsync(string name)
        {
            if (stored.ContainsKey(name))
            {
                stored.Remove(name);
            }
            else
            {
                throw new FileNotFoundException();
            }
            return Task.CompletedTask;
        }

        public Task<Stream> DownloadAsync(string name)
        {
            if (stored.TryGetValue(name, out long length))
            {
                return Task.FromResult((Stream)new MemoryStream(new byte[length]));
            }
            else
            {
                throw new FileNotFoundException();
            }
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
                try
                {
                    using (var s = new MemoryStream(new byte[item.Value]))
                    {
                        s.Position = 0;
                        destination.UploadAsync(item.Key, s).Wait();
                    }

                    result.Add(new StatusedValue<string>(item.Key, true, null));
                }
                catch (Exception ex)
                {
                    result.Add(new StatusedValue<string>(item.Key, false, ex));
                }
            }

            if (deleteSource)
            {
                foreach (var item in result)
                {
                    stored.Remove(item.Value);
                }
            }

            return Task.FromResult<IEnumerable<StatusedValue<string>>>(result);
        }

        public Task UploadAsync(string name, Stream file, bool autoDispose = false)
        {
            try
            {
                long length = 0;
                while (file.ReadByte() != -1)
                {
                    length++;
                }
                stored[name] = length;
                return Task.CompletedTask;
            }
            finally
            {
                if (autoDispose) { file.Dispose(); }
            }
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
