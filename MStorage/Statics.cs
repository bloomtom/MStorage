using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MStorage
{
    internal static class Statics
    {
        public static long ComputeStreamLength(Stream s, long expectedLength)
        {
            return s.CanSeek && expectedLength == 0 ? s.Length : expectedLength;
        }

        public static int ComputeInstantRate(long ticksElapsed, long delta)
        {
            return (int)(delta * TimeSpan.TicksPerSecond / ticksElapsed);
        }
    }
}
