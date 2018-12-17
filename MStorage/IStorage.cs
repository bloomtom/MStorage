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
        /// Deletes the given item if it exists.
        /// </summary>
        Task DeleteAsync(string name);
        /// <summary>
        /// Deletes all items stored in the storage container.
        /// </summary>
        Task DeleteAllAsync();
        Task<IEnumerable<string>> ListAsync();
        Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource);
        /// <summary>
        /// Uploads the entire given stream and closes it.
        /// </summary>
        Task UploadAsync(string name, Stream file);
        Task<Stream> DownloadAsync(string name);
        Task UploadAsync(string name, string path, bool deleteSource);
    }
}