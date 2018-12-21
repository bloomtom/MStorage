using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage.FilesystemStorage
{
    /// <summary>
    /// A filesystem based backend. Objects are stored as normal files on disk.
    /// </summary>
    public class FilesystemStorage : IStorage
    {
        /// <summary>
        /// The root directory on the filesystem to store data in.
        /// </summary>
        public string RootDirectory { get; private set; }

        /// <summary>
        /// Initialize the backend to the given root directory on disk
        /// </summary>
        public FilesystemStorage(string rootDirectory)
        {
            RootDirectory = rootDirectory;
            Directory.CreateDirectory(RootDirectory);
        }

        /// <summary>
        /// Deletes the entire root directory including all stored files and subdirectories.
        /// This is a dangerous operation if the root directory is shared!
        /// </summary>
        public Task DeleteAllAsync()
        {
            try
            {
                Directory.Delete(RootDirectory, true);
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

        /// <summary>
        /// Deletes a single file from disk by the given name. Throws FileNotFound exception if it doesnt exist.
        /// </summary>
        /// <param name="name">The file to delete.</param>
        public Task DeleteAsync(string name)
        {
            File.Delete(GetFullPath(name));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Calls OpenRead on the given object name.
        /// </summary>
        /// <param name="name">The object name to open.</param>
        /// <returns></returns>
        public Task<Stream> DownloadAsync(string name)
        {
            return Task.FromResult<Stream>(File.OpenRead(GetFullPath(name)));
        }

        /// <summary>
        /// Retrieves a list of all files stored in the root directory.
        /// Subdirectories and inaccessible files are not returned.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        public Task<IEnumerable<string>> ListAsync()
        {
            return Task.FromResult(ListFiles());
        }

        /// <summary>
        /// Transfer all files (by ListAsync) to the given destination store.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <returns>A collection of statuses indicating the success or failure state for each transfered object.</returns>
        public async Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource)
        {
            if (Equals(destination))
            {
                // Target is same as source. Nothing to do.
                return Enumerable.Empty<StatusedValue<string>>();
            }

            var result = new List<StatusedValue<string>>();
            foreach (var filename in ListFiles())
            {
                try
                {
                    string fullPath = GetFullPath(filename);
                    using (var fileStream = File.OpenRead(fullPath))
                    {
                        await destination.UploadAsync(filename, fileStream);
                    }
                    if (deleteSource)
                    {
                        File.Delete(fullPath);
                    }
                    result.Add(new StatusedValue<string>(filename, true, null));
                }
                catch (Exception ex)
                {
                    result.Add(new StatusedValue<string>(filename, false, ex));
                }
            }
            return result;
        }

        /// <summary>
        /// Saves the given stream to disk. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The filename to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        public async Task UploadAsync(string name, Stream file, bool disposeStream)
        {
            try
            {
                using (var fileStream = File.Open(GetFullPath(name), FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            finally
            {
                if (disposeStream) { file.Dispose(); }
            }
        }

        /// <summary>
        /// Copies the file at the given path into the store. The original file is optionally deleted after being copied.
        /// </summary>
        /// <param name="name">The filename to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the upload is complete.</param>
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

        /// <summary>
        /// If the compare object is of type FilesystemStorage, returns this.RootDirectory == obj.RootDirectory
        /// Else returns base.Equals(obj)
        /// </summary>
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

        /// <summary>
        /// Returns RootDirectory.GetHashCode()
        /// </summary>
        public override int GetHashCode()
        {
            return RootDirectory.GetHashCode();
        }

        /// <summary>
        /// Returns "Filesystem {RootDirectory}"
        /// </summary>
        public override string ToString()
        {
            return $"Filesystem {RootDirectory}";
        }
    }
}
