using HttpProgress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MStorageTests
{
    public abstract class TestBase
    {
        protected const string testString = "Hello, world!";

        public abstract void TestFullCycle();
        protected static void TestFullCycle(string filename, string fileBody, IStorage s)
        {
            // Upload a file
            using (Stream f = GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f).Wait();
            }

            // Make sure it exists.
            Assert.IsTrue(s.ListAsync().Result.Contains(filename), "File not found to exist after upload.");

            // Make sure the content is what we expect.
            Assert.AreEqual(fileBody, ReadStream(s.DownloadAsync(filename).Result), "Uploaded body is different than what was uploaded.");

            // Delete the file
            s.DeleteAsync(filename).Wait();

            // Make sure it's gone.
            Assert.IsFalse(s.ListAsync().Result.Contains(filename), "Failed to delete test file during cleanup.");
        }

        public abstract void TestDoubleUpload();
        protected static void TestDoubleUpload(string filename, string fileBody, IStorage s)
        {
            // Upload the same stream twice to the same destination.
            using (Stream f = GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f, true).Wait();
                Assert.ThrowsException<ObjectDisposedException>(() => f.Position = 0, "disposeStream was set to true, so it was expected that the stream would be disposed.");
            }
            // Upload again.
            using (Stream f = GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f).Wait();
            }

            // Ensure only one file exists.
            Assert.IsTrue(s.ListAsync().Result.Where(x => x == filename).Count() == 1, "A duplicate file was found after performing an overwrite.");

            // Ensure the double upload hasn't destroyed the content.
            Assert.AreEqual(fileBody, ReadStream(s.DownloadAsync(filename).Result), "File body was mangled by the overwrite.");

            // Clean up.
            s.DeleteAsync(filename).Wait();
            Assert.IsFalse(s.ListAsync().Result.Contains(filename), "Failed to delete test file during cleanup.");
        }

        public abstract void TestOverwrite();
        protected static void TestOverwrite(string filename, string fileBodyA, string fileBodyB, IStorage s)
        {
            // Upload one file.
            using (Stream f = GenerateStream(fileBodyA))
            {
                s.UploadAsync(filename, f).Wait();
            }
            Assert.AreEqual(fileBodyA, ReadStream(s.DownloadAsync(filename).Result), "Readback of initial upload did not equal the uploaded content.");

            // Upload different content to the same destination.
            using (Stream f = GenerateStream(fileBodyB))
            {
                s.UploadAsync(filename, f).Wait();
            }
            // The content should be the last written content.
            Assert.AreEqual(fileBodyB, ReadStream(s.DownloadAsync(filename).Result), "Readback of overwritten file does not exactly equal the desired content.");

            // Clean up.
            s.DeleteAsync(filename).Wait();
            Assert.IsFalse(s.ListAsync().Result.Contains(filename), "Failed to delete test file during cleanup.");
        }

        public abstract void TestDownloadNonexistent();
        protected static void TestDownloadNonexistent(IStorage s)
        {
            AssertException(new Action(() => { s.DownloadAsync("donotcreate-" + DateTime.Now.Ticks).Wait(); }), typeof(FileNotFoundException), "Download");
        }

        public abstract void TestDeleteNonexistent();
        protected static void TestDeleteNonexistent(IStorage s)
        {
            AssertException(new Action(() => { s.DeleteAsync("donotcreate-" + DateTime.Now.Ticks).Wait(); }), typeof(FileNotFoundException), "Delete");
        }

        public abstract void TestTransfer();
        protected static void TestTransfer(IStorage s)
        {
            string emptyTestString = "\0\0\0\0";

            IStorage other = new NullStorage();
            other.UploadAsync("A", GenerateStream(emptyTestString), true);
            other.UploadAsync("B", GenerateStream(testString), true);
            other.UploadAsync("C", GenerateStream(testString), true);

            s.DeleteAllAsync().Wait();
            other.TransferAsync(s, true).Wait();

            // Transfer to test store and check result.
            var transferred = s.ListAsync().Result.ToList();
            transferred.Sort();
            Assert.AreEqual(3, transferred.Count, "After transferring files, expected the destination count to equal the source.");
            Assert.AreEqual("A", transferred[0]);
            Assert.AreEqual("B", transferred[1]);
            Assert.AreEqual("C", transferred[2]);
            Assert.AreEqual(emptyTestString, ReadStream(s.DownloadAsync("A").Result), "Expected transferred file content to equal the source.");

            // Transfer from test store and check result.
            Assert.AreEqual(0, other.ListAsync().Result.Count(), "Expected empty after transfer out with delete set true. Some files may not have transferred correctly.");
            s.TransferAsync(other, false).Wait();
            Assert.AreEqual(3, other.ListAsync().Result.Count(), "Expected destination to have all files after transfering to it.");
            Assert.AreEqual(3, s.ListAsync().Result.Count(), "Expected source to still have all files as transfer was performed with deleteSource set to false.");
            Assert.AreEqual(emptyTestString, ReadStream(other.DownloadAsync("A").Result), "Expected file content to remain the same after transfer back to origin.");
            s.TransferAsync(other, true).Wait();
            Assert.AreEqual(0, s.ListAsync().Result.Count(), "Expected empty store after transfering back with deleteSource set to true.");
        }

        public abstract void TestCancellation();
        protected static void TestCancellation(IStorage s)
        {
            var source = new System.Threading.CancellationTokenSource();
            source.Cancel();

            string filename = "donotcreate-" + DateTime.Now.Ticks;
            AssertException(new Action(() => { s.UploadAsync(filename, GenerateStream(testString), cancel: source.Token).Wait(); }), typeof(System.Threading.Tasks.TaskCanceledException), "Upload");
            AssertException(new Action(() => { s.DownloadAsync(filename, source.Token).Wait(); }), typeof(System.Threading.Tasks.TaskCanceledException), "Download");
            AssertException(new Action(() => { s.DeleteAsync(filename, source.Token).Wait(); }), typeof(System.Threading.Tasks.TaskCanceledException), "Delete");
        }

        public abstract void TestProgress();
        protected static void TestProgress(string filename, long testLength, IStorage s)
        {
            int progressIterations = 0;
            double lastProgress = 0;
            long totalTransferred = 0;
            var progress = new NaiveProgress<ICopyProgress>(new Action<ICopyProgress>(x =>
            {
                progressIterations++;
                Assert.IsTrue(x.PercentComplete > lastProgress, $"Percent complete ({x.PercentComplete.ToString("0.###")}) is the same or less than it was during the last progress event (({lastProgress.ToString("0.###")})).");
                Assert.IsTrue(x.BytesPerSecond > 0, $"Bytes per second was {x.BytesPerSecond}. Expected > 0");
                Assert.AreEqual(testLength, x.ExpectedBytes, $"Expected bytes in progress event ({x.ExpectedBytes}) was not equal to the actual expected value {testLength}");

                lastProgress = x.PercentComplete;
                totalTransferred = x.BytesTransferred;
            }));

            // Test upload
            using (var file = new MemoryStream(new byte[testLength]))
            {
                s.UploadAsync(filename, file, progress: progress).Wait();
                Assert.IsTrue(progressIterations >= 4, $"Expected progress iterations ({progressIterations}) to be >= 4.");
                Assert.AreEqual(testLength, totalTransferred, $"Expected {testLength} but got {totalTransferred}");
                Assert.AreEqual(1d, lastProgress, $"Final progress was not 1 ({lastProgress})");
            }

            // Reset test variables.
            progressIterations = 0;
            lastProgress = 0;
            totalTransferred = 0;

            // Test download
            using (var downloadFile = new MemoryStream())
            {
                s.DownloadAsync(filename, downloadFile, progress).Wait();
                Assert.AreEqual(testLength, downloadFile.Length, $"testLength ({testLength} was not equal to the download file size ({downloadFile.Length}))");
                Assert.IsTrue(progressIterations >= 4, $"Expected progress iterations ({progressIterations}) to be >= 4.");
                Assert.AreEqual(testLength, totalTransferred, $"Expected {testLength} but got {totalTransferred}");
                Assert.AreEqual(1d, lastProgress, $"Final progress was not 1 ({lastProgress})");
            }

            // Delete the file
            s.DeleteAsync(filename).Wait();

            // Make sure it's gone.
            Assert.IsFalse(s.ListAsync().Result.Contains(filename), "Failed to delete test file during cleanup.");
        }

        protected static void AssertException(Action action, Type expectedException, string assertDescription = null)
        {
            Exception exception = null;

            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                exception = ex;
                if (ex is AggregateException agg)
                {
                    if (agg.InnerExceptions[0].GetType() == expectedException)
                    {
                        return;
                    }
                }
                else if (ex.GetType() == expectedException)
                {
                    return;
                }
            }

            assertDescription = string.IsNullOrWhiteSpace(assertDescription) ? "" : assertDescription + ":";
            if (exception != null)
            {
                Assert.Fail($"{assertDescription} Expected exception of type {expectedException.ToString()} but got {exception.ToString()}.");
            }
            Assert.Fail($"{assertDescription} Expected exception of type {expectedException.ToString()} but no exception was thrown.");
        }

        protected static Stream GenerateStream(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.Write(s);
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        protected static string ReadStream(Stream s)
        {
            using (var reader = new StreamReader(s))
            {
                string r = reader.ReadToEnd();
                s.Close();
                return r;
            }
        }
    }
}
