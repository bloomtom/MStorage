using Microsoft.VisualStudio.TestTools.UnitTesting;
using MStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MStorageTests
{
    public static class TestFunctions
    {
        public static void TestFullCycle(string filename, string fileBody, IStorage s)
        {
            // Upload a file
            using (Stream f = Helper.GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f).Wait();
            }

            // Make sure it exists.
            Assert.IsTrue(s.ListAsync().Result.Contains(filename));

            // Make sure the content is what we expect.
            Assert.AreEqual(fileBody, Helper.ReadStream(s.DownloadAsync(filename).Result));

            // Delete the file
            s.DeleteAsync(filename).Wait();

            // Make sure it's gone.
            Assert.IsFalse(s.ListAsync().Result.Contains(filename));
        }

        public static void TestDoubleUpload(string filename, string fileBody, IStorage s)
        {
            // Upload the same stream twice to the same destination.
            using (Stream f = Helper.GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f).Wait();
                Assert.ThrowsException<ObjectDisposedException>(() => f.Position = 0); // Extra test: Stream expected to be closed.
            }
            // Upload again.
            using (Stream f = Helper.GenerateStream(fileBody))
            {
                s.UploadAsync(filename, f).Wait();
            }

            // Ensure only one file exists.
            Assert.IsTrue(s.ListAsync().Result.Where(x => x == filename).Count() == 1);

            // Ensure the double upload hasn't destroyed the content.
            Assert.AreEqual(fileBody, Helper.ReadStream(s.DownloadAsync(filename).Result));

            // Clean up.
            s.DeleteAsync(filename).Wait();
            Assert.IsFalse(s.ListAsync().Result.Contains(filename));
        }

        public static void TestOverwrite(string filename, string fileBodyA, string fileBodyB, IStorage s)
        {
            // Upload one file.
            using (Stream f = Helper.GenerateStream(fileBodyA))
            {
                s.UploadAsync(filename, f).Wait();
            }
            Assert.AreEqual(fileBodyA, Helper.ReadStream(s.DownloadAsync(filename).Result));

            // Upload different content to the same destination.
            using (Stream f = Helper.GenerateStream(fileBodyB))
            {
                s.UploadAsync(filename, f).Wait();
            }

            // The content should be the last written content.
            Assert.AreEqual(fileBodyB, Helper.ReadStream(s.DownloadAsync(filename).Result));

            // Clean up.
            s.DeleteAsync(filename).Wait();
            Assert.IsFalse(s.ListAsync().Result.Contains(filename));
        }

        public static void TestDownloadNonexistent(IStorage s)
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
            catch(Exception ex)
            {
                Assert.Fail("Expected FileNotFoundException but got " + ex.ToString());
            }
            Assert.Fail("Expected FileNotFoundException but no exception was thrown.");
        }
    }
}
