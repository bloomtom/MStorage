using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MStorageTests
{
    class BigStream : Stream
    {
        private readonly long length;
        public BigStream(long length)
        {
            this.length = length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer.Length - offset < count) { throw new IndexOutOfRangeException("buffer not large enough to contain count."); }
            Position = Position + count;
            if (Position > length)
            {
                long diff = Position - length;
                Position = length;
                return (int)(count - diff);
            }
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position = Position + offset;
                    break;
                case SeekOrigin.End:
                    Position = length + offset;
                    break;
                default:
                    break;
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
