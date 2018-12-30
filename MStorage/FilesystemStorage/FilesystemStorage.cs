using HttpProgress;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
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
        /// <param name="progress">This event is not supported on this backedn and so will never fire.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public Task DeleteAllAsync(IProgress<long> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();

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
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

            File.Delete(GetFullPath(name));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Calls OpenRead on the given object name, returning a FileStream. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object. The underlying type is FileStream.</returns>
        public Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { throw new TaskCanceledException("Filesystem download canceled", null, cancel); }
            return Task.FromResult<Stream>(File.OpenRead(GetFullPath(name)));
        }

        /// <summary>
        /// Calls OpenRead on the given object name, returning a FileStream. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public async Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            using (var s = File.OpenRead(GetFullPath(name)))
            {
                await s.CopyToAsync(output, progressReport: progress, cancelToken: cancel);
            } 
        }

        /// <summary>
        /// Retrieves a list of all files stored in the root directory.
        /// Subdirectories and inaccessible files are not returned.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names.</returns>
        public Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();

            return Task.FromResult(ListFiles());
        }

        /// <summary>
        /// Transfer all files (by ListAsync) to the given destination store.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <returns>A collection of statuses indicating the success or failure state for each transfered object.</returns>
        public async Task TransferAsync(IStorage destination, bool deleteSource, IProgress<string> success = null, IProgress<ExceptionWithValue<string>> error = null, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();

            if (Equals(destination))
            {
                // Target is same as source. Nothing to do.
                return;
            }

            var result = new List<ExceptionWithValue<string>>();
            foreach (var filename in ListFiles())
            {
                if (cancel.IsCancellationRequested) { return; }

                try
                {
                    string fullPath = GetFullPath(filename);
                    using (var fileStream = File.OpenRead(fullPath))
                    {
                        await destination.UploadAsync(filename, fileStream, cancel: cancel);
                    }
                    if (deleteSource)
                    {
                        File.Delete(fullPath);
                    }
                    if (success != null) { success.Report(filename); }
                }
                catch (Exception ex)
                {
                    if (error != null) { error.Report(new ExceptionWithValue<string>(filename, ex)); }
                }
            }
        }

        /// <summary>
        /// Saves the given stream to disk. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The filename to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires periodically with transfer progress.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public async Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0)
        {
            long streamLength = file.CanSeek && expectedStreamLength == 0 ? file.Length : expectedStreamLength;
            try
            {
                using (var fileStream = File.Open(GetFullPath(name), FileMode.Create))
                {
                    await file.CopyToAsync(fileStream, expectedTotalBytes: streamLength, progressReport: progress, cancelToken: cancel);
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
        /// <param name="progress">Fires periodically with transfer progress.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public async Task UploadAsync(string name, string path, bool deleteSource, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();

            string destFileName = GetFullPath(name);
            var info = new FileInfo(path);
            if (destFileName != path)
            {
                using (var s = File.OpenRead(path))
                {
                    await UploadAsync(name, s, deleteSource, progress, cancel, info.Length);
                }
                if (deleteSource) { File.Delete(path); }
            }
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
