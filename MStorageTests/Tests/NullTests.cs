using Microsoft.VisualStudio.TestTools.UnitTesting;
using MStorage;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MStorageTests
{
    [TestClass]
    public class NullTests : TestBase
    {
        new const string testString = "\0\0\0\0\0\0\0\0";

        private static IStorage GenerateBackend()
        {
            return new NullStorage();
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
            TestOverwrite("testC", testString, "\0\0\0\0\0\0\0\0\0\0\0\0\0", GenerateBackend());
            TestOverwrite("testC", testString, "\0\0\0\0", GenerateBackend());
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
