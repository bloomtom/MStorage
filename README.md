# MStorage

>Bridges multiple storage backends into a simple object store layer.

Storing files on the local filesystem is easy. Storing them on a cloud provider isn't much more difficult, but when you're using multiple providers, moving between providers, or allow configuration for one or more of many providers, things can get messy fast. This library provides a clean interface to interact with several storage backends so you can just swap them out.

## Contents
  - [Nuget Packages](#nuget-packages)
  - [Available Backends](#available-backends)
  - [Usage](#usage)
  - [IStorage API](#istorage-api)
  - [Exceptions](#exceptions)
  - [Progress Reporting](#progress)
  - [Transferring Between Backends](#transferring-between-backends)
  - [Testing](#testing)


<a name="nuget-packages"></a>
## Nuget Packages

Package Name | Target Framework | Version
---|---|---
[MStorage](https://www.nuget.org/packages/bloomtom.MStorage) | .NET Core 2.1 | ![NuGet](https://img.shields.io/nuget/v/bloomtom.MStorage.svg)

This is currently a Core 2.1 package, but will be ported to Standard 2.1 when it becomes available.

<a name="available-backends"></a>
## Available Backends

 - `NullStorage`
   - A test backend which only pretends to store files. Reading back stored objects will give you the right length, but all zeros.
 - `FilesystemStorage`
   - A simple backend which stores to a directory.
 - `AzureStorage`
   - A web API backend which stores in [Microsoft Azure Blob Service](https://azure.microsoft.com/en-us/services/storage/blobs/)
 - `BunnyStorage`
   - A web API backend which stores in [BunnyCDN Cloud Storage](https://bunnycdn.com/solutions/cdn-cloud-storage)
 - `S3Storage`
   - A web API backend which stores in [Amazon AWS S3](https://aws.amazon.com/s3/) or any [S3 compatible REST endpoint](https://en.wikipedia.org/wiki/Amazon_S3#S3_API_and_competing_services).

<a name="usage"></a>
## Usage

Declare an IStorage variable.
```csharp
IStorage myStore;
```
Assign a backend to it.
```csharp
myStore = new FilesystemStorage("/path/to/storage/directory");
```
Store and retrieve with reckless abandon!
```csharp
// Upload someStream to the storage backend with the object name "My New File"
await myStore.UploadAsync("My New File", someStream);

// Return a stream containing the object "My New File" 
return await myStore.DownloadAsync("My New File");
```

<a name="istorage-api"></a>
## IStorage API

You can do more than the above of course. The following is the generic interface.
```csharp
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
```

<a name="exceptions"></a>
## Exceptions

Given the nature of networks, disks, permissions, the weather, and acts of god: This API may not succeed on all operations. In the case of failure, most methods will throw an exception. The following is a list of the most common exceptions.

 - `FileNotFoundException`
   - The object you requested could not be found.
 - `UnauthorizedAccessException`
   - Your credentials are invalid, or you don't have permission to access this object.
 - `TemporaryFailureException`
   - The backend service couldn't complete the request, but trying again later might work.
 - `ArgumentOutOfRangeException`
   - The object you sent was too large, or was otherwise invalid.
 - `InvalidOperationException`
   - Either there's a bug in this library, or the backend service is down or misbehaving.
 - `TimeoutException`
   - The operation timed out.
 - `TaskCanceledException`
   - An operation was cancelled through a cancellation token.

The class `WebStorage.cs` contains more oddball exceptions in the method `StatusCodeThrower`, but you probably don't need to handle them. The exception string will always contain the http status code for web backends, and the filesystem backend will only throw standard file IO exceptions. Depending on your use case you might just log the rare outliers, or fail fast on them.

<a name="progress"></a>
## Progress Reporting

The methods `DeleteAllAsync`, `TransferAsync`, `DownloadAsync` and `UploadAsync` all support progress reporting via `IProgress<T>`. Upload and download operations report progress using `ICopyProgress` from the [`HttpProgress`](https://github.com/bloomtom/HttpProgress) library. It's recommended that you read the notes on `ICopyProgress` and `IProgress<T>` available in that library before using progress reporting.

<a name="transferring-between-backends"></a>
## Transferring Between Backends

You may come across a situation where you need to move something or everything from one backend to another. Since backends are pluggable, this is a very clean operation! `IStorage` even gives you a helper method to perform a full migration: `TransferAsync`. This method will copy or move (depending on the `deleteSource` flag) all objects from one backend to another. A collection is returned with an entry for each object ported, indicating success or failure. Failures will typically contain an exception detailing what went wrong.

<a name="testing"></a>
## Testing

A test project is included in this repository, and testing the null / filesystem backends works out of the box. Testing the web backends requires a little more work since the services they connect to are not mocked. The following credential container class is expected under `/MStorageTests/ConnectionInfo.cs`. The `.gitignore` file excludes this from commits to prevent credential leaks via git.
```csharp
namespace MStorageTests
{
    internal static class TestSettings
    {
        private const long MB = 1024 * 1024;

        public const long progressFileSize = MB;
        public const long bigFileSize = MB * 1024 * 4;
    }
	
    internal static class BunConnectionInfo
    {
        public const string zone = "";
        public const string apiKey = "";
    }

    internal static class AwsConnectionInfo
    {
        public const string accessKey = "";
        public const string apiKey = "";
        public static readonly Amazon.RegionEndpoint endpoint = Amazon.RegionEndpoint.USEast1;
        public const string bucket = "";
    }

    internal static class AzureConnectionInfo
    {
        public const string accountName = "";
        public const string sasToken = "";
        public const string container = "";
    }
}
```
If this isn't fully populated with valid credentials, all tests for each unpopulated backend will fail.