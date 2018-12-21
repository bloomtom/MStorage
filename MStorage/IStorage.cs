using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// An interface for performing operations on an object based storage backend.
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// Deletes the given object if it exists. Throws FileNotFound exception if it doesnt.
        /// </summary>
        Task DeleteAsync(string name);

        /// <summary>
        /// Deletes all stored objects.
        /// </summary>
        Task DeleteAllAsync();

        /// <summary>
        /// Retrieve a collection of all object names stored.
        /// </summary>
        /// <returns>A collection of object names.</returns>
        Task<IEnumerable<string>> ListAsync();

        /// <summary>
        /// Transfers every object from this instance to another IStorage instance.
        /// </summary>
        /// <param name="destination">The instance to transfer to.</param>
        /// <param name="deleteSource">Delete each object in this store after it has successfully been transferred.</param>
        /// <returns>A collection of statuses indicating the success or failure state for each transfered object.</returns>
        Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource);

        /// <summary>
        /// Uploads the entire given stream. The stream is optionally closed after being consumed.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="file">The stream to upload.</param>
        /// <param name="disposeStream">If true, the file stream will be closed automatically after being consumed.</param>
        Task UploadAsync(string name, Stream file, bool disposeStream = false);

        /// <summary>
        /// Uploads the file at the given path. The original file is optionally deleted after being sent.
        /// </summary>
        /// <param name="name">The name to give this object.</param>
        /// <param name="path">A path to the file to upload.</param>
        /// <param name="deleteSource">If true, the file on disk will be deleted after the upload is complete.</param>
        Task UploadAsync(string name, string path, bool deleteSource);

        /// <summary>
        /// Retrieve an object from the store. Throws FileNotFound if the object does not exist.
        /// </summary>
        /// <param name="name">The name of the object to retrieve.</param>
        /// <returns>A stream containing the requested object.</returns>
        Task<Stream> DownloadAsync(string name);
    }
}