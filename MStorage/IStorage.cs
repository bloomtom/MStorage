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
        /// Based on the implementation, an exception may be thrown if the file does not exist or you are unauthorized to delete the file.
        /// </summary>
        Task DeleteAsync(string name);
        /// <summary>
        /// Deletes all items stored in the storage container.
        /// Based on the implementation, an exception may be thrown if the file does not exist or you are unauthorized to delete the file.
        /// </summary>
        Task DeleteAllAsync();
        Task<IEnumerable<string>> ListAsync();
        Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource);
        Task UploadAsync(string name, Stream file);
        Task<Stream> DownloadAsync(string name);
        Task UploadAsync(string name, string path, bool deleteSource);
    }
}