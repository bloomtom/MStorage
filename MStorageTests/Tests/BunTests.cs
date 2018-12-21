using Microsoft.VisualStudio.TestTools.UnitTesting;
using MStorage;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MStorageTests
{
    [TestClass]
    public class BunTests : TestBase
    {
        private static IStorage GenerateBackend()
        {
            return new MStorage.WebStorage.BunnyStorage(BunConnectionInfo.apiKey, BunConnectionInfo.zone);
        }

        [TestMethod]
        public override void TestFullCycle()
        {
            TestFullCycle("testA", testString, GenerateBackend());
        }

        [TestMethod]
        public override void TestDoubleUpload()
        {
            TestDoubleUpload("testB", testString, GenerateBackend());
        }

        [TestMethod]
        public override void TestOverwrite()
        {
            TestOverwrite("testC", testString, "This should be a different file body!", GenerateBackend());
            TestOverwrite("testC", testString, "Shorter", GenerateBackend());
        }

        [TestMethod]
        public override void TestDownloadNonexistent()
        {
            TestDownloadNonexistent(GenerateBackend());
        }

        [TestMethod]
        public override void TestTransfer()
        {
            TestTransfer(GenerateBackend());
        }
    }
}
