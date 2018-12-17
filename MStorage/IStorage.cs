using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// An interface for performing operations on an object based storage backend
    /// </summary>
    public interface IStorage
    {
        Task DeleteAsync(string name);
        Task DeleteAllAsync();
        Task<IEnumerable<string>> ListAsync();
        Task<IEnumerable<StatusedValue<string>>> TransferAsync(IStorage destination, bool deleteSource);
        Task UploadAsync(string name, Stream file);
        Task<Stream> DownloadAsync(string name);
        Task UploadAsync(string name, string path, bool deleteSource);
    }
}