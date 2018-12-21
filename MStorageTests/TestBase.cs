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
            try
            {
                s.DownloadAsync("donotcreate-" + DateTime.Now.Ticks).Wait();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions[0].GetType() == typeof(FileNotFoundException))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected FileNotFoundException but got " + ex.ToString());
            }
            Assert.Fail("Expected FileNotFoundException but no exception was thrown.");
        }

        public abstract void TestDeleteNonexistent();
        protected static void TestDeleteNonexistent(IStorage s)
        {
            try
            {
                s.DeleteAsync("donotcreate-" + DateTime.Now.Ticks).Wait();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerExceptions[0].GetType() == typeof(FileNotFoundException))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Assert.Fail("Expected FileNotFoundException but got " + ex.ToString());
            }
            Assert.Fail("Expected FileNotFoundException but no exception was thrown.");
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
            var transfered = s.ListAsync().Result.ToList();
            transfered.Sort();
            Assert.AreEqual(3, transfered.Count, "After transferring files, expected the destination count to equal the source.");
            Assert.AreEqual("A", transfered[0]);
            Assert.AreEqual("B", transfered[1]);
            Assert.AreEqual("C", transfered[2]);
            Assert.AreEqual(emptyTestString, ReadStream(s.DownloadAsync("A").Result), "Expected transfered file content to equal the source.");

            // Transfer from test store and check result.
            Assert.AreEqual(0, other.ListAsync().Result.Count(), "Expected empty after transfer out with delete set true. Some files may not have transfered correctly.");
            s.TransferAsync(other, false).Wait();
            Assert.AreEqual(3, other.ListAsync().Result.Count(), "Expected destination to have all files after transfering to it.");
            Assert.AreEqual(3, s.ListAsync().Result.Count(), "Expected source to still have all files as transfer was performed with deleteSource set to false.");
            Assert.AreEqual(emptyTestString, ReadStream(other.DownloadAsync("A").Result), "Expected file content to remain the same after transfer back to origin.");
            s.TransferAsync(other, true).Wait();
            Assert.AreEqual(0, s.ListAsync().Result.Count(), "Expected empty store after transfering back with deleteSource set to true.");
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
