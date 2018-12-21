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
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesnt.
        /// </summary>
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

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// Since uploads are not actually stored, the returned stream will be all zeros, but the length will be equal to what was "uploaded".
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <returns>A stream with the correct length containing all zeros.</returns>
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

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public Task<IEnumerable<string>> ListAsync()
        {
            return Task.FromResult<IEnumerable<string>>(stored.Keys);
        }

        /// <summary>
        /// Transfers every object from this instance to another IStorage instance.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <returns>A collection of statuses indicating the success or failure state for each transfered object.</returns>
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

        /// <summary>
        /// Consumes the entire given stream, and creates a virtual storage entry for the uploaded object containing the stream length for later hydration.
        /// The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
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

        /// <summary>
        /// Generates an object entry for the file at the given path. The original file is optionally deleted after being "stored".
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the "upload" is complete.</param>
        public Task UploadAsync(string name, string path, bool deleteSource)
        {
            stored[name] = new FileInfo(path).Length;
            if (deleteSource) { File.Delete(path); }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns "NullStorage"
        /// </summary>
        public override string ToString()
        {
            return "NullStorage";
        }
    }
}
