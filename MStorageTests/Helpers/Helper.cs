using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MStorageTests
{
    public static class Helper
    {
        public static Stream GenerateStream(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            writer.Write(s);
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        public static string ReadStream(Stream s)
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