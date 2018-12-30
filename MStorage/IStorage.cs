using HttpProgress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// An interface for performing operations on an object based storage backend.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesn't.
        /// </summary>
        /// <param name="name">The object to delete.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        Task DeleteAsync(string name, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Deletes all stored objects.
        /// </summary>
        /// <param name="progress">Fires on every delete with the current count of deleted items if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the delete operation.</param>
        Task DeleteAllAsync(IProgress<long> progress = null, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <param name="cancel">Allows cancellation of the list operation.</param>
        /// <returns>A collection of object names.</returns>
        Task<IEnumerable<string>> ListAsync(CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Transfers every object from this instance to another IStorage instance.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <param name="success">Fires after each successful transfer. Provides the name of the object transferred.</param>
        /// <param name="error">Fires when a transfer or delete error is seen.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        Task TransferAsync(IStorage destination, bool deleteSource, IProgress<string> success = null, IProgress<ExceptionWithValue<string>> error = null, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <param name="expectedStreamLength">Allows overriding the stream's expected length for progress reporting as some stream types do not support Length.</param>
        Task UploadAsync(string name, Stream file, bool disposeStream = false, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken), long expectedStreamLength = 0);

        /// <summary>
        /// Uploads the file at the given path. The original file is optionally deleted after being sent.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the upload is complete.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        Task UploadAsync(string name, string path, bool deleteSource, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        /// <returns>A stream containing the requested object.</returns>
        Task<Stream> DownloadAsync(string name, CancellationToken cancel = default(CancellationToken));

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <param name="output">The output stream data will be copied to.</param>
        /// <param name="progress">Fires periodically with transfer progress if the backend supports it.</param>
        /// <param name="cancel">Allows cancellation of the transfer.</param>
        Task DownloadAsync(string name, Stream output, IProgress<ICopyProgress> progress = null, CancellationToken cancel = default(CancellationToken));
    }
}