using System;
using System.Collections.Generic;
using System.Text;
using HttpProgress;

namespace MStorage
{
    /// <summary>
    /// A progress wrapper which substitutes a known stream length in an ICopyProgress.
    /// </summary>
    class KnownProgress : IProgress<ICopyProgress>
    {
        private readonly IProgress<ICopyProgress> progress;
        private readonly long expectedBytes;

        public KnownProgress(IProgress<ICopyProgress> progress, long expectedBytes)
        {
            this.progress = progress;
            this.expectedBytes = expectedBytes;
        }

        public void Report(ICopyProgress value)
        {
            progress.Report(new CopyProgress(value.TransferTime, value.BytesPerSecond, value.BytesTransfered, expectedBytes));
        }
    }
}
