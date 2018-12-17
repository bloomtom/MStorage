using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage.FilesystemStorage
{
    public class FilesystemStorage : IStorage
    {
        private readonly ILogger log;

        /// <summary>
        /// The root directory on the filesystem to store data in.
        /// </summary>
        public string RootDirectory { get; private set; }

        public FilesystemStorage(string rootDirectory, ILogger log)
        {
            RootDirectory = rootDirectory;
            this.log = log ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<FilesystemStorage>();

            Directory.CreateDirectory(RootDirectory);
            this.log.LogInformation($"Filesystem storage backend initialized to root directory {RootDirectory}.");
        }

        public Task DeleteAllAsync()
        {
            try
            {
                Directory.Delete(RootDirectory);
            }
            finally
            {
                if (!Directory.Exists(RootDirectory))
                {
                    Directory.CreateDirectory(RootDirectory);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name)
        {
            File.Delete(GetFullPath(name));
            return Task.CompletedTask;
        }

        public Task<Stream> DownloadAsync(string name)
        {
            return Task.FromResult<Stream>(File.OpenRead(GetFullPath(name)));
        }

        public Task<IEnumerable<string>> ListAsync()
        {
            return Task.FromResult(ListFiles());
        }

        public async Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource)
        {
            if (Equals(destination))
            {
                // Target is same as source. Nothing to do.
                log.LogInformation($"Filesystem transfer was attempted but the source and destination are the same.");
                return Enumerable.Empty<StatusedValue<string>>();
            }

            var result = new List<StatusedValue<string>>();
            foreach (var filename in ListFiles())
            {
                try
                {
                    using (var fileStream = File.OpenRead(GetFullPath(filename)))
                    {
                        await destination.UploadAsync(filename, fileStream);
                        result.Add(new StatusedValue<string>(true, filename));
                    }
                }
                catch (Exception)
                {
                    result.Add(new StatusedValue<string>(false, filename));
                }
            }
            return result;
        }

        public async Task UploadAsync(string name, Stream file)
        {
            using (var fileStream = File.Open(GetFullPath(name), FileMode.Create))
            {
                await file.CopyToAsync(fileStream).ContinueWith((x) => { file.Close(); });
            }
        }

        public Task UploadAsync(string name, string path, bool deleteSource)
        {
            string destFileName = GetFullPath(name);
            if (destFileName != path)
            {
                if (File.Exists(destFileName)) { File.Delete(destFileName); }
                File.Copy(path, destFileName);
                if (deleteSource) { File.Delete(path); }
            }

            return Task.CompletedTask;
        }

        private string GetFullPath(string filename)
        {
            return Path.Combine(RootDirectory, filename);
        }

        private IEnumerable<string> ListFiles()
        {
            var o = new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true };
            var foundFiles = new FileSystemEnumerable<string>(RootDirectory, (ref FileSystemEntry entry) => entry.FileName.ToString(), o)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => { return !entry.IsDirectory; }
            };
            return foundFiles;
        }

        public override bool Equals(object obj)
        {
            if (obj is FilesystemStorage x)
            {
                if (x.RootDirectory == RootDirectory)
                {
                    return true;
                }
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return RootDirectory.GetHashCode();
        }

        public override string ToString()
        {
            return $"Filesystem {RootDirectory}";
        }
    }
}
