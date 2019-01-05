using HttpProgress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// A null storage backend. Simulates all aspects of a normal storage backend without actually storing object data.
    /// </summary>
    public class NullStorage : IStorage
    {
        private readonly Dictionary<string, long> stored = new Dictionary<string, long>();

        /// <summary>
        /// Initialize the null storage.
        /// </summary>
        public NullStorage()
        {
        }

        /// <summary>
        /// Clears the virtual object store.
        /// </summary>
        /// <param name="progress">Fires once with the count of deleted items.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public Task DeleteAllAsync(IProgress<long> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

            long c = stored.Count;

            stored.Clear();

            if (progress != null) { progress.Report(c); }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesnt.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        public Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

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
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        public Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled<Stream>(cancel); }

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
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// Since uploads are not actually stored, the returned stream will be all zeros, but the length will be equal to what was "uploaded".
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public async Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            cancel.ThrowIfCancellationRequested();

            if (stored.TryGetValue(name, out long length))
            {
                using (Stream s = new MemoryStream(new byte[length]))
                {
                    await s.CopyToAsync(output, expectedTotalBytes: length, progressReport: progress, cancelToken: cancel);
                }
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names, or null if canceled.</returns>
        public Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled<IEnumerable<string>>(cancel); }

            return Task.FromResult<IEnumerable<string>>(stored.Keys);
        }

        /// <summary>
        /// Transfers every object from this instance to another IStorage instance.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <param name="success">Fires after each successful transfer. Provides the name of the object transferred.</param>
        /// <param name="error">Fires when a transfer or delete error is seen.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public Task TransferAsync(IStorage destination, bool deleteSource, IProgress<string> success = null, IProgress<ExceptionWithValue<string>> error = null, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

            var transferred = new List<string>();
            foreach (var item in stored)
            {
                if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

                try
                {
                    using (var s = new MemoryStream(new byte[item.Value]))
                    {
                        s.Position = 0;
                        destination.UploadAsync(item.Key, s).Wait();
                    }

                    transferred.Add(item.Key);
                    if (success != null) { success.Report(item.Key); }
                }
                catch (Exception ex)
                {
                    if (error != null) { error.Report(new ExceptionWithValue<string>(item.Key, ex)); }
                }
            }

            if (deleteSource)
            {
                foreach (var item in transferred)
                {
                    stored.Remove(item);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Consumes the entire given stream, and creates a virtual storage entry for the uploaded object containing the stream length for later hydration.
        /// The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires once with transfer statistics.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        public Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0)
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

            var totalTime = new System.Diagnostics.Stopwatch();
            var instantTime = new System.Diagnostics.Stopwatch();
            totalTime.Start();
            instantTime.Start();

            expectedStreamLength = Statics.ComputeStreamLength(file, expectedStreamLength);

            try
            {
                const long bufferSize = 32768;
                long length = 0;
                long lastReported = 0;
                while (file.ReadByte() != -1)
                {
                    length++;

                    if (length % bufferSize == 0)
                    {
                        lastReported = length;
                        progress.Report(new CopyProgress(totalTime.Elapsed, Statics.ComputeInstantRate(instantTime.ElapsedTicks, bufferSize), length, expectedStreamLength));
                        instantTime.Restart();
                    }
                }

                stored[name] = length;

                if (progress != null && lastReported != length) { ReportProgress(progress, totalTime, length); }
                return Task.CompletedTask;
            }
            finally
            {
                if (disposeStream) { file.Dispose(); }
            }
        }

        /// <summary>
        /// Generates an object entry for the file at the given path. The original file is optionally deleted after being "stored".
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the "upload" is complete.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        public Task UploadAsync(string name, string path, bool deleteSource, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken))
        {
            if (cancel.IsCancellationRequested) { return Task.FromCanceled(cancel); }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            long length = new FileInfo(path).Length;
            stored[name] = length;
            if (deleteSource) { File.Delete(path); }

            if (progress != null) { ReportProgress(progress, sw, length); }
            return Task.CompletedTask;
        }

        private static void ReportProgress(IProgress<ICopyProgress> progress, System.Diagnostics.Stopwatch sw, long length)
        {
            progress.Report(new CopyProgress(sw.Elapsed, (int)((double)length / sw.ElapsedTicks * TimeSpan.TicksPerSecond), length, length));
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
