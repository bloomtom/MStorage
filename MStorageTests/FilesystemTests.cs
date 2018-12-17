using Microsoft.VisualStudio.TestTools.UnitTesting;
using MStorage;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MStorageTests
{
    [TestClass]
    public class FilesystemTests
    {
        private const string testString = "Hello, world!";
        const string rootName = "A";

        private static IStorage GenerateBackend()
        {
            return new MStorage.FilesystemStorage.FilesystemStorage(Path.Join(Environment.CurrentDirectory, rootName), null);
        }

        [TestMethod]
        public void TestFullCycle()
        {
            TestFunctions.TestFullCycle("testA", testString, GenerateBackend());
        }

        [TestMethod]
        public void TestDoubleUpload()
        {
            TestFunctions.TestDoubleUpload("testB", testString, GenerateBackend());
        }

        [TestMethod]
        public void TestOverwrite()
        {
            TestFunctions.TestOverwrite("testC", testString, "This should be a different file body!", GenerateBackend());
            TestFunctions.TestOverwrite("testC", testString, "Shorter", GenerateBackend());
        }
    }
}
